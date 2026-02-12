using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ConfirmCommandTests
{
    private readonly ConfirmCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_NullOptions_ReturnsFail()
    {
        var step = new ScriptStep();
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no options");
    }

    [Fact]
    public async Task ExecuteAsync_MissingInto_ReturnsFail()
    {
        var step = new ScriptStep
        {
            Confirm = new ConfirmOptions
            {
                Prompt = "Are you sure?"
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("'into'");
    }
}
