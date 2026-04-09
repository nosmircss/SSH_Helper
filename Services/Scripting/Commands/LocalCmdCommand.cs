using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Terminal;

namespace SSH_Helper.Services.Scripting.Commands
{
    public class LocalCmdCommand : IScriptCommand
    {
        private const int DefaultMaxOutputBytes = 1024 * 1024;
        private const int UserClosedInteractiveWindowExitCode = unchecked((int)0xC000013A);
        private const string TruncationMarker = "[localcmd] output truncated due to max_output_bytes limit";

        private static readonly object BackgroundProcessLock = new();
        private static readonly Dictionary<object, List<TrackedBackgroundProcess>> BackgroundProcessesByContext = new();
        private static readonly HashSet<TrackedBackgroundProcess> AppLifetimeBackgroundProcesses = new();

        private readonly ILocalCmdConfirmation? _confirmation;
        private readonly IProcessRunner _processRunner;

        static LocalCmdCommand()
        {
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupAppLifetimeProcesses();
        }

        public LocalCmdCommand(ILocalCmdConfirmation? confirmation = null)
            : this(confirmation, new SystemProcessRunner())
        {
        }

        internal LocalCmdCommand(ILocalCmdConfirmation? confirmation, IProcessRunner processRunner)
        {
            _confirmation = confirmation;
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.LocalCmd == null)
                return CommandResult.Fail("LocalCmd command has no options");

            var options = step.LocalCmd;
            var command = context.SubstituteVariables(options.Command ?? string.Empty).Trim();
            var suppressOutput = options.Suppress || step.Suppress;
            var suppressCommandEcho = suppressOutput || options.Quiet;

            if (string.IsNullOrWhiteSpace(command))
                return CommandResult.ApplyOnError(step, "LocalCmd requires a 'command'");

            if (options.Interactive && string.Equals(options.RunMode, "background", StringComparison.OrdinalIgnoreCase))
                return CommandResult.ApplyOnError(step, "interactive: true and run_mode: background are mutually exclusive");

            var shell = NormalizeShell(options.Shell);
            var workingDir = string.IsNullOrWhiteSpace(options.WorkingDir)
                ? null
                : context.SubstituteVariables(options.WorkingDir);

            LocalCmdConfirmResult confirmResult;
            try
            {
                confirmResult = await HandleConfirmation(options, command, shell, workingDir, context, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return CommandResult.ApplyOnError(step, ex.Message);
            }

            if (confirmResult == LocalCmdConfirmResult.Cancel)
                return CommandResult.Fail("LocalCmd execution cancelled by user");

            if (options.Interactive)
                return await ExecuteInteractive(
                    step,
                    command,
                    shell,
                    options,
                    workingDir,
                    context,
                    cancellationToken,
                    suppressCommandEcho);

            if (string.Equals(options.RunMode, "background", StringComparison.OrdinalIgnoreCase))
                return await ExecuteBackground(step, command, shell, options, workingDir, context, suppressCommandEcho);

            return await ExecuteForeground(
                step,
                command,
                shell,
                options,
                workingDir,
                context,
                cancellationToken,
                suppressCommandEcho,
                suppressOutput);
        }

        private async Task<LocalCmdConfirmResult> HandleConfirmation(
            LocalCmdOptions options, string command, string shell, string? workingDir, ScriptContext context, CancellationToken cancellationToken)
        {
            var confirmPolicy = options.Confirm?.ToLowerInvariant() ?? "always";

            if (confirmPolicy == "never")
                return LocalCmdConfirmResult.Run;

            var currentHost = context.CurrentHost?.IpAddress ?? "(no host)";

            if (context.LocalCmdRunAllApproved &&
                context.LocalCmdApprovedHost == currentHost &&
                context.LocalCmdApprovedCommands.Contains(command))
            {
                return LocalCmdConfirmResult.Run;
            }

            if (confirmPolicy == "once" && context.LocalCmdApprovedCommands.Contains(command) &&
                context.LocalCmdApprovedHost == currentHost)
            {
                return LocalCmdConfirmResult.Run;
            }

            if (_confirmation == null)
                throw new InvalidOperationException(
                    "LocalCmd confirmation is required but no confirmation provider is configured");

            var result = await _confirmation.ConfirmAsync(command, shell, workingDir ?? "(current directory)", cancellationToken);

            if (result == LocalCmdConfirmResult.RunAll)
            {
                context.LocalCmdRunAllApproved = true;
                context.LocalCmdApprovedHost = currentHost;
                context.LocalCmdApprovedCommands.Add(command);
            }
            else if (result == LocalCmdConfirmResult.Run)
            {
                context.LocalCmdApprovedHost = currentHost;
                context.LocalCmdApprovedCommands.Add(command);
            }

            return result;
        }

