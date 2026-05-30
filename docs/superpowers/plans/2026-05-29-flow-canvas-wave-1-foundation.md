# Flow Canvas Wave 1 — Foundation & Safety Net Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land the Wave 1 "Foundation & Safety Net" for the SSH_Helper Flow Canvas: (1) a dark-first OKLCH design-token layer that eliminates every hardcoded hex outside one token module (Decision #4), (2) a global reduced-motion kill switch that persists through C# config (Decision #7), (3) a fix for the never-populated `blockTimings` plus a Run Heatmap overlay, (4) a click-to-fix Problems panel that un-flattens the C# node diagnostics, and (5) connection-validity guards that stop users authoring graph shapes the YAML exporter cannot faithfully serialize (the one authoring-touching piece, parity-verified per Decision #10).

**Architecture:** The Flow Canvas is React 19 + `@xyflow/react` v12 + Zustand 5, built single-file via Vite (`vite-plugin-singlefile`) and embedded as a .NET assembly resource, hosted in an offline WebView2 control inside a WinForms app. C#↔React communication is JSON over `PostWebMessage`/`WebMessageReceived` (`FlowCanvas/src/stores/messageBridge.ts`, `FlowCanvas/src/MessageBus.ts`, `FlowCanvas/src/communication-message-types.ts`; C# `Services/FlowCanvasBridge.cs` + `UI/FlowCanvasForm.cs`). Outbound C#→React posts are queued until React posts `ready` and marshaled to the UI thread. YAML export (`FlowCanvas/src/utils/exportGraph.ts` → `FlowCanvasBridge.ExportToYaml`) serializes `node.data.props` ONLY; `ComputeStructureHash` hashes `_stepPath:blockType` tuples — so every Wave 1 visual/transient change is provably round-trip-safe and only connection-guards touches authoring.

**Tech Stack:** TypeScript/React/Zustand/CSS (OKLCH, WAAPI/CSS animation — no animation lib in Wave 1); C# .NET 8 WinForms + Newtonsoft.Json; Playwright e2e (`FlowCanvas/e2e/*.spec.ts`, run `npm run test:e2e`); xUnit 2.7.0 + FluentAssertions + `Xunit.StaFact` `[WinFormsFact]` (`SSH_Helper.Tests/`); build `dotnet build SSH_Helper.sln`, FlowCanvas build `cd FlowCanvas && npm run build` (tsc && vite build).

---

## File Structure

### New files

| Path | Responsibility |
| --- | --- |
| `FlowCanvas/src/styles/tokens.css` | The authoritative token layer: a `:root { --fc-*: oklch(...) }` block with the ~30 surface/text/accent/state tokens, 7 constant-L/C rotating-H category hues (each with `-border/-bg/-badge/-badge-text/-text`), glow/shadow/radii/type scales, and the specialty tokens (terminal, host, start-node lineage, edge-idle, grid-dot, search-outline, comment chrome). The ONLY place OKLCH/hex literals are allowed. Imported once in `main.tsx`. |
| `FlowCanvas/src/utils/tokens.ts` | Typed mirror for JS consumers: `DEFAULT_COMMENT_COLOR` constant (user-data default, NOT a CSS var), `CATEGORY_VARS` map (category → `{border,bg,badge,badgeText,text}` of `var(--fc-cat-*)` strings) consumed by `registry.ts`, and `resolveCssVar(name)` helper for the SVG minimap fallback. |
| `FlowCanvas/src/styles/reducedMotion.css` | App-global kill switch: `body.fc-reduced-motion *{...}` duration/iteration overrides. Imported once in `main.tsx`. |
| `FlowCanvas/src/utils/connectionRules.ts` | Pure predicate `isConnectionAllowed(connection, nodes, edges): { ok; reason? }` — self-loop, no-edge-into-start, duplicate, per-handle uniqueness (containers keep distinct branches), fan-in rejection, cycle rejection. The single source of truth called by both `isValidConnection` and `onConnect`. |
| `FlowCanvas/src/panels/ProblemsPanel.tsx` | Floating panel (mirrors `DebugPanel.tsx`) listing `uiSlice.diagnostics`; row click `selectNode(nodeId)` + `useReactFlow().setCenter(...)`. Non-clickable rows for null `nodeId`. |
| `FlowCanvas/src/panels/ConnectionNotice.tsx` | Transient toast reading `uiSlice.connectionNotice`, auto-dismiss, reduced-motion aware, token-styled. |
| `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` | Asserts no raw hex in resolved styles (except user comment color), `--fc-*` vars present + OKLCH, category block border resolves to its var. |
| `FlowCanvas/e2e/flow-canvas-reduced-motion.spec.ts` | Toggle adds/removes `body.fc-reduced-motion` + emits `pref-save`; inbound `pref-restore` sets class with NO echo; running node animation-duration collapses. |
| `FlowCanvas/e2e/flow-canvas-run-timing.spec.ts` | Bugfix red/green (duration badge appears after `execution-update`); heatmap tints by relative duration; toggle persistence; export identical with heatmap on/off. |
| `FlowCanvas/e2e/flow-canvas-problems-panel.spec.ts` | Inbound `apply-result` with `diagnostics` opens panel; row click selects + centers `node-2`. |
| `FlowCanvas/e2e/flow-canvas-connection-guards.spec.ts` | Negative+gesture spec: illegal drags rejected, notice shown, graph still exports cleanly; positive container branches unchanged. |
| `SSH_Helper.Tests/UI/Form1FlowCanvasReducedMotionTests.cs` | `[WinFormsFact]` round-trip of `FlowCanvasReducedMotion` through `ConfigurationService` + `SendPersistedLayout` posting `pref-restore`. |
| `SSH_Helper.Tests/UI/FlowCanvasFormLayoutTests.cs` | `[WinFormsFact]` round-trip of `FlowCanvasHeatmapEnabled` via the extended `SavePanelSizes`/`SendPersistedLayout` path. |

### Modified files

| Path | Responsibility / change |
| --- | --- |
| `FlowCanvas/src/main.tsx` | Import `./styles/tokens.css` and `./styles/reducedMotion.css` before `App`. |
| `FlowCanvas/src/utils/theme.ts` | Becomes a thin attribute toggler: `applyTheme()` only sets `data-theme`; values live in `tokens.css`. Keep the 8 legacy `--fc-*` aliases as token references during migration. |
| `FlowCanvas/src/blockDefs/registry.ts` | `categoryColors` reads `CATEGORY_VARS` from `tokens.ts` (var() strings) — single source for `BaseBlock` + minimap. |
| `FlowCanvas/src/nodes/BaseBlock.tsx` | Hex→var() sweep (25 literals); subscribe `heatmapEnabled`; render-time heatmap tint keyed off `blockTimings` (no `node.data` mutation). |
| `FlowCanvas/src/nodes/StartNode.tsx` | Hex→var() sweep (12 literals) using dedicated `--fc-start-*` tokens; assert source-only invariant comment. |
| `FlowCanvas/src/nodes/CommentNode.tsx` | Hex→var() sweep of chrome (8 literals); per-comment `color` stays user data, default via `DEFAULT_COMMENT_COLOR`. |
| `FlowCanvas/src/nodes/AnimatedEdge.tsx` | Hex→var() sweep (6 literals). |
| `FlowCanvas/src/nodes/baseblock.css` | Keyframe colors → vars; search outlines → var; keyframes stay (motion handled by `reducedMotion.css`). |
| `FlowCanvas/src/App.tsx` | Drop dark/light ternaries; read tokens directly (9 literals); add `isValidConnection`; mount `<ProblemsPanel/>` + `<ConnectionNotice/>`; `useEffect` toggling `body.fc-reduced-motion`. |
| `FlowCanvas/src/stores/slices/graphSlice.ts` | Edge `style.stroke`/`labelStyle.fill` hex→var() (24 literals, round-trip-safe); guard `onConnect` with `isConnectionAllowed`. |
| `FlowCanvas/src/stores/slices/commentSlice.ts` | Default comment color → `DEFAULT_COMMENT_COLOR` (1 literal, kept literal — serialized to layout JSON). |
| `FlowCanvas/src/utils/exportGraph.ts` | Comment-color fallback → `DEFAULT_COMMENT_COLOR` (keep byte-identical default). |
| `FlowCanvas/src/panels/*.tsx` | Mechanical hex→var() sweep across Properties (84), Toolbar (27), DebugPanel (21), EdgeContextMenu (25), VariableInspector (19), TimelinePanel (16), SearchOverlay (15), OutputPreview (14), HostBar (6), BlockContextMenu (6), RightPanel (3), Palette (3). Toolbar/Properties also gain new toggles/panels. |
| `FlowCanvas/src/stores/slices/uiSlice.ts` | Add `reducedMotion`, `heatmapEnabled`, `diagnostics`, `connectionNotice` state + setters/restorers; `panelsVisible.problems`. |
| `FlowCanvas/src/stores/messageBridge.ts` | New handlers: `prefRestore`; extend `applyResult` (parse `diagnostics`), `executionUpdate` (the `setBlockTiming` bugfix), `layoutRestore` (heatmap); seed `prefers-reduced-motion` before `sendReady`; expose `isConnectionAllowed` on test hooks. |
| `FlowCanvas/src/communication-message-types.ts` | Add inbound `prefRestore: 'pref-restore'`, outbound `prefSave: 'pref-save'`. |
| `Models/AppConfiguration.cs` | Add `bool? FlowCanvasReducedMotion` and `bool? FlowCanvasHeatmapEnabled` to `WindowState`. |
| `UI/FlowCanvasForm.cs` | `pref-save` case + `SaveReducedMotionPref`; extend `SendPersistedLayout` (post `pref-restore` + heatmap); extend `SavePanelSizes` (heatmap). |
| `Form1.cs` | Thread `exportResult.Diagnostics` into `SendFlowCanvasApplyResult`; emit `diagnostics[]` on `apply-result`. |
| `Services/FlowCanvasBridge.cs` | No structural change; audit every `FlowCanvasExportDiagnostic` carries a `NodeId` where node-clickable. |

