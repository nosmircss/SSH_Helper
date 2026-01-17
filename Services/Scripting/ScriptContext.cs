using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
        private readonly Dictionary<string, object?> _variables = new(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _output = new();
        private string _lastCommandOutput = string.Empty;

        /// <summary>
        /// The SSH shell session for executing commands.
        /// </summary>
        public SshShellSession? Session { get; set; }

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
        /// Fired when script produces output.
        /// </summary>
        public event EventHandler<ScriptOutputEventArgs>? OutputReceived;

        /// <summary>
        /// Fired when script requests a column update for the current host.
        /// </summary>
        public event EventHandler<ColumnUpdateEventArgs>? ColumnUpdateRequested;

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

            // Add built-in variables
            _variables["_timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
            return _variables.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Gets a variable as a string, with fallback to empty string.
        /// </summary>
        public string GetVariableString(string name)
        {
            var value = GetVariable(name);
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
            return _variables.ContainsKey(name);
        }

        /// <summary>
        /// Gets all current variables (for debugging/inspection).
        /// </summary>
        public IReadOnlyDictionary<string, object?> GetAllVariables()
        {
            return _variables;
        }

        /// <summary>
        /// Substitutes ${variable} placeholders in a string.
        /// Supports nested references and array indexing: ${array[0]} or ${array[index]}
        /// </summary>
        public string SubstituteVariables(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Handle special _output variable
            var result = input.Replace("${_output}", _lastCommandOutput);

            // Replace ${variable} patterns
            result = Regex.Replace(result, @"\$\{([^}]+)\}", match =>
            {
                var expr = match.Groups[1].Value;
                return ResolveVariableExpression(expr);
            });

            return result;
        }

        /// <summary>
        /// Resolves a variable expression which may include array indexing.
        /// </summary>
        private string ResolveVariableExpression(string expr)
        {
            // Check for array indexing: varname[index]
            var arrayMatch = Regex.Match(expr, @"^(\w+)\[([^\]]+)\]$");
            if (arrayMatch.Success)
            {
                var varName = arrayMatch.Groups[1].Value;
                var indexExpr = arrayMatch.Groups[2].Value;

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
    }
}
