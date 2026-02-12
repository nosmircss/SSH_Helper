using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ChooseCommandTests
{
    private readonly ChooseCommand _command = new();

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
            Choose = new ChooseOptions
            {
                Prompt = "Pick one:",
                Options = { new ChoiceOption { Label = "a", Value = "a" } }
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("'into'");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyOptions_ReturnsFail()
    {
        var step = new ScriptStep
        {
            Choose = new ChooseOptions
            {
                Prompt = "Pick one:",
                Into = "choice"
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("at least one option");
    }
}