**Verified round-trip facts (do not re-derive):** `exportGraph.ts` serializes `node.data.props` + raw edges + comments + disabled list; `FlowCanvasBridge.ExportToYaml` reads `props`/`blockType`/`edge.data.branchPath`/`sourceHandle` only — never `edge.style.stroke`, `labelStyle.fill`, or comment `color`; comment nodes are skipped on export. `ComputeStructureHash` hashes only `_stepPath:blockType`. The ONLY persistence-touching color is `comment.color` (layout-autosave JSON / `CanvasComment.Color`, C# default `#e0c040`) — keep its literal default byte-identical across `commentSlice.ts`, `CommentNode.tsx`, `exportGraph.ts`. New prefs persist via `AppConfiguration.WindowState`, never WebView2 localStorage (Decision #7).

---

## Section 1: Token Forge [PREREQUISITE]

> Establishes the token layer first; every later section consumes `var(--fc-*)`. Per CLAUDE.md sub-agent swarming, the panel sweep (Task 5) splits across parallel sub-agents (5-6 files each). A pure CSS-var sweep cannot be strict-TDD-first, so each task is gated by `npm run build` clean + the no-raw-hex Playwright scan + unchanged parity specs.

### Task 1: Author the token layer (`tokens.css` + `tokens.ts`)

**Files:**
- Create `FlowCanvas/src/styles/tokens.css`
- Create `FlowCanvas/src/utils/tokens.ts`
- Modify `FlowCanvas/src/main.tsx` (add import, top-of-file)
- Test: `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` (created in Step 6)

- [ ] **Step 1: Write the full `tokens.css`.** Author every token (no placeholders). OKLCH values are starting points tuned to the existing hex; rounding is acceptable (visual-only).

```css
/* FlowCanvas/src/styles/tokens.css
 * Single source of truth for Flow Canvas design tokens (Decision #3/#4: OKLCH, dark-first,
 * no hex outside this file). A future light/high-contrast theme is a [data-theme] override here. */
:root {
  /* Surface ladder */
  --fc-surface-0: oklch(18% 0.03 275);
  --fc-surface-1: oklch(22% 0.035 275);
  --fc-surface-2: oklch(26% 0.04 275);
  --fc-surface-3: oklch(30% 0.045 275);
  --fc-surface-disabled: oklch(24% 0 0);
  --fc-canvas-bg: var(--fc-surface-1);

  /* Borders */
  --fc-border: oklch(34% 0.04 275);
  --fc-border-subtle: oklch(40% 0.02 275);
  --fc-border-muted: oklch(45% 0.01 275);
  --fc-border-selected: oklch(98% 0 0);

  /* Text ramp */
  --fc-text: oklch(85% 0.01 275);
  --fc-text-secondary: oklch(72% 0.01 275);
  --fc-text-muted: oklch(60% 0.01 275);
  --fc-text-faint: oklch(48% 0.01 275);
  --fc-text-disabled: oklch(40% 0.01 275);

  /* Accent rail (indigo/azure — continues #4a9eff running/active signal) */
  --fc-accent: oklch(68% 0.16 255);
  --fc-accent-strong: oklch(62% 0.18 258);
  --fc-accent-surface: oklch(30% 0.07 255);
  --fc-accent-text: oklch(88% 0.06 255);
  --fc-on-accent: oklch(15% 0 0);

  /* State colors */
  --fc-state-success: oklch(72% 0.17 150);
  --fc-state-error: oklch(60% 0.20 25);
  --fc-state-error-text: var(--fc-state-error);
  --fc-state-warning: oklch(82% 0.15 90);
  --fc-state-running: var(--fc-accent);
  --fc-state-skipped: var(--fc-text-muted);

  /* 7 category hues — constant L 62% / C 0.13, rotating H */
  --fc-cat-ssh-border: oklch(62% 0.13 255);
  --fc-cat-ssh-bg: oklch(20% 0.06 255);
  --fc-cat-ssh-badge: var(--fc-cat-ssh-border);
  --fc-cat-ssh-badge-text: var(--fc-on-accent);
  --fc-cat-ssh-text: oklch(78% 0.07 255);

  --fc-cat-control-flow-border: oklch(62% 0.13 90);
  --fc-cat-control-flow-bg: oklch(20% 0.06 90);
  --fc-cat-control-flow-badge: var(--fc-cat-control-flow-border);
  --fc-cat-control-flow-badge-text: var(--fc-on-accent);
  --fc-cat-control-flow-text: oklch(78% 0.07 90);

  --fc-cat-data-border: oklch(62% 0.13 310);
  --fc-cat-data-bg: oklch(20% 0.06 310);
  --fc-cat-data-badge: var(--fc-cat-data-border);
  --fc-cat-data-badge-text: var(--fc-text);
  --fc-cat-data-text: oklch(78% 0.07 310);

  --fc-cat-network-border: oklch(62% 0.13 175);
  --fc-cat-network-bg: oklch(20% 0.06 175);
  --fc-cat-network-badge: var(--fc-cat-network-border);
  --fc-cat-network-badge-text: var(--fc-on-accent);
  --fc-cat-network-text: oklch(78% 0.07 175);

  --fc-cat-io-border: oklch(62% 0.13 55);
  --fc-cat-io-bg: oklch(20% 0.06 55);
  --fc-cat-io-badge: var(--fc-cat-io-border);
  --fc-cat-io-badge-text: var(--fc-text);
  --fc-cat-io-text: oklch(78% 0.07 55);

  --fc-cat-grid-border: oklch(62% 0.13 235);
  --fc-cat-grid-bg: oklch(20% 0.06 235);
  --fc-cat-grid-badge: var(--fc-cat-grid-border);
  --fc-cat-grid-badge-text: var(--fc-text);
  --fc-cat-grid-text: oklch(78% 0.07 235);

  --fc-cat-timing-border: oklch(62% 0.03 275);
  --fc-cat-timing-bg: oklch(20% 0.02 275);
  --fc-cat-timing-badge: var(--fc-cat-timing-border);
  --fc-cat-timing-badge-text: var(--fc-on-accent);
  --fc-cat-timing-text: oklch(78% 0.02 275);

  /* Glow / shadow scale */
  --fc-glow-running: oklch(68% 0.16 255 / 0.4);
  --fc-glow-running-min: oklch(68% 0.16 255 / 0.3);
  --fc-glow-running-max: oklch(68% 0.16 255 / 0.6);
  --fc-glow-success: oklch(72% 0.17 150 / 0.3);
  --fc-glow-error: oklch(60% 0.20 25 / 0.3);
  --fc-glow-skipped: oklch(60% 0.01 275 / 0.2);
  --fc-glow-disabled: oklch(45% 0 0 / 0.2);
  --fc-glow-selected: oklch(98% 0 0 / 0.15);
  --fc-glow-start: oklch(72% 0.17 150 / 0.15);
  --fc-shadow-sm: 0 2px 8px oklch(0% 0 0 / 0.2);
  --fc-overlay-scrim: oklch(0% 0 0 / 0.6);

  /* Radii / type scale */
  --fc-radius-sm: 3px;
  --fc-radius-md: 6px;
  --fc-radius-lg: 8px;
  --fc-font-mono: ui-monospace, "Cascadia Code", "Consolas", monospace;
  --fc-fs-badge: 10px;
  --fc-fs-body: 12px;
  --fc-fs-header: 13px;

  /* Specialty (not in generic ladder) */
  --fc-term-bg: oklch(16% 0.01 255);
  --fc-term-surface: oklch(22% 0.01 255);
  --fc-term-surface-2: oklch(26% 0.01 255);
  --fc-term-text: oklch(85% 0.02 255);
  --fc-host-bg: oklch(20% 0.05 255);
  --fc-host-surface: oklch(92% 0.02 255);
  --fc-host-accent: oklch(72% 0.13 165);
  --fc-host-accent-strong: oklch(56% 0.13 160);
  --fc-start-grad-from: oklch(30% 0.08 155);
  --fc-start-grad-to: oklch(22% 0.07 155);
  --fc-start-accent: oklch(72% 0.17 150);
  --fc-start-badge-text: var(--fc-on-accent);
  --fc-start-chip-bg: oklch(72% 0.17 150 / 0.1);
  --fc-start-chip-border: oklch(72% 0.17 150 / 0.25);
  --fc-start-chip-text: oklch(80% 0.1 150);
  --fc-edge-idle: oklch(48% 0.01 275);
  --fc-grid-dot: oklch(34% 0.04 275);
  --fc-search-outline: oklch(82% 0.15 90);
  --fc-comment-ink: oklch(20% 0 0);
  --fc-comment-field-bg: oklch(100% 0 0 / 0.2);
  --fc-comment-default-fallback: #e0c040; /* mirror only — do NOT consume as authored color */

  /* Heatmap */
  --fc-heat-cold: var(--fc-accent);
  --fc-heat-mid: oklch(78% 0.13 90);
  --fc-heat-hot: oklch(62% 0.2 35);
  --fc-heat-ring-alpha: 0.55;

  /* Diagnostics */
  --fc-diag-error: var(--fc-state-error);
  --fc-diag-warning: var(--fc-state-warning);
  --fc-diag-row-bg: var(--fc-surface-1);
  --fc-diag-row-hover-bg: var(--fc-surface-2);

  /* Connection guard feedback */
  --fc-connection-invalid: var(--fc-state-error);
  --fc-notice-bg: var(--fc-surface-2);
  --fc-notice-fg: var(--fc-text);
  --fc-notice-border: var(--fc-state-error);

  /* Theme aliases consumed by existing panels via var(--fc-*). These KEBAB-CASE names are
     exactly what applyTheme used to emit and what panels (DebugPanel, OutputPreview, Properties,
     etc.) still reference directly — VERIFIED in-tree — so they MUST stay defined here after
     theme.ts is slimmed. (--fc-canvas-bg / --fc-text / --fc-text-secondary / --fc-text-muted are
     already defined above; --fc-button-bg was referenced by Properties.tsx but never defined by
     the old applyTheme — define it here to fix that latent gap.) */
  --fc-panel-bg: var(--fc-surface-0);
  --fc-panel-border: var(--fc-border);
  --fc-header-bg: var(--fc-surface-1);
  --fc-input-bg: oklch(15% 0.02 255);
  --fc-button-bg: var(--fc-surface-2);
}
```

- [ ] **Step 2: Write `tokens.ts`.** Typed mirror for JS consumers (minimap fallback, comment default, category map). Full code:

```ts
// FlowCanvas/src/utils/tokens.ts
import type { BlockCategory } from '../blockDefs/registry';

/** Authored default for a new comment. Serialized to layout-autosave JSON and to C# CanvasComment.Color,
 *  so this MUST stay byte-identical to the C# default and MUST NOT be routed through a CSS var. */
export const DEFAULT_COMMENT_COLOR = '#e0c040';

export interface CategoryVarSet {
  border: string;
  bg: string;
  badge: string;
  badgeText: string;
  text: string;
}

/** Category colors as CSS var() strings. CSS custom properties resolve inside React inline styles
 *  and inside SVG fill, so this is the single source consumed by BaseBlock and the minimap. */
export const CATEGORY_VARS: Record<BlockCategory, CategoryVarSet> = {
  ssh: catVars('ssh'),
  'control-flow': catVars('control-flow'),
  data: catVars('data'),
  network: catVars('network'),
  io: catVars('io'),
  grid: catVars('grid'),
  timing: catVars('timing'),
};

function catVars(c: string): CategoryVarSet {
  return {
    border: `var(--fc-cat-${c}-border)`,
    bg: `var(--fc-cat-${c}-bg)`,
    badge: `var(--fc-cat-${c}-badge)`,
    badgeText: `var(--fc-cat-${c}-badge-text)`,
    text: `var(--fc-cat-${c}-text)`,
  };
}

/** Resolve a `var(--fc-*)` reference (or bare token name) to a concrete color.
 *  Used only where a raw color string is required (SVG MiniMap nodeColor/maskColor). */
export function resolveCssVar(varRef: string, fallback = '#4a9eff'): string {
  if (typeof window === 'undefined') return fallback;
  const name = varRef.replace(/^var\(\s*/, '').replace(/\s*\)$/, '').split(',')[0].trim();
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}
```

- [ ] **Step 3: Import the token CSS in `main.tsx`** before `App` (and before any component CSS), so `:root` vars exist on first paint:

```ts
import './styles/tokens.css';
import './styles/reducedMotion.css'; // created in Section 2; safe to import an empty file now
```

  If `reducedMotion.css` does not exist yet, create it as an empty file with a header comment so this import resolves; Section 2 fills it.

- [ ] **Step 4: Slim `theme.ts` to an attribute toggler.** Token values now live in `tokens.css`. Replace the body of `applyTheme` so it only sets `data-theme` (canvas ships dark-only; the function stays as the future light/HC swap point). Keep the export signature. Delete the `themes` record and `ThemeColors` interface only after confirming no other importer reads them (grep `from './utils/theme'` / `from '../utils/theme'`); if any consumer imports `themes`, leave the record but stop calling `setProperty` (the CSS file is authoritative). Full replacement:

```ts
// FlowCanvas/src/utils/theme.ts
/** Token VALUES live in styles/tokens.css. This only flips the data-theme attribute,
 *  which is the future light/high-contrast swap point (Decision #4). */
export function applyTheme(theme: 'dark' | 'light'): void {
  document.documentElement.setAttribute('data-theme', theme);
}
```

- [ ] **Step 5: Point `registry.ts` categoryColors at the token map.** Replace the 7×5 hex matrix with the imported `CATEGORY_VARS`:

```ts
import { CATEGORY_VARS } from '../utils/tokens';
// ...
export const categoryColors = CATEGORY_VARS;
```

  Confirm the exported type still satisfies every consumer (`BaseBlock.tsx` line 41, `App.tsx` minimap line 347). `CategoryVarSet` field names match the old `{border,bg,badge,badgeText,text}` shape exactly.

- [ ] **Step 6: Write the token-sweep guard spec** `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts`. Mirror the `beforeEach` of `flow-canvas-parity.spec.ts` (install capture → goto('/') → waitForOutgoingMessage('ready') → clearOutgoingMessages). Assertions: (a) `getComputedStyle(:root).getPropertyValue('--fc-accent')` is non-empty and contains `oklch`; (b) load a fixture with one `ssh`-category `send` block and assert its rendered border resolves to the ssh category color (read via `getComputedStyle` on the `.react-flow__node` border); (c) scan the page for raw hex in `style` attributes excluding any element whose color equals `DEFAULT_COMMENT_COLOR`, asserting the count is 0 — the CI no-raw-hex gate. Use `loadGraphFixture` + a 1-node fixture from `fixtures/graphs.ts`. **Ordering:** put assertion (c) in its OWN test named `no raw hex outside the token layer` and mark it `test.skip(...)` for now — the sweep is incomplete until Task 5, so this case is intentionally disabled until Task 5 Step 4 un-skips it. Tests (a) and (b) run green from Task 1 onward.

- [ ] **Step 7: Verify.** Run and confirm:
  - `cd FlowCanvas && npm run build` → exits 0 (tsc + vite clean).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → all green.
  - `cd FlowCanvas && npm run test:e2e:parity` → unchanged green (proves the token layer did not perturb export).

- [ ] **Step 8: Commit.** `feat(flow-canvas): add OKLCH token layer (tokens.css/tokens.ts), slim theme.ts, point categoryColors at tokens`.

### Task 2: Sweep the node files (BaseBlock, StartNode, CommentNode, AnimatedEdge, baseblock.css)

**Files:**
- Modify `FlowCanvas/src/nodes/BaseBlock.tsx` (25 literals; heatmap deferred to Section 3)
- Modify `FlowCanvas/src/nodes/StartNode.tsx` (12 literals)
- Modify `FlowCanvas/src/nodes/CommentNode.tsx` (8 literals — chrome only)
- Modify `FlowCanvas/src/nodes/AnimatedEdge.tsx` (6 literals)
- Modify `FlowCanvas/src/nodes/baseblock.css` (4 literals)
- Modify `FlowCanvas/src/stores/slices/commentSlice.ts` (1 literal) + `FlowCanvas/src/utils/exportGraph.ts` (comment-color fallback)
- Test: `flow-canvas-token-sweep.spec.ts`, `flow-canvas-gesture-smoke.spec.ts`, parity specs

- [ ] **Step 1: BaseBlock.tsx replacement table.** Apply each exact `old → new`:

  | Old | New |
  | --- | --- |
  | `execGlowColors.running 'rgba(74,158,255,0.4)'` | `'var(--fc-glow-running)'` |
  | `execGlowColors.success 'rgba(46,204,113,0.3)'` | `'var(--fc-glow-success)'` |
  | `execGlowColors.error 'rgba(231,76,60,0.3)'` | `'var(--fc-glow-error)'` |
  | `execGlowColors.skipped 'rgba(150,150,150,0.2)'` | `'var(--fc-glow-skipped)'` |
  | `execGlowColors.disabled 'rgba(100,100,100,0.2)'` | `'var(--fc-glow-disabled)'` |
  | unknown-block `color:'#e74c3c'` | `color: 'var(--fc-state-error-text)'` |
  | disabled bg `'#2a2a2a'` | `'var(--fc-surface-disabled)'` |
  | selected border `'#fff'` | `'var(--fc-border-selected)'` |
  | disabled border `'#555'` | `'var(--fc-border-muted)'` |
  | boxShadow whites `rgba(255,255,255,0.15)` | `'var(--fc-glow-selected)'` |
  | false-handle / breakpoint `'#e74c3c'` | `'var(--fc-state-error)'` |
  | continue-handle `'#4a9eff'` | `'var(--fc-accent)'` |
  | breakpoint-off border `'#444'` | `'var(--fc-border-subtle)'` |
  | text `'#ccc'` | `'var(--fc-text)'` |
  | muted `'#666'` | `'var(--fc-text-faint)'` |
  | faint `'#555'` | `'var(--fc-text-disabled)'` |
  | exec-indicator `'#4a9eff'/'#2ecc71'/'#888'/'#e74c3c'` | `'var(--fc-accent)'/'var(--fc-state-success)'/'var(--fc-text-secondary)'/'var(--fc-state-error)'` |
  | duration-badge bg `'#1a1a2e'` | `'var(--fc-surface-0)'` |

- [ ] **Step 2: StartNode.tsx replacement table.**

  | Old | New |
  | --- | --- |
  | `linear-gradient(135deg, #1a3a2a, #0d2a1a)` | `linear-gradient(135deg, var(--fc-start-grad-from), var(--fc-start-grad-to))` |
  | selected border `'#fff'` | `'var(--fc-border-selected)'` |
  | accent border `'#2ecc71'` | `'var(--fc-start-accent)'` |
  | glow `rgba(255,255,255,0.15)` | `'var(--fc-glow-selected)'` |
  | glow `rgba(46,204,113,0.15)` | `'var(--fc-glow-start)'` |
  | START badge bg `'#2ecc71'` | `'var(--fc-start-accent)'` |
  | START badge text `'#000'` | `'var(--fc-start-badge-text)'` |
  | script name `'#ccc'` | `'var(--fc-text)'` |
  | chip bg `rgba(46,204,113,0.1)` | `'var(--fc-start-chip-bg)'` |
  | chip border `rgba(46,204,113,0.25)` | `'var(--fc-start-chip-border)'` |
  | chip text `'#80d4a0'` | `'var(--fc-start-chip-text)'` |
  | source handle `'#2ecc71'` | `'var(--fc-start-accent)'` |

  Add a comment at the `Handle` (no target handle): `// Invariant: Start is source-only. Adding a target handle is caught by flow-canvas-connection-guards.spec.ts.`

- [ ] **Step 3: CommentNode.tsx replacement table.** Per-comment `color` (user data) stays; default fallback uses the shared constant; chrome tokenized:

  ```ts
  import { DEFAULT_COMMENT_COLOR } from '../utils/tokens';
  // const color = commentData.color || '#e0c040';
  const color = commentData.color || DEFAULT_COMMENT_COLOR;
  ```

  | Old | New |
  | --- | --- |
  | delete-btn bg `rgba(0,0,0,0.2)` | `'var(--fc-overlay-scrim)'` |
  | delete-btn icon `'#333'` | `'var(--fc-comment-ink)'` |
  | boxShadow `rgba(0,0,0,0.2)` | `'var(--fc-shadow-sm)'` |
  | textarea bg `rgba(255,255,255,0.2)` | `'var(--fc-comment-field-bg)'` |
  | textarea ink `'#1a1a1a'` | `'var(--fc-comment-ink)'` |
  | display ink `'#1a1a1a'` | `'var(--fc-comment-ink)'` |

  Leave `background: \`${color}cc\`` exactly as-is (user data + alpha suffix).

- [ ] **Step 4: AnimatedEdge.tsx replacement table.**

  | Old | New |
  | --- | --- |
  | `stateColors.success '#2ecc71'` | `'var(--fc-state-success)'` |
  | `stateColors.running '#4a9eff'` | `'var(--fc-accent)'` |
  | `stateColors.error '#e74c3c'` | `'var(--fc-state-error)'` |
  | idle/fallback `'#666'` (3×) | `'var(--fc-edge-idle)'` |

- [ ] **Step 5: baseblock.css replacement.**

```css
@keyframes exec-pulse {
  0%, 100% { box-shadow: 0 0 8px var(--fc-glow-running-min); }
  50% { box-shadow: 0 0 24px var(--fc-glow-running-max); }
}
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.search-match { outline: 2px dashed var(--fc-search-outline) !important; outline-offset: 2px; }
.search-current { outline: 2px solid var(--fc-search-outline) !important; outline-offset: 2px; }
```

- [ ] **Step 6: commentSlice.ts + exportGraph.ts default.** In `commentSlice.ts` line ~25 replace `color: '#e0c040'` with `color: DEFAULT_COMMENT_COLOR` (import from `../../utils/tokens`). In `exportGraph.ts` replace the comment-color fallback literal with `DEFAULT_COMMENT_COLOR`. These three (commentSlice, CommentNode, exportGraph) MUST share one constant so layout JSON stays byte-identical.

- [ ] **Step 7: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts e2e/flow-canvas-gesture-smoke.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (comment-color default unchanged → layout JSON byte-identical).

- [ ] **Step 8: Commit.** `feat(flow-canvas): tokenize node + edge files; share DEFAULT_COMMENT_COLOR`.

### Task 3: Sweep App.tsx + graphSlice edge colors

**Files:**
- Modify `FlowCanvas/src/App.tsx` (lines ~280-301, 336-351; 9 literals)
- Modify `FlowCanvas/src/stores/slices/graphSlice.ts` (lines ~131, 137-218, 320-327; 24 literals — VISUAL ONLY)
- Test: `flow-canvas-token-sweep.spec.ts`, parity specs

- [ ] **Step 1: App.tsx — drop dark/light ternaries (dark-only, Decision #4).** Replace the branch block:

```ts
// Canvas ships dark-only; values come from the token layer (styles/tokens.css).
const canvasBg = 'var(--fc-canvas-bg)';
const controlsBg = 'var(--fc-surface-1)';
const controlsBorder = 'var(--fc-border)';
const minimapBg = 'var(--fc-surface-0)';
const minimapMask = 'var(--fc-overlay-scrim)';
const dotColor = 'var(--fc-grid-dot)';
const selectedStroke = 'var(--fc-accent)';
```

  `defaultEdgeOptions.stroke '#555'` → `'var(--fc-edge-idle)'`.

- [ ] **Step 2: App.tsx minimap (SVG) — resolve vars once (risk mitigation).** SVG `nodeColor`/`maskColor` may not accept `var()` in WebView2 SVG presentation context. Resolve to concrete colors once, memoized:

```ts
import { resolveCssVar } from './utils/tokens';
import { categoryColors } from './blockDefs/registry';
// ...inside FlowCanvasInner:
const minimapColors = useMemo(() => ({
  mask: resolveCssVar('var(--fc-overlay-scrim)', 'rgba(0,0,0,0.6)'),
  bg: resolveCssVar('var(--fc-surface-0)', '#12122a'),
  fallback: resolveCssVar('var(--fc-accent)', '#4a9eff'),
  byCategory: Object.fromEntries(
    (Object.keys(categoryColors) as Array<keyof typeof categoryColors>)
      .map((k) => [k, resolveCssVar(categoryColors[k].border, '#4a9eff')]),
  ),
}), []);
```

  In `<MiniMap maskColor={minimapColors.mask} ... nodeColor={(n) => minimapColors.byCategory[<category-of-n>] ?? minimapColors.fallback} />`. Keep `<Background color={dotColor} />` and `<Controls />` using the var() strings (CSS context accepts var()).

- [ ] **Step 3: graphSlice.ts edge-color sweep (round-trip-safe — these set `edge.style.stroke`/`labelStyle.fill`, never serialized).** Apply globally within the `onConnect` + branch-metadata helpers:

  | Old literal | New |
  | --- | --- |
  | non-container / default `'#666'` | `'var(--fc-edge-idle)'` |
  | then / do / success-green `'#2ecc71'` | `'var(--fc-state-success)'` |
  | else / catch / default-red `'#e74c3c'` | `'var(--fc-state-error)'` |
  | elif / case / loop-amber `'#f0c040'` | `'var(--fc-state-warning)'` |
  | finally / continuation-blue `'#4a9eff'` | `'var(--fc-accent)'` |
  | parallel teal `'#1abc9c'` | `'var(--fc-cat-network-border)'` |

  **Keep the literal→token mapping identical to the C# branch colors in `FlowCanvasBridge.cs` (lines ~406, 494, 673, 1003).** Migrating the C# literals to the same `var()` strings is OPTIONAL (cosmetic; they feed React which resolves vars) — see micro-decisions. If migrating C#, do it now in this commit so the mapping is documented in one place.

- [ ] **Step 4: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts e2e/flow-canvas-gesture-smoke.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (edge colors are visual-only; export unchanged).

- [ ] **Step 5: Commit.** `feat(flow-canvas): tokenize App.tsx canvas chrome + graphSlice edge colors (dark-only)`.

### Task 4: Sweep panel batch A (Properties, Toolbar, DebugPanel, EdgeContextMenu) — parallel sub-agent

**Files:**
- Modify `FlowCanvas/src/panels/Properties.tsx` (84), `Toolbar.tsx` (27), `DebugPanel.tsx` (21), `EdgeContextMenu.tsx` (25)
- Test: `flow-canvas-token-sweep.spec.ts`, `flow-canvas-properties-typing.spec.ts`, parity specs

> Per CLAUDE.md swarming, run Tasks 4 and 5 as parallel sub-agents (5-6 files each). Properties.tsx alone is ~24% of the sweep — plan it as its own focus within this task.

- [ ] **Step 1: Apply the canonical token map** to every literal in all four files (this is the recurring vocabulary — apply mechanically):

  | Old | New |
  | --- | --- |
  | `#1a1a2e` / `#222244` | `var(--fc-surface-1)` |
  | `#12122a` / `#16162a` | `var(--fc-surface-0)` |
  | `#141b2c` | `var(--fc-surface-2)` |
  | panel border `#2a2a4a` | `var(--fc-border)` |
  | input bg `#0d1117` | `var(--fc-input-bg)` (existing kebab alias) |
  | `#ccc` | `var(--fc-text)` |
  | `#aaa` | `var(--fc-text-secondary)` |
  | `#888` | `var(--fc-text-muted)` |
  | `#666` | `var(--fc-text-faint)` |
  | `#555` | `var(--fc-text-disabled)` |
  | accent `#4a9eff` | `var(--fc-accent)` |
  | `#e74c3c` | `var(--fc-state-error)` |
  | `#2ecc71` | `var(--fc-state-success)` |
  | `#f0c040` / `#e0c040` / `#f1c40f` | `var(--fc-state-warning)` |
  | `#9b59b6` | `var(--fc-cat-data-border)` |
  | start chips `#0d2a1a` | `var(--fc-start-grad-to)`; `#2ecc71` start → `var(--fc-start-accent)` |
  | selected/active chips `#1d2f4a` | `var(--fc-accent-surface)` |
  | `#d4e6ff` | `var(--fc-accent-text)` |
  | `#000` (on-accent) | `var(--fc-on-accent)` |
  | `#fff` | `var(--fc-border-selected)` |

- [ ] **Step 2: Verify the sub-agent's batch.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts e2e/flow-canvas-properties-typing.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 3: Commit.** `feat(flow-canvas): tokenize panel batch A (Properties/Toolbar/DebugPanel/EdgeContextMenu)`.

### Task 5: Sweep panel batch B (VariableInspector, TimelinePanel, SearchOverlay, OutputPreview, HostBar, BlockContextMenu, RightPanel, Palette) — parallel sub-agent

**Files:**
- Modify the 8 panels above (19/16/15/14/6/6/3/3 literals)
- Test: `flow-canvas-token-sweep.spec.ts`, `flow-canvas-variable-inspector.spec.ts`, parity specs

- [ ] **Step 1: Apply the canonical token map** (same table as Task 4) to the generic surface/text/accent/state literals.

- [ ] **Step 2: Apply specialty tokens** where the generic ladder is wrong:
  - OutputPreview terminal: `#0d1117`→`var(--fc-term-bg)`, `#161b22`→`var(--fc-term-surface)`, `#21262d`→`var(--fc-term-surface-2)`, `#c9d1d9`→`var(--fc-term-text)`; `--fc-font-mono` for monospace text.
  - HostBar: `#0f1a2e`→`var(--fc-host-bg)`, `#e8ecf4`→`var(--fc-host-surface)`, `#4ecca3`→`var(--fc-host-accent)`, `#1a8a5a`→`var(--fc-host-accent-strong)`.
  - Toolbar run/stop tints `#e74c3c44`/`#2ecc7144`/`#2a1a1a`/`#1a2a1a` → state-tinted surfaces (use `var(--fc-glow-error)`/`var(--fc-glow-success)` for the 44-alpha tints; `var(--fc-surface-disabled)` family for the solid tints) — note Toolbar's primary sweep is in Task 4; if any tints remain here, apply them.

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts e2e/flow-canvas-variable-inspector.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 4: Final no-hex gate.** Un-skip the `no raw hex outside the token layer` test in `flow-canvas-token-sweep.spec.ts` (remove the `test.skip` added in Task 1) and run it across a fixture exercising all panels; confirm the count is 0 (the only allowed literal is a user comment color = `DEFAULT_COMMENT_COLOR`). If any ghost remains, fix it (Decision #4 is a hard gate).

- [ ] **Step 5: Commit.** `feat(flow-canvas): tokenize panel batch B; complete hex sweep (Decision #4)`.

---

## Section 2: Reduced-Motion Kill Switch

> Single body-level CSS kill switch; `uiSlice` boolean + Toolbar toggle seeded from `prefers-reduced-motion` but explicit toggle is load-bearing; round-trips through C# `AppConfiguration` exactly like panel sizes. Fully transient — cannot alter exported YAML.

### Task 6: Author the kill switch + state + Toolbar toggle

**Files:**
- Modify/Create `FlowCanvas/src/styles/reducedMotion.css` (fill the file created in Section 1)
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts`
- Modify `FlowCanvas/src/communication-message-types.ts`
- Modify `FlowCanvas/src/stores/messageBridge.ts`
- Modify `FlowCanvas/src/App.tsx` (useEffect toggling body class)
- Modify `FlowCanvas/src/panels/Toolbar.tsx`
- Test: `FlowCanvas/e2e/flow-canvas-reduced-motion.spec.ts`

- [ ] **Step 1: Write `reducedMotion.css`.** Full content:

```css
/* FlowCanvas/src/styles/reducedMotion.css
 * Global motion kill switch. The body-level class blankets ALL CSS @keyframes
 * (exec-pulse, spin, marchingAnts, the two inline pulse keyframes) and inline
 * transition:/animation: styles because the !important duration override wins.
 * 0.001ms (not 0) keeps animationend/transitionend events firing. */
body.fc-reduced-motion *,
body.fc-reduced-motion *::before,
body.fc-reduced-motion *::after {
  animation-duration: 0.001ms !important;
  animation-iteration-count: 1 !important;
  transition-duration: 0.001ms !important;
  scroll-behavior: auto !important;
}
```

- [ ] **Step 2: Add bridge message types.** In `communication-message-types.ts`: add `prefRestore: 'pref-restore'` to `incoming`, `prefSave: 'pref-save'` to `outgoing`.

- [ ] **Step 3: Extend `uiSlice`.** Add to the interface: `reducedMotion: boolean; setReducedMotion: (value: boolean) => void; toggleReducedMotion: () => void; restoreReducedMotion: (value: boolean) => void;`. Initial state `reducedMotion: false`. Implementations (mirror the `setPanelSize`/`restorePanelSizes` split — the user-action setter posts to host, the restore setter does NOT, preventing echo loops):

```ts
setReducedMotion: (value) => {
  messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.prefSave, reducedMotion: value });
  set({ reducedMotion: value });
},
toggleReducedMotion: () => get().setReducedMotion(!get().reducedMotion),
restoreReducedMotion: (value) => set({ reducedMotion: value }), // host-driven, no echo
```

- [ ] **Step 4: Add the inbound handler + OS seed in `messageBridge.ts`.** Add a sibling to the `layoutRestore` handler (no echo):

```ts
messageBus.on(CANVAS_HOST_MESSAGES.incoming.prefRestore, (msg) => {
  if (typeof msg.reducedMotion === 'boolean') {
    store.getState().restoreReducedMotion(msg.reducedMotion);
  }
}),
```

  Immediately BEFORE `messageBus.sendReady()` (so the body class is correct on first paint; host `pref-restore` arrives after `ready` and overrides this seed — satisfying "auto-detect but explicit toggle is load-bearing"):

```ts
const prefersReduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
if (prefersReduced) store.getState().restoreReducedMotion(true);
```

- [ ] **Step 5: Toggle the body class in `App.tsx`** via a `useEffect` keyed on `reducedMotion` (keeps the slice pure/SSR-safe; mirrors the existing `applyTheme` effect):

```ts
const reducedMotion = useFlowStore((s) => s.reducedMotion);
useEffect(() => {
  document.body.classList.toggle('fc-reduced-motion', reducedMotion);
}, [reducedMotion]);
```

- [ ] **Step 6: Add the Toolbar toggle.** Selectors `const reducedMotion = useFlowStore((s) => s.reducedMotion); const toggleReducedMotion = useFlowStore((s) => s.toggleReducedMotion);`. Button in the Canvas-controls cluster after snap-to-grid, mirroring `btnStyle`:

```tsx
<button
  onClick={toggleReducedMotion}
  style={btnStyle(reducedMotion ? 'var(--fc-state-success)' : 'var(--fc-text-muted)', true)}
  title={reducedMotion ? 'Motion reduced — click to enable animations' : 'Reduce motion — disable animations'}
>
  {reducedMotion ? '⏸ Calm' : '▶ Motion'}
</button>
```

- [ ] **Step 7: Write `flow-canvas-reduced-motion.spec.ts`.** Mirror `flow-canvas-interactions.spec.ts` setup. Cases: (a) click the Motion/Calm button → `document.body.classList` contains `fc-reduced-motion` AND `waitForOutgoingMessage(page, 'pref-save')` with `reducedMotion:true`; (b) `clearOutgoingMessages` then `postHostMessage({type:'pref-restore', reducedMotion:true})` → body class set AND `getOutgoingMessages` contains NO `pref-save` (proves restore/no-echo split); (c) with reduced motion on, load a fixture, post `execution-update {state:'running'}` for a node, assert its computed `animation-duration` parses to ~0.

- [ ] **Step 8: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-reduced-motion.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 9: Commit.** `feat(flow-canvas): reduced-motion kill switch + uiSlice pref + Toolbar toggle`.

### Task 7: Persist reduced-motion through C# config

**Files:**
- Modify `Models/AppConfiguration.cs` (WindowState, after line 504)
- Modify `UI/FlowCanvasForm.cs` (OnWebMessageReceived ~253, SendPersistedLayout ~347)
- Test: `SSH_Helper.Tests/UI/Form1FlowCanvasReducedMotionTests.cs`

- [ ] **Step 1: Add the config field.** In `WindowState` after `FlowCanvasOutputHeight`:

```csharp
// Flow Canvas reduced-motion pref (persisted from React UI; null = not yet set, defer to OS prefers-reduced-motion)
public bool? FlowCanvasReducedMotion { get; set; }
```

  `bool?` (nullable) is required: the null sentinel lets the React OS-detect seed win until the user explicitly toggles. Newtonsoft tolerates the missing field on old configs (deserializes to null). No migration needed.

- [ ] **Step 2: Handle `pref-save` inbound.** In `OnWebMessageReceived` near the `layout-save` case:

```csharp
case "pref-save":
    SaveReducedMotionPref(msg);
    break;
```

- [ ] **Step 3: Add `SaveReducedMotionPref`** (mirror `SavePanelSizes`; null-guard `_configService` because existing tests construct `FlowCanvasForm(configService: null)`):

```csharp
private void SaveReducedMotionPref(JObject msg)
{
    if (_configService == null) return;
    var v = msg["reducedMotion"]?.Value<bool>();
    if (v == null) return;
    _configService.Update(c =>
    {
        c.WindowState ??= new Models.WindowState();
        c.WindowState.FlowCanvasReducedMotion = v.Value;
    });
}
```

- [ ] **Step 4: Extend `SendPersistedLayout`** to post `pref-restore` only when the value is set (so OS-detect seed is not clobbered by a never-set config):

```csharp
var rm = ws.FlowCanvasReducedMotion;
if (rm.HasValue) SendMessage(new { type = "pref-restore", reducedMotion = rm.Value });
```

  Place after the existing `panelSizes` send. `SendPersistedLayout` is already called from the `ready` case after the pending-message queue drains, so React's `prefRestore` handler is registered first.

- [ ] **Step 5: Write `Form1FlowCanvasReducedMotionTests.cs`.** Mirror `SSH_Helper.Tests/UI/Form1FlowCanvasBrowsePathTests.cs` reflection harness (`[Collection(CallbackUiSerialCollection.Name)]`, `[WinFormsFact]`, SetField/GetField/InvokePrivateMethod, read the private `_pendingMessages` ConcurrentQueue<string>, `ReadMessageOfType`). Tests:
  - Construct `FlowCanvasForm(false, configService)` with a temp-dir `ConfigurationService` (`configFilePath` ctor); invoke `SaveReducedMotionPref` via reflection with `JObject {reducedMotion:true}`; assert `configService.GetCurrent().WindowState.FlowCanvasReducedMotion == true`.
  - Then invoke `SendPersistedLayout`; read `_pendingMessages` for a `pref-restore` message with `reducedMotion:true`.
  - Null-config: construct `FlowCanvasForm(false, configService: null)`, invoke `SaveReducedMotionPref` → no throw, no-op.

- [ ] **Step 6: Verify.**
  - `dotnet build SSH_Helper.sln` → 0 (runs BuildFlowCanvas).
  - `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~Form1FlowCanvasReducedMotion` → green.

- [ ] **Step 7: Commit.** `feat(flow-canvas): persist reduced-motion pref through AppConfiguration.WindowState`.

---

## Section 3: blockTimings bugfix + Run Heatmap

> Confirmed bug: `setBlockTiming` has zero callers; `blockTimings` stays empty so the duration badge never renders during live runs. ~2-line fix in the `executionUpdate` handler. Heatmap reuses the now-populated map, gated by a persisted `heatmapEnabled` toggle. Render-only — never touches `node.data`.

### Task 8: Fix the blockTimings bug

**Files:**
- Modify `FlowCanvas/src/stores/messageBridge.ts` (executionUpdate handler, lines 183-202)
- Test: `FlowCanvas/e2e/flow-canvas-run-timing.spec.ts`

- [ ] **Step 1: Write the failing test first (TDD red).** In `flow-canvas-run-timing.spec.ts` (mirror `flow-canvas-interactions.spec.ts` setup + `loadGraphFixture` with a node `node-1`): post `execution-update {stepId:'node-1', state:'running'}` then `{stepId:'node-1', state:'success', duration:1500}`; assert the node renders the duration badge text `1.5s`. This FAILS on current code (badge never appears). Add a sub-second case: `duration:250` → `250ms`.

- [ ] **Step 2: Apply the fix.** In the `running` branch add `state.setBlockTiming(stepId, Date.now());`. In the `success`/`error`/`skipped` branch (keep `updateTimelineEntry` unchanged) add:

```ts
const now = Date.now();
const dur = msg.duration != null ? Number(msg.duration) : undefined;
state.setBlockTiming(stepId, dur != null ? now - dur : now, now);
```

  `setBlockTiming` derives `duration = end - start`, so `start = now - dur, end = now` yields exactly `dur` — lighting up the existing `BaseBlock` badge and feeding the heatmap. No `executionSlice` signature change.

- [ ] **Step 3: Verify (TDD green).**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-run-timing.spec.ts` → the badge cases now pass.

- [ ] **Step 4: Commit.** `fix(flow-canvas): populate blockTimings on execution-update so duration badge renders`.

### Task 9: Run Heatmap overlay + persisted toggle

**Files:**
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` (heatmapEnabled + toggle + restore)
- Modify `FlowCanvas/src/stores/messageBridge.ts` (extend layoutRestore)
- Modify `FlowCanvas/src/nodes/BaseBlock.tsx` (render-time tint)
- Modify `FlowCanvas/src/panels/Toolbar.tsx` (toggle button)
- Modify `Models/AppConfiguration.cs`, `UI/FlowCanvasForm.cs`
- Test: `flow-canvas-run-timing.spec.ts`, `SSH_Helper.Tests/UI/FlowCanvasFormLayoutTests.cs`

- [ ] **Step 1: Extend `uiSlice`.** Interface: `heatmapEnabled: boolean; toggleHeatmap: () => void; restoreHeatmapEnabled: (enabled: boolean) => void;`. Initial `heatmapEnabled: false`. Implementation reuses the existing `layout-save` channel (fewer message types):

```ts
toggleHeatmap: () => set((s) => {
  const next = !s.heatmapEnabled;
  messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, heatmapEnabled: next });
  return { heatmapEnabled: next };
}),
restoreHeatmapEnabled: (enabled) => set({ heatmapEnabled: enabled }),
```

- [ ] **Step 2: Extend the `layoutRestore` handler** in `messageBridge.ts` after the panelSizes block:

```ts
if (typeof msg.heatmapEnabled === 'boolean') {
  store.getState().restoreHeatmapEnabled(msg.heatmapEnabled);
}
```

- [ ] **Step 3: Heatmap tint in `BaseBlock.tsx`.** Subscribe `const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);` and compute the max duration via a memoized derived selector (avoid O(n²) per-frame):

```ts
const maxDuration = useFlowStore((s) => {
  let max = 0;
  s.blockTimings.forEach((t) => { if (t.duration && t.duration > max) max = t.duration; });
  return max;
});
// ...after durationMs is read:
const heatTint = (heatmapEnabled && durationMs != null && maxDuration > 0
  && (execState === 'idle' || execState === 'success'))
  ? heatColor(durationMs / maxDuration)
  : undefined;
