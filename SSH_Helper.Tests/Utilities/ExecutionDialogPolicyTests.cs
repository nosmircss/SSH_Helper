using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class ExecutionDialogPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void ShouldPromptForPresetExecutionOptions_ReturnsExpectedResult(int hostCount, bool expected)
    {
        ExecutionDialogPolicy.ShouldPromptForPresetExecutionOptions(hostCount).Should().Be(expected);
    }
}
