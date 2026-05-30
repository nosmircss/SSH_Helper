# Flow Canvas Wave 2b — Loop & Branch Instrumentation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Surface two runtime facts on the canvas — how many times a loop ran (`iterationCount`) and which branch a conditional took (`branchTaken`) — by plumbing them from the C# command handlers through the existing `execution-update` message into transient React store Maps and a single static node badge.

**Architecture:** Final/summary, additive. Loop/branch commands set `IterationCount`/`BranchTaken` on the `CommandResult` they return (the channel the executor already reads at `StepCompleted`, `ScriptExecutor.cs:346-356`); the executor copies them onto `StepExecutionEventArgs`; `Form1` adds two keys to the `execution-update` anonymous object; `messageBridge` parses them into two new `executionSlice` Maps (modeled on `blockTimings` — read at render, **never** written to `node.data`); `BaseBlock` renders a static tokenized chip. Export stays byte-identical because nothing touches `node.data.props` or the export path.

**Tech Stack:** C# / .NET 8 (xUnit + FluentAssertions tests); React 19 + Zustand + @xyflow/react; Playwright e2e; design tokens (OKLCH, no hex).

**Spec:** `docs/superpowers/specs/2026-05-30-flow-canvas-wave-2b-loop-branch-instrumentation-design.md`

---

## File Structure

**C# (engine + bridge):**
- `Services/Scripting/Commands/IScriptCommand.cs` — `CommandResult` gains `int? IterationCount` + `string? BranchTaken`.
- `Services/Scripting/ScriptExecutor.cs` — `StepExecutionEventArgs` gains the two props; the `StepCompleted` build site copies them from `result`.
- `Services/Scripting/Commands/{Foreach,While,Repeat}Command.cs` — set `IterationCount` on every return via an `executed` counter.
- `Services/Scripting/Commands/{If,Switch}Command.cs` — set `BranchTaken` (scope-key) on every return via a `branchTaken` local; `foreach` over elif/cases becomes an indexed `for`.
- `Form1.cs` — add `iterationCount`/`branchTaken` to the StepCompleted `execution-update` object.
- `SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs` — **new**; all C# tests, executor-level (run a `Script`, capture `StepCompleted` by `StepPath`).

**React (store + view):**
- `FlowCanvas/src/stores/slices/executionSlice.ts` — two transient Maps + setters + init + `clearExecution` reset.
- `FlowCanvas/src/stores/messageBridge.ts` — parse + guard the two fields.
- `FlowCanvas/src/communication-message-types.ts` — `ExecutionUpdateMessage` interface.
- `FlowCanvas/src/nodes/BaseBlock.tsx` — selectors + `deriveBranchLabel` + the static chip.
- `FlowCanvas/e2e/flow-canvas-loop-branch-instrumentation.spec.ts` — **new**; render spec.
- `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts` — thread the two fields onto the existing exec messages so the chip is scanned.

