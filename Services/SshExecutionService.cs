using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Renci.SshNet;
using SSH_Helper.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Event arguments for SSH execution progress updates.
    /// </summary>
    public class SshProgressEventArgs : EventArgs
    {
        public HostConnection Host { get; set; } = new();
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// Event arguments for SSH output received.
    /// </summary>
    public class SshOutputEventArgs : EventArgs
    {
        public HostConnection Host { get; set; } = new();
        public string Output { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles SSH command execution against remote hosts.
    /// </summary>
    public class SshExecutionService
    {
        private const int DefaultPollIntervalMs = 60;
        private const int MaxPages = 50000;

        public event EventHandler<SshProgressEventArgs>? ProgressChanged;
        public event EventHandler<SshOutputEventArgs>? OutputReceived;

        private volatile bool _isRunning;
        private CancellationTokenSource? _cts;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Executes commands on multiple hosts.
        /// </summary>
        /// <param name="hosts">Collection of host connections</param>
        /// <param name="commands">Commands to execute (one per line)</param>
        /// <param name="defaultUsername">Default username if not specified per-host</param>
        /// <param name="defaultPassword">Default password if not specified per-host</param>
        /// <param name="timeoutSeconds">Connection timeout in seconds</param>
        /// <returns>Results for each host</returns>
        public async Task<List<ExecutionResult>> ExecuteAsync(
            IEnumerable<HostConnection> hosts,
            string[] commands,
            string defaultUsername,
            string defaultPassword,
            int timeoutSeconds)
        {
            var results = new List<ExecutionResult>();
            _cts = new CancellationTokenSource();
            _isRunning = true;

            try
            {
                foreach (var host in hosts)
                {
                    if (_cts.Token.IsCancellationRequested)
                        break;

                    if (!host.IsValid())
                        continue;

                    var result = await Task.Run(() =>
                        ExecuteSingleHost(host, commands, defaultUsername, defaultPassword, timeoutSeconds, _cts.Token));

                    results.Add(result);
                }
            }
            finally
            {
                _isRunning = false;
            }

            return results;
        }

        /// <summary>
        /// Stops the current execution.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _cts?.Cancel();
        }

        private ExecutionResult ExecuteSingleHost(
            HostConnection host,
            string[] commands,
            string defaultUsername,
            string defaultPassword,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            var result = new ExecutionResult
            {
                Host = host,
                Timestamp = DateTime.Now
            };

            var outputBuilder = new StringBuilder();
            string username = !string.IsNullOrWhiteSpace(host.Username) ? host.Username : defaultUsername;
            string password = !string.IsNullOrWhiteSpace(host.Password) ? host.Password : defaultPassword;

            try
            {
                using var client = new SshClient(host.IpAddress, host.Port, username, password);
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                client.ErrorOccurred += (s, e) =>
                {
                    if (e.Exception != null && _isRunning)
                    {
                        OnProgressChanged(host, $"SSH Error: {e.Exception.Message}", true, true);
                    }
                };

                client.Connect();

                OnProgressChanged(host, $"Connected to {host}", false, true);

                using var shellStream = client.CreateShellStream("xterm", 200, 48, 1200, 800, 16384);
                var commandOutput = ExecuteCommandsOnStream(shellStream, commands, host, timeoutSeconds, cancellationToken);
                outputBuilder.Append(commandOutput);

                client.Disconnect();
                result.Success = true;
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Authentication failed";
                result.Exception = ex;
                outputBuilder.AppendLine(FormatError("AUTHENTICATION ERROR", host, ex));
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Connection failed";
                result.Exception = ex;
                outputBuilder.AppendLine(FormatError("CONNECTION ERROR", host, ex));
            }
            catch (Renci.SshNet.Common.SshOperationTimeoutException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Operation timed out";
                result.Exception = ex;
                outputBuilder.AppendLine(FormatError("TIMEOUT ERROR", host, ex));
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Network error";
                result.Exception = ex;
                outputBuilder.AppendLine(FormatError("NETWORK ERROR", host, ex));
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                outputBuilder.AppendLine(FormatError("ERROR", host, ex));
            }

            result.Output = outputBuilder.ToString();
            return result;
        }

        private string ExecuteCommandsOnStream(
            ShellStream shellStream,
            string[] commands,
            HostConnection host,
            int timeoutSeconds,
            CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            int idleTimeoutMs = Math.Max(1000, timeoutSeconds * 1000);

            // Trigger prompt and read initial banner/prompt
            shellStream.WriteLine("");
            shellStream.Flush();

            string banner = ReadAvailable(shellStream, DefaultPollIntervalMs, 800, 1500, true, cancellationToken);
            banner = TerminalOutputProcessor.Sanitize(banner);
            banner = TerminalOutputProcessor.Normalize(banner);

            if (!PromptDetector.TryDetectPrompt(banner, out var promptText))
            {
                var lines = banner.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                promptText = lines.LastOrDefault()?.TrimEnd() ?? "";
            }

            var promptRegex = PromptDetector.BuildPromptRegex(promptText);

            // Build header
            string header = $"{new string('#', 20)} CONNECTED TO {host} {promptText} {new string('#', 20)}";
            string separator = new string('#', header.Length);

            output.AppendLine(separator);
            output.AppendLine(header);
            output.AppendLine(separator);
            output.Append(promptText);

            OnOutputReceived(host, output.ToString());

            foreach (string commandTemplate in commands)
            {
                if (cancellationToken.IsCancellationRequested || !_isRunning)
                    break;

                if (string.IsNullOrWhiteSpace(commandTemplate) || commandTemplate.StartsWith("#"))
                    continue;

                string commandToExecute = SubstituteVariables(commandTemplate, host.Variables);

                if (string.IsNullOrWhiteSpace(commandToExecute))
                    continue;

                shellStream.WriteLine(commandToExecute);
                shellStream.Flush();

                var commandOutput = ReadUntilPromptWithPager(
                    shellStream, promptRegex, DefaultPollIntervalMs, idleTimeoutMs,
                    out bool matchedPrompt, out string? updatedPromptLiteral, cancellationToken);

                if (!string.IsNullOrEmpty(updatedPromptLiteral) &&
                    !string.Equals(updatedPromptLiteral, promptText, StringComparison.Ordinal))
                {
                    promptText = updatedPromptLiteral;
                    promptRegex = PromptDetector.BuildPromptRegex(promptText);
                }

                // Clean up double carriage returns
                if (commandOutput.StartsWith(commandToExecute + "\r\r\n", StringComparison.Ordinal))
                {
                    commandOutput = Regex.Replace(commandOutput,
                        Regex.Escape(commandToExecute) + "\r\r\n",
                        commandToExecute + "\r\n");
                }

                output.Append(commandOutput);
                OnOutputReceived(host, commandOutput);
            }

            return output.ToString();
        }

        private string SubstituteVariables(string commandTemplate, Dictionary<string, string> variables)
        {
            string result = commandTemplate;

            foreach (Match match in Regex.Matches(commandTemplate, @"\$\{([^}]+)\}"))
            {
                string variableName = match.Groups[1].Value;
                string value = variables.TryGetValue(variableName, out var v) ? v : "";
                result = result.Replace($"${{{variableName}}}", value);
            }

            return result;
        }

        private string ReadAvailable(
            ShellStream shellStream,
            int pollIntervalMs,
            int maxInactivityMs,
            int maxOverallMs,
            bool stopOnLikelyPrompt,
            CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            var sw = Stopwatch.StartNew();
            long lastDataMs = sw.ElapsedMilliseconds;
            int idleQuietMs = Math.Clamp(pollIntervalMs * 3, 100, 400);

            while (_isRunning && !cancellationToken.IsCancellationRequested)
            {
                if (sw.ElapsedMilliseconds >= maxOverallMs)
                    break;
                if (sw.ElapsedMilliseconds - lastDataMs >= Math.Min(maxInactivityMs, maxOverallMs))
                    break;

                if (shellStream.DataAvailable)
                {
                    string chunk = shellStream.Read();

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        lastDataMs = sw.ElapsedMilliseconds;
                        chunk = TerminalOutputProcessor.Sanitize(chunk);
                        chunk = TerminalOutputProcessor.StripPagerArtifacts(chunk, out bool sawPager);
                        sb.Append(chunk);

                        if (sawPager)
                        {
                            shellStream.Write(" ");
                            shellStream.Flush();
                        }

                        if (stopOnLikelyPrompt &&
                            PromptDetector.TryDetectPromptFromTail(sb.ToString(), out _) &&
                            sw.ElapsedMilliseconds - lastDataMs >= idleQuietMs)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    Thread.Sleep(Math.Min(50, Math.Max(10, pollIntervalMs / 2)));
                }
            }

            return sb.ToString();
        }

        private string ReadUntilPromptWithPager(
            ShellStream shellStream,
            Regex promptRegex,
            int pollIntervalMs,
            int maxInactivityMs,
            out bool matchedPrompt,
            out string? updatedPromptLiteral,
            CancellationToken cancellationToken)
        {
            matchedPrompt = false;
            updatedPromptLiteral = null;

            var sb = new StringBuilder();
            var sw = Stopwatch.StartNew();
            long lastActivityMs = 0;
            int pageCount = 0;

            while (_isRunning && !cancellationToken.IsCancellationRequested && pageCount < MaxPages)
            {
                if (sw.ElapsedMilliseconds - lastActivityMs > maxInactivityMs)
                    break;

                if (shellStream.DataAvailable)
                {
                    string chunk = shellStream.Read();

                    if (!string.IsNullOrEmpty(chunk))
                    {
                        lastActivityMs = sw.ElapsedMilliseconds;
                        chunk = TerminalOutputProcessor.Sanitize(chunk);
                        chunk = TerminalOutputProcessor.StripPagerArtifacts(chunk, out bool sawPager);
                        sb.Append(chunk);

                        if (sawPager)
                        {
                            lastActivityMs = sw.ElapsedMilliseconds;
                            shellStream.Write(" ");
                            shellStream.Flush();
                            pageCount++;
                            continue;
                        }

                        if (PromptDetector.BufferEndsWithPrompt(sb, promptRegex))
                        {
                            matchedPrompt = true;
                            break;
                        }

                        if (!matchedPrompt &&
                            PromptDetector.TryDetectDifferentPrompt(sb, promptRegex, out var differentPrompt))
                        {
                            matchedPrompt = true;
                            updatedPromptLiteral = differentPrompt;
                            break;
                        }
                    }
                }
                else
                {
                    Thread.Sleep(Math.Min(50, Math.Max(10, pollIntervalMs / 2)));
                }
            }

            var resultRaw = sb.ToString();

            if (!matchedPrompt &&
                PromptDetector.TryDetectPromptFromTail(resultRaw, out var tailPrompt) &&
                !string.IsNullOrWhiteSpace(tailPrompt))
            {
                updatedPromptLiteral = tailPrompt;
            }

            return TerminalOutputProcessor.Normalize(resultRaw);
        }

        private string FormatError(string errorType, HostConnection host, Exception ex)
        {
            var sb = new StringBuilder();
            string title = $"{new string('#', 20)} {errorType}: {host} {new string('#', 20)}";
            string separator = new string('#', title.Length);

            sb.AppendLine(separator);
            sb.AppendLine(title);
            sb.AppendLine(separator);

            for (var e = ex; e != null; e = e.InnerException)
            {
                sb.AppendLine($"{e.GetType().Name}: {e.Message}");
            }

            return sb.ToString();
        }

        protected virtual void OnProgressChanged(HostConnection host, string message, bool isError, bool isConnected)
        {
            ProgressChanged?.Invoke(this, new SshProgressEventArgs
            {
                Host = host,
                Message = message,
                IsError = isError,
                IsConnected = isConnected
            });
        }

        protected virtual void OnOutputReceived(HostConnection host, string output)
        {
            OutputReceived?.Invoke(this, new SshOutputEventArgs
            {
                Host = host,
                Output = output
            });
        }
    }
}
