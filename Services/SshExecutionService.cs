using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Rebex.Net;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Vault;
using SSH_Helper.UI;
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
    /// Event arguments for environment variable update requests from scripts.
    /// </summary>
    public class SshEnvironmentVariableUpdateEventArgs : EventArgs
    {
        public HostConnection Host { get; set; } = new();
        public string Variable { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event arguments for completed command execution.
    /// </summary>
    public class SshCommandCompletedEventArgs : EventArgs
    {
        public HostConnection Host { get; set; } = new();
        public string Command { get; set; } = string.Empty;
    }

    /// <summary>
    /// Handles SSH command execution against remote hosts.
    /// Now uses connection pooling and Rebex Scripting API for improved reliability.
    /// </summary>
    public class SshExecutionService : IDisposable
    {
        private const int MaxParallelHosts = 100;

        private const string SingleHostOnlyMessageSuffix =
            " not supported in folder or multi-host runs. Run the script against a single current host instead.";

        internal static readonly string[] NonRsaHostKeyFallbackAlgorithms =
        {
            "ssh-ed25519",
            "ecdsa-sha2-nistp256",
            "ecdsa-sha2-nistp384",
            "ecdsa-sha2-nistp521"
        };

        internal static readonly string[] Ed25519OnlyHostKeyAlgorithms =
        {
            "ssh-ed25519"
        };

        internal static readonly string[] ConservativeEncryptionFallbackAlgorithms =
        {
            "aes256-ctr",
            "aes128-ctr",
            "aes256-cbc",
            "aes128-cbc"
        };

        internal static readonly string[] ConservativeMacFallbackAlgorithms =
        {
            "hmac-sha2-256",
            "hmac-sha2-512",
            "hmac-sha1"
        };

        /// <summary>
        /// Tracks which algorithm tier succeeded for a given host:port, so subsequent
        /// connections can skip failed tiers and connect faster.
        /// </summary>
        internal enum HostKeyAlgorithmTier { Default, NonRsa, Ed25519Only }
        internal static readonly ConcurrentDictionary<string, HostKeyAlgorithmTier> HostAlgorithmCache = new();

        private readonly SshConnectionPool? _connectionPool;
        private readonly bool _ownsPool;
        private readonly IBrowserCallbackUiHost? _browserCallbackUiHost;
        private ILocalCmdConfirmation? _localCmdConfirmation;

        public event EventHandler<SshProgressEventArgs>? ProgressChanged;
        public event EventHandler<SshOutputEventArgs>? OutputReceived;
        public event EventHandler<SshColumnUpdateEventArgs>? ColumnUpdateRequested;
        public event EventHandler<SshEnvironmentVariableUpdateEventArgs>? EnvironmentVariableUpdateRequested;
        public event EventHandler<SshCommandCompletedEventArgs>? CommandCompleted;
        public event EventHandler? ExecutionCompleted;
        public event EventHandler<StepExecutionEventArgs>? StepStarting;
        public event EventHandler<StepExecutionEventArgs>? StepCompleted;
        public event EventHandler<DebugPauseStateChangedEventArgs>? DebugPauseStateChanged;

        private volatile bool _isRunning;
        private CancellationTokenSource? _cts;
        private volatile bool _stopOnFirstErrorCancellationRequested;
        private bool _disposed;
        private readonly object _executionLock = new();
        private volatile ScriptContext? _activeScriptContext;
        private readonly object _flowCanvasDebugBootstrapLock = new();
        private FlowCanvasDebugBootstrapState? _pendingFlowCanvasDebugBootstrapState;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// The script context for the currently executing script, if any.
        /// Used by FlowCanvas to access DebugState for breakpoint/step control.
        /// </summary>
        public ScriptContext? ActiveScriptContext => _activeScriptContext;

        private CancellationToken BeginExecution()
        {
            lock (_executionLock)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _stopOnFirstErrorCancellationRequested = false;
                _isRunning = true;
                return _cts.Token;
            }
        }

        private void EndExecution()
        {
            lock (_executionLock)
            {
                _isRunning = false;
                _stopOnFirstErrorCancellationRequested = false;
                _activeScriptContext = null;
                var cts = _cts;
                _cts = null;
                cts?.Dispose();
            }

            ClearFlowCanvasDebugStateForRun();
            OnExecutionCompleted();
        }

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
        /// Optional VaultService for resolving vault:// variable references during script execution.
        /// </summary>
        public VaultService? VaultService { get; set; }

        /// <summary>
        /// Optional environment-specific Vault profile override.
        /// </summary>
        public string? EnvironmentVaultProfile { get; set; }

        /// <summary>
        /// When enabled, emits debug timestamps and diagnostic info to help troubleshoot prompt detection.
        /// Debug output is sent via the OutputReceived event with [DEBUG] prefix.
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Configures Flow Canvas step-path mapping and debug flags to apply synchronously
        /// to each script context before its first step executes.
        /// </summary>
        public void ConfigureFlowCanvasDebugStateForRun(
            IReadOnlyDictionary<string, string> nodeToStepPathMap,
            IReadOnlyCollection<string> breakpointNodeIds,
            IReadOnlyCollection<string> disabledNodeIds)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in nodeToStepPathMap)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    continue;

                map[pair.Key] = pair.Value;
            }

            var breakpoints = new HashSet<string>(
                breakpointNodeIds.Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
            var disabled = new HashSet<string>(
                disabledNodeIds.Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);

            lock (_flowCanvasDebugBootstrapLock)
            {
                _pendingFlowCanvasDebugBootstrapState = new FlowCanvasDebugBootstrapState(
                    map,
                    breakpoints,
                    disabled);
            }
        }

        /// <summary>
        /// Clears any pending Flow Canvas run-start debug bootstrap state.
        /// </summary>
        public void ClearFlowCanvasDebugStateForRun()
        {
            lock (_flowCanvasDebugBootstrapLock)
            {
                _pendingFlowCanvasDebugBootstrapState = null;
            }
        }


        /// <summary>
        /// When enabled, attempts SSH agent authentication before falling back to key/password.
        /// </summary>
        public bool PreferSshAgent
        {
            get => _preferSshAgent;
            set
            {
                _preferSshAgent = value;
                if (_connectionPool != null)
                {
                    _connectionPool.PreferSshAgent = value;
                }
            }
        }

        private bool _preferSshAgent;

        /// <summary>
        /// Creates a new SSH execution service without connection pooling.
        /// </summary>
        public SshExecutionService()
        {
            _connectionPool = null;
            _ownsPool = false;
            UseConnectionPooling = false;
            _localCmdConfirmation = new LocalCmdConfirmationDialog();
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

            _localCmdConfirmation = new LocalCmdConfirmationDialog();
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
            _localCmdConfirmation = new LocalCmdConfirmationDialog();
        }

        public void SetLocalCmdConfirmation(ILocalCmdConfirmation? confirmation)
        {
            _localCmdConfirmation = confirmation;
        }

        internal SshExecutionService(IBrowserCallbackUiHost browserCallbackUiHost)
            : this()
        {
            _browserCallbackUiHost = browserCallbackUiHost ?? throw new ArgumentNullException(nameof(browserCallbackUiHost));
        }

        internal SshExecutionService(bool enablePooling, SshTimeoutOptions? poolTimeouts, IBrowserCallbackUiHost browserCallbackUiHost)
            : this(enablePooling, poolTimeouts)
        {
            _browserCallbackUiHost = browserCallbackUiHost ?? throw new ArgumentNullException(nameof(browserCallbackUiHost));
        }

        internal SshExecutionService(SshConnectionPool sharedPool, IBrowserCallbackUiHost browserCallbackUiHost)
            : this(sharedPool)
        {
            _browserCallbackUiHost = browserCallbackUiHost ?? throw new ArgumentNullException(nameof(browserCallbackUiHost));
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
            var cancellationToken = BeginExecution();

            var timeouts = SshTimeoutOptions.FromSeconds(timeoutSeconds);

            try
            {
                foreach (var host in hosts)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!host.IsValid())
                        continue;

                    var result = await Task.Run(() =>
                        ExecuteSingleHost(host, commands, defaultUsername, defaultPassword, timeouts, cancellationToken));

                    results.Add(result);
                }
            }
            finally
            {
                EndExecution();
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
            var cancellationToken = BeginExecution();
            SshDebugLog(dummyHost, "SERVICE", "CancellationTokenSource created, _isRunning = true", sw);

            try
            {
                foreach (var host in hostList)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!host.IsValid())
                    {
                        SshDebugLog(host, "SERVICE", $"Skipping invalid host: {host.IpAddress}", sw);
                        continue;
                    }

                    SshDebugLog(host, "SERVICE", $"Starting Task.Run for ExecuteSingleHost on {host.IpAddress}:{host.Port}", sw);
                    var result = await Task.Run(() =>
                        ExecuteSingleHost(host, commands, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader));
                    SshDebugLog(host, "SERVICE", $"ExecuteSingleHost completed for {host.IpAddress}", sw);

                    results.Add(result);
                }
            }
            finally
            {
                EndExecution();
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
            bool showHeader = true,
            bool allowFileSelectionDialogs = true)
        {
            // Check if this is a YAML script
            if (preset.IsScript)
            {
                return await ExecuteScriptAsync(hosts, preset.Commands, defaultUsername, defaultPassword, timeouts, showHeader, allowFileSelectionDialogs);
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
            bool showHeader = true,
            bool allowFileSelectionDialogs = true)
        {
            var results = new List<ExecutionResult>();
            var cancellationToken = BeginExecution();
            var hostList = hosts.ToList();

            // Parse the script once
            var parser = new ScriptParser();
            Script script;
            try
            {
                script = parser.Parse(scriptText);
                var validationErrors = parser.Validate(script, scriptText, enforceCanonicalSyntax: true);
                if (validationErrors.Count > 0)
                {
                    throw new ScriptParseException("Script validation failed:\n" + string.Join("\n", validationErrors));
                }
            }
            catch (ScriptParseException ex)
            {
                // Emit the error so it appears in the output window
                var errorOutput = $"Script parse error: {ex.Message}\n";
                OnOutputReceived(hostList.FirstOrDefault() ?? new HostConnection(), errorOutput);

                // Return error result for all hosts
                foreach (var host in hostList)
                {
                    results.Add(new ExecutionResult
                    {
                        Host = host,
                        Success = false,
                        ErrorMessage = ex.Message,
                        Output = errorOutput,
                        Timestamp = DateTime.Now
                    });
                }
                EndExecution();
                return results;
            }

            try
            {
                if (TryBuildUnattendedLocalCmdPreflightMessage(script, allowFileSelectionDialogs, out var unattendedLocalCmdMessage))
                {
                    var errorOutput = $"Script preflight error: {unattendedLocalCmdMessage}\n";
                    OnOutputReceived(hostList.FirstOrDefault() ?? new HostConnection(), errorOutput);

                    foreach (var host in hostList)
                    {
                        results.Add(new ExecutionResult
                        {
                            Host = host,
                            Success = false,
                            ErrorMessage = unattendedLocalCmdMessage,
                            Output = errorOutput,
                            Timestamp = DateTime.Now
                        });
                    }

                    return results;
                }

                var analyzer = new ScriptDependencyAnalyzer();
                var sshRequirement = analyzer.AnalyzeSshRequirements(script);

                if (hostList.Count != 1 && TryBuildSingleHostOnlyPreflightMessage(sshRequirement, out var preflightMessage))
                {
                    var errorOutput = $"Script preflight error: {preflightMessage}\n";
                    OnOutputReceived(hostList.FirstOrDefault() ?? new HostConnection(), errorOutput);

                    foreach (var host in hostList)
                    {
                        results.Add(new ExecutionResult
                        {
                            Host = host,
                            Success = false,
                            ErrorMessage = preflightMessage,
                            Output = errorOutput,
                            Timestamp = DateTime.Now
                        });
                    }

                    return results;
                }

                foreach (var host in hostList)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var needsValidHost = sshRequirement.RequiresSshSession || sshRequirement.SftpUsesDefaultHost;
                    if (needsValidHost && !host.IsValid())
                        continue;

                    var result = await Task.Run(() =>
                        ExecuteScriptOnHost(host, script, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader, sshRequirement, allowFileSelectionDialogs));

                    results.Add(result);
                }
            }
            finally
            {
                EndExecution();
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
            IProgress<FolderExecutionProgress>? progress = null,
            bool allowFileSelectionDialogs = true)
        {
            var results = new List<ExecutionResult>();
            var cancellationToken = BeginExecution();

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var hostList = hosts.Where(h => h.IsValid()).ToList();
            var presetNames = options.SelectedPresets;
            var parallelHostCount = Math.Clamp(options.ParallelHostCount, 1, MaxParallelHosts);
            var runPresetsInParallel = options.RunPresetsInParallel && parallelHostCount == 1;
            int totalHosts = hostList.Count;
            int totalPresets = presetNames.Count;
            int totalOperations = totalHosts * totalPresets;
            int completedOperations = 0;
            int completedHosts = 0;
            var errorTracker = new StopOnFirstErrorTracker();
            var stopOnFirstErrorTriggered = 0;

            try
            {
                if (options.ParallelHostCount != parallelHostCount)
                {
                    OnOutputReceived(
                        hostList.FirstOrDefault() ?? new HostConnection(),
                        $"Parallel hosts value '{options.ParallelHostCount}' adjusted to {parallelHostCount}. Maximum supported value is {MaxParallelHosts}.\n");
                }

                if (options.RunPresetsInParallel && !runPresetsInParallel)
                {
                    OnOutputReceived(
                        hostList.FirstOrDefault() ?? new HostConnection(),
                        "Preset parallel mode is disabled when running multiple hosts in parallel. Falling back to sequential presets per host.\n");
                }

                var blockedPresetNames = FindSingleHostOnlyFolderPresets(presetNames, presets);
                if (blockedPresetNames.Count > 0)
                {
                    var preflightMessage = BuildFolderSingleHostOnlyPreflightMessage(blockedPresetNames);
                    var errorOutput = $"Script preflight error: {preflightMessage}\n";
                    OnOutputReceived(hostList.FirstOrDefault() ?? new HostConnection(), errorOutput);

                    foreach (var host in hostList)
                    {
                        results.Add(new ExecutionResult
                        {
                            Host = host,
                            Success = false,
                            ErrorMessage = preflightMessage,
                            Output = errorOutput,
                            Timestamp = DateTime.Now
                        });
                    }

                    return results;
                }

                // Process hosts in batches based on ParallelHostCount
                var hostBatches = hostList
                    .Select((host, index) => new { host, index })
                    .GroupBy(x => x.index / parallelHostCount)
                    .Select(g => g.Select(x => x.host).ToList())
                    .ToList();

                foreach (var batch in hostBatches)
                {
                    if (cancellationToken.IsCancellationRequested || (options.StopOnFirstError && errorTracker.HasError))
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
                        var hostResultLock = new object();

                        var outputBuilder = new StringBuilder();
                        int completedPresets = 0;
                        var hostCancellationObserved = 0;
                        bool isFirstPreset = true;

                        // Execute presets on this host
                        if (runPresetsInParallel)
                        {
                            // Parallel preset execution
                            var presetTasks = presetNames.Select(async presetName =>
                            {
                                if (cancellationToken.IsCancellationRequested || (options.StopOnFirstError && errorTracker.HasError))
                                    return;

                                if (!presets.TryGetValue(presetName, out var preset))
                                    return;

                                // Add preset separator
                                if (!options.SuppressPresetNames)
                                {
                                    var separator = $"\r\n═══ {presetName} ═══\r\n";
                                    lock (outputBuilder) { outputBuilder.Append(separator); }
                                    OnOutputReceived(host, separator);
                                }

                                var presetResult = await ExecutePresetOnHostAsync(
                                    host,
                                    preset,
                                    defaultUsername,
                                    defaultPassword,
                                    timeouts,
                                    cancellationToken,
                                    showHeader: false,
                                    allowFileSelectionDialogs: allowFileSelectionDialogs);

                                lock (outputBuilder) { outputBuilder.Append(presetResult.Output); }

                                if (presetResult.WasCancelled)
                                {
                                    Interlocked.Exchange(ref hostCancellationObserved, 1);
                                }

                                if (!presetResult.Success)
                                {
                                    lock (hostResultLock)
                                    {
                                        hostResult.Success = false;
                                        hostResult.ErrorMessage ??= presetResult.ErrorMessage;
                                    }

                                    if (options.StopOnFirstError && errorTracker.TrySignalError())
                                    {
                                        Interlocked.Exchange(ref stopOnFirstErrorTriggered, 1);
                                        _stopOnFirstErrorCancellationRequested = true;
                                        _cts?.Cancel();
                                    }

                                    // Mark failed preset in output
                                    if (!options.SuppressPresetNames)
                                    {
                                        var failMarker = $"\r\n═══ {presetName} [FAILED] ═══\r\n";
                                        lock (outputBuilder) { outputBuilder.Append(failMarker); }
                                        OnOutputReceived(host, failMarker);
                                    }
                                }
                                var hostCompletedPresets = Interlocked.Increment(ref completedPresets);
                                var operationCount = Interlocked.Increment(ref completedOperations);
                                progress?.Report(new FolderExecutionProgress
                                {
                                    CompletedOperations = operationCount,
                                    TotalOperations = totalOperations,
                                    CurrentHost = host.IpAddress,
                                    CurrentPreset = presetName,
                                    CompletedPresets = hostCompletedPresets,
                                    TotalPresets = totalPresets,
                                    CompletedHosts = completedHosts,
                                    TotalHosts = totalHosts
                                });
                            });

                            await Task.WhenAll(presetTasks);
                        }
                        else
                        {
                            // Sequential preset execution
                            foreach (var presetName in presetNames)
                            {
                                if (cancellationToken.IsCancellationRequested || (options.StopOnFirstError && errorTracker.HasError))
                                    break;

                                if (!presets.TryGetValue(presetName, out var preset))
                                    continue;

                                // Add preset separator
                                if (!options.SuppressPresetNames)
                                {
                                    var separator = $"\r\n═══ {presetName} ═══\r\n";
                                    outputBuilder.Append(separator);
                                    OnOutputReceived(host, separator);
                                }

                                var presetResult = await ExecutePresetOnHostAsync(
                                    host,
                                    preset,
                                    defaultUsername,
                                    defaultPassword,
                                    timeouts,
                                    cancellationToken,
                                    showHeader: isFirstPreset,
                                    allowFileSelectionDialogs: allowFileSelectionDialogs);

                                isFirstPreset = false;

                                outputBuilder.Append(presetResult.Output);

                                if (presetResult.WasCancelled)
                                {
                                    hostCancellationObserved = 1;
                                }

                                if (!presetResult.Success)
                                {
                                        hostResult.Success = false;
                                        hostResult.ErrorMessage = presetResult.ErrorMessage;
                                        if (options.StopOnFirstError)
                                        {
                                            if (errorTracker.TrySignalError())
                                            {
                                                Interlocked.Exchange(ref stopOnFirstErrorTriggered, 1);
                                                _stopOnFirstErrorCancellationRequested = true;
                                                _cts?.Cancel();
                                            }
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

                                completedPresets++;
                                var operationCount = Interlocked.Increment(ref completedOperations);
                                progress?.Report(new FolderExecutionProgress
                                {
                                    CompletedOperations = operationCount,
                                    TotalOperations = totalOperations,
                                    CurrentHost = host.IpAddress,
                                    CurrentPreset = presetName,
                                    CompletedPresets = completedPresets,
                                    TotalPresets = totalPresets,
                                    CompletedHosts = completedHosts,
                                    TotalHosts = totalHosts
                                });
                            }
                        }

                        var stoppedByUser = cancellationToken.IsCancellationRequested
                            && Volatile.Read(ref stopOnFirstErrorTriggered) == 0;
                        if (Volatile.Read(ref hostCancellationObserved) != 0 ||
                            (stoppedByUser && completedPresets < totalPresets))
                        {
                            hostResult.Success = false;
                            hostResult.WasCancelled = true;
                            hostResult.ErrorMessage ??= "Operation cancelled";
                        }

                        hostResult.Output = outputBuilder.ToString();
                        return hostResult;
                    });

                    var batchResults = await Task.WhenAll(batchTasks);
                    results.AddRange(batchResults);
                    completedHosts += batch.Count;
                }
            }
            finally
            {
                EndExecution();
            }

            return results;
        }

        private static List<string> FindSingleHostOnlyFolderPresets(
            IEnumerable<string> presetNames,
            IReadOnlyDictionary<string, PresetInfo> presets)
        {
            var parser = new ScriptParser();
            var analyzer = new ScriptDependencyAnalyzer();
            var blockedPresetNames = new List<string>();

            foreach (var presetName in presetNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!presets.TryGetValue(presetName, out var preset) || !preset.IsScript)
                    continue;

                Script script;
                try
                {
                    script = parser.Parse(preset.Commands);
                    var validationErrors = parser.Validate(script, preset.Commands, enforceCanonicalSyntax: true);
                    if (validationErrors.Count > 0)
                        continue;
                }
                catch (Exception)
                {
                    continue;
                }

                var requirement = analyzer.AnalyzeSshRequirements(script);
                if (requirement.UsesInteractive || requirement.UsesBrowserCallbackCapture)
                {
                    blockedPresetNames.Add(presetName);
                }
            }

                return blockedPresetNames;
            }

        private static bool TryBuildUnattendedLocalCmdPreflightMessage(
            Script script,
            bool allowFileSelectionDialogs,
            out string message)
        {
            if (allowFileSelectionDialogs ||
                !ContainsConfirmedLocalCmd(
                    script.Steps,
                    script.SubroutineRegistry,
                    currentSubroutine: null,
                    visitedSubroutines: new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                message = string.Empty;
                return false;
            }

            message = "LocalCmd confirmation is only available during manual main-window runs. For scheduler or other unattended runs, set localcmd.confirm: never.";
            return true;
        }

        private static bool ContainsConfirmedLocalCmd(
            List<ScriptStep>? steps,
            ScriptSubroutineRegistry? registry,
            ScriptSubroutineDefinition? currentSubroutine,
            HashSet<string> visitedSubroutines)
        {
            if (steps == null)
                return false;

            foreach (var step in steps)
            {
                var stepType = step.GetStepType();
                if (stepType == StepType.LocalCmd && RequiresLocalCmdConfirmation(step.LocalCmd))
                    return true;

                if (stepType == StepType.Call &&
                    step.Call != null &&
                    registry != null &&
                    registry.TryResolve(step.Call.Subroutine, currentSubroutine, out var definition) &&
                    definition != null &&
                    visitedSubroutines.Add(definition.QualifiedName) &&
                    ContainsConfirmedLocalCmd(
                        definition.Subroutine.Steps,
                        registry,
                        definition,
                        visitedSubroutines))
                {
                    return true;
                }

                if (ContainsConfirmedLocalCmd(step.Then, registry, currentSubroutine, visitedSubroutines) ||
                    ContainsConfirmedLocalCmd(step.Else, registry, currentSubroutine, visitedSubroutines) ||
                    ContainsConfirmedLocalCmd(step.Do, registry, currentSubroutine, visitedSubroutines) ||
                    ContainsConfirmedLocalCmd(step.Try, registry, currentSubroutine, visitedSubroutines) ||
                    ContainsConfirmedLocalCmd(step.Catch, registry, currentSubroutine, visitedSubroutines) ||
                    ContainsConfirmedLocalCmd(step.Finally, registry, currentSubroutine, visitedSubroutines))
                {
                    return true;
                }

                if (step.Elif != null)
                {
                    foreach (var branch in step.Elif)
                    {
                        if (ContainsConfirmedLocalCmd(branch.Then, registry, currentSubroutine, visitedSubroutines))
                            return true;
                    }
                }

                if (step.Cases != null)
                {
                    foreach (var switchCase in step.Cases)
                    {
                        if (ContainsConfirmedLocalCmd(switchCase.Do, registry, currentSubroutine, visitedSubroutines))
                            return true;
                    }
                }

                if (step.Parallel?.Steps != null &&
                    ContainsConfirmedLocalCmd(step.Parallel.Steps, registry, currentSubroutine, visitedSubroutines))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RequiresLocalCmdConfirmation(LocalCmdOptions? localCmd)
        {
            return localCmd != null &&
                   !string.Equals(localCmd.Confirm, "never", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFolderSingleHostOnlyPreflightMessage(IReadOnlyList<string> blockedPresetNames)
        {
            var baseMessage = BuildSingleHostOnlyBaseMessage(usesInteractive: true, usesBrowserCallback: true);
            if (blockedPresetNames.Count == 0)
                return baseMessage;

            var blockedPresets = string.Join(", ", blockedPresetNames.Select(name => $"'{name}'"));
            return $"{baseMessage} Blocked preset(s): {blockedPresets}.";
        }

        private static bool TryBuildSingleHostOnlyPreflightMessage(SshRequirementResult requirement, out string message)
        {
            if (!requirement.UsesInteractive && !requirement.UsesBrowserCallbackCapture)
            {
                message = string.Empty;
                return false;
            }

            message = BuildSingleHostOnlyBaseMessage(requirement.UsesInteractive, requirement.UsesBrowserCallbackCapture);
            return true;
        }

        private static string BuildSingleHostOnlyBaseMessage(bool usesInteractive, bool usesBrowserCallback)
        {
            var features = new List<string>(2);
            if (usesInteractive) features.Add("'interactive'");
            if (usesBrowserCallback) features.Add("'browser_callback_capture'");
            return $"Scripts using {string.Join(" or ", features)} are{SingleHostOnlyMessageSuffix}";
        }

        // Execute a preset without starting a new execution scope (caller owns BeginExecution/EndExecution).
        private Task<ExecutionResult> ExecutePresetOnHostAsync(
            HostConnection host,
            PresetInfo preset,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            CancellationToken cancellationToken,
            bool showHeader,
            bool allowFileSelectionDialogs)
        {
            return Task.Run(() =>
                ExecutePresetOnHost(host, preset, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader, allowFileSelectionDialogs));
        }

        private ExecutionResult ExecutePresetOnHost(
            HostConnection host,
            PresetInfo preset,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            CancellationToken cancellationToken,
            bool showHeader,
            bool allowFileSelectionDialogs)
        {
            if (preset.IsScript)
            {
                return ExecuteScriptTextOnHost(host, preset.Commands, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader, allowFileSelectionDialogs);
            }

            var commands = preset.Commands.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return ExecuteSingleHost(host, commands, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader);
        }

        private ExecutionResult ExecuteScriptTextOnHost(
            HostConnection host,
            string scriptText,
            string defaultUsername,
            string defaultPassword,
            SshTimeoutOptions timeouts,
            CancellationToken cancellationToken,
            bool showHeader,
            bool allowFileSelectionDialogs)
        {
            var parser = new ScriptParser();
            Script script;
            try
            {
                script = parser.Parse(scriptText);
                var validationErrors = parser.Validate(script, scriptText, enforceCanonicalSyntax: true);
                if (validationErrors.Count > 0)
                {
                    throw new ScriptParseException("Script validation failed:\n" + string.Join("\n", validationErrors));
                }
            }
            catch (ScriptParseException ex)
            {
                var errorOutput = $"Script parse error: {ex.Message}\n";
                OnOutputReceived(host, errorOutput);

                return new ExecutionResult
                {
                    Host = host,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Output = errorOutput,
                    Timestamp = DateTime.Now
                };
            }

            var analyzer = new ScriptDependencyAnalyzer();
            var sshRequirement = analyzer.AnalyzeSshRequirements(script);

            if (TryBuildUnattendedLocalCmdPreflightMessage(script, allowFileSelectionDialogs, out var unattendedLocalCmdMessage))
            {
                var errorOutput = $"Script preflight error: {unattendedLocalCmdMessage}\n";
                OnOutputReceived(host, errorOutput);

                return new ExecutionResult
                {
                    Host = host,
                    Success = false,
                    ErrorMessage = unattendedLocalCmdMessage,
                    Output = errorOutput,
                    Timestamp = DateTime.Now
                };
            }

            if (TryBuildSingleHostOnlyPreflightMessage(sshRequirement, out var preflightMessage))
            {
                var errorOutput = $"Script preflight error: {preflightMessage}\n";
                OnOutputReceived(host, errorOutput);

                return new ExecutionResult
                {
                    Host = host,
                    Success = false,
                    ErrorMessage = preflightMessage,
                    Output = errorOutput,
                    Timestamp = DateTime.Now
                };
            }

            return ExecuteScriptOnHost(host, script, defaultUsername, defaultPassword, timeouts, cancellationToken, showHeader, sshRequirement, allowFileSelectionDialogs);
        }

        /// <summary>
        /// Stops the current execution.
        /// </summary>
        public void Stop()
        {
            lock (_executionLock)
            {
                _cts?.Cancel();
            }
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
                result.WasCancelled = !_stopOnFirstErrorCancellationRequested;
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
            bool showHeader = true,
            SshRequirementResult? sshRequirement = null,
            bool allowFileSelectionDialogs = true)
        {
            var result = new ExecutionResult
            {
                Host = host,
                Timestamp = DateTime.Now
            };

            var outputBuilder = new StringBuilder();
            var interactiveSessions = new List<InteractiveTerminalSessionDetails>();
            string username = !string.IsNullOrWhiteSpace(host.Username) ? host.Username : defaultUsername;
            string password = !string.IsNullOrWhiteSpace(host.Password) ? host.Password : defaultPassword;
            var effectiveDebugMode = DebugMode || script.Debug;

            try
            {
                if (sshRequirement != null && !sshRequirement.RequiresSshSession)
                {
                    interactiveSessions = ExecuteScriptLocal(host, script, username, password, outputBuilder, cancellationToken, showHeader, allowFileSelectionDialogs);
                }
                else if (UseConnectionPooling && _connectionPool != null)
                {
                    interactiveSessions = ExecuteScriptWithPool(host, script, username, password, timeouts, outputBuilder, cancellationToken, showHeader, allowFileSelectionDialogs);
                }
                else
                {
                    interactiveSessions = ExecuteScriptWithoutPool(host, script, username, password, timeouts, outputBuilder, cancellationToken, showHeader, allowFileSelectionDialogs);
                }

                result.Success = true;
            }
            catch (SshException ex) when (IsAuthenticationError(ex))
            {
                result.Success = false;
                result.ErrorMessage = "Authentication failed";
                result.Exception = ex;
                var errorOutput = FormatError(
                    "AUTHENTICATION ERROR",
                    host,
                    ex,
                    includeDebugDetails: effectiveDebugMode,
                    compactErrors: script.CompactErrors);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (SshException ex) when (IsConnectionError(ex))
            {
                result.Success = false;
                result.ErrorMessage = "Connection failed";
                result.Exception = ex;
                var errorOutput = FormatError(
                    "CONNECTION ERROR",
                    host,
                    ex,
                    includeDebugDetails: effectiveDebugMode,
                    compactErrors: script.CompactErrors);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (SshException ex) when (IsTimeoutError(ex))
            {
                result.Success = false;
                result.ErrorMessage = "Operation timed out";
                result.Exception = ex;
                var errorOutput = FormatError(
                    "TIMEOUT ERROR",
                    host,
                    ex,
                    includeDebugDetails: effectiveDebugMode,
                    compactErrors: script.CompactErrors);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                result.Success = false;
                result.ErrorMessage = "Network error";
                result.Exception = ex;
                var errorOutput = FormatError(
                    "NETWORK ERROR",
                    host,
                    ex,
                    includeDebugDetails: effectiveDebugMode,
                    compactErrors: script.CompactErrors);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.WasCancelled = !_stopOnFirstErrorCancellationRequested;
                result.ErrorMessage = "Operation cancelled";
                var errorOutput = FormatError(
                    "CANCELLED",
                    host,
                    new Exception("Operation was cancelled by user"),
                    includeDebugDetails: effectiveDebugMode,
                    compactErrors: script.CompactErrors);
                outputBuilder.AppendLine(errorOutput);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Exception = ex;
                var errorOutput = FormatError(
                    "ERROR",
                    host,
                    ex,
                    includeDebugDetails: effectiveDebugMode,
                    compactErrors: script.CompactErrors);
                outputBuilder.AppendLine(errorOutput);
                OnOutputReceived(host, errorOutput + Environment.NewLine);
            }

            result.Output = outputBuilder.ToString();
            result.InteractiveSessions = interactiveSessions;
            return result;
        }

        /// <summary>
        /// Executes a script using connection pooling.
        /// </summary>
        /// <param name="showHeader">If false, suppresses the header output.</param>
        private List<InteractiveTerminalSessionDetails> ExecuteScriptWithPool(
            HostConnection host,
            Script script,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken,
            bool showHeader = true,
            bool allowFileSelectionDialogs = true)
        {
            var effectiveDebugMode = DebugMode || script.Debug;
            var (client, session) = _connectionPool!.CreateSessionAsync(host, username, password, timeouts, cancellationToken)
                .GetAwaiter().GetResult();

            try
            {
                OnProgressChanged(host, $"Connected to {host} (pooled, script mode)", false, true);
                session.DebugMode = effectiveDebugMode;
                session.CommandCompleted += (s, e) => OnCommandCompleted(host, e.Command);

                // Build header with script name (only if showHeader is true and script doesn't suppress it)
                if (showHeader && !script.NoBanner)
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
                context.DebugMode = effectiveDebugMode;
                context.AllowFileSelectionDialogs = allowFileSelectionDialogs;
                context.VaultService = VaultService;
                context.EnvironmentVaultProfile = EnvironmentVaultProfile;
                _activeScriptContext = context;
                ApplyConfiguredFlowCanvasDebugState(context);
                SeedConnectionVariables(context, host, username, password, timeouts);
                var previousOutputEndedWithLineTerminator = EndsWithLineTerminator(outputBuilder);

                // Wire up context output to our events
                context.OutputReceived += (s, e) =>
                {
                    var output = FormatScriptOutput(e.Message, e.Type);
                    var boundaryAdjusted = NormalizeScriptOutputBoundary(
                        output,
                        e.Type,
                        previousOutputEndedWithLineTerminator);
                    output = boundaryAdjusted.Output;
                    previousOutputEndedWithLineTerminator = boundaryAdjusted.EndsWithLineTerminator;
                    if (string.IsNullOrEmpty(output))
                        return;

                    outputBuilder.Append(output);
                    OnOutputReceived(host, output);
                };

                // Wire up column update requests
                context.ColumnUpdateRequested += (s, e) =>
                {
                    OnColumnUpdateRequested(host, e.ColumnName, e.Value);
                };

                context.EnvironmentUpdateRequested += (s, e) =>
                {
                    OnEnvironmentVariableUpdateRequested(host, e.Variable, e.Value);
                };

                // Execute the script
                var executor = new ScriptExecutor(_browserCallbackUiHost, _localCmdConfirmation);
                executor.StepStarting += (s, e) => StepStarting?.Invoke(this, e);
                executor.StepCompleted += (s, e) => StepCompleted?.Invoke(this, e);
                executor.DebugPauseStateChanged += (s, e) => DebugPauseStateChanged?.Invoke(this, e);
                var scriptResult = executor.ExecuteAsync(script, context, cancellationToken)
                    .GetAwaiter().GetResult();
                EnsureScriptSucceeded(scriptResult, cancellationToken);
                return context.GetInteractiveSessionsSnapshot();
            }
            finally
            {
                session.Dispose();
                _connectionPool!.ReleaseSession(host, username);
            }
        }

        /// <summary>
        /// Executes a script without connection pooling.
        /// </summary>
        /// <param name="showHeader">If false, suppresses the header output.</param>
        private List<InteractiveTerminalSessionDetails> ExecuteScriptWithoutPool(
            HostConnection host,
            Script script,
            string username,
            string password,
            SshTimeoutOptions timeouts,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken,
            bool showHeader = true,
            bool allowFileSelectionDialogs = true)
        {
            var effectiveDebugMode = DebugMode || script.Debug;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SshDebugLog(host, "SCRIPT", $"ExecuteScriptWithoutPool entered for {host.IpAddress}:{host.Port}", debugEnabledOverride: effectiveDebugMode);

            using var client = CreateConnectedClientWithFallback(host, timeouts, sw, "SCRIPT", effectiveDebugMode);

            SshDebugLog(host, "SCRIPT", "Calling client.Login()", sw, effectiveDebugMode);

            // SSH agent, key-based, or password authentication
            if (!TryLoginWithAgent(client, username, host, sw))
            {
                if (!string.IsNullOrEmpty(host.IdentityFile) && File.Exists(host.IdentityFile))
                {
                    SshDebugLog(host, "SCRIPT", $"Using key-based auth with: {host.IdentityFile}", sw, effectiveDebugMode);
                    var passphrase = host.IdentityFilePassphrase ?? string.Empty;
                    client.Login(username, new SshPrivateKey(host.IdentityFile, passphrase));
                }
                else
                {
                    client.Login(username, password);
                }
            }

            SshDebugLog(host, "SCRIPT", "client.Login() completed", sw, effectiveDebugMode);

            OnProgressChanged(host, $"Connected to {host} (script mode)", false, true);

            SshDebugLog(host, "SCRIPT", "Starting scripting session", sw, effectiveDebugMode);
            var terminalOptions = SshTerminalOptionsFactory.Create();
            var (scripting, terminal) = SshTerminalOptionsFactory.CreateScriptingWithHistory(
                client,
                terminalOptions,
                SshTerminalOptionsFactory.DefaultColumns,
                SshTerminalOptionsFactory.DefaultRows,
                SshTerminalOptionsFactory.DefaultHistoryMaxLength);
            scripting.Timeout = (int)timeouts.CommandTimeout.TotalMilliseconds;
            SshDebugLog(host, "SCRIPT", "Scripting session created", sw, effectiveDebugMode);

            using var session = new SshShellSession(client, scripting, timeouts, terminal);
            session.DebugMode = effectiveDebugMode;
            session.CommandCompleted += (s, e) => OnCommandCompleted(host, e.Command);

            // Subscribe to session debug output so we can see banner detection, prompt detection, etc.
            session.DebugOutput += (s, e) =>
            {
                outputBuilder.Append(e.Output);
                OnOutputReceived(host, e.Output);
            };

            // Initialize session (detect prompt)
            SshDebugLog(host, "SCRIPT", "Calling session.InitializeAsync - waiting for prompt", sw, effectiveDebugMode);
            try
            {
                var banner = session.InitializeAsync(cancellationToken).GetAwaiter().GetResult();
                SshDebugLog(host, "SCRIPT", $"session.InitializeAsync completed. Prompt: {session.CurrentPrompt}", sw, effectiveDebugMode);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Provide more context about why the session might have failed
                SshDebugLog(host, "SCRIPT", $"SESSION INIT FAILED during InitializeAsync: {ex.Message}", sw, effectiveDebugMode);
                SshDebugLog(host, "SCRIPT", $"Client.IsConnected: {client.IsConnected}", sw, effectiveDebugMode);
                throw;
            }

            // Build header with script name (only if showHeader is true and script doesn't suppress it)
            if (showHeader && !script.NoBanner)
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
            context.DebugMode = effectiveDebugMode;
            context.AllowFileSelectionDialogs = allowFileSelectionDialogs;
            context.VaultService = VaultService;
            context.EnvironmentVaultProfile = EnvironmentVaultProfile;
            _activeScriptContext = context;
            ApplyConfiguredFlowCanvasDebugState(context);
            SeedConnectionVariables(context, host, username, password, timeouts);
            var previousOutputEndedWithLineTerminator = EndsWithLineTerminator(outputBuilder);

            // Wire up context output to our events
            context.OutputReceived += (s, e) =>
            {
                var output = FormatScriptOutput(e.Message, e.Type);
                var boundaryAdjusted = NormalizeScriptOutputBoundary(
                    output,
                    e.Type,
                    previousOutputEndedWithLineTerminator);
                output = boundaryAdjusted.Output;
                previousOutputEndedWithLineTerminator = boundaryAdjusted.EndsWithLineTerminator;
                if (string.IsNullOrEmpty(output))
                    return;

                outputBuilder.Append(output);
                OnOutputReceived(host, output);
            };

            // Wire up column update requests
            context.ColumnUpdateRequested += (s, e) =>
            {
                OnColumnUpdateRequested(host, e.ColumnName, e.Value);
            };

            context.EnvironmentUpdateRequested += (s, e) =>
            {
                OnEnvironmentVariableUpdateRequested(host, e.Variable, e.Value);
            };

            // Execute the script
            var executor = new ScriptExecutor(_browserCallbackUiHost, _localCmdConfirmation);
            executor.StepStarting += (s, e) => StepStarting?.Invoke(this, e);
            executor.StepCompleted += (s, e) => StepCompleted?.Invoke(this, e);
            executor.DebugPauseStateChanged += (s, e) => DebugPauseStateChanged?.Invoke(this, e);
            var scriptResult = executor.ExecuteAsync(script, context, cancellationToken)
                .GetAwaiter().GetResult();
            EnsureScriptSucceeded(scriptResult, cancellationToken);

            client.Disconnect();
            return context.GetInteractiveSessionsSnapshot();
        }

        private List<InteractiveTerminalSessionDetails> ExecuteScriptLocal(
            HostConnection host,
            Script script,
            string username,
            string password,
            StringBuilder outputBuilder,
            CancellationToken cancellationToken,
            bool showHeader = true,
            bool allowFileSelectionDialogs = true)
        {
            var effectiveDebugMode = DebugMode || script.Debug;
            OnProgressChanged(host, $"Running locally for {host} (no SSH required)", false, false);

            if (showHeader && !script.NoBanner)
            {
                var scriptName = !string.IsNullOrEmpty(script.Name) ? $" {script.Name}" : string.Empty;
                string header = $"{new string('#', 20)} LOCAL SCRIPT: {host}{scriptName} {new string('#', 20)}";
                string separator = new string('#', header.Length);

                outputBuilder.AppendLine(separator);
                outputBuilder.AppendLine(header);
                outputBuilder.AppendLine(separator);

                OnOutputReceived(host, outputBuilder.ToString());
            }

            var context = new ScriptContext(host.Variables);
            context.Session = null;
            context.DebugMode = effectiveDebugMode;
            context.AllowFileSelectionDialogs = allowFileSelectionDialogs;
            context.VaultService = VaultService;
            context.EnvironmentVaultProfile = EnvironmentVaultProfile;
            _activeScriptContext = context;
            ApplyConfiguredFlowCanvasDebugState(context);
            SeedConnectionVariables(context, host, username, password, SshTimeoutOptions.Default);
            var previousOutputEndedWithLineTerminator = EndsWithLineTerminator(outputBuilder);

            context.OutputReceived += (s, e) =>
            {
                var output = FormatScriptOutput(e.Message, e.Type);
                var boundaryAdjusted = NormalizeScriptOutputBoundary(
                    output,
                    e.Type,
                    previousOutputEndedWithLineTerminator);
                output = boundaryAdjusted.Output;
                previousOutputEndedWithLineTerminator = boundaryAdjusted.EndsWithLineTerminator;
                if (string.IsNullOrEmpty(output))
                    return;

                outputBuilder.Append(output);
                OnOutputReceived(host, output);
            };

            context.ColumnUpdateRequested += (s, e) =>
            {
                OnColumnUpdateRequested(host, e.ColumnName, e.Value);
            };

            context.EnvironmentUpdateRequested += (s, e) =>
            {
                OnEnvironmentVariableUpdateRequested(host, e.Variable, e.Value);
            };

            var executor = new ScriptExecutor(_browserCallbackUiHost, _localCmdConfirmation);
            executor.StepStarting += (s, e) => StepStarting?.Invoke(this, e);
            executor.StepCompleted += (s, e) => StepCompleted?.Invoke(this, e);
            executor.DebugPauseStateChanged += (s, e) => DebugPauseStateChanged?.Invoke(this, e);
            var scriptResult = executor.ExecuteAsync(script, context, cancellationToken)
                .GetAwaiter().GetResult();
            EnsureScriptSucceeded(scriptResult, cancellationToken);
            return context.GetInteractiveSessionsSnapshot();
        }

        private static void EnsureScriptSucceeded(ScriptResult scriptResult, CancellationToken cancellationToken)
        {
            switch (scriptResult.Status)
            {
                case ScriptExitStatus.Success:
                    return;

                case ScriptExitStatus.Cancelled:
                    throw new OperationCanceledException(
                        string.IsNullOrWhiteSpace(scriptResult.Message) ? "Script cancelled" : scriptResult.Message,
                        cancellationToken);

                case ScriptExitStatus.Error:
                    throw scriptResult.Exception ?? new InvalidOperationException(
                        string.IsNullOrWhiteSpace(scriptResult.Message) ? "Script error" : scriptResult.Message);

                case ScriptExitStatus.Failure:
                default:
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(scriptResult.Message) ? "Script failed" : scriptResult.Message);
            }
        }

        private static void SeedConnectionVariables(
            ScriptContext context,
            HostConnection host,
            string username,
            string password,
            SshTimeoutOptions? timeouts)
        {
            context.CurrentHost = host;
            context.ResolvedUsername = username;
            context.ResolvedPassword = password;
            context.Timeouts = timeouts;

            // Keep runtime context aligned with the credentials/endpoints actually used for SSH.
            if (string.IsNullOrWhiteSpace(context.GetVariableString("Host_IP")))
                context.SetVariable("Host_IP", host.ToString());

            if (!string.IsNullOrWhiteSpace(username))
                context.SetVariable("username", username);

            if (!string.IsNullOrWhiteSpace(password))
                context.SetVariable("password", password);
        }

        private void ApplyConfiguredFlowCanvasDebugState(ScriptContext context)
        {
            FlowCanvasDebugBootstrapState? bootstrapState;
            lock (_flowCanvasDebugBootstrapLock)
            {
                bootstrapState = _pendingFlowCanvasDebugBootstrapState;
            }

            if (bootstrapState == null)
                return;

            var debugState = context.DebugState;
            debugState.SetNodeToStepPathMap(new Dictionary<string, string>(
                bootstrapState.NodeToStepPathMap,
                StringComparer.Ordinal));

            foreach (var nodeId in bootstrapState.BreakpointNodeIds)
            {
                if (!debugState.HasNodeBreakpoint(nodeId))
                    debugState.ToggleNodeBreakpoint(nodeId);
            }

            foreach (var nodeId in bootstrapState.DisabledNodeIds)
            {
                if (!debugState.IsNodeDisabled(nodeId))
                    debugState.ToggleNodeDisabled(nodeId);
            }
        }

        private sealed class FlowCanvasDebugBootstrapState
        {
            public IReadOnlyDictionary<string, string> NodeToStepPathMap { get; }
            public IReadOnlyCollection<string> BreakpointNodeIds { get; }
            public IReadOnlyCollection<string> DisabledNodeIds { get; }

            public FlowCanvasDebugBootstrapState(
                IReadOnlyDictionary<string, string> nodeToStepPathMap,
                IReadOnlyCollection<string> breakpointNodeIds,
                IReadOnlyCollection<string> disabledNodeIds)
            {
                NodeToStepPathMap = nodeToStepPathMap;
                BreakpointNodeIds = breakpointNodeIds;
                DisabledNodeIds = disabledNodeIds;
            }
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
                session.CommandCompleted += (s, e) => OnCommandCompleted(host, e.Command);

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
                _connectionPool!.ReleaseSession(host, username);
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
            if (DebugMode)
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

            using var client = CreateConnectedClientWithFallback(host, timeouts, sw, "SSH");

            SshDebugLog(host, "SSH", "Calling client.Login()", sw);

            // SSH agent, key-based, or password authentication
            if (!TryLoginWithAgent(client, username, host, sw))
            {
                if (!string.IsNullOrEmpty(host.IdentityFile) && File.Exists(host.IdentityFile))
                {
                    // Use key-based authentication
                    SshDebugLog(host, "SSH", $"Using key-based auth with: {host.IdentityFile}", sw);
                    var passphrase = host.IdentityFilePassphrase ?? string.Empty;
                    client.Login(username, new SshPrivateKey(host.IdentityFile, passphrase));
                }
                else
                {
                    // Use password authentication
                    client.Login(username, password);
                }
            }
            SshDebugLog(host, "SSH", "client.Login() completed - SSH session established", sw);

            OnProgressChanged(host, $"Connected to {host}", false, true);

            SshDebugLog(host, "SSH", "Starting scripting session", sw);
            var terminalOptions = SshTerminalOptionsFactory.Create();
            var (scripting, terminal) = SshTerminalOptionsFactory.CreateScriptingWithHistory(
                client,
                terminalOptions,
                SshTerminalOptionsFactory.DefaultColumns,
                SshTerminalOptionsFactory.DefaultRows,
                SshTerminalOptionsFactory.DefaultHistoryMaxLength);
            scripting.Timeout = (int)timeouts.CommandTimeout.TotalMilliseconds;
            SshDebugLog(host, "SSH", "Scripting session created", sw);

            SshDebugLog(host, "SSH", "Creating SshShellSession", sw);
            using var session = new SshShellSession(client, scripting, timeouts, terminal);
            SshDebugLog(host, "SSH", "SshShellSession created", sw);

            // Configure debug mode BEFORE subscribing to events
            session.DebugMode = DebugMode;
            session.CommandCompleted += (s, e) => OnCommandCompleted(host, e.Command);

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

            // Flush any residual data left in the scripting buffer after prompt detection
            session.FlushBuffer();

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
        /// Tests TCP reachability of a host by opening and closing a TCP connection.
        /// Uses a lightweight port check rather than SSH auth since hosts may not be SSH servers.
        /// </summary>
        public async Task<Models.ConnectionTestResult> TestConnectionAsync(
            Models.HostConnection host, int timeoutMs, CancellationToken ct)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                var connectTask = tcpClient.ConnectAsync(host.IpAddress, host.Port);
                var timeoutTask = Task.Delay(timeoutMs, ct);

                var completed = await Task.WhenAny(connectTask, timeoutTask);
                if (completed == timeoutTask)
                {
                    ct.ThrowIfCancellationRequested();
                    return new Models.ConnectionTestResult(false, "Timeout", $"Connection timed out after {timeoutMs}ms", sw.ElapsedMilliseconds);
                }

                // Await to propagate any connection exception
                await connectTask;

                sw.Stop();
                return new Models.ConnectionTestResult(true, null, null, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException)
            {
                return new Models.ConnectionTestResult(false, "Cancelled", "Operation cancelled", sw.ElapsedMilliseconds);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                return new Models.ConnectionTestResult(false, "Network", ex.Message, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return new Models.ConnectionTestResult(false, "Unknown", ex.Message, sw.ElapsedMilliseconds);
            }
        }

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

        private Ssh CreateConnectedClientWithFallback(
            HostConnection host,
            SshTimeoutOptions timeouts,
            System.Diagnostics.Stopwatch? sw,
            string phase,
            bool? debugEnabledOverride = null)
        {
            Ssh CreateConfiguredClient(Action<Ssh>? additionalAlgorithmConfig = null)
            {
                var sshClient = new Ssh();
                sshClient.Timeout = (int)timeouts.ConnectionTimeout.TotalMilliseconds;
                ApplyAlgorithmSettings(sshClient, host);
                additionalAlgorithmConfig?.Invoke(sshClient);
                return sshClient;
            }

            SshDebugLog(host, phase, $"Ssh client created. Timeout: {timeouts.ConnectionTimeout.TotalSeconds}s", sw, debugEnabledOverride);

            // Check if we already know which algorithm tier works for this host
            var cacheKey = $"{host.IpAddress}:{host.Port}";
            var canUseFallbackCache = host.HostKeyAlgorithms is not { Length: > 0 };
            if (canUseFallbackCache && HostAlgorithmCache.TryGetValue(cacheKey, out var cachedTier) && cachedTier != HostKeyAlgorithmTier.Default)
            {
                SshDebugLog(host, phase, $"Using cached algorithm tier: {cachedTier}", sw, debugEnabledOverride);
                var cachedClient = CreateConfiguredClient(ssh => ApplyAlgorithmTier(ssh, cachedTier, host, phase, sw, debugEnabledOverride));
                try
                {
                    var cachedSw = System.Diagnostics.Stopwatch.StartNew();
                    cachedClient.Connect(host.IpAddress, host.Port);
                    cachedSw.Stop();
                    SshDebugLog(host, phase, $"client.Connect() completed in {cachedSw.ElapsedMilliseconds}ms (cached {cachedTier})", sw, debugEnabledOverride);
                    return cachedClient;
                }
                catch (Exception)
                {
                    cachedClient.Dispose();
                    // Cache miss — evict and fall through to full discovery
                    HostAlgorithmCache.TryRemove(cacheKey, out _);
                    SshDebugLog(host, phase, "Cached algorithm tier failed, falling back to full discovery", sw, debugEnabledOverride);
                }
            }

            var client = CreateConfiguredClient();
            try
            {
                SshDebugLog(host, phase, "Calling client.Connect()", sw, debugEnabledOverride);
                var connectSw = System.Diagnostics.Stopwatch.StartNew();
                client.Connect(host.IpAddress, host.Port);
                connectSw.Stop();
                SshDebugLog(host, phase, $"client.Connect() completed in {connectSw.ElapsedMilliseconds}ms", sw, debugEnabledOverride);
                if (canUseFallbackCache)
                    HostAlgorithmCache[cacheKey] = HostKeyAlgorithmTier.Default;
                return client;
            }
            catch (Exception ex) when (ShouldRetryWithAlgorithmFallback(host, ex))
            {
                client.Dispose();

                SshDebugLog(
                    host,
                    phase,
                    "Negotiation failed due to unsupported key algorithm. Retrying with non-RSA host key algorithms (ed25519/ECDSA).",
                    sw,
                    debugEnabledOverride);

                var retryClient = CreateConfiguredClient(ssh =>
                    ssh.Settings.SshParameters.SetHostKeyAlgorithms(NonRsaHostKeyFallbackAlgorithms));

                try
                {
                    SshDebugLog(
                        host,
                        phase,
                        $"Fallback host key algorithms: {string.Join(", ", NonRsaHostKeyFallbackAlgorithms)}",
                        sw,
                        debugEnabledOverride);

                    SshDebugLog(host, phase, "Calling client.Connect() (non-RSA fallback)", sw, debugEnabledOverride);
                    var retrySw = System.Diagnostics.Stopwatch.StartNew();
                    retryClient.Connect(host.IpAddress, host.Port);
                    retrySw.Stop();
                    SshDebugLog(host, phase, $"client.Connect() completed in {retrySw.ElapsedMilliseconds}ms (non-RSA fallback)", sw, debugEnabledOverride);
                    HostAlgorithmCache[cacheKey] = HostKeyAlgorithmTier.NonRsa;
                    return retryClient;
                }
                catch (Exception ex2) when (ShouldRetryWithAlgorithmFallback(host, ex2))
                {
                    retryClient.Dispose();

                    SshDebugLog(
                        host,
                        phase,
                        "Non-RSA retry still failed. Retrying with ed25519-only host key + conservative ciphers.",
                        sw,
                        debugEnabledOverride);

                    var ed25519OnlyClient = CreateConfiguredClient(ssh =>
                    {
                        var parameters = ssh.Settings.SshParameters;
                        parameters.SetHostKeyAlgorithms(Ed25519OnlyHostKeyAlgorithms);
                        parameters.SetEncryptionAlgorithms(ConservativeEncryptionFallbackAlgorithms);

                        var macApplied = TrySetSshParameterAlgorithms(parameters, "SetMacAlgorithms", ConservativeMacFallbackAlgorithms);

                        if (DebugMode || (debugEnabledOverride ?? false))
                        {
                            var macStatus = macApplied ? "applied" : "not supported by this Rebex version";
                            SshDebugLog(host, phase, $"Ed25519-only fallback MAC override: {macStatus}", sw, debugEnabledOverride);
                        }
                    });

                    SshDebugLog(
                        host,
                        phase,
                        $"Ed25519-only fallback host key algorithms: {string.Join(", ", Ed25519OnlyHostKeyAlgorithms)}",
                        sw,
                        debugEnabledOverride);

                    SshDebugLog(host, phase, "Calling client.Connect() (ed25519-only fallback)", sw, debugEnabledOverride);
                    var ed25519Sw = System.Diagnostics.Stopwatch.StartNew();
                    ed25519OnlyClient.Connect(host.IpAddress, host.Port);
                    ed25519Sw.Stop();
                    SshDebugLog(host, phase, $"client.Connect() completed in {ed25519Sw.ElapsedMilliseconds}ms (ed25519-only fallback)", sw, debugEnabledOverride);
                    HostAlgorithmCache[cacheKey] = HostKeyAlgorithmTier.Ed25519Only;

                    return ed25519OnlyClient;
                }
            }
        }

        /// <summary>
        /// Applies the algorithm configuration for a given tier to an SSH client.
        /// Used when replaying a cached algorithm tier.
        /// </summary>
        private void ApplyAlgorithmTier(Ssh ssh, HostKeyAlgorithmTier tier, HostConnection host, string phase,
            System.Diagnostics.Stopwatch? sw, bool? debugEnabledOverride)
        {
            switch (tier)
            {
                case HostKeyAlgorithmTier.NonRsa:
                    ssh.Settings.SshParameters.SetHostKeyAlgorithms(NonRsaHostKeyFallbackAlgorithms);
                    break;
                case HostKeyAlgorithmTier.Ed25519Only:
                    var parameters = ssh.Settings.SshParameters;
                    parameters.SetHostKeyAlgorithms(Ed25519OnlyHostKeyAlgorithms);
                    parameters.SetEncryptionAlgorithms(ConservativeEncryptionFallbackAlgorithms);
                    TrySetSshParameterAlgorithms(parameters, "SetMacAlgorithms", ConservativeMacFallbackAlgorithms);
                    break;
            }
        }

        private static bool ShouldRetryWithAlgorithmFallback(HostConnection host, Exception ex)
        {
            // Respect explicit user/ssh-config host key settings and only auto-fallback when none were configured.
            if (host.HostKeyAlgorithms is { Length: > 0 })
                return false;

            return HasUnsupportedKeyAlgorithmError(ex);
        }

        private static bool TrySetSshParameterAlgorithms(object sshParameters, string methodName, string[] algorithms)
        {
            var method = sshParameters
                .GetType()
                .GetMethod(methodName, new[] { typeof(string[]) });

            if (method == null)
                return false;

            method.Invoke(sshParameters, new object[] { algorithms });
            return true;
        }

        private static bool HasUnsupportedKeyAlgorithmError(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is CryptographicException cryptographicException &&
                    cryptographicException.Message.Contains("key algorithm is not supported", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (current.Message.Contains("key algorithm is not supported", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string FormatError(
            string errorType,
            HostConnection host,
            Exception ex,
            bool includeDebugDetails = false,
            bool compactErrors = false)
        {
            if (compactErrors)
            {
                return FormatCompactError(errorType, host, ex);
            }

            var sb = new StringBuilder();
            string title = $"{new string('#', 20)} {errorType}: {host} {new string('#', 20)}";
            string separator = new string('#', title.Length);

            sb.AppendLine(separator);
            sb.AppendLine(title);
            sb.AppendLine(separator);

            string? lastMessage = null;
            for (var e = ex; e != null; e = e.InnerException)
            {
                // Clean up unhelpful Rebex library messages
                var message = e.Message.Replace(" Make sure you are connecting to an SSH server.", "");

                // Skip duplicate messages in the exception chain
                if (message != lastMessage)
                {
                    sb.AppendLine($"{e.GetType().Name}: {message}");
                    lastMessage = message;
                }
            }

            if (DebugMode || includeDebugDetails)
            {
                var sshException = FindSshException(ex);
                if (sshException != null)
                {
                    AppendSshNegotiationDiagnostics(sb, host, sshException);
                }
            }

            return sb.ToString();
        }

        private static string FormatCompactError(string errorType, HostConnection host, Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var message = current.Message.Replace(" Make sure you are connecting to an SSH server.", "");
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return $"{errorType}: {host}: {current.GetType().Name}: {message}";
                }
            }

            return $"{errorType}: {host}: {ex.GetType().Name}: {ex.Message}";
        }

        private static SshException? FindSshException(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is SshException sshException)
                    return sshException;
            }

            return null;
        }

        private static void AppendSshNegotiationDiagnostics(StringBuilder sb, HostConnection host, SshException sshException)
        {
            sb.AppendLine();
            sb.AppendLine("SSH negotiation details (debug):");

            try
            {
                var serverInfo = sshException.GetServerInfo();
                if (serverInfo == null)
                {
                    sb.AppendLine("Server negotiation details: (not available from SSH library for this failure)");
                    AppendAlgorithmsLine(sb, "Configured host key algorithms", host.HostKeyAlgorithms);
                    AppendAlgorithmsLine(sb, "Configured ciphers", host.Ciphers);
                    AppendSupportedClientAlgorithms(sb);
                    return;
                }

                AppendAlgorithmsLine(sb, "Configured host key algorithms", host.HostKeyAlgorithms);
                AppendAlgorithmsLine(sb, "Configured ciphers", host.Ciphers);
                AppendAlgorithmsLine(sb, "Server host key algorithms", serverInfo.ServerHostKeyAlgorithms);
                AppendAlgorithmsLine(sb, "Server key exchange algorithms", serverInfo.KeyExchangeAlgorithms);
                AppendAlgorithmsLine(sb, "Server encryption algorithms (client->server)", serverInfo.EncryptionAlgorithmsClientToServer);
                AppendAlgorithmsLine(sb, "Server encryption algorithms (server->client)", serverInfo.EncryptionAlgorithmsServerToClient);
                AppendAlgorithmsLine(sb, "Server MAC algorithms (client->server)", serverInfo.MacAlgorithmsClientToServer);
                AppendAlgorithmsLine(sb, "Server MAC algorithms (server->client)", serverInfo.MacAlgorithmsServerToClient);
                AppendSupportedClientAlgorithms(sb);
            }
            catch (Exception debugEx)
            {
                sb.AppendLine($"SSH negotiation details unavailable (debug): {debugEx.Message}");
                AppendAlgorithmsLine(sb, "Configured host key algorithms", host.HostKeyAlgorithms);
                AppendAlgorithmsLine(sb, "Configured ciphers", host.Ciphers);
                AppendSupportedClientAlgorithms(sb);
            }
        }

        private static void AppendSupportedClientAlgorithms(StringBuilder sb)
        {
            AppendAlgorithmsLine(sb, "Client supported host key algorithms", SshParameters.GetSupportedHostKeyAlgorithms());
            AppendAlgorithmsLine(sb, "Client supported key exchange algorithms", SshParameters.GetSupportedKeyExchangeAlgorithms());
            AppendAlgorithmsLine(sb, "Client supported encryption algorithms", SshParameters.GetSupportedEncryptionAlgorithms());
            AppendAlgorithmsLine(sb, "Client supported MAC algorithms", SshParameters.GetSupportedMacAlgorithms());
        }

        private static void AppendAlgorithmsLine(StringBuilder sb, string label, IEnumerable<string>? algorithms)
        {
            var values = algorithms?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var formatted = values is { Length: > 0 }
                ? string.Join(", ", values)
                : "(none reported)";

            sb.AppendLine($"{label}: {formatted}");
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

        internal static string EnsureTrailingNewLine(string message)
        {
            if (string.IsNullOrEmpty(message))
                return Environment.NewLine;

            if (message.EndsWith("\r\n", StringComparison.Ordinal) ||
                message.EndsWith("\n", StringComparison.Ordinal) ||
                message.EndsWith("\r", StringComparison.Ordinal))
            {
                return message;
            }

            return message + Environment.NewLine;
        }

        internal static string FormatScriptOutput(string message, ScriptOutputType outputType)
        {
            if (outputType == ScriptOutputType.RawChunk)
            {
                return message ?? string.Empty;
            }

            return EnsureTrailingNewLine(message);
        }

        internal static (string Output, bool EndsWithLineTerminator) NormalizeScriptOutputBoundary(
            string output,
            ScriptOutputType outputType,
            bool previousOutputEndedWithLineTerminator)
        {
            if (string.IsNullOrEmpty(output))
                return (string.Empty, previousOutputEndedWithLineTerminator);

            var normalized = output;
            if (outputType != ScriptOutputType.RawChunk &&
                !previousOutputEndedWithLineTerminator &&
                !StartsWithLineTerminator(normalized))
            {
                normalized = Environment.NewLine + normalized;
            }

            return (normalized, EndsWithLineTerminator(normalized));
        }

        internal static bool StartsWithLineTerminator(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            var first = value[0];
            return first == '\r' || first == '\n';
        }

        internal static bool EndsWithLineTerminator(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            var last = value[^1];
            return last == '\r' || last == '\n';
        }

        internal static bool EndsWithLineTerminator(StringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (builder.Length == 0)
                return true;

            var last = builder[builder.Length - 1];
            return last == '\r' || last == '\n';
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

        protected virtual void OnEnvironmentVariableUpdateRequested(HostConnection host, string variable, string value)
        {
            EnvironmentVariableUpdateRequested?.Invoke(this, new SshEnvironmentVariableUpdateEventArgs
            {
                Host = host,
                Variable = variable,
                Value = value
            });
        }

        protected virtual void OnCommandCompleted(HostConnection host, string command)
        {
            CommandCompleted?.Invoke(this, new SshCommandCompletedEventArgs
            {
                Host = host,
                Command = command
            });
        }

        protected virtual void OnExecutionCompleted()
        {
            ExecutionCompleted?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Emits SSH debug timing information when DebugMode is enabled.
        /// </summary>
        private void SshDebugLog(HostConnection host, string phase, string message, System.Diagnostics.Stopwatch? sw = null, bool? debugEnabledOverride = null)
        {
            var debugEnabled = debugEnabledOverride ?? DebugMode;
            if (!debugEnabled) return;
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var elapsed = sw != null ? $" (+{sw.ElapsedMilliseconds}ms)" : "";
            var output = $"[DEBUG {timestamp}]{elapsed} {phase}: {message}\r\n";
            OnOutputReceived(host, output);
        }

        /// <summary>
        /// Applies SSH algorithm preferences from the host connection settings.
        /// These settings typically come from the SSH config file.
        /// </summary>
        private static void ApplyAlgorithmSettings(Ssh client, HostConnection host)
        {
            // Apply host key algorithms if specified
            // Rebex accepts OpenSSH-style algorithm IDs directly
            if (host.HostKeyAlgorithms?.Length > 0)
            {
                client.Settings.SshParameters.SetHostKeyAlgorithms(host.HostKeyAlgorithms);
            }

            // Apply encryption ciphers if specified
            // Rebex accepts OpenSSH-style cipher IDs directly
            if (host.Ciphers?.Length > 0)
            {
                client.Settings.SshParameters.SetEncryptionAlgorithms(host.Ciphers);
            }
        }

        private bool TryLoginWithAgent(Ssh client, string username, HostConnection host, System.Diagnostics.Stopwatch? sw = null)
        {
            if (!PreferSshAgent)
                return false;

            if (!IsSshAgentAvailable())
            {
                SshDebugLog(host, "SSH", "SSH agent not available; falling back to key/password.", sw);
                return false;
            }

            // The current SSH library does not expose agent-backed authentication APIs.
            SshDebugLog(host, "SSH", "SSH agent detected but not supported by current SSH library; falling back to key/password.", sw);
            return false;
        }

        private static bool IsSshAgentAvailable()
        {
            var sock = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
            if (!string.IsNullOrWhiteSpace(sock))
                return true;

            var pid = Environment.GetEnvironmentVariable("SSH_AGENT_PID");
            return !string.IsNullOrWhiteSpace(pid);
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
