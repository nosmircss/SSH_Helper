using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Event arguments for script output events.
    /// </summary>
    public class ScriptOutputEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public ScriptOutputType Type { get; set; } = ScriptOutputType.Info;
    }

    /// <summary>
    /// Event arguments for column update requests from scripts.
    /// </summary>
    public class ColumnUpdateEventArgs : EventArgs
    {
        /// <summary>
        /// The column name to update.
        /// </summary>
        public string ColumnName { get; set; } = string.Empty;

        /// <summary>
        /// The value to set in the column.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Event arguments for environment variable update requests from scripts.
    /// </summary>
    public class EnvironmentUpdateEventArgs : EventArgs
    {
        /// <summary>
        /// The environment variable name to update.
        /// </summary>
        public string Variable { get; set; } = string.Empty;

        /// <summary>
        /// The value to persist.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of script output.
    /// </summary>
    public enum ScriptOutputType
    {
        Info,
        Command,
        CommandOutput,
        RawChunk,
        Debug,
        Warning,
        Error,
        Success
    }

    /// <summary>
    /// Exit status of a script execution.
    /// </summary>
    public enum ScriptExitStatus
    {
        Success,
        Failure,
        Cancelled,
        Error
    }

    /// <summary>
    /// Result of script execution.
    /// </summary>
    public class ScriptResult
    {
        public ScriptExitStatus Status { get; set; } = ScriptExitStatus.Success;
        public string Message { get; set; } = string.Empty;
        public string FullOutput { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// Manages the execution context for a script, including variables and output.
    /// </summary>
    public class ScriptContext
    {
        private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
        private static readonly Regex ArrayExpressionRegex = new(@"^(\w+)\[([^\]]+)\]$", RegexOptions.Compiled);
        private readonly Dictionary<string, object?> _variables;
        private readonly SharedScriptExecutionState _sharedState;
        private readonly AsyncLocal<int> _loopDepth = new();
        private readonly AsyncLocal<int> _callDepth = new();

        private sealed class SharedScriptExecutionState
        {
            public object StateLock { get; } = new();
            public StringBuilder Output { get; } = new();
            public StringBuilder OutputWindow { get; } = new();
            public List<InteractiveTerminalSessionDetails> InteractiveSessions { get; } = new();
            public object InteractiveSessionsLock { get; } = new();
            public string LastCommandOutput { get; set; } = string.Empty;
            public SshShellSession? Session { get; set; }
            public HostConnection? CurrentHost { get; set; }
            public string? ResolvedUsername { get; set; }
            public string? ResolvedPassword { get; set; }
            public string? HistoryLabel { get; set; }
            public bool HistoryLabelReplacesAddress { get; set; }
            public bool HistoryLabelTouched { get; set; }
            public List<HistoryLabelOperation> HistoryLabelOperations { get; } = new();
            public SshTimeoutOptions? Timeouts { get; set; }
            public DebugState DebugState { get; } = new();
            public bool AllowFileSelectionDialogs { get; set; } = true;
            public bool DebugMode { get; set; }
            public bool LocalCmdRunAllApproved { get; set; }
            public string? LocalCmdApprovedHost { get; set; }
            public HashSet<string> LocalCmdApprovedCommands { get; } = new();
            public int SoftAssertPassed { get; set; }
            public int SoftAssertFailed { get; set; }
            public EventHandler<ScriptOutputEventArgs>? OutputReceived { get; set; }
            public EventHandler<ColumnUpdateEventArgs>? ColumnUpdateRequested { get; set; }
            public EventHandler<EnvironmentUpdateEventArgs>? EnvironmentUpdateRequested { get; set; }
        }

        /// <summary>
        /// Number of soft assertions (assert with severity: warning) that passed during this run.
        /// </summary>
        public int SoftAssertPassed => _sharedState.SoftAssertPassed;

        /// <summary>
        /// Number of soft assertions (assert with severity: warning) that failed during this run.
        /// </summary>
        public int SoftAssertFailed => _sharedState.SoftAssertFailed;

        /// <summary>
        /// Records the outcome of a soft assertion for the end-of-run summary. Thread-safe.
        /// </summary>
        public void RecordSoftAssert(bool passed)
        {
            lock (_sharedState.StateLock)
            {
                if (passed)
                    _sharedState.SoftAssertPassed++;
                else
                    _sharedState.SoftAssertFailed++;
            }
        }

        /// <summary>
        /// The SSH shell session for executing commands.
        /// </summary>
        public SshShellSession? Session
        {
            get => _sharedState.Session;
            set => _sharedState.Session = value;
        }

        /// <summary>
        /// The host currently being executed for this script context.
        /// </summary>
        public HostConnection? CurrentHost
        {
            get => _sharedState.CurrentHost;
            set => _sharedState.CurrentHost = value;
        }

        /// <summary>
        /// Resolved username used for the current host execution.
        /// </summary>
        public string? ResolvedUsername
        {
            get => _sharedState.ResolvedUsername;
            set => _sharedState.ResolvedUsername = value;
        }

        /// <summary>
        /// Resolved password used for the current host execution.
        /// </summary>
        public string? ResolvedPassword
        {
            get => _sharedState.ResolvedPassword;
            set => _sharedState.ResolvedPassword = value;
        }

        /// <summary>
        /// Optional label attached to this host's history entry via the sethistorylabel command.
        /// Null means no label was set.
        /// </summary>
        public string? HistoryLabel
        {
            get => _sharedState.HistoryLabel;
            set => _sharedState.HistoryLabel = value;
        }

        /// <summary>
        /// When true, the history entry for this host displays only HistoryLabel (IP hidden).
        /// When false, it displays "IP - HistoryLabel" (or plain IP if HistoryLabel is null).
        /// </summary>
        public bool HistoryLabelReplacesAddress
        {
            get => _sharedState.HistoryLabelReplacesAddress;
            set => _sharedState.HistoryLabelReplacesAddress = value;
        }

        /// <summary>
        /// Tracks whether sethistorylabel executed for this host, including explicit clears.
        /// </summary>
        public bool HistoryLabelTouched
        {
            get => _sharedState.HistoryLabelTouched;
            set => _sharedState.HistoryLabelTouched = value;
        }

        /// <summary>
        /// Records a sethistorylabel operation for deterministic replay outside the current script context.
        /// </summary>
        public void AddHistoryLabelOperation(HistoryLabelOperation operation)
        {
            ArgumentNullException.ThrowIfNull(operation);

            lock (_sharedState.StateLock)
            {
                _sharedState.HistoryLabelOperations.Add(operation.Clone());
            }
        }

        /// <summary>
        /// Returns a defensive copy of the recorded sethistorylabel operations.
        /// </summary>
        public IReadOnlyList<HistoryLabelOperation> GetHistoryLabelOperationsSnapshot()
        {
            lock (_sharedState.StateLock)
            {
                return _sharedState.HistoryLabelOperations
                    .Select(operation => operation.Clone())
                    .ToList();
            }
        }

        /// <summary>
        /// Timeout options for the current host execution.
        /// </summary>
        public SshTimeoutOptions? Timeouts
        {
            get => _sharedState.Timeouts;
            set => _sharedState.Timeouts = value;
        }

        /// <summary>
        /// Debug state for breakpoints and stepping.
        /// </summary>
        public DebugState DebugState => _sharedState.DebugState;

        /// <summary>
        /// Whether commands may open file-selection dialogs during this execution.
        /// </summary>
        public bool AllowFileSelectionDialogs
        {
            get => _sharedState.AllowFileSelectionDialogs;
            set => _sharedState.AllowFileSelectionDialogs = value;
        }

        /// <summary>
        /// When true, debug output (Extract results, Set values, etc.) is shown.
        /// When false, debug output is suppressed.
        /// </summary>
        public bool DebugMode
        {
            get => _sharedState.DebugMode;
            set => _sharedState.DebugMode = value;
        }

        public bool LocalCmdRunAllApproved
        {
            get => _sharedState.LocalCmdRunAllApproved;
            set => _sharedState.LocalCmdRunAllApproved = value;
        }

        public string? LocalCmdApprovedHost
        {
            get => _sharedState.LocalCmdApprovedHost;
            set => _sharedState.LocalCmdApprovedHost = value;
        }

        public HashSet<string> LocalCmdApprovedCommands => _sharedState.LocalCmdApprovedCommands;

        internal object LocalCmdTrackingKey => _sharedState;

        public void ResetLocalCmdApproval()
        {
            _sharedState.LocalCmdRunAllApproved = false;
            _sharedState.LocalCmdApprovedHost = null;
            _sharedState.LocalCmdApprovedCommands.Clear();
        }

        /// <summary>
        /// Current loop nesting depth. Managed by ScriptExecutor.
        /// </summary>
        public int LoopDepth
        {
            get => _loopDepth.Value;
            set => _loopDepth.Value = value;
        }

        /// <summary>
        /// Current subroutine call depth. Managed by call execution.
        /// </summary>
        public int CallDepth
        {
            get => _callDepth.Value;
            set => _callDepth.Value = value;
        }

        /// <summary>
        /// Vault service for reading/writing secrets. Null when Vault is not configured.
        /// </summary>
        public Vault.VaultService? VaultService { get; set; }

        /// <summary>
        /// Environment-level Vault profile override, if any.
        /// </summary>
        public string? EnvironmentVaultProfile { get; set; }

        /// <summary>
        /// Notification service for the notify command. Null when notifications are not wired up.
        /// </summary>
        public Notifications.NotificationService? NotificationService { get; set; }

        /// <summary>
        /// Active root script for this execution context.
        /// </summary>
        public Script? ActiveScript { get; set; }

        /// <summary>
        /// Resolved subroutine registry for the active script.
        /// </summary>
        public ScriptSubroutineRegistry? SubroutineRegistry { get; set; }

        /// <summary>
        /// Current subroutine definition executing inside this scope, if any.
        /// </summary>
        public ScriptSubroutineDefinition? CurrentSubroutine { get; set; }

        /// <summary>
        /// Fired when script produces output.
        /// </summary>
        public event EventHandler<ScriptOutputEventArgs>? OutputReceived
        {
            add
            {
                lock (_sharedState.StateLock)
                {
                    _sharedState.OutputReceived += value;
                }
            }
            remove
            {
                lock (_sharedState.StateLock)
                {
                    _sharedState.OutputReceived -= value;
                }
            }
        }

        /// <summary>
        /// Fired when script requests a column update for the current host.
        /// </summary>
        public event EventHandler<ColumnUpdateEventArgs>? ColumnUpdateRequested
        {
            add
            {
                lock (_sharedState.StateLock)
                {
                    _sharedState.ColumnUpdateRequested += value;
                }
            }
            remove
            {
                lock (_sharedState.StateLock)
                {
                    _sharedState.ColumnUpdateRequested -= value;
                }
            }
        }

        /// <summary>
        /// Fired when script requests an environment variable update.
        /// </summary>
        public event EventHandler<EnvironmentUpdateEventArgs>? EnvironmentUpdateRequested
        {
            add
            {
                lock (_sharedState.StateLock)
                {
                    _sharedState.EnvironmentUpdateRequested += value;
                }
            }
            remove
            {
                lock (_sharedState.StateLock)
                {
                    _sharedState.EnvironmentUpdateRequested -= value;
                }
            }
        }

        /// <summary>
        /// Gets the last command output.
        /// </summary>
        public string LastCommandOutput
        {
            get
            {
                lock (_sharedState.StateLock)
                {
                    return _sharedState.LastCommandOutput;
                }
            }
        }

        /// <summary>
        /// Gets the accumulated full output.
        /// </summary>
        public string FullOutput
        {
            get
            {
                lock (_sharedState.StateLock)
                {
                    return _sharedState.Output.ToString();
                }
            }
        }

        /// <summary>
        /// Gets the pane-formatted output transcript accumulated for the current host so far.
        /// </summary>
        public string OutputWindowText
        {
            get
            {
                lock (_sharedState.StateLock)
                {
                    return _sharedState.OutputWindow.ToString();
                }
            }
        }

        /// <summary>
        /// Creates a new script context with optional initial variables.
        /// </summary>
        /// <param name="initialVariables">Variables from CSV columns or other sources.</param>
        public ScriptContext(Dictionary<string, string>? initialVariables = null)
            : this(new SharedScriptExecutionState(), initialVariables)
        {
        }

        private ScriptContext(
            SharedScriptExecutionState sharedState,
            Dictionary<string, string>? initialVariables = null)
        {
            _sharedState = sharedState;
            _variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            // Import initial variables (e.g., from CSV columns)
            if (initialVariables != null)
            {
                foreach (var kvp in initialVariables)
                {
                    _variables[kvp.Key] = kvp.Value;
                }
            }
        }

        /// <summary>
        /// Sets a variable value.
        /// </summary>
        public void SetVariable(string name, object? value)
        {
            lock (_sharedState.StateLock)
            {
                _variables[name] = value;
            }
        }

        /// <summary>
        /// Gets a variable value, or null if not found.
        /// </summary>
        public object? GetVariable(string name)
        {
            if (string.Equals(name, "_timestamp", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString(TimestampFormat);
            if (string.Equals(name, "_output", StringComparison.OrdinalIgnoreCase))
                return LastCommandOutput;
            if (string.Equals(name, "_outputwindow", StringComparison.OrdinalIgnoreCase))
                return OutputWindowText;
            if (string.Equals(name, "_prompt", StringComparison.OrdinalIgnoreCase))
                return Session?.CurrentPrompt ?? string.Empty;

            lock (_sharedState.StateLock)
            {
                return _variables.TryGetValue(name, out var value) ? value : null;
            }
        }

        /// <summary>
        /// Gets a variable as a string, with fallback to empty string.
        /// </summary>
        public string GetVariableString(string name)
        {
            var value = GetVariable(name);
            if (value is List<string> list)
                return string.Join(", ", list);
            return value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Gets a variable as a list (for array variables).
        /// </summary>
        public List<string> GetVariableList(string name)
        {
            return ValueResolver.ResolveCollectionItems(GetVariable(name));
        }

        /// <summary>
        /// Checks if a variable exists.
        /// </summary>
        public bool HasVariable(string name)
        {
            if (string.Equals(name, "_timestamp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "_output", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "_outputwindow", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "_prompt", StringComparison.OrdinalIgnoreCase))
                return true;

            lock (_sharedState.StateLock)
            {
                return _variables.ContainsKey(name);
            }
        }

        /// <summary>
        /// Removes a variable from context.
        /// </summary>
        public void RemoveVariable(string name)
        {
            lock (_sharedState.StateLock)
            {
                _variables.Remove(name);
            }
        }

        /// <summary>
        /// Gets all current variables (for debugging/inspection).
        /// </summary>
        public IReadOnlyDictionary<string, object?> GetAllVariables()
        {
            Dictionary<string, object?> snapshot;
            lock (_sharedState.StateLock)
            {
                snapshot = new Dictionary<string, object?>(_variables, StringComparer.OrdinalIgnoreCase);
            }

            snapshot["_timestamp"] = DateTime.Now.ToString(TimestampFormat);
            snapshot["_output"] = LastCommandOutput;
            snapshot["_outputwindow"] = OutputWindowText;
            snapshot["_prompt"] = Session?.CurrentPrompt ?? string.Empty;
            return snapshot;
        }

        /// <summary>
        /// Substitutes ${variable} and {{variable}} placeholders in a string.
        /// Supports nested references and array indexing: ${array[0]} or ${array[index]}
        /// </summary>
        public string SubstituteVariables(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string lastOutput;
            string outputWindowText;
            lock (_sharedState.StateLock)
            {
                lastOutput = _sharedState.LastCommandOutput;
                outputWindowText = _sharedState.OutputWindow.ToString();
            }

            // Handle special _output variable
            var result = input.Replace("${_output}", lastOutput);
            result = result.Replace("${_outputwindow}", outputWindowText);

            // Replace ${variable} and {{variable}} patterns with support for nested ${...} expressions.
            return SubstituteVariableTokens(result);
        }

        /// <summary>
        /// Resolves a variable expression which may include array indexing.
        /// </summary>
        private string ResolveVariableExpression(string expr)
        {
            // Inline vault secret resolution: {{vault:path#key}} or {{vault:profile@path#key}}
            if (expr.StartsWith("vault:", StringComparison.OrdinalIgnoreCase))
                return ResolveVaultExpression(expr.Substring(6));

            // Support list length property: ${list.length}
            var (handled, length) = ValueResolver.TryResolveLengthExpression(expr, GetVariable);
            if (handled)
            {
                return length.ToString();
            }

            // Check for array indexing: varname[index]
            var arrayMatch = ArrayExpressionRegex.Match(expr);
            if (arrayMatch.Success)
            {
                var varName = arrayMatch.Groups[1].Value;
                var indexExpr = arrayMatch.Groups[2].Value.Trim();

                // Resolve the index (could be a number or variable name)
                int index;
                if (!int.TryParse(indexExpr, out index))
                {
                    // Try to get index from a variable
                    var indexVar = GetVariable(indexExpr);
                    if (indexVar != null && int.TryParse(indexVar.ToString(), out var varIndex))
                        index = varIndex;
                    else
                        return string.Empty;
                }

                var list = GetVariableList(varName);
                if (index >= 0 && index < list.Count)
                    return list[index];
                return string.Empty;
            }

            // Support inline function expressions: ${json.format(data)}, ${upper(json.get(x, "k"))}, etc.
            if (expr.Contains('('))
            {
                var result = ValueResolver.ResolveExpressionValue(expr, this);
                if (result != null)
                {
                    return result switch
                    {
                        string s => s,
                        List<string> list => string.Join(", ", list),
                        JsonNode node => node.ToJsonString(),
                        _ => result.ToString() ?? string.Empty
                    };
                }
            }

            // Simple variable lookup
            return GetVariableString(expr);
        }

        private string ResolveVaultExpression(string vaultExpr)
        {
            if (VaultService == null)
            {
                SetVariable("_last_error", "Vault is not configured");
                return string.Empty;
            }

            // Parse [profile@]path#key
            string? profile = null;
            string remaining = vaultExpr;

            var hashIndex = remaining.IndexOf('#');
            if (hashIndex < 0)
            {
                SetVariable("_last_error", "Vault inline syntax requires '#' delimiter: vault:[profile@]path#key");
                return string.Empty;
            }

            var key = remaining.Substring(hashIndex + 1);
            var pathPart = remaining.Substring(0, hashIndex);

            var atIndex = pathPart.IndexOf('@');
            if (atIndex >= 0)
            {
                profile = pathPart.Substring(0, atIndex);
                pathPart = pathPart.Substring(atIndex + 1);
            }

            var profileName = !string.IsNullOrEmpty(profile)
                ? profile
                : VaultService.ResolveDefaultProfileName(EnvironmentVaultProfile);

            if (string.IsNullOrEmpty(profileName))
            {
                SetVariable("_last_error", "No Vault profile available");
                return string.Empty;
            }

            try
            {
                var value = VaultService.ReadSecretAsync(profileName, pathPart, key).GetAwaiter().GetResult();
                return value ?? string.Empty;
            }
            catch (Vault.VaultException ex)
            {
                SetVariable("_last_error", ex.Message);
                return string.Empty;
            }
        }

        private string SubstituteVariableTokens(string input)
        {
            var output = new StringBuilder(input.Length);

            for (int i = 0; i < input.Length; i++)
            {
                // ${...} pattern with nested placeholder support.
                if (input[i] == '$' && i + 1 < input.Length && input[i + 1] == '{')
                {
                    if (TryExtractDollarExpression(input, i, out var expr, out var endIndex))
                    {
                        var resolvedExpr = SubstituteVariableTokens(expr);
                        output.Append(ResolveVariableExpression(resolvedExpr));
                        i = endIndex;
                        continue;
                    }
                }

                // {{...}} pattern (CSV-style variable names).
                if (input[i] == '{' && i + 1 < input.Length && input[i + 1] == '{')
                {
                    var endIndex = input.IndexOf("}}", i + 2, StringComparison.Ordinal);
                    if (endIndex >= 0)
                    {
                        var expr = input.Substring(i + 2, endIndex - (i + 2));
                        var resolvedExpr = SubstituteVariableTokens(expr);
                        output.Append(ResolveVariableExpression(resolvedExpr));
                        i = endIndex + 1;
                        continue;
                    }
                }

                output.Append(input[i]);
            }

            return output.ToString();
        }

        private static bool TryExtractDollarExpression(
            string input,
            int startIndex,
            out string expression,
            out int endIndex)
        {
            expression = string.Empty;
            endIndex = startIndex;

            if (startIndex + 1 >= input.Length || input[startIndex] != '$' || input[startIndex + 1] != '{')
                return false;

            var depth = 1;

            for (int i = startIndex + 2; i < input.Length; i++)
            {
                if (input[i] == '$' && i + 1 < input.Length && input[i + 1] == '{')
                {
                    depth++;
                    i++;
                    continue;
                }

                if (input[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        expression = input.Substring(startIndex + 2, i - (startIndex + 2));
                        endIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Records the output of a command and optionally captures it to a variable.
        /// </summary>
        public void RecordCommandOutput(string output, string? captureVariable = null)
        {
            lock (_sharedState.StateLock)
            {
                _sharedState.LastCommandOutput = output;
                _sharedState.Output.AppendLine(output);
            }

            if (!string.IsNullOrEmpty(captureVariable))
            {
                SetVariable(captureVariable, output);
            }
        }

        /// <summary>
        /// Emits output to subscribers.
        /// </summary>
        public void EmitOutput(string message, ScriptOutputType type = ScriptOutputType.Info)
        {
            // Suppress debug output when not in debug mode
            if (type == ScriptOutputType.Debug && !DebugMode)
                return;

            EventHandler<ScriptOutputEventArgs>? outputReceived;
            ScriptOutputEventArgs eventArgs;

            lock (_sharedState.StateLock)
            {
                _sharedState.Output.AppendLine(message);
                outputReceived = _sharedState.OutputReceived;
                eventArgs = new ScriptOutputEventArgs
                {
                    Message = message,
                    Type = type
                };
            }

            outputReceived?.Invoke(this, eventArgs);
        }

        /// <summary>
        /// Clears the accumulated output.
        /// </summary>
        public void ClearOutput()
        {
            lock (_sharedState.StateLock)
            {
                _sharedState.Output.Clear();
                _sharedState.OutputWindow.Clear();
            }
        }

        /// <summary>
        /// Replaces the current pane-formatted host transcript for this execution context.
        /// </summary>
        internal void SetOutputWindowText(string? output)
        {
            lock (_sharedState.StateLock)
            {
                _sharedState.OutputWindow.Clear();
                if (!string.IsNullOrEmpty(output))
                {
                    _sharedState.OutputWindow.Append(output);
                }
            }
        }

        /// <summary>
        /// Appends pane-formatted host output to the current execution transcript.
        /// </summary>
        internal void AppendOutputWindowText(string? output)
        {
            if (string.IsNullOrEmpty(output))
                return;

            lock (_sharedState.StateLock)
            {
                _sharedState.OutputWindow.Append(output);
            }
        }

        /// <summary>
        /// Requests an update to a column in the host table.
        /// </summary>
        /// <param name="columnName">The column name to update.</param>
        /// <param name="value">The value to set.</param>
        public void RequestColumnUpdate(string columnName, string value)
        {
            EventHandler<ColumnUpdateEventArgs>? handler;
            ColumnUpdateEventArgs args;
            lock (_sharedState.StateLock)
            {
                handler = _sharedState.ColumnUpdateRequested;
                args = new ColumnUpdateEventArgs
                {
                    ColumnName = columnName,
                    Value = value
                };
            }

            handler?.Invoke(this, args);
        }

        /// <summary>
        /// Requests a persisted update to an environment variable.
        /// </summary>
        /// <param name="variable">The variable name to update.</param>
        /// <param name="value">The value to persist.</param>
        public void RequestEnvironmentUpdate(string variable, string value)
        {
            EventHandler<EnvironmentUpdateEventArgs>? handler;
            EnvironmentUpdateEventArgs args;
            lock (_sharedState.StateLock)
            {
                handler = _sharedState.EnvironmentUpdateRequested;
                args = new EnvironmentUpdateEventArgs
                {
                    Variable = variable,
                    Value = value
                };
            }

            handler?.Invoke(this, args);
        }

        /// <summary>
        /// Stores one interactive terminal session audit record for this script execution context.
        /// </summary>
        public void AddInteractiveSession(InteractiveTerminalSessionDetails session)
        {
            if (session == null)
                return;

            lock (_sharedState.InteractiveSessionsLock)
            {
                var cloned = CloneInteractiveSession(session);
                if (cloned.SessionNumber <= 0)
                {
                    cloned.SessionNumber = _sharedState.InteractiveSessions.Count + 1;
                }

                _sharedState.InteractiveSessions.Add(cloned);
            }
        }

        /// <summary>
        /// Returns a deep-copied snapshot of captured interactive terminal sessions.
        /// </summary>
        public List<InteractiveTerminalSessionDetails> GetInteractiveSessionsSnapshot()
        {
            lock (_sharedState.InteractiveSessionsLock)
            {
                return _sharedState.InteractiveSessions
                    .Select(CloneInteractiveSession)
                    .ToList();
            }
        }

        /// <summary>
        /// Creates an isolated child variable scope that shares runtime/session/output state with the parent.
        /// </summary>
        public ScriptContext CreateChildScope(
            IReadOnlyDictionary<string, object?>? initialVariables = null,
            ScriptSubroutineDefinition? currentSubroutine = null)
        {
            var child = new ScriptContext(_sharedState)
            {
                ActiveScript = ActiveScript,
                SubroutineRegistry = SubroutineRegistry,
                CurrentSubroutine = currentSubroutine,
                CallDepth = CallDepth + 1,
                LoopDepth = 0
            };

            if (initialVariables != null)
            {
                foreach (var kvp in initialVariables)
                {
                    child.SetVariable(kvp.Key, CloneVariableValue(kvp.Value));
                }
            }

            return child;
        }

        /// <summary>
        /// Imports variables from a script's vars section.
        /// </summary>
        public void ImportScriptVars(Dictionary<string, object?> vars)
        {
            lock (_sharedState.StateLock)
            {
                foreach (var kvp in vars)
                {
                    // Only set if not already defined (CSV variables take precedence)
                    if (!_variables.ContainsKey(kvp.Key))
                    {
                        _variables[kvp.Key] = CloneVariableValue(kvp.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Copies selected child-scope outputs back to the caller scope.
        /// </summary>
        public void CopyOutputsFromChild(
            ScriptContext childContext,
            IReadOnlyDictionary<string, string> outputBindings,
            IEnumerable<string> declaredOutputs)
        {
            if (childContext == null || outputBindings == null)
            {
                return;
            }

            var declared = new HashSet<string>(declaredOutputs ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var binding in outputBindings)
            {
                if (!declared.Contains(binding.Key))
                {
                    continue;
                }

                SetVariable(binding.Value, CloneVariableValue(childContext.GetVariable(binding.Key)));
            }
        }

        private static object? CloneVariableValue(object? value)
        {
            return value switch
            {
                null => null,
                List<string> list => new List<string>(list),
                JsonNode node => node.DeepClone(),
                JsonElement element => CloneJsonElement(element),
                Dictionary<string, object?> dict => CloneDictionary(dict),
                _ => value
            };
        }

        private static Dictionary<string, object?> CloneDictionary(IDictionary<string, object?> source)
        {
            var clone = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in source)
            {
                clone[kvp.Key] = CloneVariableValue(kvp.Value);
            }

            return clone;
        }

        private static JsonElement CloneJsonElement(JsonElement element)
        {
            using var document = JsonDocument.Parse(element.GetRawText());
            return document.RootElement.Clone();
        }

        private static InteractiveTerminalSessionDetails CloneInteractiveSession(InteractiveTerminalSessionDetails session)
        {
            return new InteractiveTerminalSessionDetails
            {
                SessionNumber = session.SessionNumber,
                HostAddress = session.HostAddress ?? string.Empty,
                SessionMode = session.SessionMode ?? string.Empty,
                EmulationMode = session.EmulationMode ?? string.Empty,
                StartedAtUtc = session.StartedAtUtc,
                EndedAtUtc = session.EndedAtUtc,
                CloseReason = session.CloseReason ?? string.Empty,
                Completed = session.Completed,
                Transcript = session.Transcript ?? string.Empty
            };
        }
    }
}