        private async Task<CommandResult> ExecuteForeground(
            ScriptStep step, string command, string shell, LocalCmdOptions options,
            string? workingDir, ScriptContext context, CancellationToken cancellationToken,
            bool suppressCommandEcho, bool suppressOutput)
        {
            var into = options.Into;
            var stdoutBuffer = new StringBuilder();
            var stderrBuffer = new StringBuilder();
            var maxBytes = options.MaxOutputBytes > 0 ? options.MaxOutputBytes : DefaultMaxOutputBytes;
            var stdoutBytes = 0;
            var stderrBytes = 0;
            var stdoutTruncated = false;
            var stderrTruncated = false;

            var (fileName, arguments) = BuildProcessArgs(command, shell, options);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrWhiteSpace(workingDir))
                startInfo.WorkingDirectory = Environment.ExpandEnvironmentVariables(workingDir);

            ApplyEnvironmentVariables(startInfo, options.Env, context);

            if (!suppressCommandEcho)
                context.EmitOutput(
                    $"[localcmd] {shell}: {FormatCommandForBanner(command)}",
                    ScriptOutputType.Command);

            IProcessHandle process;
            try
            {
                process = _processRunner.Start(startInfo);
            }
            catch (Exception ex)
            {
                return CommandResult.ApplyOnError(step, $"Failed to start process: {ex.Message}");
            }

            using (process)
            {
                void AppendCapturedOutputLine(
                    StringBuilder buffer,
                    string line,
                    ref int currentBytes,
                    ref bool truncated)
                {
                    if (truncated)
                        return;

                    var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
                    if (currentBytes + lineBytes <= maxBytes)
                    {
                        buffer.AppendLine(line);
                        currentBytes += lineBytes;
                        return;
                    }

                    truncated = true;
                    buffer.AppendLine(TruncationMarker);
                }

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    if (!suppressOutput)
                        context.EmitOutput(e.Data, ScriptOutputType.CommandOutput);
                    AppendCapturedOutputLine(stdoutBuffer, e.Data, ref stdoutBytes, ref stdoutTruncated);
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    if (!suppressOutput)
                        context.EmitOutput(e.Data, ScriptOutputType.Warning);
                    AppendCapturedOutputLine(stderrBuffer, e.Data, ref stderrBytes, ref stderrTruncated);
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timeoutSeconds = step.Timeout.GetValueOrDefault();
                using var timeoutCts = timeoutSeconds > 0
                    ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                    : null;
                if (timeoutCts != null)
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                int exitCode;
                try
                {
                    exitCode = await process.WaitForExitAsync(timeoutCts?.Token ?? cancellationToken);
                }
                catch (OperationCanceledException) when (
                    timeoutCts is { IsCancellationRequested: true } &&
                    !cancellationToken.IsCancellationRequested)
                {
                    return CommandResult.ApplyOnError(step, $"LocalCmd timed out after {timeoutSeconds} seconds");
                }

                var stdout = stdoutBuffer.ToString().TrimEnd('\r', '\n');
                var stderr = stderrBuffer.ToString().TrimEnd('\r', '\n');

                if (!string.IsNullOrWhiteSpace(into))
                {
                    context.SetVariable($"{into}_stdout", stdout);
                    context.SetVariable($"{into}_stderr", stderr);
                    context.SetVariable($"{into}_exit_code", exitCode);
                }

                if (!string.IsNullOrWhiteSpace(step.Capture))
                    context.RecordCommandOutput(stdout, step.Capture);

                var successCodes = options.SuccessCodes ?? new List<int> { 0 };
                if (successCodes.Count == 0)
                    successCodes = new List<int> { 0 };

                if (options.FailOnNonZero && !successCodes.Contains(exitCode))
                {
                    var msg = $"LocalCmd exited with code {exitCode} (expected: {string.Join(",", successCodes)})";
                    if (!string.IsNullOrEmpty(stderr))
                        msg += $"\nstderr: {stderr}";
                    return CommandResult.ApplyOnError(step, msg);
                }

                return CommandResult.Ok($"LocalCmd completed with exit code {exitCode}");
            }
        }

        private Task<CommandResult> ExecuteBackground(
            ScriptStep step, string command, string shell, LocalCmdOptions options,
            string? workingDir, ScriptContext context, bool suppressCommandEcho)
        {
            var into = options.Into;
            var (fileName, arguments) = BuildProcessArgs(command, shell, options);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = true,
            };

            if (!string.IsNullOrWhiteSpace(workingDir))
                startInfo.WorkingDirectory = Environment.ExpandEnvironmentVariables(workingDir);

