# Flow Canvas Loop Iteration Stepper Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Post-run, every loop band (Foreach/While/Repeat) gets a top-right control cluster that steps through recorded iterations, re-scoping the neon path, branch badges, durations, inner-loop counts, and Block Output to one iteration at a time.

**Architecture:** A loop-iteration stack rides on every step event: loop commands push/update immutable `IterationFrame`s on an `AsyncLocal` in `ScriptContext` (the same isolation pattern `LoopDepth` already uses, so `parallel` arms are isolated for free); `ScriptExecutor` snapshots the stack onto `StepExecutionEventArgs`; Form1 maps loop step-paths to canvas node ids and attaches `iterationStack` to the existing `execution-update`/`step-output` messages. React accumulates an `iterationLog` (per-loop records with per-node entries, written to **every** frame on the stack — innermost exact, ancestors aggregated), and selectors derive scoped views for edges, badges, and Block Output. A new `IterationCluster` renders per loop band inside the existing `BranchBandsLayer` ViewportPortal.

**Tech Stack:** .NET 8 WinForms (xUnit + FluentAssertions tests), React 18 + Zustand + @xyflow/react (vitest + Playwright tests), WebView2 JSON message bridge.

**Spec:** `docs/superpowers/specs/2026-06-09-flow-canvas-loop-iteration-stepper-design.md`

---

## File structure

**C# (new):**
- `Services/Scripting/Models/IterationFrame.cs` — immutable frame record.

**C# (modified):**
- `Services/Scripting/ScriptContext.cs` — AsyncLocal iteration stack + push/set/pop helpers.
- `Services/Scripting/ScriptExecutor.cs` — `StepExecutionEventArgs.IterationStack` property; attach stack to all 4 event raises; normalize `step.StepPath`.
- `Services/Scripting/Commands/ForeachCommand.cs` — push/set/pop frames with item labels.
- `Services/Scripting/Commands/WhileCommand.cs`, `RepeatCommand.cs` — push/set/pop frames (no label).
- `Services/FlowCanvasBridge.cs` — `BuildIterationStackPayload` helper (testable seam).
- `Form1.cs` — attach `iterationStack` to the three outbound canvas messages.
- `Models/AppConfiguration.cs` — `WindowState.FlowCanvasIterationHistoryCap`.
- `UI/FlowCanvasForm.cs` — persist/restore the cap via pref-save / pref-restore.

**React (new):**
- `FlowCanvas/src/stores/slices/iterationSlice.ts` — iterationLog/selections/cap + recording logic.
- `FlowCanvas/src/stores/selectors/iterationScope.ts` — scope resolution + visible-iterations selectors; owns `LOOP_TYPES`.
- `FlowCanvas/src/nodes/IterationCluster.tsx` — the band control cluster (arrows, label, ALL, ⚠, scrubber).

**React (modified):**
- `FlowCanvas/src/communication-message-types.ts` — `IterationFrameMsg`, `iterationStack` field.
- `FlowCanvas/src/stores/useFlowStore.ts` — wire the new slice.
- `FlowCanvas/src/stores/messageBridge.ts` — record iteration events; clear on run start; restore cap pref.
- `FlowCanvas/src/stores/slices/executionSlice.ts` — `clearPath` also resets selections.
- `FlowCanvas/src/stores/selectors/edgePath.ts` — iteration-scoped path status; import `LOOP_TYPES` from iterationScope.
- `FlowCanvas/src/nodes/BranchBandsLayer.tsx` — render `IterationCluster` per loop band.
- `FlowCanvas/src/nodes/BaseBlock.tsx` — scoped exec state / branch badge / duration / inner ×N.
- `FlowCanvas/src/panels/OutputPreview.tsx` — sync `historyIndex` to the scoped output entry.
- `FlowCanvas/src/panels/SettingsPopover.tsx` — loop-history cap control.

**Tests (new):**
- `SSH_Helper.Tests/Scripting/ScriptContextIterationStackTests.cs`
- `SSH_Helper.Tests/Scripting/IterationStackEventTests.cs`
- `SSH_Helper.Tests/Scripting/IterationStackPayloadTests.cs`
- `FlowCanvas/src/stores/slices/__tests__/iterationSlice.test.ts`
- `FlowCanvas/src/stores/selectors/__tests__/iterationScope.test.ts`
- `FlowCanvas/e2e/flow-canvas-iteration-stepper.spec.ts`

**Key invariants (carry through every task):**
- Selections and parent links use a unique per-record `seq` (monotonic counter), never array positions (eviction shifts positions) and never the iteration index `i` (inner loops restart `i` per outer iteration).
- Nothing is ever written to `node.data` — all iteration state is transient store state, so YAML export is untouched.
- Events are recorded into **every** frame on their stack: innermost record gets exact values, ancestor records aggregate (error state sticky, otherwise last write wins).
- A loop's own `StepCompleted` fires **after** its frame is popped — a loop's events carry only its *ancestors'* frames. That is correct and tests assert it.

**Commands used throughout:**
- C# single test class: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~<ClassName>"`
- C# build only (no Node): `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
- vitest single file (from `FlowCanvas/`): `npm run test -- src/stores/slices/__tests__/iterationSlice.test.ts`
- TypeScript check (from `FlowCanvas/`): `npx tsc --noEmit`
- e2e single spec (from `FlowCanvas/`): `npx playwright test e2e/flow-canvas-iteration-stepper.spec.ts`
- If `dotnet build` fails with a locked-DLL error, the app is running — close SSH_Helper.exe first (known gotcha).

---

### Task 1: `IterationFrame` record + `ScriptContext` iteration-stack helpers

**Files:**
- Create: `Services/Scripting/Models/IterationFrame.cs`
- Modify: `Services/Scripting/ScriptContext.cs` (fields region ~line 100, near `_loopDepth`; helpers after the `CallDepth` property)
- Test: `SSH_Helper.Tests/Scripting/ScriptContextIterationStackTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SSH_Helper.Tests/Scripting/ScriptContextIterationStackTests.cs`:

```csharp
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptContextIterationStackTests
{
    [Fact]
    public void IterationStack_IsEmptyByDefault()
    {
        var context = new ScriptContext();
        context.IterationStack.Should().BeEmpty();
    }

    [Fact]
    public void PushSetPop_RoundTrips()
    {
        var context = new ScriptContext();

        context.PushIterationFrame("steps/2", -1);
        context.SetCurrentIterationFrame(0, "web-01");

        context.IterationStack.Should().HaveCount(1);
        context.IterationStack[0].Should().Be(new IterationFrame("steps/2", 0, "web-01"));

        context.PushIterationFrame("steps/2/do/1", -1);
        context.SetCurrentIterationFrame(4);
        context.IterationStack.Should().HaveCount(2);
        context.IterationStack[1].Should().Be(new IterationFrame("steps/2/do/1", 4, null));

        context.PopIterationFrame();
        context.IterationStack.Should().HaveCount(1);
        context.IterationStack[0].LoopStepPath.Should().Be("steps/2");

        context.PopIterationFrame();
        context.IterationStack.Should().BeEmpty();

        // Popping an empty stack must be a no-op, not a throw.
        context.PopIterationFrame();
        context.IterationStack.Should().BeEmpty();
    }

    [Fact]
    public void Snapshots_AreImmutable_AcrossSetCurrentIterationFrame()
    {
        var context = new ScriptContext();
        context.PushIterationFrame("steps/0", -1);
        context.SetCurrentIterationFrame(0, "a");

        var snapshot = context.IterationStack;

        context.SetCurrentIterationFrame(1, "b");

        snapshot[0].Index.Should().Be(0);
        snapshot[0].Label.Should().Be("a");
        context.IterationStack[0].Index.Should().Be(1);
    }

    [Fact]
    public async Task Stack_IsIsolated_AcrossParallelTasks()
    {
        // Mirrors ParallelCommand: one shared context, arms on Task.Run. AsyncLocal must
        // keep each arm's pushes invisible to the other.
        var context = new ScriptContext();

        var t1 = Task.Run(async () =>
        {
            context.PushIterationFrame("steps/0/parallel/0", -1);
            context.SetCurrentIterationFrame(0);
            await Task.Delay(50);
            return context.IterationStack.Select(f => f.LoopStepPath).ToArray();
        });
        var t2 = Task.Run(async () =>
        {
            context.PushIterationFrame("steps/0/parallel/1", -1);
            context.SetCurrentIterationFrame(0);
            await Task.Delay(50);
            return context.IterationStack.Select(f => f.LoopStepPath).ToArray();
        });

        var results = await Task.WhenAll(t1, t2);

        results[0].Should().Equal("steps/0/parallel/0");
        results[1].Should().Equal("steps/0/parallel/1");
        context.IterationStack.Should().BeEmpty(); // parent never saw either push
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptContextIterationStackTests" -p:SkipFlowCanvasBuild=true`
Expected: BUILD FAILURE — `IterationFrame` and the new members do not exist yet.

- [ ] **Step 3: Create the `IterationFrame` record**

Create `Services/Scripting/Models/IterationFrame.cs`:

```csharp
namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// One level of the live loop-iteration stack: which loop (by canonical step path,
    /// e.g. "steps/2"), which iteration (0-based; -1 = pushed but no iteration started yet),
    /// and an optional display label (the foreach item value, truncated). Immutable —
    /// event consumers keep references to frames without copying.
    /// </summary>
    public sealed record IterationFrame(string LoopStepPath, int Index, string? Label = null);
}
```

- [ ] **Step 4: Add the stack to `ScriptContext`**

In `Services/Scripting/ScriptContext.cs`, next to the existing `_loopDepth` field (~line 102):

```csharp
        private readonly AsyncLocal<IterationFrame[]?> _iterationStack = new();
```

`ScriptContext.cs` already uses types from `SSH_Helper.Services.Scripting.Models`; if `IterationFrame` does not resolve, add `using SSH_Helper.Services.Scripting.Models;` to the file's usings.

After the `CallDepth` property, add:

```csharp
        /// <summary>
        /// Live loop-iteration stack, outermost frame first. Backed by an AsyncLocal holding
        /// an immutable array — the same isolation pattern as <see cref="LoopDepth"/>, so
        /// parallel arms see independent stacks and event handlers can keep the returned
        /// list without copying (mutations always replace the array).
        /// </summary>
        public IReadOnlyList<IterationFrame> IterationStack =>
            _iterationStack.Value ?? System.Array.Empty<IterationFrame>();

        /// <summary>Enters a loop: pushes a frame (Index = -1 until the first iteration starts).</summary>
        public void PushIterationFrame(string loopStepPath, int index, string? label = null)
        {
            var current = _iterationStack.Value ?? System.Array.Empty<IterationFrame>();
            var next = new IterationFrame[current.Length + 1];
            System.Array.Copy(current, next, current.Length);
            next[current.Length] = new IterationFrame(loopStepPath, index, label);
            _iterationStack.Value = next;
        }

        /// <summary>Starts iteration <paramref name="index"/> of the innermost loop.</summary>
        public void SetCurrentIterationFrame(int index, string? label = null)
        {
            var current = _iterationStack.Value;
            if (current == null || current.Length == 0) return;
            var next = (IterationFrame[])current.Clone();
            next[^1] = next[^1] with { Index = index, Label = label };
            _iterationStack.Value = next;
        }

        /// <summary>Exits the innermost loop. No-op on an empty stack.</summary>
        public void PopIterationFrame()
        {
            var current = _iterationStack.Value;
            if (current == null || current.Length == 0) return;
            if (current.Length == 1) { _iterationStack.Value = null; return; }
            var next = new IterationFrame[current.Length - 1];
            System.Array.Copy(current, next, next.Length);
            _iterationStack.Value = next;
        }
```

`AsyncLocal` lives in `System.Threading` — already imported wherever `_loopDepth` compiles.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptContextIterationStackTests" -p:SkipFlowCanvasBuild=true`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add Services/Scripting/Models/IterationFrame.cs Services/Scripting/ScriptContext.cs SSH_Helper.Tests/Scripting/ScriptContextIterationStackTests.cs
git commit -m "feat(scripting): AsyncLocal loop-iteration stack on ScriptContext"
```

---

### Task 2: `StepExecutionEventArgs.IterationStack` + executor attachment

**Files:**
- Modify: `Services/Scripting/ScriptExecutor.cs` (event args class ~line 13–47; `ExecuteStepsAsync` per-step loop ~line 290–410)
- Test: `SSH_Helper.Tests/Scripting/IterationStackEventTests.cs` (created here, grown in Tasks 3–5)

- [ ] **Step 1: Write the failing test**

Create `SSH_Helper.Tests/Scripting/IterationStackEventTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class IterationStackEventTests
{
    private static async Task<List<StepExecutionEventArgs>> RunAndCaptureAll(
        Script script, ScriptContext? context = null)
    {
        var executor = new ScriptExecutor();
        var events = new List<StepExecutionEventArgs>();
        executor.StepCompleted += (_, e) => { lock (events) events.Add(e); };
        await executor.ExecuteAsync(script, context ?? new ScriptContext());
        return events;
    }

    [Fact]
    public async Task TopLevelStep_HasEmptyIterationStack()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep> { new() { Set = "x = 1" } }
        };

        var events = await RunAndCaptureAll(script);

        events.Should().HaveCount(1);
        (events[0].IterationStack ?? new List<IterationFrame>()).Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackEventTests" -p:SkipFlowCanvasBuild=true`
