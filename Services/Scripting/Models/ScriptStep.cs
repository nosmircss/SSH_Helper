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

        /// <summary>
        /// Log command - outputs a message with a specific log level.
        /// Simple form: "log: message" (defaults to info level)
        /// Options form: log: { message: "text", level: "warning" }
        /// </summary>
        public object? Log { get; set; }

        /// <summary>
        /// Webhook command - makes an HTTP request to a URL.
        /// </summary>
        public WebhookOptions? Webhook { get; set; }

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
            if (Log != null) return StepType.Log;
            if (Webhook != null) return StepType.Webhook;
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
        /// Content to write to the file. For text format, this is the raw content.
        /// For json/csv formats, this should be a variable reference like ${varname}.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Write mode: "overwrite" (default) or "append".
        /// </summary>
        public string Mode { get; set; } = "overwrite";

        /// <summary>
        /// Output format: "text" (default), "json", "jsonl" (JSON Lines), or "csv".
        /// For JSON with mode "append": arrays are concatenated, objects are deep-merged.
        /// For JSONL: each write appends a single JSON object on a new line.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// For JSON format: whether to pretty-print with indentation (default: true).
        /// </summary>
        public bool Pretty { get; set; } = true;

        /// <summary>
        /// For CSV format: optional header row. If not provided, no header is written.
        /// </summary>
        public List<string>? Headers { get; set; }
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
        /// Null means the value was not specified in the script.
        /// </summary>
        public string? Value { get; set; }
    }

    /// <summary>
    /// Options for the log command.
    /// </summary>
    public class LogOptions
    {
        /// <summary>
        /// The message to log.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Log level: "info" (default), "debug", "warning", "error", "success".
        /// </summary>
        public string Level { get; set; } = "info";
    }

    /// <summary>
    /// Options for the webhook command.
    /// </summary>
    public class WebhookOptions
    {
        /// <summary>
        /// The URL to send the request to.
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// HTTP method: "GET", "POST" (default), "PUT", "PATCH", "DELETE".
        /// </summary>
        public string Method { get; set; } = "POST";

        /// <summary>
        /// Request body (for POST, PUT, PATCH). Supports variable substitution.
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// Optional HTTP headers as key-value pairs.
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// Variable name to capture the response body into.
        /// Also sets {varname}_status with the HTTP status code.
        /// </summary>
        public string? Into { get; set; }

        /// <summary>
        /// Request timeout in seconds (default: 30).
        /// </summary>
        public int Timeout { get; set; } = 30;
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
        UpdateColumn,
        Log,
        Webhook
    }
}
