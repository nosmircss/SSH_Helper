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
}