Expected: BUILD FAILURE — `IterationStack` does not exist on `StepExecutionEventArgs`.

- [ ] **Step 3: Add the property and attach it in the executor**

In `Services/Scripting/ScriptExecutor.cs`, append to the `StepExecutionEventArgs` class (after `BranchTaken`):

```csharp
        /// <summary>
        /// Live loop-iteration stack at the moment the event fired (outermost first).
        /// Null or empty outside loops. The list is an immutable point-in-time snapshot —
        /// later iterations never mutate it. NOTE: a loop's own StepCompleted fires after
        /// its frame is popped, so a loop's events carry only its ancestors' frames.
        /// </summary>
        public IReadOnlyList<IterationFrame>? IterationStack { get; init; }
```

If `IterationFrame` does not resolve, add `using SSH_Helper.Services.Scripting.Models;` to the file's usings (it almost certainly already imports this namespace for `ScriptStep`).

In `ExecuteStepsAsync(...)`, immediately after `var stepPath = step.StepPath ?? $"steps/{stepIndex}";` add:

```csharp
                    // Loop commands read step.StepPath to tag iteration frames, so make the
                    // effective path visible on the step even when the parser didn't assign one
                    // (hand-built scripts). Idempotent: same value on every iteration.
                    step.StepPath ??= stepPath;
```

Then add `IterationStack = context.IterationStack,` to **all four** `StepExecutionEventArgs` constructions in `ExecuteStepsAsync`:

1. The disabled-node skip `StepCompleted` (after `Skipped = true`).
2. The `when:`-guard skip `StepCompleted` (after `Skipped = true`).
3. The `StepStarting` raise (after `StepName = null`).
4. The final `StepCompleted` raise (after `BranchTaken = result.BranchTaken`).

Example for the final raise — it becomes:

```csharp
                    StepCompleted?.Invoke(this, new StepExecutionEventArgs
                    {
                        StepIndex = stepIndex,
                        StepPath = stepPath,
                        StepType = stepType,
                        LineNumber = step.LineNumber,
                        StepName = null,
                        Success = result.Success,
                        Output = stepOutput,
                        DurationMs = sw.ElapsedMilliseconds,
                        IterationCount = result.IterationCount,
                        BranchTaken = result.BranchTaken,
                        IterationStack = context.IterationStack
                    });
```

(`context.IterationStack` already returns the immutable snapshot array — no copy needed.)

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackEventTests" -p:SkipFlowCanvasBuild=true`
Expected: PASS (1 test)

- [ ] **Step 5: Run the existing executor/loop suites to catch regressions**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SSH_Helper.Tests.Scripting" -p:SkipFlowCanvasBuild=true`
Expected: PASS (the `step.StepPath ??=` normalization must not break any existing Scripting test — these tests key events by `StepPath`, and the fallback writes the same value the executor already computed).

- [ ] **Step 6: Commit**

```bash
git add Services/Scripting/ScriptExecutor.cs SSH_Helper.Tests/Scripting/IterationStackEventTests.cs
git commit -m "feat(scripting): step events carry the live iteration stack"
```

---

### Task 3: `ForeachCommand` pushes frames with item labels

**Files:**
- Modify: `Services/Scripting/Commands/ForeachCommand.cs`
- Test: `SSH_Helper.Tests/Scripting/IterationStackEventTests.cs` (extend)

- [ ] **Step 1: Write the failing tests**

Append to `IterationStackEventTests`:

```csharp
    [Fact]
    public async Task Foreach_TagsNestedEvents_PerIteration_WithItemLabels()
    {
        var context = new ScriptContext();
        context.SetVariable("items", "[\"alpha\",\"beta\",\"gamma\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = x", StepPath = "steps/0/do/0" }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var bodyEvents = events.Where(e => e.StepPath == "steps/0/do/0").ToList();
        bodyEvents.Should().HaveCount(3);
        for (int i = 0; i < 3; i++)
        {
            bodyEvents[i].IterationStack.Should().NotBeNull();
            bodyEvents[i].IterationStack!.Should().HaveCount(1);
            bodyEvents[i].IterationStack![0].LoopStepPath.Should().Be("steps/0");
            bodyEvents[i].IterationStack![0].Index.Should().Be(i);
        }
        bodyEvents.Select(e => e.IterationStack![0].Label)
            .Should().Equal("alpha", "beta", "gamma");

        // The loop's own completion fires AFTER its frame pops → ancestors only (none here).
        var loopEvent = events.Single(e => e.StepPath == "steps/0");
        (loopEvent.IterationStack ?? new List<IterationFrame>()).Should().BeEmpty();
    }

    [Fact]
    public async Task Foreach_DictForm_UsesKeyAsLabel()
    {
        var context = new ScriptContext();
        context.SetVariable("map", "{\"k1\":\"v1\",\"k2\":\"v2\"}");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "key, value in map",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = value", StepPath = "steps/0/do/0" }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        events.Where(e => e.StepPath == "steps/0/do/0")
            .Select(e => e.IterationStack![0].Label)
            .Should().Equal("k1", "k2");
    }

    [Fact]
    public async Task Foreach_LongItemValue_IsTruncatedTo48Chars()
    {
        var longItem = new string('z', 60);
        var context = new ScriptContext();
        context.SetVariable("items", $"[\"{longItem}\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = x", StepPath = "steps/0/do/0" }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var label = events.Single(e => e.StepPath == "steps/0/do/0").IterationStack![0].Label;
        label.Should().HaveLength(48);
        label.Should().Be(new string('z', 47) + "…");
    }

    [Fact]
    public async Task Foreach_StackIsPopped_AfterBreak()
    {
        var context = new ScriptContext();
        context.SetVariable("items", "[\"a\",\"b\",\"c\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Break = "true", StepPath = "steps/0/do/0" }
                    }
                },
                new() { Set = "after = 1", StepPath = "steps/1" }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        // Only iteration 0 ran; the trailing top-level step must carry an EMPTY stack.
        events.Where(e => e.StepPath == "steps/0/do/0").Should().HaveCount(1);
        var after = events.Single(e => e.StepPath == "steps/1");
        (after.IterationStack ?? new List<IterationFrame>()).Should().BeEmpty();
    }
```

Note: if `ScriptStep` does not have a `Break` property with that exact name, check how `BreakCommand` is driven (grep `StepType.Break` in `Services/Scripting/`) and use the real property; the existing control-flow tests (`ScriptExecutorControlFlowTests.cs`) show the working shape — mirror it.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackEventTests" -p:SkipFlowCanvasBuild=true`
Expected: FAIL — body events have empty `IterationStack` (foreach pushes nothing yet).

- [ ] **Step 3: Implement frames in `ForeachCommand`**

In `Services/Scripting/Commands/ForeachCommand.cs`:

(a) Add the truncation helper inside the class:

```csharp
        private const int MaxLabelLength = 48;

        private static string? TruncateLabel(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value!.Length <= MaxLabelLength
                ? value
                : value.Substring(0, MaxLabelLength - 1) + "…";
        }
```

(b) Extend `IterateAsync`'s signature with a label source (last parameter):

```csharp
        private async Task<CommandResult> IterateAsync(
            ScriptStep step,
            ScriptContext context,
            CancellationToken cancellationToken,
            int count,
            string metadataPrefix,
            IReadOnlyList<string> iterationNames,
            Action<int> setIteration,
            Func<int, string?>? labelFor = null)
```

(c) Update both call sites in `ExecuteAsync`:

Dictionary form — add after `setIteration: ...`:
```csharp
                    setIteration: i =>
                    {
                        context.SetVariable(keyName, entries[i].Key);
                        context.SetVariable(valueName, entries[i].Value);
                    },
                    labelFor: i => TruncateLabel(entries[i].Key));
```

Single form:
```csharp
            return await IterateAsync(step, context, cancellationToken,
                count: items.Count,
                metadataPrefix: itemVarName,
                iterationNames: new[] { itemVarName },
                setIteration: i => context.SetVariable(itemVarName, items[i]),
                labelFor: i => TruncateLabel(items[i]));
```

(d) In `IterateAsync`, push the frame just before the existing `try` (after the `saved` dictionary is built), pop it first in the existing `finally`, and set it per iteration. The body becomes:

```csharp
            var evaluator = new ExpressionEvaluator(context);
            int executed = 0;

            // Iteration frame: tags every nested step event with (loop path, index, item label)
            // so the canvas can attribute events to iterations. Index -1 until the first
            // iteration starts; no events fire in that window.
            context.PushIterationFrame(step.StepPath ?? string.Empty, -1);

            try
            {
                for (int index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    setIteration(index);
                    context.SetVariable($"{metadataPrefix}_index", index);
                    context.SetVariable($"{metadataPrefix}_number", index + 1);
                    context.SetVariable($"{metadataPrefix}_first", index == 0);
                    context.SetVariable($"{metadataPrefix}_last", index == count - 1);
                    context.SetVariable($"{metadataPrefix}_count", count);
                    context.SetCurrentIterationFrame(index, labelFor?.Invoke(index));

                    if (!string.IsNullOrEmpty(step.When))
                    {
                        var whenCondition = context.SubstituteVariables(step.When);
                        if (!evaluator.Evaluate(whenCondition))
                            continue; // Skip this item (body not executed)
                    }

                    var result = await _executor.ExecuteStepsAsync(step.Do, context, cancellationToken, context.LoopDepth + 1);
                    executed++;

                    if (result.ShouldExit || result.ShouldReturn)
                    {
                        result.IterationCount = executed;
                        return result;
                    }

                    if (result.ShouldBreak)
                        break;

                    if (result.ShouldContinue)
                        continue;

                    if (!result.Success)
                    {
                        result.IterationCount = executed;
                        return result;
                    }
                }

                return new CommandResult { Success = true, IterationCount = executed };
            }
            finally
            {
                context.PopIterationFrame();
                foreach (var name in scopedNames)
                {
                    var (existed, value) = saved[name];
                    if (existed)
                        context.SetVariable(name, value);
                    else
                        context.RemoveVariable(name);
                }
            }
```

