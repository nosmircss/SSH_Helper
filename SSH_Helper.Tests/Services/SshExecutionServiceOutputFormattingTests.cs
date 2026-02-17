using FluentAssertions;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class SshExecutionServiceOutputFormattingTests
{
    [Fact]
    public void EnsureTrailingNewLine_AppendsWhenMissing()
    {
        var output = SshExecutionService.EnsureTrailingNewLine("packet line");

        output.Should().Be("packet line" + Environment.NewLine);
    }

    [Fact]
    public void EnsureTrailingNewLine_KeepsExistingCrLf()
    {
        var output = SshExecutionService.EnsureTrailingNewLine("packet line\r\n");

        output.Should().Be("packet line\r\n");
    }

    [Fact]
    public void EnsureTrailingNewLine_KeepsExistingLf()
    {
        var output = SshExecutionService.EnsureTrailingNewLine("packet line\n");

        output.Should().Be("packet line\n");
    }

    [Fact]
    public void FormatScriptOutput_RawChunk_PreservesChunkAsIs()
    {
        var output = SshExecutionService.FormatScriptOutput("partial", ScriptOutputType.RawChunk);

        output.Should().Be("partial");
    }

    [Fact]
    public void FormatScriptOutput_CommandOutput_AppendsTrailingNewLine()
    {
        var output = SshExecutionService.FormatScriptOutput("line", ScriptOutputType.CommandOutput);

        output.Should().Be("line" + Environment.NewLine);
    }

    [Fact]
    public void NormalizeScriptOutputBoundary_NonRawAfterRawWithoutLineBreak_PrependsBoundaryNewLine()
    {
        var result = SshExecutionService.NormalizeScriptOutputBoundary(
            output: "done" + Environment.NewLine,
            outputType: ScriptOutputType.Info,
            previousOutputEndedWithLineTerminator: false);

        result.Output.Should().Be(Environment.NewLine + "done" + Environment.NewLine);
        result.EndsWithLineTerminator.Should().BeTrue();
    }

    [Fact]
    public void NormalizeScriptOutputBoundary_NonRawAlreadyStartingWithLineBreak_DoesNotDuplicateBoundary()
    {
        var result = SshExecutionService.NormalizeScriptOutputBoundary(
            output: Environment.NewLine + "done" + Environment.NewLine,
            outputType: ScriptOutputType.Info,
            previousOutputEndedWithLineTerminator: false);

        result.Output.Should().Be(Environment.NewLine + "done" + Environment.NewLine);
        result.EndsWithLineTerminator.Should().BeTrue();
    }

    [Fact]
    public void NormalizeScriptOutputBoundary_RawChunk_DoesNotPrependBoundary()
    {
        var result = SshExecutionService.NormalizeScriptOutputBoundary(
            output: "terminal prompt",
            outputType: ScriptOutputType.RawChunk,
            previousOutputEndedWithLineTerminator: false);

        result.Output.Should().Be("terminal prompt");
        result.EndsWithLineTerminator.Should().BeFalse();
    }
}
