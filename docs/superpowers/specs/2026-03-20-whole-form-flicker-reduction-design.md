# Whole-Form Flicker Reduction Design

## Context

The narrow presets-tab flicker is already reduced by buffering the custom `BorderlessTabControl` and suppressing its native background erase. The remaining problem is broader: the whole SSH Helper window still shows visible flicker in callback regain-focus flows and during some ordinary interactions.

This is not a single missing buffering flag. `Form1` already enables form-level double buffering in `Form1.cs`, but the visible redraw still passes through a large hierarchy of stock WinForms containers, split containers, custom-painted surfaces, and a few broad `Refresh()` / `Invalidate()` paths.

The design for this pass is to treat flicker reduction as two phases:

1. Stabilize callback regain-focus repaint, where the problem is easiest to reproduce and most directly connected to recent browser-callback work.
2. Audit and narrow the remaining general-interaction repaint churn without bundling that into the first phase.

## Goals

- Reduce visible full-window flicker when SSH Helper regains focus after callback windows close.
- Preserve current callback behavior, owner restoration, and UI appearance.
- Decompose remaining interaction flicker into a second focused pass instead of mixing multiple redraw problems into one implementation.
- Prefer narrow, testable WinForms changes over broad global rendering switches.

## Non-Goals

- Do not introduce a global `WS_EX_COMPOSITED` strategy on the entire main form.
- Do not redesign the layout architecture of `Form1`.
- Do not weaken or remove the browser-callback focus restoration logic.
- Do not attempt to eliminate every possible redraw artifact in one change set.

## Current Findings

### Form-level double buffering already exists

`Form1` already calls:

- `SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true)`

That means the remaining flicker is coming from child/control/container repaint behavior rather than the absence of any main-form buffering at all.

### Callback regain-focus path is a strong repaint trigger

The keep-open callback close path now explicitly restores owner focus through `BrowserCallbackUiHost` and `BrowserCallbackFocusRestorer`. That path is intentionally aggressive so SSH Helper becomes foreground again, but it also exposes any client-area background erase or container repaint weakness still present in the main window.

### The main surface is built from many stock containers

`Form1.Designer.cs` shows a deep hierarchy of:

- `SplitContainer`
- `Panel`
- `TabControl`
- `ToolStrip` / `StatusStrip`
- `TreeView`
- `DataGridView`
- history/output panels

The custom tab control was only one piece. Stock split/container surfaces can still repaint visibly during activation if they are not buffered or if they erase before repainting.

### There are still broad redraw calls

Several paths still request wider repaint than necessary, including:

- `Refresh()` after layout/theme work
- `Invalidate(true)` on header surfaces
- `tabControl.Parent?.Invalidate()`
- `dgv_variables.Refresh()`
- repeated scrollbar/host-count refresh requests through `HostGridRestoreBatcher`

These are not automatically wrong, but they are likely contributors once the form is reactivated or when layout churn is already happening.

### Some custom controls are already hardened

Custom surfaces like `InteractiveTerminalViewportControl`, `ScintillaScriptEditorControl`, and now `BorderlessTabControl` already opt into buffered painting. That makes the remaining flicker more likely to sit in stock container seams and broad invalidation patterns rather than in every custom control equally.

## Approaches Considered

### 1. Global compositing on the main form

Enable a heavy whole-form compositing strategy such as `WS_EX_COMPOSITED`.

Pros:

- Broadest possible flicker reduction with relatively little code.

Cons:

- High risk with WinForms split containers, `DataGridView`, scrolling surfaces, and embedded browser-related UI.
- Can introduce sluggish redraw, delayed child painting, and new rendering artifacts.
- Too blunt for a form that already mixes stock and custom controls.

### 2. Two-phase targeted repaint stabilization

First stabilize the callback regain-focus repaint path, then separately audit the remaining general-interaction redraw churn.

Pros:

- Matches the actual shape of the bug.
- Keeps the first fix tied to the strongest reproduction path.
- Limits risk by avoiding a broad rendering switch.
- Produces clearer regression boundaries and verification steps.

Cons:

- Requires discipline to keep phase boundaries clean.
- Needs focused inspection of several UI seams instead of one switch.

### 3. Broad refactor of main-form layout and rendering

Replace more stock containers or refactor large parts of `Form1` layout.

Pros:

- Could produce the cleanest long-term rendering behavior.

Cons:

- Much larger than the bug warrants.
- Higher regression risk in a very busy WinForms surface.
- Not appropriate for a focused flicker-reduction pass.

## Decision

Approach 2 is the chosen design.

## Architecture

### Phase 1: Callback regain-focus flicker

This phase targets the redraw that happens when SSH Helper returns to the foreground after callback windows are dismissed.

The implementation should:

- Keep the current owner-focus restore behavior intact.
- Harden the main-form container surfaces most exposed during activation repaint.
- Prefer narrow buffering / background-erase suppression on high-coverage container seams over a global compositing switch.
- Remove or narrow any avoidable whole-surface refresh that participates in the regain-focus path.

Likely implementation seams:

- buffered or background-erase-aware wrappers for container controls that dominate the main client area
- main-form or container-level paint-path suppression where a native erase happens before repaint
- trimming explicit `Refresh()` / wide `Invalidate()` calls that are broader than necessary

### Phase 2: General interaction flicker

This phase handles redraw that still appears during ordinary use after activation flicker is reduced.

The implementation should focus on high-churn interaction paths such as:

- host-grid scrollbar and host-count refreshes
- `DataGridView` refresh/layout churn
- history/output list updates
- tree/list invalidation during preset and host interactions
- theme/layout paths that repaint more of the form than necessary

Phase 2 should prefer batching and narrower repaint requests over additional global rendering hacks.

## Expected Outcomes

### After Phase 1

- Closing callback windows should no longer expose obvious whole-form flash when SSH Helper returns to the foreground.
- The recent focus-restoration fix should remain intact.
- The main form should repaint more cohesively during activation.

### After Phase 2

- Common interactions should show less visible flicker outside the callback workflow.
- Remaining redraw hotspots should be smaller and easier to reason about.

## Verification Plan

### Automated verification

- Add focused tests for any new buffered/background-erase-aware UI wrappers or helper seams.
- Keep WinForms tests non-invasive: no visible top-level host windows unless visibility is the thing under test.
- Rerun focused UI suites for forms using the changed infrastructure.
- Rerun the browser-callback regression slice after phase-1 work.

### Manual verification

Phase 1 manual checks:

- Run the two-callback preset and close the first and second callback windows.
- Confirm SSH Helper regains focus without broad client-area flash.

Phase 2 manual checks:

- Resize the main window.
- Switch presets/favorites.
- Interact with the host grid and history/output areas.
- Confirm redraw is stable during routine UI activity.

## Risks And Mitigations

- Risk: container buffering changes splitter/layout behavior.
  Mitigation: prefer small wrappers or style changes on the most important surfaces first, and verify splitter behavior explicitly.

- Risk: suppressing background erase leaves stale pixels.
  Mitigation: only suppress erase where the control fully repaints its client area or where the container hierarchy above it guarantees repaint coverage.

- Risk: trimming repaint calls leaves stale visuals.
  Mitigation: replace broad repaint with narrower invalidation only after confirming the actual dirty region and rerun focused UI tests.

- Risk: phase scope drifts into a broad UI rewrite.
  Mitigation: keep phase 1 limited to callback regain-focus repaint and defer interaction cleanup to the second pass by design.
