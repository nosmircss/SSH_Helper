using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

/// <summary>
/// Tests for the LogCommand class.
/// </summary>
public class LogCommandTests
{
    private readonly LogCommand _command;

    public LogCommandTests()
    {
        _command = new LogCommand();
    }

    [Fact]
    public async Task ExecuteAsync_WithSimpleString_EmitsInfoOutput()
    {
        // Arrange
        var step = new ScriptStep { Log = "Test message" };
        var context = new ScriptContext();

        var outputs = new List<(string Message, ScriptOutputType Type)>();
        context.OutputReceived += (s, e) => outputs.Add((e.Message, e.Type));

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        outputs.Should().ContainSingle();
        outputs[0].Message.Should().Be("Test message");
        outputs[0].Type.Should().Be(ScriptOutputType.Info);
    }

    [Fact]
    public async Task ExecuteAsync_WithLogOptions_EmitsCorrectLevel()
    {
        // Arrange
        var step = new ScriptStep
        {
            Log = new LogOptions
            {
                Message = "Warning message",
                Level = "warning"
            }
        };
        var context = new ScriptContext();

        var outputs = new List<(string Message, ScriptOutputType Type)>();
        context.OutputReceived += (s, e) => outputs.Add((e.Message, e.Type));

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        outputs.Should().ContainSingle();
        outputs[0].Message.Should().Be("Warning message");
        outputs[0].Type.Should().Be(ScriptOutputType.Warning);
    }

    [Fact]
    public async Task ExecuteAsync_WithDebugLevel_SuppressedWhenNotDebugMode()
    {
        // Arrange
        var step = new ScriptStep
        {
            Log = new LogOptions
            {
                Message = "Debug info",
                Level = "debug"
            }
        };
        var context = new ScriptContext { DebugMode = false };

        var outputs = new List<string>();
        context.OutputReceived += (s, e) => outputs.Add(e.Message);

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        outputs.Should().BeEmpty(); // Debug output suppressed
    }

    [Fact]
    public async Task ExecuteAsync_WithDebugLevel_ShownWhenDebugMode()
    {
        // Arrange
        var step = new ScriptStep
        {
            Log = new LogOptions
            {
                Message = "Debug info",
                Level = "debug"
            }
        };
        var context = new ScriptContext { DebugMode = true };

        var outputs = new List<string>();
        context.OutputReceived += (s, e) => outputs.Add(e.Message);

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        outputs.Should().ContainSingle();
        outputs[0].Should().Be("Debug info");
    }

    [Fact]
    public async Task ExecuteAsync_SubstitutesVariables()
    {
        // Arrange
        var step = new ScriptStep { Log = "Host: ${Host_IP}" };
        var context = new ScriptContext();
        context.SetVariable("Host_IP", "192.168.1.1");

        var outputs = new List<string>();
        context.OutputReceived += (s, e) => outputs.Add(e.Message);

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        outputs[0].Should().Be("Host: 192.168.1.1");
    }

    [Theory]
    [InlineData("error", ScriptOutputType.Error)]
    [InlineData("err", ScriptOutputType.Error)]
    [InlineData("warning", ScriptOutputType.Warning)]
    [InlineData("warn", ScriptOutputType.Warning)]
    [InlineData("success", ScriptOutputType.Success)]
    [InlineData("info", ScriptOutputType.Info)]
    [InlineData("unknown", ScriptOutputType.Info)] // defaults to info
    public async Task ExecuteAsync_MapsLevelCorrectly(string level, ScriptOutputType expectedType)
    {
        // Arrange
        var step = new ScriptStep
        {
            Log = new LogOptions
            {
                Message = "Test",
                Level = level
            }
        };
        var context = new ScriptContext { DebugMode = true }; // Enable debug to capture all

        ScriptOutputType? receivedType = null;
        context.OutputReceived += (s, e) => receivedType = e.Type;

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedType.Should().Be(expectedType);
    }
}