```

  with a pure helper (token-driven, no inline hex — Decision #4):

```ts
function heatColor(ratio: number): string {
  const r = Math.max(0, Math.min(1, ratio));
  const from = r < 0.5 ? 'var(--fc-heat-cold)' : 'var(--fc-heat-mid)';
  const to = r < 0.5 ? 'var(--fc-heat-mid)' : 'var(--fc-heat-hot)';
  const pct = Math.round((r < 0.5 ? r * 2 : (r - 0.5) * 2) * 100);
  return `color-mix(in oklch, ${to} ${pct}%, ${from})`;
}
```

  Apply as a render-time `boxShadow` ring (preserves category identity; precedence rule: heatmap tints only on `idle`/`success`, never overriding the running pulse or error glow):

```ts
const containerStyle: CSSProperties = {
  background: isDisabled ? 'var(--fc-surface-disabled)' : colors.bg,
  // ...existing boxShadow precedence...
  ...(heatTint ? { boxShadow: `0 0 0 3px ${heatTint}, ${existingBoxShadow}` } : {}),
};
```

  CRITICAL: do NOT call `updateNodeData` — purely a render-time override keyed off the store, so the graph snapshot/export is unchanged.

- [ ] **Step 4: Toolbar heatmap toggle.** Selectors + button next to Timeline:

```tsx
const heatmapEnabled = useFlowStore((s) => s.heatmapEnabled);
const toggleHeatmap = useFlowStore((s) => s.toggleHeatmap);
// ...
<button
  onClick={toggleHeatmap}
  style={btnStyle(heatmapEnabled ? 'var(--fc-accent)' : 'var(--fc-text-muted)', true)}
  title="Toggle run heatmap (color blocks by duration)"
