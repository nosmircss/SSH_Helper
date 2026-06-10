# Flow Canvas Run Output Viewer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a **Run Output** tab to the Flow Canvas's bottom dock that live-mirrors the main form's output box (full combined stream, all hosts, connection banners, streamed live) so the user never switches back to Form1 to read results.

**Architecture:** The Run Output tab mirrors the one source of truth — Form1's `_outputBuffer`/`txtOutput`. Two taps in `Form1.cs` (`AppendOutputToUi`, `ClearOutput`) forward the raw stream to `FlowCanvasForm` as `run-output` / `run-output-clear` messages; the canvas is seeded with the current buffer on open. React stores the buffer in `executionSlice` and renders it in a new `RunOutputView` console with light, toggleable styling. View prefs persist through the existing `layout-save`/`layout-restore` ↔ `WindowState` channel.

**Tech Stack:** C# .NET 8 WinForms (xUnit + Xunit.StaFact + FluentAssertions), React 19 + Zustand (vanilla, no immer) + TypeScript in a WebView2 host (Vitest + @testing-library/react + jsdom).

**Spec:** `docs/superpowers/specs/2026-06-08-flow-canvas-run-output-viewer-design.md`

---

## Conventions (read once before starting)

- **React test command:** `cd FlowCanvas && npx vitest run <relative-spec-path>` (full suite: `npm test`).
- **C# test command:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~<ClassName>"`.
- **C# build check:** `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true` (skips the Node build; faster).
- **Zustand mutation style (no immer):** read state in `set((s) => ({ ... }))`; clone Maps before mutating; never mutate in place.
- **uiSlice persistence idiom:** a user toggle does `messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, <field>: next })` and `set(...)`; a paired `restoreX` setter does `set(...)` only (no echo, host-driven).
- **C# message-send idiom:** `_flowCanvasForm?.SendMessage(new { type = "...", ... })`; queued until React posts `ready`.
- **C# test seam:** in tests `_reactReady` is false, so `SendMessage` enqueues onto `private ConcurrentQueue<string> _pendingMessages`; drain + parse JSON to assert a message was sent.
- **Every C# UI test class** that touches a Form MUST carry `[Collection(CallbackUiSerialCollection.Name)]` and use `[WinFormsFact]`.
- **Commit trailer:** end every commit message with `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

## File Structure

**Phase 1 — React data layer**
- Modify `FlowCanvas/src/communication-message-types.ts` — add `runOutput` / `runOutputClear` incoming keys.
- Modify `FlowCanvas/src/stores/slices/executionSlice.ts` — `runOutput` buffer + `appendRunOutput`/`clearRunOutput`.
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` — `outputTab`, `runOutputColor/Wrap/Follow` + toggles/restores.
- Modify `FlowCanvas/src/stores/messageBridge.ts` — handle the two new messages.
- Test `FlowCanvas/src/stores/slices/__tests__/executionRunOutput.test.ts`
- Test `FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts`

**Phase 2 — C# mirror**
- Modify `UI/FlowCanvasForm.cs` — `SendRunOutputAppend` / `SendRunOutputClear` wrappers.
- Modify `Form1.cs` — forward in `AppendOutputToUi` + `ClearOutput`; seed on canvas open.
- Test `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputTests.cs`

**Phase 3 — Console component**
- Create `FlowCanvas/src/utils/runOutputClassify.ts` — line classifier.
- Create `FlowCanvas/src/panels/RunOutputView.tsx` — the console (toolbar: Follow/Wrap/Color/Copy + LIVE).
- Test `FlowCanvas/src/utils/__tests__/runOutputClassify.test.ts`
- Test `FlowCanvas/src/panels/__tests__/RunOutputView.test.tsx`

**Phase 4 — Tabbed dock**
- Modify `FlowCanvas/src/panels/OutputPreview.tsx` — tab header + child switching.
- Modify `FlowCanvas/src/stores/messageBridge.ts` — auto-switch tab + unread on run start / output.
- Test `FlowCanvas/src/panels/__tests__/OutputPreviewTabs.test.tsx`

**Phase 5 — Find in output** *(independent follow-up)*
- Modify `FlowCanvas/src/panels/RunOutputView.tsx` — find box + highlight + next/prev.
- Test `FlowCanvas/src/panels/__tests__/RunOutputFind.test.tsx`

**Phase 6 — Pop out** *(independent follow-up)*
- Modify `FlowCanvas/src/stores/slices/uiSlice.ts` — `runOutputPoppedOut` + toggle.
- Modify `FlowCanvas/src/panels/OutputPreview.tsx` / `FlowCanvas/src/App.tsx` — render floating overlay.
- Test `FlowCanvas/src/panels/__tests__/RunOutputPopOut.test.tsx`

**Phase 7 — Persistence round-trip** *(independent follow-up)*
- Modify `Models/AppConfiguration.cs` — `FlowCanvasRunOutputColor/Wrap/Follow` fields.
- Modify `UI/FlowCanvasForm.cs` — `SavePanelSizes` reads + `SendPersistedLayout` sends.
- Modify `FlowCanvas/src/stores/messageBridge.ts` — extend `layout-restore` handler.
- Test `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputPrefsTests.cs`

**Scope notes (deliberate v1 trims):** active-tab persistence and pop-out geometry persistence are NOT planned — the tab auto-switches to Run Output on every run (so persisting a manual choice has no value), and pop-out is a transient view action. Bare-`\r` terminal redraws are not emulated. These match the spec's documented v1 limitations.

---

## Phase 1 — React data layer

### Task 1.1: Message contract

**Files:**
- Modify: `FlowCanvas/src/communication-message-types.ts`

- [ ] **Step 1: Add the two incoming message keys**

In `CANVAS_HOST_MESSAGES.incoming`, after the `prefRestore: 'pref-restore',` line, add:

```ts
    runOutput: 'run-output',
    runOutputClear: 'run-output-clear',
```

These are C#→React (host→canvas), so they belong under `incoming`. No interface is needed — like `step-output` they're consumed loosely in the bridge.

- [ ] **Step 2: Type-check**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: PASS (no errors).

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/communication-message-types.ts
git commit -m "feat(flow-canvas): declare run-output / run-output-clear messages

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 1.2: `runOutput` buffer in executionSlice

**Files:**
- Modify: `FlowCanvas/src/stores/slices/executionSlice.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/executionRunOutput.test.ts`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/stores/slices/__tests__/executionRunOutput.test.ts`:

```ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save', prefSave: 'pref-save', setLayoutMode: 'set-layout-mode' } },
}));
import { useFlowStore } from '../../useFlowStore';

const MAX = 5000;

