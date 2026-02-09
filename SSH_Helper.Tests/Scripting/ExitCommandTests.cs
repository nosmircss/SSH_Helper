using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ExitCommandTests
{
    private readonly ExitCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_WithStatusOnlyToken_UsesRequestedStatus()
    {
        var step = new ScriptStep { Exit = "failure" };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.ShouldExit.Should().BeTrue();
        result.ExitStatus.Should().Be(ScriptExitStatus.Failure);
        result.Message.Should().BeEmpty();
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WithMessageText_DefaultsToSuccess()
    {
        var step = new ScriptStep { Exit = "All checks passed" };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.ShouldExit.Should().BeTrue();
        result.ExitStatus.Should().Be(ScriptExitStatus.Success);
        result.Message.Should().Be("All checks passed");
        result.Success.Should().BeTrue();
    }
}