>🔥 Heatmap</button>
```

- [ ] **Step 5: C# config + persistence.** In `WindowState` (after `FlowCanvasReducedMotion`):

```csharp
// Flow Canvas run-heatmap toggle (persisted from React UI)
public bool? FlowCanvasHeatmapEnabled { get; set; }
```

  Extend `SendPersistedLayout` to include the heatmap in the layout-restore message:

```csharp
if (panelSizes.Count > 0 || ws.FlowCanvasHeatmapEnabled.HasValue)
    SendMessage(new { type = "layout-restore", panelSizes, heatmapEnabled = ws.FlowCanvasHeatmapEnabled ?? false });
```

  Extend `SavePanelSizes` to read+persist `heatmapEnabled` (it already runs on the `layout-save` case):

```csharp
var heatmap = panelSizes?["heatmapEnabled"]?.Value<bool>();
// inside the existing Update lambda:
if (heatmap.HasValue) c.WindowState.FlowCanvasHeatmapEnabled = heatmap.Value;
```

  Note: `SavePanelSizes` currently early-returns when `panelSizes == null`; the `toggleHeatmap` message has no `panelSizes` object, so adjust the guard to also accept a message carrying only `heatmapEnabled` (read the raw `msg` JObject rather than only the `panelSizes` child, or send `heatmapEnabled` nested under `panelSizes`). Recommended: in `OnWebMessageReceived`'s `layout-save` case, pass the whole `msg` so both panel sizes and `heatmapEnabled` are visible to `SavePanelSizes`.

- [ ] **Step 6: Extend the run-timing spec + write the C# test.** In `flow-canvas-run-timing.spec.ts` add: (a) heatmap toggled on, post differing durations to node-1/node-2/node-3, assert relative `boxShadow` differs; toggling off reverts; (b) enabling emits an outgoing `layout-save` carrying `heatmapEnabled:true` (`getOutgoingMessages`); posting `{type:'layout-restore', heatmapEnabled:true}` shows the toolbar button active; (c) PARITY: capture `getGraphSnapshot()` before and after enabling heatmap + posting timings — assert byte-identical. Write `SSH_Helper.Tests/UI/FlowCanvasFormLayoutTests.cs` (`[WinFormsFact]`, temp-dir ConfigurationService): invoke `SavePanelSizes` with a JObject carrying `heatmapEnabled:true`, assert `WindowState.FlowCanvasHeatmapEnabled == true`; invoke `SendPersistedLayout`, read `_pendingMessages` for `layout-restore` with `heatmapEnabled:true`.

- [ ] **Step 7: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-run-timing.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (heatmap is render-only).
  - `dotnet build SSH_Helper.sln && dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~FlowCanvasFormLayout` → green.

- [ ] **Step 8: Commit.** `feat(flow-canvas): run heatmap overlay + persisted heatmapEnabled toggle`.

---

## Section 4: Problems Panel

> The C# bridge already computes per-node `{Severity, Message, NodeId}` diagnostics; the data is flattened to strings at `Form1.ApplyFlowCanvasGraph`. Un-flatten by adding a structured `diagnostics[]` field to the existing `apply-result` message (no new message type) and surface it in a click-to-fix panel. Purely a new field on an already-transient message — cannot alter exported YAML.

### Task 10: Thread structured diagnostics C#→React

**Files:**
- Modify `Services/FlowCanvasBridge.cs` (audit only)
- Modify `Form1.cs` (SendFlowCanvasApplyResult ~6976-6990; ApplyFlowCanvasGraph branches ~6907/6952)
- Modify `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`
- Test: extend `FlowCanvasBridgeTests.cs`; e2e in Task 11

- [ ] **Step 1: Audit `FlowCanvasBridge.cs` diagnostic Add-sites.** Confirm every `new FlowCanvasExportDiagnostic(...)` that maps to a node passes its `NodeId` (e.g. lines ~1081-1085 unreachable-node, ~1126-1130 comment-ignored, the unsupported-block-type error). No structural change; `FlowCanvasExportResult.Diagnostics` already exposes the full list. Fix any site that passes `null` for a node-clickable diagnostic.

- [ ] **Step 2: Add a `diagnostics` parameter to `SendFlowCanvasApplyResult`** and serialize the structured list, keeping the flat `errors`/`warnings` arrays for backward compat (Run/Test guards + show-error dialog still read them):

```csharp
private void SendFlowCanvasApplyResult(
    bool success,
    IReadOnlyCollection<string> errors,
    IReadOnlyCollection<string> warnings,
    Dictionary<string, string>? nodeStepMap,
    IReadOnlyList<FlowCanvasBridge.FlowCanvasExportDiagnostic>? diagnostics = null)
{
    var diag = (diagnostics ?? Array.Empty<FlowCanvasBridge.FlowCanvasExportDiagnostic>())
        .Select(d => new
        {
            nodeId = d.NodeId,
            severity = d.Severity == FlowCanvasBridge.ExportDiagnosticSeverity.Error ? "error" : "warning",
            message = d.Message,
        })
        .ToArray();

    _flowCanvasForm?.SendMessage(new
    {
        type = "apply-result",
        success,
        errors = errors.ToArray(),
        warnings = warnings.ToArray(),
        nodeStepMap = nodeStepMap ?? new Dictionary<string, string>(StringComparer.Ordinal),
        diagnostics = diag,
    });
}
```

- [ ] **Step 3: Pass `exportResult.Diagnostics`** from BOTH the failure branch (~line 6907) and the success branch (~line 6952) of `ApplyFlowCanvasGraph`. The other three callers (missing payload ~6876, selected-step ~6932, exception ~6967) pass nothing (those errors have no node).

- [ ] **Step 4: Extend `FlowCanvasBridgeTests.cs`.** Mirror `ExportGraphToYaml_UnsupportedBlockType_ReturnsErrorDiagnostic` (line ~267): assert `result.Diagnostics` contains a diagnostic with the expected `NodeId` (the existing test only checks `result.Errors` strings — add the NodeId assertion).

- [ ] **Step 5: Verify.**
  - `dotnet build SSH_Helper.sln` → 0.
  - `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~FlowCanvasBridge` → green.

- [ ] **Step 6: Commit.** `feat(flow-canvas): emit structured node diagnostics on apply-result`.

### Task 11: ProblemsPanel + uiSlice diagnostics state

**Files:**
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` (diagnostics + panelsVisible.problems)
- Modify `FlowCanvas/src/stores/messageBridge.ts` (parse diagnostics in applyResult)
- Create `FlowCanvas/src/panels/ProblemsPanel.tsx`
- Modify `FlowCanvas/src/App.tsx` (mount panel)
- Modify `FlowCanvas/src/panels/Toolbar.tsx` (toggle + error-count badge)
- Test: `FlowCanvas/e2e/flow-canvas-problems-panel.spec.ts`

