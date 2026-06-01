# Flow Canvas Wave 2a — Node & Icon Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Land Wave 2a "Node & Icon Redesign" for the SSH_Helper Flow Canvas: (1) a vendored Lucide stroke-icon system (zero new npm runtime deps) wired into a typed `icon-key → SVG` map so every block's currently-dead `def.icon` metadata finally renders; (2) a premium "accent-rail, not blob" `BaseBlock` redesign — neutralize the node body to a surface token, move the category color onto a 3px absolutely-positioned LEFT RAIL plus a category-tinted icon chip, soften the badge, and preserve **byte-for-byte** the Wave 1 exec-state precedence / heatmap ring / breakpoint gutter / duration badge / all four handles / child + disabled styling / `fc-reduced-motion` kill switch; (3) optional StartNode rail parity (keeps its green-gradient identity, source-only handle untouched); and (4) round-trip-safe branch-band containment frames derived from existing `_isChildOf`/`_stepPath` metadata via a `ViewportPortal` layer, gated behind a `uiSlice` toggle, plus harmonizing the one remaining raw-hex leak (`Properties.tsx` `_branchColor`). **Every change is VISUAL/render-only — no `node.data` write, no `exportGraph.ts`/`FlowCanvasBridge.cs` change — so the YAML round-trip is provably unaffected.**

