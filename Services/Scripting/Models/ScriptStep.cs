using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// Represents a single step in a script. Each step has exactly one command type set.
    /// </summary>
    public class ScriptStep
    {
        /// <summary>
        /// Line number in the original YAML for error reporting.
        /// </summary>
        public int LineNumber { get; set; }

        // ===== Command Types =====
        // Only one of these should be set per step

        /// <summary>
        /// Send command - executes an SSH command.
        /// Simple form: "send: command"
        /// </summary>
        public string? Send { get; set; }

        /// <summary>
        /// Print command - outputs a message.
        /// </summary>
        public string? Print { get; set; }

        /// <summary>
        /// Wait command - pauses execution for N seconds.
        /// </summary>
        public int? Wait { get; set; }

        /// <summary>
        /// Set command - variable assignment.
        /// Format: "set: varname = value" or "set: varname = varname + 1"
        /// </summary>
        public string? Set { get; set; }

        /// <summary>
        /// Exit command - terminates script execution.
        /// Format: "exit: success message" or "exit: failure message"
        /// </summary>
        public string? Exit { get; set; }

        /// <summary>
        /// Extract command - captures data from a variable using regex.
        /// </summary>
        public ExtractOptions? Extract { get; set; }

        /// <summary>
        /// If condition for conditional execution.
        /// </summary>
        public string? If { get; set; }

        /// <summary>
        /// Foreach loop header.
        /// Format: "foreach: item in collection"
        /// </summary>
        public string? Foreach { get; set; }

        /// <summary>
        /// While loop condition.
        /// </summary>
        public string? While { get; set; }

        /// <summary>
        /// Readfile command - reads a text file into a variable.
        /// </summary>
        public ReadfileOptions? Readfile { get; set; }

        /// <summary>
        /// Writefile command - writes content to a text file.
        /// </summary>
        public WritefileOptions? Writefile { get; set; }

        /// <summary>
        /// Input command - prompts user for input during script execution.
        /// </summary>
        public InputOptions? Input { get; set; }

        /// <summary>
        /// UpdateColumn command - updates a column in the host table with a value.
        /// </summary>
        public UpdateColumnOptions? UpdateColumn { get; set; }

        // ===== Command Options =====

        /// <summary>
        /// Variable name to capture command output into (for send).
        /// </summary>
        public string? Capture { get; set; }

        /// <summary>
        /// Suppress output display for send command. When true, hides both the command and its output.
        /// Useful when capturing output to parse and print selectively.
        /// </summary>
        public bool Suppress { get; set; }

        /// <summary>
        /// Custom prompt pattern to expect (regex).
        /// </summary>
        public string? Expect { get; set; }

        /// <summary>
        /// Timeout in seconds for this specific command.
        /// </summary>
        public int? Timeout { get; set; }

        /// <summary>
        /// Error handling mode: "continue" | "stop" (default)
        /// </summary>
        public string? OnError { get; set; }

        /// <summary>
        /// Steps to execute if condition is true (for if/foreach/while).
        /// </summary>
        public List<ScriptStep>? Then { get; set; }

        /// <summary>
        /// Steps to execute if condition is false (for if).
        /// </summary>
        public List<ScriptStep>? Else { get; set; }

        /// <summary>
        /// Steps to execute in loop body (for foreach/while).
        /// </summary>
        public List<ScriptStep>? Do { get; set; }

        /// <summary>
        /// Filter condition for foreach (optional).
        /// </summary>
        public string? When { get; set; }

        /// <summary>
        /// Returns the type of command this step represents.
        /// </summary>
        public StepType GetStepType()
        {
            if (!string.IsNullOrEmpty(Send)) return StepType.Send;
            if (!string.IsNullOrEmpty(Print)) return StepType.Print;
            if (Wait.HasValue) return StepType.Wait;
            if (!string.IsNullOrEmpty(Set)) return StepType.Set;
            if (!string.IsNullOrEmpty(Exit)) return StepType.Exit;
            if (Extract != null) return StepType.Extract;
            if (!string.IsNullOrEmpty(If)) return StepType.If;
            if (!string.IsNullOrEmpty(Foreach)) return StepType.Foreach;
            if (!string.IsNullOrEmpty(While)) return StepType.While;
            if (Readfile != null) return StepType.Readfile;
            if (Writefile != null) return StepType.Writefile;
            if (Input != null) return StepType.Input;
            if (UpdateColumn != null) return StepType.UpdateColumn;
            return StepType.Unknown;
        }
    }

    /// <summary>
    /// Options for the extract command.
    /// </summary>
    public class ExtractOptions
    {
        /// <summary>
        /// Source variable to extract from.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Regex pattern with capture groups.
        /// </summary>
        public string Pattern { get; set; } = string.Empty;

        /// <summary>
        /// Variable name(s) to store captured values.
        /// Can be a single string or list of strings for multiple capture groups.
        /// </summary>
        public object? Into { get; set; }

        /// <summary>
        /// Which match to capture: "first" (default), "last", "all", or a number.
        /// </summary>
        public string Match { get; set; } = "first";
    }

    /// <summary>
    /// Options for the readfile command.
    /// </summary>
    public class ReadfileOptions
    {
        /// <summary>
        /// Path to the file to read.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Variable name to store the lines into.
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// Whether to skip empty lines (default: true).
        /// </summary>
        public bool SkipEmptyLines { get; set; } = true;

        /// <summary>
        /// Whether to trim whitespace from each line (default: true).
        /// </summary>
        public bool TrimLines { get; set; } = true;

        /// <summary>
        /// Maximum number of lines to read (default: 10000, 0 = unlimited).
        /// </summary>
        public int MaxLines { get; set; } = 10000;

        /// <summary>
        /// File encoding: "utf-8" (default), "ascii", "utf-16", "utf-32".
        /// </summary>
        public string Encoding { get; set; } = "utf-8";
    }

    /// <summary>
    /// Options for the writefile command.
    /// </summary>
    public class WritefileOptions
    {
        /// <summary>
        /// Path to the file to write.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Content to write to the file.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Write mode: "append" or "overwrite" (default: append).
        /// </summary>
        public string Mode { get; set; } = "append";
    }

    /// <summary>
    /// Options for the input command.
    /// </summary>
    public class InputOptions
    {
        /// <summary>
        /// Prompt text to display to the user.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Variable name to store the user's input.
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// Default value if user provides no input.
        /// </summary>
        public string? Default { get; set; }

        /// <summary>
        /// Whether to mask input (for passwords).
        /// </summary>
        public bool Password { get; set; }

        /// <summary>
        /// Optional regex pattern to validate input against.
        /// </summary>
        public string? Validate { get; set; }

        /// <summary>
        /// Error message to show when validation fails.
        /// </summary>
        public string? ValidationError { get; set; }
    }

    /// <summary>
    /// Options for the updatecolumn command.
    /// </summary>
    public class UpdateColumnOptions
    {
        /// <summary>
        /// The column name to update in the host table.
        /// </summary>
        public string Column { get; set; } = string.Empty;

        /// <summary>
        /// The value to set. Can be a literal string or a variable reference like ${varname}.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Enumeration of step types.
    /// </summary>
    public enum StepType
    {
        Unknown,
        Send,
        Print,
        Wait,
        Set,
        Exit,
        Extract,
        If,
        Foreach,
        While,
        Readfile,
        Writefile,
        Input,
        UpdateColumn
    }
}
