using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

/// <summary>
/// Tests for the UpdateColumnCommand class.
/// </summary>
public class UpdateColumnCommandTests
{
    private readonly UpdateColumnCommand _command;

    public UpdateColumnCommandTests()
    {
        _command = new UpdateColumnCommand();
    }

    #region Basic Execution Tests

    [Fact]
    public async Task ExecuteAsync_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "version",
                Value = "1.0.0"
            }
        };
        var context = new ScriptContext();

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullUpdateColumn_ReturnsFailure()
    {
        // Arrange
        var step = new ScriptStep { UpdateColumn = null };
        var context = new ScriptContext();

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("no options");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyColumn_ReturnsFailure()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "",
                Value = "test"
            }
        };
        var context = new ScriptContext();

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("column");
    }

    [Fact]
    public async Task ExecuteAsync_WithNullValue_ReturnsFailure()
    {
        // Arrange - null value means it wasn't specified in the script
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "test",
                Value = null
            }
        };
        var context = new ScriptContext();

        // Act
        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("value");
    }

    #endregion

    #region Event Firing Tests

    [Fact]
    public async Task ExecuteAsync_FiresColumnUpdateRequestedEvent()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "status",
                Value = "active"
            }
        };
        var context = new ScriptContext();

        string? receivedColumn = null;
        string? receivedValue = null;
        context.ColumnUpdateRequested += (s, e) =>
        {
            receivedColumn = e.ColumnName;
            receivedValue = e.Value;
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedColumn.Should().Be("status");
        receivedValue.Should().Be("active");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyValue_FiresEventWithEmptyString()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "notes",
                Value = ""
            }
        };
        var context = new ScriptContext();

        string? receivedValue = null;
        context.ColumnUpdateRequested += (s, e) =>
        {
            receivedValue = e.Value;
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedValue.Should().Be("");
    }

    #endregion

    #region Variable Substitution Tests

    [Fact]
    public async Task ExecuteAsync_SubstitutesVariableInValue()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "version",
                Value = "${extracted_version}"
            }
        };
        var context = new ScriptContext();
        context.SetVariable("extracted_version", "2.5.1");

        string? receivedValue = null;
        context.ColumnUpdateRequested += (s, e) =>
        {
            receivedValue = e.Value;
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedValue.Should().Be("2.5.1");
    }

    [Fact]
    public async Task ExecuteAsync_SubstitutesMultipleVariables()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "info",
                Value = "${hostname} - ${version}"
            }
        };
        var context = new ScriptContext();
        context.SetVariable("hostname", "router1");
        context.SetVariable("version", "15.1");

        string? receivedValue = null;
        context.ColumnUpdateRequested += (s, e) =>
        {
            receivedValue = e.Value;
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedValue.Should().Be("router1 - 15.1");
    }

    [Fact]
    public async Task ExecuteAsync_SubstitutesTimestampVariable()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "last_checked",
                Value = "${_timestamp}"
            }
        };
        var context = new ScriptContext();

        string? receivedValue = null;
        context.ColumnUpdateRequested += (s, e) =>
        {
            receivedValue = e.Value;
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedValue.Should().NotBeNullOrEmpty();
        receivedValue.Should().MatchRegex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}");
    }

    [Fact]
    public async Task ExecuteAsync_UndefinedVariable_ReplacesWithEmptyString()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "test",
                Value = "prefix_${undefined_var}_suffix"
            }
        };
        var context = new ScriptContext();

        string? receivedValue = null;
        context.ColumnUpdateRequested += (s, e) =>
        {
            receivedValue = e.Value;
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedValue.Should().Be("prefix__suffix");
    }

    [Fact]
    public async Task ExecuteAsync_LiteralValue_NoSubstitution()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "status",
                Value = "completed successfully"
            }
        };
        var context = new ScriptContext();

        string? receivedValue = null;
        context.ColumnUpdateRequested += (s, e) =>
        {
            receivedValue = e.Value;
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        receivedValue.Should().Be("completed successfully");
    }

    #endregion

    #region Debug Output Tests

    [Fact]
    public async Task ExecuteAsync_InDebugMode_EmitsDebugOutput()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "test_col",
                Value = "test_val"
            }
        };
        var context = new ScriptContext { DebugMode = true };

        var outputs = new List<string>();
        context.OutputReceived += (s, e) =>
        {
            outputs.Add(e.Message);
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        outputs.Should().Contain(o => o.Contains("UpdateColumn") && o.Contains("test_col"));
    }

    [Fact]
    public async Task ExecuteAsync_NotInDebugMode_NoDebugOutput()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "test_col",
                Value = "test_val"
            }
        };
        var context = new ScriptContext { DebugMode = false };

        var outputs = new List<string>();
        context.OutputReceived += (s, e) =>
        {
            outputs.Add(e.Message);
        };

        // Act
        await _command.ExecuteAsync(step, context, CancellationToken.None);

        // Assert
        outputs.Should().BeEmpty();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExecuteAsync_CancellationNotRequested_Completes()
    {
        // Arrange
        var step = new ScriptStep
        {
            UpdateColumn = new UpdateColumnOptions
            {
                Column = "test",
                Value = "value"
            }
        };
        var context = new ScriptContext();
        var cts = new CancellationTokenSource();

        // Act
        var result = await _command.ExecuteAsync(step, context, cts.Token);

        // Assert
        result.Success.Should().BeTrue();
    }

    #endregion
}
