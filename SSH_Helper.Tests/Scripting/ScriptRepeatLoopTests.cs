using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptRepeatLoopTests
{
    private static ScriptStep Repeat(string until, int? maxIterations, params ScriptStep[] body)
        => new()
        {
            Until = until,
            MaxIterations = maxIterations,
            Do = new List<ScriptStep>(body)
        };

    // --- runtime semantics (bottom-tested do-while) ---

    [Fact]
    public async Task Repeat_BodyRunsOnce_WhenUntilAlreadyTrue()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                Repeat("1 == 1", null, new ScriptStep { Set = "n = n + 1" })
            }
        };

        await executor.ExecuteAsync(script, context);

        context.GetVariableString("n").Should().Be("1");
    }

    [Fact]
    public async Task Repeat_RepeatsUntilConditionTrue()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                Repeat("n >= 3", null, new ScriptStep { Set = "n = n + 1" })
            }
        };

        await executor.ExecuteAsync(script, context);

        context.GetVariableString("n").Should().Be("3");
    }

    [Fact]
    public async Task Repeat_BreakExitsImmediately()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                Repeat("n >= 100", null,
                    new ScriptStep { Set = "n = n + 1" },
                    new ScriptStep { BreakLoop = true })
            }
        };

        await executor.ExecuteAsync(script, context);

        context.GetVariableString("n").Should().Be("1");
    }

    [Fact]
    public async Task Repeat_RespectsMaxIterations()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Set = "n = 0" },
                Repeat("1 == 2", 5, new ScriptStep { Set = "n = n + 1" })
            }
        };

        await executor.ExecuteAsync(script, context);

        context.GetVariableString("n").Should().Be("5");
    }

    // --- parsing / validation ---

    [Fact]
    public void Parse_RepeatUntilDo_ProducesRepeatStep()
    {
        var script = new ScriptParser().Parse("""
            ---
            steps:
              - repeat:
                  until: "n >= 3"
                  do:
                    - set: n = n + 1
            """);

        var step = script.Steps[0];
        step.GetStepType().Should().Be(StepType.Repeat);
        step.Until.Should().Be("n >= 3");
        step.Do.Should().NotBeNull();
        step.Do!.Count.Should().Be(1);
    }

    [Fact]
    public void Validate_RepeatWithoutUntil_ReportsError()
    {
        const string yaml = """
            ---
            steps:
              - repeat:
                  do:
                    - set: n = 1
            """;
        var parser = new ScriptParser();
        var script = parser.Parse(yaml);

        var errors = parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("until"));
    }
}