(The only changes from the current body: the `PushIterationFrame` line before `try`, the `SetCurrentIterationFrame` line after the metadata variables, and `PopIterationFrame()` as the first statement of `finally`.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackEventTests" -p:SkipFlowCanvasBuild=true`
Expected: PASS (5 tests)

- [ ] **Step 5: Run the existing foreach suite**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ForeachCommandTests" -p:SkipFlowCanvasBuild=true`
Expected: PASS — block-scoped variable restore and control flow are unchanged.

- [ ] **Step 6: Commit**

```bash
git add Services/Scripting/Commands/ForeachCommand.cs SSH_Helper.Tests/Scripting/IterationStackEventTests.cs
git commit -m "feat(scripting): foreach tags iteration frames with item labels"
```

---

### Task 4: `WhileCommand` + `RepeatCommand` frames

**Files:**
- Modify: `Services/Scripting/Commands/WhileCommand.cs`, `Services/Scripting/Commands/RepeatCommand.cs`
- Test: `SSH_Helper.Tests/Scripting/IterationStackEventTests.cs` (extend)

- [ ] **Step 1: Write the failing tests**

Append to `IterationStackEventTests`:

```csharp
    [Fact]
    public async Task While_TagsNestedEvents_PerIteration_NoLabel()
    {
        var context = new ScriptContext();
        context.SetVariable("n", 0);
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    While = "n < 3",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "n = n + 1", StepPath = "steps/0/do/0" }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var bodyEvents = events.Where(e => e.StepPath == "steps/0/do/0").ToList();
        bodyEvents.Should().HaveCount(3);
        bodyEvents.Select(e => e.IterationStack![0].Index).Should().Equal(0, 1, 2);
        bodyEvents.Should().OnlyContain(e => e.IterationStack![0].Label == null);
        bodyEvents.Should().OnlyContain(e => e.IterationStack![0].LoopStepPath == "steps/0");
    }

    [Fact]
    public async Task NestedLoops_StackTwoFramesDeep()
    {
        var context = new ScriptContext();
        context.SetVariable("outer", "[\"o1\",\"o2\"]");
        context.SetVariable("inner", "[\"i1\",\"i2\",\"i3\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "o in outer",
                    StepPath = "steps/0",
                    Do = new List<ScriptStep>
                    {
                        new()
                        {
                            Foreach = "v in inner",
                            StepPath = "steps/0/do/0",
                            Do = new List<ScriptStep>
                            {
                                new() { Set = "last = v", StepPath = "steps/0/do/0/do/0" }
                            }
                        }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var leaf = events.Where(e => e.StepPath == "steps/0/do/0/do/0").ToList();
        leaf.Should().HaveCount(6); // 2 outer × 3 inner

        foreach (var e in leaf)
        {
            e.IterationStack.Should().HaveCount(2);
            e.IterationStack![0].LoopStepPath.Should().Be("steps/0");
            e.IterationStack![1].LoopStepPath.Should().Be("steps/0/do/0");
        }
        // Inner index restarts per outer iteration.
        leaf.Select(e => (e.IterationStack![0].Index, e.IterationStack![1].Index))
            .Should().Equal((0, 0), (0, 1), (0, 2), (1, 0), (1, 1), (1, 2));

        // The inner loop's OWN completions carry just the outer frame (its own frame popped).
        var innerLoopEvents = events.Where(e => e.StepPath == "steps/0/do/0").ToList();
        innerLoopEvents.Should().HaveCount(2);
        innerLoopEvents.Select(e => e.IterationStack!.Single().Index).Should().Equal(0, 1);
    }
```

(`Repeat` shares the identical pattern; the While test covers the `_iteration`-style loops and the nested test covers stack depth. If you want belt-and-braces, clone the While test for `Until = "n >= 3"` — optional.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackEventTests" -p:SkipFlowCanvasBuild=true`
Expected: FAIL — While body events carry no frames.

- [ ] **Step 3: Implement frames in `WhileCommand`**

In `Services/Scripting/Commands/WhileCommand.cs`, wrap the loop in push/pop and set the frame per iteration. After `int iteration = 0; int executed = 0;` the method becomes:

```csharp
            int iteration = 0;
            int executed = 0;

            context.PushIterationFrame(step.StepPath ?? string.Empty, -1);
            try
            {
                while (iteration < maxIterations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Evaluate expression directly; ExpressionEvaluator resolves variables.
                    var condition = step.While;
                    var result = evaluator.Evaluate(condition);

                    if (!result)
                    {
                        context.EmitOutput($"While: condition false after {iteration} iteration(s)", ScriptOutputType.Debug);
                        break;
                    }

                    if (iteration == 0)
                    {
                        context.EmitOutput($"While: entering loop", ScriptOutputType.Debug);
                    }

                    // Set iteration variable
                    context.SetVariable("_iteration", iteration);
                    context.SetCurrentIterationFrame(iteration);

                    // Execute the 'do' block
                    var execResult = await _executor.ExecuteStepsAsync(step.Do, context, cancellationToken, context.LoopDepth + 1);
                    executed++;

                    // Handle control flow
                    if (execResult.ShouldExit)
                    {
                        execResult.IterationCount = executed;
                        return execResult;
                    }

                    if (execResult.ShouldReturn)
                    {
                        execResult.IterationCount = executed;
                        return execResult;
                    }

                    if (execResult.ShouldBreak)
                    {
                        context.EmitOutput($"While: break after {iteration + 1} iteration(s)", ScriptOutputType.Debug);
                        break;
                    }

                    if (execResult.ShouldContinue)
                    {
                        iteration++;
                        continue;
                    }

                    if (!execResult.Success)
                    {
                        execResult.IterationCount = executed;
                        return execResult;
                    }

                    iteration++;
                }
            }
            finally
            {
                context.PopIterationFrame();
            }

            if (iteration >= maxIterations)
            {
                context.EmitOutput($"While: reached maximum iterations ({maxIterations}), stopping", ScriptOutputType.Warning);
            }

            return new CommandResult { Success = true, IterationCount = executed };
```

(Only three changes to the existing body: `PushIterationFrame` before the loop, `SetCurrentIterationFrame(iteration)` after `SetVariable("_iteration", ...)`, and the `try/finally` wrapper with `PopIterationFrame()`.)

- [ ] **Step 4: Implement frames in `RepeatCommand`** (same three changes)

In `Services/Scripting/Commands/RepeatCommand.cs`, after `int iteration = 0; int executed = 0;`:

```csharp
            int iteration = 0;
            int executed = 0;

            context.PushIterationFrame(step.StepPath ?? string.Empty, -1);
            try
            {
                while (iteration < maxIterations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    context.SetVariable("_iteration", iteration);
                    context.SetCurrentIterationFrame(iteration);

                    var execResult = await _executor.ExecuteStepsAsync(step.Do, context, cancellationToken, context.LoopDepth + 1);
                    executed++;

                    if (execResult.ShouldExit || execResult.ShouldReturn)
                    {
                        execResult.IterationCount = executed;
                        return execResult;
                    }

                    if (execResult.ShouldBreak)
                    {
                        context.EmitOutput($"Repeat: break after {iteration + 1} iteration(s)", ScriptOutputType.Debug);
                        break;
                    }

                    // `continue` falls through to the bottom condition check; a genuine failure stops the loop.
                    if (!execResult.ShouldContinue && !execResult.Success)
                    {
                        execResult.IterationCount = executed;
                        return execResult;
                    }

                    iteration++;

                    // Bottom-tested: exit once the until condition becomes true.
                    if (evaluator.Evaluate(step.Until))
                    {
                        context.EmitOutput($"Repeat: until condition true after {iteration} iteration(s)", ScriptOutputType.Debug);
                        break;
                    }
                }
            }
            finally
            {
                context.PopIterationFrame();
            }

            if (iteration >= maxIterations)
            {
                context.EmitOutput($"Repeat: reached maximum iterations ({maxIterations}), stopping", ScriptOutputType.Warning);
            }

            return new CommandResult { Success = true, IterationCount = executed };
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackEventTests" -p:SkipFlowCanvasBuild=true`
Expected: PASS (7 tests)

- [ ] **Step 6: Run the full Scripting suite**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SSH_Helper.Tests.Scripting" -p:SkipFlowCanvasBuild=true`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add Services/Scripting/Commands/WhileCommand.cs Services/Scripting/Commands/RepeatCommand.cs SSH_Helper.Tests/Scripting/IterationStackEventTests.cs
git commit -m "feat(scripting): while/repeat tag iteration frames"
```

---

### Task 5: Parallel-arm isolation regression test (test-only)

The spec's one flagged risk: `ParallelCommand` shares ONE `ScriptContext` across arms (`ParallelCommand.cs` — arms run via `Task.Run` on the shared context). The stack survives this because it is `AsyncLocal` with immutable values — each `Task.Run` arm gets an isolated logical copy. This task pins that behavior with a test so a future refactor (e.g. moving off AsyncLocal) cannot silently corrupt attribution.

**Files:**
- Test: `SSH_Helper.Tests/Scripting/IterationStackEventTests.cs` (extend)

- [ ] **Step 1: Write the test**

Append to `IterationStackEventTests`:

```csharp
    [Fact]
    public async Task ParallelArms_WithLoops_DoNotCrossContaminateStacks()
    {
        // Two parallel arms, each a foreach over 3 items, sharing one ScriptContext —
        // exactly how ParallelCommand executes arms (shared context + Task.Run).
        // Every body event must carry exactly its OWN arm's single frame.
        var context = new ScriptContext();
        context.SetVariable("itemsA", "[\"a0\",\"a1\",\"a2\"]");
        context.SetVariable("itemsB", "[\"b0\",\"b1\",\"b2\"]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    StepPath = "steps/0",
                    Parallel = new ParallelOptions
                    {
                        Steps = new List<ScriptStep>
                        {
                            new()
                            {
                                Foreach = "a in itemsA",
                                StepPath = "steps/0/parallel/0",
                                Do = new List<ScriptStep>
                                {
                                    new() { Set = "lastA = a", StepPath = "steps/0/parallel/0/do/0" }
                                }
                            },
                            new()
                            {
                                Foreach = "b in itemsB",
                                StepPath = "steps/0/parallel/1",
                                Do = new List<ScriptStep>
                                {
                                    new() { Set = "lastB = b", StepPath = "steps/0/parallel/1/do/0" }
                                }
                            }
                        }
                    }
                }
            }
        };

        var events = await RunAndCaptureAll(script, context);

        var armA = events.Where(e => e.StepPath == "steps/0/parallel/0/do/0").ToList();
        var armB = events.Where(e => e.StepPath == "steps/0/parallel/1/do/0").ToList();
        armA.Should().HaveCount(3);
        armB.Should().HaveCount(3);

        armA.Should().OnlyContain(e =>
            e.IterationStack!.Count == 1 &&
            e.IterationStack![0].LoopStepPath == "steps/0/parallel/0");
        armB.Should().OnlyContain(e =>
            e.IterationStack!.Count == 1 &&
            e.IterationStack![0].LoopStepPath == "steps/0/parallel/1");

        armA.Select(e => e.IterationStack![0].Index).OrderBy(i => i).Should().Equal(0, 1, 2);
        armB.Select(e => e.IterationStack![0].Index).OrderBy(i => i).Should().Equal(0, 1, 2);
    }