**Architecture:** The Flow Canvas is React 19 + `@xyflow/react` v12 + Zustand 5, built via Vite and embedded as a .NET assembly resource, hosted in an offline WebView2 (Chromium) control inside a WinForms app. No runtime network — everything inlines (icons are hand-vendored inline `<svg>` JSX, not a font/sprite/fetch). Wave 1 (DONE) shipped the OKLCH token layer: `FlowCanvas/src/styles/tokens.css` (all colors as `--fc-*` tokens) + `FlowCanvas/src/utils/tokens.ts` (`CATEGORY_VARS`, `DEFAULT_COMMENT_COLOR`, `resolveCssVar`, `mix(color,pct)` → `color-mix(in oklch, color pct%, transparent)`). **HARD RULE (Decision #4, enforced by the LIVE Playwright gate `e2e/flow-canvas-token-sweep.spec.ts`): NO hardcoded hex/rgba outside `tokens.css`/`tokens.ts` — add new `--fc-*` tokens there and reference `var(--fc-*)`; for translucency use `mix()`/`color-mix`, never a `var()`+alpha concat.** YAML export (`exportGraph.ts` → `FlowCanvasBridge.ExportToYaml`) serializes `node.data.props` ONLY; `def.icon` is static registry metadata never read by the exporter, and child nodes (`props._isChildOf != null`) emit no YAML — so every Wave 2a visual change is round-trip-safe and the parity specs MUST stay green.

**Tech Stack:** TypeScript/React 19/Zustand 5/CSS (OKLCH, CSS animation/WAAPI — **no animation library in Wave 2a; Framer Motion / `motion` is DEFERRED to 2b** per Wave 2 decisions, since rail/surface/icon/bands are fully static); Lucide stroke SVG **hand-vendored inline** (Decision #9 — no `lucide-react` runtime dependency); Playwright e2e (`FlowCanvas/e2e/*.spec.ts`); build `cd FlowCanvas && npm run build` (tsc && vite build); dist rebuild embedded via the `BuildFlowCanvas` MSBuild target on `dotnet build SSH_Helper.sln`.

---

## File Structure

### New files

| Path | Responsibility |
| --- | --- |
| `FlowCanvas/src/nodes/BlockIcon.tsx` | The vendored icon system. A `Record<string, JSX.Element>` of hand-inlined Lucide stroke `<svg>` glyphs keyed by the ~40 distinct `def.icon` strings (`ssh`, `term`, `if`, … `wait`), each `stroke="currentColor"` / `fill="none"` / `strokeWidth={1.75}` / 14×14, plus a neutral `box` fallback for unknown keys. Exports `<BlockIcon name={...} size? />`. Zero color literals (currentColor only) → gate-safe; zero network → offline-safe. |
| `FlowCanvas/src/utils/branchBands.ts` | Pure derivation `computeBranchBands(nodes): BranchBand[]` + `branchColorVar(branchKey): string`. Groups flattened children by `(parentId, branchKey)` parsed from `_stepPath`, computes a padded bounding box per group, maps `branchKey → var(--fc-branch-*)` (never the raw `_branchColor` hex). No `node.data` write. |
| `FlowCanvas/src/nodes/BranchBandsLayer.tsx` | A `@xyflow/react` `ViewportPortal` renderer that draws one `pointer-events:none`, low-z-index band div (`background: mix(colorVar, 8)`, `borderLeft: 3px solid colorVar`, radius 8) behind the nodes for each `BranchBand`. Subscribes to `uiSlice.branchBandsEnabled`. |
| `FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts` | Asserts: rail span exists + its `backgroundColor` resolves to `--fc-cat-ssh-border`; body container `backgroundColor` resolves to `--fc-node-surface` (proves body neutralized, not `colors.bg`); an `<svg>` icon renders in the header with computed color === `--fc-cat-ssh-border`; an unknown blockType renders the fallback glyph without throwing; exec-pulse animationDuration > 0 on running, success/selection/heat box-shadows still distinct (precedence unregressed). |
| `FlowCanvas/e2e/flow-canvas-block-icon.spec.ts` | Iterates the registry: every `def.icon` resolves to a non-empty `<svg>` (coverage); `send` icon `<svg>` has `stroke="currentColor"`; fallback path for unknown key. |
| `FlowCanvas/e2e/flow-canvas-branch-bands.spec.ts` | Loads the container fixture; asserts a band element renders behind the child node, `pointer-events:none` (dragging a child still moves it / band does not capture), band color resolves to `--fc-branch-then`, and toggling `branchBandsEnabled` hides the layer. |

### Modified files

| Path | Responsibility / change |
| --- | --- |
| `FlowCanvas/src/utils/tokens.ts` | Add `icon` to `CategoryVarSet` + `catVars()` (returns `var(--fc-cat-${c}-icon)`) so `colors.icon` is the single source for the icon tint, distinct from the softer `colors.text`. (Optional — may reuse `colors.border`; see micro-decision.) |
| `FlowCanvas/src/styles/tokens.css` | Add the new generic node tokens (`--fc-node-surface`, `--fc-node-border`, `--fc-rail-w`, `--fc-fs-header` is already present), the 7 per-category `--fc-cat-*-icon` tokens (alias the category border hue), and the `--fc-branch-*` branch tokens. The ONLY place new colors may be authored (Decision #4). |
| `FlowCanvas/src/nodes/BaseBlock.tsx` | The redesign. Neutralize `containerStyle.background` → `var(--fc-node-surface)`; soften border to `var(--fc-node-border)`; add an absolutely-positioned rail `<span>` (category `colors.border`); add the category-tinted icon chip wrapping `<BlockIcon>` as the first header element; pad header/preview left by `--fc-rail-w`; soften the badge to an outlined chip. **Preserve verbatim** lines computing `existingBoxShadow`, `heatTint`, the `exec-pulse` animation, and all four `Handle` blocks. |
| `FlowCanvas/src/nodes/StartNode.tsx` | Optional rail parity: add the same absolute rail using `var(--fc-start-accent)` and pad the header left. Keep the green-gradient body + glow identity and the **source-only Handle untouched** (connection-guards invariant). |
| `FlowCanvas/src/panels/Properties.tsx` | Hygiene: replace the raw `_branchColor` hex consumption (lines ~1735/1737/1739) with `branchColorVar(branchKey)` so the Properties branch chip references `var(--fc-branch-*)` — removes the last raw-hex leak and unifies band + chip color. |
| `FlowCanvas/src/panels/Palette.tsx` | Optional second icon consumer: render `<BlockIcon name={def.icon} size={14} />` before the label span in `PaletteItem` (reuses the existing `gap:6` row). |
| `FlowCanvas/src/stores/slices/uiSlice.ts` | Add `branchBandsEnabled: boolean` (default `true`) + `toggleBranchBands` (mirrors `heatmapEnabled`/`toggleHeatmap`). Transient store state — never in the export payload. |
| `FlowCanvas/src/App.tsx` | Mount `<BranchBandsLayer />` as the FIRST child inside `<ReactFlow>` (behind `<Controls>`/`<MiniMap>`/`<Background>` is fine; z-index keeps it under nodes). |

**Verified facts (do not re-derive):**
- `BaseBlock.tsx` is fully tokenized post-Wave-1. `containerStyle` is lines 99-110; `existingBoxShadow` lines 93-97; `heatTint` lines 78-81; running pulse `animation='exec-pulse 1.5s ease-in-out infinite'` lines 112-115; `headerStyle` 117-124; `badgeStyle` 126-136; header children 184-215; preview 218-230; the four Handles Top/Bottom/false-Right/continue-Left at 178-182 / 233-237 / 240-250 / 255-273. The Unknown-type early return is line 60.
- `def.icon` is declared on the `BlockDef` interface (registry.ts:37) and set on all 41 entries but rendered NOWHERE today (Wave 2a is its first consumer). Distinct keys (verified by grep `icon: '`): `ssh, term, sftp, if, for, while, switch, parallel, try, break, continue, call, return, exit, extract, set, parse, table, assert, ping, dns, port, http, webhook, oauth, vault, print, input, choose, multi, confirm, read, write, exists, audio, log, notify, terminal, column, env, wait` (note `while` shared by while+repeat, `set` shared by set+sethistorylabel).
- `tokens.ts` already exports `mix(color, pct)` (color-mix over transparent), `CATEGORY_VARS` with `{border,bg,badge,badgeText,text}`, and `resolveCssVar`. `BaseBlock` already imports `mix`.
- The token-sweep gate (`flow-canvas-token-sweep.spec.ts`) mounts `createSshBlockFixture()` (an `ssh`/`send` block) and scans every `[style]` element for raw `#hex` and `var(...)<hex-alpha>` concats; the only allowed literal is `DEFAULT_COMMENT_COLOR`. The probe-div var-resolution pattern is lines 60-68.
- `flow-canvas-run-timing.spec.ts` provides `settledBoxShadow()`/`boxShadowOf()` and the heatmap 3-distinct-shadows + revert test + the "PARITY: enabling heatmap is render-only" graph-snapshot test.
- `flow-canvas-reduced-motion.spec.ts` reads `node.locator('> div').first()` `animationDuration` — the redesign MUST keep the first-child div as the animated card (rail is a child INSIDE it, not a wrapper).
- Parity bundle = `npm run test:e2e:parity` → `flow-canvas-preset-parity` + `flow-canvas-preset-negative` + `flow-canvas-gesture-smoke` + `flow-canvas-connection-guards` (run `--workers=1`); plus `flow-canvas-parity.spec.ts`. These compare normalized node/edge payloads and MUST stay green — that green run is the round-trip proof.
- `Properties.tsx` reads `_branchColor` at line 1627 and pipes it raw into `mix(branchColor || …, 8)` / `borderLeft` at 1735/1737/1739 — a latent hex leak the band fixture will expose.
- `createImportedChildEditingFixture()` (fixtures/graphs.ts:424) is the ready-made if/then container fixture (`if-1` + `then-1` with `_isChildOf:'if-1'`, `_branchLabel:'then'`, `_branchColor:'#2ecc71'`) — reuse as the branch-band fixture.
- `App.tsx` renders `<ReactFlow>` at line 370 with `<Controls>`/`<MiniMap>`/`<Background>` children (400-412); `@xyflow/react` `ViewportPortal` is a first-class v12 export.
- No `lucide-react` / `motion` in `package.json` today — and none is added (icons vendored inline; motion deferred).

---

## Section 1: Icon System (vendored Lucide, no new deps)

> Build the icon source FIRST so the redesigned header (Section 2) has a real glyph to render. Icons are hand-inlined stroke SVGs (Decision #9) — zero npm runtime dependency, zero network, gate-safe (`currentColor` only). A pure presentational module cannot be strict-TDD-first, so each task is gated by `npm run build` clean + a coverage spec + unchanged parity.

### Task 1: Author `BlockIcon.tsx` (vendored stroke-icon map + fallback)

**Files:**
- Create `FlowCanvas/src/nodes/BlockIcon.tsx`
- Test: `FlowCanvas/e2e/flow-canvas-block-icon.spec.ts` (created in Step 3)

- [ ] **Step 1: Write the full `BlockIcon.tsx`.** Hand-vendored Lucide stroke paths, keyed by the registry `icon` string. Every glyph is `stroke="currentColor"`, `fill="none"`, `strokeWidth={1.75}`, `strokeLinecap="round"`, `strokeLinejoin="round"` (Lucide identity), sized by the `size` prop. The `ICONS` record covers all distinct keys; `BlockIcon` falls back to `box` for any unknown key so Unknown-type/future blocks never crash. No color literals anywhere (currentColor inherits the category tint from the parent). Full code:

```tsx
// FlowCanvas/src/nodes/BlockIcon.tsx
// Vendored Lucide stroke icons (Decision #9): inlined as JSX so they bundle into the offline
// WebView2 build with zero runtime network, zero font, zero npm dependency. Every glyph uses
// stroke="currentColor" (Decision #4 gate-safe — the category tint is inherited from the parent's
// `color`). Keyed by the registry `def.icon` string (~40 distinct values); unknown keys fall back
// to a neutral `box` glyph so Unknown-type and any future icon key render without throwing.
import { type JSX } from 'react';

/** Children (the <path>/<circle>/… stroke geometry) for each icon key. The wrapping <svg> with
 *  size + stroke attrs is applied once in BlockIcon, so each entry is geometry-only. */
const ICONS: Record<string, JSX.Element> = {
  // SSH category
  ssh: (<><polyline points="4 17 10 11 4 5" /><line x1="12" y1="19" x2="20" y2="19" /></>), // SquareTerminal-ish prompt
  term: (<><rect x="3" y="4" width="18" height="16" rx="2" /><polyline points="7 9 10 12 7 15" /><line x1="13" y1="15" x2="17" y2="15" /></>), // TerminalSquare
  sftp: (<><path d="M12 3v12" /><polyline points="7 8 12 3 17 8" /><path d="M5 21h14" /></>), // FileUp / upload arrow
  // Control flow
  if: (<><line x1="6" y1="3" x2="6" y2="15" /><circle cx="18" cy="6" r="3" /><circle cx="6" cy="18" r="3" /><path d="M18 9a9 9 0 0 1-9 9" /></>), // GitBranch
  for: (<><path d="m17 2 4 4-4 4" /><path d="M3 11v-1a4 4 0 0 1 4-4h14" /><path d="m7 22-4-4 4-4" /><path d="M21 13v1a4 4 0 0 1-4 4H3" /></>), // Repeat
  while: (<><path d="M21 12a9 9 0 1 1-3-6.7" /><polyline points="21 3 21 9 15 9" /></>), // RotateCw
  switch: (<><circle cx="12" cy="18" r="3" /><circle cx="6" cy="6" r="3" /><circle cx="18" cy="6" r="3" /><path d="M18 9v2a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2V9" /><path d="M12 12v3" /></>), // GitFork
  parallel: (<><rect x="3" y="4" width="18" height="4" rx="1" /><rect x="3" y="10" width="18" height="4" rx="1" /><rect x="3" y="16" width="18" height="4" rx="1" /></>), // Rows3
  try: (<><path d="M20 13c0 5-3.5 7.5-7.7 8.95a1 1 0 0 1-.6 0C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.2-2.7a1 1 0 0 1 1.6 0C15.5 3.8 18 5 20 5a1 1 0 0 1 1 1z" /><path d="M12 8v4" /><path d="M12 16h.01" /></>), // ShieldAlert
  break: (<><circle cx="12" cy="12" r="9" /><rect x="9" y="9" width="6" height="6" rx="1" /></>), // CircleStop
  continue: (<><polygon points="5 4 15 12 5 20 5 4" /><line x1="19" y1="5" x2="19" y2="19" /></>), // SkipForward
  call: (<><polyline points="9 10 4 15 9 20" /><path d="M20 4v7a4 4 0 0 1-4 4H4" /></>), // CornerDownLeft-style call
  return: (<><polyline points="9 14 4 9 9 4" /><path d="M20 20v-7a4 4 0 0 0-4-4H4" /></>), // CornerUpLeft
  exit: (<><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" /><polyline points="16 17 21 12 16 7" /><line x1="21" y1="12" x2="9" y2="12" /></>), // LogOut
  // Data
  extract: (<><circle cx="6" cy="6" r="3" /><circle cx="6" cy="18" r="3" /><line x1="20" y1="4" x2="8.12" y2="15.88" /><line x1="14.47" y1="14.48" x2="20" y2="20" /><line x1="8.12" y1="8.12" x2="12" y2="12" /></>), // Scissors
  set: (<><line x1="5" y1="9" x2="19" y2="9" /><line x1="5" y1="15" x2="19" y2="15" /></>), // Equal / assignment
  parse: (<><path d="M8 3H7a2 2 0 0 0-2 2v5a2 2 0 0 1-2 2 2 2 0 0 1 2 2v5a2 2 0 0 0 2 2h1" /><path d="M16 3h1a2 2 0 0 1 2 2v5a2 2 0 0 0 2 2 2 2 0 0 0-2 2v5a2 2 0 0 1-2 2h-1" /></>), // Braces
  table: (<><rect x="3" y="3" width="18" height="18" rx="2" /><line x1="3" y1="9" x2="21" y2="9" /><line x1="3" y1="15" x2="21" y2="15" /><line x1="9" y1="3" x2="9" y2="21" /></>), // Table
  assert: (<><circle cx="12" cy="12" r="9" /><polyline points="8.5 12.5 11 15 16 9" /></>), // CircleCheck
  // Network
  ping: (<><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></>), // Activity
  dns: (<><circle cx="12" cy="12" r="9" /><path d="M2 12h20" /><path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z" /></>), // Globe
  port: (<><path d="m13 2-3 7h5l-3 7" /><path d="M5 12H3" /><path d="M21 12h-2" /></>), // PlugZap / EthernetPort-ish
  http: (<><circle cx="12" cy="12" r="9" /><path d="M2 12h20" /><path d="M12 3a14 14 0 0 1 0 18 14 14 0 0 1 0-18z" /><path d="m16 8 2 2-2 2" /></>), // Globe2 with arrow
  webhook: (<><path d="M18 16.98h-5.99c-1.1 0-1.95.94-2.48 1.9A4 4 0 0 1 2 17c.01-.7.2-1.4.57-2" /><path d="m6 17 3.13-5.78c.53-.97.1-2.18-.5-3.1a4 4 0 1 1 6.89-4.06" /><path d="m12 6 3.13 5.73C15.66 12.7 16.9 13 18 13a4 4 0 0 1 0 8" /></>), // Webhook
  oauth: (<><circle cx="8" cy="15" r="4" /><path d="M10.85 12.15 19 4" /><path d="m18 5 2 2" /><path d="m15 8 2 2" /></>), // KeyRound
  vault: (<><rect x="3" y="11" width="18" height="11" rx="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" /></>), // Lock
  // IO
  print: (<><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" /></>), // MessageSquare
  input: (<><path d="M5 4h1a3 3 0 0 1 3 3 3 3 0 0 1 3-3h1" /><path d="M13 20h-1a3 3 0 0 1-3-3 3 3 0 0 1-3 3H5" /><path d="M5 16H4a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2h1" /><path d="M13 8h7a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-7" /><path d="M9 7v10" /></>), // TextCursorInput
  choose: (<><path d="m3 17 2 2 4-4" /><path d="m3 7 2 2 4-4" /><line x1="13" y1="6" x2="21" y2="6" /><line x1="13" y1="12" x2="21" y2="12" /><line x1="13" y1="18" x2="21" y2="18" /></>), // ListChecks
  multi: (<><path d="m3 17 2 2 4-4" /><path d="m3 7 2 2 4-4" /><line x1="13" y1="6" x2="21" y2="6" /><line x1="13" y1="12" x2="21" y2="12" /><line x1="13" y1="18" x2="21" y2="18" /></>), // ListChecks (multi)
  confirm: (<><circle cx="12" cy="12" r="9" /><path d="M9.5 9a2.5 2.5 0 0 1 4.5 1.5c0 1.5-2 2-2 3" /><path d="M12 17h.01" /></>), // CircleHelp
  read: (<><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /><line x1="8" y1="13" x2="16" y2="13" /><line x1="8" y1="17" x2="13" y2="17" /></>), // FileText
  write: (<><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /><path d="m10 18 3-3-2-2-3 3v2z" /></>), // FileOutput / FilePen
  exists: (<><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h6" /><polyline points="14 2 14 8 20 8" /><circle cx="16" cy="16" r="3" /><line x1="20.5" y1="20.5" x2="18.1" y2="18.1" /></>), // FileSearch
  audio: (<><polygon points="11 5 6 9 2 9 2 15 6 15 11 19 11 5" /><path d="M15.54 8.46a5 5 0 0 1 0 7.07" /><path d="M19.07 4.93a10 10 0 0 1 0 14.14" /></>), // Volume2
  log: (<><path d="M8 3H7a2 2 0 0 0-2 2v15a1 1 0 0 0 1 1h13a2 2 0 0 0 2-2V8z" /><path d="M16 3v4a1 1 0 0 0 1 1h4" /><line x1="9" y1="13" x2="15" y2="13" /><line x1="9" y1="17" x2="15" y2="17" /></>), // ScrollText
  notify: (<><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" /><path d="M10.3 21a1.94 1.94 0 0 0 3.4 0" /></>), // Bell
  terminal: (<><rect x="3" y="4" width="18" height="16" rx="2" /><polyline points="7 9 10 12 7 15" /><line x1="13" y1="15" x2="17" y2="15" /></>), // SquareTerminal
  // Grid
  column: (<><rect x="3" y="3" width="18" height="18" rx="2" /><line x1="9" y1="3" x2="9" y2="21" /><line x1="15" y1="3" x2="15" y2="21" /></>), // Columns
  env: (<><path d="M20 7h-9" /><path d="M14 17H5" /><circle cx="17" cy="17" r="3" /><circle cx="7" cy="7" r="3" /></>), // Settings2-ish sliders
  // Timing
  wait: (<><circle cx="12" cy="12" r="9" /><polyline points="12 7 12 12 15 14" /></>), // Clock
  // Neutral fallback
  box: (<><path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16Z" /><path d="m3.3 7 8.7 5 8.7-5" /><path d="M12 22V12" /></>), // Boxes / box
};

export function BlockIcon({ name, size = 14 }: { name: string; size?: number }): JSX.Element {
  const geometry = ICONS[name] ?? ICONS.box;
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      style={{ flexShrink: 0, display: 'block' }}
    >
      {geometry}
    </svg>
  );
}
```

> NOTE: the exact `<path>` data above is the Lucide-style geometry to vendor; it is fine to copy the canonical Lucide path for each named glyph if a worker prefers pixel-perfect fidelity, **but the wrapping `<svg>` attrs (`stroke="currentColor"`, `fill="none"`, `strokeWidth={1.75}`) MUST stay exactly as written** — they are what make the icon gate-safe and category-tintable. Do NOT introduce any `#hex`/`rgb()` literal in this file.

- [ ] **Step 2: Confirm key coverage.** Cross-check `ICONS` against the registry: every distinct `def.icon` value (`ssh, term, sftp, if, for, while, switch, parallel, try, break, continue, call, return, exit, extract, set, parse, table, assert, ping, dns, port, http, webhook, oauth, vault, print, input, choose, multi, confirm, read, write, exists, audio, log, notify, terminal, column, env, wait`) must have an entry. `while` covers while+repeat; `set` covers set+sethistorylabel; `term` and `terminal` are intentionally distinct entries. The `box` key is the fallback only.

- [ ] **Step 3: Write the coverage spec** `FlowCanvas/e2e/flow-canvas-block-icon.spec.ts`. Mirror the `beforeEach` of `flow-canvas-token-sweep.spec.ts` (install capture → `goto('/')` → `waitForOutgoingMessage('ready')` → `clearOutgoingMessages`). This spec does NOT depend on Section 2 (it asserts the module/import compiles and renders standalone). Cases:
  - **(a) module renders a glyph:** in a `page.evaluate`, import is not available; instead assert via a one-node fixture once Section 2 lands. For Section 1 in isolation, assert the build compiles and add a placeholder `test.fixme(...)` for the DOM cases, OR (preferred) defer the DOM assertions of this spec to run after Task 4 wires the header. Keep the spec file with the import-compile smoke only until the header exists.

  > Ordering note: a `<BlockIcon>` only appears in the DOM after Task 4 renders it in the header. To keep Section 1 self-verifying without the header, this spec's DOM cases are written but marked `test.fixme` here and un-fixmed in Task 4 Step 5. The build-compile gate (Step 4) is the real Section-1 proof.

- [ ] **Step 4: Verify.**
  - `cd FlowCanvas && npm run build` → exits 0 (tsc compiles the new JSX/SVG; proves all 40 keys are valid TSX and the file type-checks).
  - `cd FlowCanvas && npm run test:e2e:parity` → unchanged green (the module is unreferenced so far → zero export impact).

- [ ] **Step 5: Commit.** `feat(flow-canvas): vendor Lucide stroke icons as inline BlockIcon.tsx (Decision #9, no runtime dep)`.

---

## Section 2: Premium Node Card Redesign (accent rail + surface body + icon header)

> The "accent-rail, not blob" restructure of `BaseBlock`. ALL changes are render-only inline styles + new `--fc-*` tokens. The rail is an absolutely-positioned **child** of the existing first-child container (NOT a wrapper, NOT a CSS border) so `block > div:first-child` stays the bordered/animated card that the reduced-motion and run-timing specs depend on, and so the rail never enters the box-shadow/glow/heat stack — preserving Wave 1 exec-state precedence byte-for-byte.

### Task 2: Add the node + category-icon tokens to `tokens.css` / `tokens.ts`

**Files:**
- Modify `FlowCanvas/src/styles/tokens.css` (append new tokens)
- Modify `FlowCanvas/src/utils/tokens.ts` (add `icon` to `CategoryVarSet`/`catVars`)
- Test: `flow-canvas-token-sweep.spec.ts` (must stay green), parity specs

- [ ] **Step 1: Append the node-surface + rail tokens to `tokens.css`** (inside `:root`, after the category block). Decision #4: this is the only place these colors may be authored.

```css
  /* ── Wave 2a: premium node card (accent rail, neutral body) ──────────────── */
  /* Neutral node body — ~surface-1 with a hint of category-neutral chroma. Replaces colors.bg as
     the idle fill so every node body reads neutral and the rail carries the category color. */
  --fc-node-surface: oklch(22% 0.02 275);
  /* Neutral 1px outline now that the category color lives on the rail (softer than the old 2px). */
  --fc-node-border: oklch(34% 0.03 275);
  /* Single source for the accent-rail width AND the header/preview left-padding calc. 4px is a
     one-line bump if the 3px rail sub-pixel-thins at low zoom. */
  --fc-rail-w: 3px;

  /* Per-category ICON tint = the category border/rail hue (full chroma), so the header glyph matches
     the rail rather than the softer body text. currentColor on <BlockIcon> inherits this. */
  --fc-cat-ssh-icon: var(--fc-cat-ssh-border);
  --fc-cat-control-flow-icon: var(--fc-cat-control-flow-border);
  --fc-cat-data-icon: var(--fc-cat-data-border);
  --fc-cat-network-icon: var(--fc-cat-network-border);
  --fc-cat-io-icon: var(--fc-cat-io-border);
  --fc-cat-grid-icon: var(--fc-cat-grid-border);
  --fc-cat-timing-icon: var(--fc-cat-timing-border);
```

  Leave `--fc-fs-header: 13px;` as-is (already defined in Wave 1).

- [ ] **Step 2: Add `icon` to the category var set in `tokens.ts`.** This makes `colors.icon` the single source for the header tint:

```ts
export interface CategoryVarSet {
  border: string;
  bg: string;
  badge: string;
  badgeText: string;
  text: string;
  icon: string;
}

function catVars(c: string): CategoryVarSet {
  return {
    border: `var(--fc-cat-${c}-border)`,
    bg: `var(--fc-cat-${c}-bg)`,
    badge: `var(--fc-cat-${c}-badge)`,
    badgeText: `var(--fc-cat-${c}-badge-text)`,
    text: `var(--fc-cat-${c}-text)`,
    icon: `var(--fc-cat-${c}-icon)`,
  };
}
```

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → 0 (the new `icon` field type-checks for every `CategoryVarSet` consumer).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (new tokens resolve to OKLCH; no hex introduced).
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 4: Commit.** `feat(flow-canvas): add --fc-node-surface/--fc-node-border/--fc-rail-w + per-category icon tokens`.

### Task 3: Rebuild `BaseBlock` — rail, neutral surface, icon chip, softened badge

**Files:**
- Modify `FlowCanvas/src/nodes/BaseBlock.tsx`
- Test: `flow-canvas-node-redesign.spec.ts` (created in Task 4), token-sweep, run-timing, reduced-motion, parity

- [ ] **Step 1: Add the `BlockIcon` import** at the top of `BaseBlock.tsx`, beside the existing `mix` import:

```ts
import { mix } from '../utils/tokens';
import { BlockIcon } from './BlockIcon';
```

- [ ] **Step 2: Neutralize the body + soften the border (lines 99-110).** Replace the `background` and `border` of `containerStyle` ONLY — leave `borderRadius`, `minWidth`/`maxWidth`, `overflow`, `opacity`, `boxShadow`, `transition`, `position` exactly as-is (the `boxShadow` line is the exec/heat stack — DO NOT touch it):

```ts
  const containerStyle: CSSProperties = {
    background: isDisabled ? 'var(--fc-surface-disabled)' : 'var(--fc-node-surface)',
    border: `1px solid ${selected ? 'var(--fc-border-selected)' : isDisabled ? 'var(--fc-border-muted)' : 'var(--fc-node-border)'}`,
    borderRadius: 8,
    minWidth: isChild ? 160 : 180,
    maxWidth: isChild ? 260 : 280,
    overflow: 'hidden',
    opacity: isDisabled ? 0.5 : isChild ? 0.95 : 1,
    boxShadow: heatTint ? `0 0 0 3px ${heatTint}, ${existingBoxShadow}` : existingBoxShadow,
    transition: 'box-shadow 0.2s, border-color 0.2s, opacity 0.2s',
    position: 'relative',
  };
```

  > The `background` change from `colors.bg` → `var(--fc-node-surface)` and `border` from `2px solid ${...colors.border}` → `1px solid ${...var(--fc-node-border)}` are the ONLY edits in this block. The running-pulse `if (execState === 'running') { containerStyle.animation = 'exec-pulse …'; }` immediately below stays unchanged.

- [ ] **Step 3: Define `railStyle` and the icon-chip style** right after the running-pulse block (before `headerStyle`). The rail uses `colors.border` (a category var) so it auto-tints per category with zero new per-category color:

```ts
  // Accent rail: an absolutely-positioned child (NOT a CSS border) so it never participates in the
  // exec/heat boxShadow stack and survives crisp at low zoom. Category identity lives here + the icon.
  const railStyle: CSSProperties = {
    position: 'absolute',
    left: 0,
    top: 0,
    bottom: 0,
    width: 'var(--fc-rail-w)',
    background: isDisabled ? 'var(--fc-border-muted)' : colors.border,
    borderTopLeftRadius: 8,
    borderBottomLeftRadius: 8,
    pointerEvents: 'none',
  };

  // Category-tinted icon chip. color tints the stroke (currentColor); a faint category wash sits
  // behind it. mix() is the gate-safe color-mix helper — no new per-category token needed.
  const iconChipStyle: CSSProperties = {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    width: 18,
    height: 18,
    flexShrink: 0,
    borderRadius: 4,
    color: isDisabled ? 'var(--fc-text-faint)' : colors.icon,
    background: isDisabled ? 'transparent' : mix(colors.border, def.isContainer ? 20 : 14),
  };
```

- [ ] **Step 4: Pad the header + preview left by the rail width (lines 117-124 and 218-230).** Change `headerStyle.padding` and the preview block's `padding` so content clears the rail. Header:

```ts
  const headerStyle: CSSProperties = {
    padding: '4px 8px',
    paddingLeft: 'calc(8px + var(--fc-rail-w))',
    borderBottom: `1px solid ${mix(colors.border, 20)}`,
    display: 'flex',
    alignItems: 'center',
    gap: 6,
    fontSize: 'var(--fc-fs-header)',
  };
```

  Preview block — change its inline `padding: '4px 8px'` to add `paddingLeft: 'calc(8px + var(--fc-rail-w))'`:

```tsx
        <div style={{
          padding: '4px 8px',
          paddingLeft: 'calc(8px + var(--fc-rail-w))',
          fontFamily: 'monospace',
          fontSize: 11,
          color: isDisabled ? 'var(--fc-text-disabled)' : colors.text,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}>
          {previewText}
        </div>
```

- [ ] **Step 5: Soften the badge to an outlined chip (lines 126-136).** The rail + icon now own the category color, so de-emphasize the loud filled badge:

```ts
  const badgeStyle: CSSProperties = {
    background: 'transparent',
    color: isDisabled ? 'var(--fc-text-secondary)' : colors.text,
    fontSize: 10,
    fontWeight: 700,
    padding: '2px 6px',
    borderRadius: 3,
    border: `1px solid ${mix(colors.border, 40)}`,
    textTransform: 'uppercase',
    letterSpacing: '0.5px',
    flexShrink: 0,
  };
```

  > MICRO-DECISION (resolve before coding): outlined (above) vs. the original filled `colors.badge`/`colors.badgeText`. Default = outlined (rail+icon carry color). Both are token-safe; to keep filled, leave `badgeStyle` unchanged from Wave 1. The plan author should pick one and note it; the spec in Task 4 must assert whichever variant ships.

- [ ] **Step 6: Render the rail + icon chip in the JSX.** Add the rail as the FIRST child inside the container `<div>` (before the Top handle), and the icon chip as the FIRST header element after the breakpoint gutter (before the badge). The return block becomes:

```tsx
  return (
    <div style={containerStyle}>
      {/* Accent rail (category identity; absolutely positioned, out of the boxShadow stack) */}
      <span style={railStyle} data-testid="node-rail" />

      {/* Input handle (top) */}
      <Handle
        type="target"
        position={Position.Top}
        style={{ background: colors.border, width: 8, height: 8, border: 'none' }}
      />

      {/* Header */}
      <div style={headerStyle}>
        {/* Breakpoint gutter */}
        {!isChild && (
          <span
            onClick={handleBreakpointToggle}
            style={{
              width: 10, height: 10, borderRadius: '50%',
              background: hasBreakpoint ? 'var(--fc-state-error)' : 'transparent',
              border: hasBreakpoint ? 'none' : '1px solid var(--fc-border-subtle)',
              flexShrink: 0,
              cursor: 'pointer',
              boxShadow: hasBreakpoint ? '0 0 4px var(--fc-glow-error)' : 'none',
              transition: 'background 0.15s',
            }}
            title="Toggle breakpoint"
          />
        )}

        {/* Category-tinted icon chip */}
        <span style={iconChipStyle}>
          <BlockIcon name={def.icon} />
        </span>

        <span style={badgeStyle}>{def.type}</span>
        <span style={{
          color: isDisabled ? 'var(--fc-text-faint)' : 'var(--fc-text)',
          fontSize: 12,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          textDecoration: isDisabled ? 'line-through' : 'none',
        }}>
          {blockData.label || def.label}
        </span>
        {execIndicator}
      </div>
```

  **DO NOT change:** the breakpoint gutter `onClick`/styles, the label span, `execIndicator`, the preview gate, or any of the four `Handle` blocks below (Top already shown; Bottom 233-237, false-Right 240-250, continue-Left 255-273 stay byte-identical). **DO NOT change** lines 78-115 (`heatTint`, `existingBoxShadow`, the `exec-pulse` animation). The Unknown-type early return at line 60 stays (`<BlockIcon>` is only reached for known `def`s, but the `box` fallback covers any stray key defensively).

- [ ] **Step 7: Verify (gated by Task 4's spec; run what exists now).**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts` → green (the rail/icon/wash use `colors.*` vars + `mix()` only; no hex, no `var()`+alpha concat). **If this fails on a malformed concat, the bug is a literal color in the new styles — fix it as a token, never inline.**
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-run-timing.spec.ts` → green (rail is a child span; box-shadow assertions unaffected — exec precedence + heatmap ring + duration badge unregressed).
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-reduced-motion.spec.ts` → green (first-child div still carries `exec-pulse`; no new always-on animation on the icon).
  - `cd FlowCanvas && npm run test:e2e:parity` → green (no `node.data` write → export byte-identical).

- [ ] **Step 8: Commit.** `feat(flow-canvas): premium accent-rail node card (neutral surface + category icon chip + softened badge)`.

### Task 4: Node-redesign Playwright spec + un-fixme the icon spec

**Files:**
- Create `FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts`
- Modify `FlowCanvas/e2e/flow-canvas-block-icon.spec.ts` (un-fixme DOM cases from Section 1)
- Test: the two specs above + parity

- [ ] **Step 1: Write `flow-canvas-node-redesign.spec.ts`.** Mirror `flow-canvas-token-sweep.spec.ts` setup + `createSshBlockFixture()` (copy the inline fixture). Use the probe-div var-resolution pattern (token-sweep lines 60-68). Cases:

```ts
import { expect, test } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

function createSshBlockFixture(): GraphFixture {
  return {
    nodes: [{ id: 'node-ssh', type: 'block', position: { x: 140, y: 120 },
      data: { blockType: 'send', label: 'Send', props: { command: 'echo hello' } } }],
    edges: [],
  };
}
function createUnknownBlockFixture(): GraphFixture {
  return {
    nodes: [{ id: 'node-x', type: 'block', position: { x: 140, y: 120 },
      data: { blockType: '__nope__', label: 'X', props: {} } }],
    edges: [],
  };
}
async function resolveVar(page, name: string): Promise<string> {
  return page.evaluate((n) => {
    const probe = document.createElement('div');
    probe.style.color = `var(${n})`;
    document.body.appendChild(probe);
    const v = getComputedStyle(probe).color;
    probe.remove();
    return v;
  }, name);
}

test.describe('Flow Canvas Node Redesign', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('body neutralizes to --fc-node-surface (not the category bg)', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const container = page.locator('.react-flow__node[data-id="node-ssh"] > div').first();
    const bg = await container.evaluate((el) => getComputedStyle(el as HTMLElement).backgroundColor);
    expect(bg).toBe(await resolveVar(page, '--fc-node-surface'));
  });

  test('accent rail renders and resolves to the category border token', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const rail = page.locator('.react-flow__node[data-id="node-ssh"] [data-testid="node-rail"]');
    await expect(rail).toBeVisible();
    const railBg = await rail.evaluate((el) => getComputedStyle(el as HTMLElement).backgroundColor);
    expect(railBg).toBe(await resolveVar(page, '--fc-cat-ssh-border'));
  });

  test('header renders a category-tinted icon svg', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const svg = page.locator('.react-flow__node[data-id="node-ssh"] svg').first();
    await expect(svg).toBeVisible();
    expect(await svg.getAttribute('stroke')).toBe('currentColor');
    const iconColor = await svg.evaluate((el) => getComputedStyle(el as HTMLElement).color);
    expect(iconColor).toBe(await resolveVar(page, '--fc-cat-ssh-icon'));
  });

  test('unknown blockType renders the fallback glyph without throwing', async ({ page }) => {
    await loadGraphFixture(page, createUnknownBlockFixture());
    // BaseBlock early-returns the Unknown div for an unregistered blockType; assert it renders
    // (no crash) — the fallback proof for BlockIcon lives in the registry-coverage spec.
    await expect(page.locator('.react-flow__node[data-id="node-x"]')).toBeVisible();
  });

  test('exec-state precedence + heat ring unregressed (rail is a child, not the shadow)', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const container = page.locator('.react-flow__node[data-id="node-ssh"] > div').first();
    // running → exec-pulse animation present
    const { postHostMessage } = await import('./support/harness');
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-ssh', state: 'running' });
    await expect.poll(() => container.evaluate((el) =>
      Number.parseFloat(getComputedStyle(el as HTMLElement).animationDuration))).toBeGreaterThan(0);
  });
});
```

  > If the badge MICRO-DECISION (Task 3 Step 5) ships outlined, optionally add an assertion that the badge `backgroundColor` is transparent and its `borderTopColor` resolves via `mix(--fc-cat-ssh-border, 40)`. Keep it lenient (presence of a border) to avoid brittleness.

- [ ] **Step 2: Un-fixme the icon-coverage DOM cases** in `flow-canvas-block-icon.spec.ts`. Now that the header renders `<BlockIcon>`, assert per-category coverage. Minimal viable form — load one fixture per category (or iterate a small representative set) and assert each node renders a non-empty `<svg>`:

```ts
import { expect, test } from '@playwright/test';
import type { GraphFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

const SAMPLES = [
  { id: 'n-ssh', blockType: 'send' },       // ssh
  { id: 'n-if', blockType: 'if' },          // control-flow
  { id: 'n-set', blockType: 'set' },        // data
  { id: 'n-ping', blockType: 'ping' },      // network
  { id: 'n-print', blockType: 'print' },    // io
  { id: 'n-col', blockType: 'updatecolumn' }, // grid
  { id: 'n-wait', blockType: 'wait' },      // timing
];
function fixtureFor(id: string, blockType: string): GraphFixture {
  return { nodes: [{ id, type: 'block', position: { x: 120, y: 120 },
    data: { blockType, label: blockType, props: {} } }], edges: [] };
}

test.describe('Flow Canvas Block Icons', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  for (const s of SAMPLES) {
    test(`renders an svg icon for ${s.blockType}`, async ({ page }) => {
      await loadGraphFixture(page, fixtureFor(s.id, s.blockType));
      const svg = page.locator(`.react-flow__node[data-id="${s.id}"] svg`).first();
      await expect(svg).toBeVisible();
      const childCount = await svg.evaluate((el) => el.childElementCount);
      expect(childCount).toBeGreaterThan(0); // non-empty geometry, never a blank glyph
    });
  }
});
```

  Confirm the blockType keys exist in the registry (`updatecolumn` is the grid block whose icon is `column`; `wait` icon is `wait`). Adjust ids/blockTypes to real registry `type` values if any differ.

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-node-redesign.spec.ts e2e/flow-canvas-block-icon.spec.ts` → all green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 4: Commit.** `test(flow-canvas): node-redesign + icon-coverage specs (rail/surface/icon/exec-precedence)`.

### Task 5: StartNode rail parity + optional Palette icons

**Files:**
- Modify `FlowCanvas/src/nodes/StartNode.tsx` (add rail; keep gradient + source-only handle)
- Modify `FlowCanvas/src/panels/Palette.tsx` (optional: icon before label)
- Test: token-sweep, connection-guards, parity

- [ ] **Step 1: Add the rail to StartNode.** Keep the green-gradient `containerStyle` and the source-only `Handle` untouched. Add a `railStyle` (using the start accent) and pad the header left. Insert the rail span as the first child inside the container div, and add `position: 'relative'` to `containerStyle` if not already implied (it isn't set today — add it):

```ts
  const containerStyle: CSSProperties = {
    background: 'linear-gradient(135deg, var(--fc-start-grad-from), var(--fc-start-grad-to))',
    border: `2px solid ${selected ? 'var(--fc-border-selected)' : 'var(--fc-start-accent)'}`,
    borderRadius: 8,
    minWidth: 260,
    maxWidth: 300,
    overflow: 'hidden',
    boxShadow: selected ? '0 0 12px var(--fc-glow-selected)' : '0 0 12px var(--fc-glow-start)',
    transition: 'box-shadow 0.2s, border-color 0.2s',
    position: 'relative',
  };

  const railStyle: CSSProperties = {
    position: 'absolute', left: 0, top: 0, bottom: 0,
    width: 'var(--fc-rail-w)', background: 'var(--fc-start-accent)',
    borderTopLeftRadius: 8, borderBottomLeftRadius: 8, pointerEvents: 'none',
  };
```

  In the JSX, add `<span style={railStyle} />` as the first child of the container `<div>`, and add `paddingLeft: 'calc(10px + var(--fc-rail-w))'` to the header div's existing `padding: '6px 10px'`. **DO NOT add a target Handle** — Start is source-only (invariant guarded by `flow-canvas-connection-guards.spec.ts`).

- [ ] **Step 2 (optional): Add the icon to Palette.** In `PaletteItem`, render `<BlockIcon name={def.icon} size={14} />` before `<span>{def.label}</span>`, reusing the existing `gap:6` row. Import `BlockIcon` and set the row `color` to `colors.icon` so the glyph tints to the category (the row already sets `color: colors.text` — wrap the icon in a span with `color: colors.icon` to tint it distinctly while the label keeps `colors.text`):

```tsx
import { BlockIcon } from '../nodes/BlockIcon';
// ...inside the PaletteItem return, before the label span:
      <span style={{ color: colors.icon, display: 'flex', flexShrink: 0 }}>
        <BlockIcon name={def.icon} size={14} />
      </span>
      <span>{def.label}</span>
```

  This is the second presentational consumer; include only if it does not complicate the sweep. It is purely cosmetic and export-neutral.

- [ ] **Step 3: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts e2e/flow-canvas-connection-guards.spec.ts` → green (rail tokens resolve; Start still source-only).
  - `cd FlowCanvas && npm run test:e2e:parity` → green.

- [ ] **Step 4: Commit.** `feat(flow-canvas): StartNode rail parity (keeps gradient + source-only handle); optional palette icons`.

---

## Section 3: Branch Bands & Containment (round-trip-safe, gated)

> The riskiest 2a piece (layout-coupled, flat-sibling geometry) — ship it LAST, after the neutralized node surface lands so bands read cleanly, and gate it behind a `uiSlice` toggle. Bands are derived from existing `_isChildOf`/`_stepPath` metadata and rendered in a separate `ViewportPortal` layer (exactly like the heatmap heatTint: render-time only, never written to `node.data`). This piece also folds in the mandatory `Properties.tsx` raw-hex → token harmonization so the band's gate-fixture extension does not turn the no-hex gate red.

### Task 6: Branch tokens + `branchBands.ts` derivation + `uiSlice` toggle

**Files:**
- Modify `FlowCanvas/src/styles/tokens.css` (append `--fc-branch-*`)
- Create `FlowCanvas/src/utils/branchBands.ts`
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` (add `branchBandsEnabled` + `toggleBranchBands`)
- Test: build clean, parity

- [ ] **Step 1: Append the branch tokens to `tokens.css`** (inside `:root`). Each aliases an existing state/accent hue (Decision #4: authored only here):

```css
  /* ── Wave 2a: branch containment bands (alias existing state/accent hues) ── */
  --fc-branch-then: var(--fc-state-success);
  --fc-branch-try: var(--fc-state-success);
  --fc-branch-else: var(--fc-state-error);
  --fc-branch-catch: var(--fc-state-error);
  --fc-branch-default: var(--fc-state-error);
  --fc-branch-elif: var(--fc-state-warning);
  --fc-branch-do: var(--fc-state-warning);
  --fc-branch-case: var(--fc-state-warning);
  --fc-branch-finally: var(--fc-accent);
  --fc-branch-parallel: var(--fc-cat-network-border);
  --fc-branch-fallback: var(--fc-text-disabled);
```

- [ ] **Step 2: Write `branchBands.ts`.** Pure derivation + the `branchColorVar` helper shared by the band layer AND `Properties.tsx`. `branchKey` is parsed from `_stepPath` (the YAML-aligned signal C# itself prefers), falling back to `_branchLabel` only for the color-var lookup:

```ts
// FlowCanvas/src/utils/branchBands.ts
// Round-trip-safe branch-band derivation. Reads ONLY existing transient metadata
// (_isChildOf, _stepPath, _branchLabel) + node.position; writes nothing to node.data.
import type { Node } from '@xyflow/react';

export interface BranchBand {
  id: string;
  parentId: string;
  branchKey: string;
  x: number;
  y: number;
  width: number;
  height: number;
  colorVar: string;
}

const BRANCH_KEYS = [
  'then', 'else', 'elif', 'do', 'try', 'catch', 'finally', 'case', 'default', 'parallel',
] as const;

/** Map a branch key (or branch label) to its --fc-branch-* token. Single source of color truth
 *  shared by the band layer and the Properties branch chip (replaces the raw _branchColor hex). */
export function branchColorVar(key: string | undefined): string {
  const k = (key ?? '').toLowerCase();
  for (const known of BRANCH_KEYS) {
    if (k === known || k.startsWith(`${known}:`) || k.startsWith(`${known} `)) {
      return `var(--fc-branch-${known})`;
    }
  }
  return 'var(--fc-branch-fallback)';
}

/** Parse the branch segment from a child _stepPath relative to its parent. e.g.
 *  "steps/3/then/0" → "then", "steps/1/cases/0/do/0" → "case", "steps/2/else/0" → "else". */
function branchKeyFromStepPath(stepPath: string | undefined, branchLabel: string | undefined): string {
  if (stepPath) {
    const segs = stepPath.split('/');
    for (let i = segs.length - 1; i >= 0; i--) {
      const s = segs[i].toLowerCase();
      if (s === 'cases') return 'case';
      for (const known of BRANCH_KEYS) {
        if (s === known) return known;
      }
    }
  }
  // Fall back to the importer's display label (lowercased first word).
  return (branchLabel ?? 'then').split(/[:\s]/)[0].toLowerCase();
}

const NODE_W = 280; // max width from BaseBlock containerStyle (non-child 280 / child 260)
const NODE_H = 64;  // header + preview estimate
const PAD = 10;

export function computeBranchBands(nodes: Node[]): BranchBand[] {
  const groups = new Map<string, { parentId: string; branchKey: string; nodes: Node[] }>();
  for (const n of nodes) {
    const props = (n.data as { props?: Record<string, unknown> } | undefined)?.props;
    const parentId = props?.['_isChildOf'] as string | undefined;
    if (!parentId) continue;
    const branchKey = branchKeyFromStepPath(
      props?.['_stepPath'] as string | undefined,
      props?.['_branchLabel'] as string | undefined,
    );
    const groupId = `${parentId}::${branchKey}`;
    if (!groups.has(groupId)) groups.set(groupId, { parentId, branchKey, nodes: [] });
    groups.get(groupId)!.nodes.push(n);
  }

  const bands: BranchBand[] = [];
  for (const [groupId, g] of groups) {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
    for (const n of g.nodes) {
      minX = Math.min(minX, n.position.x);
      minY = Math.min(minY, n.position.y);
      maxX = Math.max(maxX, n.position.x + NODE_W);
      maxY = Math.max(maxY, n.position.y + NODE_H);
    }
    bands.push({
      id: groupId,
      parentId: g.parentId,
      branchKey: g.branchKey,
      x: minX - PAD,
      y: minY - PAD,
      width: (maxX - minX) + PAD * 2,
      height: (maxY - minY) + PAD * 2,
      colorVar: branchColorVar(g.branchKey),
    });
  }
  return bands;
}
```

- [ ] **Step 3: Add `branchBandsEnabled` to `uiSlice`** (mirror `heatmapEnabled`/`toggleHeatmap` at lines 26/81/118-123). Interface: `branchBandsEnabled: boolean; toggleBranchBands: () => void;`. Initial `branchBandsEnabled: true`. Implementation (transient — no host message needed; it is a pure view preference for v1, unlike heatmap which persists):

```ts
toggleBranchBands: () => set((s) => ({ branchBandsEnabled: !s.branchBandsEnabled })),
```

  > MICRO-DECISION: v1 keeps the toggle transient (default-on). Persisting it through `AppConfiguration.WindowState` (like heatmap) is a trivial follow-on if requested — out of 2a scope to keep the C# surface untouched.

- [ ] **Step 4: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (nothing rendered yet; pure additive state + util).

- [ ] **Step 5: Commit.** `feat(flow-canvas): branch tokens + computeBranchBands derivation + uiSlice.branchBandsEnabled`.

### Task 7: `BranchBandsLayer` + mount + Properties hex→token harmonization

**Files:**
- Create `FlowCanvas/src/nodes/BranchBandsLayer.tsx`
- Modify `FlowCanvas/src/App.tsx` (mount the layer inside `<ReactFlow>`)
- Modify `FlowCanvas/src/panels/Properties.tsx` (replace raw `_branchColor` with `branchColorVar`)
- Test: `flow-canvas-branch-bands.spec.ts`, token-sweep, parity

- [ ] **Step 1: Write `BranchBandsLayer.tsx`.** Renders one band div per `BranchBand` inside a `ViewportPortal` (canvas-space, auto pan/zoom), behind nodes, `pointer-events:none`. No glass:

```tsx
// FlowCanvas/src/nodes/BranchBandsLayer.tsx
import { ViewportPortal } from '@xyflow/react';
import { useFlowStore } from '../stores/useFlowStore';
import { computeBranchBands } from '../utils/branchBands';
import { mix } from '../utils/tokens';

export default function BranchBandsLayer() {
  const nodes = useFlowStore((s) => s.nodes);
  const enabled = useFlowStore((s) => s.branchBandsEnabled);
  if (!enabled) return null;
  const bands = computeBranchBands(nodes);
  if (bands.length === 0) return null;

  return (
    <ViewportPortal>
      {bands.map((b) => (
        <div
          key={b.id}
          data-testid="branch-band"
          data-branch={b.branchKey}
          style={{
            position: 'absolute',
            transform: `translate(${b.x}px, ${b.y}px)`,
            width: b.width,
            height: b.height,
            background: mix(b.colorVar, 8),
            borderLeft: `3px solid ${b.colorVar}`,
            borderRadius: 8,
            pointerEvents: 'none',
            zIndex: -1, // behind .react-flow__node
          }}
        />
      ))}
    </ViewportPortal>
  );
}
```

  > `zIndex: -1` inside the viewport portal places bands beneath nodes; if the WebView2 stacking context renders them above, drop to a sibling negative z or set the node layer's z explicitly. Verify visually + via the pointer-events test (Step 4) which is the real guard.

- [ ] **Step 2: Mount the layer in `App.tsx`.** Import it and render `<BranchBandsLayer />` as the FIRST child inside `<ReactFlow>` (before `<Controls>`), so it paints behind the controls/minimap/background chrome and (via `zIndex:-1`) behind nodes:

```tsx
import BranchBandsLayer from './nodes/BranchBandsLayer';
// ...inside <ReactFlow ...>:
            >
              <BranchBandsLayer />
              <Controls ... />
              <MiniMap ... />
              <Background ... />
```

- [ ] **Step 3: Harmonize `Properties.tsx` (remove the last raw hex).** Replace the raw `_branchColor` consumption (lines ~1627, 1735, 1737, 1739) with `branchColorVar(branchKey)`. Derive `branchKey` from `_stepPath` (preferred) or fall back to `_branchLabel`:

```ts
import { branchColorVar } from '../utils/branchBands';
// near line 1626-1627, replace `branchColor`:
  const branchLabel = blockData.props?.['_branchLabel'] as string | undefined;
  const branchStepPath = blockData.props?.['_stepPath'] as string | undefined;
  const branchTint = branchColorVar(
    branchStepPath
      ? // reuse the same parse as the band layer via the label as a cheap proxy when present
        (branchLabel ?? branchStepPath)
      : branchLabel,
  );
```

  Then in the chip JSX (lines ~1735/1737/1739) use `branchTint` instead of the raw hex:

```tsx
          background: mix(branchTint, 8),
          borderRadius: 4,
          borderLeft: `2px solid ${branchTint}`,
        }}>
          <span style={{ fontSize: 10, color: branchTint, fontWeight: 600, textTransform: 'uppercase' }}>
            {branchLabel}
          </span>
```

  > This removes the `_branchColor` read entirely. `_branchColor` stays on `node.data.props` (it is importer metadata — do NOT delete it, that would be a `node.data` mutation); it is simply no longer consumed for styling. The chip now references `var(--fc-branch-*)` only — gate-clean.

- [ ] **Step 4: Write `flow-canvas-branch-bands.spec.ts`.** Mirror `flow-canvas-interactions.spec.ts` setup; load `createImportedChildEditingFixture()`. Cases:

```ts
import { expect, test } from '@playwright/test';
import { createImportedChildEditingFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages, installHostMessageCapture, loadGraphFixture, waitForOutgoingMessage,
} from './support/harness';

async function resolveVar(page, name: string): Promise<string> {
  return page.evaluate((n) => {
    const probe = document.createElement('div');
    probe.style.color = `var(${n})`;
    document.body.appendChild(probe);
    const v = getComputedStyle(probe).color;
    probe.remove();
    return v;
  }, name);
}

test.describe('Flow Canvas Branch Bands', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createImportedChildEditingFixture());
    await expect(page.locator('.react-flow__node[data-id="then-1"]')).toBeVisible();
  });

  test('renders a then-branch band behind the child', async ({ page }) => {
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    await expect(band).toBeVisible();
    const borderColor = await band.evaluate((el) => getComputedStyle(el as HTMLElement).borderLeftColor);
    expect(borderColor).toBe(await resolveVar(page, '--fc-branch-then'));
  });

  test('band is pointer-events:none (does not capture node drag)', async ({ page }) => {
    const band = page.locator('[data-testid="branch-band"][data-branch="then"]');
    const pe = await band.evaluate((el) => getComputedStyle(el as HTMLElement).pointerEvents);
    expect(pe).toBe('none');
  });

  test('toggling branchBandsEnabled hides the layer', async ({ page }) => {
    await page.evaluate(() => (window as any).__store?.getState?.().toggleBranchBands?.());
    // If no test hook exists, drive the Toolbar toggle instead (add one or assert via store).
    await expect(page.locator('[data-testid="branch-band"]')).toHaveCount(0);
  });
});
```

  > The toggle test needs a way to flip `branchBandsEnabled`. If the codebase exposes a store test hook, use it; otherwise add a small Toolbar button (mirroring the heatmap toggle) and drive it by role/name. Pick one and make the test match.

- [ ] **Step 5: Extend the token-sweep gate to cover the branch child.** In `flow-canvas-token-sweep.spec.ts`, the existing `no raw hex` test mounts only the ssh fixture. Add a sibling test that loads `createImportedChildEditingFixture()` AND selects `then-1` so `Properties.tsx` renders the branch chip, then runs the same hex/malformed scan — proving the chip emits only `var(--fc-branch-*)`. This test would FAIL against the old raw `_branchColor` hex, locking in the harmonization.

- [ ] **Step 6: Verify.**
  - `cd FlowCanvas && npm run build` → 0.
  - `cd FlowCanvas && npx playwright test e2e/flow-canvas-branch-bands.spec.ts e2e/flow-canvas-token-sweep.spec.ts` → green.
  - `cd FlowCanvas && npm run test:e2e:parity` → green (bands are a `ViewportPortal` layer + transient flag; no `node.data` write; `_branchColor` still present but unconsumed → export byte-identical).

- [ ] **Step 7: Commit.** `feat(flow-canvas): branch-band ViewportPortal layer + Properties hex→token harmonization`.

---

## Section 4: Integration Verification + Dist Rebuild

### Task 8: Full-suite verification + embedded dist rebuild

**Files:**
- Test: full Playwright suite + parity + dist gate; `dotnet build SSH_Helper.sln`

- [ ] **Step 1: Full e2e suite.** `cd FlowCanvas && npm run test:e2e` → all green (the new node-redesign, block-icon, branch-bands specs + every pre-existing spec).

- [ ] **Step 2: Parity gate (round-trip proof).** `cd FlowCanvas && npm run test:e2e:parity` → green. Because no `node.data`/`exportGraph.ts`/`FlowCanvasBridge.cs` change occurred, `getGraphSnapshot` and the execute-canvas/YAML payload are byte-identical before/after every Wave 2a change.

- [ ] **Step 3: Dist build gate (proves inlined SVGs survive the production single-asset build).** `cd FlowCanvas && npm run test:e2e:dist` → green (runs `npm run build` then the preview-config Playwright run against the built `dist/`; confirms the vendored `<svg>` icons inline into the production bundle with zero runtime network).

- [ ] **Step 4: Embedded rebuild.** `dotnet build SSH_Helper.sln` → 0 (runs the `BuildFlowCanvas` MSBuild target → rebuilds `FlowCanvas/dist/` → re-embeds it as assembly resources so the WinForms host ships the redesigned canvas).

- [ ] **Step 5: Manual smoke (fresh-eyes pass).** Launch the app, open Flow Canvas, import a script with all block categories + a container (if/foreach/try). Confirm: every node shows its category icon in the header; the 3px accent rail reads the category color; bodies are neutral surface; exec a run and confirm the running pulse / success glow / heatmap ring / duration badge / breakpoint dot all behave as in Wave 1; branch bands frame the container children. No glass/backdrop-filter on any node (toolbar/panels only).

- [ ] **Step 6: Commit.** `chore(flow-canvas): rebuild embedded dist for Wave 2a node & icon redesign`.

---

## Wave 2a Exit Criteria

- [ ] `cd FlowCanvas && npm run build` exits 0 (tsc + vite clean; all ~40 vendored icon keys type-check).
- [ ] Embedded `FlowCanvas/dist/` rebuilt and re-embedded via `dotnet build SSH_Helper.sln` (exit 0).
- [ ] All 36 block types (41 registry entries) render their correct Lucide stroke icon in the node header; unknown/future keys render the `box` fallback without throwing (`flow-canvas-block-icon.spec.ts` green).
- [ ] Accent-rail cards render: body neutralized to `var(--fc-node-surface)`, 3px category rail (`--fc-rail-w`), category-tinted icon chip, softened badge (`flow-canvas-node-redesign.spec.ts` green).
- [ ] Wave 1 behavior UNREGRESSED: exec-state precedence (running pulse / success-error glow / selection / heatmap ring), duration badge, breakpoint gutter, all four handles (Top/Bottom/false-Right/continue-Left), child + disabled styling, and the `fc-reduced-motion` kill switch (`flow-canvas-run-timing.spec.ts` + `flow-canvas-reduced-motion.spec.ts` green; rail is a child span, not part of the box-shadow stack).
- [ ] NO hardcoded hex/rgba outside `tokens.css`/`tokens.ts`; the last raw leak (`Properties.tsx` `_branchColor`) is replaced by `branchColorVar` → `var(--fc-branch-*)` (`flow-canvas-token-sweep.spec.ts` no-hex + no-malformed-concat gate green, including the new branch-child case).
- [ ] Locked identity honored: ACCENT RAIL not blob (category color = rail + icon tint, neutral body); NO glass/backdrop-filter on any node; glow reserved for active/selected/exec/breakpoint states.
- [ ] Branch bands render behind container children from existing `_isChildOf`/`_stepPath` metadata via a `ViewportPortal` layer, `pointer-events:none`, gated by `uiSlice.branchBandsEnabled` (`flow-canvas-branch-bands.spec.ts` green) — with NO export change.
- [ ] Round-trip unaffected: `npm run test:e2e:parity` stays green (preset-parity + preset-negative + gesture-smoke + connection-guards); `getGraphSnapshot` byte-identical before/after; no `node.data` / `exportGraph.ts` / `FlowCanvasBridge.cs` change.
- [ ] `npm run test:e2e:dist` green — vendored inline SVGs survive the production single-asset build with zero runtime network (offline WebView2 safe).
- [ ] NO new npm runtime dependency added (icons hand-vendored per Decision #9); Framer Motion / `motion` DEFERRED to Wave 2b (2a is fully static).
- [ ] Full `npm run test:e2e` suite green; each task ended with a commit.
