using System.IO;
using System.Drawing;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Rebex.Net;
using Rebex.TerminalEmulation;
using SSH_Helper.Forms;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Utilities;

// Alias to avoid conflict with SSH_Helper.Services.Scripting namespace.
using RebexScripting = Rebex.TerminalEmulation.Scripting;

namespace SSH_Helper.Services.Terminal
{
    public interface IInteractiveTerminalService
    {
        Task<InteractiveTerminalRunResult> RunAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken);
    }

    public sealed class InteractiveTerminalRunResult
    {
        public bool Success { get; private init; }
        public bool SharedUnavailable { get; private init; }
        public string? ErrorMessage { get; private init; }
        public string? CapturedTranscript { get; private init; }
        public string? CompletionReason { get; private init; }

        public static InteractiveTerminalRunResult Ok(string? capturedTranscript = null, string? completionReason = null) => new()
        {
            Success = true,
            CapturedTranscript = capturedTranscript,
            CompletionReason = completionReason
        };

        public static InteractiveTerminalRunResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };

        public static InteractiveTerminalRunResult SharedUnavailableResult(string message) => new()
        {
            Success = false,
            SharedUnavailable = true,
            ErrorMessage = message
        };
    }

    public sealed class InteractiveTerminalService : IInteractiveTerminalService
    {
        private static readonly Regex AlternateScreenSequenceRegex = new(
            "\u001B\\[\\?(?:1049|1047|47)(?:;\\d+)?(?<mode>[hl])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private const string InteractiveCloseReasonUserClosed = "user_closed";
        private const string InteractiveCloseReasonDisconnected = "disconnected";
        private const string InteractiveCloseReasonCancelled = "cancelled";
        private const string InteractiveCloseReasonError = "error";
        private const string InteractiveCloseReasonCtrlCContinue = "ctrl_c_continue";
        private const string InteractiveCloseReasonTimeoutContinue = "timeout_continue";
        private const string InteractiveCloseReasonMaxLinesContinue = "max_lines_continue";
        private const string InteractiveCloseReasonEarlyClosePartial = "early_close_partial";
        private const string InteractiveCloseReasonNaturalComplete = "natural_complete";
        private const int InteractiveTranscriptMaxLines = 500_000;
        private static readonly string InteractiveTranscriptCapNotice =
            $"{Environment.NewLine}[... interactive transcript capped ...]{Environment.NewLine}";
        private const int InteractiveMirrorOutputMaxLines = 50_000;
        private static readonly string InteractiveMirrorOutputCapNotice =
            $"{Environment.NewLine}[... interactive mirror output capped ...]{Environment.NewLine}";

        private enum InteractiveCloseReasonState
        {
            UserClosed = 0,
            Disconnected = 1,
            Cancelled = 2,
            Error = 3
        }

        private sealed class InteractiveWindowRunSummary
        {
            public bool WasLaunched { get; set; }
            public bool CancelledByToken { get; set; }
            public string CloseReason { get; set; } = InteractiveCloseReasonUserClosed;
            public string Transcript { get; set; } = string.Empty;

            public bool Completed =>
                string.Equals(CloseReason, InteractiveCloseReasonUserClosed, StringComparison.Ordinal) ||
                string.Equals(CloseReason, InteractiveCloseReasonDisconnected, StringComparison.Ordinal) ||
                string.Equals(CloseReason, InteractiveCloseReasonCtrlCContinue, StringComparison.Ordinal) ||
                string.Equals(CloseReason, InteractiveCloseReasonTimeoutContinue, StringComparison.Ordinal) ||
                string.Equals(CloseReason, InteractiveCloseReasonMaxLinesContinue, StringComparison.Ordinal) ||
                string.Equals(CloseReason, InteractiveCloseReasonEarlyClosePartial, StringComparison.Ordinal) ||
                string.Equals(CloseReason, InteractiveCloseReasonNaturalComplete, StringComparison.Ordinal);
        }

        private sealed class SharedCommandGuardState
        {
            public StringBuilder CurrentLine { get; } = new();
            public int SentCharactersOnLine { get; set; }

            public void Reset()
            {
                CurrentLine.Clear();
                SentCharactersOnLine = 0;
            }
        }

        private readonly record struct CapturePromptSetup(Regex? PromptRegex, string PromptLiteral);

        private sealed class ScreenUpdateDispatcher
        {
            private readonly InteractiveTerminalForm _form;
            private readonly object _lock = new();
            private TerminalScreenSnapshot? _latestSnapshot;
            private bool _postQueued;

            public bool IsBacklogged
            {
                get
                {
                    lock (_lock)
                    {
                        return _postQueued && _latestSnapshot != null;
                    }
                }
            }

            public ScreenUpdateDispatcher(InteractiveTerminalForm form)
            {
                _form = form;
            }

            public void Enqueue(TerminalScreenSnapshot snapshot)
            {
                if (_form.IsDisposed)
                    return;

                lock (_lock)
                {
                    _latestSnapshot = snapshot;
                }

                TryQueueDrain();
            }

            private void TryQueueDrain()
            {
                var shouldPost = false;
                lock (_lock)
                {
                    if (!_postQueued)
                    {
                        _postQueued = true;
                        shouldPost = true;
                    }
                }

                if (!shouldPost)
                    return;

                try
                {
                    if (_form.InvokeRequired)
                    {
                        _form.BeginInvoke((Action)DrainOnUiThread);
                    }
                    else
                    {
                        DrainOnUiThread();
                    }
                }
                catch
                {
                    lock (_lock)
                    {
                        _postQueued = false;
                        _latestSnapshot = null;
                    }
                }
            }

            private void DrainOnUiThread()
            {
                TerminalScreenSnapshot? snapshot;
                lock (_lock)
                {
                    snapshot = _latestSnapshot;
                    _latestSnapshot = null;
                    _postQueued = false;
                }

                if (snapshot != null && !_form.IsDisposed)
                {
                    try
                    {
                        _form.SetScreen(snapshot);
                    }
                    catch
                    {
                        lock (_lock)
                        {
                            _latestSnapshot = null;
                            _postQueued = false;
                        }
                        return;
                    }
                }

                var hasPendingSnapshot = false;
                lock (_lock)
                {
                    hasPendingSnapshot = _latestSnapshot != null;
                }

                if (hasPendingSnapshot)
                {
                    TryQueueDrain();
                }
            }
        }

        internal readonly record struct TranscriptCaptureResult(string CapturedText, bool InAlternateScreen);

        public async Task<InteractiveTerminalRunResult> RunAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(options.Command) &&
                options.Session != InteractiveSessionMode.Separate)
            {
                return InteractiveTerminalRunResult.Fail("Interactive command mode requires 'session: separate'.");
            }

            if (!options.ShowWindow &&
                string.IsNullOrWhiteSpace(options.Command))
            {
                return InteractiveTerminalRunResult.Fail("interactive.show_window=false requires interactive.command.");
            }

            if (!options.ShowWindow &&
                !options.MaxSeconds.HasValue &&
                !options.MaxLines.HasValue)
            {
                return InteractiveTerminalRunResult.Fail("interactive.show_window=false requires interactive.max_seconds or interactive.max_lines.");
            }

            if (!string.IsNullOrWhiteSpace(options.Command))
                return await RunSeparateCaptureAsync(context, options, cancellationToken);

            return options.Session == InteractiveSessionMode.Shared
                ? await RunSharedAsync(context, options, cancellationToken)
                : await RunSeparateAsync(context, options, cancellationToken);
        }

        private async Task<InteractiveTerminalRunResult> RunSharedAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            var sharedSession = context.Session;
            if (sharedSession == null || !sharedSession.IsConnected)
            {
                return InteractiveTerminalRunResult.SharedUnavailableResult(
                    "InteractiveSharedUnavailable: no active shared SSH shell session is available for session=shared.");
            }

            var startedAtUtc = DateTime.UtcNow;
            var runSummary = new InteractiveWindowRunSummary();
            var timeouts = context.Timeouts ?? SshTimeoutOptions.Default;
            try
            {
                sharedSession.FlushBuffer();
                var terminal = sharedSession.SharedTerminal;
                startedAtUtc = DateTime.UtcNow;

                runSummary = await RunWindowLoopAsync(
                    title: ResolveInteractiveWindowTitle(
                        options,
                        context.CurrentHost?.ToString() ?? "Current Host",
                        captureMode: false),
                    context: context,
                    scripting: sharedSession.SharedScripting,
                    terminal: terminal,
                    sessionMode: options.Session,
                    keepAliveInterval: timeouts.KeepAliveInterval,
                    cancellationToken: cancellationToken,
                    isConnectionAlive: () => sharedSession.IsConnected,
                    onCancellation: () =>
                    {
                        // Required behavior: stop/cancel force-closes active shared interactive session.
                        sharedSession.Dispose();
                    });

                if (runSummary.CancelledByToken)
                    throw new OperationCanceledException(cancellationToken);

                sharedSession.SyncAfterInteractive();
                return InteractiveTerminalRunResult.Ok(runSummary.Transcript, runSummary.CloseReason);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return InteractiveTerminalRunResult.Fail($"Interactive terminal failed: {ex.Message}");
            }
            finally
            {
                if (runSummary.WasLaunched)
                {
                    context.AddInteractiveSession(CreateSessionDetails(
                        runSummary,
                        context,
                        context.CurrentHost?.ToString() ?? string.Empty,
                        options,
                        startedAtUtc,
                        DateTime.UtcNow));
                }
            }
        }

        private async Task<InteractiveTerminalRunResult> RunSeparateAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            var host = context.CurrentHost;
            if (host == null || string.IsNullOrWhiteSpace(host.IpAddress))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires current host connection details.");
            }

            var username = !string.IsNullOrWhiteSpace(context.ResolvedUsername) ? context.ResolvedUsername : host.Username;
            var password = !string.IsNullOrWhiteSpace(context.ResolvedPassword) ? context.ResolvedPassword : host.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires a resolved username.");
            }

            if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(host.IdentityFile))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires a resolved password or an identity file.");
            }

            var timeouts = context.Timeouts ?? SshTimeoutOptions.Default;
            Ssh? client = null;
            VirtualTerminal? virtualTerminal = null;
            RebexScripting? scripting = null;
            var startedAtUtc = DateTime.UtcNow;
            var runSummary = new InteractiveWindowRunSummary();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                client = new Ssh
                {
                    Timeout = (int)timeouts.ConnectionTimeout.TotalMilliseconds
                };

                ApplyAlgorithmSettings(client, host);
                await Task.Run(() => ConnectAndLogin(client, host, username, password ?? string.Empty), cancellationToken);

                var terminalOptions = SshTerminalOptionsFactory.Create();
                var columns = ResolveTerminalColumns(options);
                var rows = ResolveTerminalRows(options);
                (scripting, virtualTerminal) = SshTerminalOptionsFactory.CreateScriptingWithHistory(
                    client,
                    terminalOptions,
                    columns,
                    rows,
                    SshTerminalOptionsFactory.DefaultHistoryMaxLength);
                var startupPromptSetup = await InitializeSeparateSessionStartupAsync(
                    scripting,
                    timeouts,
                    context.DebugMode,
                    cancellationToken);
                var startupPromptLiteral = ResolveStartupPromptLiteral(
                    startupPromptSetup.PromptLiteral,
                    context.Session?.CurrentPrompt);
                startedAtUtc = DateTime.UtcNow;

                runSummary = await RunWindowLoopAsync(
                    title: ResolveInteractiveWindowTitle(options, host.ToString(), captureMode: false),
                    context: context,
                    scripting: scripting,
                    terminal: virtualTerminal,
                    sessionMode: options.Session,
                    keepAliveInterval: timeouts.KeepAliveInterval,
                    cancellationToken: cancellationToken,
                    isConnectionAlive: () => client != null && client.IsConnected,
                    onCancellation: () => CloseSeparateResources(client, virtualTerminal, scripting),
                    startupPromptLiteral: startupPromptLiteral,
                    initialWindowWidth: options.Width,
                    initialWindowHeight: options.Height,
                    initialColumns: columns,
                    initialRows: rows);

                if (runSummary.CancelledByToken)
                    throw new OperationCanceledException(cancellationToken);

                return InteractiveTerminalRunResult.Ok(runSummary.Transcript, runSummary.CloseReason);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return InteractiveTerminalRunResult.Fail($"Interactive terminal failed: {ex.Message}");
            }
            finally
            {
                CloseSeparateResources(client, virtualTerminal, scripting);
                if (runSummary.WasLaunched)
                {
                    context.AddInteractiveSession(CreateSessionDetails(
                        runSummary,
                        context,
                        host.ToString(),
                        options,
                        startedAtUtc,
                        DateTime.UtcNow));
                }
            }
        }

        private async Task<InteractiveTerminalRunResult> RunSeparateCaptureAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            var host = context.CurrentHost;
            if (host == null || string.IsNullOrWhiteSpace(host.IpAddress))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires current host connection details.");
            }

            if (string.IsNullOrWhiteSpace(options.Command))
            {
                return InteractiveTerminalRunResult.Fail("Interactive capture mode requires a non-empty command.");
            }

            var username = !string.IsNullOrWhiteSpace(context.ResolvedUsername) ? context.ResolvedUsername : host.Username;
            var password = !string.IsNullOrWhiteSpace(context.ResolvedPassword) ? context.ResolvedPassword : host.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires a resolved username.");
            }

            if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(host.IdentityFile))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires a resolved password or an identity file.");
            }

            var timeouts = context.Timeouts ?? SshTimeoutOptions.Default;
            Ssh? client = null;
            VirtualTerminal? virtualTerminal = null;
            RebexScripting? scripting = null;
            var startedAtUtc = DateTime.UtcNow;
            var runSummary = new InteractiveWindowRunSummary();

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                client = new Ssh
                {
                    Timeout = (int)timeouts.ConnectionTimeout.TotalMilliseconds
                };

                ApplyAlgorithmSettings(client, host);
                await Task.Run(() => ConnectAndLogin(client, host, username, password ?? string.Empty), cancellationToken);

                var terminalOptions = SshTerminalOptionsFactory.Create();
                var columns = ResolveTerminalColumns(options);
                var rows = ResolveTerminalRows(options);
                (scripting, virtualTerminal) = SshTerminalOptionsFactory.CreateScriptingWithHistory(
                    client,
                    terminalOptions,
                    columns,
                    rows,
                    SshTerminalOptionsFactory.DefaultHistoryMaxLength);
                var capturePromptSetup = await InitializeSeparateSessionStartupAsync(
                    scripting,
                    timeouts,
                    context.DebugMode,
                    cancellationToken);
                var capturePromptRegex = capturePromptSetup.PromptRegex;
                var startupPromptLiteral = ResolveStartupPromptLiteral(
                    capturePromptSetup.PromptLiteral,
                    context.Session?.CurrentPrompt);
                startedAtUtc = DateTime.UtcNow;

                if (options.ShowWindow)
                {
                    runSummary = await RunCaptureWindowLoopAsync(
                        title: ResolveInteractiveWindowTitle(options, host.ToString(), captureMode: true),
                        context: context,
                        scripting: scripting,
                        terminal: virtualTerminal,
                        command: options.Command,
                        maxSeconds: options.MaxSeconds,
                        maxLines: options.MaxLines,
                        mirrorOutput: options.MirrorOutput,
                        startupPromptLiteral: startupPromptLiteral,
                        capturePromptRegex: capturePromptRegex,
                        keepAliveInterval: timeouts.KeepAliveInterval,
                        cancellationToken: cancellationToken,
                        isConnectionAlive: () => client != null && client.IsConnected,
                        onCancellation: () => CloseSeparateResources(client, virtualTerminal, scripting),
                        initialWindowWidth: options.Width,
                        initialWindowHeight: options.Height,
                        initialColumns: columns,
                        initialRows: rows);
                }
                else
                {
                    runSummary = await RunCaptureHeadlessLoopAsync(
                        context: context,
                        scripting: scripting,
                        terminal: virtualTerminal,
                        command: options.Command,
                        maxSeconds: options.MaxSeconds,
                        maxLines: options.MaxLines,
                        mirrorOutput: options.MirrorOutput,
                        startupPromptLiteral: startupPromptLiteral,
                        capturePromptRegex: capturePromptRegex,
                        keepAliveInterval: timeouts.KeepAliveInterval,
                        cancellationToken: cancellationToken,
                        isConnectionAlive: () => client != null && client.IsConnected,
                        onCancellation: () => CloseSeparateResources(client, virtualTerminal, scripting));
                }

                if (runSummary.CancelledByToken)
                    throw new OperationCanceledException(cancellationToken);

                if (string.Equals(runSummary.CloseReason, InteractiveCloseReasonDisconnected, StringComparison.Ordinal))
                {
                    return InteractiveTerminalRunResult.Fail("Interactive capture disconnected before completion.");
                }

                if (string.Equals(runSummary.CloseReason, InteractiveCloseReasonError, StringComparison.Ordinal))
                {
                    return InteractiveTerminalRunResult.Fail("Interactive capture failed.");
                }

                return InteractiveTerminalRunResult.Ok(runSummary.Transcript, runSummary.CloseReason);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return InteractiveTerminalRunResult.Fail($"Interactive terminal failed: {ex.Message}");
            }
            finally
            {
                CloseSeparateResources(client, virtualTerminal, scripting);
                if (runSummary.WasLaunched)
                {
                    context.AddInteractiveSession(CreateSessionDetails(
                        runSummary,
                        context,
                        host.ToString(),
                        options,
                        startedAtUtc,
                        DateTime.UtcNow));
                }
            }
        }

        private async Task<InteractiveWindowRunSummary> RunCaptureWindowLoopAsync(
            string title,
            ScriptContext context,
            RebexScripting scripting,
            ITerminal? terminal,
            string command,
            int? maxSeconds,
            int? maxLines,
            bool mirrorOutput,
            string? startupPromptLiteral,
            Regex? capturePromptRegex,
            TimeSpan keepAliveInterval,
            CancellationToken cancellationToken,
            Func<bool>? isConnectionAlive,
            Action? onCancellation,
            int? initialWindowWidth = null,
            int? initialWindowHeight = null,
            int? initialColumns = null,
            int? initialRows = null)
        {
            var ioLock = new object();
            var pendingInput = new ConcurrentQueue<Action<RebexScripting>>();
            using var uiLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var wasLaunched = 0;
            var cancelledByToken = 0;
            var pumpHadError = 0;
            var completionSignaled = 0;
            var completionReason = InteractiveCloseReasonEarlyClosePartial;
            var transcriptBuilder = new StringBuilder();
            var transcriptLock = new object();
            var inAlternateScreen = false;
            var inAlternateScreenState = 0;
            var commandDispatched = 0;
            var commandPromptArmed = 0;
            var capturedLineCount = 0;
            var previousChunkEndedWithCarriageReturn = false;
            var transcriptLineCount = 0;
            var transcriptPreviousChunkEndedWithCarriageReturn = false;
            var transcriptCapped = false;
            var transcriptPendingBuffer = new StringBuilder();
            var mirrorPendingBuffer = new StringBuilder();
            var mirrorOutputLock = new object();
            var mirroredLinesEmitted = 0;
            var mirrorOutputCapped = false;
            var mirrorPreviousChunkEndedWithCarriageReturn = false;
            InteractiveTerminalForm? form = null;

            EventHandler? disconnectedHandler = null;
            EventHandler<ActionRequestEventArgs>? actionRequestedHandler = null;
            EventHandler<DataReceivedEventArgs>? dataReceivedHandler = null;
            FormClosedEventHandler? formClosedHandler = null;
            EventHandler<string>? textInputHandler = null;
            EventHandler<TerminalKeyEventArgs>? keyInputHandler = null;
            EventHandler<TerminalSizeChangedEventArgs>? terminalSizeChangedHandler = null;

            bool TrySetCompletion(string reason)
            {
                if (Interlocked.CompareExchange(ref completionSignaled, 1, 0) != 0)
                    return false;

                completionReason = reason;
                completionTcs.TrySetResult(true);
                return true;
            }

            void TrySendCtrlC()
            {
                try
                {
                    lock (ioLock)
                    {
                        scripting.Send(ConsoleKey.C, ConsoleModifiers.Control);
                    }
                }
                catch
                {
                    // Ignore send failures while terminal is shutting down.
                }
            }

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                Interlocked.Exchange(ref cancelledByToken, 1);
                if (TrySetCompletion(InteractiveCloseReasonCancelled))
                {
                    onCancellation?.Invoke();
                }

                if (form != null && !form.IsDisposed)
                {
                    RequestCloseSafe(form);
                }
            });

            if (terminal != null)
            {
                disconnectedHandler = (_, _) =>
                {
                    if (TrySetCompletion(InteractiveCloseReasonDisconnected) &&
                        form != null)
                    {
                        RequestCloseSafe(form);
                    }
                };

                actionRequestedHandler = (_, args) =>
                {
                    if (args.Action != RequestedAction.DisconnectRequest)
                        return;

                    if (TrySetCompletion(InteractiveCloseReasonDisconnected) &&
                        form != null)
                    {
                        RequestCloseSafe(form);
                    }
                };

                dataReceivedHandler = (_, args) =>
                {
                    string capturedText = string.Empty;
                    string mirroredChunk = string.Empty;
                    string? transcriptChunkDebugMessage = null;
                    var reachedMaxLines = false;

                    lock (transcriptLock)
                    {
                        var alternateBefore = inAlternateScreen;
                        var captureResult = FilterTranscriptChunkForAudit(
                            args.RawData,
                            inAlternateScreen,
                            () => args.StrippedData);

                        inAlternateScreen = captureResult.InAlternateScreen;
                        Volatile.Write(ref inAlternateScreenState, inAlternateScreen ? 1 : 0);
                        capturedText = captureResult.CapturedText;
                        var transcriptAssemblyInput = ResolveTranscriptAssemblyInput(
                            args.RawData,
                            capturedText,
                            alternateBefore,
                            inAlternateScreen);
                        var transcriptChunk = PrepareMirroredChunkForEmission(
                            transcriptAssemblyInput,
                            transcriptPendingBuffer,
                            flush: false);
                        if (!string.IsNullOrEmpty(transcriptChunk))
                        {
                            AppendTranscriptWithCap(
                                transcriptBuilder,
                                transcriptChunk,
                                ref transcriptLineCount,
                                ref transcriptPreviousChunkEndedWithCarriageReturn,
                                ref transcriptCapped);

                            if (maxLines.HasValue && maxLines.Value > 0)
                            {
                                capturedLineCount += CountLinesFromCapturedChunk(
                                    transcriptChunk,
                                    ref previousChunkEndedWithCarriageReturn);
                                reachedMaxLines = capturedLineCount >= maxLines.Value;
                            }
                        }

                        if (context.DebugMode &&
                            ShouldEmitTranscriptChunkDebug(args.RawData, args.StrippedData, capturedText))
                        {
                            transcriptChunkDebugMessage = BuildTranscriptChunkDebugMessage(
                                phase: "capture-window",
                                inAlternateBefore: alternateBefore,
                                inAlternateAfter: inAlternateScreen,
                                rawData: args.RawData,
                                strippedData: args.StrippedData,
                                capturedText: capturedText);
                        }
                    }

                    EmitInteractiveTranscriptDebug(context, transcriptChunkDebugMessage);

                    if (ShouldMirrorCaptureChunk(mirrorOutput, capturedText))
                    {
                        lock (mirrorOutputLock)
                        {
                            mirroredChunk = PrepareMirroredChunkForEmission(
                                capturedText,
                                mirrorPendingBuffer,
                                flush: false);
                            var cappedMirrorChunk = ApplyMirrorOutputCap(
                                mirroredChunk,
                                mirroredLinesEmitted,
                                mirrorOutputCapped,
                                mirrorPreviousChunkEndedWithCarriageReturn);
                            mirroredChunk = cappedMirrorChunk.Output;
                            mirroredLinesEmitted = cappedMirrorChunk.EmittedLines;
                            mirrorOutputCapped = cappedMirrorChunk.IsCapped;
                            mirrorPreviousChunkEndedWithCarriageReturn =
                                cappedMirrorChunk.PreviousChunkEndedWithCarriageReturn;
                        }
                    }

                    if (!string.IsNullOrEmpty(mirroredChunk))
                    {
                        context.EmitOutput(mirroredChunk, ScriptOutputType.RawChunk);
                    }

                    if (reachedMaxLines &&
                        Interlocked.CompareExchange(ref completionSignaled, 0, 0) == 0)
                    {
                        TrySendCtrlC();
                        TrySetCompletion(InteractiveCloseReasonMaxLinesContinue);
                    }

                    if (Volatile.Read(ref commandDispatched) == 1)
                    {
                        var stripped = args.StrippedData;
                        if (Interlocked.CompareExchange(ref commandPromptArmed, 0, 0) == 0 &&
                            ShouldArmCaptureNaturalCompletion(stripped, command, capturePromptRegex))
                        {
                            Interlocked.Exchange(ref commandPromptArmed, 1);
                        }

                        if (Interlocked.CompareExchange(ref commandPromptArmed, 0, 0) == 1 &&
                            ShouldCompleteCaptureOnPrompt(stripped, capturePromptRegex))
                        {
                            TrySetCompletion(InteractiveCloseReasonNaturalComplete);
                        }
                    }
                };

                terminal.Disconnected += disconnectedHandler;
                terminal.ActionRequested += actionRequestedHandler;
                terminal.DataReceived += dataReceivedHandler;
            }

            await InvokeOnUiThreadAsync(() =>
            {
                form = new InteractiveTerminalForm(
                    title,
                    initialWindowWidth,
                    initialWindowHeight,
                    initialColumns,
                    initialRows);

                formClosedHandler = (_, _) =>
                {
                    if (Interlocked.CompareExchange(ref completionSignaled, 0, 0) == 0)
                    {
                        TrySetCompletion(InteractiveCloseReasonEarlyClosePartial);
                    }

                    uiLoopCancellation.Cancel();
                };
                form.FormClosed += formClosedHandler;

                textInputHandler = (_, text) =>
                {
                    pendingInput.Enqueue(stream => stream.Send(text));
                };
                form.TextInput += textInputHandler;

                keyInputHandler = (_, keyArgs) =>
                {
                    if (keyArgs.ConsoleKey == ConsoleKey.C &&
                        (keyArgs.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control &&
                        Volatile.Read(ref commandDispatched) == 1)
                    {
                        TrySendCtrlC();
                        TrySetCompletion(InteractiveCloseReasonCtrlCContinue);
                        return;
                    }

                    pendingInput.Enqueue(stream =>
                    {
                        if (keyArgs.FunctionKey.HasValue)
                        {
                            stream.Send(keyArgs.FunctionKey.Value, keyArgs.Modifiers);
                        }
                        else if (keyArgs.ConsoleKey.HasValue)
                        {
                            stream.Send(keyArgs.ConsoleKey.Value, keyArgs.Modifiers);
                        }
                    });
                };
                form.KeyInput += keyInputHandler;

                terminalSizeChangedHandler = (_, sizeArgs) =>
                {
                    if (terminal == null)
                        return;

                    try
                    {
                        lock (ioLock)
                        {
                            terminal.SetScreenSize(sizeArgs.Columns, sizeArgs.Rows);
                        }
                    }
                    catch
                    {
                        // Ignore resize failures while terminal is shutting down.
                    }
                };
                form.TerminalSizeChanged += terminalSizeChangedHandler;

                form.CopyAllTextProvider = () =>
                {
                    if (terminal == null)
                        return string.Empty;

                    lock (ioLock)
                    {
                        return BuildClipboardText(terminal);
                    }
                };
                form.SelectionTextProvider = selection =>
                {
                    if (terminal == null)
                        return string.Empty;

                    lock (ioLock)
                    {
                        return BuildSelectionClipboardText(terminal, selection);
                    }
                };

                form.ClearScrollbackAction = () =>
                {
                    if (terminal == null)
                        return;

                    lock (ioLock)
                    {
                        ClearScrollbackPreservingScreen(terminal);
                    }
                };

                form.ResetTerminalAction = () =>
                {
                    if (terminal == null)
                        return;

                    lock (ioLock)
                    {
                        ResetTerminalState(terminal);
                    }
                };

                form.Show(Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
                Interlocked.Exchange(ref wasLaunched, 1);
                form.FocusTerminal();
            });

            if (mirrorOutput)
            {
                var promptPrefix = BuildMirroredStartupPromptPrefix(startupPromptLiteral);
                if (!string.IsNullOrEmpty(promptPrefix))
                {
                    lock (transcriptLock)
                    {
                        AppendTranscriptWithCap(
                            transcriptBuilder,
                            promptPrefix,
                            ref transcriptLineCount,
                            ref transcriptPreviousChunkEndedWithCarriageReturn,
                            ref transcriptCapped);
                    }

                    lock (mirrorOutputLock)
                    {
                        var cappedPromptPrefix = ApplyMirrorOutputCap(
                            promptPrefix,
                            mirroredLinesEmitted,
                            mirrorOutputCapped,
                            mirrorPreviousChunkEndedWithCarriageReturn);
                        promptPrefix = cappedPromptPrefix.Output;
                        mirroredLinesEmitted = cappedPromptPrefix.EmittedLines;
                        mirrorOutputCapped = cappedPromptPrefix.IsCapped;
                        mirrorPreviousChunkEndedWithCarriageReturn =
                            cappedPromptPrefix.PreviousChunkEndedWithCarriageReturn;
                    }

                    if (!string.IsNullOrEmpty(promptPrefix))
                    {
                        context.EmitOutput(promptPrefix, ScriptOutputType.RawChunk);
                    }
                }
            }

            pendingInput.Enqueue(stream =>
            {
                stream.Send(command + "\r");
                Interlocked.Exchange(ref commandDispatched, 1);
            });

            Task timeoutTask = Task.CompletedTask;
            if (maxSeconds.HasValue && maxSeconds.Value > 0)
            {
                timeoutTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(maxSeconds.Value), uiLoopCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref completionSignaled, 0, 0) != 0)
                        return;

                    TrySendCtrlC();
                    TrySetCompletion(InteractiveCloseReasonTimeoutContinue);
                }, CancellationToken.None);
            }

            var pumpTask = Task.Run(async () =>
            {
                try
                {
                    var hadPumpError = await PumpFullAsync(
                        form!,
                        scripting,
                        terminal,
                        ioLock,
                        pendingInput,
                        keepAliveInterval,
                        isConnectionAlive,
                        () => Interlocked.CompareExchange(ref completionSignaled, 0, 0) == 1,
                        () => Volatile.Read(ref inAlternateScreenState) == 1,
                        uiLoopCancellation.Token,
                        closeOnTerminalClosed: false);

                    if (hadPumpError)
                    {
                        Interlocked.Exchange(ref pumpHadError, 1);
                        TrySetCompletion(InteractiveCloseReasonError);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected while shutting down capture mode.
                }
                catch (Exception ex)
                {
                    Interlocked.Exchange(ref pumpHadError, 1);
                    TrySetCompletion(InteractiveCloseReasonError);
                    if (form != null)
                    {
                        AppendOutputSafe(form, $"\r\n[interactive-error] {ex.Message}\r\n");
                    }
                }
            }, CancellationToken.None);

            await completionTcs.Task;

            string detachedHistoryText = string.Empty;
            if (terminal != null && IsDetachedCaptureCompletionReason(completionReason))
            {
                try
                {
                    lock (ioLock)
                    {
                        detachedHistoryText = BuildClipboardText(terminal);
                    }
                }
                catch
                {
                    detachedHistoryText = string.Empty;
                }
            }

            if (form != null &&
                !form.IsDisposed &&
                IsDetachedCaptureCompletionReason(completionReason))
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    if (!form.IsDisposed)
                    {
                        form.EnableDetachedReadOnlyMode("Detached (read-only)", detachedHistoryText);
                    }
                });
            }

            uiLoopCancellation.Cancel();
            await pumpTask;
            await timeoutTask;

            if (mirrorOutput)
            {
                string mirroredTail;
                lock (mirrorOutputLock)
                {
                    mirroredTail = PrepareMirroredChunkForEmission(
                        null,
                        mirrorPendingBuffer,
                        flush: true);
                    var cappedMirrorTail = ApplyMirrorOutputCap(
                        mirroredTail,
                        mirroredLinesEmitted,
                        mirrorOutputCapped,
                        mirrorPreviousChunkEndedWithCarriageReturn);
                    mirroredTail = cappedMirrorTail.Output;
                    mirroredLinesEmitted = cappedMirrorTail.EmittedLines;
                    mirrorOutputCapped = cappedMirrorTail.IsCapped;
                    mirrorPreviousChunkEndedWithCarriageReturn =
                        cappedMirrorTail.PreviousChunkEndedWithCarriageReturn;
                }

                if (!string.IsNullOrEmpty(mirroredTail))
                {
                    context.EmitOutput(mirroredTail, ScriptOutputType.RawChunk);
                }
            }

            if (terminal != null)
            {
                try
                {
                    if (disconnectedHandler != null)
                        terminal.Disconnected -= disconnectedHandler;
                }
                catch
                {
                    // Ignore detach failures while terminal is shutting down.
                }

                try
                {
                    if (actionRequestedHandler != null)
                        terminal.ActionRequested -= actionRequestedHandler;
                }
                catch
                {
                    // Ignore detach failures while terminal is shutting down.
                }

                try
                {
                    if (dataReceivedHandler != null)
                        terminal.DataReceived -= dataReceivedHandler;
                }
                catch
                {
                    // Ignore detach failures while terminal is shutting down.
                }
            }

            if (form != null)
            {
                try
                {
                    if (formClosedHandler != null)
                        form.FormClosed -= formClosedHandler;
                    if (textInputHandler != null)
                        form.TextInput -= textInputHandler;
                    if (keyInputHandler != null)
                        form.KeyInput -= keyInputHandler;
                    if (terminalSizeChangedHandler != null)
                        form.TerminalSizeChanged -= terminalSizeChangedHandler;
                }
                catch
                {
                    // Ignore detach failures while form is closing.
                }
            }

            if (Volatile.Read(ref cancelledByToken) == 1 || cancellationToken.IsCancellationRequested)
            {
                completionReason = InteractiveCloseReasonCancelled;
            }
            else if (Volatile.Read(ref pumpHadError) == 1 &&
                !string.Equals(completionReason, InteractiveCloseReasonEarlyClosePartial, StringComparison.Ordinal) &&
                !IsDetachedCaptureCompletionReason(completionReason))
            {
                completionReason = InteractiveCloseReasonError;
            }

            string transcript;
            lock (transcriptLock)
            {
                var pendingTranscriptTail = PrepareMirroredChunkForEmission(
                    capturedText: null,
                    pendingBuffer: transcriptPendingBuffer,
                    flush: true);
                if (!string.IsNullOrEmpty(pendingTranscriptTail))
                {
                    AppendTranscriptWithCap(
                        transcriptBuilder,
                        pendingTranscriptTail,
                        ref transcriptLineCount,
                        ref transcriptPreviousChunkEndedWithCarriageReturn,
                        ref transcriptCapped);
                }

                transcript = transcriptBuilder.ToString();
            }
            EmitInteractiveTranscriptDebug(
                context,
                $"[interactive-debug capture-window] transcript_before_prefix=\"{FormatInteractiveDebugText(transcript)}\"");
            transcript = PrependStartupPromptIfMissing(transcript, startupPromptLiteral);
            EmitInteractiveTranscriptDebug(
                context,
                $"[interactive-debug capture-window] transcript_after_prefix=\"{FormatInteractiveDebugText(transcript)}\"");

            return new InteractiveWindowRunSummary
            {
                WasLaunched = Volatile.Read(ref wasLaunched) == 1,
                CancelledByToken = Volatile.Read(ref cancelledByToken) == 1 || cancellationToken.IsCancellationRequested,
                CloseReason = completionReason,
                Transcript = transcript
            };
        }

        private async Task<InteractiveWindowRunSummary> RunCaptureHeadlessLoopAsync(
            ScriptContext context,
            RebexScripting scripting,
            ITerminal? terminal,
            string command,
            int? maxSeconds,
            int? maxLines,
            bool mirrorOutput,
            string? startupPromptLiteral,
            Regex? capturePromptRegex,
            TimeSpan keepAliveInterval,
            CancellationToken cancellationToken,
            Func<bool>? isConnectionAlive,
            Action? onCancellation)
        {
            var ioLock = new object();
            var pendingInput = new ConcurrentQueue<Action<RebexScripting>>();
            using var loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var completionTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelledByToken = 0;
            var pumpHadError = 0;
            var completionSignaled = 0;
            var completionReason = InteractiveCloseReasonError;
            var transcriptBuilder = new StringBuilder();
            var transcriptLock = new object();
            var inAlternateScreen = false;
            var inAlternateScreenState = 0;
            var commandDispatched = 0;
            var commandPromptArmed = 0;
            var capturedLineCount = 0;
            var previousChunkEndedWithCarriageReturn = false;
            var transcriptLineCount = 0;
            var transcriptPreviousChunkEndedWithCarriageReturn = false;
            var transcriptCapped = false;
            var transcriptPendingBuffer = new StringBuilder();
            var mirrorPendingBuffer = new StringBuilder();
            var mirrorOutputLock = new object();
            var mirroredLinesEmitted = 0;
            var mirrorOutputCapped = false;
            var mirrorPreviousChunkEndedWithCarriageReturn = false;

            EventHandler? disconnectedHandler = null;
            EventHandler<ActionRequestEventArgs>? actionRequestedHandler = null;
            EventHandler<DataReceivedEventArgs>? dataReceivedHandler = null;

            bool TrySetCompletion(string reason)
            {
                if (Interlocked.CompareExchange(ref completionSignaled, 1, 0) != 0)
                    return false;

                completionReason = reason;
                completionTcs.TrySetResult(true);
                return true;
            }

            void TrySendCtrlC()
            {
                try
                {
                    lock (ioLock)
                    {
                        scripting.Send(ConsoleKey.C, ConsoleModifiers.Control);
                    }
                }
                catch
                {
                    // Ignore send failures while terminal is shutting down.
                }
            }

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                Interlocked.Exchange(ref cancelledByToken, 1);
                if (TrySetCompletion(InteractiveCloseReasonCancelled))
                {
                    onCancellation?.Invoke();
                }
            });

            if (terminal != null)
            {
                disconnectedHandler = (_, _) =>
                {
                    TrySetCompletion(InteractiveCloseReasonDisconnected);
                };

                actionRequestedHandler = (_, args) =>
                {
                    if (args.Action == RequestedAction.DisconnectRequest)
                    {
                        TrySetCompletion(InteractiveCloseReasonDisconnected);
                    }
                };

                dataReceivedHandler = (_, args) =>
                {
                    string capturedText = string.Empty;
                    string mirroredChunk = string.Empty;
                    string? transcriptChunkDebugMessage = null;
                    var reachedMaxLines = false;

                    lock (transcriptLock)
                    {
                        var alternateBefore = inAlternateScreen;
                        var captureResult = FilterTranscriptChunkForAudit(
                            args.RawData,
                            inAlternateScreen,
                            () => args.StrippedData);

                        inAlternateScreen = captureResult.InAlternateScreen;
                        Volatile.Write(ref inAlternateScreenState, inAlternateScreen ? 1 : 0);
                        capturedText = captureResult.CapturedText;
                        var transcriptAssemblyInput = ResolveTranscriptAssemblyInput(
                            args.RawData,
                            capturedText,
                            alternateBefore,
                            inAlternateScreen);
                        var transcriptChunk = PrepareMirroredChunkForEmission(
                            transcriptAssemblyInput,
                            transcriptPendingBuffer,
                            flush: false);
                        if (!string.IsNullOrEmpty(transcriptChunk))
                        {
                            AppendTranscriptWithCap(
                                transcriptBuilder,
                                transcriptChunk,
                                ref transcriptLineCount,
                                ref transcriptPreviousChunkEndedWithCarriageReturn,
                                ref transcriptCapped);

                            if (maxLines.HasValue && maxLines.Value > 0)
                            {
                                capturedLineCount += CountLinesFromCapturedChunk(
                                    transcriptChunk,
                                    ref previousChunkEndedWithCarriageReturn);
                                reachedMaxLines = capturedLineCount >= maxLines.Value;
                            }
                        }

                        if (context.DebugMode &&
                            ShouldEmitTranscriptChunkDebug(args.RawData, args.StrippedData, capturedText))
                        {
                            transcriptChunkDebugMessage = BuildTranscriptChunkDebugMessage(
                                phase: "capture-headless",
                                inAlternateBefore: alternateBefore,
                                inAlternateAfter: inAlternateScreen,
                                rawData: args.RawData,
                                strippedData: args.StrippedData,
                                capturedText: capturedText);
                        }
                    }

                    EmitInteractiveTranscriptDebug(context, transcriptChunkDebugMessage);

                    if (ShouldMirrorCaptureChunk(mirrorOutput, capturedText))
                    {
                        lock (mirrorOutputLock)
                        {
                            mirroredChunk = PrepareMirroredChunkForEmission(
                                capturedText,
                                mirrorPendingBuffer,
                                flush: false);
                            var cappedMirrorChunk = ApplyMirrorOutputCap(
                                mirroredChunk,
                                mirroredLinesEmitted,
                                mirrorOutputCapped,
                                mirrorPreviousChunkEndedWithCarriageReturn);
                            mirroredChunk = cappedMirrorChunk.Output;
                            mirroredLinesEmitted = cappedMirrorChunk.EmittedLines;
                            mirrorOutputCapped = cappedMirrorChunk.IsCapped;
                            mirrorPreviousChunkEndedWithCarriageReturn =
                                cappedMirrorChunk.PreviousChunkEndedWithCarriageReturn;
                        }
                    }

                    if (!string.IsNullOrEmpty(mirroredChunk))
                    {
                        context.EmitOutput(mirroredChunk, ScriptOutputType.RawChunk);
                    }

                    if (reachedMaxLines &&
                        Interlocked.CompareExchange(ref completionSignaled, 0, 0) == 0)
                    {
                        TrySendCtrlC();
                        TrySetCompletion(InteractiveCloseReasonMaxLinesContinue);
                    }

                    if (Volatile.Read(ref commandDispatched) == 1)
                    {
                        var stripped = args.StrippedData;
                        if (Interlocked.CompareExchange(ref commandPromptArmed, 0, 0) == 0 &&
                            ShouldArmCaptureNaturalCompletion(stripped, command, capturePromptRegex))
                        {
                            Interlocked.Exchange(ref commandPromptArmed, 1);
                        }

                        if (Interlocked.CompareExchange(ref commandPromptArmed, 0, 0) == 1 &&
                            ShouldCompleteCaptureOnPrompt(stripped, capturePromptRegex))
                        {
                            TrySetCompletion(InteractiveCloseReasonNaturalComplete);
                        }
                    }
                };

                terminal.Disconnected += disconnectedHandler;
                terminal.ActionRequested += actionRequestedHandler;
                terminal.DataReceived += dataReceivedHandler;
            }
            else
            {
                TrySetCompletion(InteractiveCloseReasonError);
            }

            if (mirrorOutput)
            {
                var promptPrefix = BuildMirroredStartupPromptPrefix(startupPromptLiteral);
                if (!string.IsNullOrEmpty(promptPrefix))
                {
                    lock (transcriptLock)
                    {
                        AppendTranscriptWithCap(
                            transcriptBuilder,
                            promptPrefix,
                            ref transcriptLineCount,
                            ref transcriptPreviousChunkEndedWithCarriageReturn,
                            ref transcriptCapped);
                    }

                    lock (mirrorOutputLock)
                    {
                        var cappedPromptPrefix = ApplyMirrorOutputCap(
                            promptPrefix,
                            mirroredLinesEmitted,
                            mirrorOutputCapped,
                            mirrorPreviousChunkEndedWithCarriageReturn);
                        promptPrefix = cappedPromptPrefix.Output;
                        mirroredLinesEmitted = cappedPromptPrefix.EmittedLines;
                        mirrorOutputCapped = cappedPromptPrefix.IsCapped;
                        mirrorPreviousChunkEndedWithCarriageReturn =
                            cappedPromptPrefix.PreviousChunkEndedWithCarriageReturn;
                    }

                    if (!string.IsNullOrEmpty(promptPrefix))
                    {
                        context.EmitOutput(promptPrefix, ScriptOutputType.RawChunk);
                    }
                }
            }

            pendingInput.Enqueue(stream =>
            {
                stream.Send(command + "\r");
                Interlocked.Exchange(ref commandDispatched, 1);
            });

            Task timeoutTask = Task.CompletedTask;
            if (maxSeconds.HasValue && maxSeconds.Value > 0)
            {
                timeoutTask = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(maxSeconds.Value), loopCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(ref completionSignaled, 0, 0) != 0)
                        return;

                    TrySendCtrlC();
                    TrySetCompletion(InteractiveCloseReasonTimeoutContinue);
                }, CancellationToken.None);
            }

            var pumpTask = Task.Run(async () =>
            {
                try
                {
                    var hadPumpError = await PumpCaptureHeadlessAsync(
                        scripting,
                        terminal,
                        ioLock,
                        pendingInput,
                        keepAliveInterval,
                        isConnectionAlive,
                        () => Interlocked.CompareExchange(ref completionSignaled, 0, 0) == 1,
                        () => Volatile.Read(ref inAlternateScreenState) == 1,
                        loopCancellation.Token);

                    if (hadPumpError)
                    {
                        Interlocked.Exchange(ref pumpHadError, 1);
                        TrySetCompletion(InteractiveCloseReasonError);
                        return;
                    }

                    if (Interlocked.CompareExchange(ref completionSignaled, 0, 0) == 0)
                    {
                        TrySetCompletion(InteractiveCloseReasonDisconnected);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected while shutting down capture mode.
                }
                catch
                {
                    Interlocked.Exchange(ref pumpHadError, 1);
                    TrySetCompletion(InteractiveCloseReasonError);
                }
            }, CancellationToken.None);

            await completionTcs.Task;
            loopCancellation.Cancel();
            await pumpTask;
            await timeoutTask;

            if (mirrorOutput)
            {
                string mirroredTail;
                lock (mirrorOutputLock)
                {
                    mirroredTail = PrepareMirroredChunkForEmission(
                        null,
                        mirrorPendingBuffer,
                        flush: true);
                    var cappedMirrorTail = ApplyMirrorOutputCap(
                        mirroredTail,
                        mirroredLinesEmitted,
                        mirrorOutputCapped,
                        mirrorPreviousChunkEndedWithCarriageReturn);
                    mirroredTail = cappedMirrorTail.Output;
                    mirroredLinesEmitted = cappedMirrorTail.EmittedLines;
                    mirrorOutputCapped = cappedMirrorTail.IsCapped;
                    mirrorPreviousChunkEndedWithCarriageReturn =
                        cappedMirrorTail.PreviousChunkEndedWithCarriageReturn;
                }

                if (!string.IsNullOrEmpty(mirroredTail))
                {
                    context.EmitOutput(mirroredTail, ScriptOutputType.RawChunk);
                }
            }

            if (terminal != null)
            {
                try
                {
                    if (disconnectedHandler != null)
                        terminal.Disconnected -= disconnectedHandler;
                }
                catch
                {
                    // Ignore detach failures while terminal is shutting down.
                }

                try
                {
                    if (actionRequestedHandler != null)
                        terminal.ActionRequested -= actionRequestedHandler;
                }
                catch
                {
                    // Ignore detach failures while terminal is shutting down.
                }

                try
                {
                    if (dataReceivedHandler != null)
                        terminal.DataReceived -= dataReceivedHandler;
                }
                catch
                {
                    // Ignore detach failures while terminal is shutting down.
                }
            }

            if (Volatile.Read(ref cancelledByToken) == 1 || cancellationToken.IsCancellationRequested)
            {
                completionReason = InteractiveCloseReasonCancelled;
            }
            else if (Volatile.Read(ref pumpHadError) == 1 && !IsCaptureSuccessCloseReason(completionReason))
            {
                completionReason = InteractiveCloseReasonError;
            }

            string transcript;
            lock (transcriptLock)
            {
                var pendingTranscriptTail = PrepareMirroredChunkForEmission(
                    capturedText: null,
                    pendingBuffer: transcriptPendingBuffer,
                    flush: true);
                if (!string.IsNullOrEmpty(pendingTranscriptTail))
                {
                    AppendTranscriptWithCap(
                        transcriptBuilder,
                        pendingTranscriptTail,
                        ref transcriptLineCount,
                        ref transcriptPreviousChunkEndedWithCarriageReturn,
                        ref transcriptCapped);
                }

                transcript = transcriptBuilder.ToString();
            }
            EmitInteractiveTranscriptDebug(
                context,
                $"[interactive-debug capture-headless] transcript_final=\"{FormatInteractiveDebugText(transcript)}\"");

            return new InteractiveWindowRunSummary
            {
                WasLaunched = true,
                CancelledByToken = Volatile.Read(ref cancelledByToken) == 1 || cancellationToken.IsCancellationRequested,
                CloseReason = completionReason,
                Transcript = transcript
            };
        }

        private async Task<InteractiveWindowRunSummary> RunWindowLoopAsync(
            string title,
            ScriptContext context,
            RebexScripting scripting,
            ITerminal? terminal,
            InteractiveSessionMode sessionMode,
            TimeSpan keepAliveInterval,
            CancellationToken cancellationToken,
            Func<bool>? isConnectionAlive,
            Action? onCancellation,
            string? startupPromptLiteral = null,
            int? initialWindowWidth = null,
            int? initialWindowHeight = null,
            int? initialColumns = null,
            int? initialRows = null)
        {
            var ioLock = new object();
            var pendingInput = new ConcurrentQueue<Action<RebexScripting>>();
            var formClosedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var uiLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var cancelledByToken = 0;
            var closeReasonState = (int)InteractiveCloseReasonState.UserClosed;
            var wasLaunched = 0;
            var transcriptBuilder = new StringBuilder();
            var transcriptLock = new object();
            var transcriptLineCount = 0;
            var transcriptPreviousChunkEndedWithCarriageReturn = false;
            var transcriptCapped = false;
            var transcriptPendingBuffer = new StringBuilder();
            var inAlternateScreen = false;
            var inAlternateScreenState = 0;
            var sharedCommandGuard = sessionMode == InteractiveSessionMode.Shared ? new SharedCommandGuardState() : null;
            InteractiveTerminalForm? form = null;
            var terminalDisconnected = 0;
            var terminalRequestedDisconnect = 0;
            var ctrlDRequestedTick = -1L;
            var sawLogoutAfterCtrlD = 0;
            var pumpHadError = 0;
            EventHandler? disconnectedHandler = null;
            EventHandler<ActionRequestEventArgs>? actionRequestedHandler = null;
            EventHandler<DataReceivedEventArgs>? dataReceivedHandler = null;

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                Interlocked.Exchange(ref cancelledByToken, 1);
                TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Cancelled);
                onCancellation?.Invoke();

                if (form == null || form.IsDisposed)
                    return;

                try
                {
                    RequestCloseSafe(form);
                }
                catch
                {
                    // Form might already be disposing.
                }
            });

            if (terminal != null)
            {
                disconnectedHandler = (_, _) =>
                {
                    Interlocked.Exchange(ref terminalDisconnected, 1);
                    TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Disconnected);
                    if (form != null)
                        RequestCloseSafe(form);
                };

                actionRequestedHandler = (_, args) =>
                {
                    if (args.Action != RequestedAction.DisconnectRequest)
                        return;

                    Interlocked.Exchange(ref terminalRequestedDisconnect, 1);
                    TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Disconnected);
                    if (form != null)
                        RequestCloseSafe(form);
                };

                dataReceivedHandler = (_, args) =>
                {
                    string? transcriptChunkDebugMessage = null;
                    lock (transcriptLock)
                    {
                        var alternateBefore = inAlternateScreen;
                        var captureResult = FilterTranscriptChunkForAudit(
                            args.RawData,
                            inAlternateScreen,
                            () => args.StrippedData);

                        inAlternateScreen = captureResult.InAlternateScreen;
                        Volatile.Write(ref inAlternateScreenState, inAlternateScreen ? 1 : 0);
                        var transcriptAssemblyInput = ResolveTranscriptAssemblyInput(
                            args.RawData,
                            captureResult.CapturedText,
                            alternateBefore,
                            inAlternateScreen);
                        var transcriptChunk = PrepareMirroredChunkForEmission(
                            transcriptAssemblyInput,
                            transcriptPendingBuffer,
                            flush: false);
                        if (!string.IsNullOrEmpty(transcriptChunk))
                        {
                            AppendTranscriptWithCap(
                                transcriptBuilder,
                                transcriptChunk,
                                ref transcriptLineCount,
                                ref transcriptPreviousChunkEndedWithCarriageReturn,
                                ref transcriptCapped);
                        }

                        if (context.DebugMode &&
                            ShouldEmitTranscriptChunkDebug(args.RawData, args.StrippedData, captureResult.CapturedText))
                        {
                            transcriptChunkDebugMessage = BuildTranscriptChunkDebugMessage(
                                phase: "interactive-window",
                                inAlternateBefore: alternateBefore,
                                inAlternateAfter: inAlternateScreen,
                                rawData: args.RawData,
                                strippedData: args.StrippedData,
                                capturedText: captureResult.CapturedText);
                        }
                    }

                    EmitInteractiveTranscriptDebug(context, transcriptChunkDebugMessage);

                    var ctrlDTick = Interlocked.Read(ref ctrlDRequestedTick);
                    if (ctrlDTick < 0)
                        return;

                    var stripped = args.StrippedData;
                    if (string.IsNullOrWhiteSpace(stripped))
                        return;

                    if (ContainsLogoutLine(stripped))
                    {
                        Interlocked.Exchange(ref sawLogoutAfterCtrlD, 1);
                        TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Disconnected);
                        if (form != null)
                            RequestCloseSafe(form);
                    }
                };

                terminal.Disconnected += disconnectedHandler;
                terminal.ActionRequested += actionRequestedHandler;
                terminal.DataReceived += dataReceivedHandler;
            }

            await InvokeOnUiThreadAsync(() =>
            {
                form = new InteractiveTerminalForm(
                    title,
                    initialWindowWidth,
                    initialWindowHeight,
                    initialColumns,
                    initialRows);

                form.FormClosed += (_, _) =>
                {
                    formClosedTcs.TrySetResult(true);
                    uiLoopCancellation.Cancel();
                };

                form.TextInput += (_, text) =>
                {
                    if (sharedCommandGuard != null)
                    {
                        if (EnqueueSharedTextInput(sharedCommandGuard, pendingInput, text))
                        {
                            EnqueueSharedDetachRequest(sharedCommandGuard, pendingInput, form);
                        }

                        return;
                    }

                    pendingInput.Enqueue(stream => stream.Send(text));
                };

                form.KeyInput += (_, keyArgs) =>
                {
                    if (ShouldCloseSharedWindowWithoutSendingEof(sessionMode, keyArgs))
                    {
                        RequestCloseSafe(form);
                        return;
                    }

                    if (keyArgs.ConsoleKey == ConsoleKey.D &&
                        (keyArgs.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
                    {
                        Interlocked.Exchange(ref ctrlDRequestedTick, Environment.TickCount64);
                    }

                    if (sharedCommandGuard != null)
                    {
                        if (ShouldBlockSharedShellCommandOnEnter(sharedCommandGuard, keyArgs))
                        {
                            EnqueueSharedDetachRequest(sharedCommandGuard, pendingInput, form);
                            return;
                        }

                        UpdateSharedCommandGuardForKey(sharedCommandGuard, keyArgs);
                    }

                    pendingInput.Enqueue(stream =>
                    {
                        if (keyArgs.FunctionKey.HasValue)
                        {
                            stream.Send(keyArgs.FunctionKey.Value, keyArgs.Modifiers);
                        }
                        else if (keyArgs.ConsoleKey.HasValue)
                        {
                            stream.Send(keyArgs.ConsoleKey.Value, keyArgs.Modifiers);
                        }
                    });
                };

                form.TerminalSizeChanged += (_, sizeArgs) =>
                {
                    if (terminal == null)
                        return;

                    try
                    {
                        lock (ioLock)
                        {
                            terminal.SetScreenSize(sizeArgs.Columns, sizeArgs.Rows);
                        }
                    }
                    catch
                    {
                        // Ignore resize failures while terminal is closing.
                    }
                };

                form.CopyAllTextProvider = () =>
                {
                    if (terminal == null)
                        return string.Empty;

                    lock (ioLock)
                    {
                        return BuildClipboardText(terminal);
                    }
                };
                form.SelectionTextProvider = selection =>
                {
                    if (terminal == null)
                        return string.Empty;

                    lock (ioLock)
                    {
                        return BuildSelectionClipboardText(terminal, selection);
                    }
                };

                form.ClearScrollbackAction = () =>
                {
                    if (terminal == null)
                        return;

                    lock (ioLock)
                    {
                        ClearScrollbackPreservingScreen(terminal);
                    }
                };

                form.ResetTerminalAction = () =>
                {
                    if (terminal == null)
                        return;

                    lock (ioLock)
                    {
                        ResetTerminalState(terminal);
                    }
                };

                form.Show(Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
                Interlocked.Exchange(ref wasLaunched, 1);
                form.FocusTerminal();
            });

            var pumpTask = Task.Run(async () =>
            {
                try
                {
                    var hadPumpError = await PumpFullAsync(
                        form!,
                        scripting,
                        terminal,
                        ioLock,
                        pendingInput,
                        keepAliveInterval,
                        isConnectionAlive,
                        () => Volatile.Read(ref terminalDisconnected) == 1 ||
                              Volatile.Read(ref terminalRequestedDisconnect) == 1 ||
                              Volatile.Read(ref sawLogoutAfterCtrlD) == 1,
                        () => Volatile.Read(ref inAlternateScreenState) == 1,
                        uiLoopCancellation.Token);
                    if (hadPumpError)
                    {
                        Interlocked.Exchange(ref pumpHadError, 1);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is expected.
                }
                catch (Exception ex)
                {
                    TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Error);
                    if (form != null)
                    {
                        AppendOutputSafe(form, $"\r\n[interactive-error] {ex.Message}\r\n");
                        RequestCloseSafe(form);
                    }
                }
            }, CancellationToken.None);

            await formClosedTcs.Task;
            uiLoopCancellation.Cancel();
            await pumpTask;

            if (terminal != null)
            {
                try
                {
                    if (disconnectedHandler != null)
                        terminal.Disconnected -= disconnectedHandler;
                }
                catch
                {
                    // Ignore handler detach failures during shutdown.
                }

                try
                {
                    if (actionRequestedHandler != null)
                        terminal.ActionRequested -= actionRequestedHandler;
                }
                catch
                {
                    // Ignore handler detach failures during shutdown.
                }

                try
                {
                    if (dataReceivedHandler != null)
                        terminal.DataReceived -= dataReceivedHandler;
                }
                catch
                {
                    // Ignore handler detach failures during shutdown.
                }
            }

            if (Volatile.Read(ref terminalDisconnected) == 1 ||
                Volatile.Read(ref terminalRequestedDisconnect) == 1 ||
                Volatile.Read(ref sawLogoutAfterCtrlD) == 1)
            {
                TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Disconnected);
            }

            if (Volatile.Read(ref pumpHadError) == 1)
            {
                TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Error);
            }

            if (Volatile.Read(ref cancelledByToken) == 1 || cancellationToken.IsCancellationRequested)
            {
                TrySetCloseReason(ref closeReasonState, InteractiveCloseReasonState.Cancelled);
            }

            string transcript;
            lock (transcriptLock)
            {
                var pendingTranscriptTail = PrepareMirroredChunkForEmission(
                    capturedText: null,
                    pendingBuffer: transcriptPendingBuffer,
                    flush: true);
                if (!string.IsNullOrEmpty(pendingTranscriptTail))
                {
                    AppendTranscriptWithCap(
                        transcriptBuilder,
                        pendingTranscriptTail,
                        ref transcriptLineCount,
                        ref transcriptPreviousChunkEndedWithCarriageReturn,
                        ref transcriptCapped);
                }

                transcript = transcriptBuilder.ToString();
            }
            EmitInteractiveTranscriptDebug(
                context,
                $"[interactive-debug interactive-window] transcript_before_prefix=\"{FormatInteractiveDebugText(transcript)}\"");
            transcript = PrependStartupPromptIfMissing(transcript, startupPromptLiteral);
            EmitInteractiveTranscriptDebug(
                context,
                $"[interactive-debug interactive-window] transcript_after_prefix=\"{FormatInteractiveDebugText(transcript)}\"");

            return new InteractiveWindowRunSummary
            {
                WasLaunched = Volatile.Read(ref wasLaunched) == 1,
                CancelledByToken = Volatile.Read(ref cancelledByToken) == 1 || cancellationToken.IsCancellationRequested,
                CloseReason = ResolveCloseReason((InteractiveCloseReasonState)Volatile.Read(ref closeReasonState)),
                Transcript = transcript
            };
        }

        private static async Task<bool> PumpCaptureHeadlessAsync(
            RebexScripting scripting,
            ITerminal? terminal,
            object ioLock,
            ConcurrentQueue<Action<RebexScripting>> pendingInput,
            TimeSpan keepAliveInterval,
            Func<bool>? isConnectionAlive,
            Func<bool>? isCaptureCompleted,
            Func<bool>? isAlternateScreenActive,
            CancellationToken cancellationToken)
        {
            if (terminal == null)
            {
                return true;
            }

            const int processTimeoutMs = 2;
            const int idleDelayMs = 4;
            var hadFatalError = false;
            var nextKeepAliveAtUtc = ComputeNextKeepAliveUtc(keepAliveInterval);

            while (!cancellationToken.IsCancellationRequested)
            {
                var hadPendingInput = !pendingInput.IsEmpty;
                try
                {
                    lock (ioLock)
                    {
                        if (isCaptureCompleted != null && isCaptureCompleted())
                            break;

                        if (isConnectionAlive != null && !isConnectionAlive())
                            break;

                        TrySendKeepAliveIfDue(scripting, keepAliveInterval, ref nextKeepAliveAtUtc);

                        FlushPendingInput(scripting, pendingInput);
                        var terminalState = scripting.Process(processTimeoutMs);

                        if ((terminalState & TerminalState.Disconnected) == TerminalState.Disconnected)
                            break;

                        if (isCaptureCompleted != null && isCaptureCompleted())
                            break;

                        if (isConnectionAlive != null && !isConnectionAlive())
                            break;
                    }
                }
                catch (Exception ex) when (IsTimeoutException(ex))
                {
                    await Task.Delay(25, cancellationToken);
                    continue;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    hadFatalError = true;
                    break;
                }

                // Retain API parity with the full-window pump and keep callback future-proof.
                _ = isAlternateScreenActive?.Invoke();

                if (hadPendingInput)
                    continue;

                await Task.Delay(idleDelayMs, cancellationToken);
            }

            return hadFatalError;
        }

        private static async Task<bool> PumpFullAsync(
            InteractiveTerminalForm form,
            RebexScripting scripting,
            ITerminal? terminal,
            object ioLock,
            ConcurrentQueue<Action<RebexScripting>> pendingInput,
            TimeSpan keepAliveInterval,
            Func<bool>? isConnectionAlive,
            Func<bool>? isTerminalClosed,
            Func<bool>? isAlternateScreenActive,
            CancellationToken cancellationToken,
            bool closeOnTerminalClosed = true)
        {
            if (terminal == null)
            {
                AppendOutputSafe(form, "\r\n[interactive-error] Interactive terminal is unavailable.\r\n");
                RequestCloseSafe(form);
                return true;
            }

            const int processTimeoutMs = 2;
            const int activeDelayMs = 2;
            const int idleDelayMs = 8;
            const int activeRenderIntervalMs = 20;
            const int idleRenderIntervalMs = 120;
            var lastScreenHash = int.MinValue;
            var lastRenderedHistoryLength = 0;
            var lastRequestedOffset = int.MinValue;
            var lastAlternateScreenActive = false;
            var lastObservedHistoryLength = -1;
            var lastObservedCursorLeft = int.MinValue;
            var lastObservedCursorTop = int.MinValue;
            var lastSnapshotTick = Environment.TickCount64;
            var hadFatalError = false;
            var nextKeepAliveAtUtc = ComputeNextKeepAliveUtc(keepAliveInterval);
            var screenUpdateDispatcher = new ScreenUpdateDispatcher(form);

            while (!cancellationToken.IsCancellationRequested)
            {
                TerminalScreenSnapshot? snapshot = null;
                var hadPendingInput = !pendingInput.IsEmpty;
                try
                {
                    lock (ioLock)
                    {
                        if (isTerminalClosed != null && isTerminalClosed())
                        {
                            if (closeOnTerminalClosed)
                                RequestCloseSafe(form);
                            break;
                        }

                        if (isConnectionAlive != null && !isConnectionAlive())
                        {
                            RequestCloseSafe(form);
                            break;
                        }

                        TrySendKeepAliveIfDue(scripting, keepAliveInterval, ref nextKeepAliveAtUtc);

                        FlushPendingInput(scripting, pendingInput);
                        var terminalState = scripting.Process(processTimeoutMs);

                        if ((terminalState & TerminalState.Disconnected) == TerminalState.Disconnected)
                        {
                            if (closeOnTerminalClosed)
                                RequestCloseSafe(form);
                            break;
                        }

                        if (isTerminalClosed != null && isTerminalClosed())
                        {
                            if (closeOnTerminalClosed)
                                RequestCloseSafe(form);
                            break;
                        }

                        if (isConnectionAlive != null && !isConnectionAlive())
                        {
                            RequestCloseSafe(form);
                            break;
                        }

                        var screen = terminal.Screen;
                        var totalHistoryLength = Math.Max(0, terminal.HistoryLength);
                        var cursorLeft = screen.CursorLeft;
                        var cursorTop = screen.CursorTop;
                        var hasTerminalActivity =
                            totalHistoryLength != lastObservedHistoryLength ||
                            cursorLeft != lastObservedCursorLeft ||
                            cursorTop != lastObservedCursorTop;

                        lastObservedHistoryLength = totalHistoryLength;
                        lastObservedCursorLeft = cursorLeft;
                        lastObservedCursorTop = cursorTop;

                        var alternateScreenActive = isAlternateScreenActive != null && isAlternateScreenActive();
                        var requestedOffset = alternateScreenActive ? 0 : form.ScrollbackOffset;
                        if (!alternateScreenActive &&
                            requestedOffset > 0 &&
                            totalHistoryLength > lastRenderedHistoryLength)
                        {
                            // Match PuTTY-like behavior: keep a stable scrollback anchor while new data arrives.
                            requestedOffset = Math.Min(
                                totalHistoryLength,
                                requestedOffset + (totalHistoryLength - lastRenderedHistoryLength));
                        }

                        var viewportChanged =
                            requestedOffset != lastRequestedOffset ||
                            alternateScreenActive != lastAlternateScreenActive;
                        var nowTick = Environment.TickCount64;
                        var renderIntervalMs = hasTerminalActivity ? activeRenderIntervalMs : idleRenderIntervalMs;
                        var rendererBacklogged = screenUpdateDispatcher.IsBacklogged;
                        var renderDue =
                            viewportChanged ||
                            (!rendererBacklogged && unchecked(nowTick - lastSnapshotTick) >= renderIntervalMs);

                        if (renderDue)
                        {
                            snapshot = BuildScreenSnapshot(
                                terminal,
                                requestedOffset,
                                applyFollowTailAnchoring: !alternateScreenActive);
                            lastSnapshotTick = nowTick;
                            lastRenderedHistoryLength = snapshot.HistoryLength;
                            lastRequestedOffset = requestedOffset;
                            lastAlternateScreenActive = alternateScreenActive;
                        }
                    }
                }
                catch (Exception ex) when (IsTimeoutException(ex))
                {
                    await Task.Delay(25, cancellationToken);
                    continue;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    AppendOutputSafe(form, $"\r\n[interactive-error] {ex.Message}\r\n");
                    hadFatalError = true;
                    RequestCloseSafe(form);
                    break;
                }

                if (snapshot != null && snapshot.Hash != lastScreenHash)
                {
                    lastScreenHash = snapshot.Hash;
                    screenUpdateDispatcher.Enqueue(snapshot);
                    if (!hadPendingInput)
                    {
                        await Task.Delay(activeDelayMs, cancellationToken);
                    }
                    continue;
                }

                if (hadPendingInput)
                    continue;

                await Task.Delay(idleDelayMs, cancellationToken);
            }

            return hadFatalError;
        }

        internal static TranscriptCaptureResult FilterTranscriptChunkForAudit(
            string? rawData,
            string? strippedData,
            bool inAlternateScreen)
        {
            return FilterTranscriptChunkForAudit(
                rawData,
                inAlternateScreen,
                () => strippedData);
        }

        internal static TranscriptCaptureResult FilterTranscriptChunkForAudit(
            string? rawData,
            bool inAlternateScreen,
            Func<string?> strippedDataProvider)
        {
            ArgumentNullException.ThrowIfNull(strippedDataProvider);

            if (string.IsNullOrEmpty(rawData))
            {
                if (inAlternateScreen)
                    return new TranscriptCaptureResult(string.Empty, inAlternateScreen);

                var stripped = strippedDataProvider();
                if (string.IsNullOrEmpty(stripped))
                    return new TranscriptCaptureResult(string.Empty, inAlternateScreen);

                return new TranscriptCaptureResult(stripped, inAlternateScreen);
            }

            if (rawData.IndexOf("\u001B[?", StringComparison.Ordinal) < 0)
            {
                if (inAlternateScreen)
                    return new TranscriptCaptureResult(string.Empty, inAlternateScreen);

                return new TranscriptCaptureResult(strippedDataProvider() ?? string.Empty, inAlternateScreen);
            }

            var matches = AlternateScreenSequenceRegex.Matches(rawData);
            if (matches.Count == 0)
            {
                if (inAlternateScreen)
                    return new TranscriptCaptureResult(string.Empty, inAlternateScreen);

                return new TranscriptCaptureResult(strippedDataProvider() ?? string.Empty, inAlternateScreen);
            }

            var nextAlternateState = inAlternateScreen;
            var sawEnter = false;
            var sawLeave = false;
            var firstEnterIndex = -1;
            foreach (Match match in matches)
            {
                var mode = match.Groups["mode"].Value;
                if (string.Equals(mode, "h", StringComparison.Ordinal))
                {
                    nextAlternateState = true;
                    sawEnter = true;
                    if (firstEnterIndex < 0)
                        firstEnterIndex = match.Index;
                }
                else
                {
                    nextAlternateState = false;
                    sawLeave = true;
                }
            }

            if (sawEnter)
            {
                var preservedPrefix = CaptureRawPrefixBeforeAlternateScreen(rawData, firstEnterIndex);
                if (!string.IsNullOrEmpty(preservedPrefix))
                    return new TranscriptCaptureResult(preservedPrefix, nextAlternateState);

                if (!nextAlternateState && sawLeave)
                    return new TranscriptCaptureResult(strippedDataProvider() ?? string.Empty, false);

                return new TranscriptCaptureResult(string.Empty, nextAlternateState);
            }

            if (nextAlternateState)
                return new TranscriptCaptureResult(string.Empty, true);

            // Leaving alternate screen often includes the prompt in stripped data; keep that.
            return new TranscriptCaptureResult(strippedDataProvider() ?? string.Empty, false);
        }

        private static string CaptureRawPrefixBeforeAlternateScreen(string rawData, int firstEnterIndex)
        {
            if (firstEnterIndex <= 0 || firstEnterIndex > rawData.Length)
                return string.Empty;

            var prefix = rawData[..firstEnterIndex];
            return TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(prefix));
        }

        private static bool ContainsLogoutLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Match common shell exit marker seen after Ctrl+D on Linux shells.
            return text.Contains("\nlogout\n", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("\r\nlogout\r\n", StringComparison.OrdinalIgnoreCase) ||
                   text.Trim().Equals("logout", StringComparison.OrdinalIgnoreCase);
        }

        private static void TrySetCloseReason(
            ref int closeReasonState,
            InteractiveCloseReasonState nextReason)
        {
            _ = Interlocked.CompareExchange(
                ref closeReasonState,
                (int)nextReason,
                (int)InteractiveCloseReasonState.UserClosed);
        }

        private static string ResolveCloseReason(InteractiveCloseReasonState reason)
        {
            return reason switch
            {
                InteractiveCloseReasonState.Disconnected => InteractiveCloseReasonDisconnected,
                InteractiveCloseReasonState.Cancelled => InteractiveCloseReasonCancelled,
                InteractiveCloseReasonState.Error => InteractiveCloseReasonError,
                _ => InteractiveCloseReasonUserClosed
            };
        }

        internal static bool IsDetachedCaptureCompletionReason(string closeReason)
        {
            return string.Equals(closeReason, InteractiveCloseReasonCtrlCContinue, StringComparison.Ordinal) ||
                   string.Equals(closeReason, InteractiveCloseReasonTimeoutContinue, StringComparison.Ordinal) ||
                   string.Equals(closeReason, InteractiveCloseReasonMaxLinesContinue, StringComparison.Ordinal) ||
                   string.Equals(closeReason, InteractiveCloseReasonNaturalComplete, StringComparison.Ordinal);
        }

        internal static bool IsCaptureSuccessCloseReason(string closeReason)
        {
            return IsDetachedCaptureCompletionReason(closeReason) ||
                   string.Equals(closeReason, InteractiveCloseReasonEarlyClosePartial, StringComparison.Ordinal) ||
                   string.Equals(closeReason, InteractiveCloseReasonUserClosed, StringComparison.Ordinal);
        }

        internal static bool ShouldMirrorCaptureChunk(bool mirrorOutput, string? capturedText)
        {
            return mirrorOutput && !string.IsNullOrEmpty(capturedText);
        }

        internal static void AppendTranscriptWithCap(
            StringBuilder transcriptBuilder,
            string? capturedText,
            ref int transcriptLineCount,
            ref bool previousChunkEndedWithCarriageReturn,
            ref bool transcriptCapped)
        {
            ArgumentNullException.ThrowIfNull(transcriptBuilder);
            if (transcriptCapped || string.IsNullOrEmpty(capturedText))
                return;

            transcriptLineCount = Math.Max(0, transcriptLineCount);
            if (transcriptLineCount >= InteractiveTranscriptMaxLines)
            {
                AppendTranscriptCapNotice(
                    transcriptBuilder,
                    ref transcriptLineCount,
                    ref previousChunkEndedWithCarriageReturn,
                    ref transcriptCapped);
                return;
            }

            var remainingLines = InteractiveTranscriptMaxLines - transcriptLineCount;
            var prefixLength = GetMirrorChunkPrefixLengthWithinCaps(
                capturedText,
                remainingLines,
                previousChunkEndedWithCarriageReturn,
                out var addedLines,
                out var endsWithCarriageReturn);
            if (prefixLength > 0)
            {
                transcriptBuilder.Append(capturedText, 0, prefixLength);
            }

            transcriptLineCount += addedLines;
            previousChunkEndedWithCarriageReturn = endsWithCarriageReturn;

            if (prefixLength < capturedText.Length || transcriptLineCount >= InteractiveTranscriptMaxLines)
            {
                AppendTranscriptCapNotice(
                    transcriptBuilder,
                    ref transcriptLineCount,
                    ref previousChunkEndedWithCarriageReturn,
                    ref transcriptCapped);
            }
        }

        internal static string NormalizeMirroredTranscript(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(text));
        }

        internal static (
            string Output,
            int EmittedLines,
            bool IsCapped,
            bool PreviousChunkEndedWithCarriageReturn) ApplyMirrorOutputCap(
            string? chunk,
            int emittedLines,
            bool isCapped,
            bool previousChunkEndedWithCarriageReturn)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return (
                    string.Empty,
                    Math.Max(0, emittedLines),
                    isCapped,
                    previousChunkEndedWithCarriageReturn);
            }

            var safeEmittedLines = Math.Max(0, emittedLines);
            if (isCapped || safeEmittedLines >= InteractiveMirrorOutputMaxLines)
            {
                return (
                    string.Empty,
                    InteractiveMirrorOutputMaxLines,
                    true,
                    previousChunkEndedWithCarriageReturn);
            }

            var remainingLines = InteractiveMirrorOutputMaxLines - safeEmittedLines;
            var prefixLength = GetMirrorChunkPrefixLengthWithinCaps(
                chunk,
                remainingLines,
                previousChunkEndedWithCarriageReturn,
                out var addedLines,
                out var endsWithCarriageReturn);
            if (prefixLength >= chunk.Length)
            {
                var newLineCount = safeEmittedLines + addedLines;
                var reachedCap = newLineCount >= InteractiveMirrorOutputMaxLines;
                return (
                    chunk,
                    newLineCount,
                    reachedCap,
                    endsWithCarriageReturn);
            }

            var prefix = prefixLength > 0 ? chunk.Substring(0, prefixLength) : string.Empty;
            return (
                prefix + InteractiveMirrorOutputCapNotice,
                InteractiveMirrorOutputMaxLines,
                true,
                false);
        }

        private static void AppendTranscriptCapNotice(
            StringBuilder transcriptBuilder,
            ref int transcriptLineCount,
            ref bool previousChunkEndedWithCarriageReturn,
            ref bool transcriptCapped)
        {
            if (transcriptCapped)
                return;

            transcriptBuilder.Append(InteractiveTranscriptCapNotice);
            transcriptLineCount = InteractiveTranscriptMaxLines;
            previousChunkEndedWithCarriageReturn = false;
            transcriptCapped = true;
        }

        private static int GetMirrorChunkPrefixLengthWithinCaps(
            string chunk,
            int remainingLines,
            bool previousChunkEndedWithCarriageReturn,
            out int addedLines,
            out bool endsWithCarriageReturn)
        {
            var takeLength = 0;
            var linesAdded = 0;
            var safeRemainingLines = Math.Max(0, remainingLines);
            var previousWasCarriageReturn = previousChunkEndedWithCarriageReturn;

            for (var i = 0; i < chunk.Length; i++)
            {
                var current = chunk[i];
                if (safeRemainingLines <= 0 &&
                    !(previousWasCarriageReturn && current == '\n'))
                {
                    break;
                }

                if (current == '\n')
                {
                    if (!previousWasCarriageReturn)
                    {
                        if (safeRemainingLines <= 0)
                            break;

                        safeRemainingLines--;
                        linesAdded++;
                    }

                    previousWasCarriageReturn = false;
                }
                else if (current == '\r')
                {
                    if (safeRemainingLines <= 0)
                        break;

                    safeRemainingLines--;
                    linesAdded++;
                    previousWasCarriageReturn = true;
                }
                else
                {
                    previousWasCarriageReturn = false;
                }

                takeLength++;
            }

            addedLines = linesAdded;
            endsWithCarriageReturn = previousWasCarriageReturn;
            return takeLength;
        }

        internal static string PrepareMirroredChunkForEmission(
            string? capturedText,
            StringBuilder pendingBuffer,
            bool flush)
        {
            ArgumentNullException.ThrowIfNull(pendingBuffer);

            if (!string.IsNullOrEmpty(capturedText))
            {
                pendingBuffer.Append(capturedText);
            }

            if (pendingBuffer.Length == 0)
                return string.Empty;

            var emitLength = flush ? pendingBuffer.Length : GetMirrorEmitLength(pendingBuffer);
            if (emitLength <= 0)
                return string.Empty;

            var rawSegment = pendingBuffer.ToString(0, emitLength);
            pendingBuffer.Remove(0, emitLength);
            return NormalizeMirroredTranscript(rawSegment);
        }

        private static int GetMirrorEmitLength(StringBuilder pendingBuffer)
        {
            for (var i = pendingBuffer.Length - 1; i >= 0; i--)
            {
                if (pendingBuffer[i] == '\n')
                    return i + 1;
            }

            return 0;
        }

        internal static int CountLinesFromCapturedChunk(
            string? capturedText,
            ref bool previousChunkEndedWithCarriageReturn)
        {
            if (string.IsNullOrEmpty(capturedText))
                return 0;

            var lines = 0;
            var previousWasCr = previousChunkEndedWithCarriageReturn;

            for (var i = 0; i < capturedText.Length; i++)
            {
                var current = capturedText[i];
                if (current == '\n')
                {
                    if (!previousWasCr)
                    {
                        lines++;
                    }

                    previousWasCr = false;
                    continue;
                }

                if (current == '\r')
                {
                    lines++;
                    previousWasCr = true;
                    continue;
                }

                previousWasCr = false;
            }

            previousChunkEndedWithCarriageReturn = previousWasCr;
            return lines;
        }

        internal static bool ShouldArmCaptureNaturalCompletion(string? text, string command, Regex? promptRegex = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (ContainsCommandEchoLine(text, command))
                return true;

            return !ShouldCompleteCaptureOnPrompt(text, promptRegex);
        }

        internal static bool ShouldCompleteCaptureOnPrompt(string? text, Regex? promptRegex = null)
        {
            return ContainsLikelyPromptLine(text, promptRegex);
        }

        private static bool ContainsCommandEchoLine(string? text, string command)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(command))
                return false;

            var normalizedCommand = command.Trim();
            if (normalizedCommand.Length == 0)
                return false;

            var normalizedText = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            var lines = normalizedText.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                if (line.IndexOf(normalizedCommand, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool ContainsLikelyPromptLine(string? text, Regex? promptRegex = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var normalized = TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(text));
            var lines = normalized.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].TrimEnd();
                if (line.Length == 0)
                    continue;

                if (promptRegex != null)
                    return promptRegex.IsMatch(line);

                return PromptDetector.IsLikelyPrompt(line);
            }

            return false;
        }

        private static CapturePromptSetup TryBuildCapturePromptSetup(string? startupOutput)
        {
            if (string.IsNullOrWhiteSpace(startupOutput))
                return new CapturePromptSetup(null, string.Empty);

            var normalized = TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(startupOutput));
            if (!PromptDetector.TryDetectPromptFromTail(normalized, out var startupPrompt) ||
                string.IsNullOrWhiteSpace(startupPrompt))
            {
                return new CapturePromptSetup(null, string.Empty);
            }

            return new CapturePromptSetup(
                PromptDetector.BuildPromptRegex(startupPrompt),
                startupPrompt.TrimEnd());
        }

        private static async Task<CapturePromptSetup> InitializeSeparateSessionStartupAsync(
            RebexScripting scripting,
            SshTimeoutOptions timeouts,
            bool debugMode,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(scripting);
            ArgumentNullException.ThrowIfNull(timeouts);

            var startupOutput = new StringBuilder();
            var anyDataEvent = ScriptEvent.FromRegex(@"[\s\S]");
            var maxBannerAttempts = 3;
            var maxBannerAccepts = 5;
            var bannerAcceptCount = 0;
            var pollInterval = debugMode ? 3000 : 1000;
            var savedTimeout = scripting.Timeout;
            var overallTimeout = Math.Max(1, (int)timeouts.InitialPromptTimeout.TotalMilliseconds);

            try
            {
                scripting.Timeout = overallTimeout;

                for (var attempt = 0; attempt < maxBannerAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var attemptSw = System.Diagnostics.Stopwatch.StartNew();

                    while (attemptSw.ElapsedMilliseconds < overallTimeout)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        scripting.Timeout = pollInterval;

                        string chunk;
                        try
                        {
                            chunk = scripting.ReadUntil(anyDataEvent);
                        }
                        catch (Exception ex) when (IsTimeoutException(ex))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(chunk))
                            continue;

                        startupOutput.Append(chunk);
                        var capturePromptSetup = TryBuildCapturePromptSetup(startupOutput.ToString());
                        if (capturePromptSetup.PromptRegex != null)
                        {
                            FlushCaptureStartupBuffer(scripting, cancellationToken);
                            return capturePromptSetup;
                        }

                        var normalized = TerminalOutputProcessor.Normalize(
                            TerminalOutputProcessor.Sanitize(startupOutput.ToString()));
                        var bannerMatch = SshShellSession.Patterns.BannerAcceptPrompt.Match(normalized);
                        if (!bannerMatch.Success)
                            continue;

                        var acceptKey = ResolveBannerAcceptKey(bannerMatch);
                        if (bannerAcceptCount >= maxBannerAccepts)
                        {
                            scripting.Send(acceptKey + "\r");
                            startupOutput.Clear();
                            bannerAcceptCount++;
                            await Task.Delay(500, cancellationToken);
                            if (bannerAcceptCount > maxBannerAccepts + 2)
                                break;

                            continue;
                        }

                        bannerAcceptCount++;
                        scripting.Send(acceptKey);
                        startupOutput.Clear();
                        await Task.Delay(500, cancellationToken);
                    }

                    if (attempt < maxBannerAttempts - 1)
                    {
                        scripting.Send("\r");
                        await Task.Delay(200, cancellationToken);
                    }
                }

                return TryBuildCapturePromptSetup(startupOutput.ToString());
            }
            finally
            {
                scripting.Timeout = savedTimeout;
            }
        }

        internal static string BuildMirroredStartupPromptPrefix(string? startupPromptLiteral)
        {
            if (string.IsNullOrWhiteSpace(startupPromptLiteral))
                return string.Empty;

            var prompt = startupPromptLiteral.TrimEnd();
            if (prompt.Length == 0)
                return string.Empty;

            var last = prompt[^1];
            if (last == '\r' || last == '\n')
                return prompt;

            if (char.IsWhiteSpace(last))
                return prompt;

            return prompt + " ";
        }

        internal static string ResolveStartupPromptLiteral(string? detectedPromptLiteral, string? sessionPromptLiteral)
        {
            var detected = detectedPromptLiteral?.TrimEnd();
            if (!string.IsNullOrWhiteSpace(detected))
                return detected;

            var fallback = sessionPromptLiteral?.TrimEnd();
            if (!string.IsNullOrWhiteSpace(fallback))
                return fallback;

            return string.Empty;
        }

        internal static string PrependStartupPromptIfMissing(string? transcript, string? startupPromptLiteral)
        {
            if (string.IsNullOrEmpty(transcript))
                return transcript ?? string.Empty;

            var prefix = BuildMirroredStartupPromptPrefix(startupPromptLiteral);
            if (string.IsNullOrEmpty(prefix))
                return transcript;

            if (transcript.StartsWith(prefix, StringComparison.Ordinal))
                return transcript;

            var promptLiteral = startupPromptLiteral?.TrimEnd();
            if (!string.IsNullOrEmpty(promptLiteral) &&
                transcript.StartsWith(promptLiteral, StringComparison.Ordinal))
            {
                return transcript;
            }

            var normalizedTranscript = transcript.TrimStart('\r', '\n');
            return prefix + normalizedTranscript;
        }

        internal static string ResolveBannerAcceptKey(Match bannerMatch)
        {
            for (var i = 1; i <= 4; i++)
            {
                if (bannerMatch.Groups[i].Success && !string.IsNullOrEmpty(bannerMatch.Groups[i].Value))
                {
                    return bannerMatch.Groups[i].Value.ToLowerInvariant();
                }
            }

            return "a";
        }

        private static InteractiveTerminalSessionDetails CreateSessionDetails(
            InteractiveWindowRunSummary summary,
            ScriptContext context,
            string hostAddress,
            InteractiveOptions options,
            DateTime startedAtUtc,
            DateTime endedAtUtc)
        {
            var cleanedTranscript = CleanTranscriptForAudit(summary.Transcript);
            EmitInteractiveTranscriptDebug(
                context,
                $"[interactive-debug session-details] transcript_before_clean=\"{FormatInteractiveDebugText(summary.Transcript)}\"");
            EmitInteractiveTranscriptDebug(
                context,
                $"[interactive-debug session-details] transcript_after_clean=\"{FormatInteractiveDebugText(cleanedTranscript)}\"");

            return new InteractiveTerminalSessionDetails
            {
                HostAddress = hostAddress ?? string.Empty,
                SessionMode = options.Session.ToString().ToLowerInvariant(),
                EmulationMode = string.Empty,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                CloseReason = summary.CloseReason,
                Completed = summary.Completed,
                Transcript = cleanedTranscript
            };
        }

        internal static bool ShouldCloseSharedWindowWithoutSendingEof(
            InteractiveSessionMode sessionMode,
            TerminalKeyEventArgs keyArgs)
        {
            return sessionMode == InteractiveSessionMode.Shared &&
                   keyArgs.ConsoleKey == ConsoleKey.D &&
                   (keyArgs.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control;
        }

        internal static bool ShouldBlockSharedShellCommand(string? commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return false;

            var normalized = commandText.Trim();
            return string.Equals(normalized, "exit", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "logout", StringComparison.OrdinalIgnoreCase);
        }

        internal static string CleanTranscriptForAudit(string? transcript)
        {
            if (string.IsNullOrEmpty(transcript))
                return string.Empty;

            if (transcript.IndexOf('\b') < 0 && transcript.IndexOf((char)0x7F) < 0)
                return transcript;

            // Transcript audit should reflect the final visible command line state.
            // Some devices emit DEL (0x7F) for erase/backspace behavior, so map it
            // to backspace before running cursor-aware normalization.
            var normalizedInput = transcript.IndexOf((char)0x7F) >= 0
                ? transcript.Replace((char)0x7F, '\b')
                : transcript;
            return TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(normalizedInput));
        }

        internal static string ResolveTranscriptAssemblyInput(
            string? rawData,
            string? capturedText,
            bool inAlternateBefore,
            bool inAlternateAfter)
        {
            if (!inAlternateBefore &&
                !inAlternateAfter &&
                !string.IsNullOrEmpty(rawData) &&
                rawData.IndexOf("\u001B[?", StringComparison.Ordinal) < 0)
            {
                return rawData;
            }

            return capturedText ?? string.Empty;
        }

        internal static bool ShouldEmitTranscriptChunkDebug(
            string? rawData,
            string? strippedData,
            string? capturedText)
        {
            if (ContainsTranscriptDebugControlCharacters(rawData) ||
                ContainsTranscriptDebugControlCharacters(strippedData) ||
                ContainsTranscriptDebugControlCharacters(capturedText))
            {
                return true;
            }

            return !string.Equals(
                strippedData ?? string.Empty,
                capturedText ?? string.Empty,
                StringComparison.Ordinal);
        }

        internal static string FormatInteractiveDebugText(string? value, int maxLength = 240)
        {
            if (string.IsNullOrEmpty(value))
                return "<empty>";

            var escaped = EscapeControlCharactersForDebug(value);
            var safeLength = Math.Max(8, maxLength);
            if (escaped.Length <= safeLength)
                return escaped;

            var remaining = escaped.Length - safeLength;
            return $"{escaped[..safeLength]}...[+{remaining} chars]";
        }

        private static string BuildTranscriptChunkDebugMessage(
            string phase,
            bool inAlternateBefore,
            bool inAlternateAfter,
            string? rawData,
            string? strippedData,
            string? capturedText)
        {
            return
                $"[interactive-debug {phase}] alt={inAlternateBefore}->{inAlternateAfter} " +
                $"raw=\"{FormatInteractiveDebugText(rawData)}\" " +
                $"stripped=\"{FormatInteractiveDebugText(strippedData)}\" " +
                $"captured=\"{FormatInteractiveDebugText(capturedText)}\"";
        }

        private static bool ContainsTranscriptDebugControlCharacters(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (var index = 0; index < value.Length; index++)
            {
                var current = value[index];
                if (current == '\b' ||
                    current == '\t' ||
                    current == (char)0x7F ||
                    current == (char)0x1B)
                {
                    return true;
                }

                if (current == '\r')
                {
                    var nextIsLineFeed = index + 1 < value.Length && value[index + 1] == '\n';
                    if (!nextIsLineFeed)
                        return true;
                }
            }

            return false;
        }

        private static string EscapeControlCharactersForDebug(string value)
        {
            var escaped = new StringBuilder(value.Length + 32);
            foreach (var current in value)
            {
                switch (current)
                {
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    case '\b':
                        escaped.Append("\\b");
                        break;
                    case (char)0x7F:
                        escaped.Append("\\x7F");
                        break;
                    case (char)0x1B:
                        escaped.Append("\\x1B");
                        break;
                    default:
                        if (char.IsControl(current))
                        {
                            escaped.Append("\\x");
                            escaped.Append(((int)current).ToString("X2"));
                        }
                        else
                        {
                            escaped.Append(current);
                        }
                        break;
                }
            }

            return escaped.ToString();
        }

        private static void EmitInteractiveTranscriptDebug(ScriptContext context, string? message)
        {
            if (string.IsNullOrEmpty(message) || !context.DebugMode)
                return;

            context.EmitOutput(message, ScriptOutputType.Debug);
            System.Diagnostics.Debug.WriteLine(message);
        }

        private static bool ShouldBlockSharedShellCommandOnEnter(
            SharedCommandGuardState guardState,
            TerminalKeyEventArgs keyArgs)
        {
            if (keyArgs.FunctionKey != FunctionKey.Enter)
                return false;

            return ShouldBlockSharedShellCommand(guardState.CurrentLine.ToString());
        }

        private static void UpdateSharedCommandGuardForKey(
            SharedCommandGuardState guardState,
            TerminalKeyEventArgs keyArgs)
        {
            if (keyArgs.FunctionKey == FunctionKey.Enter)
            {
                guardState.Reset();
                return;
            }

            if (keyArgs.FunctionKey == FunctionKey.Backspace)
            {
                if (guardState.SentCharactersOnLine > 0 &&
                    guardState.CurrentLine.Length > 0)
                {
                    guardState.CurrentLine.Remove(guardState.CurrentLine.Length - 1, 1);
                    guardState.SentCharactersOnLine--;
                }

                return;
            }

            if (keyArgs.FunctionKey == FunctionKey.Tab)
            {
                guardState.CurrentLine.Append('\t');
                guardState.SentCharactersOnLine++;
                return;
            }

            if (keyArgs.FunctionKey.HasValue || keyArgs.ConsoleKey.HasValue)
            {
                // Cursor navigation and control key combos can alter line state in ways this local buffer
                // cannot reliably reconstruct, so drop tracking until the user starts typing again.
                guardState.Reset();
            }
        }

        private static bool EnqueueSharedTextInput(
            SharedCommandGuardState guardState,
            ConcurrentQueue<Action<RebexScripting>> pendingInput,
            string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var pendingSegment = new StringBuilder();

            foreach (var nextChar in text)
            {
                if (nextChar == '\r')
                    continue;

                if (nextChar == '\n')
                {
                    if (ShouldBlockSharedShellCommand(guardState.CurrentLine.ToString()))
                    {
                        return true;
                    }

                    pendingSegment.Append('\n');
                    var lineToSend = pendingSegment.ToString();
                    pendingInput.Enqueue(stream => stream.Send(lineToSend));
                    guardState.Reset();
                    pendingSegment.Clear();
                    continue;
                }

                guardState.CurrentLine.Append(nextChar);
                pendingSegment.Append(nextChar);
            }

            if (pendingSegment.Length <= 0)
                return false;

            var textToSend = pendingSegment.ToString();
            pendingInput.Enqueue(stream => stream.Send(textToSend));
            guardState.SentCharactersOnLine += textToSend.Length;
            return false;
        }

        private static void EnqueueSharedDetachRequest(
            SharedCommandGuardState guardState,
            ConcurrentQueue<Action<RebexScripting>> pendingInput,
            InteractiveTerminalForm form)
        {
            var repeatCount = Math.Max(0, guardState.SentCharactersOnLine);
            guardState.Reset();

            pendingInput.Enqueue(stream =>
            {
                for (var i = 0; i < repeatCount; i++)
                {
                    stream.Send(FunctionKey.Backspace, ConsoleModifiers.None);
                }

                RequestCloseSafe(form);
            });
        }

        private static void FlushPendingInput(
            RebexScripting scripting,
            ConcurrentQueue<Action<RebexScripting>> pendingInput)
        {
            while (pendingInput.TryDequeue(out var sendAction))
            {
                try
                {
                    sendAction(scripting);
                }
                catch
                {
                    // Ignore send failures while terminal is closing.
                }
            }
        }

        private static DateTime ComputeNextKeepAliveUtc(TimeSpan keepAliveInterval)
        {
            if (keepAliveInterval <= TimeSpan.Zero)
                return DateTime.MaxValue;

            return DateTime.UtcNow + keepAliveInterval;
        }

        private static void TrySendKeepAliveIfDue(
            RebexScripting scripting,
            TimeSpan keepAliveInterval,
            ref DateTime nextKeepAliveAtUtc)
        {
            if (keepAliveInterval <= TimeSpan.Zero)
                return;

            var now = DateTime.UtcNow;
            if (now < nextKeepAliveAtUtc)
                return;

            try
            {
                scripting.KeepAlive();
            }
            catch
            {
                // Ignore keepalive failures; main pump loop still handles disconnect signals.
            }
            finally
            {
                nextKeepAliveAtUtc = now + keepAliveInterval;
            }
        }

        private static void AppendOutputSafe(InteractiveTerminalForm form, string text)
        {
            if (form.IsDisposed || string.IsNullOrEmpty(text))
                return;

            try
            {
                if (form.InvokeRequired)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        if (!form.IsDisposed)
                            form.AppendOutput(text);
                    }));
                }
                else
                {
                    form.AppendOutput(text);
                }
            }
            catch
            {
                // UI closed while appending.
            }
        }

        private static void RequestCloseSafe(InteractiveTerminalForm form)
        {
            if (form.IsDisposed)
                return;

            try
            {
                if (form.InvokeRequired)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        if (!form.IsDisposed)
                            form.Close();
                    }));
                }
                else
                {
                    form.Close();
                }
            }
            catch
            {
                // UI closed while requesting close.
            }
        }

        private static TerminalScreenSnapshot BuildScreenSnapshot(
            ITerminal terminal,
            int scrollbackOffset,
            bool applyFollowTailAnchoring = true)
        {
            var screen = terminal.Screen;
            var columns = Math.Max(1, screen.Columns);
            var rows = Math.Max(1, screen.Rows);
            var historyLength = Math.Max(0, terminal.HistoryLength);
            var appliedOffset = Math.Clamp(scrollbackOffset, 0, historyLength);
            var effectiveOffset = appliedOffset;
            if (applyFollowTailAnchoring && appliedOffset == 0)
            {
                // In follow-tail mode, keep the host cursor visually anchored near the bottom after resize,
                // matching the behavior users expect from terminal emulators like PuTTY.
                var cursorTop = Math.Clamp(screen.CursorTop, 0, rows - 1);
                var tailAnchorOffset = Math.Max(0, (rows - 1) - cursorTop);
                effectiveOffset = Math.Min(historyLength, tailAnchorOffset);
            }
            var palette = terminal.Palette;
            var count = columns * rows;
            var characters = new char[count];
            var foreColors = new int[count];
            var backColors = new int[count];
            var rowHashes = new int[rows];
            var hash = 17;
            var paletteCache = new Dictionary<int, int>();
            var defaultForeColor = ResolvePaletteColorArgb(
                palette,
                TerminalColor.LightGray,
                Color.FromArgb(187, 187, 187).ToArgb(),
                paletteCache);
            var defaultBackColor = ResolvePaletteColorArgb(
                palette,
                TerminalColor.Black,
                Color.Black.ToArgb(),
                paletteCache);

            hash = CombineHash(hash, columns);
            hash = CombineHash(hash, rows);
            hash = CombineHash(hash, historyLength);
            hash = CombineHash(hash, appliedOffset);
            hash = CombineHash(hash, effectiveOffset);

            var absoluteIndex = 0;
            for (var row = 0; row < rows; row++)
            {
                var rowHash = 17;
                var sourceRow = row - effectiveOffset;
                for (var column = 0; column < columns; column++)
                {
                    var cell = screen.GetCell(column, sourceRow);
                    var character = cell.Character == '\0' ? ' ' : cell.Character;
                    var foreColorIndex = cell.Bold && cell.ForeColor >= TerminalColor.Black && cell.ForeColor <= TerminalColor.LightGray
                        ? cell.ForeColor + 8
                        : cell.ForeColor;
                    var foreColor = ResolvePaletteColorArgb(palette, foreColorIndex, defaultForeColor, paletteCache);
                    var backColor = ResolvePaletteColorArgb(palette, cell.BackColor, defaultBackColor, paletteCache);

                    // Some streams resolve "default foreground" to palette black (0) on black background,
                    // which makes plain text invisible. Normalize that case back to default foreground.
                    if (backColor == defaultBackColor &&
                        foreColor == defaultBackColor)
                    {
                        foreColor = defaultForeColor;
                    }

                    rowHash = CombineHash(rowHash, character);
                    rowHash = CombineHash(rowHash, foreColor);
                    rowHash = CombineHash(rowHash, backColor);
                    characters[absoluteIndex] = character;
                    foreColors[absoluteIndex] = foreColor;
                    backColors[absoluteIndex] = backColor;

                    absoluteIndex++;
                }

                rowHashes[row] = rowHash;
                hash = CombineHash(hash, rowHash);
            }

            var cursorColumn = -1;
            var cursorRow = -1;
            var cursorForeColor = defaultBackColor;
            var cursorBackColor = defaultForeColor;
            var fallbackCursorBackColor = ResolvePaletteColorArgb(
                palette,
                TerminalColor.LightGreen,
                Color.Lime.ToArgb(),
                paletteCache);

            var viewportCursorRow = screen.CursorTop + effectiveOffset;
            if (screen.CursorLeft >= 0 &&
                screen.CursorLeft < columns &&
                viewportCursorRow >= 0 &&
                viewportCursorRow < rows)
            {
                cursorColumn = screen.CursorLeft;
                cursorRow = viewportCursorRow;
                var cursorIndex = cursorRow * columns + cursorColumn;
                var cellForeColor = foreColors[cursorIndex];
                var cellBackColor = backColors[cursorIndex];
                cursorForeColor = cellBackColor;
                cursorBackColor = cellForeColor;

                // Keep a PuTTY-like visible cursor on default blank cells (common after resize/tail anchoring).
                if (cursorForeColor == cursorBackColor ||
                    (cellForeColor == defaultForeColor && cellBackColor == defaultBackColor))
                {
                    cursorForeColor = defaultBackColor;
                    cursorBackColor = fallbackCursorBackColor;
                }
            }

            hash = CombineHash(hash, cursorColumn);
            hash = CombineHash(hash, cursorRow);
            hash = CombineHash(hash, cursorForeColor);
            hash = CombineHash(hash, cursorBackColor);

            return new TerminalScreenSnapshot
            {
                Columns = columns,
                Rows = rows,
                HistoryLength = historyLength,
                ScrollbackOffset = appliedOffset,
                EffectiveScrollOffset = effectiveOffset,
                Characters = characters,
                ForeColors = foreColors,
                BackColors = backColors,
                RowHashes = rowHashes,
                CursorColumn = cursorColumn,
                CursorRow = cursorRow,
                CursorForeColor = cursorForeColor,
                CursorBackColor = cursorBackColor,
                Hash = hash
            };
        }

        private static string BuildClipboardText(ITerminal terminal)
        {
            var screen = terminal.Screen;
            var columns = Math.Max(1, screen.Columns);
            var rows = Math.Max(1, screen.Rows);
            var historyLength = Math.Max(0, terminal.HistoryLength);
            var totalRows = historyLength + rows;
            if (totalRows <= 0)
                return string.Empty;

            var lines = screen.GetRegionText(0, -historyLength, columns, totalRows);
            if (lines == null || lines.Length == 0)
                return string.Empty;

            var builder = new StringBuilder(lines.Length * Math.Max(8, columns));
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    builder.AppendLine();

                builder.Append((lines[i] ?? string.Empty).TrimEnd(' '));
            }

            return builder.ToString();
        }

        private static string BuildSelectionClipboardText(ITerminal terminal, TerminalBufferSelection selection)
        {
            var screen = terminal.Screen;
            var historyLength = Math.Max(0, terminal.HistoryLength);
            var rows = Math.Max(1, screen.Rows);
            var columns = Math.Max(1, selection.Columns);
            var totalRows = historyLength + rows;
            if (totalRows <= 0)
                return string.Empty;

            var startRow = Math.Clamp(selection.StartBufferRow, 0, totalRows - 1);
            var endRow = Math.Clamp(selection.EndBufferRow, 0, totalRows - 1);
            if (endRow < startRow)
                return string.Empty;

            var startColumn = Math.Clamp(selection.StartColumn, 0, columns - 1);
            var endColumn = Math.Clamp(selection.EndColumn, 0, columns - 1);
            var lines = new List<string>(endRow - startRow + 1);

            for (var row = startRow; row <= endRow; row++)
            {
                var lineStartColumn = row == startRow ? startColumn : 0;
                var lineEndColumn = row == endRow ? endColumn : columns - 1;
                if (lineEndColumn < lineStartColumn)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                var width = lineEndColumn - lineStartColumn + 1;
                var sourceRow = row - historyLength;
                var region = screen.GetRegionText(lineStartColumn, sourceRow, width, 1);
                var line = region != null && region.Length > 0
                    ? region[0] ?? string.Empty
                    : string.Empty;
                if (line.Length > width)
                {
                    line = line[..width];
                }

                lines.Add(line.TrimEnd(' '));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void ClearScrollbackPreservingScreen(ITerminal terminal)
        {
            var screen = terminal.Screen;
            var columns = Math.Max(1, screen.Columns);
            var rows = Math.Max(1, screen.Rows);
            var cursorLeft = screen.CursorLeft;
            var cursorTop = screen.CursorTop;
            var region = screen.GetRegion(0, 0, columns, rows);
            var cells = new TerminalCell[columns, rows];

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    cells[column, row] = region[column, row];
                }
            }

            screen.Clear(true);

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    screen.SetCell(column, row, cells[column, row]);
                }
            }

            var maxColumn = Math.Max(0, columns - 1);
            var maxRow = Math.Max(0, rows - 1);
            screen.SetCursorPosition(
                Math.Clamp(cursorLeft, 0, maxColumn),
                Math.Clamp(cursorTop, 0, maxRow));
        }

        private static void ResetTerminalState(ITerminal terminal)
        {
            terminal.Screen.Clear(true);
        }

        private static int CombineHash(int current, int value)
        {
            unchecked
            {
                return (current * 397) ^ value;
            }
        }

        private static int ResolvePaletteColorArgb(
            TerminalPalette? palette,
            int paletteIndex,
            int fallback,
            Dictionary<int, int> paletteCache)
        {
            if (palette == null || paletteIndex < 0)
                return fallback;

            if (paletteCache.TryGetValue(paletteIndex, out var cachedArgb))
                return cachedArgb;

            try
            {
                var colorArgb = palette.GetColor(paletteIndex).ToArgb();
                paletteCache[paletteIndex] = colorArgb;
                return colorArgb;
            }
            catch
            {
                return fallback;
            }
        }

        private static Task InvokeOnUiThreadAsync(Action action)
        {
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (mainForm == null || !mainForm.InvokeRequired)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            mainForm.BeginInvoke(new Action(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));
            return tcs.Task;
        }

        private static string FlushCaptureStartupBuffer(RebexScripting scripting, CancellationToken cancellationToken)
        {
            var savedTimeout = scripting.Timeout;
            var anyDataEvent = ScriptEvent.FromRegex(@"[\s\S]");
            var startupOutput = new StringBuilder();
            try
            {
                scripting.Timeout = 150;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var chunk = scripting.ReadUntil(anyDataEvent);
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        startupOutput.Append(chunk);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown/cancel requested while draining startup output.
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                // Timeout means there is no more startup output to drain.
            }
            finally
            {
                scripting.Timeout = savedTimeout;
            }

            return startupOutput.ToString();
        }

        private static bool IsTimeoutException(Exception ex)
        {
            return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase);
        }

        private static void CloseSeparateResources(Ssh? client, VirtualTerminal? virtualTerminal, RebexScripting? scripting)
        {
            try
            {
                if (scripting is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
                // Ignore cleanup exceptions.
            }

            if (!ReferenceEquals(scripting, virtualTerminal))
            {
                try
                {
                    virtualTerminal?.Dispose();
                }
                catch
                {
                    // Ignore cleanup exceptions.
                }
            }

            try
            {
                if (client != null && client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch
            {
                // Ignore cleanup exceptions.
            }

            try
            {
                client?.Dispose();
            }
            catch
            {
                // Ignore cleanup exceptions.
            }
        }

        private static void ConnectAndLogin(Ssh client, HostConnection host, string username, string password)
        {
            client.Connect(host.IpAddress, host.Port);

            if (!string.IsNullOrEmpty(host.IdentityFile) && File.Exists(host.IdentityFile))
            {
                var passphrase = host.IdentityFilePassphrase ?? string.Empty;
                client.Login(username, new SshPrivateKey(host.IdentityFile, passphrase));
                return;
            }

            client.Login(username, password);
        }

        private static void ApplyAlgorithmSettings(Ssh client, HostConnection host)
        {
            if (host.HostKeyAlgorithms?.Length > 0)
            {
                client.Settings.SshParameters.SetHostKeyAlgorithms(host.HostKeyAlgorithms);
            }

            if (host.Ciphers?.Length > 0)
            {
                client.Settings.SshParameters.SetEncryptionAlgorithms(host.Ciphers);
            }
        }

        private static int ResolveTerminalColumns(InteractiveOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options.Columns.HasValue && options.Columns.Value > 0
                ? options.Columns.Value
                : SshTerminalOptionsFactory.DefaultColumns;
        }

        private static string ResolveInteractiveWindowTitle(
            InteractiveOptions options,
            string hostLabel,
            bool captureMode)
        {
            ArgumentNullException.ThrowIfNull(options);

            var customTitle = options.Title?.Trim();
            if (!string.IsNullOrWhiteSpace(customTitle))
            {
                return customTitle;
            }

            var safeHostLabel = string.IsNullOrWhiteSpace(hostLabel) ? "Current Host" : hostLabel;
            if (captureMode)
            {
                return $"{safeHostLabel} - Interactive Capture";
            }

            return $"{safeHostLabel} - Interactive ({options.Session.ToString().ToLowerInvariant()})";
        }

        private static int ResolveTerminalRows(InteractiveOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return options.Rows.HasValue && options.Rows.Value > 0
                ? options.Rows.Value
                : SshTerminalOptionsFactory.DefaultRows;
        }
    }
}
