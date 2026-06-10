# Flow Canvas — Run Output Viewer

**Date:** 2026-06-08
**Status:** Approved design, ready for planning
**Author:** brainstormed with Chris

## Problem

When a script runs from the Flow Canvas, the full combined output only appears in the
main form's output box (`txtOutput`). To read results, the user has to leave the canvas
and switch back to Form1. We want the final output viewable **inside** the canvas window.

The canvas already has a bottom "Output" panel (`OutputPreview.tsx`), but it only shows
the output of the **currently selected block** — not the whole run, and not the connection
banners or any non-step output the main form shows.

## Goal

Add a **Run Output** tab to the canvas's existing bottom dock that is a faithful, live
mirror of the main form's output box: the full combined stream across all hosts, including
the `############### CONNECTED TO … ###############` banners, streamed live as the run
proceeds. The user should never need to switch to the main form to read results.

Non-goals (v1):
- A separate OS-level output window (deferred — see Pop out).
- Full terminal redraw emulation of bare carriage returns (deferred — see Newlines).
- Per-host filtering / jump-to-host (deferred; output is combined, exactly like the main form).

## Core principle — mirror the one source of truth

The Run Output tab does **not** re-derive output from per-step messages. It mirrors the
main form's output buffer. Whatever appends to `txtOutput` also appends to the canvas;
whatever clears `txtOutput` also clears the canvas. This guarantees parity for free —
banners, all hosts, non-step output, and correct ordering — and behaves identically whether
the run was launched from the canvas or the main form.

## C# integration points (all already exist in `Form1.cs`)

Verified symbols:

- `_outputBuffer` — `StringBuilder` (Form1.cs:268), guarded by `_outputBufferLock` (269).
  Holds the raw accumulated output (unmodified line endings). `GetOutputBufferText()` returns
  `_outputBuffer.ToString()` (Form1.cs:12974).
- `_uiOutputThrottler` — `OutputThrottler` (Form1.cs:270), constructed at 331 with
  `AppendOutputToUi` as its flush callback and a 50 ms interval.
- `AppendOutputToUi(string output)` (Form1.cs:13910) — the throttler-flush callback.
  Receives the batched raw chunk; appends it to `txtOutput` via
  `NormalizeNewlinesForDisplay(output)` (13921). **This is the single UI append chokepoint.**
- `ClearOutput()` (Form1.cs:13981) — clears `txtOutput` and `_outputBuffer`. Already called
  before every run, so clear-on-rerun is automatic.
- `_flowCanvasForm` — `FlowCanvasForm?` (Form1.cs:175). Existing send pattern used throughout:
  `_flowCanvasForm?.SendMessage(new { type = "...", ... })` (e.g. 6857, 6996, 7127).

### Changes

1. **Forward live output.** In `AppendOutputToUi`, after the chunk is computed, forward the
   **raw** `output` (pre-normalization) to the canvas, guarded only by "canvas is open":
   ```csharp
   _flowCanvasForm?.SendRunOutputAppend(output);
   ```
   Forwarding from the throttler-flush callback preserves the same batched 50 ms cadence as
   `txtOutput`. React renders raw `\n` itself — no WinForms newline normalization is applied
   to the forwarded copy. Forward before any `txtOutput`-availability early-return so the
   canvas still receives output if the main box is momentarily unavailable.

2. **Forward clears.** In `ClearOutput`, after clearing, call
   `_flowCanvasForm?.SendRunOutputClear();`.

3. **Seed on open.** When the canvas is opened (the `_flowCanvasForm = new FlowCanvasForm(...)`
   path, ~Form1.cs:6633), seed it once with the current buffer so a tab opened after a run
   isn't empty:
   ```csharp
   _flowCanvasForm.SendRunOutputClear();
   _flowCanvasForm.SendRunOutputAppend(GetOutputBufferText());
   ```
   Both calls queue behind the existing `ready` handshake (`_pendingMessages`) and drain when
   React posts `ready`, so ordering is safe.

All forwarding is a no-op when the canvas is closed (`_flowCanvasForm` is null) → zero
overhead in the common case.

