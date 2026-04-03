using System.Text;
using FluentAssertions;
using SSH_Helper.Forms;
using SSH_Helper.Services;
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
    public void CleanTranscriptForAudit_TabAutocompleteBackspaces_PreservesExecutedCommand()
    {
        var raw =
            "FortiGate-VM64-KVM # get system status" +
            "\b\b\b\b\b\bstandalone-cluster" +
            "\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\bstartup-error-log" +
            "\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\b\bstandalone-cluster\r\n";

        var cleaned = InteractiveTerminalService.CleanTranscriptForAudit(raw);

        cleaned.Should().Be("FortiGate-VM64-KVM # get system standalone-cluster\r\n");
    }

    [Fact]
    public void ShouldEmitTranscriptChunkDebug_BackspaceChunk_ReturnsTrue()
    {
        var shouldEmit = InteractiveTerminalService.ShouldEmitTranscriptChunkDebug(
            rawData: "status\b\b\b",
            strippedData: "status",
            capturedText: "sta");

        shouldEmit.Should().BeTrue();
    }

    [Fact]
    public void ShouldEmitTranscriptChunkDebug_UnchangedPlainChunk_ReturnsFalse()
    {
        var shouldEmit = InteractiveTerminalService.ShouldEmitTranscriptChunkDebug(
            rawData: "hostname\r\n",
            strippedData: "hostname\r\n",
            capturedText: "hostname\r\n");

        shouldEmit.Should().BeFalse();
    }

    [Fact]
    public void FormatInteractiveDebugText_EscapesControlCharactersAndTruncates()
    {
        const string value = "AB\tCD\r\nE\bF\u007F\u001B12345";

        var formatted = InteractiveTerminalService.FormatInteractiveDebugText(value, maxLength: 10);

        formatted.Should().Be("AB\\tCD\\r\\n...[+17 chars]");
    }

    [Fact]
    public void ResolveTranscriptAssemblyInput_NonAlternateRawChunk_UsesRawData()
    {
        const string raw = "\u001B[15Drtup-error-log\u001B[K";
        const string captured = "rtup-error-log";

        var result = InteractiveTerminalService.ResolveTranscriptAssemblyInput(
            rawData: raw,
            capturedText: captured,
            inAlternateBefore: false,
            inAlternateAfter: false);

        result.Should().Be(raw);
    }

    [Fact]
    public void ResolveTranscriptAssemblyInput_AlternateScreenTransition_UsesCapturedText()
    {
        const string raw = "\u001B[?1049hFULLSCREEN";
        const string captured = "vi test.txt\r\n";

        var result = InteractiveTerminalService.ResolveTranscriptAssemblyInput(
            rawData: raw,
            capturedText: captured,
            inAlternateBefore: false,
            inAlternateAfter: true);

        result.Should().Be(captured);
    }

    [Fact]
    public void PrepareMirroredChunkForEmission_CursorRewriteAcrossChunks_MatchesWholeStreamNormalization()
    {
        const string chunk1 = "FortiGate-VM64-KVM # get system standalone-cluster";
        const string chunk2 = "\u001B[15Drtup-error-log\u001B[K";
        const string chunk3 = "\u001B[14Dtus\u001B[K\r\n";
        var expected = InteractiveTerminalService.NormalizeMirroredTranscript(chunk1 + chunk2 + chunk3);

        var pending = new StringBuilder();
        var first = InteractiveTerminalService.PrepareMirroredChunkForEmission(chunk1, pending, flush: false);
        var second = InteractiveTerminalService.PrepareMirroredChunkForEmission(chunk2, pending, flush: false);
        var third = InteractiveTerminalService.PrepareMirroredChunkForEmission(chunk3, pending, flush: false);

        first.Should().BeEmpty();
        second.Should().BeEmpty();
        third.Should().Be(expected);
    }

    [Fact]
    public void IsDetachedCaptureCompletionReason_CtrlCAndTimeout_ReturnTrue()
    {
        InteractiveTerminalService.IsDetachedCaptureCompletionReason("ctrl_c_continue").Should().BeTrue();
        InteractiveTerminalService.IsDetachedCaptureCompletionReason("timeout_continue").Should().BeTrue();
        InteractiveTerminalService.IsDetachedCaptureCompletionReason("max_lines_continue").Should().BeTrue();
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
    public void AppendTranscriptWithCap_WhenExceeded_CapsAndKeepsTranscriptStart()
    {
        const int transcriptCap = 500_000;
        var builder = new StringBuilder();
        var lineCount = transcriptCap - 1;
        var previousChunkEndedWithCarriageReturn = false;
        var transcriptCapped = false;
        const string chunk = "line-0\r\nline-1\r\n";

        InteractiveTerminalService.AppendTranscriptWithCap(
            builder,
            chunk,
            ref lineCount,
            ref previousChunkEndedWithCarriageReturn,
            ref transcriptCapped);

        lineCount.Should().Be(transcriptCap);
        transcriptCapped.Should().BeTrue();
        builder.ToString().Should().Contain("line-0");
        builder.ToString().Should().NotContain("line-1");
        builder.ToString().Should().Contain("interactive transcript capped");
    }

    [Fact]
    public void AppendTranscriptWithCap_LargeSingleLine_DoesNotTrimWithoutLineBreak()
    {
        var builder = new StringBuilder(new string('a', 500_000));
        var lineCount = 0;
        var previousChunkEndedWithCarriageReturn = false;
        var transcriptCapped = false;

        InteractiveTerminalService.AppendTranscriptWithCap(
            builder,
            new string('b', 60_000),
            ref lineCount,
            ref previousChunkEndedWithCarriageReturn,
            ref transcriptCapped);

        lineCount.Should().Be(0);
        transcriptCapped.Should().BeFalse();
        builder.Length.Should().Be(560_000);
        builder.ToString().Should().NotContain("interactive transcript capped");
    }

    [Fact]
    public void AppendTranscriptWithCap_AfterCapReached_DropsLaterChunks()
    {
        const int transcriptCap = 500_000;
        var builder = new StringBuilder();
        var lineCount = transcriptCap - 1;
        var previousChunkEndedWithCarriageReturn = false;
        var transcriptCapped = false;

        const string firstChunk = "line\r\nline-should-not-fit\r\n";
        InteractiveTerminalService.AppendTranscriptWithCap(
            builder,
            firstChunk,
            ref lineCount,
            ref previousChunkEndedWithCarriageReturn,
            ref transcriptCapped);
        var before = builder.ToString();

        InteractiveTerminalService.AppendTranscriptWithCap(
            builder,
            "this should not be appended\r\n",
            ref lineCount,
            ref previousChunkEndedWithCarriageReturn,
            ref transcriptCapped);

        transcriptCapped.Should().BeTrue();
        lineCount.Should().Be(transcriptCap);
        builder.ToString().Should().Be(before);
    }

    [Fact]
    public void ApplyMirrorOutputCap_WhenChunkExceedsLineCap_AppendsNoticeAndCaps()
    {
        const int mirrorCap = 50_000;
        const string chunk = "line-0\r\nline-should-not-fit\r\n";

        var result = InteractiveTerminalService.ApplyMirrorOutputCap(
            chunk,
            emittedLines: mirrorCap - 1,
            isCapped: false,
            previousChunkEndedWithCarriageReturn: false);

        result.IsCapped.Should().BeTrue();
        result.EmittedLines.Should().Be(mirrorCap);
        result.Output.Should().Contain("interactive mirror output capped");
    }

    [Fact]
    public void ApplyMirrorOutputCap_AfterCapReached_SuppressesFutureOutput()
    {
        const int mirrorCap = 50_000;
        var first = InteractiveTerminalService.ApplyMirrorOutputCap(
            "line\r\nline-should-not-fit\r\n",
            emittedLines: mirrorCap - 1,
            isCapped: false,
            previousChunkEndedWithCarriageReturn: false);

        var second = InteractiveTerminalService.ApplyMirrorOutputCap(
            "more-data",
            first.EmittedLines,
            first.IsCapped,
            first.PreviousChunkEndedWithCarriageReturn);

        second.IsCapped.Should().BeTrue();
        second.Output.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeMirroredTranscript_RemovesControlArtifactsAndKeepsVisibleText()
    {
        const string raw = "FortiGate # ^D\b\bexit\r\n";

        var normalized = InteractiveTerminalService.NormalizeMirroredTranscript(raw);

        normalized.Should().Be("FortiGate # exit\r\n");
        normalized.Should().NotContain("^D");
        normalized.Should().NotContain("\b");
    }

    [Fact]
    public void BuildMirroredStartupPromptPrefix_AppendsTrailingSpaceWhenMissing()
    {
        var prefix = InteractiveTerminalService.BuildMirroredStartupPromptPrefix("FGT-01 #");

        prefix.Should().Be("FGT-01 # ");
    }

    [Fact]
    public void BuildMirroredStartupPromptPrefix_EmptyInput_ReturnsEmpty()
    {
        var prefix = InteractiveTerminalService.BuildMirroredStartupPromptPrefix("   ");

        prefix.Should().BeEmpty();
    }

    [Fact]
    public void PrependStartupPromptIfMissing_AddsPromptToTranscriptStart()
    {
        var transcript = "show system interface\r\nconfig system interface\r\n";

        var result = InteractiveTerminalService.PrependStartupPromptIfMissing(
            transcript,
            "FortiGate-VM64-KVM #");

        result.Should().StartWith("FortiGate-VM64-KVM # show system interface");
    }

    [Fact]
    public void PrependStartupPromptIfMissing_AlreadyPrefixed_DoesNotDuplicate()
    {
        var transcript = "FortiGate-VM64-KVM # show system interface\r\n";

        var result = InteractiveTerminalService.PrependStartupPromptIfMissing(
            transcript,
            "FortiGate-VM64-KVM #");

        result.Should().Be(transcript);
    }

    [Fact]
    public void PrependStartupPromptIfMissing_EmptyTranscript_RemainsEmpty()
    {
        var result = InteractiveTerminalService.PrependStartupPromptIfMissing(
            string.Empty,
            "FortiGate-VM64-KVM #");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ResolveStartupPromptLiteral_PrefersDetectedPrompt()
    {
        var result = InteractiveTerminalService.ResolveStartupPromptLiteral(
            "FGT-DETECTED #",
            "FGT-FALLBACK #");

        result.Should().Be("FGT-DETECTED #");
    }

    [Fact]
    public void ResolveStartupPromptLiteral_UsesFallbackWhenDetectedMissing()
    {
        var result = InteractiveTerminalService.ResolveStartupPromptLiteral(
            "   ",
            "FGT-FALLBACK #");

        result.Should().Be("FGT-FALLBACK #");
    }

    [Fact]
    public void ResolveStartupPromptLiteral_EmptyWhenNoDetectedOrFallback()
    {
        var result = InteractiveTerminalService.ResolveStartupPromptLiteral(
            "   ",
            "   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void PrepareMirroredChunkForEmission_CarriesControlSequenceAcrossChunks()
    {
        var pending = new StringBuilder();

        var first = InteractiveTerminalService.PrepareMirroredChunkForEmission(
            "FortiGate # ^",
            pending,
            flush: false);
        var second = InteractiveTerminalService.PrepareMirroredChunkForEmission(
            "D\b\bexit\r\n",
            pending,
            flush: false);

        first.Should().BeEmpty();
        second.Should().Be("FortiGate # exit\r\n");
    }

    [Fact]
    public void CountLinesFromCapturedChunk_CountsCrlfAndLfWithoutDoubleCounting()
    {
        var previousChunkEndedWithCarriageReturn = false;
        var lines = InteractiveTerminalService.CountLinesFromCapturedChunk(
            "line1\r\nline2\nline3\rline4",
            ref previousChunkEndedWithCarriageReturn);

        lines.Should().Be(3);
        previousChunkEndedWithCarriageReturn.Should().BeFalse();
    }

    [Fact]
    public void CountLinesFromCapturedChunk_HandlesChunkBoundaryCrLf()
    {
        var previousChunkEndedWithCarriageReturn = false;
        var first = InteractiveTerminalService.CountLinesFromCapturedChunk(
            "line1\r",
            ref previousChunkEndedWithCarriageReturn);
        var second = InteractiveTerminalService.CountLinesFromCapturedChunk(
            "\nline2\r\n",
            ref previousChunkEndedWithCarriageReturn);

        first.Should().Be(1);
        second.Should().Be(1);
        previousChunkEndedWithCarriageReturn.Should().BeFalse();
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

    [Fact]
    public void ResolveBannerAcceptKey_PressToAcceptPattern_ReturnsCapturedKey()
    {
        var match = SshShellSession.Patterns.BannerAcceptPrompt.Match("Press 'q' to accept:");

        var key = InteractiveTerminalService.ResolveBannerAcceptKey(match);

        key.Should().Be("q");
    }

    [Fact]
    public void ResolveBannerAcceptKey_PressAnyKeyPattern_DefaultsToA()
    {
        var match = SshShellSession.Patterns.BannerAcceptPrompt.Match("Press any key to continue");

        var key = InteractiveTerminalService.ResolveBannerAcceptKey(match);

        key.Should().Be("a");
    }
}
