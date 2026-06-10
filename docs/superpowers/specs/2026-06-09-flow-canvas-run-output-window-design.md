# Flow Canvas — Detachable Run Output Window

**Date:** 2026-06-09
**Status:** Approved design, ready for planning
**Author:** brainstormed with Chris
**Supersedes:** the in-canvas pop-out overlay (`RunOutputPopOut.tsx`) from `2026-06-08-flow-canvas-run-output-viewer-design.md` Phase 6.

## Problem

The Run Output console can currently "pop out" only into a floating overlay **inside** the WebView2. A DOM overlay can never leave the browser host, so it can't be dragged to a second monitor. The user wants the popped-out console to be a **real OS window** they can move anywhere.

## Goal

Replace the in-canvas overlay with a real, native, top-level window that hosts the **same** `RunOutputView` console (full Color / Find / Follow / Wrap parity, one source of truth) and can be dragged to any monitor. The window is fed by the existing output stream and is independent of the Flow Canvas window.

Decision (from brainstorm): the window hosts a **second WebView2** that loads the same dist with `?panel=runoutput`, rendering only `<RunOutputView/>`. This reuses the exact console component with zero duplicated rendering/classification logic.

Non-goals (v1): multiple simultaneous output windows (single instance); a native non-WebView2 renderer.

## Architecture

```
Form1 (owns the output stream: _outputBuffer / AppendOutputToUi / ClearOutput)
  ├── _flowCanvasForm : FlowCanvasForm        (existing — docked console in the canvas)
  └── _runOutputWindow : RunOutputWindowForm   (NEW — detachable OS window)
        └── WebView2 → https://flowcanvas.local/index.html?panel=runoutput
              └── React: RunOutputWindowApp → <RunOutputView/>
```

Form1 already fans the raw output to the canvas (`_flowCanvasForm?.SendRunOutputAppend(output)`). It gains a second sink: `_runOutputWindow?.SendRunOutputAppend(output)` (plus clear + seed-on-open). The output pipeline is otherwise unchanged.

The window is **owned by Form1, not the canvas** — closing the Flow Canvas does not close it, and it keeps receiving output as long as it's open.

## React side

### Entry branch — `FlowCanvas/src/main.tsx`
Read `new URLSearchParams(window.location.search).get('panel')`. If it equals `'runoutput'`, render `<RunOutputWindowApp/>`; otherwise render `<App/>` as today.

