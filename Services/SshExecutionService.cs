using System.Text;
using Rebex.Net;
using Rebex.TerminalEmulation;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Utilities;

// Alias to avoid conflict with SSH_Helper.Services.Scripting namespace
using RebexScripting = Rebex.TerminalEmulation.Scripting;

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
    /// Event arguments for column update requests from scripts.
    /// </summary>
    public class SshColumnUpdateEventArgs : EventArgs
    {
        public HostConnection Host { get; set; } = new();
        public string ColumnName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles SSH command execution against remote hosts.
    /// Now uses connection pooling and Rebex Scripting API for improved reliability.
    /// </summary>
    public class SshExecutionService : IDisposable
    {
        private readonly SshConnectionPool? _connectionPool;
        private readonly bool _ownsPool;

        public event EventHandler<SshProgressEventArgs>? ProgressChanged;
        public event EventHandler<SshOutputEventArgs>? OutputReceived;
        public event EventHandler<SshColumnUpdateEventArgs>? ColumnUpdateRequested;

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
        /// When enabled, emits detailed startup timing info to help diagnose delays from button click to SSH connection.
        /// Debug output is sent via the OutputReceived event with [SSH DEBUG] prefix.
        /// </summary>
        public bool SshDebugMode { get; set; }

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
        /// <param name="showHeader">If false, suppresses the "CONNECTED TO" header output.</param>
        public async Task<List<ExecutionResult>> ExecuteAsync(
            IEnumerable<HostConnection> hosts,
            string[] commands,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            bool showHeader = true)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var hostList = hosts.ToList();
            var dummyHost = hostList.FirstOrDefault() ?? new HostConnection();
            SshDebugLog(dummyHost, "SERVICE", $"ExecuteAsync entered. Hosts: {hostList.Count}, Commands: {commands.Length}", sw);

            var results = new List<ExecutionResult>();
            _cts = new CancellationTokenSource();
            _isRunning = true;
            SshDebugLog(dummyHost, "SERVICE", "CancellationTokenSource created, _isRunning = true", sw);

            try
            {
                foreach (var host in hostList)
                {
                    if (_cts.Token.IsCancellationRequested)
                        break;

                    if (!host.IsValid())
                    {
                        SshDebugLog(host, "SERVICE", $"Skipping invalid host: {host.IpAddress}", sw);
                        continue;
                    }

                    SshDebugLog(host, "SERVICE", $"Starting Task.Run for ExecuteSingleHost on {host.IpAddress}:{host.Port}", sw);
                    var result = await Task.Run(() =>
                        ExecuteSingleHost(host, commands, defaultUsername, defaultPassword, timeouts, _cts.Token, showHeader));
                    SshDebugLog(host, "SERVICE", $"ExecuteSingleHost completed for {host.IpAddress}", sw);

                    results.Add(result);
                }
            }
            finally
            {
                _isRunning = false;
                SshDebugLog(dummyHost, "SERVICE", "ExecuteAsync complete", sw);
            }

            return results;
        }

        /// <summary>
        /// Executes a preset on multiple hosts. Automatically detects script vs simple commands.
        /// </summary>
        /// <param name="showHeader">If false, suppresses the "CONNECTED TO" header output.</param>
        public async Task<List<ExecutionResult>> ExecutePresetAsync(
            IEnumerable<HostConnection> hosts,
            PresetInfo preset,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            bool showHeader = true)
        {
            // Check if this is a YAML script
            if (preset.IsScript)
            {
                return await ExecuteScriptAsync(hosts, preset.Commands, defaultUsername, defaultPassword, timeouts, showHeader);
            }
            else
            {
                // Simple commands - use existing logic
                var commands = preset.Commands.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                return await ExecuteAsync(hosts, commands, defaultUsername, defaultPassword, timeouts, showHeader);
            }
        }

        /// <summary>
        /// Executes a YAML script on multiple hosts.
        /// </summary>
        /// <param name="showHeader">If false, suppresses the "CONNECTED TO" header output.</param>
        public async Task<List<ExecutionResult>> ExecuteScriptAsync(
            IEnumerable<HostConnection> hosts,
            string scriptText,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            bool showHeader = true)
        {
            var results = new List<ExecutionResult>();
            _cts = new CancellationTokenSource();
            _isRunning = true;

            // Parse the script once
            var parser = new ScriptParser();
            Script script;
            try
            {
                script = parser.Parse(scriptText);
                var validationErrors = parser.Validate(script, scriptText);
                if (validationErrors.Count > 0)
                {
                    throw new ScriptParseException("Script validation failed:\n" + string.Join("\n", validationErrors));
                }
            }
            catch (ScriptParseException ex)
            {
                // Return error result for all hosts
                foreach (var host in hosts)
                {
                    results.Add(new ExecutionResult
                    {
                        Host = host,
                        Success = false,
                        ErrorMessage = ex.Message,
                        Output = $"Script parse error: {ex.Message}",
                        Timestamp = DateTime.Now
                    });
                }
                _isRunning = false;
                return results;
            }

            try
            {
                foreach (var host in hosts)
                {
                    if (_cts.Token.IsCancellationRequested)
                        break;

                    if (!host.IsValid())
                        continue;

                    var result = await Task.Run(() =>
                        ExecuteScriptOnHost(host, script, defaultUsername, defaultPassword, timeouts, _cts.Token, showHeader));

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
        /// Executes multiple presets from a folder on multiple hosts with configurable parallelism.
        /// </summary>
        /// <param name="hosts">Collection of host connections</param>
        /// <param name="presets">Dictionary of preset name to PresetInfo</param>
        /// <param name="defaultUsername">Default username if not specified per-host</param>
        /// <param name="defaultPassword">Default password if not specified per-host</param>
        /// <param name="timeouts">Timeout configuration</param>
        /// <param name="options">Folder execution options (parallelism settings)</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <returns>Results for each host</returns>
        public async Task<List<ExecutionResult>> ExecuteFolderAsync(
            IEnumerable<HostConnection> hosts,
            Dictionary<string, PresetInfo> presets,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            FolderExecutionOptions options,
            IProgress<FolderExecutionProgress>? progress = null)
        {
            var results = new List<ExecutionResult>();
            _cts = new CancellationTokenSource();
            _isRunning = true;

            var hostList = hosts.Where(h => h.IsValid()).ToList();
            var presetNames = options.SelectedPresets;
            int totalHosts = hostList.Count;
            int totalPresets = presetNames.Count;
            int completedHosts = 0;
            bool errorOccurred = false;

            try
            {
                // Process hosts in batches based on ParallelHostCount
                var hostBatches = hostList
                    .Select((host, index) => new { host, index })
                    .GroupBy(x => x.index / options.ParallelHostCount)
                    .Select(g => g.Select(x => x.host).ToList())
                    .ToList();

                foreach (var batch in hostBatches)
                {
                    if (_cts.Token.IsCancellationRequested || (options.StopOnFirstError && errorOccurred))
                        break;

                    // Execute batch in parallel
                    var batchTasks = batch.Select(async host =>
                    {
                        var hostResult = new ExecutionResult
                        {
                            Host = host,
                            Timestamp = DateTime.Now,
                            Success = true
                        };

                        var outputBuilder = new StringBuilder();
                        int completedPresets = 0;
                        bool isFirstPreset = true;

                        // Execute presets on this host
                        if (options.RunPresetsInParallel)
                        {
                            // Parallel preset execution
                            var presetTasks = presetNames.Select(async presetName =>
                            {
                                if (_cts.Token.IsCancellationRequested || (options.StopOnFirstError && errorOccurred))
                                    return;

                                if (!presets.TryGetValue(presetName, out var preset))
                                    return;

                                progress?.Report(new FolderExecutionProgress
                                {
                                    CurrentHost = host.IpAddress,
                                    CurrentPreset = presetName,
                                    CompletedPresets = completedPresets,
                                    TotalPresets = totalPresets,
                                    CompletedHosts = completedHosts,
                                    TotalHosts = totalHosts
                                });

                                // Add preset separator
                                if (!options.SuppressPresetNames)
                                {
                                    var separator = $"\r\n═══ {presetName} ═══\r\n";
                                    lock (outputBuilder) { outputBuilder.Append(separator); }
                                    OnOutputReceived(host, separator);
                                }

                                var presetResults = await ExecutePresetAsync(
                                    new[] { host },
                                    preset,
                                    defaultUsername,
                                    defaultPassword,
                                    timeouts,
                                    showHeader: false);

                                if (presetResults.Count > 0)
                                {
                                    var presetResult = presetResults[0];
                                    lock (outputBuilder) { outputBuilder.Append(presetResult.Output); }

                                    if (!presetResult.Success)
                                    {
                                        hostResult.Success = false;
                                        hostResult.ErrorMessage = presetResult.ErrorMessage;
                                        if (options.StopOnFirstError)
                                            errorOccurred = true;

                                        // Mark failed preset in output
                                        if (!options.SuppressPresetNames)
                                        {
                                            var failMarker = $"\r\n═══ {presetName} [FAILED] ═══\r\n";
                                            lock (outputBuilder) { outputBuilder.Append(failMarker); }
                                            OnOutputReceived(host, failMarker);
                                        }
                                    }
                                }

                                Interlocked.Increment(ref completedPresets);
                            });

                            await Task.WhenAll(presetTasks);
                        }
                        else
                        {
                            // Sequential preset execution
                            foreach (var presetName in presetNames)
                            {
                                if (_cts.Token.IsCancellationRequested || (options.StopOnFirstError && errorOccurred))
                                    break;

                                if (!presets.TryGetValue(presetName, out var preset))
                                    continue;

                                progress?.Report(new FolderExecutionProgress
                                {
                                    CurrentHost = host.IpAddress,
                                    CurrentPreset = presetName,
                                    CompletedPresets = completedPresets,
                                    TotalPresets = totalPresets,
                                    CompletedHosts = completedHosts,
                                    TotalHosts = totalHosts
                                });

                                // Add preset separator
                                if (!options.SuppressPresetNames)
                                {
                                    var separator = $"\r\n═══ {presetName} ═══\r\n";
                                    outputBuilder.Append(separator);
                                    OnOutputReceived(host, separator);
                                }

                                var presetResults = await ExecutePresetAsync(
                                    new[] { host },
                                    preset,
                                    defaultUsername,
                                    defaultPassword,
                                    timeouts,
                                    showHeader: isFirstPreset);

                                isFirstPreset = false;

                                if (presetResults.Count > 0)
                                {
                                    var presetResult = presetResults[0];
                                    outputBuilder.Append(presetResult.Output);

                                    if (!presetResult.Success)
                                    {
                                        hostResult.Success = false;
                                        hostResult.ErrorMessage = presetResult.ErrorMessage;
                                        if (options.StopOnFirstError)
                                        {
                                            errorOccurred = true;
                                            // Mark failed preset in output
                                            if (!options.SuppressPresetNames)
                                            {
                                                var failMarker = $"\r\n═══ {presetName} [FAILED] ═══\r\n";
                                                outputBuilder.Append(failMarker);
                                                OnOutputReceived(host, failMarker);
                                            }
                                            break;
                                        }
                                    }
                                }

                                completedPresets++;
                            }
                        }

                        hostResult.Output = outputBuilder.ToString();
                        return hostResult;
                    });

                    var batchResults = await Task.WhenAll(batchTasks);
                    results.AddRange(batchResults);
                    completedHosts += batch.Count;

                    progress?.Report(new FolderExecutionProgress
                    {
                        CurrentHost = string.Empty,
                        CurrentPreset = string.Empty,
                        CompletedPresets = totalPresets,
                        TotalPresets = totalPresets,
                        CompletedHosts = completedHosts,
                        TotalHosts = totalHosts
                    });
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
            CancellationToken cancellationToken,
            bool showHeader = true)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog(host, "HOST", $"ExecuteSingleHost entered for {host.IpAddress}:{host.Port}");

            var result = new ExecutionResult
            {
                Host = host,
                Timestamp = DateTime.Now
            };

            var outputBuilder = new StringBuilder();
            string username = !string.IsNullOrWhiteSpace(host.Username) ? host.Username : defaultUsername;
            string password = !string.IsNullOrWhiteSpace(host.Password) ? host.Password : defaultPassword;
            SshDebugLog(host, "HOST", $"Credentials resolved. Username: {username}, UsePooling: {UseConnectionPooling}", sw);

            try
            {
                if (UseConnectionPooling && _connectionPool != null)
                {
                    SshDebugLog(host, "HOST", "Calling ExecuteWithPool", sw);
                    ExecuteWithPool(host, commands, username, password, timeouts, outputBuilder, cancellationToken, showHeader);
                    SshDebugLog(host, "HOST", "ExecuteWithPool returned", sw);
                }
                else
                {
                    SshDebugLog(host, "HOST", "Calling ExecuteWithoutPool", sw);
                    ExecuteWithoutPool(host, commands, username, password, timeouts, outputBuilder, cancellationToken, showHeader);
                    SshDebugLog(host, "HOST", "ExecuteWithoutPool returned", sw);
                }

                result.Success = true;
                SshDebugLog(host, "HOST", "Execution successful", sw);
            }
            catch (SshException ex) when (IsAuthenticationError(ex))
            {
                result.Success = false;
                result.ErrorMessage = "Authentication failed";
                result.Exception = ex;
                var errorOutput = FormatError("AUTHENTICATION ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (SshException ex) when (IsConnectionError(ex))
            {
                result.Success = false;
                result.ErrorMessage = "Connection failed";
                result.Exception = ex;
                var errorOutput = FormatError("CONNECTION ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (SshException ex) when (IsTimeoutError(ex))
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
        /// Executes a script on a single host.
        /// </summary>
        private ExecutionResult ExecuteScriptOnHost(
            HostConnection host,
            Script script,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            CancellationToken cancellationToken,
            bool showHeader = true)
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
                    ExecuteScriptWithPool(host, script, username, password, timeouts, outputBuilder, cancellationToken, showHeader);
                }
                else
                {
                    ExecuteScriptWithoutPool(host, script, username, password, timeouts, outputBuilder, cancellationToken, showHeader);
                }

                result.Success = true;
            }
            catch (SshException ex) when (IsAuthenticationError(ex))
            {
                result.Success = false;
                result.ErrorMessage = "Authentication failed";
                result.Exception = ex;
                var errorOutput = FormatError("AUTHENTICATION ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (SshException ex) when (IsConnectionError(ex))
            {
                result.Success = false;
                result.ErrorMessage = "Connection failed";
                result.Exception = ex;
                var errorOutput = FormatError("CONNECTION ERROR", host, ex);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (SshException ex) when (IsTimeoutError(ex))
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
        /// Executes a script using connection pooling.
        /// </summary>
        /// <param name="showHeader">If false, suppresses the header output.</param>
        private void ExecuteScriptWithPool(
            HostConnection host,
            Script script,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken,
            bool showHeader = true)
        {
            var (client, session) = _connectionPool!.CreateSessionAsync(host, username, password, timeouts, cancellationToken)
                .GetAwaiter().GetResult();

            try
            {
                OnProgressChanged(host, $"Connected to {host} (pooled, script mode)", false, true);
                session.DebugMode = DebugMode;

                // Build header with script name (only if showHeader is true)
                if (showHeader)
                {
                    var prompt = session.CurrentPrompt;
                    var scriptName = !string.IsNullOrEmpty(script.Name) ? $" {script.Name}" : "";
                    string header = $"{new string('#', 20)} {host} {prompt} SCRIPT: {scriptName} {new string('#', 20)}";
                    string separator = new string('#', header.Length);

                    outputBuilder.AppendLine(separator);
                    outputBuilder.AppendLine(header);
                    outputBuilder.AppendLine(separator);

                    OnOutputReceived(host, outputBuilder.ToString());
                }

                // Create script context with host variables
                var context = new ScriptContext(host.Variables);
                context.Session = session;
                context.DebugMode = DebugMode;

                // Wire up context output to our events
                context.OutputReceived += (s, e) =>
                {
                    var output = e.Message + Environment.NewLine;
                    outputBuilder.Append(output);
                    OnOutputReceived(host, output);
                };

                // Wire up column update requests
                context.ColumnUpdateRequested += (s, e) =>
                {
                    OnColumnUpdateRequested(host, e.ColumnName, e.Value);
                };

                // Execute the script
                var executor = new ScriptExecutor();
                var scriptResult = executor.ExecuteAsync(script, context, cancellationToken)
                    .GetAwaiter().GetResult();

                // Report final status
                var statusMsg = $"\n=== Script {scriptResult.Status}: {scriptResult.Message} ===\n";
                outputBuilder.Append(statusMsg);
                OnOutputReceived(host, statusMsg);
            }
            finally
            {
                session.Dispose();
            }
        }

        /// <summary>
        /// Executes a script without connection pooling.
        /// </summary>
        /// <param name="showHeader">If false, suppresses the header output.</param>
        private void ExecuteScriptWithoutPool(
            HostConnection host,
            Script script,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken,
            bool showHeader = true)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog(host, "SCRIPT", $"ExecuteScriptWithoutPool entered for {host.IpAddress}:{host.Port}");

            // Create Rebex SSH client
            using var client = new Ssh();
            client.Timeout = (int)timeouts.ConnectionTimeout.TotalMilliseconds;
            SshDebugLog(host, "SCRIPT", $"Ssh client created. Timeout: {timeouts.ConnectionTimeout.TotalSeconds}s", sw);

            SshDebugLog(host, "SCRIPT", "Calling client.Connect()", sw);
            var connectSw = System.Diagnostics.Stopwatch.StartNew();
            client.Connect(host.IpAddress, host.Port);
            connectSw.Stop();
            SshDebugLog(host, "SCRIPT", $"client.Connect() completed in {connectSw.ElapsedMilliseconds}ms", sw);

            SshDebugLog(host, "SCRIPT", "Calling client.Login()", sw);
            client.Login(username, password);
            SshDebugLog(host, "SCRIPT", "client.Login() completed", sw);

            OnProgressChanged(host, $"Connected to {host} (script mode)", false, true);

            SshDebugLog(host, "SCRIPT", "Starting scripting session", sw);
            RebexScripting scripting = client.StartScripting();
            scripting.Timeout = (int)timeouts.CommandTimeout.TotalMilliseconds;
            SshDebugLog(host, "SCRIPT", "Scripting session created", sw);

            using var session = new SshShellSession(client, scripting, timeouts);
            session.DebugMode = DebugMode;

            // Also enable debug mode on the session if SSH debug is on
            if (SshDebugMode)
            {
                session.DebugMode = true;
            }

            // Subscribe to session debug output so we can see banner detection, prompt detection, etc.
            session.DebugOutput += (s, e) =>
            {
                outputBuilder.Append(e.Output);
                OnOutputReceived(host, e.Output);
            };

            // Initialize session (detect prompt)
            SshDebugLog(host, "SCRIPT", "Calling session.InitializeAsync - waiting for prompt", sw);
            try
            {
                var banner = session.InitializeAsync(cancellationToken).GetAwaiter().GetResult();
                SshDebugLog(host, "SCRIPT", $"session.InitializeAsync completed. Prompt: {session.CurrentPrompt}", sw);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Provide more context about why the session might have failed
                SshDebugLog(host, "SCRIPT", $"SESSION INIT FAILED during InitializeAsync: {ex.Message}", sw);
                SshDebugLog(host, "SCRIPT", $"Client.IsConnected: {client.IsConnected}", sw);
                throw;
            }

            // Build header with script name (only if showHeader is true)
            if (showHeader)
            {
                var prompt = session.CurrentPrompt;
                var scriptName = !string.IsNullOrEmpty(script.Name) ? $" {script.Name}" : "";
                string header = $"{new string('#', 20)} SCRIPT: {host} {prompt}{scriptName} {new string('#', 20)}";
                string separator = new string('#', header.Length);

                outputBuilder.AppendLine(separator);
                outputBuilder.AppendLine(header);
                outputBuilder.AppendLine(separator);

                OnOutputReceived(host, outputBuilder.ToString());
            }

            // Create script context with host variables
            var context = new ScriptContext(host.Variables);
            context.Session = session;
            context.DebugMode = DebugMode;

            // Wire up context output to our events
            context.OutputReceived += (s, e) =>
            {
                var output = e.Message + Environment.NewLine;
                outputBuilder.Append(output);
                OnOutputReceived(host, output);
            };

            // Wire up column update requests
            context.ColumnUpdateRequested += (s, e) =>
            {
                OnColumnUpdateRequested(host, e.ColumnName, e.Value);
            };

            // Execute the script
            var executor = new ScriptExecutor();
            var scriptResult = executor.ExecuteAsync(script, context, cancellationToken)
                .GetAwaiter().GetResult();

            // Report final status
            var statusMsg = $"\n=== Script {scriptResult.Status}: {scriptResult.Message} ===\n";
            outputBuilder.Append(statusMsg);
            OnOutputReceived(host, statusMsg);

            client.Disconnect();
        }

        /// <summary>
        /// Executes commands using connection pooling and the new SshShellSession.
        /// </summary>
        /// <param name="showHeader">If false, suppresses the "CONNECTED TO" header output.</param>
        private void ExecuteWithPool(
            HostConnection host,
            string[] commands,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken,
            bool showHeader = true)
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
                bool headerSent = !showHeader; // If not showing header, pretend it's already sent

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

                // Build header (only if showHeader is true)
                if (showHeader)
                {
                    var prompt = session.CurrentPrompt;
                    string header = $"{new string('#', 20)} CONNECTED TO {host} {prompt} {new string('#', 20)}";
                    string separator = new string('#', header.Length);

                    outputBuilder.AppendLine("\r\n" + separator);
                    outputBuilder.AppendLine(header);
                    outputBuilder.AppendLine(separator);
                    outputBuilder.Append(prompt + " ");

                    OnOutputReceived(host, outputBuilder.ToString());
                    headerSent = true;
                }

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
        /// <param name="showHeader">If false, suppresses the "CONNECTED TO" header output.</param>
        private void ExecuteWithoutPool(
            HostConnection host,
            string[] commands,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken,
            bool showHeader = true)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog(host, "SSH", $"ExecuteWithoutPool entered. Creating Ssh client for {host.IpAddress}:{host.Port}");

            // Test raw TCP connectivity first to isolate network latency from SSH negotiation
            if (SshDebugMode)
            {
                try
                {
                    using var tcpTest = new System.Net.Sockets.TcpClient();
                    var tcpSw = System.Diagnostics.Stopwatch.StartNew();
                    tcpTest.Connect(host.IpAddress, host.Port);
                    tcpSw.Stop();
                    SshDebugLog(host, "SSH", $"TCP pre-check completed in {tcpSw.ElapsedMilliseconds}ms (raw socket connect)", sw);
                    tcpTest.Close();
                }
                catch (Exception ex)
                {
                    SshDebugLog(host, "SSH", $"TCP pre-check failed: {ex.Message}", sw);
                }
            }

            // Create Rebex SSH client
            using var client = new Ssh();
            client.Timeout = (int)timeouts.ConnectionTimeout.TotalMilliseconds;
            SshDebugLog(host, "SSH", $"Ssh client created. Timeout: {timeouts.ConnectionTimeout.TotalSeconds}s", sw);

            SshDebugLog(host, "SSH", "Calling client.Connect() - TCP handshake + SSH negotiation starting", sw);
            var connectSw = System.Diagnostics.Stopwatch.StartNew();
            client.Connect(host.IpAddress, host.Port);
            connectSw.Stop();
            SshDebugLog(host, "SSH", $"client.Connect() completed in {connectSw.ElapsedMilliseconds}ms", sw);

            SshDebugLog(host, "SSH", "Calling client.Login()", sw);
            client.Login(username, password);
            SshDebugLog(host, "SSH", "client.Login() completed - SSH session established", sw);

            OnProgressChanged(host, $"Connected to {host}", false, true);

            SshDebugLog(host, "SSH", "Starting scripting session", sw);
            RebexScripting scripting = client.StartScripting();
            scripting.Timeout = (int)timeouts.CommandTimeout.TotalMilliseconds;
            SshDebugLog(host, "SSH", "Scripting session created", sw);

            SshDebugLog(host, "SSH", "Creating SshShellSession", sw);
            using var session = new SshShellSession(client, scripting, timeouts);
            SshDebugLog(host, "SSH", "SshShellSession created", sw);

            // Configure debug mode BEFORE subscribing to events
            session.DebugMode = DebugMode;

            // Track if we've sent the header yet (to avoid duplicating in outputBuilder)
            bool headerSent = !showHeader; // If not showing header, pretend it's already sent

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
            SshDebugLog(host, "SSH", "Calling session.InitializeAsync - waiting for shell prompt", sw);
            var banner = session.InitializeAsync(cancellationToken).GetAwaiter().GetResult();
            SshDebugLog(host, "SSH", $"session.InitializeAsync completed. Prompt detected: {session.CurrentPrompt}", sw);

            // Build header (only if showHeader is true)
            if (showHeader)
            {
                var prompt = session.CurrentPrompt;
                string header = $"{new string('#', 20)} CONNECTED TO {host} {prompt} {new string('#', 20)}";
                string separator = new string('#', header.Length);

                outputBuilder.AppendLine("\r\n" + separator);
                outputBuilder.AppendLine(header);
                outputBuilder.AppendLine(separator);
                outputBuilder.Append(prompt + " ");

                OnOutputReceived(host, outputBuilder.ToString());
                headerSent = true;
            }

            // Execute commands using the session
            // Output is captured via OutputReceived event above, no need to append return value
            SshDebugLog(host, "SSH", $"Calling session.ExecuteBatchAsync with {commands.Length} command(s)", sw);
            session.ExecuteBatchAsync(commands, host.Variables, cancellationToken)
                .GetAwaiter().GetResult();
            SshDebugLog(host, "SSH", "session.ExecuteBatchAsync completed", sw);

            SshDebugLog(host, "SSH", "Calling client.Disconnect()", sw);
            client.Disconnect();
            SshDebugLog(host, "SSH", "client.Disconnect() completed", sw);
        }

        /// <summary>
        /// Checks if an SshException indicates an authentication error.
        /// </summary>
        private static bool IsAuthenticationError(SshException ex)
        {
            var msg = ex.Message.ToLowerInvariant();
            return msg.Contains("authentication") || msg.Contains("password") || msg.Contains("login") || msg.Contains("credentials");
        }

        /// <summary>
        /// Checks if an SshException indicates a connection error.
        /// </summary>
        private static bool IsConnectionError(SshException ex)
        {
            var msg = ex.Message.ToLowerInvariant();
            return msg.Contains("connection") || msg.Contains("refused") || msg.Contains("reset") || msg.Contains("closed");
        }

        /// <summary>
        /// Checks if an SshException indicates a timeout error.
        /// </summary>
        private static bool IsTimeoutError(SshException ex)
        {
            var msg = ex.Message.ToLowerInvariant();
            return msg.Contains("timeout") || msg.Contains("time limit") || msg.Contains("timed out");
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

        protected virtual void OnColumnUpdateRequested(HostConnection host, string columnName, string value)
        {
            ColumnUpdateRequested?.Invoke(this, new SshColumnUpdateEventArgs
            {
                Host = host,
                ColumnName = columnName,
                Value = value
            });
        }

        /// <summary>
        /// Emits SSH debug timing information when SshDebugMode is enabled.
        /// </summary>
        private void SshDebugLog(HostConnection host, string phase, string message, System.Diagnostics.Stopwatch? sw = null)
        {
            if (!SshDebugMode) return;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var elapsed = sw != null ? $" (+{sw.ElapsedMilliseconds}ms)" : "";
            var output = $"[SSH DEBUG {timestamp}]{elapsed} {phase}: {message}\r\n";
            OnOutputReceived(host, output);
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
