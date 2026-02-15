using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class MultiselectCommandTests
{
    private readonly MultiselectCommand _command = new();

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
            Multiselect = new MultiselectOptions
            {
                Prompt = "Select:",
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
            Multiselect = new MultiselectOptions
            {
                Prompt = "Select:",
                Into = "selections"
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("at least one option");
    }

    [Fact]
    public async Task ExecuteAsync_OptionsSourceMissing_ReturnsFail()
    {
        var step = new ScriptStep
        {
            Multiselect = new MultiselectOptions
            {
                Prompt = "Select:",
                Into = "choices",
                OptionsFrom = "interface_list"
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("did not resolve");
    }
}