- [ ] **Step 1: Extend `uiSlice`.** Add the shared type and state:

```ts
export interface NodeDiagnostic { nodeId?: string; severity: 'error' | 'warning'; message: string; }
// interface additions:
//   panelsVisible: { ...; problems: boolean };
//   diagnostics: NodeDiagnostic[];
//   setDiagnostics: (d: NodeDiagnostic[]) => void;
```

  Add `problems: false` to `panelsVisible` default; `diagnostics: []` default; `setDiagnostics: (d) => set({ diagnostics: d })`. Fold `diagnostics: []` into `clearExportStatus` so it clears everywhere exportStatus does (execution-started, undo/redo resets, graphSlice `clearedExportStatusState`).

- [ ] **Step 2: Parse diagnostics in the `applyResult` handler** (after `setExportStatus`, leave the string path untouched):

```ts
const rawDiag = Array.isArray(msg.diagnostics) ? msg.diagnostics : [];
const parsed: NodeDiagnostic[] = rawDiag.map((d: any) => ({
  nodeId: d.nodeId != null ? String(d.nodeId) : undefined,
  severity: d.severity === 'error' ? 'error' : 'warning',
  message: String(d.message ?? ''),
}));
store.getState().setDiagnostics(parsed);
```

- [ ] **Step 3: Write `ProblemsPanel.tsx`** (mirror `DebugPanel.tsx` floating card; mounted inside the ReactFlowProvider so `useReactFlow` works). Full component:

```tsx
import { useReactFlow } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';

export default function ProblemsPanel() {
  const visible = useFlowStore((s) => s.panelsVisible.problems);
  const diagnostics = useFlowStore((s) => s.diagnostics);
  const selectNode = useFlowStore((s) => s.selectNode);
  const getNode = useFlowStore((s) => s.nodes);
  const { setCenter } = useReactFlow();
  const reducedMotion = useFlowStore((s) => s.reducedMotion);

  if (!visible || diagnostics.length === 0) return null;

  const focus = (nodeId?: string) => {
    if (!nodeId) return;
    const node = getNode.find((n) => n.id === nodeId);
    if (!node) return; // node may have been deleted since export
    selectNode(nodeId);
    setCenter(node.position.x, node.position.y, { zoom: 1, duration: reducedMotion ? 0 : 400 });
  };

  return (
    <div style={{
      position: 'absolute', bottom: 16, left: 16, width: 360, maxHeight: 240, overflowY: 'auto',
      background: 'var(--fc-surface-1)', border: '1px solid var(--fc-border)',
      borderRadius: 'var(--fc-radius-md)', boxShadow: 'var(--fc-shadow-sm)', zIndex: 20,
      fontSize: 'var(--fc-fs-body)', color: 'var(--fc-text)',
    }}>
      <div style={{ padding: '6px 10px', borderBottom: '1px solid var(--fc-border)', fontWeight: 600 }}>
        Problems ({diagnostics.length})
      </div>
      {diagnostics.map((d, i) => (
        <div
          key={i}
          onClick={() => focus(d.nodeId)}
          title={d.nodeId ? 'Click to select & center this block' : undefined}
          style={{
            display: 'flex', gap: 8, padding: '6px 10px',
            cursor: d.nodeId ? 'pointer' : 'default',
            background: 'var(--fc-diag-row-bg)',
            borderLeft: `3px solid ${d.severity === 'error' ? 'var(--fc-diag-error)' : 'var(--fc-diag-warning)'}`,
          }}
        >
          <span aria-hidden>{d.severity === 'error' ? '✕' : '⚠'}</span>
          <span>{d.message}</span>
        </div>
      ))}
    </div>
  );
}
```

  Order diagnostics errors-before-warnings if not already ordered by the bridge.

