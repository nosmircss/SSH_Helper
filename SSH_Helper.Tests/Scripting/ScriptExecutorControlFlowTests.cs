using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
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
    public async Task ExecuteAsync_TryCatchFinally_SendFailOnNonZero_PreservesLastErrorAcrossCatch()
    {
        var executor = new ScriptExecutor();
        ReplaceCommand(
            executor,
            StepType.Send,
            new SendCommand(_ => new FakeSendSession(command =>
                $"{command}\r\nzsh: command not found: definitely_not_a_command_qa_try_12345\r\n{SendCommand.ExitStatusSentinel}:127\r\ntester$")));

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
                            Send = "definitely_not_a_command_qa_try_12345",
                            FailOnNonZero = true
                        }
                    },
                    Catch = new List<ScriptStep>
                    {
                        new() { Set = "caught = true" },
                        new() { Set = "caught_message = _last_error" }
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
        context.GetVariableString("caught").Should().BeOneOf("true", "True");
        context.GetVariableString("caught_message").Should().Be("Command exited with status 127");
        context.GetVariableString("finalized").Should().Be("yes");
        context.HasVariable("_last_error").Should().BeFalse();
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

    [Fact]
    public async Task ExecuteAsync_RetryCancelled_RestoresOriginalOnError()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var retryStep = new ScriptStep
        {
            Wait = 5,
            Retry = 2,
            OnError = "continue"
        };
        var script = new Script
        {
            Steps = new List<ScriptStep> { retryStep }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var result = await executor.ExecuteAsync(script, context, cts.Token);

        result.Status.Should().Be(ScriptExitStatus.Cancelled);
        retryStep.OnError.Should().Be("continue");
    }

    [Fact]
    public async Task ExecuteAsync_ReadfilePickerCancel_StopsScriptImmediately()
    {
        var executor = new ScriptExecutor();
        ReplaceCommand(executor, StepType.Readfile, new ReadFileCommand((_, _) => Task.FromResult<string?>(null)));

        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new()
                {
                    OnError = "continue",
                    Readfile = new ReadfileOptions
                    {
                        SelectFile = true,
                        Into = "entries"
                    }
                },
                new() { Set = "after = done" }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Cancelled);
        context.GetVariable("entries").Should().BeEquivalentTo(new List<string>());
        context.HasVariable("after").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_CollectionHelpers_SimplifyMembershipAndUniquenessFlow()
    {
        var parser = new ScriptParser();
        var yaml = @"---
vars:
  exclude_service_matches:
    - Cloudflare-CDN
    - Amazon-AWS
    - Cloudflare-Web
steps:
  - set:
      expression: exclude_service_matches_norm = lower_all(trim_all(distinct(exclude_service_matches)))
  - set:
      expression: matched_services = list(""Cloudflare-CDN"", ""Contoso-Hosting"", ""contoso-hosting"", """")
  - set:
      expression: unique_services = list()
  - foreach:
      iterator: svc in compact(matched_services)
      do:
        - set:
            expression: svc_key = lower(trim(svc))
        - if:
            condition: svc_key not in exclude_service_matches_norm
            then:
              - set:
                  expression: unique_services = push_unique(unique_services, svc)
  - set:
      expression: unique_count = length(unique_services)";

        var script = parser.Parse(yaml);
        parser.Validate(script, yaml, enforceCanonicalSyntax: true).Should().BeEmpty();

        var executor = new ScriptExecutor();
        var context = new ScriptContext();

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariable("exclude_service_matches_norm").Should().BeEquivalentTo(
            new List<string> { "cloudflare-cdn", "amazon-aws", "cloudflare-web" },
            options => options.WithStrictOrdering());
        context.GetVariable("unique_services").Should().BeEquivalentTo(
            new List<string> { "Contoso-Hosting" },
            options => options.WithStrictOrdering());
        context.GetVariable("unique_count").Should().Be(1);
    }

    private static void ReplaceCommand(ScriptExecutor executor, StepType stepType, IScriptCommand command)
    {
        var field = typeof(ScriptExecutor).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();

        var commands = field!.GetValue(executor).Should().BeAssignableTo<Dictionary<StepType, IScriptCommand>>().Subject;
        commands[stepType] = command;
    }

    private sealed class FakeSendSession : SendCommand.ISendCommandSession
    {
        private readonly System.Func<string, string> _execute;

        public FakeSendSession(System.Func<string, string> execute)
        {
            _execute = execute;
        }

        public string? CurrentPrompt => "tester$";

        public Task<string> ExecuteAsync(
            string command,
            string? expectPattern,
            int? timeoutSeconds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_execute(command));
        }

        public Task<string> ExecuteWithRespondsAsync(
            string command,
            IReadOnlyList<(string expectPattern, string reply)> responds,
            int? timeoutSeconds,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_execute(command));
        }
    }
}
