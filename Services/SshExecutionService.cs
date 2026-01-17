using System.Text;
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
    /// Now uses connection pooling and the Expect API for improved reliability.
    /// </summary>
    public class SshExecutionService : IDisposable
    {
        private readonly SshConnectionPool? _connectionPool;
        private readonly bool _ownsPool;

        public event EventHandler<SshProgressEventArgs>? ProgressChanged;
        public event EventHandler<SshOutputEventArgs>? OutputReceived;

        private volatile bool _isRunning;
        private CancellationTokenSource? _cts;
        private bool _disposed;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets the connection pool (if pooling is enabled).
        /// </summary>
        public SshConnectionPool? ConnectionPool => _connectionPool;

        /// <summary>
        /// Gets or sets whether to use connection pooling.
        /// When enabled, connections are reused for subsequent executions.
        /// </summary>
        public bool UseConnectionPooling { get; set; }

        /// <summary>
        /// When enabled, emits debug timestamps and diagnostic info to help troubleshoot prompt detection.
        /// Debug output is sent via the OutputReceived event with [DEBUG] prefix.
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Creates a new SSH execution service without connection pooling.
        /// </summary>
        public SshExecutionService()
        {
            _connectionPool = null;
            _ownsPool = false;
            UseConnectionPooling = false;
        }

        /// <summary>
        /// Creates a new SSH execution service with an internal connection pool.
        /// </summary>
        /// <param name="enablePooling">Whether to enable connection pooling</param>
        /// <param name="poolTimeouts">Default timeouts for pooled connections</param>
        public SshExecutionService(bool enablePooling, SshTimeoutOptions? poolTimeouts = null)
        {
            if (enablePooling)
            {
                _connectionPool = new SshConnectionPool(poolTimeouts);
                _ownsPool = true;
                UseConnectionPooling = true;
            }
            else
            {
                _connectionPool = null;
                _ownsPool = false;
                UseConnectionPooling = false;
            }
        }

        /// <summary>
        /// Creates a new SSH execution service with a shared connection pool.
        /// </summary>
        /// <param name="sharedPool">Shared connection pool instance</param>
        public SshExecutionService(SshConnectionPool sharedPool)
        {
            _connectionPool = sharedPool ?? throw new ArgumentNullException(nameof(sharedPool));
            _ownsPool = false;
            UseConnectionPooling = true;
        }

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

            var timeouts = SshTimeoutOptions.FromSeconds(timeoutSeconds);

            try
            {
                foreach (var host in hosts)
                {
                    if (_cts.Token.IsCancellationRequested)
                        break;

                    if (!host.IsValid())
                        continue;

                    var result = await Task.Run(() =>
                        ExecuteSingleHost(host, commands, defaultUsername, defaultPassword, timeouts, _cts.Token));

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
        /// Executes commands on multiple hosts with custom timeout options.
        /// </summary>
        public async Task<List<ExecutionResult>> ExecuteAsync(
            IEnumerable<HostConnection> hosts,
            string[] commands,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts)
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
                        ExecuteSingleHost(host, commands, defaultUsername, defaultPassword, timeouts, _cts.Token));

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
            SshTimeoutOptions timeouts,
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
                if (UseConnectionPooling && _connectionPool != null)
                {
                    ExecuteWithPool(host, commands, username, password, timeouts, outputBuilder, cancellationToken);
                }
                else
                {
                    ExecuteWithoutPool(host, commands, username, password, timeouts, outputBuilder, cancellationToken);
                }

                result.Success = true;
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Authentication failed";
                result.Exception = ex;
                var errorOutput = FormatError("AUTHENTICATION ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Connection failed";
                result.Exception = ex;
                var errorOutput = FormatError("CONNECTION ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (Renci.SshNet.Common.SshOperationTimeoutException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Operation timed out";
                result.Exception = ex;
                var errorOutput = FormatError("TIMEOUT ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Network error";
                result.Exception = ex;
                var errorOutput = FormatError("NETWORK ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Operation cancelled";
                var errorOutput = FormatError("CANCELLED", host, new Exception("Operation was cancelled by user"));
                outputBuilder.AppendLine(errorOutput);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                var errorOutput = FormatError("ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }

            result.Output = outputBuilder.ToString();
            return result;
        }

        /// <summary>
        /// Executes commands using connection pooling and the new SshShellSession.
        /// </summary>
        private void ExecuteWithPool(
            HostConnection host,
            string[] commands,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken)
        {
            // Get or create connection from pool
            var (client, session) = _connectionPool!.CreateSessionAsync(host, username, password, timeouts, cancellationToken)
                .GetAwaiter().GetResult();

            try
            {
                OnProgressChanged(host, $"Connected to {host} (pooled)", false, true);

                // Configure debug mode BEFORE subscribing to events
                session.DebugMode = DebugMode;

                // Track if we've sent the header yet (to avoid duplicating in outputBuilder)
                bool headerSent = false;

                // Subscribe to real-time output - capture ALL output to outputBuilder for history
                session.OutputReceived += (s, e) =>
                {
                    if (headerSent) // Only capture command output after header is sent
                    {
                        outputBuilder.Append(e.Output);
                    }
                    OnOutputReceived(host, e.Output);
                };
                session.DebugOutput += (s, e) =>
                {
                    outputBuilder.Append(e.Output); // Include debug in history
                    OnOutputReceived(host, e.Output);
                };

                // Emit debug state for troubleshooting
                if (DebugMode)
                {
                    var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    var debugMsg = $"[DEBUG {timestamp}] SshExecutionService.DebugMode = {DebugMode}, session.DebugMode = {session.DebugMode} (pooled)\r\n";
                    outputBuilder.Append(debugMsg);
                    OnOutputReceived(host, debugMsg);
                }

                // Build header
                var prompt = session.CurrentPrompt;
                string header = $"{new string('#', 20)} CONNECTED TO {host} {prompt} {new string('#', 20)}";
                string separator = new string('#', header.Length);

                outputBuilder.AppendLine(separator);
                outputBuilder.AppendLine(header);
                outputBuilder.AppendLine(separator);
                outputBuilder.Append(prompt);

                OnOutputReceived(host, outputBuilder.ToString());
                headerSent = true;

                // Execute commands using the session
                // Output is captured via OutputReceived event above, no need to append return value
                session.ExecuteBatchAsync(commands, host.Variables, cancellationToken)
                    .GetAwaiter().GetResult();
            }
            finally
            {
                session.Dispose();
                // Note: Connection stays in pool for reuse
            }
        }

        /// <summary>
        /// Executes commands without pooling (original behavior, but using SshShellSession).
        /// </summary>
        private void ExecuteWithoutPool(
            HostConnection host,
            string[] commands,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken)
        {
            using var client = new SshClient(host.IpAddress, host.Port, username, password);
            client.ConnectionInfo.Timeout = timeouts.ConnectionTimeout;

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
            using var session = new SshShellSession(shellStream, timeouts);

            // Configure debug mode BEFORE subscribing to events
            session.DebugMode = DebugMode;

            // Track if we've sent the header yet (to avoid duplicating in outputBuilder)
            bool headerSent = false;

            // Subscribe to real-time output - capture ALL output to outputBuilder for history
            session.OutputReceived += (s, e) =>
            {
                if (headerSent) // Only capture command output after header is sent
                {
                    outputBuilder.Append(e.Output);
                }
                OnOutputReceived(host, e.Output);
            };
            session.DebugOutput += (s, e) =>
            {
                outputBuilder.Append(e.Output); // Include debug in history
                OnOutputReceived(host, e.Output);
            };

            // Emit debug state for troubleshooting
            if (DebugMode)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var debugMsg = $"[DEBUG {timestamp}] SshExecutionService.DebugMode = {DebugMode}, session.DebugMode = {session.DebugMode}\r\n";
                outputBuilder.Append(debugMsg);
                OnOutputReceived(host, debugMsg);
            }

            // Initialize session (detect prompt)
            var banner = session.InitializeAsync(cancellationToken).GetAwaiter().GetResult();

            // Build header
            var prompt = session.CurrentPrompt;
            string header = $"{new string('#', 20)} CONNECTED TO {host} {prompt} {new string('#', 20)}";
            string separator = new string('#', header.Length);

            outputBuilder.AppendLine(separator);
            outputBuilder.AppendLine(header);
            outputBuilder.AppendLine(separator);
            outputBuilder.Append(prompt);

            OnOutputReceived(host, outputBuilder.ToString());
            headerSent = true;

            // Execute commands using the session
            // Output is captured via OutputReceived event above, no need to append return value
            session.ExecuteBatchAsync(commands, host.Variables, cancellationToken)
                .GetAwaiter().GetResult();

            client.Disconnect();
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

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _cts?.Cancel();
                _cts?.Dispose();

                if (_ownsPool && _connectionPool != null)
                {
                    _connectionPool.Dispose();
                }
            }
        }
    }
}