- [ ] **Step 4: Mount in `App.tsx`** as a sibling to `<DebugPanel />`: `<ProblemsPanel />`.

- [ ] **Step 5: Toolbar Problems toggle.** Selector `const problemsVisible = useFlowStore((s) => s.panelsVisible.problems);` and a button `onClick={() => togglePanel('problems')}` styled `btnStyle(problemsVisible ? 'var(--fc-accent)' : 'var(--fc-text-muted)', true)`, optionally with an error-count badge from `diagnostics.filter(d => d.severity === 'error').length`.

- [ ] **Step 6: Write `flow-canvas-problems-panel.spec.ts`.** Mirror `flow-canvas-parity.spec.ts` setup. Load a 2-node fixture, ensure the Problems panel is toggled on, then `postHostMessage({type:'apply-result', success:false, errors:['...'], warnings:[], diagnostics:[{nodeId:'node-2', severity:'error', message:'Bad block'}]})`. Assert: panel shows `Bad block`; clicking the row applies selection to `node-2` (`.react-flow__node.selected` corresponds to node-2). Verify behaviorally that the clicked node is brought near the viewport (bounding-box visible).

- [ ] **Step 7: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-problems-panel.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (no fixture edits; export unchanged).

- [ ] **Step 8: Commit.** `feat(flow-canvas): click-to-fix Problems panel reading structured diagnostics`.

---

## Section 5: Connection-Validity Guards

> The one authoring-touching Wave 1 piece. Today there is NO connection validation — `onConnect` unconditionally `addEdge`s. This lets users author shapes the C# exporter cannot faithfully serialize (multiple plain successors, fan-in, cycles, edges into Start). Add a pure predicate wired to both `isValidConnection` (live drag feedback) and `onConnect` (hard guard). MUST extend the Import→Export→re-import parity + negative specs before merge (Decision #10).

### Task 12: Author the connection-rules predicate (TDD via test hook)

**Files:**
- Create `FlowCanvas/src/utils/connectionRules.ts`
- Modify `FlowCanvas/src/stores/messageBridge.ts` (expose `isConnectionAllowed` on test hooks)
- Test: `FlowCanvas/e2e/flow-canvas-connection-guards.spec.ts` (table-driven via hook)

- [ ] **Step 1: Write the table-driven failing test (TDD red).** In `flow-canvas-connection-guards.spec.ts`, drive the rule via `page.evaluate(() => window.__FLOWCANVAS_TEST_HOOKS__.isConnectionAllowed(conn, nodes, edges))`. Cases (assert `ok`): self-loop → false; duplicate edge → false; 2nd plain successor from a non-container → false; fan-in (2nd edge into same target) → false; cycle (target reaches source) → false; edge into `__start__` → false; container `if` then+else (distinct handles) → true; container `continue` edge → true; first plain successor → true.

- [ ] **Step 2: Write `connectionRules.ts`.** Pure, dependency-light (imports only `blockDefMap`). Full code:

```ts
// FlowCanvas/src/utils/connectionRules.ts
import type { Connection, Node, Edge } from '@xyflow/react';
import { blockDefMap } from '../blockDefs/registry';

export interface ConnectionVerdict { ok: boolean; reason?: string; }

const START_ID = '__start__';

export function isConnectionAllowed(connection: Connection, nodes: Node[], edges: Edge[]): ConnectionVerdict {
  const { source, target, sourceHandle, targetHandle } = connection;
  if (!source || !target) return { ok: false, reason: 'Incomplete connection.' };

  if (source === target) return { ok: false, reason: 'A block cannot connect to itself.' };
  if (target === START_ID) return { ok: false, reason: 'Nothing can connect into Start.' };

  // Duplicate edge (same source+sourceHandle+target+targetHandle)
  const isDuplicate = edges.some(
    (e) => e.source === source && e.target === target
      && (e.sourceHandle ?? null) === (sourceHandle ?? null)
      && (e.targetHandle ?? null) === (targetHandle ?? null),
  );
  if (isDuplicate) return { ok: false, reason: 'That connection already exists.' };

  // Fan-in: target already has an incoming edge (Wave 1 forbids fan-in; exporter treats >1 incoming as a stop)
  const targetHasIncoming = edges.some((e) => e.target === target);
  if (targetHasIncoming) return { ok: false, reason: 'A block can have only one incoming connection.' };

  // Per-handle uniqueness. Containers keep distinct branch edges that leave via the BOTTOM handle
  // (sourceHandle null/empty) disambiguated by branchPath — so a container may emit MULTIPLE empty-handle
  // edges; a non-container may emit exactly ONE. Named handles ('continue','false', etc.) allow one each.
  const sourceNode = nodes.find((n) => n.id === source);
  const blockType = (sourceNode?.data as Record<string, unknown> | undefined)?.blockType as string | undefined;
  const def = blockType ? blockDefMap.get(blockType) : undefined;
  const isContainer = !!def?.isContainer;
  const handleKey = sourceHandle ?? '';

  const sameHandleEdges = edges.filter(
    (e) => e.source === source && (e.sourceHandle ?? '') === handleKey,
  );
  if (handleKey === '') {
    // Empty/bottom handle: containers may branch multiple times; non-containers exactly once.
    if (!isContainer && sameHandleEdges.length >= 1) {
      return { ok: false, reason: 'This block already has a successor.' };
    }
  } else if (sameHandleEdges.length >= 1) {
    return { ok: false, reason: `This block already has a connection on its "${handleKey}" output.` };
  }

  // Cycle: target can already reach source via existing edges (adding source->target would close a loop).
  if (canReach(target, source, edges)) {
    return { ok: false, reason: 'That connection would create a loop.' };
  }

  return { ok: true };
}

function canReach(from: string, to: string, edges: Edge[]): boolean {
  if (from === to) return true;
  const adjacency = new Map<string, string[]>();
  for (const e of edges) {
    const list = adjacency.get(e.source) ?? [];
    list.push(e.target);
    adjacency.set(e.source, list);
  }
  const seen = new Set<string>();
  const stack = [from];
  while (stack.length) {
    const node = stack.pop()!;
    if (node === to) return true;
    if (seen.has(node)) continue;
    seen.add(node);
    for (const next of adjacency.get(node) ?? []) stack.push(next);
  }
  return false;
}
```

