using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptExecutorSoftAssertTests
{
    private static ScriptStep SoftAssert(string condition)
        => new() { Assert = new AssertOptions { Condition = condition, Severity = "warning" } };

    [Fact]
    public async Task SoftAssertFailure_DoesNotTerminate_AndIsCounted()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                SoftAssert("1 == 2"),
                new() { Set = "ran = yes" }
            }
        };

        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        context.GetVariableString("ran").Should().Be("yes");
        context.SoftAssertFailed.Should().Be(1);
        context.SoftAssertPassed.Should().Be(0);
    }

    [Fact]
    public async Task SoftAssertPass_IsCounted()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script { Steps = new List<ScriptStep> { SoftAssert("1 == 1") } };

        await executor.ExecuteAsync(script, context);

        context.SoftAssertPassed.Should().Be(1);
        context.SoftAssertFailed.Should().Be(0);
    }

    [Fact]
    public async Task HardAssert_IsNotCountedAsSoft()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Assert = new AssertOptions { Condition = "1 == 1" } } // no severity => hard
            }
        };

        await executor.ExecuteAsync(script, context);

        context.SoftAssertPassed.Should().Be(0);
        context.SoftAssertFailed.Should().Be(0);
    }

    [Fact]
    public async Task Summary_EmittedAtCompletion_WhenSoftAssertsPresent()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var messages = new List<string>();
        context.OutputReceived += (_, e) => messages.Add(e.Message);

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                SoftAssert("1 == 1"),
                SoftAssert("2 == 3")
            }
        };

        await executor.ExecuteAsync(script, context);

        context.SoftAssertPassed.Should().Be(1);
        context.SoftAssertFailed.Should().Be(1);
        messages.Should().Contain(m =>
            m.Contains("Soft assertions") && m.Contains("1 passed") && m.Contains("1 failed"));
    }

    [Fact]
    public async Task NoSummary_WhenNoSoftAsserts()
    {
        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var messages = new List<string>();
        context.OutputReceived += (_, e) => messages.Add(e.Message);

        var script = new Script { Steps = new List<ScriptStep> { new() { Set = "a = 1" } } };

        await executor.ExecuteAsync(script, context);

        messages.Should().NotContain(m => m.Contains("Soft assertions"));
    }
}