```

`ParallelOptions` is defined in `Services/Scripting/Models/ScriptStep.cs` (~line 1530); it exposes `Steps` (and `MaxConcurrent`, which defaults to unbounded when 0). If the `Steps` property has a different setter shape, mirror how existing parallel tests construct it (grep `ParallelOptions` under `SSH_Helper.Tests/`).

- [ ] **Step 2: Run the test — expected to PASS immediately**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ParallelArms_WithLoops" -p:SkipFlowCanvasBuild=true`
Expected: PASS. This is a regression pin, not a red-green cycle — the AsyncLocal design makes it pass by construction. **If it FAILS**, stop and re-read `ScriptContext` — something is sharing mutable stack state across arms, and that must be fixed before any React work proceeds (the whole feature's attribution correctness rests here).

- [ ] **Step 3: Commit**

```bash
git add SSH_Helper.Tests/Scripting/IterationStackEventTests.cs
git commit -m "test(scripting): pin parallel-arm isolation of iteration stacks"
```

---

### Task 6: `FlowCanvasBridge.BuildIterationStackPayload` + Form1 message wiring

**Files:**
- Modify: `Services/FlowCanvasBridge.cs` (class is `internal sealed class FlowCanvasBridge` in `namespace SSH_Helper.Services`; add near `TryGetTopLevelStepIndex`, ~line 5248)
- Modify: `Form1.cs` — `SshService_StepStarting` (~line 13601), `SshService_StepCompleted` (~line 13627)
- Test: `SSH_Helper.Tests/Scripting/IterationStackPayloadTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `SSH_Helper.Tests/Scripting/IterationStackPayloadTests.cs`:

```csharp
using System.Collections.Generic;
using FluentAssertions;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class IterationStackPayloadTests
{
    private static readonly Dictionary<string, string> Map = new(System.StringComparer.Ordinal)
    {
        ["steps/0"] = "node-F",
        ["steps/0/do/1"] = "node-W",
    };

    [Fact]
    public void NullOrEmptyStack_ReturnsNull()
    {
        FlowCanvasBridge.BuildIterationStackPayload(null, Map).Should().BeNull();
        FlowCanvasBridge.BuildIterationStackPayload(new List<IterationFrame>(), Map).Should().BeNull();
    }

    [Fact]
    public void ResolvableFrames_MapToLoopNodeIds_InOrder()
    {
        var stack = new List<IterationFrame>
        {
            new("steps/0", 2, "web-02"),
            new("steps/0/do/1", 4, null),
        };

        var payload = FlowCanvasBridge.BuildIterationStackPayload(stack, Map);

        payload.Should().HaveCount(2);
        payload![0]["loopId"].Should().Be("node-F");
        payload[0]["i"].Should().Be(2);
        payload[0]["label"].Should().Be("web-02");
        payload[1]["loopId"].Should().Be("node-W");
        payload[1]["i"].Should().Be(4);
        payload[1]["label"].Should().BeNull();
    }

    [Fact]
    public void UnresolvableOrNotStartedFrames_AreSkipped_Gracefully()
    {
        var stack = new List<IterationFrame>
        {
            new("steps/0", 1, "a"),
            new("subroutines/x/steps/2", 0, null), // no canvas node — skipped
            new("steps/0/do/1", -1, null),         // pushed, no iteration yet — skipped
            new("", 3, null),                      // no path — skipped
        };

        var payload = FlowCanvasBridge.BuildIterationStackPayload(stack, Map);

        payload.Should().HaveCount(1);
        payload![0]["loopId"].Should().Be("node-F");
    }

    [Fact]
    public void NullMap_OrNothingResolvable_ReturnsNull()
    {
        var stack = new List<IterationFrame> { new("steps/9", 0, null) };
        FlowCanvasBridge.BuildIterationStackPayload(stack, null).Should().BeNull();
        FlowCanvasBridge.BuildIterationStackPayload(stack, Map).Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackPayloadTests" -p:SkipFlowCanvasBuild=true`
Expected: BUILD FAILURE — method does not exist.

- [ ] **Step 3: Implement the helper in `FlowCanvasBridge`**

Add to `Services/FlowCanvasBridge.cs` (next to `TryGetTopLevelStepIndex`), with `using SSH_Helper.Services.Scripting.Models;` added to the file's usings if missing:

```csharp
        /// <summary>
        /// Converts a live iteration stack into the canvas message payload: each frame's loop
        /// step-path is mapped to its canvas node id. Frames that don't resolve (subroutine
        /// loops with no canvas node, empty paths, not-yet-started frames with Index &lt; 0)
        /// are skipped individually — a dropped middle frame simply re-parents its children
        /// to the nearest resolvable ancestor on the React side. Returns null when nothing
        /// resolves, so callers serialize the field as absent.
        /// </summary>
        internal static List<Dictionary<string, object?>>? BuildIterationStackPayload(
            IReadOnlyList<IterationFrame>? stack,
            IReadOnlyDictionary<string, string>? stepPathToNodeId)
        {
            if (stack == null || stack.Count == 0 || stepPathToNodeId == null) return null;

            List<Dictionary<string, object?>>? frames = null;
            foreach (var frame in stack)
            {
                if (string.IsNullOrWhiteSpace(frame.LoopStepPath)) continue;
                if (frame.Index < 0) continue;
                if (!stepPathToNodeId.TryGetValue(frame.LoopStepPath, out var loopNodeId)) continue;

                (frames ??= new List<Dictionary<string, object?>>()).Add(new Dictionary<string, object?>
                {
                    ["loopId"] = loopNodeId,
                    ["i"] = frame.Index,
                    ["label"] = frame.Label,
                });
            }
            return frames;
        }
```

(Newtonsoft serializes `Dictionary<string, object?>` as a JSON object, so the wire shape is `[{ "loopId": "...", "i": 2, "label": "web-02" }]`.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~IterationStackPayloadTests" -p:SkipFlowCanvasBuild=true`
Expected: PASS (4 tests)

- [ ] **Step 5: Wire into Form1's three outbound messages**

In `Form1.cs`, `SshService_StepStarting` (~line 13601) — the send becomes:

```csharp
            _flowCanvasForm.SendMessage(new
            {
                type = "execution-update",
                stepId = nodeId,
                state = e.Skipped ? "skipped" : "running",
                iterationStack = Services.FlowCanvasBridge.BuildIterationStackPayload(e.IterationStack, _stepPathToNodeIdMap)
            });
```

In `SshService_StepCompleted` (~line 13627), compute the payload once and attach to both messages:

```csharp
            var iterationStack = Services.FlowCanvasBridge.BuildIterationStackPayload(e.IterationStack, _stepPathToNodeIdMap);

            _flowCanvasForm.SendMessage(new
            {
                type = "execution-update",
                stepId = nodeId,
                state = e.Skipped ? "skipped" : (e.Success == true ? "success" : "error"),
                duration = e.DurationMs,
                iterationCount = e.IterationCount,
                branchTaken = e.BranchTaken,
                variables,
                iterationStack
            });

            // Send step output if available
            if (!string.IsNullOrEmpty(e.Output))
            {
                _flowCanvasForm.SendMessage(new
                {
                    type = "step-output",
                    stepId = nodeId,
                    output = e.Output,
                    iterationStack
                });
            }
        }
```

(If `Form1.cs` has a `using SSH_Helper.Services;` already, drop the `Services.` qualifier.)

- [ ] **Step 6: Build to verify**

Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: Build succeeded. (Form1 step handlers have no direct unit tests; the e2e task exercises this path end-to-end.)

- [ ] **Step 7: Commit**

```bash
git add Services/FlowCanvasBridge.cs Form1.cs SSH_Helper.Tests/Scripting/IterationStackPayloadTests.cs
git commit -m "feat(flow-canvas): bridge maps iteration stacks onto canvas messages"
```

---

### Task 7: React message types

**Files:**
- Modify: `FlowCanvas/src/communication-message-types.ts`

- [ ] **Step 1: Add the frame type and field**

After the `ExecutionUpdateMessage` interface, add:

```typescript
/** One frame of a loop-iteration stack (outermost first), as sent by the host. */
export interface IterationFrameMsg {
  /** Canvas node id of the loop block. */
  loopId: string;
  /** 0-based iteration index within that loop. */
  i: number;
  /** Foreach item value (truncated host-side); null/absent for while/repeat. */
  label?: string | null;
}
```

And extend `ExecutionUpdateMessage` with one field (after `branchTaken`):

```typescript
  /** Live loop-iteration stack for this step event (outermost first); absent outside loops. */
  iterationStack?: IterationFrameMsg[] | null;
```

(`step-output` messages are handled untyped in the bridge, so no second interface is needed.)

- [ ] **Step 2: Type-check**

Run (from `FlowCanvas/`): `npx tsc --noEmit`
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/communication-message-types.ts
git commit -m "feat(flow-canvas): iteration-stack message types"
```

---

### Task 8: `iterationSlice` — the recording store

**Files:**
- Create: `FlowCanvas/src/stores/slices/iterationSlice.ts`
- Modify: `FlowCanvas/src/stores/useFlowStore.ts`
- Test: `FlowCanvas/src/stores/slices/__tests__/iterationSlice.test.ts`

**Design notes the code must honor (from the spec):**
- Every event writes into the records of EVERY frame on its stack — innermost exact, ancestors aggregated (error sticky, otherwise last write wins, including `outputIdx`).
- Records get a unique monotonic `seq`; `parent` links and selections refer to `seq` (array positions shift under cap eviction; iteration index `i` repeats when an inner loop re-runs per outer iteration).
- Matching an event to an existing record: the loop's LAST record matches iff `last.i === frame.i` AND `last.parent?.seq === <seq resolved for the parent frame in this same pass>` — this is what stops a restarted inner loop (new outer iteration, `i` back to 0) from being merged into the previous outer iteration's record.
- Cap eviction drops oldest records per loop; `totalIterations` keeps the true count for the "of 8,213" display.

- [ ] **Step 1: Write the failing tests**

Create `FlowCanvas/src/stores/slices/__tests__/iterationSlice.test.ts`. Copy the module-level mocks from the top of the sibling `graphSlice.addConnectDelete.test.ts` verbatim (it mocks the layout-autosave util and the MessageBus so importing the store doesn't touch the WebView bridge), then:

```typescript
import { describe, it, expect, beforeEach } from 'vitest';
import { useFlowStore } from '../../useFlowStore';
import type { IterationFrameMsg } from '../../../communication-message-types';

const F = (loopId: string, i: number, label?: string): IterationFrameMsg => ({ loopId, i, label });

function reset() {
  useFlowStore.getState().clearIterations();
  useFlowStore.setState({ iterationHistoryCap: 500 });
}

describe('iterationSlice — recordIterationEvent', () => {
  beforeEach(reset);

  it('creates one record per iteration with per-node entries', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [F('L', 0, 'host0')], { state: 'running' });
    rec('A', [F('L', 0, 'host0')], { state: 'success', duration: 12 });
    rec('A', [F('L', 1, 'host1')], { state: 'success', duration: 15 });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(records).toHaveLength(2);
    expect(records[0].i).toBe(0);
    expect(records[0].label).toBe('host0');
    expect(records[0].nodes.get('A')).toMatchObject({ state: 'success', duration: 12 });
    expect(records[1].i).toBe(1);
    expect(useFlowStore.getState().totalIterations.get('L')).toBe(2);
  });

  it('writes to every frame on the stack — ancestors aggregate with sticky error', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    // Outer iteration 0, inner iterations 0..1; node X errors in inner 0, succeeds in inner 1.
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'error' });
    rec('X', [F('OUT', 0), F('IN', 1)], { state: 'success', duration: 9 });

    const outer = useFlowStore.getState().iterationLog.get('OUT')![0];
    const inner = useFlowStore.getState().iterationLog.get('IN')!;

    // Inner records: exact per-iteration values.
    expect(inner[0].nodes.get('X')!.state).toBe('error');
    expect(inner[1].nodes.get('X')!.state).toBe('success');
    // Outer record aggregates: error is sticky even after a later success.
    expect(outer.nodes.get('X')!.state).toBe('error');
    expect(outer.failed).toBe(true);
    expect(inner[0].failed).toBe(true);
    expect(inner[1].failed).toBe(false);
    // Parent links point at the outer record's seq.
    expect(inner[0].parent).toEqual({ loopId: 'OUT', seq: outer.seq });
  });

  it('restarted inner loops start NEW records, not merged ones', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'success' });
    rec('X', [F('OUT', 1), F('IN', 0)], { state: 'success' }); // inner i restarts at 0

    const inner = useFlowStore.getState().iterationLog.get('IN')!;
    expect(inner).toHaveLength(2);
    expect(inner[0].i).toBe(0);
    expect(inner[1].i).toBe(0);
    expect(inner[0].parent!.seq).not.toBe(inner[1].parent!.seq);
  });

  it('evicts oldest records past the cap and keeps the true total', () => {
    useFlowStore.setState({ iterationHistoryCap: 3 });
    const rec = useFlowStore.getState().recordIterationEvent;
    for (let i = 0; i < 5; i++) rec('A', [F('L', i)], { state: 'success' });

    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(records.map((r) => r.i)).toEqual([2, 3, 4]);
    expect(useFlowStore.getState().totalIterations.get('L')).toBe(5);
  });

  it('ignores malformed frames and empty stacks', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [], { state: 'success' });
    rec('A', [{ loopId: 'L', i: -1 } as IterationFrameMsg], { state: 'success' });
    expect(useFlowStore.getState().iterationLog.size).toBe(0);
  });
});