### `UI/FlowCanvasForm.cs`

Add thin wrappers over the existing `SendMessage(object)` queue:

```csharp
public void SendRunOutputAppend(string chunk) =>
    SendMessage(new { type = "run-output", chunk });

public void SendRunOutputClear() =>
    SendMessage(new { type = "run-output-clear" });
```

(Seed = `SendRunOutputClear()` followed by one `SendRunOutputAppend(fullBuffer)`.)

Per Development Guideline 8, Flow Canvas changes touch both sides; here the C# side is Form1 +
FlowCanvasForm. `FlowCanvasBridge.cs` is **not** involved — there is no graph/YAML change.

## Message contract (C# → React)

Two new incoming messages. No React → C# messages are added.

| Message | Payload | Effect |
|---|---|---|
| `run-output` | `{ chunk: string }` | Append `chunk` to the run-output buffer |
| `run-output-clear` | `{}` | Clear the run-output buffer |

Seed = one `run-output-clear` then one `run-output` carrying the whole buffer.

- `FlowCanvas/src/communication-message-types.ts` — add `runOutput: 'run-output'` and
  `runOutputClear: 'run-output-clear'` to the incoming map, plus their interfaces.
- `FlowCanvas/src/stores/messageBridge.ts` — register handlers next to the existing
  `stepOutput` (281) / `executionStarted` (204) handlers: `run-output` → `appendRunOutput`,
  `run-output-clear` → `clearRunOutput`.

## React state

### `stores/slices/executionSlice.ts` (run-lifecycle data, cohesive with `blockOutputs`)
- `runOutput: string` — the mirrored buffer.
- `appendRunOutput(chunk: string)` — appends, then enforces the **last ~5,000 lines** cap
  (drop oldest) to bound the DOM and memory. This is a deliberate, documented divergence from
  the main form's 2,000,000-char cap; acceptable because the canvas is a viewer, not the
  scripting source of `${_output}`.
- `clearRunOutput()` — resets to empty. Fired on run start via the mirrored `ClearOutput`.

### `stores/slices/uiSlice.ts` (view prefs, cohesive with existing panel prefs)
- Active bottom-dock tab: `'block' | 'run'`.
- Toggles: `runOutputColor` (default `true`), `runOutputWrap` (default `false`),
  `runOutputFollow` (default `true`).
- Pop-out: `runOutputPoppedOut: boolean` + `{ x, y, w, h }` position.
- Unread indicator: `runOutputUnread: boolean` (set when output arrives while the Block tab is
  active; cleared when the Run tab is shown).

All view prefs persist to `WindowState` through the existing `pref-save` channel, exactly like
the current canvas display settings (heatmap, reduced motion, density, etc.).

## UI

### Tabbed bottom dock — `panels/OutputPreview.tsx`
Add a tab header to the existing dock: **Block Output | Run Output**. The dock keeps its
resize handle, height persistence, and term styling. The active tab selects which child
renders. Behavior:
- On `execution-started`, switch the active tab to **Run Output** and clear `runOutputUnread`.
- If the user manually switches to Block while a run streams, show an unread `•` dot on the
  Run Output tab; clear it when they return.

### Console — `panels/RunOutputView.tsx` (new)
Monospace terminal rendering using existing tokens (`--fc-term-bg`, `--fc-term-text`,
`--fc-font-mono`, `--fc-state-error`, accent for banners). Toolbar, left→right:

| Button | Behavior |
|---|---|
| **⌕ Find** | In-panel find (Ctrl+F) with highlight + next/prev, scoped to this buffer |
| **⤓ Follow** | Stick-to-bottom; auto-unsticks when the user scrolls up; re-stick on click |
| **↵ Wrap** | Toggle `white-space: pre` ↔ `pre-wrap` |
| **🎨 Color** | Toggle line styling on/off (default on) |
| **⧉ Copy** | Copy the whole buffer to the clipboard |
| **⤢ Pop out** | Detach the panel into a draggable floating overlay within the canvas |

