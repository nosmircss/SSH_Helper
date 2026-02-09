using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class UpdateEnvironmentCommandTests
{
    private readonly UpdateEnvironmentCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_WithValidOptions_ReturnsSuccess()
    {
        var step = new ScriptStep
        {
            UpdateEnvironment = new UpdateEnvironmentOptions
            {
                Variable = "api_token",
                Value = "abc123"
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FiresEnvironmentUpdateRequestedEvent()
    {
        var step = new ScriptStep
        {
            UpdateEnvironment = new UpdateEnvironmentOptions
            {
                Variable = "api_token",
                Value = "abc123"
            }
        };
        var context = new ScriptContext();

        string? receivedVariable = null;
        string? receivedValue = null;
        context.EnvironmentUpdateRequested += (_, e) =>
        {
            receivedVariable = e.Variable;
            receivedValue = e.Value;
        };

        await _command.ExecuteAsync(step, context, CancellationToken.None);

        receivedVariable.Should().Be("api_token");
        receivedValue.Should().Be("abc123");
    }

    [Fact]
    public async Task ExecuteAsync_SubstitutesValue_AndUpdatesContextVariable()
    {
        var step = new ScriptStep
        {
            UpdateEnvironment = new UpdateEnvironmentOptions
            {
                Variable = "api_token",
                Value = "${new_token}"
            }
        };
        var context = new ScriptContext();
        context.SetVariable("new_token", "rotated-token");

        await _command.ExecuteAsync(step, context, CancellationToken.None);

        context.GetVariableString("api_token").Should().Be("rotated-token");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingVariable_ReturnsFailure()
    {
        var step = new ScriptStep
        {
            UpdateEnvironment = new UpdateEnvironmentOptions
            {
                Variable = "",
                Value = "abc123"
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("variable");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullValue_ReturnsFailure()
    {
        var step = new ScriptStep
        {
            UpdateEnvironment = new UpdateEnvironmentOptions
            {
                Variable = "api_token",
                Value = null
            }
        };
        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("value");
    }

    [Fact]
    public async Task ExecuteAsync_InDebugMode_EmitsDebugOutput()
    {
        var step = new ScriptStep
        {
            UpdateEnvironment = new UpdateEnvironmentOptions
            {
                Variable = "api_token",
                Value = "abc123"
            }
        };
        var context = new ScriptContext { DebugMode = true };
        var outputs = new List<string>();
        context.OutputReceived += (_, e) => outputs.Add(e.Message);

        await _command.ExecuteAsync(step, context, CancellationToken.None);

        outputs.Should().Contain(o => o.Contains("UpdateEnvironment") && o.Contains("api_token"));
    }
}