**Test strategy note (why executor-level for C#):** Every new C# test runs a real `Script` through `ScriptExecutor` and captures `StepCompleted` args keyed by `StepPath`. This is the project's proven idiom (`ScriptRepeatLoopTests`, `ScriptExecutorControlFlowTests`, `ScriptExecutorStepPathTests`) and it exercises the exact shipped pipeline — the command sets `result.X` **and** the executor copies it to the event arg the canvas consumes — in one assertion. A failing assertion after Task 1 (which adds the copy) therefore pins the command behavior.

---

## Task 1: C# field plumbing — CommandResult + StepExecutionEventArgs + executor copy

**Files:**
- Modify: `Services/Scripting/Commands/IScriptCommand.cs`
- Modify: `Services/Scripting/ScriptExecutor.cs:13-41` and `:346-356`

Structural prerequisite. Both fields default `null` everywhere until later tasks populate them, so there is no behavior to assert yet — the gate is a clean build.

- [ ] **Step 1: Add the two properties to `CommandResult`**

In `Services/Scripting/Commands/IScriptCommand.cs`, inside `class CommandResult`, after the `SuppressedError` property (line 67), add:

```csharp
        /// <summary>
        /// For loop commands (foreach/while/repeat): the number of times the loop body executed.
        /// Null for non-loop commands. Read by the executor onto StepExecutionEventArgs.
        /// </summary>
        public int? IterationCount { get; set; }

        /// <summary>
        /// For branch commands (if/switch): the scope-path key of the branch taken
        /// (then | else | elif/{i}/then | cases/{i}/do | default), matching the canvas
        /// edge.data.branchPath vocabulary. Null when no branch ran or for non-branch commands.
        /// </summary>
        public string? BranchTaken { get; set; }
```

- [ ] **Step 2: Add the two properties to `StepExecutionEventArgs`**

In `Services/Scripting/ScriptExecutor.cs`, inside `class StepExecutionEventArgs`, after the `Skipped` property (line 40), add:

```csharp

        /// <summary>Loop body execution count (only set on StepCompleted for foreach/while/repeat).</summary>
        public int? IterationCount { get; init; }

        /// <summary>Scope-path key of the branch taken (only set on StepCompleted for if/switch).</summary>
        public string? BranchTaken { get; init; }
```

- [ ] **Step 3: Copy them at the StepCompleted build site**

In `Services/Scripting/ScriptExecutor.cs`, the post-execution `StepCompleted` invocation (`:346-356`). Change:

```csharp
                    // Fire step-completed event
                    StepCompleted?.Invoke(this, new StepExecutionEventArgs
                    {
                        StepIndex = stepIndex,
                        StepPath = stepPath,
                        StepType = stepType,
                        LineNumber = step.LineNumber,
                        StepName = null,
                        Success = result.Success,
                        Output = context.LastCommandOutput,
                        DurationMs = sw.ElapsedMilliseconds
                    });
```

to (add the two trailing properties):

```csharp
                    // Fire step-completed event
                    StepCompleted?.Invoke(this, new StepExecutionEventArgs
                    {
                        StepIndex = stepIndex,
                        StepPath = stepPath,
                        StepType = stepType,
                        LineNumber = step.LineNumber,
                        StepName = null,
                        Success = result.Success,
                        Output = context.LastCommandOutput,
                        DurationMs = sw.ElapsedMilliseconds,
                        IterationCount = result.IterationCount,
                        BranchTaken = result.BranchTaken
                    });
```

(The two skipped-step `StepCompleted` sites at `:299-307` and `:318-326` are left unchanged — a skipped loop/branch correctly reports no count/branch.)

- [ ] **Step 4: Build to verify**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add Services/Scripting/Commands/IScriptCommand.cs Services/Scripting/ScriptExecutor.cs
git commit -m "feat(scripting): add IterationCount/BranchTaken to CommandResult + StepExecutionEventArgs"
```

---

## Task 2: Foreach iteration count

**Files:**
- Create: `SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs`
- Modify: `Services/Scripting/Commands/ForeachCommand.cs` (`IterateAsync:86-162`)

- [ ] **Step 1: Write the failing tests (new file with the shared harness)**

Create `SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs`:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class LoopBranchInstrumentationTests
{
    // Runs a script and returns the last StepCompleted event arg seen for each canonical StepPath.
    // Asserting via the event arg proves both the command set result.X AND the executor copied it
    // onto StepExecutionEventArgs (the exact data the Flow Canvas consumes).
    private static async Task<Dictionary<string, StepExecutionEventArgs>> RunAndCapture(
        Script script, ScriptContext? context = null)
    {
        var executor = new ScriptExecutor();
        var completed = new Dictionary<string, StepExecutionEventArgs>();
        executor.StepCompleted += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.StepPath))
                completed[e.StepPath!] = e;
        };
        await executor.ExecuteAsync(script, context ?? new ScriptContext());
        return completed;
    }

    [Fact]
    public async Task Foreach_ReportsBodyExecutionCount()
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
                    Do = new List<ScriptStep> { new() { Set = "last = x" } }
                }
            }
        };

        var completed = await RunAndCapture(script, context);

        completed["steps/0"].IterationCount.Should().Be(3);
    }

    [Fact]
    public async Task Foreach_EmptyCollection_ReportsZero()
    {
        var context = new ScriptContext();
        context.SetVariable("items", "[]");
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Foreach = "x in items",
                    Do = new List<ScriptStep> { new() { Set = "last = x" } }
                }
            }
        };

        var completed = await RunAndCapture(script, context);

        completed["steps/0"].IterationCount.Should().Be(0);
    }

    [Fact]
    public async Task Foreach_Break_ReportsExecutedCount()
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
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "last = x" },
                        new() { BreakLoop = true }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script, context);

        completed["steps/0"].IterationCount.Should().Be(1); // body ran once, then broke
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LoopBranchInstrumentationTests.Foreach"`
Expected: FAIL — `IterationCount` is `null` (ForeachCommand doesn't set it yet), so `.Should().Be(3)` fails.

- [ ] **Step 3: Implement the `executed` counter in `ForeachCommand.IterateAsync`**

In `Services/Scripting/Commands/ForeachCommand.cs`, replace the `try { for ... } ` body (lines 112-150) so the body-execution count is tracked and set on every return. The `var evaluator = new ExpressionEvaluator(context);` line stays; add `int executed = 0;` after it, and update the loop:

```csharp
            var evaluator = new ExpressionEvaluator(context);
            int executed = 0;

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

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LoopBranchInstrumentationTests.Foreach"`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs Services/Scripting/Commands/ForeachCommand.cs
git commit -m "feat(scripting): foreach reports body-execution IterationCount"
```

---

## Task 3: While + Repeat iteration count

**Files:**
- Modify: `SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs`
- Modify: `Services/Scripting/Commands/WhileCommand.cs`
- Modify: `Services/Scripting/Commands/RepeatCommand.cs`

- [ ] **Step 1: Add the failing tests**

In `LoopBranchInstrumentationTests.cs`, add inside the class:

```csharp
    [Fact]
    public async Task While_ReportsIterationCount()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                new()
                {
                    While = "n < 3",
                    Do = new List<ScriptStep> { new() { Set = "n = n + 1" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/1"].IterationCount.Should().Be(3);
    }

    [Fact]
    public async Task Repeat_ReportsIterationCount()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                new()
                {
                    Until = "n >= 3",
                    Do = new List<ScriptStep> { new() { Set = "n = n + 1" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/1"].IterationCount.Should().Be(3);
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LoopBranchInstrumentationTests.While|FullyQualifiedName~LoopBranchInstrumentationTests.Repeat"`
Expected: FAIL — `IterationCount` is `null`.

- [ ] **Step 3: Implement `WhileCommand`**

In `Services/Scripting/Commands/WhileCommand.cs`, add `int executed = 0;` next to `int iteration = 0;` (line 33) and set `IterationCount` on each early/error return + the final return. Replace the loop body's control-flow block and the final return:

```csharp
            int iteration = 0;
            int executed = 0;

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

            if (iteration >= maxIterations)
            {
                context.EmitOutput($"While: reached maximum iterations ({maxIterations}), stopping", ScriptOutputType.Warning);
            }

            return new CommandResult { Success = true, IterationCount = executed };
```

- [ ] **Step 4: Implement `RepeatCommand`**

In `Services/Scripting/Commands/RepeatCommand.cs`, add `int executed = 0;` next to `int iteration = 0;` (line 35) and set `IterationCount` on each error return + the final return:

```csharp
            int iteration = 0;
            int executed = 0;
            while (iteration < maxIterations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                context.SetVariable("_iteration", iteration);

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

            if (iteration >= maxIterations)
            {
                context.EmitOutput($"Repeat: reached maximum iterations ({maxIterations}), stopping", ScriptOutputType.Warning);
            }

            return new CommandResult { Success = true, IterationCount = executed };
```

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LoopBranchInstrumentationTests.While|FullyQualifiedName~LoopBranchInstrumentationTests.Repeat"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs Services/Scripting/Commands/WhileCommand.cs Services/Scripting/Commands/RepeatCommand.cs
git commit -m "feat(scripting): while/repeat report body-execution IterationCount"
```

---

## Task 4: If + Switch branch key

**Files:**
- Modify: `SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs`
- Modify: `Services/Scripting/Commands/IfCommand.cs`
- Modify: `Services/Scripting/Commands/SwitchCommand.cs`

- [ ] **Step 1: Add the failing tests**

In `LoopBranchInstrumentationTests.cs`, add inside the class:

```csharp
    [Fact]
    public async Task If_Then_ReportsThen()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 1",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } },
                    Else = new List<ScriptStep> { new() { Set = "x = 2" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("then");
    }

    [Fact]
    public async Task If_Else_ReportsElse()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 0",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } },
                    Else = new List<ScriptStep> { new() { Set = "x = 2" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("else");
    }

    [Fact]
    public async Task If_Elif_ReportsElifKey()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 0",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } },
                    Elif = new List<ElifBranch>
                    {
                        new() { If = "1 == 1", Then = new List<ScriptStep> { new() { Set = "x = 3" } } }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("elif/0/then");
    }

    [Fact]
    public async Task If_NoBranch_ReportsNull()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    If = "1 == 0",
                    Then = new List<ScriptStep> { new() { Set = "x = 1" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().BeNull();
    }

    [Fact]
    public async Task Switch_Case_ReportsCaseKey()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Switch = "alpha",
                    Cases = new List<SwitchCase>
                    {
                        new() { Value = "beta", Do = new List<ScriptStep> { new() { Set = "x = 1" } } },
                        new() { Value = "alpha", Do = new List<ScriptStep> { new() { Set = "x = 2" } } }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("cases/1/do");
    }

    [Fact]
    public async Task Switch_Default_ReportsDefault()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Switch = "zzz",
                    Cases = new List<SwitchCase>
                    {
                        new() { Value = "alpha", Do = new List<ScriptStep> { new() { Set = "x = 1" } } }
                    },
                    Else = new List<ScriptStep> { new() { Set = "x = 9" } }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().Be("default");
    }

    [Fact]
    public async Task Switch_NoMatch_ReportsNull()
    {
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Switch = "zzz",
                    Cases = new List<SwitchCase>
                    {
                        new() { Value = "alpha", Do = new List<ScriptStep> { new() { Set = "x = 1" } } }
                    }
                }
            }
        };

        var completed = await RunAndCapture(script);

        completed["steps/0"].BranchTaken.Should().BeNull();
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LoopBranchInstrumentationTests.If|FullyQualifiedName~LoopBranchInstrumentationTests.Switch"`
Expected: FAIL — `BranchTaken` is `null` for the then/elif/else/case/default tests (the null tests pass coincidentally; the rest fail).

- [ ] **Step 3: Implement `IfCommand`**

In `Services/Scripting/Commands/IfCommand.cs`, replace the body from the condition evaluation (line 28) to the final return (line 76) with a `branchTaken` local set at each chosen branch and applied to every return. Note the elif `foreach` becomes an indexed `for` to get the elif index:

```csharp
            // Evaluate the condition
            var evaluator = new ExpressionEvaluator(context);
            var result = evaluator.Evaluate(condition);

            context.EmitOutput($"If '{condition}' => {result}", ScriptOutputType.Debug);

            string? branchTaken = null;

            if (result)
            {
                branchTaken = "then";
                // Execute 'then' block
                if (step.Then != null && step.Then.Count > 0)
                {
                    var thenResult = await _executor.ExecuteStepsAsync(step.Then, context, cancellationToken, context.LoopDepth);
                    if (thenResult.IsControlFlow || !thenResult.Success)
                    {
                        thenResult.BranchTaken = branchTaken;
                        return thenResult;
                    }
                }
            }
            else
            {
                if (step.Elif != null)
                {
                    for (int i = 0; i < step.Elif.Count; i++)
                    {
                        var elif = step.Elif[i];
                        if (string.IsNullOrWhiteSpace(elif.If))
                            continue;

                        var elifResult = evaluator.Evaluate(elif.If);
                        context.EmitOutput($"Elif '{elif.If}' => {elifResult}", ScriptOutputType.Debug);
                        if (!elifResult)
                            continue;

                        branchTaken = $"elif/{i}/then";

                        if (elif.Then.Count > 0)
                        {
                            var branchResult = await _executor.ExecuteStepsAsync(elif.Then, context, cancellationToken, context.LoopDepth);
                            if (branchResult.IsControlFlow || !branchResult.Success)
                            {
                                branchResult.BranchTaken = branchTaken;
                                return branchResult;
                            }
                        }

                        return new CommandResult { Success = true, BranchTaken = branchTaken };
                    }
                }

                // Execute 'else' block
                if (step.Else != null && step.Else.Count > 0)
                {
                    branchTaken = "else";
                    var elseResult = await _executor.ExecuteStepsAsync(step.Else, context, cancellationToken, context.LoopDepth);
                    if (elseResult.IsControlFlow || !elseResult.Success)
                    {
                        elseResult.BranchTaken = branchTaken;
                        return elseResult;
                    }
                }
            }

            return new CommandResult { Success = true, BranchTaken = branchTaken };
```

- [ ] **Step 4: Implement `SwitchCommand`**

In `Services/Scripting/Commands/SwitchCommand.cs`, replace from the `if (step.Cases != null)` block (line 28) through the final return (line 77) with a `branchTaken` local; the cases `foreach` becomes an indexed `for`:

```csharp
            string? branchTaken = null;

            if (step.Cases != null)
            {
                for (int i = 0; i < step.Cases.Count; i++)
                {
                    var switchCase = step.Cases[i];
                    var caseValue = context.SubstituteVariables(switchCase.Value).Trim();

                    bool matches;
                    if (caseValue.StartsWith("matches ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Regex match: "matches pattern"
                        var pattern = caseValue.Substring(8).Trim();
                        try
                        {
                            matches = System.Text.RegularExpressions.Regex.IsMatch(
                                resolvedValue, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        catch
                        {
                            matches = false;
                        }
                    }
                    else
                    {
                        // Case-insensitive equality
                        matches = string.Equals(resolvedValue, caseValue, StringComparison.OrdinalIgnoreCase);
                    }

                    if (matches)
                    {
                        context.EmitOutput($"Switch matched case '{switchCase.Value}'", ScriptOutputType.Debug);
                        branchTaken = $"cases/{i}/do";
                        if (switchCase.Do != null && switchCase.Do.Count > 0)
                        {
                            var result = await _executor.ExecuteStepsAsync(switchCase.Do, context, cancellationToken, context.LoopDepth);
                            result.BranchTaken = branchTaken;
                            return result;
                        }
                        return new CommandResult { Success = true, BranchTaken = branchTaken };
                    }
                }
            }

            // No match - execute default (stored in Else)
            if (step.Else != null && step.Else.Count > 0)
            {
                context.EmitOutput("Switch using default branch", ScriptOutputType.Debug);
                branchTaken = "default";
                var defaultResult = await _executor.ExecuteStepsAsync(step.Else, context, cancellationToken, context.LoopDepth);
                defaultResult.BranchTaken = branchTaken;
                return defaultResult;
            }

            context.EmitOutput("Switch: no matching case and no default", ScriptOutputType.Debug);
            return new CommandResult { Success = true, BranchTaken = branchTaken };
```

- [ ] **Step 5: Run to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~LoopBranchInstrumentationTests.If|FullyQualifiedName~LoopBranchInstrumentationTests.Switch"`
Expected: PASS (7 tests).

- [ ] **Step 6: Run the whole new fixture + the existing scripting suite (no regressions)**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Scripting"`
Expected: PASS (the new fixture + all existing scripting tests — the `for`-loop refactors and `executed` counters must not change control-flow behavior).

- [ ] **Step 7: Commit**

```bash
git add SSH_Helper.Tests/Scripting/LoopBranchInstrumentationTests.cs Services/Scripting/Commands/IfCommand.cs Services/Scripting/Commands/SwitchCommand.cs
git commit -m "feat(scripting): if/switch report the taken branch scope-key"
```

---

## Task 5: Form1 — add the two fields to the execution-update message

**Files:**
- Modify: `Form1.cs:13573-13580` (the `SshService_StepCompleted` handler)

No automated test — this is thin UI glue. The executor-event tests (Tasks 2-4) already prove the data reaches `StepExecutionEventArgs`; this step forwards it to the canvas, verified by build + the manual GUI smoke in Task 9/closing.

- [ ] **Step 1: Add the two initializer keys**

In `Form1.cs`, the StepCompleted `execution-update` message. Change:

```csharp
            _flowCanvasForm.SendMessage(new
            {
                type = "execution-update",
                stepId = nodeId,
                state = e.Skipped ? "skipped" : (e.Success == true ? "success" : "error"),
                duration = e.DurationMs,
                variables
            });
```

to:

```csharp
            _flowCanvasForm.SendMessage(new
            {
                type = "execution-update",
                stepId = nodeId,
                state = e.Skipped ? "skipped" : (e.Success == true ? "success" : "error"),
                duration = e.DurationMs,
                iterationCount = e.IterationCount,
                branchTaken = e.BranchTaken,
                variables
            });
```

(The StepStarting handler at `:13547-13552` is intentionally **not** changed — these are completion-only summary facts.)

- [ ] **Step 2: Build to verify**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Form1.cs
git commit -m "feat(flow-canvas): forward iterationCount/branchTaken on the execution-update message"
```

---

## Task 6: React store — executionSlice transient Maps

**Files:**
- Modify: `FlowCanvas/src/stores/slices/executionSlice.ts`

Build-only (no unit-test runner in this project; the Maps are proven end-to-end by the render spec in Task 8). Modeled exactly on `blockTimings` — pure store Maps, **never** written to `node.data`.

- [ ] **Step 1: Add the Maps + setters to the `ExecutionSlice` interface**

In `executionSlice.ts`, in the `ExecutionSlice` interface, after the `blockTimings` line (line 24) add:

```typescript
  loopIterations: Map<string, number>;
  branchTaken: Map<string, string>;
```

and after the `setBlockTiming` declaration (line 30) add:

```typescript
  setLoopIteration: (id: string, iteration: number) => void;
  setBranchTaken: (id: string, key: string) => void;
```

- [ ] **Step 2: Initialize the Maps**

In `createExecutionSlice`, after `blockTimings: new Map(),` (line 41) add:

```typescript
  loopIterations: new Map(),
  branchTaken: new Map(),
```

- [ ] **Step 3: Implement the setters**

After the `setBlockTiming` setter (closes at line 76) add:

```typescript
  setLoopIteration: (id, iteration) => {
    set((s) => {
      const next = new Map(s.loopIterations);
      next.set(id, iteration);
      return { loopIterations: next };
    });
  },

  setBranchTaken: (id, key) => {
    set((s) => {
      const next = new Map(s.branchTaken);
      next.set(id, key);
      return { branchTaken: next };
    });
  },
```

- [ ] **Step 4: Reset them in `clearExecution`**

In `clearExecution`, inside the first `set({ ... })`, after `blockTimings: new Map(),` (line 82) add:

```typescript
      loopIterations: new Map(),
      branchTaken: new Map(),
```

- [ ] **Step 5: Build to verify**

Run: `cd FlowCanvas && npm run build`
Expected: build succeeds (TypeScript compiles; the new slice members satisfy `ExecutionSlice`).

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/stores/slices/executionSlice.ts
git commit -m "feat(flow-canvas): transient loopIterations/branchTaken store Maps"
```

---

## Task 7: React bridge — parse the two fields; type the message

**Files:**
- Modify: `FlowCanvas/src/communication-message-types.ts`
- Modify: `FlowCanvas/src/stores/messageBridge.ts`

- [ ] **Step 1: Add the `ExecutionUpdateMessage` interface**

In `FlowCanvas/src/communication-message-types.ts`, after the type aliases (line 49) append:

```typescript

/** Shape of an 'execution-update' host message (fields are validated loosely at parse time). */
export interface ExecutionUpdateMessage {
  type: 'execution-update';
  stepId: string | number;
  state: string;
  duration?: number | null;
  variables?: Record<string, unknown>;
  changedKeys?: string[];
  /** Loop body-execution count (foreach/while/repeat); number or null. */
  iterationCount?: number | null;
  /** Taken branch scope-key (if/switch), e.g. 'else', 'cases/2/do', 'elif/0/then'. */
  branchTaken?: string | null;
}
```

- [ ] **Step 2: Parse + guard the two fields in the execution-update handler**

In `FlowCanvas/src/stores/messageBridge.ts`, inside the `executionUpdate` handler, after the variables block (the `if (msg.variables && ...) { ... }` that closes at line 237) and before the handler's closing `}),` add:

```typescript

      // Loop & branch instrumentation (final/summary; arrives with the completion message).
      // Stored in transient executionSlice Maps — never written onto node.data, so export is unaffected.
      if (msg.iterationCount != null) {
        const n = Number(msg.iterationCount);
        if (Number.isFinite(n) && n >= 0) {
          state.setLoopIteration(stepId, n);
        }
      }
      if (typeof msg.branchTaken === 'string' && msg.branchTaken.trim().length > 0) {
        state.setBranchTaken(stepId, msg.branchTaken.trim());
      }
```

- [ ] **Step 3: Build to verify**

Run: `cd FlowCanvas && npm run build`
Expected: build succeeds. (`msg.iterationCount`/`msg.branchTaken` read the same loose way the existing handler reads `msg.duration`/`msg.variables`.)

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/communication-message-types.ts FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): parse iterationCount/branchTaken from execution-update"
```

---

## Task 8: BaseBlock — static instrumentation chip (+ render spec)

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx`
- Create: `FlowCanvas/e2e/flow-canvas-loop-branch-instrumentation.spec.ts`

TDD across the React slice: write the render spec first (it fails — no chip), then add the selectors + helper + chip so it passes. The store wiring (Tasks 6-7) is already in place, so a green result proves message → store → DOM end-to-end.

- [ ] **Step 1: Write the failing render spec**

Create `FlowCanvas/e2e/flow-canvas-loop-branch-instrumentation.spec.ts`:

```typescript
import { expect, test, type Locator, type Page } from '@playwright/test';
import { createInteractionFixture } from './fixtures/graphs';
import {
  clearOutgoingMessages,
  installHostMessageCapture,
  loadGraphFixture,
  postHostMessage,
  waitForOutgoingMessage,
} from './support/harness';

const nodeById = (page: Page, id: string): Locator => page.locator(`.react-flow__node[data-id="${id}"]`);
const motionButton = (page: Page): Locator => page.locator('button[title*="motion" i]').first();
const hasReducedMotion = (page: Page) =>
  page.evaluate(() => document.body.classList.contains('fc-reduced-motion'));

test.describe('Flow Canvas Loop & Branch Instrumentation', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
    await loadGraphFixture(page, createInteractionFixture());
    await expect(nodeById(page, 'node-1')).toBeVisible();
  });

  test('loop node shows the ×N iteration badge on completion', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 100, iterationCount: 5 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×5');
  });

  test('iterationCount 0 shows ×0', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: 0 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×0');
  });

  test('no loop badge when iterationCount absent', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveCount(0);
  });

  test('branch node shows the derived label (else / case #3 / elif #1)', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 10, branchTaken: 'else' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveText('else');

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 10, branchTaken: 'cases/2/do' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveText('case #3');

    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 10, branchTaken: 'elif/0/then' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveText('elif #1');
  });

  test('malformed instrumentation is ignored (no badge)', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: -1, branchTaken: '   ' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveCount(0);
    await expect(nodeById(page, 'node-1').getByTestId('exec-branch-badge')).toHaveCount(0);
  });

  test('execution-started (re-run) clears the badge', async ({ page }) => {
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: 3 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×3');
    await postHostMessage(page, { type: 'execution-started' });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveCount(0);
  });

  test('badge renders identically under reduced motion (no motion added)', async ({ page }) => {
    await motionButton(page).click();
    await expect.poll(() => hasReducedMotion(page)).toBe(true);
    await postHostMessage(page, { type: 'execution-update', stepId: 'node-1', state: 'success', duration: 5, iterationCount: 4 });
    await expect(nodeById(page, 'node-1').getByTestId('exec-loop-badge')).toHaveText('×4');
  });
});
```

- [ ] **Step 2: Run the spec to verify it fails**

Run: `cd FlowCanvas && npx playwright test e2e/flow-canvas-loop-branch-instrumentation.spec.ts`
Expected: FAIL — the `exec-loop-badge` / `exec-branch-badge` test ids do not exist yet.

- [ ] **Step 3: Add the `deriveBranchLabel` helper (module scope)**

In `FlowCanvas/src/nodes/BaseBlock.tsx`, after the `formatDuration` function (line 36) add:

```typescript
// Maps a runtime branch scope-key (matching edge.data.branchPath) to a short badge label.
function deriveBranchLabel(key: string): string {
  if (key === 'then' || key === 'else' || key === 'default') return key;
  const elif = key.match(/^elif\/(\d+)/);
  if (elif) return `elif #${Number(elif[1]) + 1}`;
  const c = key.match(/^cases\/(\d+)/);
  if (c) return `case #${Number(c[1]) + 1}`;
  return key;
}
```

- [ ] **Step 4: Add the selectors**

In `BaseBlock`, after the `maxDuration` selector (closes at line 79) add:

```typescript
  const loopIteration = useFlowStore((s) => s.loopIterations.get(id));
  const branchTakenKey = useFlowStore((s) => s.branchTaken.get(id));
```

- [ ] **Step 5: Render the chip inside the exec indicator**

In `BaseBlock.tsx`, inside the `execIndicator` JSX, after the duration-badge block (the `{badgeText && ( ... )}` that closes at line 239) and before the closing `</span>` (line 240), add:

```tsx
      {loopIteration != null && (
        <span data-testid="exec-loop-badge" style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          ×{loopIteration}
        </span>
      )}
      {branchTakenKey && (
        <span data-testid="exec-branch-badge" style={{
          fontSize: 8,
          color: 'var(--fc-text-secondary)',
          background: 'var(--fc-surface-0)',
          padding: '1px 4px',
          borderRadius: 3,
          marginLeft: 2,
        }}>
          {deriveBranchLabel(branchTakenKey)}
        </span>
      )}
```

- [ ] **Step 6: Build, then run the spec to verify it passes**

Run: `cd FlowCanvas && npm run build && npx playwright test e2e/flow-canvas-loop-branch-instrumentation.spec.ts`
Expected: build succeeds; all 7 render-spec tests PASS.

- [ ] **Step 7: Commit**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx FlowCanvas/e2e/flow-canvas-loop-branch-instrumentation.spec.ts
git commit -m "feat(flow-canvas): static loop/branch instrumentation badge on BaseBlock"
```

---

## Task 9: Token-sweep coverage + full verification gate

**Files:**
- Modify: `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts`

Extend the no-hex gate so the chip's styles are scanned, then run every gate the spec lists.

- [ ] **Step 1: Thread the instrumentation onto the token-sweep's exec messages**

In `FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts`, the block that drives exec states (around lines 196-199). Change:

```typescript
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-run', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'success', duration: 300 });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-err', state: 'error' });
```

to (add the loop badge to the success node and the branch badge to the error node so both chips are present in the scanned DOM):

```typescript
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-run', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'running' });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-ok', state: 'success', duration: 300, iterationCount: 4 });
    await postHostMessage(page, { type: 'execution-update', stepId: 'exec-err', state: 'error', branchTaken: 'else' });
```

- [ ] **Step 2: Run the token-sweep gate**

Run: `cd FlowCanvas && npx playwright test e2e/flow-canvas-token-sweep.spec.ts`
Expected: PASS — the chip uses only `var(--fc-*)` tokens, so the no-hex / no-`var()`-alpha-concat scan stays green with the chips rendered.

- [ ] **Step 3: Run the parity proof (the round-trip gate) under --workers=1**

Run: `cd FlowCanvas && npm run test:e2e:parity`
Expected: PASS, 22/22 (export byte-identical — nothing touched `node.data.props` or the export path).

- [ ] **Step 4: Run the reduced-motion + cinematics specs (no regression)**

Run: `cd FlowCanvas && npx playwright test e2e/flow-canvas-reduced-motion.spec.ts e2e/flow-canvas-execution-cinematics.spec.ts`
Expected: PASS (the chip adds no animation; the exec indicator still renders RUNNING/DONE/✗ + duration badge alongside the new chip).

- [ ] **Step 5: Run the dist gate**

Run: `cd FlowCanvas && npm run test:e2e:dist`
Expected: PASS (the chip survives the production single-asset bundle).

- [ ] **Step 6: Full C# build + test (re-embeds dist)**

Run: `dotnet build SSH_Helper.sln && dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
Expected: Build 0 errors; all tests pass.

- [ ] **Step 7: Dead-code sweep (additive cycle — confirm no orphans)**

Run a grep for any unused selector/import introduced. Confirm `loopIterations`/`branchTaken`/`setLoopIteration`/`setBranchTaken`/`deriveBranchLabel`/`ExecutionUpdateMessage` each have a producer and a consumer; confirm no `node.data` write was added for the new fields.

- [ ] **Step 8: Commit**

```bash
git add FlowCanvas/e2e/flow-canvas-token-sweep.spec.ts
git commit -m "test(flow-canvas): token-sweep covers the loop/branch instrumentation chip"
```

---

## Self-Review

**1. Spec coverage** (each spec section → task):
- C# data layer (`CommandResult`/`StepExecutionEventArgs` + populate + copy): Tasks 1-4. ✓
- Message protocol (two additive `execution-update` fields): Task 5. ✓
- React store (transient Maps + setters + init + reset + parse + type): Tasks 6-7. ✓
- Minimal visual readout (static chip, derived label): Task 8. ✓
- Tests (C# executor-event, render spec, token-sweep, parity, reduced-motion, dist, dotnet): Tasks 2-4, 8, 9. ✓
- Round-trip safety (parity 22/22 under --workers=1; nothing on node.data): Task 9 Step 3 + Step 7. ✓
- Reduced-motion (no new motion; badge identical): Task 8 spec + Task 9 Step 4. ✓
- Deferred items (live ticking, try/catch, edge highlight, animation, parallel) — correctly absent from every task. ✓

**2. Placeholder scan:** No TBD/TODO; every code step shows complete code; every run step shows the command and expected result. ✓

**3. Type/name consistency:** `IterationCount` (`int?`) and `BranchTaken` (`string?`) used identically across `CommandResult`, `StepExecutionEventArgs`, and the copy site. Message keys `iterationCount`/`branchTaken` match the React parse. Store members `loopIterations`/`branchTaken` + setters `setLoopIteration`/`setBranchTaken` match between the slice (Task 6), the bridge calls (Task 7), and the selectors (Task 8). The BaseBlock local is `branchTakenKey` (no shadow of the slice's `branchTaken` Map). Test ids `exec-loop-badge`/`exec-branch-badge` match between Task 8's JSX and both spec files. Branch keys `then`/`else`/`elif/{i}/then`/`cases/{i}/do`/`default` match between the C# commands (Task 4) and `deriveBranchLabel` (Task 8). ✓

---

## Notes / known infra

- **VBCSCompiler/Defender parity-CLI parallel build-lock race** (not a regression): full parallel `npm run test:e2e`/`:dist` can show transient parity-CLI failures from a shared `obj/SSH_Helper.dll` lock; the parity gate is `--workers=1` for exactly this reason — prove green there.
- **Iteration-count convention:** `IterationCount` = number of times the loop **body** executed (a `when`-filtered foreach item does not count; `break` counts the breaking pass since its body ran). The tests pin this.
- **Manual GUI smoke (closing step, outside the task list):** run the desktop app, execute a preset containing a `foreach` and an `if`/`switch`, and confirm the loop node shows `×N` and the branch node shows the taken-branch label after the run, with no change to exported YAML.
