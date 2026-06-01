# Flow Canvas Neon Block Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make idle Flow Canvas blocks "pop" by giving each a category-hued border + a balanced category-colored ambient glow ("Classic Ring"), replacing the 3px rail, while preserving every existing execution-state visual.

**Architecture:** Extract the node-card border-color and box-shadow decisions into pure, unit-testable helpers in a new `utils/nodeStyle.ts` (jsdom cannot compute `color-mix()`/`var()`, so correctness is verified at the string level, not via rendered styles). `BaseBlock.tsx` calls those helpers; the idle ring is a non-`!important` inline box-shadow inserted where `'none'` is today, so the class-driven running/error animations still override it. The rail span is removed; the category border now carries identity. No YAML/graph data changes.

**Tech Stack:** React 18 + TypeScript, `@xyflow/react`, Vite, Vitest + React Testing Library (jsdom), OKLCH design tokens (`color-mix(in oklch, …)`).

**Spec:** `docs/superpowers/specs/2026-05-31-flow-canvas-neon-block-design.md`

**Note — one deviation from the spec:** the spec mentioned a `tokens.ts` helper and CSS alpha tokens. This plan puts the derivation in a focused `utils/nodeStyle.ts` and keeps the alphas (36/46/60) as constants inside `idleNeon()` rather than as separate CSS custom properties — CSS alpha tokens would be unconsumed (dead) because the value is built in JS via `mix()`. The contract (per-category derivation from `--fc-cat-*-border`, Balanced alphas 36/46/60) is unchanged.

**All commands run from the `FlowCanvas/` directory unless noted.**

---

## File structure

- **Create** `FlowCanvas/src/utils/nodeStyle.ts` — pure resolvers: `idleNeon(border)`, `nodeBorderColor(...)`, `resolveNodeShadow(...)`. One responsibility: compute node-card border/shadow strings from state. Depends on `mix()` from `utils/tokens.ts`.
- **Create** `FlowCanvas/src/utils/__tests__/nodeStyle.test.ts` — unit tests for the three resolvers.
- **Modify** `FlowCanvas/src/nodes/BaseBlock.tsx` — call the resolvers for border + box-shadow; remove the rail + its padding offsets; tint the header label.
- **Modify** `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx` — replace the `node-rail` test with a rail-removed assertion.
- **Modify** `FlowCanvas/src/styles/tokens.css` — add a comment documenting where the idle ring is derived. **Keep `--fc-rail-w`** (StartNode still uses it).
- **Modify** `FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts` and `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` — these encode the old "rail + neutral border" contract; update to the new "category border, no rail" contract.
- **Unchanged (verify only):** `FlowCanvas/src/nodes/StartNode.tsx` (keeps `--fc-glow-start` and `--fc-rail-w`; never used the resolvers), `App.tsx` (MiniMap), `reducedMotion.css` (ring is static).

---

## Task 1: Pure node-style resolvers (`utils/nodeStyle.ts`)

**Files:**
- Create: `FlowCanvas/src/utils/nodeStyle.ts`
- Test: `FlowCanvas/src/utils/__tests__/nodeStyle.test.ts`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/utils/__tests__/nodeStyle.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { idleNeon, nodeBorderColor, resolveNodeShadow } from '../nodeStyle';

const SSH = 'var(--fc-cat-ssh-border)';

describe('idleNeon', () => {
  it('builds the Balanced 3-part ring from the category hue via color-mix', () => {
    expect(idleNeon(SSH)).toBe(
      '0 0 0 1px color-mix(in oklch, var(--fc-cat-ssh-border) 36%, transparent), ' +
      '0 0 10px -2px color-mix(in oklch, var(--fc-cat-ssh-border) 46%, transparent), ' +
      'inset 0 0 10px -7px color-mix(in oklch, var(--fc-cat-ssh-border) 60%, transparent)',
    );
  });
});

