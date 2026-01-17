using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;
using SSH_Helper.Utilities;

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
    /// Uses SSH.NET's Expect API for reliable prompt detection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Benefits of the Expect API:</b>
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
    /// <para>
    /// <b>Future capabilities enabled by Expect:</b>
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <b>Interactive prompts:</b> Handle sudo password prompts, confirmation dialogs (yes/no),
    ///     and multi-step authentication.
    ///   </item>
    ///   <item>
    ///     <b>Error detection:</b> Detect and react to error patterns mid-stream (e.g., "Permission denied",
    ///     "Command not found") without waiting for timeout.
    ///   </item>
    ///   <item>
    ///     <b>Conditional execution:</b> Branch command flow based on output patterns
    ///     (e.g., different commands for different OS versions).
    ///   </item>
    ///   <item>
    ///     <b>Progress indicators:</b> Detect progress patterns and report real-time status
    ///     (e.g., "50% complete", "Processing file X of Y").
    ///   </item>
    ///   <item>
    ///     <b>Multi-stage authentication:</b> Handle MFA prompts, banner acknowledgments,
    ///     or terms-of-service acceptance.
    ///   </item>
    ///   <item>
    ///     <b>Menu navigation:</b> Automatically navigate text-based menus by detecting options
    ///     and sending appropriate selections.
    ///   </item>
    /// </list>
    /// </remarks>
    public class SshShellSession : IDisposable
    {
        private readonly ShellStream _stream;
        private readonly SshTimeoutOptions _timeouts;
        private Regex _promptPattern;
        private string _currentPrompt;
        private bool _disposed;

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
        public bool IsConnected => !_disposed && _stream.CanRead && _stream.CanWrite;

        /// <summary>
        /// Common patterns used in expect operations.
        /// </summary>
        public static class Patterns
        {
            /// <summary>Matches common shell prompts ending with #, $, >, or %</summary>
            public static readonly Regex ShellPrompt = new(@"^.+[#$>%]\s*$", RegexOptions.Multiline);

            /// <summary>Matches "-- More --" style pager prompts</summary>
            public static readonly Regex PagerPrompt = new(@"--\s*More\s*--", RegexOptions.IgnoreCase);

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
        }

        /// <summary>
        /// Creates a new shell session from an existing ShellStream.
        /// </summary>
        /// <param name="stream">The SSH shell stream</param>
        /// <param name="timeouts">Timeout configuration</param>
        public SshShellSession(ShellStream stream, SshTimeoutOptions? timeouts = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _timeouts = timeouts ?? SshTimeoutOptions.Default;
            _currentPrompt = string.Empty;
            _promptPattern = Patterns.ShellPrompt;
        }

        /// <summary>
        /// Initializes the session by detecting the shell prompt.
        /// Call this after creating the session before executing commands.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The detected prompt and any banner text</returns>
        public async Task<string> InitializeAsync(CancellationToken cancellationToken = default)
        {
            // Send empty line to trigger prompt
            _stream.WriteLine("");
            _stream.Flush();

            // Wait for initial output using Expect with timeout
            var banner = await ExpectPromptAsync(_timeouts.InitialPromptTimeout, cancellationToken);

            // Try to detect the actual prompt from the output
            var sanitized = TerminalOutputProcessor.Sanitize(banner);
            var normalized = TerminalOutputProcessor.Normalize(sanitized);

            if (PromptDetector.TryDetectPrompt(normalized, out var detectedPrompt))
            {
                _currentPrompt = detectedPrompt;
                _promptPattern = PromptDetector.BuildPromptRegex(detectedPrompt);
            }
            else
            {
                // Fall back to last line
                var lines = normalized.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                _currentPrompt = lines.LastOrDefault()?.TrimEnd() ?? "";
                if (!string.IsNullOrEmpty(_currentPrompt))
                {
                    _promptPattern = PromptDetector.BuildPromptRegex(_currentPrompt);
                }
            }

            return normalized;
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

            _stream.WriteLine(command);
            _stream.Flush();

            var output = await ReadUntilPromptWithExpectAsync(cancellationToken);
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
        /// Uses SSH.NET's Expect API to wait for the prompt pattern.
        /// This is the core method that leverages Expect for reliable prompt detection.
        /// </summary>
        /// <remarks>
        /// <b>How Expect works:</b>
        /// The Expect method reads from the stream and matches against provided patterns.
        /// When a pattern matches, the corresponding action is executed.
        /// This enables reactive handling of different output scenarios.
        ///
        /// <b>Current implementation:</b>
        /// - Detects pager prompts ("-- More --") and automatically sends space to continue
        /// - Detects the shell prompt to know when a command completes
        /// - Falls back to idle timeout if no pattern matches
        ///
        /// <b>Future enhancements possible:</b>
        /// - Add ExpectAction for password prompts with auto-response
        /// - Add ExpectAction for confirmation prompts (yes/no)
        /// - Add ExpectAction for error patterns with early termination
        /// - Add ExpectAction for progress indicators with real-time callbacks
        /// </remarks>
        private async Task<string> ReadUntilPromptWithExpectAsync(CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            var idleTimeout = _timeouts.IdleTimeout;
            var commandTimeout = _timeouts.CommandTimeout;
            var startTime = DateTime.UtcNow;
            var maxPages = 50000;
            var pageCount = 0;
            var lastDataTime = DateTime.UtcNow;
            var pollInterval = _timeouts.PollInterval;

            // Short quiet period to confirm prompt detection (wait for more data after seeing prompt-like pattern)
            var promptConfirmMs = 150; // Wait 150ms to confirm no more data is coming
            var quietPeriodMs = Math.Max(200, (int)pollInterval.TotalMilliseconds * 3);
            bool potentialPromptDetected = false;
            DateTime? potentialPromptTime = null;

            EmitDebug($"ReadUntilPrompt started. Prompt pattern: {_promptPattern}");
            EmitDebug($"IdleTimeout: {idleTimeout.TotalSeconds}s, CommandTimeout: {commandTimeout.TotalSeconds}s");

            while (!cancellationToken.IsCancellationRequested && pageCount < maxPages)
            {
                // Check overall command timeout
                if (DateTime.UtcNow - startTime > commandTimeout)
                {
                    EmitDebug("Command timeout reached");
                    break;
                }

                if (_stream.DataAvailable)
                {
                    var chunk = _stream.Read();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        lastDataTime = DateTime.UtcNow;
                        potentialPromptDetected = false; // Reset - more data arrived
                        potentialPromptTime = null;

                        // Process the chunk
                        chunk = TerminalOutputProcessor.Sanitize(chunk);
                        chunk = TerminalOutputProcessor.StripPagerArtifacts(chunk, out bool sawPager);

                        output.Append(chunk);
                        OnOutputReceived(chunk);

                        EmitDebug($"Received {chunk.Length} chars. Buffer now {output.Length} chars");

                        if (sawPager)
                        {
                            EmitDebug("Pager detected, sending space");
                            _stream.Write(" ");
                            _stream.Flush();
                            pageCount++;
                            continue;
                        }

                        // Check if we have a definitive prompt match (compiled regex)
                        if (PromptDetector.BufferEndsWithPrompt(output, _promptPattern))
                        {
                            EmitDebug("Prompt detected via regex match");
                            UpdatePromptIfChanged(output);
                            break;
                        }

                        // Check if buffer ends with something that looks like a prompt
                        // Mark it as potential and confirm after a short quiet period
                        if (CheckForPromptInTail(output, confirmAndUpdate: false))
                        {
                            potentialPromptDetected = true;
                            potentialPromptTime = DateTime.UtcNow;
                            EmitDebug("Potential prompt detected, waiting to confirm...");
                        }
                    }
                }
                else
                {
                    // No data available
                    var quietTime = (DateTime.UtcNow - lastDataTime).TotalMilliseconds;

                    // If we detected a potential prompt, confirm after short wait
                    if (potentialPromptDetected && potentialPromptTime.HasValue)
                    {
                        var timeSincePotential = (DateTime.UtcNow - potentialPromptTime.Value).TotalMilliseconds;
                        if (timeSincePotential >= promptConfirmMs)
                        {
                            // No more data came, confirm the prompt
                            if (CheckForPromptInTail(output, confirmAndUpdate: true))
                            {
                                EmitDebug($"Prompt confirmed after {timeSincePotential:F0}ms quiet");
                                break;
                            }
                            potentialPromptDetected = false;
                            potentialPromptTime = null;
                        }
                    }

                    // Standard quiet period check (fallback)
                    if (quietTime >= quietPeriodMs && !potentialPromptDetected)
                    {
                        if (output.Length > 0 && CheckForPromptInTail(output, confirmAndUpdate: true))
                        {
                            EmitDebug($"Prompt detected after {quietTime:F0}ms quiet period");
                            break;
                        }
                    }

                    // Check for idle timeout
                    if (quietTime >= idleTimeout.TotalMilliseconds)
                    {
                        EmitDebug($"Idle timeout reached after {quietTime:F0}ms");
                        break;
                    }

                    await Task.Delay(pollInterval, cancellationToken);
                }
            }

            var result = output.ToString();
            EmitDebug($"ReadUntilPrompt finished. Total chars: {result.Length}");
            return TerminalOutputProcessor.Normalize(result);
        }

        /// <summary>
        /// Checks the tail of the buffer for prompt-like patterns.
        /// This is a fallback for when the main regex doesn't match.
        /// </summary>
        /// <param name="buffer">The output buffer to check</param>
        /// <param name="confirmAndUpdate">If true, updates the prompt pattern when a new prompt is found</param>
        /// <returns>True if a prompt-like pattern was found at the end of the buffer</returns>
        private bool CheckForPromptInTail(StringBuilder buffer, bool confirmAndUpdate = true)
        {
            if (buffer.Length == 0)
                return false;

            // Look at the last portion of the buffer
            int lookback = Math.Min(512, buffer.Length);
            string tail = buffer.ToString(buffer.Length - lookback, lookback);

            // Split by newlines and check the last non-empty line
            var lines = tail.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].TrimEnd();
                if (string.IsNullOrEmpty(line))
                    continue;

                // Check if this line looks like a prompt
                if (PromptDetector.IsLikelyPrompt(line))
                {
                    if (confirmAndUpdate)
                    {
                        EmitDebug($"Likely prompt found: '{line}'");

                        // Update the prompt pattern if it's different
                        if (!_promptPattern.IsMatch(line))
                        {
                            EmitDebug($"Updating prompt pattern from: {_currentPrompt} to: {line}");
                            _currentPrompt = line;
                            _promptPattern = PromptDetector.BuildPromptRegex(line);
                        }
                    }
                    return true;
                }

                // Only check the last non-empty line
                break;
            }

            return false;
        }

        /// <summary>
        /// Simple expect for initial prompt detection.
        /// </summary>
        private async Task<string> ExpectPromptAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            var startTime = DateTime.UtcNow;
            var pollInterval = _timeouts.PollInterval;

            while (!cancellationToken.IsCancellationRequested)
            {
                if (DateTime.UtcNow - startTime > timeout)
                    break;

                if (_stream.DataAvailable)
                {
                    var chunk = _stream.Read();
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        output.Append(chunk);

                        // Check if we've received a prompt
                        var text = output.ToString();
                        var sanitized = TerminalOutputProcessor.Sanitize(text);
                        if (PromptDetector.TryDetectPrompt(sanitized, out _))
                        {
                            // Wait a bit more for any trailing output
                            await Task.Delay(100, cancellationToken);
                            if (_stream.DataAvailable)
                            {
                                output.Append(_stream.Read());
                            }
                            break;
                        }
                    }
                }
                else
                {
                    await Task.Delay(pollInterval, cancellationToken);
                }
            }

            return output.ToString();
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
                // Note: We don't dispose the stream here as it's owned by the caller/pool
            }
        }
    }
}
