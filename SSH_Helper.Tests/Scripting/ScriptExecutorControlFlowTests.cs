using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptExecutorControlFlowTests
{
    [Fact]
    public async Task ExecuteAsync_OnErrorContinue_SetsAndClearsLastError()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Readfile = new ReadfileOptions
                    {
                        Path = @"C:\\definitely-missing\\file.txt",
                        Into = "content"
                    },
                    OnError = "continue"
                },
                new()
                {
                    If = "_last_error is not empty",
                    Then = new List<ScriptStep>
                    {
                        new() { Set = "error_seen = yes" }
                    }
                },
                new() { Set = "after = done" }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariableString("error_seen").Should().Be("yes");
        context.GetVariableString("after").Should().Be("done");
        context.HasVariable("_last_error").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_BreakAndContinue_ControlWhileFlow()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "i = 0" },
                new() { Set = "output = ''" },
                new()
                {
                    While = "i < 5",
                    MaxIterations = 20,
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "i = i + 1" },
                        new()
                        {
                            If = "i == 2",
                            Then = new List<ScriptStep> { new() { ContinueLoop = true } }
                        },
                        new()
                        {
                            If = "i == 4",
                            Then = new List<ScriptStep> { new() { BreakLoop = true } }
                        },
                        new() { Set = "output = \"${output}${i}\"" }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariableString("output").Should().Be("13");
        context.GetVariable("i").Should().Be(4d);
    }

    [Fact]
    public async Task ExecuteAsync_TryCatchFinally_ExecutesCatchAndFinally()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    Try = new List<ScriptStep>
                    {
                        new()
                        {
                            Readfile = new ReadfileOptions
                            {
                                Path = @"C:\\definitely-missing\\file.txt",
                                Into = "content"
                            }
                        }
                    },
                    Catch = new List<ScriptStep>
                    {
                        new() { Set = "caught = _last_error" }
                    },
                    Finally = new List<ScriptStep>
                    {
                        new() { Set = "finalized = yes" }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariableString("caught").Should().NotBeEmpty();
        context.GetVariableString("finalized").Should().Be("yes");
    }

    [Fact]
    public async Task ExecuteAsync_WhileHonorsPerStepMaxIterations()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "i = 0" },
                new()
                {
                    While = "i < 100",
                    MaxIterations = 3,
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "i = i + 1" }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariable("i").Should().Be(3d);
    }

    [Fact]
    public async Task ExecuteAsync_IfConditionWithSpacesInVariable_ParsesCorrectly()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "name = \"John Doe\"" },
                new()
                {
                    If = "${name} == \"John Doe\"",
                    Then = new List<ScriptStep>
                    {
                        new() { Set = "matched = yes" }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariableString("matched").Should().Be("yes");
    }

    [Fact]
    public async Task ExecuteAsync_ParallelPropagatesBreak_OutOfWhileLoop()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "loop = 0" },
                new()
                {
                    While = "loop < 3",
                    MaxIterations = 10,
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "loop = loop + 1" },
                        new()
                        {
                            Parallel = new SSH_Helper.Services.Scripting.Models.ParallelOptions
                            {
                                Steps = new List<ScriptStep>
                                {
                                    new() { BreakLoop = true }
                                }
                            }
                        },
                        new() { Set = "after_inner = should_not_be_set" }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariable("loop").Should().Be(1d);
        context.HasVariable("after_inner").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ParallelPropagatesContinue_SkipsRemainingWhileBody()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "i = 0" },
                new() { Set = "marks = ''" },
                new()
                {
                    While = "i < 3",
                    MaxIterations = 10,
                    Do = new List<ScriptStep>
                    {
                        new() { Set = "i = i + 1" },
                        new()
                        {
                            Parallel = new SSH_Helper.Services.Scripting.Models.ParallelOptions
                            {
                                Steps = new List<ScriptStep>
                                {
                                    new() { ContinueLoop = true }
                                }
                            }
                        },
                        new() { Set = "marks = \"${marks}x\"" }
                    }
                }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariable("i").Should().Be(3d);
        context.GetVariableString("marks").Should().BeEmpty();
    }
}
