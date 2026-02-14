using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _output = new();
        private readonly List<InteractiveTerminalSessionDetails> _interactiveSessions = new();
        private readonly object _interactiveSessionsLock = new();
        private string _lastCommandOutput = string.Empty;

        /// <summary>
        /// The SSH shell session for executing commands.
        /// </summary>
        public SshShellSession? Session { get; set; }

        /// <summary>
        /// The host currently being executed for this script context.
        /// </summary>
        public HostConnection? CurrentHost { get; set; }

        /// <summary>
        /// Resolved username used for the current host execution.
        /// </summary>
        public string? ResolvedUsername { get; set; }

        /// <summary>
        /// Resolved password used for the current host execution.
        /// </summary>
        public string? ResolvedPassword { get; set; }

        /// <summary>
        /// Timeout options for the current host execution.
        /// </summary>
        public SshTimeoutOptions? Timeouts { get; set; }

        /// <summary>
        /// Debug state for breakpoints and stepping.
        /// </summary>
        public DebugState DebugState { get; } = new();

        /// <summary>
        /// When true, debug output (Extract results, Set values, etc.) is shown.
        /// When false, debug output is suppressed.
        /// </summary>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Current loop nesting depth. Managed by ScriptExecutor.
        /// </summary>
        public int LoopDepth { get; set; }

        /// <summary>
        /// Fired when script produces output.
        /// </summary>
        public event EventHandler<ScriptOutputEventArgs>? OutputReceived;

        /// <summary>
        /// Fired when script requests a column update for the current host.
        /// </summary>
        public event EventHandler<ColumnUpdateEventArgs>? ColumnUpdateRequested;

        /// <summary>
        /// Fired when script requests an environment variable update.
        /// </summary>
        public event EventHandler<EnvironmentUpdateEventArgs>? EnvironmentUpdateRequested;

        /// <summary>
        /// Gets the last command output.
        /// </summary>
        public string LastCommandOutput => _lastCommandOutput;

        /// <summary>
        /// Gets the accumulated full output.
        /// </summary>
        public string FullOutput => _output.ToString();

        /// <summary>
        /// Creates a new script context with optional initial variables.
        /// </summary>
        /// <param name="initialVariables">Variables from CSV columns or other sources.</param>
        public ScriptContext(Dictionary<string, string>? initialVariables = null)
        {
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
            _variables[name] = value;
        }

        /// <summary>
        /// Gets a variable value, or null if not found.
        /// </summary>
        public object? GetVariable(string name)
        {
            if (string.Equals(name, "_timestamp", StringComparison.OrdinalIgnoreCase))
                return DateTime.Now.ToString(TimestampFormat);

            return _variables.TryGetValue(name, out var value) ? value : null;
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
            var value = GetVariable(name);
            if (value is List<string> list)
                return list;
            if (value is string s)
                return new List<string> { s };
            return new List<string>();
        }

        /// <summary>
        /// Checks if a variable exists.
        /// </summary>
        public bool HasVariable(string name)
        {
            if (string.Equals(name, "_timestamp", StringComparison.OrdinalIgnoreCase))
                return true;

            return _variables.ContainsKey(name);
        }

        /// <summary>
        /// Removes a variable from context.
        /// </summary>
        public void RemoveVariable(string name)
        {
            _variables.Remove(name);
        }

        /// <summary>
        /// Gets all current variables (for debugging/inspection).
        /// </summary>
        public IReadOnlyDictionary<string, object?> GetAllVariables()
        {
            var snapshot = new Dictionary<string, object?>(_variables, StringComparer.OrdinalIgnoreCase)
            {
                ["_timestamp"] = DateTime.Now.ToString(TimestampFormat)
            };
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

            // Handle special _output variable
            var result = input.Replace("${_output}", _lastCommandOutput);

            // Replace ${variable} and {{variable}} patterns with support for nested ${...} expressions.
            return SubstituteVariableTokens(result);
        }

        /// <summary>
        /// Resolves a variable expression which may include array indexing.
        /// </summary>
        private string ResolveVariableExpression(string expr)
        {
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

            // Simple variable lookup
            return GetVariableString(expr);
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
            _lastCommandOutput = output;
            _variables["_output"] = output;
            _output.AppendLine(output);

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

            _output.AppendLine(message);
            OutputReceived?.Invoke(this, new ScriptOutputEventArgs
            {
                Message = message,
                Type = type
            });
        }

        /// <summary>
        /// Clears the accumulated output.
        /// </summary>
        public void ClearOutput()
        {
            _output.Clear();
        }

        /// <summary>
        /// Requests an update to a column in the host table.
        /// </summary>
        /// <param name="columnName">The column name to update.</param>
        /// <param name="value">The value to set.</param>
        public void RequestColumnUpdate(string columnName, string value)
        {
            ColumnUpdateRequested?.Invoke(this, new ColumnUpdateEventArgs
            {
                ColumnName = columnName,
                Value = value
            });
        }

        /// <summary>
        /// Requests a persisted update to an environment variable.
        /// </summary>
        /// <param name="variable">The variable name to update.</param>
        /// <param name="value">The value to persist.</param>
        public void RequestEnvironmentUpdate(string variable, string value)
        {
            EnvironmentUpdateRequested?.Invoke(this, new EnvironmentUpdateEventArgs
            {
                Variable = variable,
                Value = value
            });
        }

        /// <summary>
        /// Stores one interactive terminal session audit record for this script execution context.
        /// </summary>
        public void AddInteractiveSession(InteractiveTerminalSessionDetails session)
        {
            if (session == null)
                return;

            lock (_interactiveSessionsLock)
            {
                var cloned = CloneInteractiveSession(session);
                if (cloned.SessionNumber <= 0)
                {
                    cloned.SessionNumber = _interactiveSessions.Count + 1;
                }

                _interactiveSessions.Add(cloned);
            }
        }

        /// <summary>
        /// Returns a deep-copied snapshot of captured interactive terminal sessions.
        /// </summary>
        public List<InteractiveTerminalSessionDetails> GetInteractiveSessionsSnapshot()
        {
            lock (_interactiveSessionsLock)
            {
                return _interactiveSessions
                    .Select(CloneInteractiveSession)
                    .ToList();
            }
        }

        /// <summary>
        /// Imports variables from a script's vars section.
        /// </summary>
        public void ImportScriptVars(Dictionary<string, object?> vars)
        {
            foreach (var kvp in vars)
            {
                // Only set if not already defined (CSV variables take precedence)
                if (!_variables.ContainsKey(kvp.Key))
                {
                    _variables[kvp.Key] = kvp.Value;
                }
            }
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
