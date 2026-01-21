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
            /// <summary>Matches common shell prompts ending with #, $, >, or %</summary>
            public static readonly Regex ShellPrompt = new(@"^.+[#$>%]\s*$", RegexOptions.Multiline);

            /// <summary>Shell prompt pattern string for Rebex ScriptEvent</summary>
            public const string ShellPromptPattern = @"(?:^|[\r\n])[\w][\w.-]*(?:\s*\([^)]+\))?\s*[#$>%]\s*$";

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
            _shellPromptEvent = ScriptEvent.FromRegex(Patterns.ShellPromptPattern);
            _pagerEvent = ScriptEvent.FromRegex(string.Join("|", Patterns.PagerPatterns));
            _promptOrPagerEvent = ScriptEvent.FromRegex(
                $@"(?:{string.Join("|", Patterns.PagerPatterns)}|{Patterns.ShellPromptPattern})");
            _bannerEvent = ScriptEvent.FromRegex(Patterns.BannerAcceptPattern);
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
            var attemptCount = 0;

            // Check connection validity before starting
            if (!IsConnected)
            {
                throw new ObjectDisposedException(nameof(SshShellSession), "SSH connection is not valid. The connection may have failed or been closed by the server.");
            }

            // Set initial timeout for prompt detection
            _scripting.Timeout = (int)_timeouts.InitialPromptTimeout.TotalMilliseconds;

            // Send empty line to trigger prompt
            try
            {
                _scripting.Send("\r");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("SSH shell was closed unexpectedly while initializing. The connection may have been terminated by the server.", ex);
            }

            while (attemptCount < maxBannerAttempts)
            {
                attemptCount++;
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Wait for initial output using ReadUntil with banner and prompt events
                    EmitDebug($"Waiting for banner or prompt (attempt {attemptCount}/{maxBannerAttempts})");
                    var output = _scripting.ReadUntil(_bannerEvent!, _shellPromptEvent!);
                    allOutput.Append(output);

                    // Try to detect the actual prompt from the output
                    var sanitized = TerminalOutputProcessor.Sanitize(output ?? "");
                    var normalized = TerminalOutputProcessor.Normalize(sanitized);

                    // Check if we got a real shell prompt
                    if (PromptDetector.TryDetectPrompt(normalized, out var detectedPrompt))
                    {
                        _currentPrompt = detectedPrompt;
                        _promptPattern = PromptDetector.BuildPromptRegex(detectedPrompt);
                        EmitDebug($"Shell prompt detected: {detectedPrompt}");
                        break;
                    }

                    // Check for banner acceptance prompt (e.g., "Press 'a' to accept")
                    var bannerMatch = Patterns.BannerAcceptPrompt.Match(normalized);
                    if (bannerMatch.Success)
                    {
                        EmitDebug($"[InitializeAsync] Banner prompt matched: '{bannerMatch.Value}' at position {bannerMatch.Index}");

                        // Send acceptance key IMMEDIATELY - FortiGate has very short timeout
                        var keyToPress = GetBannerAcceptKey(bannerMatch);
                        EmitDebug($"[InitializeAsync] Sending banner acceptance key: '{keyToPress}'");

                        try
                        {
                            _scripting.Send(keyToPress);
                            EmitDebug("[InitializeAsync] Banner acceptance sent successfully");
                        }
                        catch (Exception ex)
                        {
                            EmitDebug($"[InitializeAsync] Exception during banner acceptance: {ex.GetType().Name}: {ex.Message}");
                            throw;
                        }

                        // Small delay to let the server process the keypress
                        await Task.Delay(100, cancellationToken);
                        continue; // Loop to wait for the real prompt
                    }

                    // No shell prompt and no banner prompt - check if last line could be a prompt
                    var lines = normalized.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                    var lastLine = lines.LastOrDefault()?.TrimEnd() ?? "";

                    if (!string.IsNullOrEmpty(lastLine) && PromptDetector.IsLikelyPrompt(lastLine))
                    {
                        _currentPrompt = lastLine;
                        _promptPattern = PromptDetector.BuildPromptRegex(lastLine);
                        EmitDebug($"Fallback prompt detected: {lastLine}");
                        break;
                    }

                    // If we reach here, we didn't find a valid prompt - try sending another newline
                    if (attemptCount < maxBannerAttempts)
                    {
                        EmitDebug($"No valid prompt found, attempt {attemptCount}/{maxBannerAttempts}, sending newline");
                        _scripting.Send("\r");
                    }
                }
                catch (SshException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase))
                {
                    EmitDebug($"Timeout waiting for prompt on attempt {attemptCount}");
                    if (attemptCount >= maxBannerAttempts)
                        break;
                }
            }

            if (string.IsNullOrEmpty(_currentPrompt))
            {
                EmitDebug("Warning: Could not detect a valid shell prompt after all attempts");
            }

            return TerminalOutputProcessor.Normalize(TerminalOutputProcessor.Sanitize(allOutput.ToString()));
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
        /// </summary>
        private string ReadUntilPromptCore(CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            var maxPages = 50000;
            var pageCount = 0;

            // Set command timeout
            _scripting.Timeout = (int)_timeouts.CommandTimeout.TotalMilliseconds;

            EmitDebug($"ReadUntilPrompt started. Prompt pattern: {_promptPattern}");
            EmitDebug($"CommandTimeout: {_timeouts.CommandTimeout.TotalSeconds}s");

            while (!cancellationToken.IsCancellationRequested && pageCount < maxPages)
            {
                // Check if connection is still valid
                if (!IsConnected)
                {
                    EmitDebug("Connection is no longer valid during ReadUntilPrompt");
                    throw new InvalidOperationException("SSH connection was closed unexpectedly.");
                }

                try
                {
                    // Read until we hit either a prompt or a pager
                    var chunk = _scripting.ReadUntil(_promptOrPagerEvent!);

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        // Process the chunk
                        chunk = TerminalOutputProcessor.Sanitize(chunk);
                        chunk = TerminalOutputProcessor.StripPagerArtifacts(chunk, out bool sawPager);

                        output.Append(chunk);
                        OnOutputReceived(chunk);

                        EmitDebug($"Received {chunk.Length} chars. Buffer now {output.Length} chars");

                        // Check if we hit a pager
                        bool hitPager = false;
                        foreach (var pattern in Patterns.PagerPatterns)
                        {
                            if (Regex.IsMatch(chunk, pattern, RegexOptions.IgnoreCase))
                            {
                                hitPager = true;
                                pageCount++;
                                EmitDebug($"Pager detected ({pageCount}), sending space");
                                _scripting.Send(" ");
                                break;
                            }
                        }

                        if (!hitPager)
                        {
                            // We hit the shell prompt - done!
                            EmitDebug("Prompt detected, command complete");
                            UpdatePromptIfChanged(output);
                            break;
                        }
                    }
                }
                catch (SshException ex) when (ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase))
                {
                    EmitDebug("Command timeout reached");
                    break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    EmitDebug($"Error during ReadUntilPrompt: {ex.Message}");
                    break;
                }
            }

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
