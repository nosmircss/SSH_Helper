using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI;

public sealed class ManualExecutionStatusProgressTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void ShouldShowProgress_OnlyReturnsTrueForMultiOperationRuns(int totalOperations, bool expected)
    {
        ManualExecutionStatusProgress.ShouldShowProgress(totalOperations).Should().Be(expected);
    }

    [Fact]
    public void Advance_UsesPercentBasedStatusText()
    {
        var state = ManualExecutionStatusProgress.Advance(
            previousCompletedOperations: 0,
            new FolderExecutionProgress
            {
                CompletedOperations = 2,
                TotalOperations = 5
            });

        state.CompletedOperations.Should().Be(2);
        state.TotalOperations.Should().Be(5);
        state.StatusText.Should().Be("Running... 40%");
    }

    [Fact]
    public void Advance_DoesNotMoveBackwardWhenParallelReportsArriveOutOfOrder()
    {
        var state = ManualExecutionStatusProgress.Advance(
            previousCompletedOperations: 3,
            new FolderExecutionProgress
            {
                CompletedOperations = 2,
                TotalOperations = 5
            });

        state.CompletedOperations.Should().Be(3);
        state.StatusText.Should().Be("Running... 60%");
    }

    [Fact]
    public void Advance_FallsBackToSimpleRunningTextWhenTotalOperationsInvalid()
    {
        var state = ManualExecutionStatusProgress.Advance(
            previousCompletedOperations: 0,
            new FolderExecutionProgress
            {
                CompletedOperations = 1,
                TotalOperations = 0
            });

        state.CompletedOperations.Should().Be(0);
        state.TotalOperations.Should().Be(0);
        state.StatusText.Should().Be("Running...");
    }
}
