using FluentAssertions;
using SSH_Helper.Services.Terminal;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class InteractiveTerminalServiceTranscriptFilterTests
{
    [Fact]
    public void FilterTranscriptChunkForAudit_NormalOutput_PreservesText()
    {
        var result = InteractiveTerminalService.FilterTranscriptChunkForAudit(
            rawData: "hostname\r\nversion\r\n",
            strippedData: "hostname\r\nversion\r\n",
            inAlternateScreen: false);

        result.CapturedText.Should().Be("hostname\r\nversion\r\n");
        result.InAlternateScreen.Should().BeFalse();
    }

    [Fact]
    public void FilterTranscriptChunkForAudit_EnteringAlternateScreen_DropsFullscreenContent()
    {
        var raw = "host$ vi test.txt\r\n\u001b[?1049hFULLSCREEN_CONTENT";
        var stripped = "host$ vi test.txt\r\nFULLSCREEN_CONTENT";

        var result = InteractiveTerminalService.FilterTranscriptChunkForAudit(
            rawData: raw,
            strippedData: stripped,
            inAlternateScreen: false);

        result.CapturedText.Should().Contain("vi test.txt");
        result.CapturedText.Should().NotContain("FULLSCREEN_CONTENT");
        result.InAlternateScreen.Should().BeTrue();
    }

    [Fact]
    public void FilterTranscriptChunkForAudit_WhileInAlternateScreen_SuppressesChunk()
    {
        var result = InteractiveTerminalService.FilterTranscriptChunkForAudit(
            rawData: "CURSES_REDRAW",
            strippedData: "CURSES_REDRAW",
            inAlternateScreen: true);

        result.CapturedText.Should().BeEmpty();
        result.InAlternateScreen.Should().BeTrue();
    }

    [Fact]
    public void FilterTranscriptChunkForAudit_LeavingAlternateScreen_ResumesCapture()
    {
        var raw = "\u001b[?1049l\r\nhost$ ";
        var stripped = "host$ ";

        var result = InteractiveTerminalService.FilterTranscriptChunkForAudit(
            rawData: raw,
            strippedData: stripped,
            inAlternateScreen: true);

        result.CapturedText.Should().Contain("host$");
        result.InAlternateScreen.Should().BeFalse();
    }
}