describe('executionSlice runOutput', () => {
  beforeEach(() => {
    useFlowStore.getState().clearRunOutput();
  });

  it('starts empty', () => {
    expect(useFlowStore.getState().runOutput).toBe('');
  });

  it('appendRunOutput concatenates raw chunks', () => {
    useFlowStore.getState().appendRunOutput('### CONNECTED ###\n');
    useFlowStore.getState().appendRunOutput('line one\n');
    expect(useFlowStore.getState().runOutput).toBe('### CONNECTED ###\nline one\n');
  });

  it('clearRunOutput empties the buffer', () => {
    useFlowStore.getState().appendRunOutput('something');
    useFlowStore.getState().clearRunOutput();
    expect(useFlowStore.getState().runOutput).toBe('');
  });

  it('caps the buffer at the last 5000 lines', () => {
    const big = Array.from({ length: MAX + 200 }, (_, i) => `line ${i}`).join('\n');
    useFlowStore.getState().appendRunOutput(big);
    const lines = useFlowStore.getState().runOutput.split('\n');
    expect(lines.length).toBeLessThanOrEqual(MAX);
    expect(lines[lines.length - 1]).toBe(`line ${MAX + 200 - 1}`);
  });

  it('clearExecution also resets runOutput', () => {
    useFlowStore.getState().appendRunOutput('residue');
    useFlowStore.getState().clearExecution();
    expect(useFlowStore.getState().runOutput).toBe('');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/executionRunOutput.test.ts`
Expected: FAIL — `appendRunOutput is not a function` / `runOutput` undefined.

- [ ] **Step 3: Implement in executionSlice.ts**

Add the cap constant above the slice (after the imports):

```ts
const MAX_RUN_OUTPUT_LINES = 5000;
```

Add to the `ExecutionSlice` interface (after `pathVisible: boolean;`):

```ts
  /** Live mirror of the main form's output box (full combined run stream). */
  runOutput: string;
```

Add to the action signatures (after `clearPath: () => void;`):

```ts
  appendRunOutput: (chunk: string) => void;
  clearRunOutput: () => void;
```

Add `runOutput: '',` to the initial state object (next to `pathVisible: true,`).

Add the implementations (next to `clearPath`):

```ts
  appendRunOutput: (chunk) => set((s) => {
    let next = s.runOutput + chunk;
    // Bound memory + DOM: keep only the last MAX_RUN_OUTPUT_LINES lines.
    const lines = next.split('\n');
    if (lines.length > MAX_RUN_OUTPUT_LINES) {
      next = lines.slice(lines.length - MAX_RUN_OUTPUT_LINES).join('\n');
    }
    return { runOutput: next };
  }),

  clearRunOutput: () => set({ runOutput: '' }),
```

In `clearExecution`, add `runOutput: '',` to the first `set({ ... })` object (next to `pathVisible: true,`).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/executionRunOutput.test.ts`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/slices/executionSlice.ts FlowCanvas/src/stores/slices/__tests__/executionRunOutput.test.ts
git commit -m "feat(flow-canvas): runOutput buffer in executionSlice with 5000-line cap

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 1.3: View-pref state in uiSlice

**Files:**
- Modify: `FlowCanvas/src/stores/slices/uiSlice.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts`

These toggles emit `layout-save` from day one. The C# side ignores unknown fields until Phase 7, so this is harmless and means no rework when persistence lands.

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts`:

```ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save', prefSave: 'pref-save', setLayoutMode: 'set-layout-mode' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';

describe('uiSlice run-output view prefs', () => {
  beforeEach(() => {
    useFlowStore.setState({ outputTab: 'block', runOutputColor: true, runOutputWrap: false, runOutputFollow: true, runOutputUnread: false });
    vi.clearAllMocks();
  });

  it('defaults: block tab, color on, wrap off, follow on', () => {
    const s = useFlowStore.getState();
    expect([s.outputTab, s.runOutputColor, s.runOutputWrap, s.runOutputFollow]).toEqual(['block', true, false, true]);
  });

  it('setOutputTab switches and clears unread when showing run', () => {
    useFlowStore.setState({ runOutputUnread: true });
    useFlowStore.getState().setOutputTab('run');
    expect(useFlowStore.getState().outputTab).toBe('run');
    expect(useFlowStore.getState().runOutputUnread).toBe(false);
  });

  it('toggleRunOutputColor flips state and persists via layout-save', () => {
    useFlowStore.getState().toggleRunOutputColor();
    expect(useFlowStore.getState().runOutputColor).toBe(false);
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', runOutputColor: false }));
  });

  it('toggleRunOutputWrap and toggleRunOutputFollow persist via layout-save', () => {
    useFlowStore.getState().toggleRunOutputWrap();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', runOutputWrap: true }));
    useFlowStore.getState().toggleRunOutputFollow();
    expect(messageBus.send).toHaveBeenCalledWith(expect.objectContaining({ type: 'layout-save', runOutputFollow: false }));
  });

  it('restore setters apply without echo', () => {
    useFlowStore.getState().restoreRunOutputPrefs({ runOutputColor: false, runOutputWrap: true, runOutputFollow: false });
    const s = useFlowStore.getState();
    expect([s.runOutputColor, s.runOutputWrap, s.runOutputFollow]).toEqual([false, true, false]);
    expect(messageBus.send).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: FAIL — `setOutputTab is not a function`.

- [ ] **Step 3: Implement in uiSlice.ts**

Add to the `UISlice` interface (after the `panelSizes: PanelSizes;` line):

```ts
  // Run Output tab view state
  outputTab: 'block' | 'run';
  runOutputColor: boolean;
  runOutputWrap: boolean;
  runOutputFollow: boolean;
  runOutputUnread: boolean;
```

Add to the interface's action list (after `restorePanelSizes`):

```ts
  setOutputTab: (tab: 'block' | 'run') => void;
  setRunOutputUnread: (unread: boolean) => void;
  toggleRunOutputColor: () => void;
  toggleRunOutputWrap: () => void;
  toggleRunOutputFollow: () => void;
  restoreRunOutputPrefs: (prefs: Partial<{ runOutputColor: boolean; runOutputWrap: boolean; runOutputFollow: boolean }>) => void;
```

Add to the initial state object (after `panelSizes: { ...DEFAULT_PANEL_SIZES },`):

```ts
  outputTab: 'block',
  runOutputColor: true,
  runOutputWrap: false,
  runOutputFollow: true,
  runOutputUnread: false,
```

Add the implementations (after `restorePanelSizes`):

```ts
  setOutputTab: (tab) => set((s) => ({
    outputTab: tab,
    runOutputUnread: tab === 'run' ? false : s.runOutputUnread,
  })),

  setRunOutputUnread: (unread) => set({ runOutputUnread: unread }),

  toggleRunOutputColor: () => set((s) => {
    const next = !s.runOutputColor;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, runOutputColor: next });
    return { runOutputColor: next };
  }),

  toggleRunOutputWrap: () => set((s) => {
    const next = !s.runOutputWrap;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, runOutputWrap: next });
    return { runOutputWrap: next };
  }),

  toggleRunOutputFollow: () => set((s) => {
    const next = !s.runOutputFollow;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, runOutputFollow: next });
    return { runOutputFollow: next };
  }),

  restoreRunOutputPrefs: (prefs) => set((s) => ({
    runOutputColor: prefs.runOutputColor ?? s.runOutputColor,
    runOutputWrap: prefs.runOutputWrap ?? s.runOutputWrap,
    runOutputFollow: prefs.runOutputFollow ?? s.runOutputFollow,
  })),
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/slices/uiSlice.ts FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts
git commit -m "feat(flow-canvas): run-output tab + color/wrap/follow view prefs in uiSlice

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 1.4: Bridge the new messages to the store

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts`

- [ ] **Step 1: Add handlers next to the stepOutput handler**

In `initMessageBridge`, inside the `unsubs` array, immediately after the existing `stepOutput` handler (the `messageBus.on(CANVAS_HOST_MESSAGES.incoming.stepOutput, ...)` block), add:

```ts
    // Full run output — live mirror of the main form's output box
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutput, (msg) => {
      if (typeof msg.chunk === 'string' && msg.chunk.length > 0) {
        store.getState().appendRunOutput(msg.chunk);
      }
    }),

    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutputClear, () => {
      store.getState().clearRunOutput();
    }),
```

- [ ] **Step 2: Type-check**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 3: Run the full React suite (no regressions)**

Run: `cd FlowCanvas && npm test`
Expected: PASS (existing suite green; the two new slice specs included).

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): route run-output / run-output-clear into the store

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> **CHECKPOINT — end of Phase 1.** React data layer is complete and unit-tested. Nothing is user-visible yet. Stop for review before Phase 2.

---

## Phase 2 — C# mirror

### Task 2.1: `FlowCanvasForm` send wrappers

**Files:**
- Modify: `UI/FlowCanvasForm.cs`
- Test: `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputTests.cs`

- [ ] **Step 1: Write the failing test**

Create `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputTests.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class FlowCanvasFormRunOutputTests
{
    [WinFormsFact]
    public void SendRunOutputAppend_QueuesRunOutputMessageWithChunk()
    {
        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);

        flowCanvas.SendRunOutputAppend("### CONNECTED ###\nhello\n");

        var queue = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        var msg = ReadMessageOfType(queue, "run-output");
        msg.Should().NotBeNull();
        msg!["chunk"]?.ToString().Should().Be("### CONNECTED ###\nhello\n");
    }

    [WinFormsFact]
    public void SendRunOutputClear_QueuesRunOutputClearMessage()
    {
        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);

        flowCanvas.SendRunOutputClear();

        var queue = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        ReadMessageOfType(queue, "run-output-clear").Should().NotBeNull();
    }

    private static JObject? ReadMessageOfType(ConcurrentQueue<string> queue, string expectedType)
    {
        foreach (var json in queue.ToArray())
        {
            var parsed = JObject.Parse(json);
            if (string.Equals(parsed["type"]?.ToString(), expectedType, StringComparison.Ordinal))
                return parsed;
        }
        return null;
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized");
        return value!;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasFormRunOutputTests"`
Expected: FAIL — compile error: `SendRunOutputAppend` / `SendRunOutputClear` not defined.

- [ ] **Step 3: Implement the wrappers**

In `UI/FlowCanvasForm.cs`, immediately after the `SetTargetHost(object? hostData)` method (around line 311), add:

```csharp
        /// <summary>Appends a raw chunk of the main-form run output to the canvas's Run Output tab.</summary>
        public void SendRunOutputAppend(string chunk)
        {
            if (string.IsNullOrEmpty(chunk)) return;
            SendMessage(new { type = "run-output", chunk });
        }

        /// <summary>Clears the canvas's Run Output buffer (mirrors Form1.ClearOutput).</summary>
        public void SendRunOutputClear()
        {
            SendMessage(new { type = "run-output-clear" });
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasFormRunOutputTests"`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add UI/FlowCanvasForm.cs SSH_Helper.Tests/UI/FlowCanvasFormRunOutputTests.cs
git commit -m "feat(flow-canvas): SendRunOutputAppend/Clear wrappers on FlowCanvasForm

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2.2: Tap Form1's output paths + seed on open

**Files:**
- Modify: `Form1.cs` (3 sites)

No new unit test (requires a live Form1 + canvas); verified by build + the manual parity check in Task 4.4. Each edit is a single guarded line that no-ops when the canvas is closed.

- [ ] **Step 1: Forward live output from `AppendOutputToUi`**

In `Form1.cs`, in `AppendOutputToUi` (line ~13910), after `ScrollOutputToEnd();` (line 13922) and before the method's closing brace, add the forward. Forward the **raw** `output` (the canvas renders `\n` itself — do not send the WinForms-normalized form):

```csharp
            txtOutput.AppendText(NormalizeNewlinesForDisplay(output));
            ScrollOutputToEnd();

            // Mirror the same chunk into the Flow Canvas Run Output tab (no-op if closed).
            _flowCanvasForm?.SendRunOutputAppend(output);
```

- [ ] **Step 2: Forward clears from `ClearOutput`**

In `ClearOutput` (line ~13981), after the `finally { ... }` block closes (after line 14006) and before the method's closing brace at 14007, add:

```csharp
            finally
            {
                // Resume drawing and force repaint
                NativeMethods.SendMessage(txtOutput.Handle, NativeMethods.WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                txtOutput.Invalidate();
            }

            // Mirror the clear into the Flow Canvas Run Output tab (no-op if closed).
            _flowCanvasForm?.SendRunOutputClear();
```

- [ ] **Step 3: Seed the canvas on open**

In `OpenFlowCanvas` (line ~6620), after `SendTargetHostToCanvas();` (line 6780) and before the method's closing brace at 6781, add the seed. It queues behind the `ready` handshake and drains in order:

```csharp
            // Send the initial target host to the Host Bar
            SendTargetHostToCanvas();

            // Seed the Run Output tab with whatever the main output box currently holds,
            // so a canvas opened after a run isn't empty. Clear first so reopening doesn't double up.
            _flowCanvasForm.SendRunOutputClear();
            _flowCanvasForm.SendRunOutputAppend(GetBufferedOutputSnapshot());
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Form1.cs
git commit -m "feat(flow-canvas): mirror main-form output into the canvas (taps + seed)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> **CHECKPOINT — end of Phase 2.** Output now flows to the store, but there's no UI to show the Run Output buffer yet. Stop for review before Phase 3.

---

## Phase 3 — Console component

### Task 3.1: Line classifier

**Files:**
- Create: `FlowCanvas/src/utils/runOutputClassify.ts`
- Test: `FlowCanvas/src/utils/__tests__/runOutputClassify.test.ts`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/utils/__tests__/runOutputClassify.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { classifyRunOutputLine } from '../runOutputClassify';

describe('classifyRunOutputLine', () => {
  it('classifies the ### banner delimiter as banner', () => {
    expect(classifyRunOutputLine('############### CONNECTED TO 10.0.0.5 admin@fw> ###############')).toBe('banner');
  });

  it('classifies obvious error lines as error', () => {
    expect(classifyRunOutputLine('command parse error before \'sytsem\'')).toBe('error');
    expect(classifyRunOutputLine('Command fail. Return code -61')).toBe('error');
    expect(classifyRunOutputLine('% Invalid input detected')).toBe('error');
    expect(classifyRunOutputLine('Permission denied')).toBe('error');
  });

  it('classifies plain output as normal', () => {
    expect(classifyRunOutputLine('Version: FortiGate-100F v7.4.3')).toBe('normal');
    expect(classifyRunOutputLine('Uptime: 47 days')).toBe('normal');
  });

  it('does not treat a short hash comment as a banner', () => {
    expect(classifyRunOutputLine('# a comment')).toBe('normal');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/utils/__tests__/runOutputClassify.test.ts`
Expected: FAIL — cannot find module `../runOutputClassify`.

- [ ] **Step 3: Implement the classifier**

Create `FlowCanvas/src/utils/runOutputClassify.ts`:

```ts
export type RunOutputLineKind = 'banner' | 'error' | 'normal';

// Form1's host/script delimiters look like: "############### CONNECTED TO ... ###############"
const BANNER_RE = /^#{6,}.*#{6,}$/;

// Conservative, best-effort error heuristic. Cosmetic only — the Color toggle is the escape hatch.
const ERROR_RE = /(command (parse )?error|command fail|return code\s+-?\d|%\s*invalid|permission denied|\bfail(?:ed)?\b)/i;

export function classifyRunOutputLine(line: string): RunOutputLineKind {
  const trimmed = line.trim();
  if (BANNER_RE.test(trimmed)) return 'banner';
  if (ERROR_RE.test(line)) return 'error';
  return 'normal';
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/utils/__tests__/runOutputClassify.test.ts`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/runOutputClassify.ts FlowCanvas/src/utils/__tests__/runOutputClassify.test.ts
git commit -m "feat(flow-canvas): run-output line classifier (banner/error/normal)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3.2: `RunOutputView` console component

**Files:**
- Create: `FlowCanvas/src/panels/RunOutputView.tsx`
- Test: `FlowCanvas/src/panels/__tests__/RunOutputView.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/panels/__tests__/RunOutputView.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    runOutput: '',
    isRunning: false,
    runOutputColor: true,
    runOutputWrap: false,
    runOutputFollow: true,
    toggleRunOutputColor: vi.fn(),
    toggleRunOutputWrap: vi.fn(),
    toggleRunOutputFollow: vi.fn(),
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputView from '../RunOutputView';

describe('RunOutputView', () => {
  beforeEach(() => {
    mock.state.runOutput = '';
    mock.state.isRunning = false;
    mock.state.runOutputColor = true;
    vi.clearAllMocks();
  });

  it('shows an empty-state hint when there is no output', () => {
    render(<RunOutputView />);
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
    expect(screen.getByText(/no run output yet/i)).toBeInTheDocument();
  });

  it('renders one element per line with a data-kind when color is on', () => {
    mock.state.runOutput = '############### CONNECTED TO h ###############\nVersion 1.0\nCommand fail. Return code -1';
    render(<RunOutputView />);
    const lines = screen.getAllByTestId('run-output-line');
    expect(lines).toHaveLength(3);
    expect(lines[0].getAttribute('data-kind')).toBe('banner');
    expect(lines[1].getAttribute('data-kind')).toBe('normal');
    expect(lines[2].getAttribute('data-kind')).toBe('error');
  });

  it('renders plain text (no per-line kinds) when color is off', () => {
    mock.state.runOutput = '############### CONNECTED ###############\nplain';
    mock.state.runOutputColor = false;
    render(<RunOutputView />);
    expect(screen.queryAllByTestId('run-output-line')).toHaveLength(0);
    expect(screen.getByTestId('run-output-plain').textContent).toContain('############### CONNECTED ###############');
  });

  it('shows the LIVE indicator only while running', () => {
    mock.state.isRunning = true;
    render(<RunOutputView />);
    expect(screen.getByTestId('run-output-live')).toBeInTheDocument();
  });

  it('Color button toggles the color pref', () => {
    render(<RunOutputView />);
    screen.getByTestId('run-output-btn-color').click();
    expect(mock.state.toggleRunOutputColor).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputView.test.tsx`
Expected: FAIL — cannot find module `../RunOutputView`.

- [ ] **Step 3: Implement the component**

Create `FlowCanvas/src/panels/RunOutputView.tsx`:

```tsx
/**
 * Run Output console — a live mirror of the main form's output box.
 * Renders the executionSlice.runOutput buffer with optional light styling
 * (teal banners, red error lines) gated behind the Color toggle.
 */
import { useEffect, useMemo, useRef } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { classifyRunOutputLine } from '../utils/runOutputClassify';

const KIND_COLOR: Record<string, string> = {
  banner: 'var(--fc-host-accent)',
  error: 'var(--fc-state-error)',
  normal: 'var(--fc-term-text)',
};

export default function RunOutputView() {
  const runOutput = useFlowStore((s) => s.runOutput);
  const isRunning = useFlowStore((s) => s.isRunning);
  const color = useFlowStore((s) => s.runOutputColor);
  const wrap = useFlowStore((s) => s.runOutputWrap);
  const follow = useFlowStore((s) => s.runOutputFollow);
  const toggleColor = useFlowStore((s) => s.toggleRunOutputColor);
  const toggleWrap = useFlowStore((s) => s.toggleRunOutputWrap);
  const toggleFollow = useFlowStore((s) => s.toggleRunOutputFollow);

  const scrollRef = useRef<HTMLDivElement>(null);

  const lines = useMemo(() => (color ? runOutput.split('\n') : null), [color, runOutput]);

  // Stick-to-bottom while following.
  useEffect(() => {
    if (follow && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [runOutput, follow]);

  // If the user scrolls up, stop following (re-enable via the Follow button).
  const onScroll = () => {
    const el = scrollRef.current;
    if (!el || !follow) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 24;
    if (!atBottom) toggleFollow();
  };

  const hasOutput = runOutput.length > 0;
  const whiteSpace = wrap ? 'pre-wrap' : 'pre';

  return (
    <div
      data-testid="run-output-view"
      style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0, background: 'var(--fc-term-bg)' }}
    >
      {/* Toolbar */}
      <div style={{
        display: 'flex', alignItems: 'center', gap: 2, padding: '0 8px', height: 26,
        background: 'var(--fc-term-surface)', borderBottom: '1px solid var(--fc-border)', flexShrink: 0,
      }}>
        {isRunning && (
          <span data-testid="run-output-live" style={{ display: 'flex', alignItems: 'center', gap: 5, marginRight: 8, fontSize: 10, fontWeight: 600, color: 'var(--fc-state-success)' }}>
            <span style={{ width: 7, height: 7, borderRadius: '50%', background: 'var(--fc-state-success)', boxShadow: '0 0 6px var(--fc-state-success)' }} />
            LIVE
          </span>
        )}
        <div style={{ flex: 1 }} />
        <ToolbarButton testid="run-output-btn-follow" active={follow} onClick={toggleFollow} title="Stick to bottom">⤓ Follow</ToolbarButton>
        <ToolbarButton testid="run-output-btn-wrap" active={wrap} onClick={toggleWrap} title="Word wrap">↵ Wrap</ToolbarButton>
        <ToolbarButton testid="run-output-btn-color" active={color} onClick={toggleColor} title="Colorize output">🎨 Color</ToolbarButton>
        <ToolbarButton testid="run-output-btn-copy" active={false} onClick={() => navigator.clipboard.writeText(runOutput)} title="Copy all">⧉ Copy</ToolbarButton>
      </div>

      {/* Body */}
      <div
        ref={scrollRef}
        onScroll={onScroll}
        style={{ flex: 1, overflow: 'auto', padding: 8, fontFamily: 'var(--fc-font-mono)', fontSize: 11, lineHeight: 1.5 }}
      >
        {!hasOutput && (
          <div style={{ color: 'var(--fc-text-muted)' }}>No run output yet — run a script to see it here.</div>
        )}
        {hasOutput && color && (
          <div>
            {lines!.map((line, i) => {
              const kind = classifyRunOutputLine(line);
              return (
                <div key={i} data-testid="run-output-line" data-kind={kind} style={{ color: KIND_COLOR[kind], whiteSpace }}>
                  {line || ' '}
                </div>
              );
            })}
          </div>
        )}
        {hasOutput && !color && (
          <pre data-testid="run-output-plain" style={{ margin: 0, color: 'var(--fc-term-text)', whiteSpace, fontFamily: 'inherit' }}>{runOutput}</pre>
        )}
      </div>
    </div>
  );
}

function ToolbarButton({ testid, active, onClick, title, children }: {
  testid: string; active: boolean; onClick: () => void; title: string; children: React.ReactNode;
}) {
  return (
    <button
      data-testid={testid}
      onClick={onClick}
      title={title}
      style={{
        fontSize: 11, fontWeight: 600, padding: '4px 7px', borderRadius: 5, cursor: 'pointer',
        background: active ? 'var(--fc-accent-surface)' : 'transparent',
        color: active ? 'var(--fc-accent)' : 'var(--fc-text-muted)',
        border: `1px solid ${active ? 'var(--fc-border-selected)' : 'transparent'}`,
        fontFamily: 'inherit',
      }}
    >
      {children}
    </button>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputView.test.tsx`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/panels/RunOutputView.tsx FlowCanvas/src/panels/__tests__/RunOutputView.test.tsx
git commit -m "feat(flow-canvas): RunOutputView console (color/wrap/follow/copy + LIVE)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> **CHECKPOINT — end of Phase 3.** The console renders and is tested, but isn't mounted in the dock yet. Stop for review before Phase 4.

---

## Phase 4 — Tabbed dock

### Task 4.1: Tab header + child switching in OutputPreview

**Files:**
- Modify: `FlowCanvas/src/panels/OutputPreview.tsx`
- Test: `FlowCanvas/src/panels/__tests__/OutputPreviewTabs.test.tsx`

The existing OutputPreview body (block output) is preserved; a tab strip selects between the existing block view and the new `RunOutputView`.

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/panels/__tests__/OutputPreviewTabs.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    blockOutputs: new Map(),
    togglePanel: vi.fn(),
    panelSizes: { outputHeight: 200 },
    setPanelSize: vi.fn(),
    outputTab: 'block' as 'block' | 'run',
    setOutputTab: vi.fn((t: 'block' | 'run') => { mock.state.outputTab = t; }),
    runOutputUnread: false,
    // RunOutputView selectors (used when the run tab is active):
    runOutput: '',
    isRunning: false,
    runOutputColor: true,
    runOutputWrap: false,
    runOutputFollow: true,
    toggleRunOutputColor: vi.fn(),
    toggleRunOutputWrap: vi.fn(),
    toggleRunOutputFollow: vi.fn(),
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import OutputPreview from '../OutputPreview';

function renderPanel() {
  return render(<OutputPreview output="" />);
}

describe('OutputPreview tabs', () => {
  beforeEach(() => {
    mock.state.outputTab = 'block';
    mock.state.runOutputUnread = false;
    vi.clearAllMocks();
  });

  it('renders both tab buttons', () => {
    renderPanel();
    expect(screen.getByTestId('output-tab-block')).toBeInTheDocument();
    expect(screen.getByTestId('output-tab-run')).toBeInTheDocument();
  });

  it('shows the block view by default, not the run console', () => {
    renderPanel();
    expect(screen.queryByTestId('run-output-view')).toBeNull();
  });

  it('clicking the Run tab calls setOutputTab', () => {
    renderPanel();
    screen.getByTestId('output-tab-run').click();
    expect(mock.state.setOutputTab).toHaveBeenCalledWith('run');
  });

  it('shows the run console when outputTab is run', () => {
    mock.state.outputTab = 'run';
    renderPanel();
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
  });

  it('shows an unread dot on the Run tab when runOutputUnread and not on it', () => {
    mock.state.runOutputUnread = true;
    renderPanel();
    expect(screen.getByTestId('output-tab-run-unread')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/OutputPreviewTabs.test.tsx`
Expected: FAIL — no `output-tab-block` testid.

- [ ] **Step 3: Implement tabs in OutputPreview.tsx**

Add the import at the top (after the `useFlowStore` import):

```tsx
import RunOutputView from './RunOutputView';
```

Add these selectors next to the existing ones inside the component:

```tsx
  const outputTab = useFlowStore((s) => s.outputTab);
  const setOutputTab = useFlowStore((s) => s.setOutputTab);
  const runOutputUnread = useFlowStore((s) => s.runOutputUnread);
```

Replace the existing `{/* Header */}` block (the `<div>` containing the `Output` span, blockLabel, history pager, Copy, and `×`) so that:
1. A tab strip is inserted between the resize handle and the header.
2. The block-specific header controls only render when `outputTab === 'block'`.

Insert the tab strip immediately after the resize-handle `</div>` and before the header `<div>`:

```tsx
      {/* Tab strip */}
      <div style={{
        display: 'flex', alignItems: 'stretch', height: 26, flexShrink: 0,
        background: 'var(--fc-term-surface)', borderBottom: '1px solid var(--fc-term-surface-2)',
      }}>
        <TabButton testid="output-tab-block" active={outputTab === 'block'} onClick={() => setOutputTab('block')}>
          Block Output
        </TabButton>
        <TabButton testid="output-tab-run" active={outputTab === 'run'} onClick={() => setOutputTab('run')}>
          Run Output
          {runOutputUnread && outputTab !== 'run' && (
            <span data-testid="output-tab-run-unread" style={{
              marginLeft: 6, width: 6, height: 6, borderRadius: '50%',
              background: 'var(--fc-accent)', display: 'inline-block',
            }} />
          )}
        </TabButton>
        <div style={{ flex: 1 }} />
        <button onClick={handleClose} title="Unpin output panel" style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted)', cursor: 'pointer', fontSize: 14, padding: '0 8px',
        }}>×</button>
      </div>
```

Wrap the existing `{/* Header */}` div and the `{/* Content */}` `<pre>` so they only render for the block tab, and render `RunOutputView` for the run tab. Replace the existing header+content region with:

```tsx
      {outputTab === 'block' ? (
        <>
          {/* Header (block) */}
          <div style={{
            padding: '2px 10px', background: 'var(--fc-term-surface)',
            borderBottom: '1px solid var(--fc-term-surface-2)', display: 'flex', alignItems: 'center',
            fontSize: 12, height: headerHeight, flexShrink: 0,
          }}>
            <span style={{ color: 'var(--fc-text-muted)' }}>Output</span>
            {blockLabel && (
              <span style={{ color: 'var(--fc-accent)', marginLeft: 8, fontSize: 11 }}>{blockLabel}</span>
            )}
            {allOutputs.length > 1 && (
              <span style={{ color: 'var(--fc-text-muted)', marginLeft: 8, fontSize: 10 }}>
                ({historyIndex >= 0 ? historyIndex + 1 : allOutputs.length}/{allOutputs.length})
                <button
                  onClick={() => setHistoryIndex(Math.max(0, (historyIndex < 0 ? allOutputs.length - 1 : historyIndex) - 1))}
                  style={{ background: 'none', border: 'none', color: 'var(--fc-accent)', cursor: 'pointer', fontSize: 10, padding: '0 4px' }}
                >◀</button>
                <button
                  onClick={() => {
                    const next = (historyIndex < 0 ? allOutputs.length : historyIndex) + 1;
                    setHistoryIndex(next >= allOutputs.length ? -1 : next);
                  }}
                  style={{ background: 'none', border: 'none', color: 'var(--fc-accent)', cursor: 'pointer', fontSize: 10, padding: '0 4px' }}
                >▶</button>
              </span>
            )}
            <div style={{ flex: 1 }} />
            {hasOutput && (
              <button onClick={() => navigator.clipboard.writeText(displayOutput)} style={{
                background: 'none', border: 'none', color: 'var(--fc-text-muted)', cursor: 'pointer', fontSize: 11, marginRight: 8,
              }}>Copy</button>
            )}
          </div>
          {/* Content (block) */}
          <pre style={{
            margin: 0, padding: 8, fontSize: 11,
            color: hasOutput ? 'var(--fc-term-text)' : 'var(--fc-text-muted)',
            lineHeight: 1.5, overflowY: 'auto', flex: 1, fontFamily: 'var(--fc-font-mono)',
            whiteSpace: 'pre-wrap', wordBreak: 'break-all',
          }}>
            {hasOutput ? displayOutput : (nodeId ? '(no output)' : 'Select a block to view its output')}
          </pre>
        </>
      ) : (
        <RunOutputView />
      )}
```

Add the `TabButton` helper at the bottom of the file (after the component):

```tsx
function TabButton({ testid, active, onClick, children }: {
  testid: string; active: boolean; onClick: () => void; children: React.ReactNode;
}) {
  return (
    <button
      data-testid={testid}
      onClick={onClick}
      style={{
        display: 'flex', alignItems: 'center', padding: '0 12px', fontSize: 11, fontWeight: 600,
        background: 'none', border: 'none', cursor: 'pointer', fontFamily: 'inherit',
        color: active ? 'var(--fc-text)' : 'var(--fc-text-muted)',
        borderBottom: `2px solid ${active ? 'var(--fc-accent)' : 'transparent'}`,
      }}
    >
      {children}
    </button>
  );
}
```

> Note: the old `×` close button that lived in the block header is now in the shared tab strip — make sure it is not duplicated inside the block header block above (it was removed in the replacement).

- [ ] **Step 4: Run test to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/OutputPreviewTabs.test.tsx`
Expected: PASS (5 tests).

- [ ] **Step 5: Type-check the whole app**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/panels/OutputPreview.tsx FlowCanvas/src/panels/__tests__/OutputPreviewTabs.test.tsx
git commit -m "feat(flow-canvas): tabbed bottom dock — Block Output | Run Output

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4.2: Auto-switch to Run Output on run start; mark unread on output

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts`

- [ ] **Step 1: Auto-switch on run start**

In the existing `executionStarted` handler (currently calls `clearExecution`, `clearTimeline`, `setRunning(true)`, `clearExportStatus`), add a line to focus the Run Output tab:

```ts
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.executionStarted, () => {
      store.getState().clearExecution();
      store.getState().clearTimeline();
      store.getState().setRunning(true);
      store.getState().clearExportStatus();
      store.getState().setOutputTab('run');
    }),
```

- [ ] **Step 2: Mark unread when output arrives off-tab**

Update the `runOutput` handler added in Task 1.4 so it flags unread when the user isn't on the Run tab:

```ts
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.runOutput, (msg) => {
      if (typeof msg.chunk === 'string' && msg.chunk.length > 0) {
        store.getState().appendRunOutput(msg.chunk);
        if (store.getState().outputTab !== 'run') {
          store.getState().setRunOutputUnread(true);
        }
      }
    }),
```

- [ ] **Step 3: Type-check + run the React suite**

Run: `cd FlowCanvas && npx tsc --noEmit && npm test`
Expected: PASS (no regressions).

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): focus Run Output on run start; unread dot off-tab

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4.3: Full build

- [ ] **Step 1: Build the whole solution including the React bundle**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded (runs `npm run build` in FlowCanvas/ via the BuildFlowCanvas target, then the .NET build). 0 errors.

- [ ] **Step 2: Commit (only if the build produced tracked artifact changes; otherwise skip)**

```bash
git status --porcelain
# If nothing tracked changed, no commit needed.
```

---

### Task 4.4: Manual parity verification

- [ ] **Step 1: Run the app and verify the feature end-to-end**

Run: `dotnet run --project SSH_Helper.csproj`

Verify:
1. Open the Flow Canvas. The bottom dock shows **Block Output | Run Output** tabs.
2. Run a script (multi-host if possible) from the canvas. The dock auto-switches to **Run Output**; the **LIVE** dot shows; output streams in with teal `###` banners and red error lines.
3. Compare against the main form's output box — same text, same host banners, same ordering.
4. Toggle **Color** off → plain text, byte-for-byte like the main box. **Wrap** toggles word-wrap. Scroll up → **Follow** unsticks; click **Follow** → re-sticks to bottom. **Copy** copies the buffer.
5. Re-run → the Run Output clears and refills.
6. Close the canvas, run a script from the main form, reopen the canvas → Run Output is **seeded** with the existing output.

- [ ] **Step 2: Note any defects and loop back to the relevant task. When clean, the core feature is done.**

> **CHECKPOINT — end of Phase 4.** Core feature complete and verified. Phases 5–7 are independent enhancements; implement as desired.

---

## Phase 5 — Find in output (independent follow-up)

### Task 5.1: In-panel find

**Files:**
- Modify: `FlowCanvas/src/panels/RunOutputView.tsx`
- Test: `FlowCanvas/src/panels/__tests__/RunOutputFind.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/panels/__tests__/RunOutputFind.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    runOutput: 'alpha\nbravo error\ncharlie\nbravo again',
    isRunning: false,
    runOutputColor: true,
    runOutputWrap: false,
    runOutputFollow: false,
    toggleRunOutputColor: vi.fn(),
    toggleRunOutputWrap: vi.fn(),
    toggleRunOutputFollow: vi.fn(),
  } as any,
}));

vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputView from '../RunOutputView';

describe('RunOutputView find', () => {
  beforeEach(() => { vi.clearAllMocks(); });

  it('opens the find box when Find is clicked', () => {
    render(<RunOutputView />);
    screen.getByTestId('run-output-btn-find').click();
    expect(screen.getByTestId('run-output-find-input')).toBeInTheDocument();
  });

  it('reports the match count for the query', () => {
    render(<RunOutputView />);
    screen.getByTestId('run-output-btn-find').click();
    fireEvent.change(screen.getByTestId('run-output-find-input'), { target: { value: 'bravo' } });
    expect(screen.getByTestId('run-output-find-count').textContent).toContain('2');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputFind.test.tsx`
Expected: FAIL — no `run-output-btn-find`.

- [ ] **Step 3: Implement find**

In `RunOutputView.tsx`, add find state at the top of the component:

```tsx
  const [findOpen, setFindOpen] = useState(false);
  const [findQuery, setFindQuery] = useState('');
```

(Add `useState` to the React import: `import { useEffect, useMemo, useRef, useState } from 'react';`.)

Compute the match count:

```tsx
  const matchCount = useMemo(() => {
    if (!findQuery) return 0;
    const q = findQuery.toLowerCase();
    let count = 0, idx = 0;
    const hay = runOutput.toLowerCase();
    while ((idx = hay.indexOf(q, idx)) !== -1) { count++; idx += q.length; }
    return count;
  }, [findQuery, runOutput]);
```

Add a Find button to the toolbar (before the Follow button):

```tsx
        <ToolbarButton testid="run-output-btn-find" active={findOpen} onClick={() => setFindOpen((v) => !v)} title="Find">⌕ Find</ToolbarButton>
```

Add the find box between the toolbar and the body `<div ref={scrollRef}>`:

```tsx
      {findOpen && (
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8, padding: '4px 8px', flexShrink: 0,
          background: 'var(--fc-term-surface)', borderBottom: '1px solid var(--fc-border)',
        }}>
          <input
            data-testid="run-output-find-input"
            autoFocus
            value={findQuery}
            onChange={(e) => setFindQuery(e.target.value)}
            placeholder="Find in output…"
            style={{
              flex: 1, background: 'var(--fc-term-bg)', color: 'var(--fc-term-text)',
              border: '1px solid var(--fc-border)', borderRadius: 4, padding: '3px 6px',
              fontFamily: 'var(--fc-font-mono)', fontSize: 11,
            }}
          />
          <span data-testid="run-output-find-count" style={{ fontSize: 10, color: 'var(--fc-text-muted)' }}>
            {findQuery ? `${matchCount} match${matchCount === 1 ? '' : 'es'}` : ''}
          </span>
        </div>
      )}
```

Highlight matches in the colored line renderer by wrapping matched substrings. Replace the `{line || ' '}` child with a call to a highlight helper:

```tsx
                  {highlight(line, findQuery)}
```

Add the helper at the bottom of the file:

```tsx
function highlight(line: string, query: string): React.ReactNode {
  if (!query) return line || ' ';
  const q = query.toLowerCase();
  const lower = line.toLowerCase();
  const parts: React.ReactNode[] = [];
  let i = 0, key = 0, idx;
  while ((idx = lower.indexOf(q, i)) !== -1) {
    if (idx > i) parts.push(line.slice(i, idx));
    parts.push(<mark key={key++} style={{ background: 'var(--fc-accent)', color: 'var(--fc-term-bg)' }}>{line.slice(idx, idx + q.length)}</mark>);
    i = idx + q.length;
  }
  if (i < line.length) parts.push(line.slice(i));
  return parts.length ? parts : (line || ' ');
}
```

> Note: highlighting applies in the colored renderer. Under Color-off (plain `<pre>`), find still reports counts; full plain-text highlight is out of v1 find scope.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputFind.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/panels/RunOutputView.tsx FlowCanvas/src/panels/__tests__/RunOutputFind.test.tsx
git commit -m "feat(flow-canvas): find-in-output for the Run Output console

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase 6 — Pop out (independent follow-up)

### Task 6.1: Pop-out state in uiSlice

**Files:**
- Modify: `FlowCanvas/src/stores/slices/uiSlice.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts` (extend)

- [ ] **Step 1: Add a failing assertion to the existing uiSlice run-output test**

Append to `uiSliceRunOutput.test.ts` (inside the same `describe`):

```ts
  it('toggleRunOutputPoppedOut flips the floating-overlay flag', () => {
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(false);
    useFlowStore.getState().toggleRunOutputPoppedOut();
    expect(useFlowStore.getState().runOutputPoppedOut).toBe(true);
  });
```

Also add `runOutputPoppedOut: false` to the `useFlowStore.setState({ ... })` reset in `beforeEach`.

- [ ] **Step 2: Run to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: FAIL — `toggleRunOutputPoppedOut is not a function`.

- [ ] **Step 3: Implement**

In `uiSlice.ts`: add `runOutputPoppedOut: boolean;` to the interface state, `toggleRunOutputPoppedOut: () => void;` to the actions, `runOutputPoppedOut: false,` to the initial state, and the implementation:

```ts
  toggleRunOutputPoppedOut: () => set((s) => ({ runOutputPoppedOut: !s.runOutputPoppedOut })),
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/stores/slices/__tests__/uiSliceRunOutput.test.ts`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/slices/uiSlice.ts FlowCanvas/src/stores/slices/__tests__/uiSliceRunOutput.test.ts
git commit -m "feat(flow-canvas): runOutputPoppedOut state for the Run Output console

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 6.2: Floating, draggable overlay

**Files:**
- Modify: `FlowCanvas/src/panels/RunOutputView.tsx` (Pop-out button)
- Create: `FlowCanvas/src/panels/RunOutputPopOut.tsx` (draggable wrapper)
- Modify: `FlowCanvas/src/App.tsx` (mount the overlay)
- Test: `FlowCanvas/src/panels/__tests__/RunOutputPopOut.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `FlowCanvas/src/panels/__tests__/RunOutputPopOut.test.tsx`:

```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import React from 'react';

const mock = vi.hoisted(() => ({
  state: {
    runOutputPoppedOut: true,
    toggleRunOutputPoppedOut: vi.fn(),
    runOutput: 'hello world',
    isRunning: false,
    runOutputColor: true, runOutputWrap: false, runOutputFollow: false,
    toggleRunOutputColor: vi.fn(), toggleRunOutputWrap: vi.fn(), toggleRunOutputFollow: vi.fn(),
  } as any,
}));
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (selector: (s: any) => any) => selector(mock.state),
}));

import RunOutputPopOut from '../RunOutputPopOut';

describe('RunOutputPopOut', () => {
  beforeEach(() => { mock.state.runOutputPoppedOut = true; vi.clearAllMocks(); });

  it('renders a floating overlay containing the console', () => {
    render(<RunOutputPopOut />);
    expect(screen.getByTestId('run-output-popout')).toBeInTheDocument();
    expect(screen.getByTestId('run-output-view')).toBeInTheDocument();
  });

  it('renders nothing when not popped out', () => {
    mock.state.runOutputPoppedOut = false;
    const { container } = render(<RunOutputPopOut />);
    expect(container.firstChild).toBeNull();
  });

  it('the dock button calls toggleRunOutputPoppedOut', () => {
    render(<RunOutputPopOut />);
    fireEvent.click(screen.getByTestId('run-output-popout-dock'));
    expect(mock.state.toggleRunOutputPoppedOut).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputPopOut.test.tsx`
Expected: FAIL — cannot find module `../RunOutputPopOut`.

- [ ] **Step 3: Implement the draggable overlay**

Create `FlowCanvas/src/panels/RunOutputPopOut.tsx`:

```tsx
/** Floating, draggable overlay that hosts RunOutputView when popped out of the dock. */
import { useRef, useState } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import RunOutputView from './RunOutputView';

export default function RunOutputPopOut() {
  const poppedOut = useFlowStore((s) => s.runOutputPoppedOut);
  const toggle = useFlowStore((s) => s.toggleRunOutputPoppedOut);
  const [pos, setPos] = useState({ x: 220, y: 80 });
  const drag = useRef<{ dx: number; dy: number } | null>(null);

  if (!poppedOut) return null;

  const onMouseDown = (e: React.MouseEvent) => {
    drag.current = { dx: e.clientX - pos.x, dy: e.clientY - pos.y };
    const onMove = (ev: MouseEvent) => {
      if (!drag.current) return;
      setPos({ x: ev.clientX - drag.current.dx, y: ev.clientY - drag.current.dy });
    };
    const onUp = () => { drag.current = null; window.removeEventListener('mousemove', onMove); window.removeEventListener('mouseup', onUp); };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
  };

  return (
    <div
      data-testid="run-output-popout"
      style={{
        position: 'absolute', left: pos.x, top: pos.y, width: 520, height: 300, zIndex: 20,
        display: 'flex', flexDirection: 'column',
        background: 'var(--fc-panel-bg)', border: '1px solid var(--fc-panel-border)',
        borderRadius: 8, overflow: 'hidden', boxShadow: 'var(--fc-shadow-sm)',
      }}
    >
      <div
        onMouseDown={onMouseDown}
        style={{
          display: 'flex', alignItems: 'center', height: 24, padding: '0 8px', cursor: 'move',
          background: 'var(--fc-header-bg)', borderBottom: '1px solid var(--fc-panel-border)',
          fontSize: 11, fontWeight: 600, color: 'var(--fc-text-secondary)', flexShrink: 0,
        }}
      >
        ⠿ Run Output
        <div style={{ flex: 1 }} />
        <button data-testid="run-output-popout-dock" onClick={toggle} title="Dock back into the bottom panel" style={{
          background: 'none', border: 'none', color: 'var(--fc-text-muted)', cursor: 'pointer', fontSize: 12, padding: 0,
        }}>⤢ Dock</button>
      </div>
      <RunOutputView />
    </div>
  );
}
```

In `RunOutputView.tsx`, add a selector near the other selectors (so the test selector-shim resolves it; an absent field just yields `undefined`, which is a harmless no-op `onClick`):

```tsx
  const togglePoppedOut = useFlowStore((s) => s.toggleRunOutputPoppedOut);
```

Then add a Pop-out toolbar button (after the Copy button):

```tsx
        <ToolbarButton testid="run-output-btn-popout" active={false} onClick={togglePoppedOut} title="Pop out / dock">⤢ Pop out</ToolbarButton>
```

In `App.tsx`: import and mount the overlay as a sibling of `DebugPanel` (inside the `position: 'relative'` row container at line ~343). Add the import near the other panel imports:

```tsx
import RunOutputPopOut from './panels/RunOutputPopOut';
```

Mount it right after `<DebugPanel />` (App.tsx line ~413):

```tsx
          <RunOutputPopOut />
```

Finally, in `OutputPreview.tsx`, when the run tab is selected but popped out, render a placeholder instead of the inline console so it isn't shown twice. Add a selector at the top of the component (next to the other run-output selectors):

```tsx
  const poppedOut = useFlowStore((s) => s.runOutputPoppedOut);
```

Then replace `<RunOutputView />` in the run-tab branch with:

```tsx
        poppedOut
          ? <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--fc-text-muted)', fontSize: 11 }}>Run Output is popped out</div>
          : <RunOutputView />
```

(An absent `runOutputPoppedOut` in a test mock resolves to `undefined` → falsy → renders the console, so the Phase 4 tab tests keep passing.)

- [ ] **Step 4: Run the new test + type-check**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/RunOutputPopOut.test.tsx && npx tsc --noEmit`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/panels/RunOutputPopOut.tsx FlowCanvas/src/panels/RunOutputView.tsx FlowCanvas/src/panels/OutputPreview.tsx FlowCanvas/src/App.tsx FlowCanvas/src/panels/__tests__/RunOutputPopOut.test.tsx
git commit -m "feat(flow-canvas): pop the Run Output console into a draggable overlay

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase 7 — Persistence round-trip (independent follow-up)

The toggles already emit `layout-save` (Phase 1). This phase makes the C# host store those fields and replay them on open.

### Task 7.1: WindowState fields

**Files:**
- Modify: `Models/AppConfiguration.cs`

- [ ] **Step 1: Add nullable fields to `WindowState`**

In `Models/AppConfiguration.cs`, in the `WindowState` class after `FlowCanvasCompactComments`, add:

```csharp
        // Flow Canvas Run Output tab view prefs (persisted from React UI; null = use the React default)
        public bool? FlowCanvasRunOutputColor { get; set; }
        public bool? FlowCanvasRunOutputWrap { get; set; }
        public bool? FlowCanvasRunOutputFollow { get; set; }
```

- [ ] **Step 2: Build**

Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Models/AppConfiguration.cs
git commit -m "feat(flow-canvas): WindowState fields for Run Output view prefs

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 7.2: Save + restore in FlowCanvasForm

**Files:**
- Modify: `UI/FlowCanvasForm.cs`
- Test: `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputPrefsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `SSH_Helper.Tests/UI/FlowCanvasFormRunOutputPrefsTests.cs`:

```csharp
using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class FlowCanvasFormRunOutputPrefsTests
{
    [WinFormsFact]
    public void SavePanelSizes_PersistsRunOutputPrefs_ThenSendPersistedLayoutReplaysThem()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"RunOutputPrefs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        try
        {
            var configService = new ConfigurationService(Path.Combine(testDir, "config.json"));
            using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: configService);

            InvokePrivate(flowCanvas, "SavePanelSizes",
                JObject.FromObject(new { runOutputColor = false, runOutputWrap = true, runOutputFollow = false }));

            var ws = configService.GetCurrent().WindowState!;
            ws.FlowCanvasRunOutputColor.Should().BeFalse();
            ws.FlowCanvasRunOutputWrap.Should().BeTrue();
            ws.FlowCanvasRunOutputFollow.Should().BeFalse();

            var queue = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
            InvokePrivate(flowCanvas, "SendPersistedLayout");
            var restore = ReadMessageOfType(queue, "layout-restore");
            restore.Should().NotBeNull();
            restore!["runOutputColor"]?.Value<bool>().Should().BeFalse();
            restore["runOutputWrap"]?.Value<bool>().Should().BeTrue();
            restore["runOutputFollow"]?.Value<bool>().Should().BeFalse();
        }
        finally { try { Directory.Delete(testDir, true); } catch { } }
    }

    private static JObject? ReadMessageOfType(ConcurrentQueue<string> queue, string type)
    {
        foreach (var json in queue.ToArray())
        {
            var p = JObject.Parse(json);
            if (string.Equals(p["type"]?.ToString(), type, StringComparison.Ordinal)) return p;
        }
        return null;
    }

    private static void InvokePrivate(object instance, string method, params object?[] args)
    {
        var m = instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        m.Should().NotBeNull($"{method} should exist");
        m!.Invoke(instance, args);
    }

    private static T GetField<T>(object instance, string field) where T : class
    {
        var f = instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        f.Should().NotBeNull();
        return (f!.GetValue(instance) as T)!;
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasFormRunOutputPrefsTests"`
Expected: FAIL — `layout-restore` message lacks the run-output fields (assertion fails).

- [ ] **Step 3: Implement save**

In `UI/FlowCanvasForm.cs` `SavePanelSizes`, add the three reads near the other `msg[...]` reads:

```csharp
            var runOutputColor = msg["runOutputColor"]?.Value<bool>();
            var runOutputWrap = msg["runOutputWrap"]?.Value<bool>();
            var runOutputFollow = msg["runOutputFollow"]?.Value<bool>();
```

Add them to the all-null early-return guard:

```csharp
                && compact == null && defaultLayoutMode == null
                && runOutputColor == null && runOutputWrap == null && runOutputFollow == null)
                return;
```

Add the writes inside the `_configService.Update(c => { ... })` block:

```csharp
                if (runOutputColor.HasValue) c.WindowState.FlowCanvasRunOutputColor = runOutputColor.Value;
                if (runOutputWrap.HasValue) c.WindowState.FlowCanvasRunOutputWrap = runOutputWrap.Value;
                if (runOutputFollow.HasValue) c.WindowState.FlowCanvasRunOutputFollow = runOutputFollow.Value;
```

- [ ] **Step 4: Implement restore**

In `SendPersistedLayout`, add the three fields to the `layout-restore` anonymous object (after `compactCommentsEnabled = ...`):

```csharp
                runOutputColor = ws.FlowCanvasRunOutputColor,
                runOutputWrap = ws.FlowCanvasRunOutputWrap,
                runOutputFollow = ws.FlowCanvasRunOutputFollow,
```

(These are `bool?`; React guards each field by type, so nulls are harmless.)

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasFormRunOutputPrefsTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add UI/FlowCanvasForm.cs SSH_Helper.Tests/UI/FlowCanvasFormRunOutputPrefsTests.cs
git commit -m "feat(flow-canvas): persist Run Output view prefs to WindowState

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 7.3: Apply restored prefs on the React side

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts`

- [ ] **Step 1: Locate the layout-restore handler**

Search `FlowCanvas/src/stores/messageBridge.ts` for `incoming.layoutRestore` (e.g. `rg -n "layoutRestore" FlowCanvas/src/stores/messageBridge.ts`, or your editor's find). It registers a `messageBus.on(CANVAS_HOST_MESSAGES.incoming.layoutRestore, (msg) => { ... })` block that currently calls the `restoreX` setters (e.g. `restoreHeatmapEnabled`, `restorePanelSizes`, `restoreBranchBands`). You'll add to that block.

- [ ] **Step 2: Add the run-output restore call**

Inside the `incoming.layoutRestore` handler, after the existing `restore*` calls, add:

```ts
      store.getState().restoreRunOutputPrefs({
        runOutputColor: typeof msg.runOutputColor === 'boolean' ? msg.runOutputColor : undefined,
        runOutputWrap: typeof msg.runOutputWrap === 'boolean' ? msg.runOutputWrap : undefined,
        runOutputFollow: typeof msg.runOutputFollow === 'boolean' ? msg.runOutputFollow : undefined,
      });
```

- [ ] **Step 3: Type-check + run the React suite**

Run: `cd FlowCanvas && npx tsc --noEmit && npm test`
Expected: PASS.

- [ ] **Step 4: Manual round-trip check**

Run: `dotnet run --project SSH_Helper.csproj` → open canvas → toggle Color off + Wrap on → close canvas → reopen → Color is off, Wrap is on.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): restore Run Output view prefs on canvas open

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Final verification (after whichever phases you ship)

- [ ] `cd FlowCanvas && npm test` — full React suite green.
- [ ] `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~FlowCanvasFormRunOutput"` — C# run-output tests green.
- [ ] `dotnet build SSH_Helper.sln` — full build (incl. React bundle) succeeds.
- [ ] Manual parity check from Task 4.4 passes.
