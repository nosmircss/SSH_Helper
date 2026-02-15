using FluentAssertions;
using SSH_Helper.Forms;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Terminal;
using SSH_Helper.Utilities;
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

    [Fact]
    public void ShouldCloseSharedWindowWithoutSendingEof_SharedCtrlD_ReturnsTrue()
    {
        var result = InteractiveTerminalService.ShouldCloseSharedWindowWithoutSendingEof(
            InteractiveSessionMode.Shared,
            new TerminalKeyEventArgs
            {
                ConsoleKey = ConsoleKey.D,
                Modifiers = ConsoleModifiers.Control
            });

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCloseSharedWindowWithoutSendingEof_SeparateCtrlD_ReturnsFalse()
    {
        var result = InteractiveTerminalService.ShouldCloseSharedWindowWithoutSendingEof(
            InteractiveSessionMode.Separate,
            new TerminalKeyEventArgs
            {
                ConsoleKey = ConsoleKey.D,
                Modifiers = ConsoleModifiers.Control
            });

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCloseSharedWindowWithoutSendingEof_SharedNonCtrlD_ReturnsFalse()
    {
        var result = InteractiveTerminalService.ShouldCloseSharedWindowWithoutSendingEof(
            InteractiveSessionMode.Shared,
            new TerminalKeyEventArgs
            {
                ConsoleKey = ConsoleKey.C,
                Modifiers = ConsoleModifiers.Control
            });

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("logout")]
    [InlineData("  EXIT  ")]
    [InlineData("\tLogout\t")]
    public void ShouldBlockSharedShellCommand_ExitAndLogout_ReturnsTrue(string value)
    {
        var result = InteractiveTerminalService.ShouldBlockSharedShellCommand(value);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("show version")]
    [InlineData("exit now")]
    [InlineData("logout 1")]
    public void ShouldBlockSharedShellCommand_NonProtectedCommands_ReturnsFalse(string? value)
    {
        var result = InteractiveTerminalService.ShouldBlockSharedShellCommand(value);
        result.Should().BeFalse();
    }

    [Fact]
    public void CleanTranscriptForAudit_BackspaceSequence_RemovesSquareArtifacts()
    {
        const string raw = "○ → exit\b\b\b\b\r\n";

        var cleaned = InteractiveTerminalService.CleanTranscriptForAudit(raw);

        cleaned.Should().Be("○ → exit\r\n");
        cleaned.Should().NotContain("\b");
    }

    [Fact]
    public void CleanTranscriptForAudit_DeleteControl_RemovesPreviousCharacter()
    {
        const string raw = "logout\u007F\u007F\r\n";

        var cleaned = InteractiveTerminalService.CleanTranscriptForAudit(raw);

        cleaned.Should().Be("logout\r\n");
        cleaned.Should().NotContain("\u007F");
    }

    [Fact]
    public void IsDetachedCaptureCompletionReason_CtrlCAndTimeout_ReturnTrue()
    {
        InteractiveTerminalService.IsDetachedCaptureCompletionReason("ctrl_c_continue").Should().BeTrue();
        InteractiveTerminalService.IsDetachedCaptureCompletionReason("timeout_continue").Should().BeTrue();
        InteractiveTerminalService.IsDetachedCaptureCompletionReason("natural_complete").Should().BeTrue();
    }

    [Fact]
    public void IsCaptureSuccessCloseReason_EarlyClosePartial_ReturnsTrue()
    {
        var result = InteractiveTerminalService.IsCaptureSuccessCloseReason("early_close_partial");
        result.Should().BeTrue();
    }

    [Fact]
    public void IsCaptureSuccessCloseReason_Disconnected_ReturnsFalse()
    {
        var result = InteractiveTerminalService.IsCaptureSuccessCloseReason("disconnected");
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldMirrorCaptureChunk_ReflectsMirrorFlagAndContent()
    {
        InteractiveTerminalService.ShouldMirrorCaptureChunk(false, "line").Should().BeFalse();
        InteractiveTerminalService.ShouldMirrorCaptureChunk(true, string.Empty).Should().BeFalse();
        InteractiveTerminalService.ShouldMirrorCaptureChunk(true, "line").Should().BeTrue();
    }

    [Fact]
    public void ShouldArmCaptureNaturalCompletion_PromptOnlyChunk_DoesNotArm()
    {
        var result = InteractiveTerminalService.ShouldArmCaptureNaturalCompletion(
            "fw-host # ",
            "diagnose sniffer packet any 'icmp' 4 0 a");

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldArmCaptureNaturalCompletion_CommandEchoChunk_Arms()
    {
        var result = InteractiveTerminalService.ShouldArmCaptureNaturalCompletion(
            "fw-host # diagnose sniffer packet any 'icmp' 4 0 a",
            "diagnose sniffer packet any 'icmp' 4 0 a");

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldArmCaptureNaturalCompletion_NonPromptOutput_Arms()
    {
        var result = InteractiveTerminalService.ShouldArmCaptureNaturalCompletion(
            "interfaces sniff line",
            "diagnose sniffer packet any 'icmp' 4 0 a");

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldCompleteCaptureOnPrompt_WithKnownPromptRegex_PacketDirectionChunk_ReturnsFalse()
    {
        var promptRegex = PromptDetector.BuildPromptRegex("FGT-01 #");

        var result = InteractiveTerminalService.ShouldCompleteCaptureOnPrompt(
            "10.10.10.11 ->",
            promptRegex);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldCompleteCaptureOnPrompt_WithKnownPromptRegex_ReturnedPrompt_ReturnsTrue()
    {
        var promptRegex = PromptDetector.BuildPromptRegex("FGT-01 #");

        var result = InteractiveTerminalService.ShouldCompleteCaptureOnPrompt(
            "FGT-01 # ",
            promptRegex);

        result.Should().BeTrue();
    }
}