### `FlowCanvas/src/RunOutputWindowApp.tsx` (new)
A thin standalone host:
- Renders `<RunOutputView/>` inside a full-viewport dark container (`background: var(--fc-term-bg)`, `height: 100vh`, flex column).
- On mount: sets `runOutputPoppedOut = true` in the store (this reuses the logic we built so the console's own "Pop out" button is hidden — it would be meaningless in the window).
- Initializes `initRunOutputWindowBridge()` and tears it down on unmount.

### `FlowCanvas/src/stores/runOutputWindowBridge.ts` (new)
A minimal bridge (NOT the full `messageBridge.ts`, which pulls in graph/layout machinery). Mirrors the `messageBus.on(...)` + `unsubs` pattern. Handles only:
- on init → `messageBus.sendReady()` (or `send({type:'ready'})`) so the host drains its queue.
- `run-output` → `appendRunOutput(chunk)` (with the off-tab unread guard skipped — this window is always "the run output").
- `run-output-clear` → `clearRunOutput()`.
- `execution-started` → `setRunning(true)`; `execution-finished` → `setRunning(false)` (drives the LIVE dot).
- `pref-restore` / `layout-restore` → `restoreRunOutputPrefs(...)` + `setTheme(...)` (seed Color/Wrap/Follow + dark/light on open).
- Returns a cleanup that unsubscribes.

The window's `RunOutputView` Color/Wrap/Follow toggles still emit `layout-save` (existing behavior); `RunOutputWindowForm` persists them to the same `WindowState` fields, so the dock and the window stay consistent.

### Dock behavior — `OutputPreview.tsx` / `uiSlice.ts` / `messageBridge.ts`
`runOutputPoppedOut` now means **"the detached window is open."** When true:
- The canvas console's **Pop out** button (`RunOutputView` toolbar) sends `open-run-output-window` to the host (instead of toggling the overlay) and the store sets `outputTab = 'block'` — the dock switches to the **Block Output** tab.
- Clicking the **Run Output** tab while popped out **docks back**: it sends `close-run-output-window` to the host and (on confirmation) the console returns to the dock showing the run tab.
- Auto-focus-on-run-start (`executionStarted → setOutputTab('run')`) and the unread dot only fire when **not** popped out (the live output is visible in the window).
- When the user closes the window via its title-bar X, the host notifies React (`run-output-window-closed`) → `runOutputPoppedOut = false` and `setOutputTab('run')` so the console reappears in the dock.

### Removed
- `FlowCanvas/src/panels/RunOutputPopOut.tsx` and `__tests__/RunOutputPopOut.test.tsx` (the overlay).
- The `<RunOutputPopOut />` mount in `App.tsx`.
- `toggleRunOutputPoppedOut` becomes driven by the open/close-window flow rather than a local overlay toggle (see uiSlice changes below).

## C# side

### `UI/RunOutputWindowForm.cs` (new, ~150 lines)
A focused WebView2 host — the same handshake/queue plumbing as `FlowCanvasForm`, minus all the canvas event surface:
- `WebView2` initialized like `FlowCanvasForm.InitializeWebView2Async` (shared `CoreWebView2Environment` user-data folder; `SetVirtualHostNameToFolderMapping("flowcanvas.local", distPath, Allow)`; navigate to `https://flowcanvas.local/index.html?panel=runoutput`).
- `ConcurrentQueue<string> _pendingMessages` + `_reactReady` + `SendMessage(object)` / `PostOrQueue` (identical idiom).
- On `ready`: drain queue + send a seed (`layout-restore` with persisted Color/Wrap/Follow + theme).
- Public: `SendRunOutputAppend(string)`, `SendRunOutputClear()` (same shapes as FlowCanvasForm).
- Handles inbound `layout-save` → persist Color/Wrap/Follow to `WindowState` (reuse the existing field-writes; extract the shared writer if cheap, else a small focused copy).
- `FormClosed` → raise `WindowClosed` event so Form1 can notify the canvas.
- Title "Run Output", dark title bar via `DialogTheme.SetDarkTitleBar`, themed via `DialogTheme.ApplyTo`.
- Size/position persisted to new `WindowState.FlowCanvasRunOutputWindow{Left,Top,Width,Height}` (nullable ints).

### `Form1.cs`
- Field `private RunOutputWindowForm? _runOutputWindow;` + a `ModelessDialogManager<RunOutputWindowForm>` (single-instance).
- `OpenRunOutputWindow()` — `ShowOrActivate`, wire `WindowClosed` (→ tell canvas via `_flowCanvasForm?.SendMessage(new { type = "run-output-window-closed" })` and null the field), then seed: `SendRunOutputClear()` + `SendRunOutputAppend(GetBufferedOutputSnapshot())`.
- In `AppendOutputToUi`: add `_runOutputWindow?.SendRunOutputAppend(output);` next to the canvas sink. In `ClearOutput`: add `_runOutputWindow?.SendRunOutputClear();`. Also forward `execution-started`/`execution-finished`-equivalent run state to the window (a `SendRunState(bool)` wrapper) so the LIVE dot works — wired from the same place the canvas gets those signals.

### `UI/FlowCanvasForm.cs`
- New inbound messages in `HandleHostMessage`: `open-run-output-window` → raise `OnOpenRunOutputWindow`; `close-run-output-window` → raise `OnCloseRunOutputWindow`.
- New events `OnOpenRunOutputWindow` / `OnCloseRunOutputWindow` (wired in Form1 to open/close `_runOutputWindow`).

### `Models/AppConfiguration.cs`
- `WindowState`: add nullable `FlowCanvasRunOutputWindowLeft/Top/Width/Height` (geometry), following the existing nullable convention.

## Message contract (additions)

| Direction | Message | Payload | Effect |
|---|---|---|---|
| React→C# | `open-run-output-window` | `{}` | Form1 opens the window |
| React→C# | `close-run-output-window` | `{}` | Form1 closes the window |
| C#→React (canvas) | `run-output-window-closed` | `{}` | canvas clears `runOutputPoppedOut`, shows run tab |
| C#→React (window) | `run-output`, `run-output-clear` | (existing) | append / clear |
| C#→React (window) | `execution-started` / `execution-finished` | (existing) | LIVE on/off |
| C#→React (window) | `layout-restore` | prefs + theme | seed Color/Wrap/Follow + dark/light |

## Testing

- **React:** `main.tsx` branch selects `RunOutputWindowApp` for `?panel=runoutput`; `RunOutputWindowApp` renders `run-output-view` and the console's Pop-out button is hidden (poppedOut=true); `runOutputWindowBridge` routes `run-output`/`-clear`/run-state to store actions (drive via `messageBus` test hook). Dock: Pop-out switches `outputTab` to `block`; Run-tab-click while popped out triggers the close-window path; `run-output-window-closed` restores the run tab.
- **C#:** `RunOutputWindowForm.SendRunOutputAppend/Clear` queue the right messages (drain `_pendingMessages`, `[WinFormsFact]` + serial collection); `layout-save` inbound persists prefs to `WindowState`.

## Files

**React**
- `FlowCanvas/src/main.tsx` — panel branch.
- `FlowCanvas/src/RunOutputWindowApp.tsx` — **new** standalone host.
- `FlowCanvas/src/stores/runOutputWindowBridge.ts` — **new** minimal bridge.
- `FlowCanvas/src/panels/RunOutputView.tsx` — Pop-out button sends `open-run-output-window`.
- `FlowCanvas/src/stores/slices/uiSlice.ts` — `runOutputPoppedOut` open/close semantics; dock-tab transitions.
- `FlowCanvas/src/stores/messageBridge.ts` — handle `run-output-window-closed`; gate auto-focus/unread on `!poppedOut`.
- `FlowCanvas/src/App.tsx` — remove `<RunOutputPopOut/>`.
- **Delete** `FlowCanvas/src/panels/RunOutputPopOut.tsx` + its test.

**C#**
- `UI/RunOutputWindowForm.cs` — **new** window host.
- `Form1.cs` — own/open/feed the window; fan output + run state.
- `UI/FlowCanvasForm.cs` — open/close-window inbound messages + events.
- `Models/AppConfiguration.cs` — window geometry fields.

## Risks / trade-offs
- **Second WebView2 memory.** Shares the `CoreWebView2Environment` (same user-data folder) so it reuses the browser process; renderer overhead only. Acceptable.
- **Two stores (canvas + window) hold their own `runOutput`.** Both are fed the same stream from Form1, so they stay in sync; the window seeds from the buffer on open. No shared state needed.
- **Bridge duplication.** The window bridge is intentionally a small, separate file (handles ~5 messages) rather than reusing the canvas `messageBridge.ts` — keeping the window lightweight and decoupled from canvas/graph concerns.
- **Pref persistence from two surfaces.** Both the dock and the window write Color/Wrap/Follow to the same `WindowState` fields; last-writer-wins, which is fine for booleans.
