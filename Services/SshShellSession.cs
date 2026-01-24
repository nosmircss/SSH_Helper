using System.Text;
using System.Text.RegularExpressions;
using Rebex.Net;
using Rebex.TerminalEmulation;
using SSH_Helper.Utilities;

// Alias to avoid conflict with SSH_Helper.Services.Scripting namespace
using RebexScripting = Rebex.TerminalEmulation.Scripting;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Event arguments for real-time output from the shell session.
    /// </summary>
    public class ShellOutputEventArgs : EventArgs
    {
        public string Output { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents an interactive SSH shell session that maintains state across commands.
    /// Uses Rebex Scripting API for reliable prompt detection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Benefits of the Scripting API:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Pattern-based matching:</b> Wait for specific patterns (prompts, errors, confirmations)
    ///     rather than arbitrary timeouts or manual buffer parsing.
    ///   </item>
    ///   <item>
    ///     <b>Multiple pattern support:</b> Handle different outcomes (success prompt, error message,
    ///     password prompt) in a single expect call with different actions.
    ///   </item>
    ///   <item>
    ///     <b>Automatic pager handling:</b> Respond to pagination prompts automatically while
    ///     continuing to wait for the final prompt.
    ///   </item>
    ///   <item>
    ///     <b>Cleaner code:</b> Declarative pattern matching vs. manual read loops with string parsing.
    ///   </item>
    /// </list>
    /// </remarks>
    public class SshShellSession : IDisposable
    {
        private readonly Ssh _sshClient;
        private readonly RebexScripting _scripting;
        private readonly SshTimeoutOptions _timeouts;
        private Regex _promptPattern;
        private string _currentPrompt;
        private bool _disposed;

        // Rebex ScriptEvents for pattern matching
        private ScriptEvent? _shellPromptEvent;
        private ScriptEvent? _pagerEvent;
        private ScriptEvent? _promptOrPagerEvent;
        private ScriptEvent? _bannerEvent;

        /// <summary>
        /// When enabled, emits debug timestamps and diagnostic info to help troubleshoot prompt detection.
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Fired when output is received from the shell (for real-time display).
        /// </summary>
        public event EventHandler<ShellOutputEventArgs>? OutputReceived;

        /// <summary>
        /// Fired when debug information is available (only when DebugMode is true).
        /// </summary>
        public event EventHandler<ShellOutputEventArgs>? DebugOutput;

        /// <summary>
        /// Gets the current detected prompt.
        /// </summary>
        public string CurrentPrompt => _currentPrompt;

        /// <summary>
        /// Gets whether the session is still valid and connected.
        /// </summary>
        public bool IsConnected => !_disposed && _sshClient.IsConnected;

        /// <summary>
        /// Common patterns used in expect operations.
        /// </summary>
        public static class Patterns
        {
            /// <summary>Matches common shell prompts ending with #, $, >, %, or arrow characters</summary>
            public static readonly Regex ShellPrompt = new(@"^.+[#$>%\u2192\u276F\u279C]\s*$", RegexOptions.Multiline);

            /// <summary>Shell prompt pattern string for Rebex ScriptEvent</summary>
            public const string ShellPromptPattern = @"(?:^|[\r\n]).+[#$>%\u2192\u276F\u279C]\s*$";

            /// <summary>Matches "-- More --" style pager prompts</summary>
            public static readonly Regex PagerPrompt = new(@"--\s*More\s*--", RegexOptions.IgnoreCase);

            /// <summary>Pager patterns for Rebex ScriptEvent</summary>
            public static readonly string[] PagerPatterns = new[]
            {
                @"--\s*[Mm]ore\s*--",                    // --More-- or -- More --
                @"--\s*[Pp]ress\s+",                     // --Press SPACE-- (pager-specific)
                @"\(END\)",                              // (END) from less
                @"lines\s+\d+-\d+",                      // lines 1-24
                @"Press\s+(?:[Ss][Pp][Aa][Cc][Ee]|any\s+key)",  // Press SPACE / Press any key
            };

            /// <summary>Matches yes/no confirmation prompts</summary>
            public static readonly Regex ConfirmationPrompt = new(@"\[(?:y(?:es)?/n(?:o)?|confirm)\]\s*[:?]?\s*$", RegexOptions.IgnoreCase);

            /// <summary>Matches password prompts</summary>
            public static readonly Regex PasswordPrompt = new(@"(?:password|passphrase)\s*:\s*$", RegexOptions.IgnoreCase);

            /// <summary>Matches sudo password prompts</summary>
            public static readonly Regex SudoPrompt = new(@"\[sudo\].*password.*:\s*$", RegexOptions.IgnoreCase);

            /// <summary>Matches common error indicators</summary>
            public static readonly Regex ErrorIndicator = new(
                @"(?:error|failed|denied|not found|invalid|unknown command)",
                RegexOptions.IgnoreCase);

            /// <summary>
            /// Matches pre-login banner acceptance prompts (e.g., FortiGate EULA).
            /// Returns the key to press in group 1.
            /// Examples: "(Press 'a' to accept):", "Press 'q' to quit", "[Press any key to continue]"
            /// FortiGate specific: "Press 'a' to accept:", "(a)ccept", "[A]ccept"
            /// </summary>
            public static readonly Regex BannerAcceptPrompt = new(
                @"(?:" +
                    @"press\s+['""]?([aqy])['""]?\s+to\s+(?:accept|agree|continue|quit)" + // "Press 'a' to accept"
                    @"|press\s+any\s+key" +                                                  // "Press any key"
                    @"|\(([aqy])\)\s*(?:ccept|gree|uit|ontinue)" +                          // "(a)ccept" or "(q)uit"
                    @"|\[([AaQqYy])\]\s*(?:ccept|gree|uit|ontinue)" +                       // "[A]ccept" or "[Q]uit"
                    @"|to\s+accept[,:]?\s+press\s+['""]?([aqy])['""]?" +                    // "to accept, press 'a'"
                @")",
                RegexOptions.IgnoreCase);

            /// <summary>Banner pattern string for Rebex ScriptEvent</summary>
            public const string BannerAcceptPattern =
                @"(?:" +
                    @"press\s+['""]?([aqy])['""]?\s+to\s+(?:accept|agree|continue|quit)" +
                    @"|press\s+any\s+key" +
                    @"|\(([aqy])\)\s*(?:ccept|gree|uit|ontinue)" +
                    @"|\[([AaQqYy])\]\s*(?:ccept|gree|uit|ontinue)" +
                    @"|to\s+accept[,:]?\s+press\s+['""]?([aqy])['""]?" +
                @")";
        }

        /// <summary>
        /// Creates a new shell session from a Rebex SSH client and Scripting instance.
        /// </summary>
        /// <param name="sshClient">The Rebex SSH client</param>
        /// <param name="scripting">The Rebex Scripting instance from StartScripting()</param>
        /// <param name="timeouts">Timeout configuration</param>
        public SshShellSession(Ssh sshClient, RebexScripting scripting, SshTimeoutOptions? timeouts = null)
        {
            _sshClient = sshClient ?? throw new ArgumentNullException(nameof(sshClient));
            _scripting = scripting ?? throw new ArgumentNullException(nameof(scripting));
            _timeouts = timeouts ?? SshTimeoutOptions.Default;
            _currentPrompt = string.Empty;
            _promptPattern = Patterns.ShellPrompt;

            // Initialize ScriptEvents for pattern matching
            InitializeScriptEvents();
        }

        /// <summary>
        /// Initializes the Rebex ScriptEvents for pattern matching.
        /// </summary>
        private void InitializeScriptEvents()
        {
            // Use Regex objects with proper options (especially IgnoreCase for banner detection)
            // This matches the RebexPOC approach that works correctly
            _shellPromptEvent = ScriptEvent.FromRegex(Patterns.ShellPromptPattern);

            // Create pager regex with IgnoreCase
            var pagerPattern = string.Join("|", Patterns.PagerPatterns);
            var pagerRegex = new Regex(pagerPattern, RegexOptions.IgnoreCase);
            _pagerEvent = ScriptEvent.FromRegex(pagerRegex);

            // Combined prompt-or-pager pattern with IgnoreCase
            var combinedPattern = $@"(?:{pagerPattern}|{Patterns.ShellPromptPattern})";
            var combinedRegex = new Regex(combinedPattern, RegexOptions.IgnoreCase);
            _promptOrPagerEvent = ScriptEvent.FromRegex(combinedRegex);

            // Use the existing Regex object which already has IgnoreCase
            _bannerEvent = ScriptEvent.FromRegex(Patterns.BannerAcceptPrompt);
        }

        /// <summary>
        /// Rebuilds the prompt ScriptEvent with a specific detected prompt pattern.
        /// Must be called after initialization detects the actual prompt.
        /// </summary>
        private void RebuildPromptEvent(string specificPromptPattern)
        {
            var pagerPattern = string.Join("|", Patterns.PagerPatterns);
            var combinedPattern = $@"(?:{pagerPattern}|{specificPromptPattern})";
            var combinedRegex = new Regex(combinedPattern, RegexOptions.IgnoreCase);
            _promptOrPagerEvent = ScriptEvent.FromRegex(combinedRegex);
        }

        /// <summary>
        /// Initializes the session by detecting the shell prompt.
        /// Automatically handles pre-login banners that require acceptance (e.g., FortiGate EULA).
        /// Call this after creating the session before executing commands.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The detected prompt and any banner text</returns>
        public async Task<string> InitializeAsync(CancellationToken cancellationToken = default)
        {
            var allOutput = new StringBuilder();
            var maxBannerAttempts = 3; // Prevent infinite loops if banner keeps appearing

            // Check connection validity before starting
            if (!IsConnected)
            {
                throw new ObjectDisposedException(nameof(SshShellSession), "SSH connection is not valid. The connection may have failed or been closed by the server.");
            }

            // Set initial timeout for prompt detection
            _scripting.Timeout = (int)_timeouts.InitialPromptTimeout.TotalMilliseconds;

            // NOTE: Do NOT send \r before reading initial output.
            // Devices like FortiGate send their banner/prompt automatically after shell starts.
            // Sending \r prematurely can cause the connection to be closed.

            // Always use polling-based initialization for reliable prompt/banner detection.
            // The polling approach accumulates all data before pattern matching, avoiding
            // premature prompt matches from incremental Rebex ScriptEvent processing.
            // Debug output is controlled by EmitDebug() which checks DebugMode internally.
            await InitializeWithPollingAsync(allOutput, maxBannerAttempts, cancellationToken);

            if (string.IsNullOrEmpty(_currentPrompt))
            {
                EmitDebug("WARNING: Could not detect a valid shell prompt after all attempts");
                EmitDebug($"Total raw data received across all attempts ({allOutput.Length} chars): {EscapeForDebug(allOutput.ToString(), 1000)}");
            }

            return TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(allOutput.ToString()));
        }

        /// <summary>
        /// Flushes any remaining data in the scripting buffer.
        /// Call after InitializeAsync() to ensure no residual prompt/banner
        /// data bleeds into command execution output.
        /// </summary>
        public void FlushBuffer()
        {
            var savedTimeout = _scripting.Timeout;
            _scripting.Timeout = 200;
            var anyDataEvent = ScriptEvent.FromRegex(@"[\s\S]");
            try
            {
                while (true)
                {
                    _scripting.ReadUntil(anyDataEvent);
                }
            }
            catch (Exception ex) when (
                ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase))
            {
                // Timeout means buffer is drained
            }
            finally
            {
                _scripting.Timeout = savedTimeout;
            }
        }

        /// <summary>
        /// Polling-based initialization: uses a catch-all ScriptEvent to read data as it arrives,
        /// accumulates all received data, and checks banner/prompt patterns on the full buffer.
        /// This avoids premature pattern matches from Rebex's incremental ScriptEvent processing.
        /// </summary>
        private async Task InitializeWithPollingAsync(StringBuilder allOutput, int maxBannerAttempts, CancellationToken cancellationToken)
        {
            var attemptCount = 0;
            var overallTimeout = _scripting.Timeout; // Save original (e.g., 30000ms)
            var pollInterval = DebugMode ? 3000 : 1000; // Longer polls in debug for readable output pacing
            var anyDataEvent = ScriptEvent.FromRegex(@"[\s\S]"); // Matches as soon as any data arrives
            var bannerAcceptCount = 0;
            var maxBannerAccepts = 5; // Max times we'll send the acceptance key before giving up

            EmitDebug($"Using polling initialization: {pollInterval}ms idle polls, {overallTimeout}ms overall timeout");
            EmitDebug($"ShellPromptPattern: {Patterns.ShellPromptPattern}");
            EmitDebug($"BannerAcceptPattern: {Patterns.BannerAcceptPattern}");

            while (attemptCount < maxBannerAttempts)
            {
                attemptCount++;
                cancellationToken.ThrowIfCancellationRequested();
                EmitDebug($"--- Attempt {attemptCount}/{maxBannerAttempts} ---");

                var attemptSw = System.Diagnostics.Stopwatch.StartNew();
                var dataReceived = false;

                // Poll loop: read data in chunks as it arrives
                while (attemptSw.ElapsedMilliseconds < overallTimeout)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _scripting.Timeout = pollInterval;

                    try
                    {
                        // Use catch-all event to return as soon as ANY data arrives
                        var chunk = _scripting.ReadUntil(anyDataEvent);

                        if (!string.IsNullOrEmpty(chunk))
                        {
                            dataReceived = true;
                            allOutput.Append(chunk);

                            // Check accumulated data against our patterns
                            var accumulated = allOutput.ToString();
                            var sanitized = TerminalOutputProcessor.Sanitize(accumulated);
                            var normalized = TerminalOutputProcessor.Normalize(sanitized);

                            // Check for shell prompt
                            if (PromptDetector.TryDetectPrompt(normalized, out var detectedPrompt))
                            {
                                _currentPrompt = detectedPrompt;
                                _promptPattern = PromptDetector.BuildPromptRegex(detectedPrompt);
                                RebuildPromptEvent(_promptPattern.ToString());
                                EmitDebug($"Shell prompt detected: {EscapeForDebug(detectedPrompt, 200)}");
                                EmitDebug($"Built prompt regex: {_promptPattern}");
                                _scripting.Timeout = overallTimeout;
                                return;
                            }

                            // Check for banner
                            var bannerMatch = Patterns.BannerAcceptPrompt.Match(normalized);
                            if (bannerMatch.Success)
                            {
                                if (bannerAcceptCount >= maxBannerAccepts)
                                {
                                    EmitDebug($"Banner acceptance limit reached ({maxBannerAccepts}). Trying with carriage return...");
                                    var keyToPress = GetBannerAcceptKey(bannerMatch);
                                    _scripting.Send(keyToPress + "\r");
                                    await Task.Delay(500, cancellationToken);
                                    allOutput.Clear();
                                    bannerAcceptCount++;
                                    if (bannerAcceptCount > maxBannerAccepts + 2)
                                    {
                                        EmitDebug("Banner acceptance failed after all retries. Moving to next attempt.");
                                        break;
                                    }
                                    continue;
                                }

                                bannerAcceptCount++;
                                EmitDebug($"Banner prompt matched ({bannerAcceptCount}/{maxBannerAccepts}): '{bannerMatch.Value}'");
                                var key = GetBannerAcceptKey(bannerMatch);
                                EmitDebug($"Sending banner acceptance key: '{key}'");
                                _scripting.Send(key);
                                await Task.Delay(500, cancellationToken);
                                // Clear the buffer completely so we don't re-match old banner text
                                allOutput.Clear();
                                continue;
                            }

                            // Check fallback prompt detection
                            var lines = normalized.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                            var lastLine = lines.LastOrDefault()?.TrimEnd() ?? "";
                            if (!string.IsNullOrEmpty(lastLine) && PromptDetector.IsLikelyPrompt(lastLine))
                            {
                                _currentPrompt = lastLine;
                                _promptPattern = PromptDetector.BuildPromptRegex(lastLine);
                                RebuildPromptEvent(_promptPattern.ToString());
                                EmitDebug($"Fallback prompt detected: {EscapeForDebug(lastLine, 200)}");
                                EmitDebug($"Built fallback prompt regex: {_promptPattern}");
                                _scripting.Timeout = overallTimeout;
                                return;
                            }

                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException &&
                        (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Short poll timed out - no new data in this interval
                        EmitDebug($"[+{attemptSw.ElapsedMilliseconds}ms] Poll timeout (no new data in last {pollInterval}ms). Total buffered: {allOutput.Length} chars");

                        if (attemptSw.ElapsedMilliseconds >= overallTimeout)
                        {
                            EmitDebug($"Overall timeout reached ({overallTimeout}ms)");
                            break;
                        }
                    }
                }

                // Attempt finished (timed out) - log what we have
                if (allOutput.Length > 0 && !dataReceived)
                {
                    EmitDebug($"Attempt {attemptCount} finished. No new data received.");
                }
                else if (allOutput.Length > 0)
                {
                    // We have data but no pattern matched - analyze why
                    var normalized = TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(allOutput.ToString()));
                    var debugLines = normalized.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    EmitDebug($"Pattern matching failed. Analyzing {debugLines.Length} lines:");
                    for (int i = 0; i < debugLines.Length; i++)
                    {
                        var candidate = debugLines[i].TrimEnd();
                        var isLikely = PromptDetector.IsLikelyPrompt(candidate);
                        var lastChar = candidate.Length > 0 ? candidate[^1] : '\0';
                        var charInfo = lastChar != '\0' ? $"LastChar: '{lastChar}' (U+{(int)lastChar:X4})" : "LastChar: <empty>";
                        EmitDebug($"  Line[{i}]: \"{EscapeForDebug(candidate, 200)}\" | {charInfo} | IsLikelyPrompt: {isLikely}");
                    }
                }
                else
                {
                    EmitDebug($"Attempt {attemptCount} finished. No data received from server at all.");
                }

                // Try sending newline for next attempt
                if (attemptCount < maxBannerAttempts)
                {
                    EmitDebug($"Sending newline to retry...");
                    _scripting.Send("\r");
                    await Task.Delay(200, cancellationToken);
                }
            }

            _scripting.Timeout = overallTimeout; // Restore original timeout
        }

        /// <summary>
        /// Executes a single command and waits for the prompt to return.
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The command output (including the command echo and final prompt)</returns>
        public async Task<string> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            EmitDebug($">>> Sending command: \"{EscapeForDebug(command, 200)}\"");
            EmitDebug($">>> Prompt regex in use: {_promptPattern}");

            _scripting.Send(command + "\r");

            var output = await ReadUntilPromptAsync(cancellationToken);
            return output;
        }

        /// <summary>
        /// Executes multiple commands in sequence, maintaining session state.
        /// </summary>
        /// <param name="commands">Commands to execute</param>
        /// <param name="variables">Variables for substitution (${name} syntax)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Combined output from all commands</returns>
        public async Task<string> ExecuteBatchAsync(
            IEnumerable<string> commands,
            Dictionary<string, string>? variables = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            var output = new StringBuilder();

            foreach (var commandTemplate in commands)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(commandTemplate) || commandTemplate.StartsWith("#"))
                    continue;

                // Substitute variables
                var command = SubstituteVariables(commandTemplate, variables);
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                var result = await ExecuteAsync(command, cancellationToken);
                output.Append(result);
            }

            return output.ToString();
        }

        /// <summary>
        /// Uses Rebex Scripting API to wait for the prompt pattern.
        /// Handles pager prompts automatically.
        /// </summary>
        private Task<string> ReadUntilPromptAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() => ReadUntilPromptCore(cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Core implementation of ReadUntilPrompt (runs on background thread).
        /// Uses polling with data accumulation to reliably capture all output.
        /// </summary>
        private string ReadUntilPromptCore(CancellationToken cancellationToken)
        {
            return ReadUntilPromptWithPolling(cancellationToken);
        }

        /// <summary>
        /// ReadUntilPrompt implementation: polls for data in short intervals, accumulates
        /// all received data, and checks for prompt/pager patterns in the accumulated buffer.
        /// This avoids premature regex matches on the command-echo prompt that occur with
        /// blocking ReadUntil(patternEvent) calls.
        /// </summary>
        private string ReadUntilPromptWithPolling(CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            var rawBuffer = new StringBuilder();
            var maxPages = 50000;
            var pageCount = 0;

            var overallTimeout = (int)_timeouts.CommandTimeout.TotalMilliseconds;
            var batchTimeout = 50; // Short timeout to accumulate chars into batches
            var idleTimeout = 2000; // How long to wait with no data before trying again
            var anyDataEvent = ScriptEvent.FromRegex(@"[\s\S]");

            // Guard against matching the command-echo prompt. When a command is sent,
            // the device echoes "prompt# command-text\r\n" before the output. Without
            // this guard, the prompt regex can match the echo before output arrives.
            bool seenNewlineAfterEcho = false;
            var minTimeBeforePromptMatch = 500; // ms - safety for commands with no output

            EmitDebug($"ReadUntilPrompt started.");
            EmitDebug($"  Prompt pattern: {_promptPattern}");
            EmitDebug($"  CommandTimeout: {_timeouts.CommandTimeout.TotalSeconds}s");

            var overallSw = System.Diagnostics.Stopwatch.StartNew();
            bool promptMatched = false;

            while (!cancellationToken.IsCancellationRequested && pageCount < maxPages)
            {
                if (!IsConnected)
                {
                    EmitDebug("Connection lost during ReadUntilPrompt");
                    throw new InvalidOperationException("SSH connection was closed unexpectedly.");
                }

                if (overallSw.ElapsedMilliseconds >= overallTimeout)
                {
                    EmitDebug($"Overall command timeout reached ({overallTimeout}ms)");
                    break;
                }

                // Inner loop: accumulate characters into a batch using short timeout
                var batch = new StringBuilder();
                _scripting.Timeout = batchTimeout;
                bool gotData = false;

                while (true)
                {
                    try
                    {
                        var ch = _scripting.ReadUntil(anyDataEvent);
                        if (!string.IsNullOrEmpty(ch))
                        {
                            batch.Append(ch);
                            gotData = true;

                            // Track newlines inside the loop for early prompt detection
                            if (!seenNewlineAfterEcho && ch.Contains('\n'))
                            {
                                seenNewlineAfterEcho = true;
                            }

                            // Early-break: if echo guard is satisfied and batch contains a
                            // prompt terminator, exit inner loop immediately to let the outer
                            // loop run the full prompt regex check without waiting for timeout
                            if (seenNewlineAfterEcho && ContainsPromptTerminator(batch))
                            {
                                break;
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException &&
                        (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase)))
                    {
                        // No more data available within batchTimeout — batch is complete
                        break;
                    }

                    // Safety: don't accumulate forever in the inner loop
                    if (batch.Length > 4096 || overallSw.ElapsedMilliseconds >= overallTimeout)
                        break;
                }

                if (gotData)
                {
                    var chunk = batch.ToString();
                    rawBuffer.Append(chunk);
                    EmitDebug($"[+{overallSw.ElapsedMilliseconds}ms] Received {chunk.Length} chars. RAW: {EscapeForDebug(chunk, 300)}");

                    // Check for pager BEFORE stripping
                    bool hitPager = false;
                    foreach (var pattern in Patterns.PagerPatterns)
                    {
                        if (Regex.IsMatch(chunk, pattern, RegexOptions.IgnoreCase))
                        {
                            hitPager = true;
                            break;
                        }
                    }

                    // Process chunk for clean output
                    var processed = TerminalOutputProcessor.Sanitize(chunk);
                    processed = TerminalOutputProcessor.StripPagerArtifacts(processed, out _);
                    processed = TerminalOutputProcessor.StripPagerDismissalArtifacts(processed);

                    output.Append(processed);
                    OnOutputReceived(processed);

                    if (hitPager)
                    {
                        pageCount++;
                        EmitDebug($"Pager detected ({pageCount}), sending space");
                        _scripting.Send(" ");
                        continue;
                    }

                    // Only check for prompt after the command echo line has completed
                    // (contains a newline), or after enough time for no-output commands.
                    bool canCheckPrompt = seenNewlineAfterEcho ||
                                          overallSw.ElapsedMilliseconds >= minTimeBeforePromptMatch;

                    if (canCheckPrompt)
                    {
                        // Check if accumulated normalized output matches prompt
                        var accumulated = TerminalOutputProcessor.Sanitize(rawBuffer.ToString());
                        var normalized = TerminalOutputProcessor.Normalize(accumulated);

                        if (_promptPattern != null && _promptPattern.IsMatch(normalized))
                        {
                            EmitDebug($"Prompt regex matched! Command complete.");
                            promptMatched = true;
                            UpdatePromptIfChanged(output);
                            break;
                        }
                    }
                }
                else
                {
                    // No data received — wait for idleTimeout before trying again
                    _scripting.Timeout = idleTimeout;
                    try
                    {
                        var ch = _scripting.ReadUntil(anyDataEvent);
                        if (!string.IsNullOrEmpty(ch))
                        {
                            // Got data after idle wait — put it back in the buffer and continue
                            rawBuffer.Append(ch);
                            // Feed back into the next iteration's batch start
                            continue;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException &&
                        (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                         ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase)))
                    {
                        EmitDebug($"[+{overallSw.ElapsedMilliseconds}ms] No data for {idleTimeout}ms. Buffer: {rawBuffer.Length} chars");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        EmitDebug($"Error during polling read: {ex.Message}");
                        break;
                    }
                }
            }

            // Diagnostic dump when prompt wasn't matched (only in debug mode to avoid computation)
            if (DebugMode && !promptMatched && rawBuffer.Length > 0)
            {
                EmitDebug("=== TIMEOUT: Prompt regex did NOT match. Buffer analysis: ===");
                var finalNormalized = TerminalOutputProcessor.Normalize(
                    TerminalOutputProcessor.Sanitize(rawBuffer.ToString()));
                var lines = finalNormalized.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                EmitDebug($"  Total lines: {lines.Length}, Total raw chars: {rawBuffer.Length}");
                EmitDebug($"  Prompt regex: {_promptPattern}");

                var startLine = Math.Max(0, lines.Length - 10);
                for (int i = startLine; i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd();
                    var isMatch = _promptPattern != null && _promptPattern.IsMatch(line);
                    var isLikely = PromptDetector.IsLikelyPrompt(line);
                    var lastChar = line.Length > 0 ? line[^1] : '\0';
                    var charInfo = lastChar != '\0' ? $"U+{(int)lastChar:X4} '{lastChar}'" : "<empty>";
                    EmitDebug($"  Line[{i}]: \"{EscapeForDebug(line, 120)}\" | End: {charInfo} | RegexMatch: {isMatch} | IsLikelyPrompt: {isLikely}");
                }

                var rawTail = rawBuffer.Length > 300
                    ? rawBuffer.ToString(rawBuffer.Length - 300, 300)
                    : rawBuffer.ToString();
                EmitDebug($"  Raw buffer tail: {EscapeForDebug(rawTail, 300)}");
            }
            else if (DebugMode && !promptMatched && rawBuffer.Length == 0)
            {
                EmitDebug("=== TIMEOUT: No data received from server at all ===");
            }

            // Restore original timeout
            _scripting.Timeout = overallTimeout;

            var result = output.ToString();
            EmitDebug($"ReadUntilPrompt finished. Total chars: {result.Length}");
            return TerminalOutputProcessor.Normalize(result);
        }

        /// <summary>
        /// Extracts the key to press from a banner acceptance prompt match.
        /// Checks all capture groups since the regex has multiple alternatives.
        /// </summary>
        private static string GetBannerAcceptKey(Match bannerMatch)
        {
            // Check each capture group (groups 1-4 contain the key in different patterns)
            for (int i = 1; i <= 4; i++)
            {
                if (bannerMatch.Groups[i].Success && !string.IsNullOrEmpty(bannerMatch.Groups[i].Value))
                {
                    return bannerMatch.Groups[i].Value.ToLowerInvariant();
                }
            }
            // Default to 'a' for patterns like "press any key"
            return "a";
        }

        /// <summary>
        /// Checks if the prompt has changed (e.g., entered config mode) and updates tracking.
        /// </summary>
        private void UpdatePromptIfChanged(StringBuilder buffer)
        {
            if (PromptDetector.TryDetectDifferentPrompt(buffer, _promptPattern, out var newPrompt))
            {
                _currentPrompt = newPrompt;
                _promptPattern = PromptDetector.BuildPromptRegex(newPrompt);
                RebuildPromptEvent(_promptPattern.ToString());
            }
        }

        /// <summary>
        /// Substitutes ${variable} placeholders in the command string.
        /// </summary>
        private static string SubstituteVariables(string command, Dictionary<string, string>? variables)
        {
            if (variables == null || variables.Count == 0)
                return command;

            var result = command;
            foreach (System.Text.RegularExpressions.Match match in Regex.Matches(command, @"\$\{([^}]+)\}"))
            {
                var variableName = match.Groups[1].Value;
                var value = variables.TryGetValue(variableName, out var v) ? v : "";
                result = result.Replace($"${{{variableName}}}", value);
            }

            return result;
        }

        protected virtual void OnOutputReceived(string output)
        {
            OutputReceived?.Invoke(this, new ShellOutputEventArgs { Output = output });
        }

        private void EmitDebug(string message)
        {
            if (DebugMode)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var debugLine = $"[DEBUG {timestamp}] {message}\r\n";
                DebugOutput?.Invoke(this, new ShellOutputEventArgs { Output = debugLine });
            }
        }

        /// <summary>
        /// Escapes control characters for debug display.
        /// </summary>
        private static string EscapeForDebug(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input))
                return "<empty>";

            var sb = new StringBuilder();
            var len = Math.Min(input.Length, maxLength);

            for (int i = 0; i < len; i++)
            {
                char c = input[i];
                switch (c)
                {
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\x1b': sb.Append("\\e"); break;  // ESC
                    case '\b': sb.Append("\\b"); break;    // Backspace
                    case '\0': sb.Append("\\0"); break;
                    default:
                        if (c < 32)
                            sb.Append($"\\x{(int)c:X2}");
                        else
                            sb.Append(c);
                        break;
                }
            }

            if (input.Length > maxLength)
                sb.Append("...");

            return sb.ToString();
        }

        /// <summary>
        /// Fast check for whether a StringBuilder contains any prompt terminator character.
        /// Used as a guard to break the inner batch loop early for prompt detection.
        /// </summary>
        private static bool ContainsPromptTerminator(StringBuilder sb)
        {
            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if (c == '#' || c == '>' || c == '$' || c == '%' ||
                    c == '\u2192' || c == '\u276F' || c == '\u279C')
                    return true;
            }
            return false;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SshShellSession));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // Note: We don't dispose the Ssh client or Scripting here as they're owned by the caller/pool
            }
        }
    }
}