- [ ] **Step 3: Expose on test hooks.** In `installFlowCanvasTestHooks` (messageBridge.ts ~line 92), add `hooks.isConnectionAllowed = (conn, nodes, edges) => isConnectionAllowed(conn, nodes, edges);` and import `isConnectionAllowed`. Update the harness's `FlowCanvasTestHooks` type if it constrains keys.

- [ ] **Step 4: Verify (TDD green).**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-connection-guards.spec.ts` → the table cases pass.

- [ ] **Step 5: Commit.** `feat(flow-canvas): pure isConnectionAllowed predicate + test hook`.

### Task 13: Wire the guard into ReactFlow + onConnect + feedback

**Files:**
- Modify `FlowCanvas/src/App.tsx` (isValidConnection prop; mount ConnectionNotice)
- Modify `FlowCanvas/src/stores/slices/graphSlice.ts` (guard onConnect)
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` (connectionNotice)
- Create `FlowCanvas/src/panels/ConnectionNotice.tsx`
- Test: `flow-canvas-connection-guards.spec.ts` (gesture + parity); `flow-canvas-preset-parity.spec.ts`, `flow-canvas-preset-negative.spec.ts`, `flow-canvas-gesture-smoke.spec.ts`

- [ ] **Step 1: Add the `connectionNotice` primitive to `uiSlice`.**

```ts
// interface:
//   connectionNotice: { message: string; nonce: number } | null;
//   showConnectionNotice: (message: string) => void;
//   clearConnectionNotice: () => void;
// default: connectionNotice: null,
showConnectionNotice: (message) => set((s) => ({ connectionNotice: { message, nonce: (s.connectionNotice?.nonce ?? 0) + 1 } })),
clearConnectionNotice: () => set({ connectionNotice: null }),
```

- [ ] **Step 2: Guard `onConnect` at the top (before pushSnapshot).** In `graphSlice.ts`:

```ts
onConnect: (connection) => {
  const verdict = isConnectionAllowed(connection, get().nodes, get().edges);
  if (!verdict.ok) {
    get().showConnectionNotice(verdict.reason ?? 'Connection not allowed.');
    return;
  }
  get().pushSnapshot('Connect edge');
  set((state) => { /* unchanged branch-metadata + addEdge logic */ });
},
```

  Do NOT change the edgeProps/branch-metadata logic for allowed connections (parity: identical output for valid drags). Import `isConnectionAllowed`.

- [ ] **Step 3: Add `isValidConnection` to ReactFlow in `App.tsx`** (a `useCallback` reading `getState()` so it always sees current nodes/edges without re-subscribing):

```ts
const isValidConnection = useCallback(
  (conn: Connection | Edge) =>
    isConnectionAllowed(conn as Connection, useFlowStore.getState().nodes, useFlowStore.getState().edges).ok,
  [],
);
// <ReactFlow ... isValidConnection={isValidConnection} />
```

  Confirm against `@xyflow/react` v12 that `isValidConnection` receives a `Connection`-shaped object and returning `false` cleanly aborts the drop (verify via Context7 / v12 docs before coding if unsure).

- [ ] **Step 4: Write `ConnectionNotice.tsx`** (transient, reduced-motion aware, token-styled). Full component:

```tsx
import { useEffect } from 'react';
import { useFlowStore } from '../stores/useFlowStore';

export default function ConnectionNotice() {
  const notice = useFlowStore((s) => s.connectionNotice);
  const clear = useFlowStore((s) => s.clearConnectionNotice);

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(clear, 2500);
    return () => clearTimeout(t);
  }, [notice, clear]);

  if (!notice) return null;
  return (
    <div style={{
      position: 'absolute', top: 16, left: '50%', transform: 'translateX(-50%)', zIndex: 30,
      padding: '8px 14px', maxWidth: 420,
      background: 'var(--fc-notice-bg)', color: 'var(--fc-notice-fg)',
      border: '1px solid var(--fc-notice-border)', borderRadius: 'var(--fc-radius-md)',
      boxShadow: 'var(--fc-shadow-sm)', fontSize: 'var(--fc-fs-body)',
    }}>
      {notice.message}
    </div>
  );
}
```

  Mount `<ConnectionNotice />` in `App.tsx` next to `<ProblemsPanel />`. The body-level `fc-reduced-motion` class already neutralizes any enter/exit transition.

- [ ] **Step 5: Extend the connection-guards spec with gesture + parity.** Add to `flow-canvas-connection-guards.spec.ts` (mirror `flow-canvas-gesture-smoke.spec.ts` real-drag pattern `sourceHandle.dragTo(targetHandle)` + `getGraphSnapshot` edge assertions): self-loop drag → edge count unchanged + notice visible; duplicate drag → no new edge; 2nd plain successor → rejected; fan-in → rejected; cycle → rejected; POSITIVE: `if` then+else and `continue` edges still produce IDENTICAL edges to today (regression guard against over-blocking). PARITY: after a rejected drag click apply-yaml and run `evaluateParityCases([...])` asserting `exportSuccess && exportValidationErrors === []`; for the positive container cases assert `semanticEquivalent === true`. Add a `createConnectionGuardFixture` to `fixtures/graphs.ts` for the multi-target/cycle/fan-in cases. Register the spec in the `test:e2e:parity` npm script alongside the other three.

- [ ] **Step 6: Confirm guard does NOT gate load/undo paths.** Verify `setNodes`/`setEdges`/`applyEdgeChanges`/undo-redo do NOT call `isConnectionAllowed` — only `onConnect` and `isValidConnection`. Existing presets with pre-existing fan-in must still load (the guard only runs on new user drags). Add an assertion: `loadGraphFixture` with a fan-in fixture then `getGraphSnapshot` shows the loaded edges intact.

- [ ] **Step 7: Verify (full parity gate — Decision #10).**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (now includes connection-guards).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-connection-guards.spec.ts e2e/flow-canvas-preset-parity.spec.ts e2e/flow-canvas-preset-negative.spec.ts e2e/flow-canvas-gesture-smoke.spec.ts` → all green.

- [ ] **Step 8: Commit.** `feat(flow-canvas): connection-validity guards (isValidConnection + onConnect) with in-canvas notice`.

### Task 14: Full Wave 1 integration verification + rebuild

**Files:** all of the above. No code changes — verification + the load-bearing rebuild.

- [ ] **Step 1: Rebuild the embedded bundle.** The committed `FlowCanvas/dist/` is what the C# app embeds; the timing bugfix (and every sweep) is invisible at runtime until rebuilt. Run `cd FlowCanvas && npm run build` then `dotnet build SSH_Helper.sln` (re-embeds dist).

- [ ] **Step 2: Run the full e2e suite.** `cd FlowCanvas && npm run test:e2e` → all specs green (token-sweep, reduced-motion, run-timing, problems-panel, connection-guards, plus the pre-existing parity/preset/gesture/interactions/properties/variable specs unchanged).

- [ ] **Step 3: Run the C# suite (logic layer).** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` → green. If UI/dialog tests flake under full parallel run (known per project memory), re-run the affected `[WinFormsFact]` classes in isolation with `--blame-hang-timeout`.

- [ ] **Step 4: Manual smoke (fresh-eyes pass).** Launch `dotnet run --project SSH_Helper.csproj`, open Flow Canvas: toggle reduced motion (animations stop, persists across app restart), run a script (duration badges appear), toggle heatmap (blocks tint by duration, persists), apply an invalid graph (Problems panel lists it, click selects+centers the node), attempt an illegal connection (rejected with a notice), close and reopen the app and confirm reduced-motion + heatmap toggles restored from config.json.

- [ ] **Step 5: Commit.** `chore(flow-canvas): rebuild embedded dist for Wave 1 + integration verification`.

---

## Wave 1 Exit Criteria

- [ ] `cd FlowCanvas && npm run build` exits 0 (tsc + vite, no type errors).
- [ ] `dotnet build SSH_Helper.sln` exits 0 (BuildFlowCanvas target re-embeds the rebuilt `dist/`).
- [ ] No hardcoded hex/rgba outside the token layer: `flow-canvas-token-sweep.spec.ts` no-raw-hex scan returns 0 (the only allowed literal is a user comment color = `DEFAULT_COMMENT_COLOR`). `tokens.css`/`tokens.ts` are the sole definition sites (Decision #4).
- [ ] All ~30 OKLCH tokens + 7 category hues resolve on `:root`; dark-only ships, light/HC remains a pure `[data-theme]` token swap.
- [ ] Reduced-motion: Toolbar toggle adds/removes `body.fc-reduced-motion`, neutralizes all CSS animations/transitions; persists through `AppConfiguration.WindowState.FlowCanvasReducedMotion` and round-trips across app restart; restore does NOT echo a `pref-save`; OS `prefers-reduced-motion` seeds the default, explicit toggle overrides (Decision #7).
- [ ] blockTimings bug fixed: the duration badge renders during live runs (`flow-canvas-run-timing.spec.ts` red→green).
- [ ] Run Heatmap tints blocks by relative duration when enabled, reverts when off, never mutates `node.data`; `FlowCanvasHeatmapEnabled` persists and round-trips across restart.
- [ ] Problems panel lists structured `{nodeId, severity, message}` diagnostics; clicking a row selects and centers the offending node; null-nodeId rows are non-clickable; `FlowCanvasBridgeTests` asserts `NodeId` is carried.
- [ ] Connection guards reject self-loops, duplicates, 2nd plain successors, fan-in, cycles, and edges into Start, with an in-canvas notice; container branches (then/elif/else, cases, parallel, try/catch/finally, continue) are unchanged; guards do NOT gate load/undo paths.
- [ ] STRICT round-trip (Decision #10): `flow-canvas-parity.spec.ts`, `flow-canvas-preset-parity.spec.ts`, `flow-canvas-preset-negative.spec.ts`, `flow-canvas-gesture-smoke.spec.ts` all green unchanged; the connection-guards spec proves rejected drags leave an exportable graph (`exportSuccess && exportValidationErrors === []`) and accepted container branches are `semanticEquivalent`; heatmap/diagnostics/reduced-motion export-equality asserted before/after toggling.
- [ ] `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` green (reduced-motion + heatmap persistence + diagnostics NodeId tests).
- [ ] Every task ended in a commit; embedded `dist/` rebuilt so the fixes are live at runtime; manual fresh-eyes smoke passed.
