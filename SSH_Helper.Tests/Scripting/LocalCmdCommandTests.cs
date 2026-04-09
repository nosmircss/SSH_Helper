using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class LocalCmdCommandTests
{
    [Fact]
    public async Task Foreground_CapturesStdoutStderrAndExitCode()
    {
        var runner = new StubProcessRunner(stdout: "hello world", stderr: "", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "echo hello world",
                Confirm = "never",
                Into = "result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("result_stdout").Should().Be("hello world");
        context.GetVariableString("result_stderr").Should().BeEmpty();
        context.GetVariable("result_exit_code").Should().Be(0);
    }

    [Fact]
    public async Task Foreground_NonZeroExitCode_FailsWhenFailOnNonZeroTrue()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "error occurred", exitCode: 1);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "failing-command",
                Confirm = "never",
                FailOnNonZero = true,
                Into = "result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        context.GetVariable("result_exit_code").Should().Be(1);
    }

    [Fact]
    public async Task Foreground_NonZeroExitCode_SucceedsWhenFailOnNonZeroFalse()
    {
        var runner = new StubProcessRunner(stdout: "partial output", stderr: "", exitCode: 2);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "some-command",
                Confirm = "never",
                FailOnNonZero = false,
                Into = "result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("result_exit_code").Should().Be(2);
    }

    [Fact]
    public async Task Foreground_CustomSuccessCodes_AllowsNonZero()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 3010);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "installer.exe",
                Confirm = "never",
                FailOnNonZero = true,
                SuccessCodes = new List<int> { 0, 3010 },
                Into = "result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("result_exit_code").Should().Be(3010);
    }

    [Fact]
    public async Task Background_SetsStartupMetadata()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0, processId: 42);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "long-running.exe",
                Confirm = "never",
                RunMode = "background",
                Into = "bg"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("bg_pid").Should().Be(42);
        context.GetVariable("bg_started").Should().Be(true);
    }

    [Fact]
    public async Task Background_CommandBanner_MultilineCommand_UsesVisibleLineBreakMarkers()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0, processId: 43);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "\"This is the text I want to see in Notepad\" | Out-File -FilePath temp.txt -Encoding utf8\r\n\r\nnotepad temp.txt",
                Confirm = "never",
                RunMode = "background",
                Shell = "powershell"
            }
        };

        var context = new ScriptContext();
        var outputs = new List<string>();
        context.OutputReceived += (_, e) => outputs.Add(e.Message);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var banner = outputs.Find(line => line.Contains("[localcmd:background]", StringComparison.OrdinalIgnoreCase));
        banner.Should().NotBeNull();
        banner.Should().Contain("\\n");
        banner.Should().NotContain("utf8notepad");
    }

    [Fact]
    public async Task MissingCommand_Fails()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "",
                Confirm = "never"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task InteractiveAndBackground_MutuallyExclusive()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "some-cmd",
                Confirm = "never",
                Interactive = true,
                RunMode = "background"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Interactive_KeepOpen_CapturesExitCodeAndUsesDirectShellProcess()
    {
        string? startedFileName = null;
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            onStart: info => startedFileName = info.FileName);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "date",
                Confirm = "never",
                Interactive = true,
                KeepOpen = true,
                Shell = "powershell",
                Quiet = true,
                Into = "date2"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        startedFileName.Should().Be("powershell.exe");
        context.GetVariable("date2_exit_code").Should().Be(0);
    }

    [Fact]
    public async Task Interactive_Detached_ReturnsImmediatelyWithoutWaitingAndSetsStartupMetadata()
    {
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            processId: 515,
            waitForExit: _ => throw new InvalidOperationException("Detached interactive should not wait for exit."));
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "date",
                Confirm = "never",
                Interactive = true,
                KeepOpen = true,
                Lifetime = "detached",
                LifetimeSpecified = true,
                FailOnNonZero = true,
                Into = "session"
            }
        };

        var context = new ScriptContext();
        var outputs = new List<string>();
        context.OutputReceived += (_, e) => outputs.Add(e.Message);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().ContainEquivalentOf("detached mode");
        context.GetVariable("session_pid").Should().Be(515);
        context.GetVariable("session_started").Should().Be(true);
        context.GetVariableString("session_start_error").Should().BeEmpty();
        context.GetVariable("session_exit_code").Should().BeNull();
        outputs.Should().Contain(line => line.Contains("fail_on_nonzero", StringComparison.OrdinalIgnoreCase));
        runner.LastHandle.Should().NotBeNull();
        runner.LastHandle!.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Interactive_KeepOpen_HonorsWorkingDirectory()
    {
        string? startedWorkingDirectory = null;
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            onStart: info => startedWorkingDirectory = info.WorkingDirectory);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "date",
                Confirm = "never",
                Interactive = true,
                KeepOpen = true,
                Shell = "powershell",
                WorkingDir = "C:\\"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        startedWorkingDirectory.Should().Be("C:\\");
    }

    [Fact]
    public async Task Interactive_KeepOpen_PowerShell_UsesSessionTranscriptCapture()
    {
        string? startedArguments = null;
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            onStart: info => startedArguments = info.Arguments);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "ping 8.8.8.8",
                Confirm = "never",
                Interactive = true,
                KeepOpen = true,
                Shell = "powershell",
                Quiet = true
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var decoded = NormalizePowerShellCommandForAssertions(startedArguments);
        decoded.Should().Contain("Start-Transcript");
        decoded.Should().Contain("-Path");
        decoded.Should().NotContain("Tee-Object");
    }

    [Fact]
    public async Task Interactive_WindowCloseExitCode_DoesNotFail_WhenFailOnNonZeroTrue()
    {
        const int windowClosedExitCode = unchecked((int)0xC000013A);
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: windowClosedExitCode);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "ping 8.8.8.8",
                Confirm = "never",
                Interactive = true,
                KeepOpen = true,
                Shell = "powershell",
                FailOnNonZero = true,
                Into = "interactive_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("interactive_result_exit_code").Should().Be(windowClosedExitCode);

        var sessions = context.GetInteractiveSessionsSnapshot();
        sessions.Should().HaveCount(1);
        sessions[0].CloseReason.Should().Be("user_closed_window");
    }

    [Fact]
    public async Task Interactive_NonZeroExitCode_FailsWhenFailOnNonZeroTrue()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 5);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "exit 5",
                Confirm = "never",
                Interactive = true,
                FailOnNonZero = true,
                Into = "interactive_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        context.GetVariable("interactive_result_exit_code").Should().Be(5);
    }

    [Fact]
    public async Task Interactive_NonZeroExitCode_OnErrorContinue_ReturnsSuppressedAndCapturesExitCode()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 5);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            OnError = "continue",
            LocalCmd = new LocalCmdOptions
            {
                Command = "exit 5",
                Confirm = "never",
                Interactive = true,
                FailOnNonZero = true,
                Into = "interactive_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariable("interactive_result_exit_code").Should().Be(5);
    }

    [Fact]
    public async Task Interactive_PowerShell_CapturesAuditTranscriptIntoInteractiveSessions()
    {
        string? startedArguments = null;
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            onStart: info => startedArguments = info.Arguments,
            waitForExit: _ =>
            {
                var transcriptPath = TryExtractInteractiveAuditTranscriptPath(startedArguments);
                transcriptPath.Should().NotBeNullOrWhiteSpace();
                File.WriteAllText(transcriptPath!, "audit-line-1\naudit-line-2");
                return Task.FromResult(0);
            });
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "date",
                Confirm = "never",
                Interactive = true,
                Shell = "powershell",
                Quiet = true,
                Into = "audit_result"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("audit_result_exit_code").Should().Be(0);

        var sessions = context.GetInteractiveSessionsSnapshot();
        sessions.Should().HaveCount(1);
        sessions[0].SessionMode.Should().Be("localcmd-interactive");
        sessions[0].EmulationMode.Should().Be("powershell");
        sessions[0].Transcript.Should().Contain("audit-line-1");
        sessions[0].Transcript.Should().Contain("audit-line-2");

        var transcriptPathAfter = TryExtractInteractiveAuditTranscriptPath(startedArguments);
        transcriptPathAfter.Should().NotBeNullOrWhiteSpace();
        File.Exists(transcriptPathAfter!).Should().BeFalse();
    }

    [Fact]
    public async Task Interactive_PowerShell_QuotedExecutablePath_UsesCallOperatorInAuditWrapper()
    {
        string? startedArguments = null;
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            onStart: info => startedArguments = info.Arguments);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "'C:\\Program Files\\Git\\usr\\bin\\bash.exe' -l -i -c 'echo hi'",
                Confirm = "never",
                Interactive = true,
                KeepOpen = true,
                Shell = "powershell",
                Quiet = true
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var decoded = NormalizePowerShellCommandForAssertions(startedArguments);
        decoded.Should().Contain("& { & 'C:\\Program Files\\Git\\usr\\bin\\bash.exe' -l -i -c 'echo hi' }");
    }

    [Fact]
    public async Task Interactive_Cmd_WrapsCommandForAuditCapture()
    {
        string? startedArguments = null;
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            onStart: info => startedArguments = info.Arguments);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "date /t",
                Confirm = "never",
                Interactive = true,
                Shell = "cmd",
                Quiet = true
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        startedArguments.Should().Contain("-EncodedCommand");
        startedArguments.Should().NotContain("Tee-Object");

        var decodedAuditCommand = DecodePowerShellEncodedCommand(startedArguments!);
        decodedAuditCommand.Should().Contain("$ProgressPreference = 'SilentlyContinue';");
        decodedAuditCommand.Should().Contain("Tee-Object");
        decodedAuditCommand.Should().Contain("-FilePath");
    }

    [Fact]
    public async Task Confirmation_Cancel_AbortsExecution()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0);
        var confirmation = new StubConfirmation(LocalCmdConfirmResult.Cancel);
        var command = new LocalCmdCommand(confirmation, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "dangerous-cmd",
                Confirm = "always"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        runner.StartCount.Should().Be(0);
    }

    [Fact]
    public async Task Confirmation_RunAll_SetsContextFlag()
    {
        var runner = new StubProcessRunner(stdout: "ok", stderr: "", exitCode: 0);
        var confirmation = new StubConfirmation(LocalCmdConfirmResult.RunAll);
        var command = new LocalCmdCommand(confirmation, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "safe-cmd",
                Confirm = "always",
                Into = "r"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.LocalCmdRunAllApproved.Should().BeTrue();
    }

    [Fact]
    public async Task Confirmation_CancelledByExecutionToken_ThrowsWithoutStartingProcess()
    {
        var runner = new StubProcessRunner(stdout: "ok", stderr: "", exitCode: 0);
        var confirmationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var confirmation = new StubConfirmation(async (_, _, _, cancellationToken) =>
        {
            confirmationStarted.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return LocalCmdConfirmResult.Run;
        });
        var command = new LocalCmdCommand(confirmation, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "safe-cmd",
                Confirm = "always"
            }
        };

        var context = new ScriptContext();
        using var cts = new CancellationTokenSource();
        var executeTask = command.ExecuteAsync(step, context, cts.Token);

        await confirmationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await executeTask);
        runner.StartCount.Should().Be(0);
    }

    [Fact]
    public async Task Confirmation_RequiredPolicyWithoutProvider_Fails()
    {
        var runner = new StubProcessRunner(stdout: "ok", stderr: "", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "safe-cmd",
                Confirm = "always"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().ContainEquivalentOf("confirmation");
        runner.StartCount.Should().Be(0);
    }

    [Fact]
    public async Task Foreground_Timeout_ReturnsFailure()
    {
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            waitForExit: async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return 0;
            });
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            Timeout = 1,
            LocalCmd = new LocalCmdOptions
            {
                Command = "long-running-command",
                Confirm = "never"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().ContainEquivalentOf("timed out");
    }

    [Fact]
    public async Task Foreground_MaxOutputBytes_AppendsTruncationMarker()
    {
        var runner = new StubProcessRunner(stdout: "abcdefghijklmnopqrstuvwxyz", stderr: "", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "echo long-output",
                Confirm = "never",
                Into = "result",
                MaxOutputBytes = 8
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("result_stdout").Should().ContainEquivalentOf("truncated");
    }

    [Fact]
    public async Task Foreground_Quiet_HidesCommandEchoButStreamsOutput()
    {
        var runner = new StubProcessRunner(stdout: "hello world", stderr: "", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "echo hello world",
                Confirm = "never",
                Quiet = true,
                Into = "result"
            }
        };

        var context = new ScriptContext();
        var outputs = new List<string>();
        context.OutputReceived += (_, e) => outputs.Add(e.Message);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        outputs.Should().NotContain(line => line.Contains("[localcmd]", StringComparison.OrdinalIgnoreCase));
        outputs.Should().Contain(line => line.Contains("hello world", StringComparison.Ordinal));
        context.GetVariableString("result_stdout").Should().Be("hello world");
    }

    [Fact]
    public async Task Foreground_Suppress_HidesCommandEchoAndLiveOutputButStillCaptures()
    {
        var runner = new StubProcessRunner(stdout: "hello world", stderr: "warn line", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "echo hello world",
                Confirm = "never",
                Suppress = true,
                Into = "result"
            }
        };

        var context = new ScriptContext();
        var outputs = new List<string>();
        context.OutputReceived += (_, e) => outputs.Add(e.Message);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        outputs.Should().NotContain(line => line.Contains("[localcmd]", StringComparison.OrdinalIgnoreCase));
        outputs.Should().NotContain(line => line.Contains("hello world", StringComparison.Ordinal));
        outputs.Should().NotContain(line => line.Contains("warn line", StringComparison.Ordinal));
        context.GetVariableString("result_stdout").Should().Be("hello world");
        context.GetVariableString("result_stderr").Should().Be("warn line");
    }

    [Fact]
    public async Task Background_ScriptLifetime_IsKilledDuringCleanup()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0, processId: 77);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "bg.exe",
                Confirm = "never",
                RunMode = "background",
                Lifetime = "script"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        runner.LastHandle.Should().NotBeNull();
        runner.LastHandle!.KillCalled.Should().BeFalse();

        LocalCmdCommand.CleanupTrackedBackgroundProcesses(context, onCancel: false);

        runner.LastHandle!.KillCalled.Should().BeTrue();
        runner.LastHandle!.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Background_AppLifetime_KillOnCancel_True_KillsProcessOnCancelledCleanup()
    {
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0, processId: 78);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "bg.exe",
                Confirm = "never",
                RunMode = "background",
                Lifetime = "app",
                KillOnCancel = true
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        runner.LastHandle.Should().NotBeNull();
        runner.LastHandle!.KillCalled.Should().BeFalse();

        LocalCmdCommand.CleanupTrackedBackgroundProcesses(context, onCancel: true);

        runner.LastHandle!.KillCalled.Should().BeTrue();
        runner.LastHandle!.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task Background_Detached_IgnoresPidAndDisposeMetadataErrors()
    {
        var runner = new StubProcessRunner(
            stdout: "",
            stderr: "",
            exitCode: 0,
            processId: 79,
            throwOnIdAccess: true,
            throwOnDispose: true);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "bg.exe",
                Confirm = "never",
                RunMode = "background",
                Lifetime = "detached",
                Into = "bg"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().ContainEquivalentOf("unknown");
        context.GetVariable("bg_pid").Should().Be(-1);
        context.GetVariable("bg_started").Should().Be(true);
    }

    [Fact]
    public void BuildProcessArgs_Powershell_UsesNoProfileAndPrependsProgressSuppression()
    {
        var options = new LocalCmdOptions { Shell = "powershell" };
        var (fileName, args) = LocalCmdCommand.BuildProcessArgs("Get-Process", "powershell", options);
        var decoded = DecodePowerShellEncodedCommand(args);

        fileName.Should().Be("powershell.exe");
        args.Should().Contain("-NoProfile");
        args.Should().Contain("-EncodedCommand");
        decoded.Should().Be("$ProgressPreference = 'SilentlyContinue'; Get-Process");
    }

    [Fact]
    public void BuildProcessArgs_Powershell_QuotedExecutablePath_ExecutesDirectly()
    {
        var options = new LocalCmdOptions { Shell = "powershell" };
        var command = "'C:\\Program Files\\Git\\usr\\bin\\bash.exe' -l -i -c 'echo hi'";

        var (fileName, args) = LocalCmdCommand.BuildProcessArgs(command, "powershell", options);

        fileName.Should().Be("C:\\Program Files\\Git\\usr\\bin\\bash.exe");
        args.Should().Be("-l -i -c 'echo hi'");
    }

    [Fact]
    public void BuildProcessArgs_Cmd()
    {
        var options = new LocalCmdOptions { Shell = "cmd" };
        var (fileName, args) = LocalCmdCommand.BuildProcessArgs("dir", "cmd", options);

        fileName.Should().Be("cmd.exe");
        args.Should().Contain("/c");
        args.Should().Contain("dir");
    }

    [Fact]
    public void BuildProcessArgs_Cmd_WithArgs_IncludesCustomArguments()
    {
        var options = new LocalCmdOptions { Shell = "cmd", Args = new List<string> { "/d", "/q" } };
        var (fileName, args) = LocalCmdCommand.BuildProcessArgs("dir", "cmd", options);

        fileName.Should().Be("cmd.exe");
        args.Should().Contain("/d /q");
        args.Should().Contain("/c");
        args.Should().Contain("dir");
    }

    [Fact]
    public void BuildProcessArgs_Powershell_QuotesArgsContainingSpaces()
    {
        var options = new LocalCmdOptions
        {
            Shell = "powershell",
            Args = new List<string> { "-ExecutionPolicy", "Remote Signed" }
        };

        var (fileName, args) = LocalCmdCommand.BuildProcessArgs("Get-Date", "powershell", options);

        fileName.Should().Be("powershell.exe");
        args.Should().Contain("-ExecutionPolicy");
        args.Should().Contain("\"Remote Signed\"");
    }

    [Fact]
    public void BuildProcessArgs_Custom_QuotesArgsContainingSpaces()
    {
        var options = new LocalCmdOptions
        {
            Shell = "custom",
            ShellPath = "python",
            Args = new List<string> { "-m", "my module" }
        };

        var (fileName, args) = LocalCmdCommand.BuildProcessArgs("script.py", "custom", options);

        fileName.Should().Be("python");
        args.Should().Contain("-m");
        args.Should().Contain("\"my module\"");
    }

    [Fact]
    public void BuildProcessArgs_Custom()
    {
        var options = new LocalCmdOptions { Shell = "custom", ShellPath = "python" };
        var (fileName, args) = LocalCmdCommand.BuildProcessArgs("script.py", "custom", options);

        fileName.Should().Be("python");
        args.Should().Contain("script.py");
    }

    [Fact]
    public void BuildInteractiveKeepOpenArgs_Powershell_UsesNoExit()
    {
        var options = new LocalCmdOptions { Shell = "powershell" };
        var (fileName, args) = LocalCmdCommand.BuildInteractiveKeepOpenArgs("Get-Date", "powershell", options);
        var decoded = DecodePowerShellEncodedCommand(args);

        fileName.Should().Be("powershell.exe");
        args.Should().Contain("-NoExit");
        args.Should().Contain("-EncodedCommand");
        decoded.Should().Be("Get-Date");
    }

    [Fact]
    public void BuildInteractiveKeepOpenArgs_Powershell_QuotedExecutablePath_AddsCallOperator()
    {
        var options = new LocalCmdOptions { Shell = "powershell" };
        var command = "'C:\\Program Files\\Git\\usr\\bin\\bash.exe' -l -i -c 'echo hi'";

        var (fileName, args) = LocalCmdCommand.BuildInteractiveKeepOpenArgs(command, "powershell", options);
        var decoded = DecodePowerShellEncodedCommand(args);

        fileName.Should().Be("powershell.exe");
        args.Should().Contain("-NoExit");
        decoded.Should().Be("& 'C:\\Program Files\\Git\\usr\\bin\\bash.exe' -l -i -c 'echo hi'");
    }

    [Fact]
    public void BuildInteractiveKeepOpenArgs_Cmd_UsesKeepWindowFlag()
    {
        var options = new LocalCmdOptions { Shell = "cmd" };
        var (fileName, args) = LocalCmdCommand.BuildInteractiveKeepOpenArgs("dir", "cmd", options);

        fileName.Should().Be("cmd.exe");
        args.Should().Contain("/K");
    }

    [Fact]
    public void BuildInteractiveKeepOpenArgs_Cmd_WithArgs_IncludesCustomArguments()
    {
        var options = new LocalCmdOptions { Shell = "cmd", Args = new List<string> { "/q" } };
        var (fileName, args) = LocalCmdCommand.BuildInteractiveKeepOpenArgs("dir", "cmd", options);

        fileName.Should().Be("cmd.exe");
        args.Should().Contain("/q");
        args.Should().Contain("/K");
    }

    [Fact]
    public void BuildInteractiveArgs_KeepOpen_Powershell_BypassesWindowsTerminalLauncher()
    {
        var options = new LocalCmdOptions { Shell = "powershell", KeepOpen = true };
        var (fileName, args) = LocalCmdCommand.BuildInteractiveArgs(
            "Get-Date",
            "powershell",
            options,
            null,
            "Test Title");

        fileName.Should().Be("powershell.exe");
        args.Should().Contain("-NoExit");
    }

    [Fact]
    public void BuildInteractiveArgs_KeepOpen_PowerShellExeAlias_UsesNoExit()
    {
        var options = new LocalCmdOptions { Shell = "powershell.exe", KeepOpen = true };
        var (fileName, args) = LocalCmdCommand.BuildInteractiveArgs(
            "Get-Date",
            "powershell.exe",
            options,
            null,
            "Test Title");

        fileName.Should().Be("powershell.exe");
        args.Should().Contain("-NoExit");
    }

    [Fact]
    public void BuildInteractiveArgs_NonKeepOpen_Powershell_UsesDirectShellProcessForReliableCapture()
    {
        var options = new LocalCmdOptions { Shell = "powershell", KeepOpen = false };
        var (fileName, args) = LocalCmdCommand.BuildInteractiveArgs(
            "Get-Date",
            "powershell",
            options,
            null,
            "Test Title");
        var decoded = DecodePowerShellEncodedCommand(args);

        fileName.Should().Be("powershell.exe");
        args.Should().Contain("-NoProfile");
        args.Should().Contain("-EncodedCommand");
        decoded.Should().Be("$ProgressPreference = 'SilentlyContinue'; Get-Date");
    }

    [Fact]
    public async Task Foreground_CaptureProperty_RecordsOutput()
    {
        var runner = new StubProcessRunner(stdout: "captured text", stderr: "", exitCode: 0);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            Capture = "my_output",
            LocalCmd = new LocalCmdOptions
            {
                Command = "echo captured text",
                Confirm = "never"
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("my_output").Should().Be("captured text");
    }

    [Fact]
    public async Task VariableSubstitution_InCommand()
    {
        string? capturedCommand = null;
        var runner = new StubProcessRunner(stdout: "", stderr: "", exitCode: 0,
            onStart: info => capturedCommand = info.Arguments);
        var command = new LocalCmdCommand(null, runner);

        var step = new ScriptStep
        {
            LocalCmd = new LocalCmdOptions
            {
                Command = "ping {{Host_IP}}",
                Confirm = "never"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("Host_IP", "10.0.0.1");
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var normalizedCommand = NormalizePowerShellCommandForAssertions(capturedCommand);
        normalizedCommand.Should().Contain("10.0.0.1");
    }

    private static string? TryExtractInteractiveAuditTranscriptPath(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return null;

        var normalizedArguments = NormalizePowerShellCommandForAssertions(arguments);

        foreach (var marker in new[] { "-FilePath ''", "-Path ''", "-FilePath '", "-Path '" })
        {
            var markerIndex = normalizedArguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
                continue;

            var start = markerIndex + marker.Length;
            var endToken = marker.EndsWith("''", StringComparison.Ordinal) ? "''" : "'";
            var end = normalizedArguments.IndexOf(endToken, start, StringComparison.Ordinal);
            if (end > start)
                return normalizedArguments[start..end].Replace("''", "'", StringComparison.Ordinal);
        }

        return null;
    }

    private static string NormalizePowerShellCommandForAssertions(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return string.Empty;

        return arguments.Contains("-EncodedCommand", StringComparison.OrdinalIgnoreCase)
            ? DecodePowerShellEncodedCommand(arguments)
            : arguments;
    }

    private static string DecodePowerShellEncodedCommand(string arguments)
    {
        var marker = "-EncodedCommand ";
        var markerIndex = arguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        markerIndex.Should().BeGreaterThanOrEqualTo(0);

        var encoded = arguments[(markerIndex + marker.Length)..].TrimStart();
        var terminatorIndex = encoded.IndexOfAny([' ', '"']);
        if (terminatorIndex >= 0)
            encoded = encoded[..terminatorIndex];

        var bytes = Convert.FromBase64String(encoded);
        return System.Text.Encoding.Unicode.GetString(bytes);
    }
}

internal class StubProcessRunner : IProcessRunner
{
    private readonly string _stdout;
    private readonly string _stderr;
    private readonly int _exitCode;
    private readonly int _processId;
    private readonly Action<ProcessStartInfo>? _onStart;
    private readonly Func<CancellationToken, Task<int>>? _waitForExit;
    private readonly bool _throwOnIdAccess;
    private readonly bool _throwOnDispose;

    public int StartCount { get; private set; }
    public StubProcessHandle? LastHandle { get; private set; }

    public StubProcessRunner(string stdout, string stderr, int exitCode,
        int processId = 100,
        Action<ProcessStartInfo>? onStart = null,
        Func<CancellationToken, Task<int>>? waitForExit = null,
        bool throwOnIdAccess = false,
        bool throwOnDispose = false)
    {
        _stdout = stdout;
        _stderr = stderr;
        _exitCode = exitCode;
        _processId = processId;
        _onStart = onStart;
        _waitForExit = waitForExit;
        _throwOnIdAccess = throwOnIdAccess;
        _throwOnDispose = throwOnDispose;
    }

    public IProcessHandle Start(ProcessStartInfo startInfo)
    {
        StartCount++;
        _onStart?.Invoke(startInfo);
        LastHandle = new StubProcessHandle(_stdout, _stderr, _exitCode, _processId,
            startInfo.RedirectStandardOutput, startInfo.RedirectStandardError, _waitForExit,
            _throwOnIdAccess, _throwOnDispose);
        return LastHandle;
    }
}

internal class StubProcessHandle : IProcessHandle
{
    private readonly string _stdout;
    private readonly string _stderr;
    private readonly int _exitCode;
    private readonly bool _redirectOut;
    private readonly bool _redirectErr;
    private readonly Func<CancellationToken, Task<int>>? _waitForExit;
    private readonly int _processId;
    private readonly bool _throwOnIdAccess;
    private readonly bool _throwOnDispose;

    public bool Disposed { get; private set; }
    public bool KillCalled { get; private set; }
    public bool HasExited { get; private set; }

    public int Id
    {
        get
        {
            if (_throwOnIdAccess)
                throw new InvalidOperationException("No process is associated with this object.");
            return _processId;
        }
    }

    public event DataReceivedEventHandler? OutputDataReceived;
    public event DataReceivedEventHandler? ErrorDataReceived;

    public StubProcessHandle(string stdout, string stderr, int exitCode, int processId,
        bool redirectOut, bool redirectErr, Func<CancellationToken, Task<int>>? waitForExit,
        bool throwOnIdAccess, bool throwOnDispose)
    {
        _stdout = stdout;
        _stderr = stderr;
        _exitCode = exitCode;
        _processId = processId;
        _redirectOut = redirectOut;
        _redirectErr = redirectErr;
        _waitForExit = waitForExit;
        _throwOnIdAccess = throwOnIdAccess;
        _throwOnDispose = throwOnDispose;
    }

    public void BeginOutputReadLine()
    {
        if (!_redirectOut) return;
        foreach (var line in _stdout.Split('\n'))
        {
            OutputDataReceived?.Invoke(this,
                CreateDataReceivedEventArgs(line.TrimEnd('\r')));
        }
        OutputDataReceived?.Invoke(this, CreateDataReceivedEventArgs(null));
    }

    public void BeginErrorReadLine()
    {
        if (!_redirectErr) return;
        if (!string.IsNullOrEmpty(_stderr))
        {
            foreach (var line in _stderr.Split('\n'))
            {
                ErrorDataReceived?.Invoke(this,
                    CreateDataReceivedEventArgs(line.TrimEnd('\r')));
            }
        }
        ErrorDataReceived?.Invoke(this, CreateDataReceivedEventArgs(null));
    }

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken)
    {
        if (_waitForExit != null)
            return _waitForExit(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        HasExited = true;
        return Task.FromResult(_exitCode);
    }

    public void Kill(bool entireProcessTree = true)
    {
        KillCalled = true;
        HasExited = true;
    }

    public void Dispose()
    {
        Disposed = true;
        if (_throwOnDispose)
            throw new InvalidOperationException("No process is associated with this object.");
    }

    #pragma warning disable SYSLIB0050
    private static DataReceivedEventArgs CreateDataReceivedEventArgs(string? data)
    {
        var args = (DataReceivedEventArgs)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(DataReceivedEventArgs));

        var field = typeof(DataReceivedEventArgs).GetField("_data",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(args, data);

        return args;
    }
    #pragma warning restore SYSLIB0050
}

internal class StubConfirmation : ILocalCmdConfirmation
{
    private readonly Func<string, string, string, CancellationToken, Task<LocalCmdConfirmResult>> _handler;

    public StubConfirmation(LocalCmdConfirmResult result)
    {
        _handler = (_, _, _, _) => Task.FromResult(result);
    }

    public StubConfirmation(Func<string, string, string, CancellationToken, Task<LocalCmdConfirmResult>> handler)
    {
        _handler = handler;
    }

    public Task<LocalCmdConfirmResult> ConfirmAsync(string resolvedCommand, string shell, string workingDir, CancellationToken cancellationToken)
    {
        return _handler(resolvedCommand, shell, workingDir, cancellationToken);
    }
}