describe('nodeBorderColor', () => {
  it('uses the white selected border when selected (wins over disabled)', () => {
    expect(nodeBorderColor({ selected: true, isDisabled: true, border: SSH })).toBe('var(--fc-border-selected)');
  });
  it('uses the muted border when disabled', () => {
    expect(nodeBorderColor({ selected: false, isDisabled: true, border: SSH })).toBe('var(--fc-border-muted)');
  });
  it('uses the category hue otherwise (the persistent ring)', () => {
    expect(nodeBorderColor({ selected: false, isDisabled: false, border: SSH })).toBe(SSH);
  });
});

describe('resolveNodeShadow', () => {
  const base = { selected: false, heatActive: false, border: SSH } as const;
  it('idle (no heat) → the category neon ring', () => {
    expect(resolveNodeShadow({ ...base, execState: 'idle' })).toBe(idleNeon(SSH));
  });
  it('idle + heat active → none (heat ring takes the slot, no double-ring)', () => {
    expect(resolveNodeShadow({ ...base, execState: 'idle', heatActive: true })).toBe('none');
  });
  it('selected → the white selected glow, not the idle ring', () => {
    expect(resolveNodeShadow({ ...base, execState: 'idle', selected: true })).toBe('0 0 12px var(--fc-glow-selected)');
  });
  it('success → the success glow', () => {
    expect(resolveNodeShadow({ ...base, execState: 'success' })).toBe('0 0 10px var(--fc-glow-success)');
  });
  it('skipped → the skipped glow', () => {
    expect(resolveNodeShadow({ ...base, execState: 'skipped' })).toBe('0 0 16px var(--fc-glow-skipped)');
  });
  it('running → none (the fc-exec-running animation owns the shadow)', () => {
    expect(resolveNodeShadow({ ...base, execState: 'running' })).toBe('none');
  });
  it('error → none (the fc-exec-error animation owns the shadow)', () => {
    expect(resolveNodeShadow({ ...base, execState: 'error' })).toBe('none');
  });
  it('disabled → none', () => {
    expect(resolveNodeShadow({ ...base, execState: 'disabled' })).toBe('none');
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npx vitest run src/utils/__tests__/nodeStyle.test.ts`
Expected: FAIL — `Failed to resolve import '../nodeStyle'` (module does not exist yet).

- [ ] **Step 3: Write the implementation**

Create `FlowCanvas/src/utils/nodeStyle.ts`:

```ts
import { mix } from './tokens';

export type NodeExecState = 'idle' | 'running' | 'success' | 'error' | 'skipped' | 'disabled';

/**
 * Balanced idle "neon ring": a crisp 1px structural ring + a softened ambient glow + a faint
 * inner light, all derived from the block's category border hue via color-mix. Alphas 36/46/60
 * are the approved "Balanced" intensity (below running's 0.4a so idle never out-shouts a run).
 */
export function idleNeon(border: string): string {
  return (
    `0 0 0 1px ${mix(border, 36)}, ` +
    `0 0 10px -2px ${mix(border, 46)}, ` +
    `inset 0 0 10px -7px ${mix(border, 60)}`
  );
}

/** Card border color: white when selected, muted when disabled, else the persistent category hue. */
export function nodeBorderColor(opts: { selected: boolean; isDisabled: boolean; border: string }): string {
  if (opts.selected) return 'var(--fc-border-selected)';
  if (opts.isDisabled) return 'var(--fc-border-muted)';
  return opts.border;
}

/**
 * Inline box-shadow for the card BEFORE the heat-ring wrap (kept in BaseBlock). Mirrors the
 * historical success/skipped/selected ladder and adds the idle category ring as the terminal
 * branch, replacing the old `'none'`. running/error/disabled return 'none' — running & error are
 * owned by CSS class animations (which paint after inline styles); the idle ring is suppressed
 * when the heat ring is active so they never double-ring.
 */
export function resolveNodeShadow(opts: {
  execState: NodeExecState;
  selected: boolean;
  heatActive: boolean;
  border: string;
}): string {
  const { execState, selected, heatActive, border } = opts;
  if (execState === 'success') return '0 0 10px var(--fc-glow-success)';
  if (execState === 'skipped') return '0 0 16px var(--fc-glow-skipped)';
  if (selected) return '0 0 12px var(--fc-glow-selected)';
  if (execState === 'idle' && !heatActive) return idleNeon(border);
  return 'none';
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `npx vitest run src/utils/__tests__/nodeStyle.test.ts`
Expected: PASS (all 12 assertions green).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/nodeStyle.ts FlowCanvas/src/utils/__tests__/nodeStyle.test.ts
git commit -m "feat(flow-canvas): pure node-style resolvers for neon idle ring"
```

---

## Task 2: Wire resolvers into BaseBlock (border + box-shadow)

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx` (import; `existingBoxShadow` block ~lines 139-143; `containerStyle.border` ~line 148)
- Test: `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx`

- [ ] **Step 1: Write the failing test**

Add to `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx`, inside `describe('BaseBlock', …)`:

```tsx
  it('renders an idle node without crashing and keeps the block-node container', () => {
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.getByTestId('block-node')).toBeInTheDocument();
  });
```

(This is a render-smoke test. Style values use `color-mix`/`var()` which jsdom drops, so border/shadow *correctness* is covered by Task 1's unit tests; here we only guard against wiring regressions.)

- [ ] **Step 2: Run the test to verify current state**

Run: `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx`
Expected: the new test PASSES already (container exists today). This step pins the smoke test before refactoring; proceed.

- [ ] **Step 3: Edit BaseBlock — add the import**

In `FlowCanvas/src/nodes/BaseBlock.tsx`, find:

```tsx
import { mix } from '../utils/tokens';
```

Replace with:

```tsx
import { mix } from '../utils/tokens';
import { nodeBorderColor, resolveNodeShadow } from '../utils/nodeStyle';
```

- [ ] **Step 4: Edit BaseBlock — replace the box-shadow ladder**

Find (the comment block + ladder, ~lines 136-144):

```tsx
  // running + error are class-driven: the fc-exec-running / fc-exec-error animations own the
  // box-shadow via the cascade (CSS animations outrank inline styles), so no inline glow here.
  // success settles to a soft static glow on the INLINE path so the heat ring still stacks;
  // skipped keeps its glow; selection / idle unchanged.
  const existingBoxShadow =
    execState === 'success' ? '0 0 10px var(--fc-glow-success)'
      : execState === 'skipped' ? '0 0 16px var(--fc-glow-skipped)'
        : selected ? '0 0 12px var(--fc-glow-selected)'
          : 'none';
```

Replace with:

```tsx
  // running + error are class-driven: the fc-exec-running / fc-exec-error animations own the
  // box-shadow via the cascade (CSS animations outrank inline styles), so no inline glow here.
  // success/skipped settle to a soft static glow on the INLINE path so the heat ring still stacks;
  // idle gets the category "neon ring" (gated off when the heat ring is active). See utils/nodeStyle.
  const heatActive = heatTint != null;
  const existingBoxShadow = resolveNodeShadow({
    execState,
    selected,
    heatActive,
    border: colors.border,
  });
```

- [ ] **Step 5: Edit BaseBlock — category border**

Find (in `containerStyle`, ~line 148):

```tsx
    border: `1px solid ${selected ? 'var(--fc-border-selected)' : isDisabled ? 'var(--fc-border-muted)' : 'var(--fc-node-border)'}`,
```

Replace with:

```tsx
    border: `1px solid ${nodeBorderColor({ selected, isDisabled, border: colors.border })}`,
```

- [ ] **Step 6: Run tests + typecheck**

Run: `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx`
Expected: PASS (existing tests + the new smoke test).
Run: `npx tsc -b --noEmit` (or `npm run build` if `-b` complains without emit)
Expected: no type errors (note `heatActive`/`colors.border` are in scope; `colors` is defined ~line 105, `heatTint` ~line 121, both before this block).

- [ ] **Step 7: Commit**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx
git commit -m "feat(flow-canvas): category-hued border + idle neon glow on blocks"
```

---

## Task 3: Remove the rail, fix padding, tint the header label

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx` (`railStyle` ~167-176; `headerStyle.paddingLeft` ~197; `previewStyle.paddingLeft` ~221; label color ~240; rail `<span>` ~233)
- Test: `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx`

- [ ] **Step 1: Update the failing test (rail removal)**

In `FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx`, find:

```tsx
  it('exposes a node-rail test id for category color', () => {
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.getByTestId('node-rail')).toBeInTheDocument();
  });
```

Replace with:

```tsx
  it('no longer renders the legacy accent rail (the category border carries identity now)', () => {
    renderNode({ data: { blockType: 'send', label: 'Send', props: {} } as any });
    expect(screen.queryByTestId('node-rail')).toBeNull();
  });
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx`
Expected: FAIL — the rail `<span data-testid="node-rail">` still renders, so `queryByTestId` is not null.

- [ ] **Step 3: Remove the rail `<span>`**

In `BaseBlock.tsx`, find and delete these two lines (~232-233):

```tsx
      {/* Accent rail (category identity; absolutely positioned, out of the boxShadow stack) */}
      <span style={railStyle} data-testid="node-rail" />
```

- [ ] **Step 4: Remove `railStyle`**

Delete the whole `railStyle` block (~165-176):

```tsx
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
  };
```

- [ ] **Step 5: Drop the rail padding offset in the header**

In `headerStyle` (~194-195), find:

```tsx
    padding: '4px 8px',
    paddingLeft: 'calc(8px + var(--fc-rail-w))',
```

Replace with (the `4px 8px` shorthand already gives an 8px left edge):

```tsx
    padding: '4px 8px',
```

- [ ] **Step 6: Drop the rail padding offset in the preview**

In the inline preview `<div>` style (~342-344), find:

```tsx
          padding: '4px 8px',
          paddingLeft: 'calc(8px + var(--fc-rail-w))',
```

Replace with:

```tsx
          padding: '4px 8px',
```

> **Note — no label/title change needed.** The design's "category-tinted TYPE label" is already satisfied: the uppercase `def.type` badge uses `badgeStyle.color = colors.text` (the per-category `--fc-cat-<c>-text` token). The "mono title" is already satisfied too: the preview band already uses `fontFamily: 'monospace'`. So Task 3 is rail-removal + padding only — the main title stays neutral (`var(--fc-text)`) for readability of long command titles.

- [ ] **Step 7: Run tests to verify they pass**

Run: `npx vitest run src/nodes/__tests__/BaseBlock.test.tsx`
Expected: PASS — `node-rail` is gone; label text ("Send") and handle tests still pass.
Run: `npx tsc -b --noEmit`
Expected: no type errors. `CSSProperties` stays used by `iconChipStyle`/`headerStyle`/`badgeStyle`, so leave that import.

- [ ] **Step 8: Commit**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/nodes/__tests__/BaseBlock.test.tsx
git commit -m "feat(flow-canvas): drop accent rail; category border now carries identity"
```

---

## Task 4: Document the idle ring in tokens.css (keep `--fc-rail-w`)

**Files:**
- Modify: `FlowCanvas/src/styles/tokens.css`

> **Do NOT remove `--fc-rail-w`.** `StartNode.tsx` (lines ~65 and ~74) still uses it for its own accent rail; removing it would break the Start node. Only BaseBlock stopped using it — that is expected and fine (an orphaned-looking but still-consumed token).

- [ ] **Step 1: Confirm StartNode still consumes the token**

Run (from repo root): `npx rg "fc-rail-w" FlowCanvas/src`
Expected: matches in `tokens.css` (definition) and `StartNode.tsx` (2 uses); `BaseBlock.tsx` no longer matches after Task 3. The token **stays**.

- [ ] **Step 2: Add a documentation comment for the idle ring**

In `FlowCanvas/src/styles/tokens.css`, find the start glow line (~131):

```css
  --fc-glow-start: oklch(72% 0.17 150 / 0.15);
```

Add immediately after it:

```css
  /* Idle "neon ring": derived per-category in utils/nodeStyle.ts (idleNeon) from
     --fc-cat-*-border via color-mix; alphas 36/46/60 = "Balanced" intensity (no token here).
     Intensity scale: soft 0.15a background / balanced ~0.36-0.46a idle / 0.3-0.4a run/success/error. */
```

- [ ] **Step 3: Verify the build still passes**

Run: `npm run build`
Expected: `tsc` + `vite build` complete with no errors.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/styles/tokens.css
git commit -m "docs(flow-canvas): note idle neon ring derivation in tokens.css"
```

---

## Task 5: Update the e2e design-contract specs

Two Playwright specs encode the **old** Wave 2a contract ("body border neutral, category color on the rail"). The new design inverts this (category border, no rail), so they must be updated or they will fail.

**Files:**
- Modify: `FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts`
- Modify: `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts`

- [ ] **Step 1: node-redesign — replace the rail test with a border test**

In `flow-canvas-node-redesign.spec.ts`, find:

```ts
  test('accent rail renders and resolves to the category border token', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const rail = page.locator('.react-flow__node[data-id="node-ssh"] [data-testid="node-rail"]');
    await expect(rail).toBeVisible();
    const railBg = await rail.evaluate((el) => getComputedStyle(el as HTMLElement).backgroundColor);
    expect(railBg).toBe(await resolveVar(page, '--fc-cat-ssh-border'));
  });
```

Replace with:

```ts
  test('card border resolves to the category border token (the neon ring, no rail)', async ({ page }) => {
    await loadGraphFixture(page, createSshBlockFixture());
    const card = page.locator('.react-flow__node[data-id="node-ssh"] > div').first();
    const border = await card.evaluate((el) => getComputedStyle(el as HTMLElement).borderTopColor);
    expect(border).toBe(await resolveVar(page, '--fc-cat-ssh-border'));
    // the legacy rail is gone — identity is on the border now
    await expect(page.locator('.react-flow__node[data-id="node-ssh"] [data-testid="node-rail"]')).toHaveCount(0);
  });
```

(The other node-redesign tests are unaffected: "body neutralizes to --fc-node-surface" (body bg unchanged), "header renders a category-tinted icon svg", "outlined badge …", "unknown blockType …", and "exec-state precedence …" — that last one selects the container `> div` and checks the running animation; the rail was a child, not the container. The file's top comment block still describes the old rail design; optionally reword it.)

- [ ] **Step 2: token-sweep — body border is now the category hue, rail gone**

In `flow-canvas-token-sweep.spec.ts`, find the test title (~line 121):

```ts
  test('ssh block body border is neutral and the accent rail carries the category token', async ({ page }) => {
```

Replace it with:

```ts
  test('ssh block border carries the category token (neon ring, no rail)', async ({ page }) => {
```

Then, inside that same test, find the assertion block:

```ts
    const renderedBorder = await container.evaluate(
      (el) => getComputedStyle(el as HTMLElement).borderTopColor,
    );
    expect(renderedBorder).not.toBe('');
    expect(renderedBorder).toBe(await resolveVar(page, '--fc-node-border'));

    // The accent rail child span now carries the category color.
    const railBg = await block
      .locator('[data-testid="node-rail"]')
      .evaluate((el) => getComputedStyle(el as HTMLElement).backgroundColor);
    expect(railBg).not.toBe('');
    expect(railBg).toBe(await resolveVar(page, '--fc-cat-ssh-border'));
```

Replace it with:

```ts
    const renderedBorder = await container.evaluate(
      (el) => getComputedStyle(el as HTMLElement).borderTopColor,
    );
    expect(renderedBorder).not.toBe('');
    // The card border now carries the category hue directly (the neon ring); the rail is gone.
    expect(renderedBorder).toBe(await resolveVar(page, '--fc-cat-ssh-border'));
    await expect(block.locator('[data-testid="node-rail"]')).toHaveCount(0);
```

Optionally update the preceding comment (~lines 117-120) that explains the old "color on the rail" design so it matches the new border-carries-identity reality.

- [ ] **Step 3: Run the two updated specs**

First-time only: `npm run test:e2e:install` (installs the Chromium browser).
Run: `npm run test:e2e -- flow-canvas-node-redesign flow-canvas-token-sweep`
Expected: both specs PASS against the new design.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-node-redesign.spec.ts FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts
git commit -m "test(flow-canvas): update e2e design contracts for neon ring (border, no rail)"
```

---

## Task 6: Full verification (build, suite, StartNode, YAML round-trip, visual)

**Files:** none (verification only)

- [ ] **Step 1: Full JS test suite**

Run: `npm test`  (i.e. `vitest run`)
Expected: all tests PASS, including the existing `StartNode.test.tsx` (StartNode never used the resolvers and is unaffected).

- [ ] **Step 2: Lint**

Run: `npm run lint`
Expected: no new errors. (If `CSSProperties` or any symbol is now unused, remove it and re-run.)

- [ ] **Step 3: Production web build**

Run: `npm run build`
Expected: clean `tsc -b` + `vite build`; `FlowCanvas/dist/` produced.

- [ ] **Step 4: .NET build (embeds the dist)**

Run (from repo root): `dotnet build SSH_Helper.sln`
Expected: build succeeds (the `BuildFlowCanvas` target runs `npm run build` and embeds `dist/`).

- [ ] **Step 5: (Optional, thorough) full e2e suite**

First-time only: `npm run test:e2e:install`.
Run: `npm run test:e2e`
Expected: green. Pay attention to `flow-canvas-node-redesign`, `flow-canvas-token-sweep`, `flow-canvas-branch-bands`, and `flow-canvas-execution-cinematics` (the node-visual contracts). If any *other* spec asserts the old rail/neutral-border, update it the same way as Task 5 and note it.

- [ ] **Step 6: Manual — visual + state review**

Run (from repo root): `dotnet run --project SSH_Helper.csproj`, open the Flow Canvas, load a sample script from `ScriptSamples/` (a long FortiGate-style one if available). Confirm:
- Idle blocks show a **category-hued border + balanced glow**; the 3px rail is gone; the uppercase TYPE badge stays category-tinted (the main title stays neutral).
- A **running** block's breathing glow still clearly out-shouts idle blocks; **error** shake/ripple unaffected; **success**/**skipped** glows intact.
- **Selected** shows the white glow (not the category ring); **disabled** is muted at 0.5 opacity with no ring.
- Toggle the **heatmap**: idle blocks show the 3px heat ring only (no double-ring).
- **StartNode** keeps its green identity (no category ring).
- **Container** blocks (if/foreach/try) render the ring cleanly over their branch bands; child blocks at min width are not crowded.

- [ ] **Step 7: Manual — YAML round-trip (export safety)**

In the running app: import a script from `ScriptSamples/` into the canvas, then export it back to YAML. Confirm the exported YAML is byte-identical to the source (no graph/data fields changed by the visual work). If a sample is already loaded, export → re-import → export and diff the two exports.

- [ ] **Step 8: Final commit (if any verification fixes were needed)**

```bash
git add -A
git commit -m "test(flow-canvas): verify neon block redesign (build, suite, round-trip)"
```

(If no fixes were needed, skip — the feature is already committed across Tasks 1-5.)

---

## Self-review (completed during authoring)

- **Spec coverage:** category border (T2) · balanced per-category glow via `idleNeon` (T1/T2) · idle gated on `idle && !heat && !selected` (T1 `resolveNodeShadow` + T2 wiring) · precedence vs running/error/success/skipped/selected/disabled/heat (T1 tests) · rail removed (T3) · "tinted TYPE label" + "mono title" already satisfied by the existing `def.type` badge + mono preview (T3 note) · idle-ring doc + `--fc-rail-w` retained for StartNode (T4) · old e2e design contracts updated (T5) · StartNode unaffected + YAML round-trip + visual + build verification (T6). All spec sections map to a task.
- **Placeholder scan:** none — every code/test step has complete content and exact commands.
- **Type consistency:** `idleNeon`, `nodeBorderColor`, `resolveNodeShadow`, `NodeExecState` are defined in T1 and consumed with identical signatures in T2; `heatActive = heatTint != null` matches `resolveNodeShadow`'s `heatActive` param.
- **jsdom caveat documented:** style correctness is unit-tested at the string level (T1); component DOM tests assert structure only (T2/T3); pixel/computed-color correctness is covered by the Playwright e2e specs (T5) + manual review (T6).