describe('iterationSlice — selections', () => {
  beforeEach(reset);

  it('selecting an outer iteration resets descendant selections', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'success' });
    rec('X', [F('OUT', 1), F('IN', 0)], { state: 'success' });

    const st = useFlowStore.getState();
    const innerSeq = st.iterationLog.get('IN')![0].seq;
    st.setIterationSelection('IN', innerSeq);
    expect(useFlowStore.getState().iterationSelections.get('IN')).toBe(innerSeq);

    const outerSeq1 = st.iterationLog.get('OUT')![1].seq;
    useFlowStore.getState().setIterationSelection('OUT', outerSeq1);
    expect(useFlowStore.getState().iterationSelections.get('IN')).toBeNull();
  });

  it('selecting an inner iteration pulls every ancestor to the containing iteration', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('X', [F('OUT', 0), F('IN', 0)], { state: 'success' });
    rec('X', [F('OUT', 1), F('IN', 0)], { state: 'success' });

    const st = useFlowStore.getState();
    const secondInner = st.iterationLog.get('IN')![1];
    st.setIterationSelection('IN', secondInner.seq);

    const outerRecords = useFlowStore.getState().iterationLog.get('OUT')!;
    expect(useFlowStore.getState().iterationSelections.get('OUT')).toBe(outerRecords[1].seq);
  });

  it('clearIterations wipes log, selections, and totals', () => {
    const rec = useFlowStore.getState().recordIterationEvent;
    rec('A', [F('L', 0)], { state: 'success' });
    useFlowStore.getState().setIterationSelection('L', useFlowStore.getState().iterationLog.get('L')![0].seq);

    useFlowStore.getState().clearIterations();

    expect(useFlowStore.getState().iterationLog.size).toBe(0);
    expect(useFlowStore.getState().iterationSelections.size).toBe(0);
    expect(useFlowStore.getState().totalIterations.size).toBe(0);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run (from `FlowCanvas/`): `npm run test -- src/stores/slices/__tests__/iterationSlice.test.ts`
Expected: FAIL — the slice does not exist (import/type errors).

- [ ] **Step 3: Implement the slice**

Create `FlowCanvas/src/stores/slices/iterationSlice.ts`:

```typescript
import type { StateCreator } from 'zustand';
import type { FlowStore } from '../useFlowStore';
import type { BlockExecState } from './executionSlice';
import type { IterationFrameMsg } from '../../communication-message-types';
import { messageBus } from '../../MessageBus';
import { CANVAS_HOST_MESSAGES } from '../../communication-message-types';

export const DEFAULT_ITERATION_HISTORY_CAP = 500;

export interface IterationNodeEntry {
  state: BlockExecState;
  branchTaken?: string;
  duration?: number;
  /** Index into executionSlice.blockOutputs[nodeId] for this iteration's output. */
  outputIdx?: number;
}

export interface IterationRecord {
  /** Unique, monotonic per run. Selections and parent links use seq — array positions
   *  shift under eviction, and iteration index i repeats when an inner loop re-runs. */
  seq: number;
  /** 0-based iteration index within the loop (the executor's index — may have gaps
   *  for when-skipped foreach items). */
  i: number;
  /** Foreach item value (truncated host-side); undefined for while/repeat. */
  label?: string;
  /** True if any step in this iteration (at any depth) errored. */
  failed: boolean;
  /** The containing iteration of the next loop up, or null for top-level loops. */
  parent: { loopId: string; seq: number } | null;
  /** Per-node entries: innermost-loop records hold exact values; ancestor records
   *  aggregate (error sticky, otherwise last write wins). */
  nodes: Map<string, IterationNodeEntry>;
}

export interface IterationSlice {
  iterationLog: Map<string, IterationRecord[]>;
  /** Selected record seq per loop node id; null/absent = ALL (aggregate view). */
  iterationSelections: Map<string, number | null>;
  /** True iteration count per loop (survives eviction). */
  totalIterations: Map<string, number>;
  iterationSeq: number;
  iterationHistoryCap: number;

  recordIterationEvent: (
    nodeId: string,
    stack: IterationFrameMsg[],
    patch: Partial<IterationNodeEntry>,
  ) => void;
  setIterationSelection: (loopId: string, seq: number | null) => void;
  clearIterations: () => void;
  /** User-initiated: persists via pref-save. */
  setIterationHistoryCap: (v: number) => void;
  /** Host-driven restore: no pref-save echo. */
  restoreIterationHistoryCap: (v: number) => void;
}

export const createIterationSlice: StateCreator<FlowStore, [], [], IterationSlice> = (set, get) => ({
  iterationLog: new Map(),
  iterationSelections: new Map(),
  totalIterations: new Map(),
  iterationSeq: 0,
  iterationHistoryCap: DEFAULT_ITERATION_HISTORY_CAP,

  recordIterationEvent: (nodeId, stack, patch) => set((s) => {
    if (!Array.isArray(stack) || stack.length === 0) return {};

    const log = new Map(s.iterationLog);
    const totals = new Map(s.totalIterations);
    let nextSeq = s.iterationSeq;
    const eventFailed = patch.state === 'error';
    let parentRef: { loopId: string; seq: number } | null = null;

    for (let d = 0; d < stack.length; d++) {
      const frame = stack[d];
      if (!frame || typeof frame.loopId !== 'string' || frame.loopId.length === 0) continue;
      if (!Number.isFinite(frame.i) || frame.i < 0) continue;

      const records = [...(log.get(frame.loopId) ?? [])];
      const last = records[records.length - 1];
      // The event belongs to the loop's latest record only if BOTH the iteration index and
      // the containing parent iteration match — a restarted inner loop (i back to 0 under a
      // new outer iteration) must start a fresh record, never merge into the old one.
      const sameParent = (last?.parent?.seq ?? null) === (parentRef?.seq ?? null);
      let rec: IterationRecord;
      if (last && last.i === frame.i && sameParent) {
        rec = { ...last, nodes: new Map(last.nodes) };
        records[records.length - 1] = rec;
      } else {
        rec = {
          seq: ++nextSeq,
          i: frame.i,
          label: typeof frame.label === 'string' && frame.label.length > 0 ? frame.label : undefined,
          failed: false,
          parent: parentRef,
          nodes: new Map(),
        };
        records.push(rec);
        if (records.length > s.iterationHistoryCap) {
          records.splice(0, records.length - s.iterationHistoryCap);
        }
      }

      const prev = rec.nodes.get(nodeId);
      rec.nodes.set(nodeId, {
        state: prev?.state === 'error' ? 'error' : (patch.state ?? prev?.state ?? 'running'),
        branchTaken: patch.branchTaken ?? prev?.branchTaken,
        duration: patch.duration ?? prev?.duration,
        outputIdx: patch.outputIdx ?? prev?.outputIdx,
      });
      if (eventFailed) rec.failed = true;

      log.set(frame.loopId, records);
      totals.set(frame.loopId, Math.max(totals.get(frame.loopId) ?? 0, frame.i + 1));
      parentRef = { loopId: frame.loopId, seq: rec.seq };
    }

    return { iterationLog: log, totalIterations: totals, iterationSeq: nextSeq };
  }),

  setIterationSelection: (loopId, seq) => set((s) => {
    const sels = new Map(s.iterationSelections);

    // Walk a loop's parent chain (via any of its records) to test ancestry.
    const isDescendantOf = (childLoop: string, ancestorLoop: string): boolean => {
      let curLoop: string | undefined = childLoop;
      const seen = new Set<string>();
      while (curLoop && !seen.has(curLoop)) {
        seen.add(curLoop);
        const recs = s.iterationLog.get(curLoop) ?? [];
        const parent = recs.find((r) => r.parent)?.parent;
        if (!parent) return false;
        if (parent.loopId === ancestorLoop) return true;
        curLoop = parent.loopId;
      }
      return false;
    };

    // Changing this loop's selection re-ranges every nested loop's iteration list.
    for (const otherLoop of sels.keys()) {
      if (otherLoop !== loopId && isDescendantOf(otherLoop, loopId)) sels.set(otherLoop, null);
    }
    sels.set(loopId, seq);

    // Inner-pulls-outer: a concrete selection forces each ancestor to the containing
    // iteration, so clusters can never contradict each other.
    if (seq != null) {
      let rec = (s.iterationLog.get(loopId) ?? []).find((r) => r.seq === seq);
      const seen = new Set<string>([loopId]);
      while (rec?.parent && !seen.has(rec.parent.loopId)) {
        const pLoop: string = rec.parent.loopId;
        const pSeq: number = rec.parent.seq;
        seen.add(pLoop);
        sels.set(pLoop, pSeq);
        rec = (s.iterationLog.get(pLoop) ?? []).find((r) => r.seq === pSeq);
      }
    }

    return { iterationSelections: sels };
  }),

  clearIterations: () => set({
    iterationLog: new Map(),
    iterationSelections: new Map(),
    totalIterations: new Map(),
    iterationSeq: 0,
  }),

  setIterationHistoryCap: (v) => {
    if (!Number.isFinite(v) || v < 1) return;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.prefSave, iterationHistoryCap: v });
    set({ iterationHistoryCap: v });
  },

  restoreIterationHistoryCap: (v) => {
    if (!Number.isFinite(v) || v < 1) return;
    set({ iterationHistoryCap: v }); // host-driven, no echo
  },
});
```

Wire it in `FlowCanvas/src/stores/useFlowStore.ts` — add the import, the type union member, and the spread, exactly like the other ten slices:

```typescript
import { createIterationSlice, type IterationSlice } from './slices/iterationSlice';
```
```typescript
export type FlowStore = GraphSlice &
  ExecutionSlice &
  DebugSlice &
  VariableSlice &
  UndoSlice &
  TimelineSlice &
  UISlice &
  CommentSlice &
  HostSlice &
  SettingsSlice &
  IterationSlice;
```
```typescript
  ...createIterationSlice(...a),
```

- [ ] **Step 4: Run tests to verify they pass**

Run (from `FlowCanvas/`): `npm run test -- src/stores/slices/__tests__/iterationSlice.test.ts`
Expected: PASS (8 tests)

- [ ] **Step 5: Type-check and run the full vitest suite**

Run (from `FlowCanvas/`): `npx tsc --noEmit && npm test`
Expected: clean + all existing tests still pass.

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/stores/slices/iterationSlice.ts FlowCanvas/src/stores/useFlowStore.ts FlowCanvas/src/stores/slices/__tests__/iterationSlice.test.ts
git commit -m "feat(flow-canvas): iteration log store with seq-keyed records and hierarchical selections"
```

---

### Task 9: messageBridge wiring + clearPath reset

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts` (executionStarted ~line 203; executionUpdate ~line 218; stepOutput ~line 282; prefRestore ~line 452)
- Modify: `FlowCanvas/src/stores/slices/executionSlice.ts` (`clearPath`, ~line 124)

The recording logic itself is fully covered by Task 8's slice tests; this task is thin plumbing, asserted end-to-end in Task 15's e2e spec.

- [ ] **Step 1: Clear iteration state when a run starts**

In the `executionStarted` handler, after `store.getState().clearTimeline();` add:

```typescript
      store.getState().clearIterations();
```

- [ ] **Step 2: Record from `executionUpdate`**

At the END of the `executionUpdate` handler (after the existing `branchTaken` block), add:

```typescript
      // Iteration attribution: every tagged event lands in the iteration log (transient,
      // never written onto node.data, so export is unaffected).
      if (Array.isArray(msg.iterationStack) && msg.iterationStack.length > 0) {
        state.recordIterationEvent(stepId, msg.iterationStack, {
          state: execState,
          duration: msg.duration != null ? Number(msg.duration) : undefined,
          branchTaken:
            typeof msg.branchTaken === 'string' && msg.branchTaken.trim().length > 0
              ? msg.branchTaken.trim()
              : undefined,
        });
      }
```

- [ ] **Step 3: Record output indices from `stepOutput`**

Replace the `stepOutput` handler body:

```typescript
    // Per-step output
    messageBus.on(CANVAS_HOST_MESSAGES.incoming.stepOutput, (msg) => {
      if (msg.stepId && msg.output) {
        const stepId = String(msg.stepId);
        store.getState().appendBlockOutput(
          stepId,
          String(msg.output),
          msg.stepType ? String(msg.stepType) : undefined
        );
        // Tie this output entry to its iteration so the stepper can recall it.
        if (Array.isArray(msg.iterationStack) && msg.iterationStack.length > 0) {
          const outputIdx = (store.getState().blockOutputs.get(stepId)?.length ?? 1) - 1;
          store.getState().recordIterationEvent(stepId, msg.iterationStack, { outputIdx });
        }
      }
    }),
```

- [ ] **Step 4: Restore the cap pref**

In the `prefRestore` handler (~line 452), after the `reducedMotion` check, add:

```typescript
      if (typeof msg.iterationHistoryCap === 'number' && msg.iterationHistoryCap > 0) {
        store.getState().restoreIterationHistoryCap(msg.iterationHistoryCap);
      }
```

- [ ] **Step 5: Clear Path resets selections**

In `FlowCanvas/src/stores/slices/executionSlice.ts`, the `clearPath` action becomes:

```typescript
  // Clear Path: hide the edge highlight only. Node blockStates/badges are untouched.
  // Iteration selections reset too — a hidden path with a live iteration scope would
  // leave badges/outputs silently showing one iteration with no visible cue.
  clearPath: () => set({ pathVisible: false, iterationSelections: new Map() }),
```

(`iterationSelections` is a sibling slice's key in the same flat store — setting it here compiles because the slice's `set` is typed against `FlowStore`.)

- [ ] **Step 6: Type-check and run the suite**

Run (from `FlowCanvas/`): `npx tsc --noEmit && npm test`
Expected: clean + PASS.

- [ ] **Step 7: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts FlowCanvas/src/stores/slices/executionSlice.ts
git commit -m "feat(flow-canvas): record iteration events from host messages"
```

---

### Task 10: Scope selectors + iteration-scoped edge path status

**Files:**
- Create: `FlowCanvas/src/stores/selectors/iterationScope.ts`
- Modify: `FlowCanvas/src/stores/selectors/edgePath.ts`
- Test: `FlowCanvas/src/stores/selectors/__tests__/iterationScope.test.ts`

**Import direction (avoids a cycle):** `LOOP_TYPES` moves to `iterationScope.ts` and is exported; `edgePath.ts` imports it from there and deletes its local copy. `iterationScope.ts` must NOT import anything from `edgePath.ts`.

- [ ] **Step 1: Write the failing tests**

Create `FlowCanvas/src/stores/selectors/__tests__/iterationScope.test.ts` (same module-level mocks as the Task 8 test file):

```typescript
import { describe, it, expect, beforeEach } from 'vitest';
import { useFlowStore } from '../../useFlowStore';
import type { IterationFrameMsg } from '../../../communication-message-types';
import { selectIterationScope, selectVisibleIterations, LOOP_TYPES } from '../iterationScope';
import { selectEdgePathStatus } from '../edgePath';
import type { Node, Edge } from '@xyflow/react';

const F = (loopId: string, i: number, label?: string): IterationFrameMsg => ({ loopId, i, label });

const node = (id: string, blockType: string, props: Record<string, unknown> = {}): Node => ({
  id,
  type: 'flowBlock',
  position: { x: 0, y: 0 },
  data: { blockType, props },
});

const edge = (id: string, source: string, target: string): Edge => ({ id, source, target });

/** Foreach L wrapping A -> B, with a plain successor T after the loop. */
function seedGraph() {
  useFlowStore.setState({
    nodes: [
      node('L', 'foreach', { _stepPath: 'steps/0' }),
      node('A', 'ssh', { _stepPath: 'steps/0/do/0', _isChildOf: 'L', _branchLabel: 'do' }),
      node('B', 'print', { _stepPath: 'steps/0/do/1', _isChildOf: 'L', _branchLabel: 'do' }),
      node('T', 'ssh', { _stepPath: 'steps/1' }),
    ],
    edges: [edge('eLA', 'L', 'A'), edge('eAB', 'A', 'B'), edge('eLT', 'L', 'T')],
  });
}

function runTwoIterations() {
  const st = useFlowStore.getState();
  // Iteration 0: A and B both run and succeed.
  st.recordIterationEvent('A', [F('L', 0, 'h0')], { state: 'success', duration: 5 });
  st.recordIterationEvent('B', [F('L', 0, 'h0')], { state: 'success', duration: 7 });
  // Iteration 1: only A runs (B never reached).
  st.recordIterationEvent('A', [F('L', 1, 'h1')], { state: 'success', duration: 6 });
  // Aggregate state as the run would leave it.
  st.setBlockState('L', 'success');
  st.setBlockState('A', 'success');
  st.setBlockState('B', 'success');
  st.setBlockState('T', 'success');
  st.setLoopIteration('L', 2);
}

beforeEach(() => {
  useFlowStore.getState().clearIterations();
  useFlowStore.getState().clearExecution();
  useFlowStore.setState({ nodes: [], edges: [], pathVisible: true });
});

describe('selectIterationScope', () => {
  it('returns null with no selections (aggregate view)', () => {
    seedGraph();
    runTwoIterations();
    expect(selectIterationScope(useFlowStore.getState(), 'A')).toBeNull();
  });

  it('returns the selected record for nodes inside the loop, null outside', () => {
    seedGraph();
    runTwoIterations();
    const st = useFlowStore.getState();
    const rec0 = st.iterationLog.get('L')![0];
    st.setIterationSelection('L', rec0.seq);

    const after = useFlowStore.getState();
    expect(selectIterationScope(after, 'A')?.seq).toBe(rec0.seq);
    expect(selectIterationScope(after, 'B')?.seq).toBe(rec0.seq);
    expect(selectIterationScope(after, 'T')).toBeNull();   // outside the loop
    expect(selectIterationScope(after, 'L')).toBeNull();   // the loop node itself is governed by ITS ancestors
  });
});

describe('selectVisibleIterations', () => {
  it('is unconstrained without ancestor selections and exposes LOOP_TYPES', () => {
    seedGraph();
    runTwoIterations();
    expect(LOOP_TYPES.has('foreach')).toBe(true);
    expect(selectVisibleIterations(useFlowStore.getState(), 'L')).toHaveLength(2);
  });
});

describe('selectEdgePathStatus under iteration scope', () => {
  it('scopes loop-body edges to the selected iteration', () => {
    seedGraph();
    runTwoIterations();
    const st = useFlowStore.getState();

    // Aggregate: everything reached at least once → on-path.
    expect(selectEdgePathStatus(st, 'eAB')).toBe('on-path');

    // Iteration 0: B was reached → eAB on-path.
    st.setIterationSelection('L', st.iterationLog.get('L')![0].seq);
    expect(selectEdgePathStatus(useFlowStore.getState(), 'eAB')).toBe('on-path');

    // Iteration 1: B never ran → eAB drops out of the path.
    useFlowStore.getState().setIterationSelection('L', useFlowStore.getState().iterationLog.get('L')![1].seq);
    expect(selectEdgePathStatus(useFlowStore.getState(), 'eAB')).not.toBe('on-path');

    // The edge to T (outside the loop) keeps its aggregate status either way.
    expect(selectEdgePathStatus(useFlowStore.getState(), 'eLT')).toBe('on-path');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run (from `FlowCanvas/`): `npm run test -- src/stores/selectors/__tests__/iterationScope.test.ts`
Expected: FAIL — `iterationScope.ts` does not exist.

- [ ] **Step 3: Implement `iterationScope.ts`**

Create `FlowCanvas/src/stores/selectors/iterationScope.ts`:

```typescript
import type { Node } from '@xyflow/react';
import type { FlowStore } from '../useFlowStore';
import type { IterationRecord } from '../slices/iterationSlice';

/** Loop container block types. Owned here (not edgePath) so edgePath can import it
 *  while this module stays free of edgePath imports (no cycle). */
export const LOOP_TYPES = new Set(['foreach', 'while', 'repeat']);

function propsOf(node: Node | undefined): Record<string, unknown> {
  const data = (node?.data ?? {}) as Record<string, unknown>;
  return (data.props ?? {}) as Record<string, unknown>;
}

function blockTypeOf(node: Node | undefined): string | undefined {
  const data = (node?.data ?? {}) as Record<string, unknown>;
  return typeof data.blockType === 'string' ? data.blockType : undefined;
}

function recordBySeq(state: FlowStore, loopId: string, seq: number): IterationRecord | undefined {
  return (state.iterationLog.get(loopId) ?? []).find((r) => r.seq === seq);
}

/**
 * The governing iteration record for a node: walk the node's ancestor chain
 * (props._isChildOf) and return the selected record of the INNERMOST loop ancestor
 * that has a non-null selection. Null = aggregate view. Because events are written
 * to every frame on their stack, the innermost selected ancestor's record answers
 * for every node beneath it.
 */
export function selectIterationScope(state: FlowStore, nodeId: string): IterationRecord | null {
  let cur = nodeId;
  const seen = new Set<string>();
  while (!seen.has(cur)) {
    seen.add(cur);
    const node = state.nodes.find((n) => n.id === cur);
    const parentId = propsOf(node)['_isChildOf'];
    if (typeof parentId !== 'string' || parentId.length === 0) return null;
    const parentNode = state.nodes.find((n) => n.id === parentId);
    const bt = blockTypeOf(parentNode);
    if (bt && LOOP_TYPES.has(bt)) {
      const sel = state.iterationSelections.get(parentId);
      if (sel != null) {
        const rec = recordBySeq(state, parentId, sel);
        if (rec) return rec;
      }
    }
    cur = parentId;
  }
  return null;
}

/**
 * The records of `loopId` visible under the current ancestor selections, time-ordered.
 * Unconstrained when no ancestor loop has a selection.
 */
export function selectVisibleIterations(state: FlowStore, loopId: string): IterationRecord[] {
  const records = state.iterationLog.get(loopId) ?? [];
  const governing = selectIterationScope(state, loopId);
  if (!governing) return records;
  return records.filter((r) => {
    let p = r.parent;
    const seen = new Set<number>();
    while (p && !seen.has(p.seq)) {
      if (p.seq === governing.seq) return true;
      seen.add(p.seq);
      p = recordBySeq(state, p.loopId, p.seq)?.parent ?? null;
    }
    return false;
  });
}
```

- [ ] **Step 4: Scope `selectEdgePathStatus`**

In `FlowCanvas/src/stores/selectors/edgePath.ts`:

(a) Delete the local `const LOOP_TYPES = new Set(['foreach', 'while', 'repeat']);` and add to the imports:

```typescript
import { selectIterationScope, LOOP_TYPES } from './iterationScope';
```

(b) In `selectEdgePathStatus`, directly after the `sourceNode`/`targetNode`/`blockType` lookups (before the branch-detection block), insert:

```typescript
  // Iteration scoping: when any ancestor loop of this edge's target has a selected
  // iteration, the edge reflects that single iteration instead of the aggregate.
  // Reached in that iteration (any recorded state, incl. skipped) → on-path; a branch
  // arm whose child never ran that iteration → untaken; anything else → idle.
  const iterScope = selectIterationScope(state, edge.target);
  if (iterScope) {
    if (iterScope.nodes.has(edge.target)) return 'on-path';
    return edgeIsBranch(edge, targetNode) ? 'untaken' : 'idle';
  }
```

- [ ] **Step 5: Run tests to verify they pass**

Run (from `FlowCanvas/`): `npm run test -- src/stores/selectors/__tests__/iterationScope.test.ts`
Expected: PASS (4 tests)

- [ ] **Step 6: Type-check and full suite (edgePath has existing consumers)**

Run (from `FlowCanvas/`): `npx tsc --noEmit && npm test`
Expected: clean + PASS — especially any existing edge-path/live-wires vitest suites.

- [ ] **Step 7: Commit**

```bash
git add FlowCanvas/src/stores/selectors/iterationScope.ts FlowCanvas/src/stores/selectors/edgePath.ts FlowCanvas/src/stores/selectors/__tests__/iterationScope.test.ts
git commit -m "feat(flow-canvas): iteration-scoped path status via scope selectors"
```

---

### Task 11: `IterationCluster` component + band integration

**Files:**
- Create: `FlowCanvas/src/nodes/IterationCluster.tsx`
- Modify: `FlowCanvas/src/nodes/BranchBandsLayer.tsx`
- Test: `FlowCanvas/src/nodes/__tests__/IterationCluster.test.tsx`

- [ ] **Step 1: Write the failing tests**

Create `FlowCanvas/src/nodes/__tests__/IterationCluster.test.tsx` (same module-level mocks as the Task 8 store test — copy them from `graphSlice.addConnectDelete.test.ts`; jsdom + @testing-library are already configured for this project per the existing component-test harness):

```tsx
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { useFlowStore } from '../../stores/useFlowStore';
import type { IterationFrameMsg } from '../../communication-message-types';
import type { BranchBand } from '../../utils/branchBands';
import IterationCluster from '../IterationCluster';

const F = (loopId: string, i: number, label?: string): IterationFrameMsg => ({ loopId, i, label });

const band: BranchBand = {
  id: 'L::do', parentId: 'L', branchKey: 'do',
  x: 0, y: 0, width: 320, height: 120,
  colorVar: 'var(--fc-branch-warning)', depth: 0, memberIds: [],
};

function seed(iterations: number, failedAt: number[] = []) {
  useFlowStore.getState().clearIterations();
  useFlowStore.setState({ isRunning: false });
  const rec = useFlowStore.getState().recordIterationEvent;
  for (let i = 0; i < iterations; i++) {
    rec('A', [F('L', i, `host${i}`)], { state: failedAt.includes(i) ? 'error' : 'success' });
  }
}

describe('IterationCluster', () => {
  beforeEach(() => seed(3));

  it('renders nothing while a run is in progress', () => {
    useFlowStore.setState({ isRunning: true });
    render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iteration-cluster')).toBeNull();
  });

  it('renders nothing when the loop has no recorded iterations', () => {
    useFlowStore.getState().clearIterations();
    render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iteration-cluster')).toBeNull();
  });

  it('shows the count in ALL mode and steps into iteration 1 on ▶', () => {
    render(<IterationCluster band={band} />);
    expect(screen.getByTestId('iter-counter').textContent).toBe('3');

    fireEvent.click(screen.getByTestId('iter-next'));
    expect(screen.getByTestId('iter-counter').textContent).toBe('1/3');
    expect(screen.getByTestId('iter-label').textContent).toBe('host0');

    const sel = useFlowStore.getState().iterationSelections.get('L');
    expect(sel).toBe(useFlowStore.getState().iterationLog.get('L')![0].seq);
  });

  it('ALL chip returns to the aggregate view', () => {
    render(<IterationCluster band={band} />);
    fireEvent.click(screen.getByTestId('iter-next'));
    fireEvent.click(screen.getByTestId('iter-all'));
    expect(useFlowStore.getState().iterationSelections.get('L')).toBeNull();
    expect(screen.getByTestId('iter-counter').textContent).toBe('3');
  });

  it('⚠ chip appears only with failures and jumps to the failed iteration', () => {
    render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iter-fail')).toBeNull();

    seed(3, [1]);
    render(<IterationCluster band={band} />);
    const fail = screen.getByTestId('iter-fail');
    expect(fail.textContent).toContain('1');

    fireEvent.click(fail);
    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(useFlowStore.getState().iterationSelections.get('L')).toBe(records[1].seq);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run (from `FlowCanvas/`): `npm run test -- src/nodes/__tests__/IterationCluster.test.tsx`
Expected: FAIL — component does not exist.

- [ ] **Step 3: Implement the component**

Create `FlowCanvas/src/nodes/IterationCluster.tsx`:

```tsx
import { useMemo, type CSSProperties } from 'react';
import { useFlowStore } from '../stores/useFlowStore';
import { selectVisibleIterations } from '../stores/selectors/iterationScope';
import { mix } from '../utils/tokens';
import type { BranchBand } from '../utils/branchBands';

const chip: CSSProperties = {
  borderRadius: 999, padding: '2px 7px', cursor: 'pointer',
  font: 'inherit', letterSpacing: '0.05em',
};
const arrow: CSSProperties = {
  background: 'none', border: 'none', cursor: 'pointer',
  color: 'var(--fc-accent)', font: 'inherit', padding: '0 2px',
};

interface IterationClusterProps {
  band: BranchBand;
}

/**
 * Post-run iteration stepper pinned to a loop band's top-right:
 *   [ALL] [◀ web-02 · 3/12 ▶] [⚠ 2]
 * Stepping re-scopes the path overlay, badges, durations, and Block Output to one
 * iteration (selectors do the scoping; this control only drives iterationSelections).
 */
export default function IterationCluster({ band }: IterationClusterProps) {
  const loopId = band.parentId;
  const isRunning = useFlowStore((s) => s.isRunning);
  // Subscribe to the stable map references; derive the (fresh-array) visible list in a
  // memo so the zustand snapshot stays referentially stable between store changes.
  const log = useFlowStore((s) => s.iterationLog);
  const sels = useFlowStore((s) => s.iterationSelections);
  const nodes = useFlowStore((s) => s.nodes);
  const total = useFlowStore((s) => s.totalIterations.get(loopId) ?? 0);
  const setSelection = useFlowStore((s) => s.setIterationSelection);

  const visible = useMemo(
    () => selectVisibleIterations(useFlowStore.getState(), loopId),
    [log, sels, nodes, loopId],
  );

  if (isRunning || visible.length === 0) return null;

  const selection = sels.get(loopId) ?? null;
  const pos = selection == null ? -1 : visible.findIndex((r) => r.seq === selection);
  const current = pos >= 0 ? visible[pos] : null;
  const failedCount = visible.filter((r) => r.failed).length;
  const kept = log.get(loopId)?.length ?? 0;
  const evicted = total > kept;

  const step = (delta: 1 | -1) => {
    const next = pos < 0
      ? (delta > 0 ? 0 : visible.length - 1)
      : Math.min(visible.length - 1, Math.max(0, pos + delta));
    setSelection(loopId, visible[next].seq);
  };

  const jumpFailed = () => {
    if (failedCount === 0) return;
    for (let k = 1; k <= visible.length; k++) {
      const idx = ((pos < 0 ? -1 : pos) + k + visible.length) % visible.length;
      if (visible[idx].failed) { setSelection(loopId, visible[idx].seq); return; }
    }
  };

  const label = current ? (current.label ?? `#${current.i + 1}`) : null;
  const counter = pos < 0 ? `${visible.length}` : `${pos + 1}/${visible.length}`;

  return (
    <div
      data-testid="iteration-cluster"
      style={{
        position: 'absolute',
        transform: `translate(calc(${band.x + band.width - 8}px - 100%), ${band.y - 11}px)`,
        display: 'flex', alignItems: 'center', gap: 4,
        zIndex: 6, pointerEvents: 'auto',
        font: '600 9px/1.4 system-ui, sans-serif',
      }}
    >
      <button
        data-testid="iter-all"
        onClick={() => setSelection(loopId, null)}
        title="Show all iterations (aggregate view)"
        style={{
          ...chip,
          color: pos < 0 ? 'oklch(17% 0.02 275)' : 'var(--fc-text-secondary)',
          background: pos < 0 ? band.colorVar : 'var(--fc-surface-0)',
          border: `1px solid ${mix(band.colorVar, 45)}`,
        }}
      >
        ALL
      </button>
      <span style={{
        display: 'inline-flex', alignItems: 'center', gap: 4,
        background: 'var(--fc-surface-0)',
        border: `1px solid ${mix(band.colorVar, 45)}`,
        borderRadius: 999, padding: '2px 6px',
      }}>
        <button data-testid="iter-prev" onClick={() => step(-1)} style={arrow} title="Previous iteration">◀</button>
        {label && (
          <span
            data-testid="iter-label"
            title={label}
            style={{
              fontFamily: 'Consolas, monospace', color: 'var(--fc-edge-traversed)',
              maxWidth: 90, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap',
            }}
          >
            {label}
          </span>
        )}
        <span data-testid="iter-counter" style={{ color: 'oklch(88% 0.02 275)', fontVariantNumeric: 'tabular-nums' }}>
          {counter}
        </span>
        <button data-testid="iter-next" onClick={() => step(1)} style={arrow} title="Next iteration">▶</button>
      </span>
      {failedCount > 0 && (
        <button
          data-testid="iter-fail"
          onClick={jumpFailed}
          title={`Jump to next failed iteration (${failedCount} failed)`}
          style={{
            ...chip,
            color: 'var(--fc-state-error)', background: 'var(--fc-surface-0)',
            border: '1px solid color-mix(in oklch, var(--fc-state-error) 55%, transparent)',
          }}
        >
          ⚠ {failedCount}
        </button>
      )}
      {evicted && (
        <span data-testid="iter-evicted" style={{ color: 'var(--fc-text-secondary)' }}>
          of {total}
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Render it from `BranchBandsLayer`**

In `FlowCanvas/src/nodes/BranchBandsLayer.tsx`, add the import:

```tsx
import IterationCluster from './IterationCluster';
```

and inside the `<ViewportPortal>`, after the band-handle `bands.map(...)` block, add a third sibling map:

```tsx
      {/* Iteration stepper clusters: one per loop band, top-right. Post-run only —
          the component returns null while running or with no recorded iterations.
          Sibling of the rectangles/handles so zIndex isn't trapped (same precedent). */}
      {bands.filter((b) => b.branchKey === 'do').map((b) => (
        <IterationCluster key={`${b.id}::iters`} band={b} />
      ))}
```

Known limitation (by design, documented in the spec): the cluster lives in the bands layer, so disabling branch bands hides it too — the band is the visual anchor.

- [ ] **Step 5: Run tests to verify they pass**

Run (from `FlowCanvas/`): `npm run test -- src/nodes/__tests__/IterationCluster.test.tsx`
Expected: PASS (5 tests)

- [ ] **Step 6: Type-check + full suite, then commit**

Run (from `FlowCanvas/`): `npx tsc --noEmit && npm test`
Expected: clean + PASS.

```bash
git add FlowCanvas/src/nodes/IterationCluster.tsx FlowCanvas/src/nodes/BranchBandsLayer.tsx FlowCanvas/src/nodes/__tests__/IterationCluster.test.tsx
git commit -m "feat(flow-canvas): iteration stepper cluster on loop bands"
```

---

### Task 12: Scoped BaseBlock badges + OutputPreview sync

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx` (store reads ~line 95–110; duration ~line 140–145; execIndicator ~line 240–305)
- Modify: `FlowCanvas/src/panels/OutputPreview.tsx` (state block ~line 23–40)

Behavior here is asserted by the Task 15 e2e spec (badge text, output index per iteration); no new vitest file.

- [ ] **Step 1: Scope BaseBlock's display values**

In `FlowCanvas/src/nodes/BaseBlock.tsx`, add the import:

```tsx
import { selectIterationScope, selectVisibleIterations, LOOP_TYPES } from '../stores/selectors/iterationScope';
```

Replace the two aggregate reads (`loopIteration` / `branchTakenKey`, ~line 103–104) with:

```tsx
  const loopIterationAggregate = useFlowStore((s) => s.loopIterations.get(id));
  const branchTakenAggregate = useFlowStore((s) => s.branchTaken.get(id));
  // Iteration scoping: when an ancestor loop has a selected iteration, this block's
  // indicator/badges/duration show that single iteration. Display-only and transient —
  // node.data is never written. The governing record is referentially stable between
  // store changes, so this selector doesn't churn renders.
  const iterScope = useFlowStore((s) => selectIterationScope(s, id));
  const scopedEntry = iterScope?.nodes.get(id);
```

BaseBlock already has the node's `blockType` in scope (it drives the icon/category); reusing that variable, add below the lines above:

```tsx
  const isLoopBlock = LOOP_TYPES.has(blockType);
  // An inner loop's ×N under an outer selection = its iterations within that outer iteration.
  const scopedInnerCount = useFlowStore((s) =>
    iterScope && isLoopBlock ? selectVisibleIterations(s, id).length : null,
  );
  const loopIteration = iterScope ? (scopedInnerCount ?? undefined) : loopIterationAggregate;
  const branchTakenKey = iterScope ? scopedEntry?.branchTaken : branchTakenAggregate;
```

If `blockType` is declared *below* this point in the file, move these two lines after its declaration — the order of plain `const` derivations is free; keep all `useFlowStore` hook calls unconditional and in a fixed order.

Replace the duration line (~line 143):

```tsx
  const durationMs = iterScope ? scopedEntry?.duration : timing?.duration;
```

Add the display state right after:

```tsx
  // Not reached in the selected iteration → render as idle (chip hidden).
  const displayExecState = iterScope ? (scopedEntry?.state ?? 'idle') : execState;
```

Then in the `execIndicator` block (~line 240) replace every `execState` reference with `displayExecState` — the outer condition (`displayExecState !== 'idle' && displayExecState !== 'disabled'`), the color ternary, the RUNNING/DONE/SKIP/ERROR ternary, and the `badgeText` line (~line 145):

```tsx
  const badgeText = displayExecState === 'running' ? liveText : durationText;
```

Leave every OTHER use of `execState` in the file (cinematics classes, disabled handling, etc.) untouched — only the indicator/badge cluster is scoped.

- [ ] **Step 2: Sync OutputPreview's history index**

In `FlowCanvas/src/panels/OutputPreview.tsx`:

Add `useEffect` to the React import if absent, and:

```tsx
import { selectIterationScope } from '../stores/selectors/iterationScope';
```

After the existing state declarations (below `const [historyIndex, setHistoryIndex] = useState(-1);`), add:

```tsx
  const iterScope = useFlowStore((s) => (nodeId ? selectIterationScope(s, nodeId) : null));
  // Iteration stepper sync: a selected iteration pins the viewer to that iteration's
  // output entry; returning to ALL returns to the latest.
  useEffect(() => {
    if (!nodeId) return;
    if (!iterScope) { setHistoryIndex(-1); return; }
    const idx = iterScope.nodes.get(nodeId)?.outputIdx;
    setHistoryIndex(idx != null ? idx : -1);
  }, [iterScope, nodeId]);
```

(The manual ◀/▶ history buttons still work — they just move within the run's entries; the effect re-pins only when the scope or node changes.)

- [ ] **Step 3: Type-check + full suite**

Run (from `FlowCanvas/`): `npx tsc --noEmit && npm test`
Expected: clean + PASS (BaseBlock has existing vitest coverage — the aggregate path must be unchanged when no selection exists).

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/src/panels/OutputPreview.tsx
git commit -m "feat(flow-canvas): iteration-scoped badges, durations and block output"
```

---

### Task 13: Tick scrubber for N > 20

**Files:**
- Modify: `FlowCanvas/src/nodes/IterationCluster.tsx`
- Test: `FlowCanvas/src/nodes/__tests__/IterationCluster.test.tsx` (extend)

- [ ] **Step 1: Write the failing tests**

Append to the IterationCluster test file:

```tsx
describe('IterationCluster — scrubber', () => {
  it('appears above 20 iterations, not at 20', () => {
    seed(20);
    const { unmount } = render(<IterationCluster band={band} />);
    expect(screen.queryByTestId('iter-scrubber')).toBeNull();
    unmount();

    seed(21);
    render(<IterationCluster band={band} />);
    expect(screen.getByTestId('iter-scrubber')).not.toBeNull();
  });

  it('buckets ticks at 60 max and clicking a tick selects its first iteration', () => {
    seed(200, [150]);
    render(<IterationCluster band={band} />);
    const ticks = screen.getAllByTestId('iter-tick');
    expect(ticks.length).toBeLessThanOrEqual(60);

    fireEvent.click(ticks[0]);
    const records = useFlowStore.getState().iterationLog.get('L')!;
    expect(useFlowStore.getState().iterationSelections.get('L')).toBe(records[0].seq);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run (from `FlowCanvas/`): `npm run test -- src/nodes/__tests__/IterationCluster.test.tsx`
Expected: FAIL — no `iter-scrubber` testid.

- [ ] **Step 3: Implement the scrubber**

In `IterationCluster.tsx`, add constants below the imports:

```tsx
/** Scrubber appears when the visible iteration count exceeds this. */
const SCRUBBER_THRESHOLD = 20;
/** Above this many iterations, ticks become buckets. */
const MAX_TICKS = 60;
/** Horizontal clearances along the band's top edge (px): past the LOOP pill / reserved for the cluster. */
const SCRUB_LEFT = 86;
const SCRUB_RIGHT_RESERVE = 230;
```

Compute buckets before the `return` (after `counter`):

```tsx
  const showScrubber = visible.length > SCRUBBER_THRESHOLD;
  const bucketSize = showScrubber ? Math.ceil(visible.length / MAX_TICKS) : 1;
  const buckets: { startPos: number; failed: boolean; active: boolean }[] = [];
  if (showScrubber) {
    for (let b = 0; b * bucketSize < visible.length; b++) {
      const start = b * bucketSize;
      const end = Math.min(visible.length, start + bucketSize);
      buckets.push({
        startPos: start,
        failed: visible.slice(start, end).some((r) => r.failed),
        active: pos >= start && pos < end,
      });
    }
  }
```

Wrap the existing root in a fragment and render the track as a second absolutely-positioned sibling (it sits along the band's top edge between the LOOP pill and the cluster, so the cluster itself never widens):

```tsx
  return (
    <>
      {showScrubber && (
        <div
          data-testid="iter-scrubber"
          style={{
            position: 'absolute',
            transform: `translate(${band.x + SCRUB_LEFT}px, ${band.y - 8}px)`,
            width: Math.max(60, band.width - SCRUB_LEFT - SCRUB_RIGHT_RESERVE),
            height: 15, display: 'flex', alignItems: 'center', gap: 1,
            background: 'var(--fc-surface-0)',
            border: `1px solid ${mix(band.colorVar, 45)}`,
            borderRadius: 999, padding: '0 6px',
            zIndex: 6, pointerEvents: 'auto',
          }}
        >
          {buckets.map((bk, idx) => (
            <span
              key={idx}
              data-testid="iter-tick"
              onClick={() => setSelection(loopId, visible[bk.startPos].seq)}
              title={`${visible[bk.startPos].label ?? `#${visible[bk.startPos].i + 1}`} · ${bk.startPos + 1}/${visible.length}`}
              style={{
                flex: 1, minWidth: 2, borderRadius: 2, cursor: 'pointer',
                height: bk.active ? 11 : bk.failed ? 8 : 6,
                background: bk.active
                  ? 'var(--fc-edge-traversed)'
                  : bk.failed
                    ? 'var(--fc-state-error)'
                    : mix(band.colorVar, 35),
              }}
            />
          ))}
        </div>
      )}
      <div data-testid="iteration-cluster" ...the existing cluster div unchanged... </div>
    </>
  );
```

- [ ] **Step 4: Run tests to verify they pass**

Run (from `FlowCanvas/`): `npm run test -- src/nodes/__tests__/IterationCluster.test.tsx`
Expected: PASS (7 tests)

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/nodes/IterationCluster.tsx FlowCanvas/src/nodes/__tests__/IterationCluster.test.tsx
git commit -m "feat(flow-canvas): bucketed tick scrubber for loops past 20 iterations"
```

---

### Task 14: Iteration-history cap setting (React popover + C# persistence)

**Files:**
- Modify: `FlowCanvas/src/panels/SettingsPopover.tsx`
- Modify: `Models/AppConfiguration.cs` (`WindowState`, ~line 478–543)
- Modify: `UI/FlowCanvasForm.cs` (`SaveReducedMotionPref` ~line 494–503; pref-restore sender ~line 441–442)

The React slice actions (`setIterationHistoryCap` → pref-save echo, `restoreIterationHistoryCap`) and the bridge's pref-restore handling already landed in Tasks 8–9; this task adds the UI control and the C# persistence.

- [ ] **Step 1: Add the popover control**

In `FlowCanvas/src/panels/SettingsPopover.tsx`, define next to the existing `densityOptions`:

```tsx
  const iterationCapOptions: readonly { label: string; v: number }[] = [
    { label: '100', v: 100 }, { label: '250', v: 250 }, { label: '500', v: 500 },
    { label: '1k', v: 1000 }, { label: '5k', v: 5000 },
  ];
  const iterationHistoryCap = useFlowStore((s) => s.iterationHistoryCap);
  const setIterationHistoryCap = useFlowStore((s) => s.setIterationHistoryCap);
```

and render, below the existing `Canvas density` Segmented control:

```tsx
          <Segmented
            label="Loop history (iterations kept per loop)"
            value={iterationHistoryCap}
            options={iterationCapOptions}
            onChange={setIterationHistoryCap}
          />
```

(`Segmented` is the same control the density setting uses; match its exact prop names from that usage if they differ.)

- [ ] **Step 2: Persist in `WindowState`**

In `Models/AppConfiguration.cs`, after `FlowCanvasCompactComments` (~line 289):

```csharp
        // Flow Canvas loop-iteration history cap (persisted from React UI; null = React default of 500)
        public int? FlowCanvasIterationHistoryCap { get; set; }
```

- [ ] **Step 3: Extend the pref-save handler**

In `UI/FlowCanvasForm.cs`, `SaveReducedMotionPref` becomes (same name — it's the pref-save sink):

```csharp
        private void SaveReducedMotionPref(JObject msg)
        {
            if (_configService == null) return;
            var v = msg["reducedMotion"]?.Value<bool>();
            var cap = msg["iterationHistoryCap"]?.Value<int>();
            if (v == null && cap == null) return;
            _configService.Update(c =>
            {
                c.WindowState ??= new Models.WindowState();
                if (v != null) c.WindowState.FlowCanvasReducedMotion = v.Value;
                if (cap is > 0) c.WindowState.FlowCanvasIterationHistoryCap = cap.Value;
            });
        }
```

- [ ] **Step 4: Extend the pref-restore sender**

Still in `UI/FlowCanvasForm.cs`, replace lines ~441–442:

```csharp
            var rm = ws.FlowCanvasReducedMotion;
            var iterCap = ws.FlowCanvasIterationHistoryCap;
            if (rm.HasValue || iterCap.HasValue)
                SendMessage(new { type = "pref-restore", reducedMotion = rm, iterationHistoryCap = iterCap });
```

(`reducedMotion` may now serialize as `null` when only the cap is set — the React handler type-checks each field independently, and the "no prefs → no message" behavior is preserved.)

- [ ] **Step 5: Verify both sides**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Form1FlowCanvasReducedMotionTests" -p:SkipFlowCanvasBuild=true`
Expected: PASS — that class pins the pref-restore message behavior; if an assertion checks the exact message shape, update it to tolerate the added `iterationHistoryCap` field.

Run (from `FlowCanvas/`): `npx tsc --noEmit && npm test`
Expected: clean + PASS.

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/panels/SettingsPopover.tsx Models/AppConfiguration.cs UI/FlowCanvasForm.cs
git commit -m "feat(flow-canvas): persistable loop-history cap setting"
```

---

### Task 15: Playwright e2e — the full message-to-pixels path

**Files:**
- Test: `FlowCanvas/e2e/flow-canvas-iteration-stepper.spec.ts`

- [ ] **Step 1: Check the fixture shape**

Open `FlowCanvas/e2e/fixtures/graphs.ts` and confirm the node shape `loadGraphFixture` expects (node `type`, `data.blockType`, `data.props`). The spec below assumes the same shape the vitest graph helpers use (`type: 'flowBlock'`, props carrying `_stepPath`/`_isChildOf`/`_branchLabel`); adjust those literals to match the fixture file if they differ. Also mirror how existing specs enable branch bands if they are not on by default (check `layout-restore`'s `branchBandsEnabled` in `flow-canvas-loop-branch-instrumentation.spec.ts`'s beforeEach).

- [ ] **Step 2: Write the spec**

Create `FlowCanvas/e2e/flow-canvas-iteration-stepper.spec.ts`:

```typescript
import { expect, test, type Page } from '@playwright/test';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const frame = (loopId: string, i: number, label?: string) => ({ loopId, i, label });

const loopGraph = () => ({
  nodes: [
    {
      id: 'F', type: 'flowBlock', position: { x: 0, y: 0 },
      data: { blockType: 'foreach', label: 'For each host', props: { _stepPath: 'steps/0', foreach: 'host in hosts' } },
    },
    {
      id: 'A', type: 'flowBlock', position: { x: 220, y: 140 },
      data: { blockType: 'ssh', label: 'Check disk', props: { _stepPath: 'steps/0/do/0', _isChildOf: 'F', _branchLabel: 'do' } },
    },
    {
      id: 'B', type: 'flowBlock', position: { x: 220, y: 280 },
      data: { blockType: 'print', label: 'Report', props: { _stepPath: 'steps/0/do/1', _isChildOf: 'F', _branchLabel: 'do' } },
    },
  ],
  edges: [
    { id: 'eFA', source: 'F', target: 'A' },
    { id: 'eAB', source: 'A', target: 'B' },
  ],
});

/** 3 iterations: A runs in all three; B runs in 0 and 2 only; A errors in iteration 1. */
async function simulateRun(page: Page) {
  await postHostMessage(page, { type: 'execution-started' });
  for (let i = 0; i < 3; i++) {
    const stack = [frame('F', i, `host${i}`)];
    await postHostMessage(page, { type: 'execution-update', stepId: 'A', state: 'running', iterationStack: stack });
    const aState = i === 1 ? 'error' : 'success';
    await postHostMessage(page, { type: 'execution-update', stepId: 'A', state: aState, duration: 10 + i, iterationStack: stack });
    await postHostMessage(page, { type: 'step-output', stepId: 'A', output: `disk output ${i}`, iterationStack: stack });
    if (i !== 1) {
      await postHostMessage(page, { type: 'execution-update', stepId: 'B', state: 'running', iterationStack: stack });
      await postHostMessage(page, { type: 'execution-update', stepId: 'B', state: 'success', duration: 5, iterationStack: stack });
    }
  }
  await postHostMessage(page, { type: 'execution-update', stepId: 'F', state: 'success', duration: 60, iterationCount: 3 });
  await postHostMessage(page, { type: 'execution-finished' });
}

test.describe('Flow Canvas Iteration Stepper', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, loopGraph());
    await expect(page.locator('.react-flow__node[data-id="F"]')).toBeVisible();
    await simulateRun(page);
  });

  test('cluster appears post-run with the iteration count and steps through iterations', async ({ page }) => {
    const cluster = page.getByTestId('iteration-cluster');
    await expect(cluster).toHaveCount(1);
    await expect(page.getByTestId('iter-counter')).toHaveText('3');

    await page.getByTestId('iter-next').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('1/3');
    await expect(page.getByTestId('iter-label')).toHaveText('host0');
  });

  test('stepping re-scopes the neon path to the selected iteration', async ({ page }) => {
    // Aggregate: both loop-body edges lit (vertical paths are "hidden" to toBeVisible — use counts).
    await expect(page.locator('.fc-edge-onpath')).toHaveCount(2);

    // Iteration 2 (i=1): B never ran → only F→A stays lit.
    await page.getByTestId('iter-next').click();
    await page.getByTestId('iter-next').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('2/3');
    await expect(page.locator('.fc-edge-onpath')).toHaveCount(1);

    // Back to ALL restores the aggregate.
    await page.getByTestId('iter-all').click();
    await expect(page.locator('.fc-edge-onpath')).toHaveCount(2);
  });

  test('⚠ jumps to the failed iteration and the block shows its error state', async ({ page }) => {
    await expect(page.getByTestId('iter-fail')).toHaveText(/1/);
    await page.getByTestId('iter-fail').click();
    await expect(page.getByTestId('iter-counter')).toHaveText('2/3');
    await expect(page.getByTestId('iter-label')).toHaveText('host1');

    // A shows ERROR for this iteration; B (never reached) shows no chip.
    await expect(page.locator('.react-flow__node[data-id="A"]')).toContainText('ERROR');
  });

  test('Block Output follows the selected iteration', async ({ page }) => {
    await page.locator('.react-flow__node[data-id="A"]').click(); // selects node + Block tab
    await page.getByTestId('iter-next').click(); // iteration 1 → first output entry
    await expect(page.locator('text=(1/3)')).toBeVisible();
    await expect(page.locator('text=disk output 0')).toBeVisible();

    await page.getByTestId('iter-next').click();
    await expect(page.locator('text=(2/3)')).toBeVisible();
  });

  test('a new run clears the cluster', async ({ page }) => {
    await expect(page.getByTestId('iteration-cluster')).toHaveCount(1);
    await postHostMessage(page, { type: 'execution-started' });
    await expect(page.getByTestId('iteration-cluster')).toHaveCount(0);
  });
});
```

- [ ] **Step 3: Run the spec**

Run (from `FlowCanvas/`): `npx playwright test e2e/flow-canvas-iteration-stepper.spec.ts`
Expected: PASS (5 tests). Iterate on selector/fixture mismatches here — this spec is the end-to-end proof of the message contract from Task 6 through Task 13. Run headless (the config default); do not use the MCP browser.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-iteration-stepper.spec.ts
git commit -m "test(flow-canvas): e2e iteration stepper coverage"
```

---

### Task 16: Full verification pass

- [ ] **Step 1: C# suite**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj -p:SkipFlowCanvasBuild=true`
Expected: PASS. Known infra quirk: WinForms UI/dialog tests can flake under full parallel runs — if a UI-namespace test hangs, re-run that class in isolation before suspecting this feature.

- [ ] **Step 2: React suite + types**

Run (from `FlowCanvas/`): `npx tsc --noEmit && npm test`
Expected: clean + PASS.

- [ ] **Step 3: e2e**

Run (from `FlowCanvas/`): `npx playwright test e2e/flow-canvas-iteration-stepper.spec.ts e2e/flow-canvas-loop-branch-instrumentation.spec.ts e2e/flow-canvas-execution-path-highlight.spec.ts`
Expected: the new spec and loop-branch-instrumentation PASS. (~11 unrelated e2e failures pre-exist from a stale reduced-motion selector in other spec files — do not chase them; if the path-highlight spec fails, check whether it is one of those before investigating.)

- [ ] **Step 4: Full build (both stacks)**

Close SSH_Helper.exe if running (DLL-lock), then:
Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded — this also runs the Vite/tsc build via the BuildFlowCanvas target.

- [ ] **Step 5: Manual smoke (visual judgment the tests can't make)**

Run the app → Flow Canvas → load a preset with a foreach (e.g. from `ScriptSamples/`) → run it → confirm: cluster appears at the loop band's top-right; stepping re-lights the path; the foreach item value reads correctly; ALL restores; the cluster scales with zoom and rides along when the band is dragged.

- [ ] **Step 6: Final commit (if any fixups)**

```bash
git add -A
git commit -m "feat(flow-canvas): loop iteration stepper — final integration fixups"
```

---

## Out of scope (per spec)

Live mid-run browsing, variables-panel time-travel per iteration, and keyboard stepping are explicitly deferred. The recording model built here supports all three later.
