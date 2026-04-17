using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class SetHistoryLabelCommandTests
{
    private readonly SetHistoryLabelCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_AppendMode_AppendsAndPreservesReplaceWhenOmitted()
    {
        var context = new ScriptContext
        {
            HistoryLabel = "Core",
            HistoryLabelReplacesAddress = true
        };
        var step = new ScriptStep
        {
            SetHistoryLabel = new SetHistoryLabelOptions
            {
                Value = "Router",
                Mode = "append",
                Separator = " "
            }
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.HistoryLabelTouched.Should().BeTrue();
        context.HistoryLabel.Should().Be("Core Router");
        context.HistoryLabelReplacesAddress.Should().BeTrue();
        context.GetHistoryLabelOperationsSnapshot().Should().ContainSingle().Which.ReplaceAddress.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_PrependMode_PlacesValueBeforeExistingLabel()
    {
        var context = new ScriptContext
        {
            HistoryLabel = "Router"
        };
        var step = new ScriptStep
        {
            SetHistoryLabel = new SetHistoryLabelOptions
            {
                Value = "Core",
                Mode = "prepend",
                Separator = " "
            }
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.HistoryLabel.Should().Be("Core Router");
        context.HistoryLabelReplacesAddress.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ClearMode_ClearsLabelAndReplaceFlag()
    {
        var context = new ScriptContext
        {
            HistoryLabel = "Core Router",
            HistoryLabelReplacesAddress = true
        };
        var step = new ScriptStep
        {
            SetHistoryLabel = new SetHistoryLabelOptions
            {
                Mode = "clear",
                Value = "ignored",
                Replace = true
            }
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.HistoryLabelTouched.Should().BeTrue();
        context.HistoryLabel.Should().BeNull();
        context.HistoryLabelReplacesAddress.Should().BeFalse();
        context.GetHistoryLabelOperationsSnapshot().Should().ContainSingle().Which.Mode.Should().Be("clear");
    }
}