            ApplyEnvironmentVariables(startInfo, options.Env, context);

            if (!suppressCommandEcho)
                context.EmitOutput(
                    $"[localcmd:background] {shell}: {FormatCommandForBanner(command)}",
                    ScriptOutputType.Command);

            IProcessHandle process;
            try
            {
                process = _processRunner.Start(startInfo);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(into))
                {
                    context.SetVariable($"{into}_pid", -1);
                    context.SetVariable($"{into}_started", false);
                    context.SetVariable($"{into}_start_error", ex.Message);
                }
                return Task.FromResult(CommandResult.ApplyOnError(step, $"Failed to start background process: {ex.Message}"));
            }

            int? processId = null;
            try
            {
                processId = process.Id;
            }
            catch
            {
                // PID can be unavailable for some process handles; startup still succeeded.
            }

            if (!string.IsNullOrWhiteSpace(into))
            {
                context.SetVariable($"{into}_pid", processId ?? -1);
                context.SetVariable($"{into}_started", true);
                context.SetVariable($"{into}_start_error", string.Empty);
            }

            var lifetime = NormalizeLifetime(options.Lifetime);
            if (string.Equals(lifetime, "detached", StringComparison.OrdinalIgnoreCase))
            {
                TryDispose(process);
            }
            else
            {
                RegisterBackgroundProcess(
                    context,
                    process,
                    lifetime,
                    options.KillOnCancel);
            }

            var pidLabel = processId?.ToString() ?? "unknown";
            return Task.FromResult(CommandResult.Ok($"Background process started (PID: {pidLabel})"));
        }

        private async Task<CommandResult> ExecuteInteractive(
            ScriptStep step, string command, string shell, LocalCmdOptions options,
            string? workingDir, ScriptContext context, CancellationToken cancellationToken,
            bool suppressCommandEcho)
        {
            var title = string.IsNullOrWhiteSpace(options.Title)
                ? "Local Command"
                : context.SubstituteVariables(options.Title);
            var startedAtUtc = DateTime.UtcNow;
            var hostAddress = context.CurrentHost?.ToString() ?? string.Empty;
            var lifetime = NormalizeLifetime(options.Lifetime);
            var detachedInteractive = options.LifetimeSpecified &&
                                      string.Equals(lifetime, "detached", StringComparison.OrdinalIgnoreCase);
            var auditCapture = PrepareInteractiveAuditCapture(command, shell, options.KeepOpen, captureEnabled: !detachedInteractive);

            if (!suppressCommandEcho)
                context.EmitOutput(
                    $"[localcmd:interactive] Opening terminal: {FormatCommandForBanner(command)}",
                    ScriptOutputType.Command);

            var (fileName, arguments) = BuildInteractiveArgs(auditCapture.LaunchCommand, shell, options, workingDir, title);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = false,
            };

            if (!string.IsNullOrWhiteSpace(workingDir))
                startInfo.WorkingDirectory = Environment.ExpandEnvironmentVariables(workingDir);

            ApplyEnvironmentVariables(startInfo, options.Env, context);

            IProcessHandle process;
            try
            {
                process = _processRunner.Start(startInfo);
            }
            catch (Exception ex)
            {
                return CommandResult.ApplyOnError(step, $"Failed to launch interactive terminal: {ex.Message}");
            }

            if (detachedInteractive)
            {
                int? processId = null;
                try
                {
                    processId = process.Id;
                }
                catch
                {
                    // PID can be unavailable for some process handles; startup still succeeded.
                }

                if (!string.IsNullOrWhiteSpace(options.Into))
                {
                    context.SetVariable($"{options.Into}_pid", processId ?? -1);
                    context.SetVariable($"{options.Into}_started", true);
                    context.SetVariable($"{options.Into}_start_error", string.Empty);
                }

                if (options.FailOnNonZero)
                {
                    context.EmitOutput(
                        "[localcmd:interactive] Detached session launched; fail_on_nonzero cannot be evaluated because exit code is not tracked.",
                        ScriptOutputType.Warning);
                }

                TryDispose(process);
                var pidLabel = processId?.ToString() ?? "unknown";
                return CommandResult.Ok($"Interactive terminal launched in detached mode (PID: {pidLabel})");
            }

            try
            {
                using (process)
                {
                    var timeoutSeconds = step.Timeout.GetValueOrDefault();
                    using var timeoutCts = timeoutSeconds > 0
                        ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                        : null;
                    if (timeoutCts != null)
                        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    int exitCode;
                    try
                    {
                        exitCode = await process.WaitForExitAsync(timeoutCts?.Token ?? cancellationToken);
                    }
                    catch (OperationCanceledException) when (
                        timeoutCts is { IsCancellationRequested: true } &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        CaptureInteractiveAuditSession(
                            context,
                            hostAddress,
                            shell,
                            startedAtUtc,
                            DateTime.UtcNow,
                            "timeout",
                            completed: false,
                            auditCapture,
                            options.MaxOutputBytes);
                        return CommandResult.ApplyOnError(step, $"LocalCmd timed out after {timeoutSeconds} seconds");
                    }
                    catch (OperationCanceledException)
                    {
                        CaptureInteractiveAuditSession(
                            context,
                            hostAddress,
                            shell,
                            startedAtUtc,
                            DateTime.UtcNow,
                            "cancelled",
                            completed: false,
                            auditCapture,
                            options.MaxOutputBytes);
                        throw;
                    }

                    if (!string.IsNullOrWhiteSpace(options.Into))
                        context.SetVariable($"{options.Into}_exit_code", exitCode);

                    var successCodes = options.SuccessCodes ?? new List<int> { 0 };
                    if (successCodes.Count == 0)
                        successCodes = new List<int> { 0 };
                    var userClosedWindow = IsInteractiveWindowCloseExitCode(exitCode);
                    var closeReason = userClosedWindow
                        ? "user_closed_window"
                        : $"exit_code:{exitCode}";

                    CaptureInteractiveAuditSession(
                        context,
                        hostAddress,
                        shell,
                        startedAtUtc,
                        DateTime.UtcNow,
                        closeReason,
                        completed: true,
                        auditCapture,
                        options.MaxOutputBytes);

                    if (options.FailOnNonZero &&
                        !successCodes.Contains(exitCode) &&
                        !userClosedWindow)
                    {
                        var msg = $"LocalCmd exited with code {exitCode} (expected: {string.Join(",", successCodes)})";
                        return CommandResult.ApplyOnError(step, msg);
                    }

                    return CommandResult.Ok($"Interactive terminal closed (exit code: {exitCode})");
                }
            }
            finally
            {
                CleanupInteractiveAuditCapture(auditCapture);
            }
        }

        internal static (string fileName, string arguments) BuildProcessArgs(
            string command, string shell, LocalCmdOptions options)
        {
            var extraArgs = options.Args != null && options.Args.Count > 0
                ? JoinCommandLineArguments(options.Args)
                : string.Empty;

            if (IsPowerShellShell(shell))
            {
                if (TryParseQuotedExecutableInvocation(command, out var directFileName, out var directArguments))
                {
                    if (!string.IsNullOrWhiteSpace(extraArgs))
                        directArguments = string.IsNullOrWhiteSpace(directArguments)
                            ? extraArgs
                            : $"{extraArgs} {directArguments}";

                    return (directFileName, directArguments);
                }

                var psArgs = $"-NoLogo -NoProfile -NonInteractive -EncodedCommand {EncodePowerShellCommand(PrepareNonInteractivePowerShellCommand(command))}";
                if (!string.IsNullOrWhiteSpace(extraArgs))
                    psArgs = $"{extraArgs} {psArgs}";
                return (ResolvePowerShellExecutable(shell), psArgs);
            }

            if (IsCmdShell(shell))
                return (ResolveCmdExecutable(shell), BuildCmdArguments(command, extraArgs, keepOpen: false));

            if (string.Equals(shell, "custom", StringComparison.OrdinalIgnoreCase))
            {
                var shellPath = options.ShellPath ?? throw new InvalidOperationException(
                    "shell_path is required when shell is 'custom'");
                var customArgs = string.IsNullOrWhiteSpace(extraArgs)
                    ? command
                    : $"{extraArgs} {command}";
                return (shellPath, customArgs);
            }

            return (shell, command);
        }

        internal static (string fileName, string arguments) BuildInteractiveArgs(
            string command, string shell, LocalCmdOptions options, string? workingDir, string title)
        {
            if (options.KeepOpen &&
                (IsPowerShellShell(shell) || IsCmdShell(shell)))
            {
                // Launch keep-open sessions directly so we can track the shell process lifetime
                // instead of waiting on a transient wt.exe launcher process.
                return BuildInteractiveKeepOpenArgs(command, shell, options);
            }

            if (RequiresDirectInteractiveShellForReliableCapture(shell))
            {
                // localcmd interactive transcript capture for powershell/cmd is file-based.
                // Waiting on wt.exe is not a reliable proxy for command completion when wt
                // attaches to an existing window, so use a directly tracked shell process.
                return BuildProcessArgs(command, shell, options);
            }

            var wtPath = FindWindowsTerminal();

            if (wtPath != null)
            {
                var sb = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(workingDir))
                    sb.Append($"-d \"{workingDir}\" ");
                sb.Append($"--title \"{title}\" ");

                var (shellExe, shellArgs) = BuildProcessArgs(command, shell, options);
                sb.Append($"-- {shellExe}");
                if (!string.IsNullOrEmpty(shellArgs))
                    sb.Append($" {shellArgs}");

                return (wtPath, sb.ToString());
            }

            if (IsPowerShellShell(shell))
                return (ResolvePowerShellExecutable(shell), $"-NoLogo -NoProfile -EncodedCommand {EncodePowerShellCommand(PrepareNonInteractivePowerShellCommand(command))}");

            if (IsCmdShell(shell))
                return (ResolveCmdExecutable(shell), BuildCmdArguments(command, options.Args != null && options.Args.Count > 0
                    ? string.Join(" ", options.Args)
                    : string.Empty, keepOpen: false));

            var shellPath = options.ShellPath ?? shell;
            return (shellPath, command);
        }

        internal static (string fileName, string arguments) BuildInteractiveKeepOpenArgs(
            string command, string shell, LocalCmdOptions options)
        {
            var extraArgs = options.Args != null && options.Args.Count > 0
                ? JoinCommandLineArguments(options.Args)
                : string.Empty;

            if (IsPowerShellShell(shell))
            {
                var normalizedCommand = PreparePowerShellCommand(command);
                var psArgs = $"-NoLogo -NoExit -EncodedCommand {EncodePowerShellCommand(normalizedCommand)}";
                if (!string.IsNullOrWhiteSpace(extraArgs))
                    psArgs = $"{extraArgs} {psArgs}";
                return (ResolvePowerShellExecutable(shell), psArgs);
            }

            if (IsCmdShell(shell))
                return (ResolveCmdExecutable(shell), BuildCmdArguments(command, extraArgs, keepOpen: true));

            return BuildProcessArgs(command, shell, options);
        }

        private static string? FindWindowsTerminal()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var wtPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
            if (File.Exists(wtPath))
                return wtPath;

            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            foreach (var dir in pathDirs)
            {
                var candidate = Path.Combine(dir, "wt.exe");
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static string EncodePowerShellCommand(string command)
        {
            return Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        }

        private static string FormatCommandForBanner(string command)
        {
            return ScriptingHelpers.FormatForDisplay(command);
        }

        private static bool RequiresDirectInteractiveShellForReliableCapture(string shell)
        {
            return IsPowerShellShell(shell) || IsCmdShell(shell);
        }

        private static bool IsInteractiveWindowCloseExitCode(int exitCode)
        {
            return exitCode == UserClosedInteractiveWindowExitCode;
        }

        private static InteractiveAuditCapture PrepareInteractiveAuditCapture(
            string command,
            string shell,
            bool keepOpen,
            bool captureEnabled)
        {
            if (!captureEnabled)
                return new InteractiveAuditCapture(command, null);

            if (IsPowerShellShell(shell))
            {
                var transcriptPath = BuildInteractiveAuditTranscriptPath();
                var escapedPath = EscapeSingleQuotedPowerShellLiteral(transcriptPath);
                var launchCommand = BuildPowerShellInteractiveAuditCommand(command, escapedPath, keepOpen);
                return new InteractiveAuditCapture(launchCommand, transcriptPath);
            }

            if (IsCmdShell(shell))
            {
                var transcriptPath = BuildInteractiveAuditTranscriptPath();
                var escapedPath = EscapeSingleQuotedPowerShellLiteral(transcriptPath);
                var teeCommand = $"$ProgressPreference = 'SilentlyContinue'; $input | Tee-Object -FilePath '{escapedPath}' -Append";
                var launchCommand =
                    $"({command}) 2>&1 | powershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand {EncodePowerShellCommand(teeCommand)}";
                return new InteractiveAuditCapture(launchCommand, transcriptPath);
            }

            return new InteractiveAuditCapture(command, null);
        }

        private static string NormalizeShell(string? shell)
        {
            return string.IsNullOrWhiteSpace(shell) ? "powershell" : shell.Trim();
        }

        private static bool IsPowerShellShell(string shell)
        {
            if (string.IsNullOrWhiteSpace(shell))
                return false;

            var normalized = shell.Trim();
            return string.Equals(normalized, "powershell", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("\\powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/powershell.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCmdShell(string shell)
        {
            if (string.IsNullOrWhiteSpace(shell))
                return false;

            var normalized = shell.Trim();
            return string.Equals(normalized, "cmd", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("\\cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/cmd.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePowerShellExecutable(string shell)
        {
            var normalized = shell.Trim();
            if (string.Equals(normalized, "powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("\\powershell.exe", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/powershell.exe", StringComparison.OrdinalIgnoreCase))
                return normalized;

            return "powershell.exe";
        }

        private static string ResolveCmdExecutable(string shell)
        {
            var normalized = shell.Trim();
            if (string.Equals(normalized, "cmd", StringComparison.OrdinalIgnoreCase))
                return "cmd.exe";

            if (string.Equals(normalized, "cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("\\cmd.exe", StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith("/cmd.exe", StringComparison.OrdinalIgnoreCase))
                return normalized;

            return "cmd.exe";
        }

        private static string BuildPowerShellInteractiveAuditCommand(
            string command,
            string escapedTranscriptPath,
            bool keepOpen)
        {
            var normalizedCommand = PreparePowerShellCommand(command);

            if (keepOpen)
            {
                // Keep transcript active for the whole shell session so user-entered follow-up
                // commands are included in execution-details interactive history.
                return
                    $"try {{ Start-Transcript -Path '{escapedTranscriptPath}' -Append | Out-Null }} catch {{ }}; & {{ {normalizedCommand} }}";
            }

            return
                $"$__sshHelperTranscriptActive = $false; " +
                $"try {{ Start-Transcript -Path '{escapedTranscriptPath}' -Append | Out-Null; $__sshHelperTranscriptActive = $true }} catch {{ }}; " +
                $"try {{ & {{ {normalizedCommand} }} }} finally {{ if ($__sshHelperTranscriptActive) {{ try {{ Stop-Transcript | Out-Null }} catch {{ }} }} }}";
        }

        private static string PreparePowerShellCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return command;

            var trimmed = command.TrimStart();
            if (trimmed.StartsWith("&", StringComparison.Ordinal))
                return command;

            // Commands that start with a quoted executable path need a call operator in PowerShell.
            if (!StartsWithQuotedToken(trimmed))
                return command;

            return $"& {trimmed}";
        }

        private static string PrepareNonInteractivePowerShellCommand(string command)
        {
            var normalizedCommand = PreparePowerShellCommand(command);

            // Suppress startup/module initialization progress records so localcmd does not
            // surface PowerShell CLIXML progress noise on stderr for normal commands.
            return $"$ProgressPreference = 'SilentlyContinue'; {normalizedCommand}";
        }

        private static bool StartsWithQuotedToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            var quote = value[0];
            if (quote != '\'' && quote != '"')
                return false;

            for (var i = 1; i < value.Length; i++)
            {
                if (value[i] != quote)
                    continue;

                if (quote == '\'' && i + 1 < value.Length && value[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                if (quote == '"' && i > 0 && value[i - 1] == '`')
                    continue;

                return true;
            }

            return false;
        }

        private static bool TryParseQuotedExecutableInvocation(
            string command,
            out string fileName,
            out string arguments)
        {
            fileName = string.Empty;
            arguments = string.Empty;

            if (string.IsNullOrWhiteSpace(command))
                return false;

            var trimmed = command.TrimStart();

            if (trimmed.StartsWith("&", StringComparison.Ordinal))
                trimmed = trimmed.Substring(1).TrimStart();

            if (trimmed.Length < 3 || (trimmed[0] != '\'' && trimmed[0] != '"'))
                return false;

            var quote = trimmed[0];
            var tokenEnd = -1;

            for (var i = 1; i < trimmed.Length; i++)
            {
                if (trimmed[i] != quote)
                    continue;

                if (quote == '\'' && i + 1 < trimmed.Length && trimmed[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                if (quote == '"' && i > 0 && trimmed[i - 1] == '`')
                    continue;

                tokenEnd = i;
                break;
            }

            if (tokenEnd < 0)
                return false;

            var token = trimmed.Substring(1, tokenEnd - 1);
            token = quote == '\''
                ? token.Replace("''", "'", StringComparison.Ordinal)
                : token.Replace("`\"", "\"", StringComparison.Ordinal);

            if (!token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return false;

            fileName = token;
            arguments = trimmed.Substring(tokenEnd + 1).TrimStart();
            return true;
        }

        private static string BuildInteractiveAuditTranscriptPath()
        {
            return Path.Combine(
                Path.GetTempPath(),
                $"ssh_helper_localcmd_interactive_{Guid.NewGuid():N}.log");
        }

        private static string EscapeSingleQuotedPowerShellLiteral(string value)
        {
            return value.Replace("'", "''", StringComparison.Ordinal);
        }

        private static void CaptureInteractiveAuditSession(
            ScriptContext context,
            string hostAddress,
            string shell,
            DateTime startedAtUtc,
            DateTime endedAtUtc,
            string closeReason,
            bool completed,
            InteractiveAuditCapture auditCapture,
            int maxOutputBytes)
        {
            var transcript = ReadInteractiveAuditTranscript(auditCapture.TranscriptPath, maxOutputBytes);
            var details = new InteractiveTerminalSessionDetails
            {
                HostAddress = hostAddress,
                SessionMode = "localcmd-interactive",
                EmulationMode = shell,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                CloseReason = closeReason,
                Completed = completed,
                Transcript = transcript
            };

            context.AddInteractiveSession(details);
        }

        private static string ReadInteractiveAuditTranscript(string? transcriptPath, int maxOutputBytes)
        {
            if (string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
                return string.Empty;

            try
            {
                var transcript = File.ReadAllText(transcriptPath);
                var cleanedTranscript = InteractiveTerminalService.CleanTranscriptForAudit(transcript);
                return ApplyTranscriptLimit(cleanedTranscript, maxOutputBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ApplyTranscriptLimit(string transcript, int maxOutputBytes)
        {
            if (string.IsNullOrEmpty(transcript))
                return string.Empty;

            var effectiveMaxBytes = maxOutputBytes > 0 ? maxOutputBytes : DefaultMaxOutputBytes;
            if (Encoding.UTF8.GetByteCount(transcript) <= effectiveMaxBytes)
                return transcript;

            var builder = new StringBuilder();
            var currentBytes = 0;
            using var reader = new StringReader(transcript);
            while (reader.ReadLine() is { } line)
            {
                var lineBytes = Encoding.UTF8.GetByteCount(line) + 1;
                if (currentBytes + lineBytes > effectiveMaxBytes)
                {
                    builder.AppendLine(TruncationMarker);
                    break;
                }

                builder.AppendLine(line);
                currentBytes += lineBytes;
            }

            return builder.ToString().TrimEnd('\r', '\n');
        }

        private static void CleanupInteractiveAuditCapture(InteractiveAuditCapture auditCapture)
        {
            if (string.IsNullOrWhiteSpace(auditCapture.TranscriptPath))
                return;

            try
            {
                if (File.Exists(auditCapture.TranscriptPath))
                    File.Delete(auditCapture.TranscriptPath);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static string BuildCmdArguments(string command, string extraArgs, bool keepOpen)
        {
            var modeFlag = keepOpen ? "/K" : "/c";
            if (string.IsNullOrWhiteSpace(extraArgs))
                return $"{modeFlag} {QuoteCommandLineArgument(command)}";

            return $"{extraArgs} {modeFlag} {QuoteCommandLineArgument(command)}";
        }

        private static string JoinCommandLineArguments(IEnumerable<string> arguments)
        {
            return string.Join(" ", arguments.Select(QuoteCommandLineArgument));
        }

        private static string QuoteCommandLineArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
                return "\"\"";

            if (!argument.Any(ch => char.IsWhiteSpace(ch) || ch == '"'))
                return argument;

            var builder = new StringBuilder(argument.Length + 2);
            builder.Append('"');
            var pendingBackslashes = 0;

            foreach (var ch in argument)
            {
                if (ch == '\\')
                {
                    pendingBackslashes++;
                    continue;
                }

                if (ch == '"')
                {
                    builder.Append('\\', pendingBackslashes * 2 + 1);
                    builder.Append('"');
                    pendingBackslashes = 0;
                    continue;
                }

                if (pendingBackslashes > 0)
                {
                    builder.Append('\\', pendingBackslashes);
                    pendingBackslashes = 0;
                }

                builder.Append(ch);
            }

            if (pendingBackslashes > 0)
                builder.Append('\\', pendingBackslashes * 2);

            builder.Append('"');
            return builder.ToString();
        }

        private static string NormalizeLifetime(string? lifetime)
        {
            return lifetime?.ToLowerInvariant() switch
            {
                "script" => "script",
                "app" => "app",
                _ => "detached",
            };
        }

        private static void RegisterBackgroundProcess(
            ScriptContext context,
            IProcessHandle process,
            string lifetime,
            bool killOnCancel)
        {
            var tracked = new TrackedBackgroundProcess(process, lifetime, killOnCancel);
            var trackingKey = context.LocalCmdTrackingKey;

            lock (BackgroundProcessLock)
            {
                if (!BackgroundProcessesByContext.TryGetValue(trackingKey, out var trackedList))
                {
                    trackedList = new List<TrackedBackgroundProcess>();
                    BackgroundProcessesByContext[trackingKey] = trackedList;
                }

                trackedList.Add(tracked);
                if (string.Equals(lifetime, "app", StringComparison.OrdinalIgnoreCase))
                    AppLifetimeBackgroundProcesses.Add(tracked);
            }
        }

        internal static void CleanupTrackedBackgroundProcesses(ScriptContext context, bool onCancel)
        {
            List<TrackedBackgroundProcess>? trackedList;
            var trackingKey = context.LocalCmdTrackingKey;

            lock (BackgroundProcessLock)
            {
                if (!BackgroundProcessesByContext.TryGetValue(trackingKey, out trackedList))
                    return;

                BackgroundProcessesByContext.Remove(trackingKey);
            }

            foreach (var tracked in trackedList)
            {
                var shouldKill = string.Equals(tracked.Lifetime, "script", StringComparison.OrdinalIgnoreCase)
                    || (onCancel
                        && tracked.KillOnCancel
                        && !string.Equals(tracked.Lifetime, "detached", StringComparison.OrdinalIgnoreCase));

                if (shouldKill)
                {
                    TryKillAndDispose(tracked.Process);
                    RemoveFromAppLifetimeTracking(tracked);
                    continue;
                }

                // Not killed (app lifetime without kill-on-cancel); keep running and preserve tracking for app shutdown.
                if (!string.Equals(tracked.Lifetime, "app", StringComparison.OrdinalIgnoreCase))
                {
                    tracked.Process.Dispose();
                }
            }
        }

        private static void CleanupAppLifetimeProcesses()
        {
            TrackedBackgroundProcess[] tracked;
            lock (BackgroundProcessLock)
            {
                tracked = new TrackedBackgroundProcess[AppLifetimeBackgroundProcesses.Count];
                AppLifetimeBackgroundProcesses.CopyTo(tracked);
                AppLifetimeBackgroundProcesses.Clear();
            }

            foreach (var process in tracked)
            {
                TryKillAndDispose(process.Process);
            }
        }

        private static void RemoveFromAppLifetimeTracking(TrackedBackgroundProcess tracked)
        {
            lock (BackgroundProcessLock)
            {
                AppLifetimeBackgroundProcesses.Remove(tracked);
            }
        }

        private static void TryKillAndDispose(IProcessHandle process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best-effort cleanup.
            }

            TryDispose(process);
        }

        private static void TryDispose(IProcessHandle process)
        {
            try
            {
                process.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        private static void ApplyEnvironmentVariables(
            ProcessStartInfo startInfo, Dictionary<string, string>? env, ScriptContext context)
        {
            if (env == null || env.Count == 0)
                return;

            foreach (var kvp in env)
            {
                var key = context.SubstituteVariables(kvp.Key);
                var value = context.SubstituteVariables(kvp.Value);
                startInfo.Environment[key] = value;
            }
        }

        private sealed class TrackedBackgroundProcess
        {
            public TrackedBackgroundProcess(IProcessHandle process, string lifetime, bool killOnCancel)
            {
                Process = process;
                Lifetime = lifetime;
                KillOnCancel = killOnCancel;
            }

            public IProcessHandle Process { get; }
            public string Lifetime { get; }
            public bool KillOnCancel { get; }
        }

        private readonly record struct InteractiveAuditCapture(string LaunchCommand, string? TranscriptPath);
    }

    public interface IProcessRunner
    {
        IProcessHandle Start(ProcessStartInfo startInfo);
    }

    public interface IProcessHandle : IDisposable
    {
        int Id { get; }
        bool HasExited { get; }
        event DataReceivedEventHandler OutputDataReceived;
        event DataReceivedEventHandler ErrorDataReceived;
        void BeginOutputReadLine();
        void BeginErrorReadLine();
        void Kill(bool entireProcessTree = true);
        Task<int> WaitForExitAsync(CancellationToken cancellationToken);
    }

    internal sealed class SystemProcessRunner : IProcessRunner
    {
        public IProcessHandle Start(ProcessStartInfo startInfo)
        {
            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start process");
            return new SystemProcessHandle(process);
        }
    }

    internal sealed class SystemProcessHandle : IProcessHandle
    {
        private readonly Process _process;

        public SystemProcessHandle(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public int Id => _process.Id;
        public bool HasExited => _process.HasExited;

        public event DataReceivedEventHandler OutputDataReceived
        {
            add => _process.OutputDataReceived += value;
            remove => _process.OutputDataReceived -= value;
        }

        public event DataReceivedEventHandler ErrorDataReceived
        {
            add => _process.ErrorDataReceived += value;
            remove => _process.ErrorDataReceived -= value;
        }

        public void BeginOutputReadLine() => _process.BeginOutputReadLine();
        public void BeginErrorReadLine() => _process.BeginErrorReadLine();
        public void Kill(bool entireProcessTree = true) => _process.Kill(entireProcessTree);

        public async Task<int> WaitForExitAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { Kill(entireProcessTree: true); } catch { }
                throw;
            }

            return _process.ExitCode;
        }

        public void Dispose()
        {
            _process.Dispose();
        }
    }
}