A small **LIVE** indicator shows while `isRunning`.

### Line styling (Color on)
Classify each line for rendering; off = byte-for-byte plain text.
- **Banner** — regex `^#{6,}.*#{6,}$` (the generated `###############` headers). 100% reliable
  because we generate these delimiters → render as a teal section header.
- **Error** — a conservative best-effort heuristic, e.g.
  `/(command (parse )?error|command fail|return code\s+-?\d|%\s*invalid|permission denied|\bfail(ed)?\b)/i`
  → red tint. Best-effort and purely cosmetic; the Color toggle is the escape hatch if it
  misfires.
- Everything else → default term text.

### Pop out (v1)
Floating, draggable overlay **within the canvas window** (absolute-positioned, same approach as
`DebugPanel.tsx`). While popped out, the Run Output view leaves the bottom dock (Block Output
remains in the dock); toggling re-docks it. Position/size persist via `uiSlice` → `pref-save`.
A real separate OS window (second monitor) is explicitly deferred.

## Newlines
React splits lines on `\r\n` and `\n` and renders each as a break. **Known v1 limitation:**
bare `\r` terminal redraws (progress lines that overwrite themselves) are not emulated — they
render as ordinary text. The main form doesn't truly emulate them either
(`NormalizeNewlinesForDisplay` just preserves bare CR). Full redraw emulation is deferred.

## Persistence (`Models` `WindowState` + `pref-save`)
Add fields to `WindowState` for: active output tab, `runOutputColor`, `runOutputWrap`,
`runOutputFollow`, pop-out flag + geometry. Wire them through the existing canvas `pref-save`
handler. Follow the existing serialization conventions (e.g. enums are int-serialized — do not
reorder).

## Testing

### C# (xUnit)
- Forwarding: `AppendOutputToUi` and `ClearOutput` emit `run-output` / `run-output-clear`
  through the FlowCanvasForm seam (assert message type + payload). Use the existing testable
  message path rather than a live WebView2.
- Seed-on-open emits clear + a single append carrying the full buffer.

### React (vitest + jsdom)
- Line classification: banner / error / normal produce the expected class or `data-*` attribute
  (assert at class/string level — jsdom can't compute `color-mix`/`var`, per the project's
  vitest-harness conventions).
- Color **off** → no styling classes (plain text).
- Follow: appends auto-scroll while stuck; scrolling up unsticks; new appends don't yank.
- Copy copies the full buffer; tab auto-switches to Run Output on `execution-started`; unread
  dot logic.

### Manual / parity
- Run the same script from the main form and from the canvas; confirm the Run Output tab matches
  `txtOutput` (banners, multi-host ordering, errors).

## Files

**React**
- `FlowCanvas/src/panels/OutputPreview.tsx` — add tab header + child switching.
- `FlowCanvas/src/panels/RunOutputView.tsx` — **new** console component.
- `FlowCanvas/src/stores/slices/executionSlice.ts` — `runOutput` + append/clear (+ line cap).
- `FlowCanvas/src/stores/slices/uiSlice.ts` — tab + toggles + pop-out state.
- `FlowCanvas/src/stores/messageBridge.ts` — handle the two new messages.
- `FlowCanvas/src/communication-message-types.ts` — message keys + interfaces.

**C#**
- `Form1.cs` — forward in `AppendOutputToUi` + `ClearOutput`; seed on canvas open.
- `UI/FlowCanvasForm.cs` — `SendRunOutputAppend` / `SendRunOutputClear`.
- `Models/…WindowState` — persisted view prefs; wire through the `pref-save` handler.

## Risks / trade-offs
- **Output volume.** Bounded by the ~5,000-line React cap; forwarding rides the existing 50 ms
  throttle so message rate is already smoothed.
- **Error heuristic false positives/negatives.** Cosmetic only; Color toggle disables it.
- **Bare-CR redraws.** Not emulated in v1 (documented).
- **Double rendering cost.** Output is now appended in two places (txtOutput + canvas) only when
  the canvas is open; negligible given the throttle and line cap.
