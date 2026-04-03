using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptExecutorStepPathTests
{
    [Fact]
    public async Task ExecuteAsync_AssignsCanonicalStepPaths_ForNestedScopes()
    {
        var executor = new ScriptExecutor();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Print = "top" },
                new()
                {
                    If = "1 == 1",
                    Then = new List<ScriptStep>
                    {
                        new() { Print = "then-0" },
                        new() { Print = "then-1" }
                    },
                    Elif = new List<ElifBranch>
                    {
                        new()
                        {
                            If = "0 == 1",
                            Then = new List<ScriptStep>
                            {
                                new() { Print = "elif-0" }
                            }
                        }
                    },
                    Else = new List<ScriptStep>
                    {
                        new() { Print = "else-0" }
                    }
                },
                new()
                {
                    While = "1 == 0",
                    Do = new List<ScriptStep>
                    {
                        new() { Print = "loop-0" }
                    }
                },
                new()
                {
                    Switch = "alpha",
                    Cases = new List<SwitchCase>
                    {
                        new()
                        {
                            Value = "alpha",
                            Do = new List<ScriptStep>
                            {
                                new() { Print = "case-0" }
                            }
                        }
                    }
                },
                new()
                {
                    Try = new List<ScriptStep>
                    {
                        new() { Print = "try-0" }
                    },
                    Catch = new List<ScriptStep>
                    {
                        new() { Print = "catch-0" }
                    },
                    Finally = new List<ScriptStep>
                    {
                        new() { Print = "finally-0" }
                    }
                },
                new()
                {
                    Parallel = new SSH_Helper.Services.Scripting.Models.ParallelOptions
                    {
                        Steps = new List<ScriptStep>
                        {
                            new() { Print = "parallel-0" },
                            new() { Print = "parallel-1" }
                        }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, new ScriptContext());

        result.Status.Should().Be(ScriptExitStatus.Success);

        script.Steps[0].StepPath.Should().Be("steps/0");
        script.Steps[1].StepPath.Should().Be("steps/1");
        script.Steps[1].Then![0].StepPath.Should().Be("steps/1/then/0");
        script.Steps[1].Then![1].StepPath.Should().Be("steps/1/then/1");
        script.Steps[1].Elif![0].Then[0].StepPath.Should().Be("steps/1/elif/0/then/0");
        script.Steps[1].Else![0].StepPath.Should().Be("steps/1/else/0");
        script.Steps[2].Do![0].StepPath.Should().Be("steps/2/do/0");
        script.Steps[3].Cases![0].Do[0].StepPath.Should().Be("steps/3/cases/0/do/0");
        script.Steps[4].Try![0].StepPath.Should().Be("steps/4/try/0");
        script.Steps[4].Catch![0].StepPath.Should().Be("steps/4/catch/0");
        script.Steps[4].Finally![0].StepPath.Should().Be("steps/4/finally/0");
        script.Steps[5].Parallel!.Steps[0].StepPath.Should().Be("steps/5/parallel/0/0");
        script.Steps[5].Parallel!.Steps[1].StepPath.Should().Be("steps/5/parallel/1/0");
    }

    [Fact]
    public async Task ExecuteAsync_EmitsStepLifecycleEvents_WithStepPath()
    {
        var executor = new ScriptExecutor();
        var stepPaths = new List<string>();
        executor.StepStarting += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.StepPath))
                stepPaths.Add(args.StepPath!);
        };

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Print = "first" },
                new()
                {
                    If = "1 == 1",
                    Then = new List<ScriptStep>
                    {
                        new() { Print = "nested" }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, new ScriptContext());

        result.Status.Should().Be(ScriptExitStatus.Success);
        stepPaths.Should().Contain("steps/0");
        stepPaths.Should().Contain("steps/1");
        stepPaths.Should().Contain("steps/1/then/0");
    }
}
