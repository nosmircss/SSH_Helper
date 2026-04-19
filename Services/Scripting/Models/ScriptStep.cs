using System;
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

        /// <summary>
        /// Canonical scope-aware step identity (for example: steps/2/then/0).
        /// Assigned by the executor before runtime to correlate debug/runtime events with Flow Canvas nodes.
        /// </summary>
        public string? StepPath { get; set; }

        /// <summary>
        /// Step command key discovered during parse, even when required payload fields are missing.
        /// Enables precise validation errors instead of generic "unknown step" diagnostics.
        /// </summary>
        public StepType DeclaredStepType { get; set; } = StepType.Unknown;

        /// <summary>
        /// Indicates whether this step used deprecated root-level on_error syntax.
        /// </summary>
        public bool UsesStepRootOnError { get; set; }

        /// <summary>
        /// Parser-originated validation issues for this step.
        /// </summary>
        public List<string> ParseErrors { get; } = new();

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
        /// Exists command - checks local file or directory existence.
        /// </summary>
        public ExistsOptions? Exists { get; set; }

        /// <summary>
        /// Playsound command - plays a local audio file.
        /// </summary>
        public PlaySoundOptions? PlaySound { get; set; }

        /// <summary>
        /// Input command - prompts user for input during script execution.
        /// </summary>
        public InputOptions? Input { get; set; }

        /// <summary>
        /// UpdateColumn command - updates a column in the host table with a value.
        /// </summary>
        public UpdateColumnOptions? UpdateColumn { get; set; }

        /// <summary>
        /// UpdateEnvironment command - updates an environment variable with a value.
        /// </summary>
        public UpdateEnvironmentOptions? UpdateEnvironment { get; set; }

        /// <summary>
        /// Log command - outputs a message with a specific log level.
        /// Simple form: "log: message" (defaults to info level)
        /// Options form: log: { message: "text", level: "warning" }
        /// </summary>
        public object? Log { get; set; }

        /// <summary>
        /// SetHistoryLabel command - attaches a label to this host's history entry.
        /// Simple form: "sethistorylabel: text"
        /// Options form: sethistorylabel: { value: "text", replace: true, mode: append, separator: " " }
        /// </summary>
        public object? SetHistoryLabel { get; set; }

        /// <summary>
        /// Http command - makes HTTP requests with auth, headers, and response capture.
        /// </summary>
        public HttpOptions? Http { get; set; }

        /// <summary>
        /// Browser callback capture command - opens a browser flow and captures localhost callback parameters.
        /// </summary>
        public BrowserCallbackCaptureOptions? BrowserCallbackCapture { get; set; }

        /// <summary>
        /// Ping command - performs ICMP reachability checks.
        /// </summary>
        public PingOptions? Ping { get; set; }

        /// <summary>
        /// Dns command - resolves DNS records.
        /// </summary>
        public DnsOptions? Dns { get; set; }

        /// <summary>
        /// Portcheck command - checks TCP port availability.
        /// </summary>
        public PortcheckOptions? Portcheck { get; set; }

        /// <summary>
        /// Sftp command - uploads/downloads files via SFTP.
        /// </summary>
        public SftpOptions? Sftp { get; set; }

        /// <summary>
        /// Webhook command - makes an HTTP request to a URL.
        /// </summary>
        public WebhookOptions? Webhook { get; set; }

        /// <summary>
        /// Notify command - sends a notification via Slack/Teams/Discord webhook, Windows toast, or SMTP email.
        /// </summary>
        public NotifyOptions? Notify { get; set; }

        /// <summary>
        /// Parse command - parses device configuration text into structured JSON data.
        /// </summary>
        public ParseOptions? Parse { get; set; }

        /// <summary>
        /// Choose command - prompts user to select one option from a list.
        /// </summary>
        public ChooseOptions? Choose { get; set; }

        /// <summary>
        /// Multiselect command - prompts user to select multiple options from a list.
        /// </summary>
        public MultiselectOptions? Multiselect { get; set; }

        /// <summary>
        /// Confirm command - prompts user with a yes/no question.
        /// </summary>
        public ConfirmOptions? Confirm { get; set; }

        /// <summary>
        /// Interactive command - opens an in-app SSH terminal and blocks until closed.
        /// </summary>
        public InteractiveOptions? Interactive { get; set; }

        /// <summary>
        /// Assert command - validates a condition and fails/warns if not met.
        /// </summary>
        public AssertOptions? Assert { get; set; }

        /// <summary>
        /// Switch command - dispatches execution based on a value matching cases.
        /// Shorthand: "switch: ${var}" sets the value to match against.
        /// </summary>
        public string? Switch { get; set; }

        /// <summary>
        /// Cases for the switch command. Each case has a value to match and steps to execute.
        /// </summary>
        public List<SwitchCase>? Cases { get; set; }

        /// <summary>
        /// Parallel command - executes multiple steps concurrently.
        /// </summary>
        public ParallelOptions? Parallel { get; set; }

        /// <summary>
        /// Call command - invokes a local or imported subroutine.
        /// </summary>
        public CallOptions? Call { get; set; }

        /// <summary>
        /// Table command - formats data into aligned columns for display.
        /// </summary>
        public TableOptions? Table { get; set; }

        /// <summary>
        /// Local command - executes a command on the local machine.
        /// </summary>
        public LocalCmdOptions? LocalCmd { get; set; }

        /// <summary>
        /// Vault command - reads, writes, or patches secrets from HashiCorp Vault.
        /// </summary>
        public VaultStepOptions? Vault { get; set; }

        /// <summary>
        /// Break command - exits the current loop.
        /// </summary>
        public bool BreakLoop { get; set; }

        /// <summary>
        /// Continue command - skips to the next loop iteration.
        /// </summary>
        public bool ContinueLoop { get; set; }

        /// <summary>
        /// Return command - exits the current subroutine early when true.
        /// </summary>
        public bool ReturnFromSubroutine { get; set; }

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
        /// Whether on_error is set to "continue".
        /// </summary>
        public bool IsOnErrorContinue =>
            string.Equals(OnError, "continue", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Number of retry attempts on step failure (0 = no retry).
        /// </summary>
        public int? Retry { get; set; }

        /// <summary>
        /// Delay in seconds between retry attempts (default: 1).
        /// </summary>
        public int? RetryDelay { get; set; }

        /// <summary>
        /// When true, send treats detectable non-zero POSIX shell exit status as step failure.
        /// </summary>
        public bool FailOnNonZero { get; set; }

        /// <summary>
        /// Expect/reply pairs for interactive send commands.
        /// After sending the command, each pair waits for the expect pattern then sends the reply.
        /// </summary>
        public List<RespondPair>? Respond { get; set; }

        /// <summary>
        /// Steps to execute if condition is true (for if/foreach/while).
        /// </summary>
        public List<ScriptStep>? Then { get; set; }

        /// <summary>
        /// Steps to execute if condition is false (for if).
        /// </summary>
        public List<ScriptStep>? Else { get; set; }

        /// <summary>
        /// Ordered else-if branches evaluated when the primary if condition is false.
        /// </summary>
        public List<ElifBranch>? Elif { get; set; }

        /// <summary>
        /// Steps to execute in loop body (for foreach/while).
        /// </summary>
        public List<ScriptStep>? Do { get; set; }

        /// <summary>
        /// Filter condition for foreach (optional).
        /// </summary>
        public string? When { get; set; }

        /// <summary>
        /// Maximum iteration cap for while loops (optional).
        /// </summary>
        public int? MaxIterations { get; set; }

        /// <summary>
        /// Steps to execute inside a structured try block.
        /// </summary>
        public List<ScriptStep>? Try { get; set; }

        /// <summary>
        /// Steps to execute when the try block fails.
        /// </summary>
        public List<ScriptStep>? Catch { get; set; }

        /// <summary>
        /// Steps to execute after try/catch regardless of outcome.
        /// </summary>
        public List<ScriptStep>? Finally { get; set; }

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
            if (Try != null || Catch != null || Finally != null) return StepType.Try;
            if (BreakLoop) return StepType.Break;
            if (ContinueLoop) return StepType.Continue;
            if (Readfile != null) return StepType.Readfile;
            if (Writefile != null) return StepType.Writefile;
            if (Exists != null) return StepType.Exists;
            if (PlaySound != null) return StepType.PlaySound;
            if (Input != null) return StepType.Input;
            if (UpdateColumn != null) return StepType.UpdateColumn;
            if (UpdateEnvironment != null) return StepType.UpdateEnvironment;
            if (Log != null) return StepType.Log;
            if (SetHistoryLabel != null) return StepType.SetHistoryLabel;
            if (Http != null) return StepType.Http;
            if (BrowserCallbackCapture != null) return StepType.BrowserCallbackCapture;
            if (Ping != null) return StepType.Ping;
            if (Dns != null) return StepType.Dns;
            if (Portcheck != null) return StepType.Portcheck;
            if (Sftp != null) return StepType.Sftp;
            if (Webhook != null) return StepType.Webhook;
            if (Notify != null) return StepType.Notify;
            if (Parse != null) return StepType.Parse;
            if (Choose != null) return StepType.Choose;
            if (Multiselect != null) return StepType.Multiselect;
            if (Confirm != null) return StepType.Confirm;
            if (Interactive != null) return StepType.Interactive;
            if (Assert != null) return StepType.Assert;
            if (!string.IsNullOrEmpty(Switch)) return StepType.Switch;
            if (Parallel != null) return StepType.Parallel;
            if (Call != null) return StepType.Call;
            if (Table != null) return StepType.Table;
            if (LocalCmd != null) return StepType.LocalCmd;
            if (Vault != null) return StepType.Vault;
            if (ReturnFromSubroutine) return StepType.Return;
            if (DeclaredStepType != StepType.Unknown) return DeclaredStepType;
            return StepType.Unknown;
        }
    }

    /// <summary>
    /// Represents a single elif branch under an if step.
    /// </summary>
    public class ElifBranch
    {
        /// <summary>
        /// Line number in the original YAML for error reporting.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The elif condition expression.
        /// </summary>
        public string If { get; set; } = string.Empty;

        /// <summary>
        /// Steps to execute when the elif condition is true.
        /// </summary>
        public List<ScriptStep> Then { get; set; } = new();
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

        /// <summary>
        /// When true (default), the step fails if the regex produces zero matches.
        /// When false, zero matches silently set the target variable(s) to empty strings.
        /// </summary>
        public bool Required { get; set; } = true;
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
        /// Whether to prompt the operator to choose the file at runtime.
        /// </summary>
        public bool SelectFile { get; set; }

        /// <summary>
        /// Optional custom prompt message shown when selecting a file at runtime.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Optional comma-separated list of allowed file extensions for the selected/read path.
        /// </summary>
        public string? FileExt { get; set; }

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
    /// Options for the exists command.
    /// </summary>
    public class ExistsOptions
    {
        /// <summary>
        /// Local path to evaluate after variable/environment expansion.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Variable name to receive the boolean existence result.
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// Type filter: any (default), file, or directory.
        /// </summary>
        public string Type { get; set; } = "any";
    }

    /// <summary>
    /// Options for the playsound command.
    /// </summary>
    public class PlaySoundOptions
    {
        /// <summary>
        /// Local audio file path to play after variable/environment expansion.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Whether to block until playback completes (default: true).
        /// </summary>
        public bool Wait { get; set; } = true;

        /// <summary>
        /// Playback volume as a percentage from 0 to 100 (default: 100).
        /// </summary>
        public int Volume { get; set; } = 100;

        /// <summary>
        /// Optional maximum wait time in seconds when Wait is true.
        /// Supports fractional values (for example, 0.25).
        /// </summary>
        public double? MaxSeconds { get; set; }

        /// <summary>
        /// Optional variable name to capture success state and metadata.
        /// </summary>
        public string? Into { get; set; }
    }

    /// <summary>
    /// Options for the input command.
    /// </summary>
    public class InputOptions
    {
        /// <summary>
        /// Optional dialog window title.
        /// </summary>
        public string? Title { get; set; }

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
    /// Represents a single option in a choose/multiselect list.
    /// Can be a simple string (label = value) or a label/value pair.
    /// </summary>
    public class ChoiceOption
    {
        /// <summary>
        /// Display label shown to the user.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Value stored in the variable. Defaults to Label if not specified.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>
    /// Options for the choose command (single selection from a list).
    /// </summary>
    public class ChooseOptions
    {
        /// <summary>
        /// Optional dialog window title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Prompt text to display to the user.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Variable name to store the selected value.
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// List of options to choose from.
        /// </summary>
        public List<ChoiceOption> Options { get; set; } = new();

        /// <summary>
        /// Optional variable/expression source for options.
        /// When set, the value should resolve to a list (e.g., List&lt;string&gt;).
        /// </summary>
        public string? OptionsFrom { get; set; }

        /// <summary>
        /// Default selection (matched against option values).
        /// </summary>
        public string? Default { get; set; }
    }

    /// <summary>
    /// Options for the multiselect command (multiple selections from a list).
    /// </summary>
    public class MultiselectOptions
    {
        /// <summary>
        /// Optional dialog window title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Prompt text to display to the user.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Variable name to store the selected values.
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// List of options to choose from.
        /// </summary>
        public List<ChoiceOption> Options { get; set; } = new();

        /// <summary>
        /// Optional variable/expression source for options.
        /// When set, the value should resolve to a list (e.g., List&lt;string&gt;).
        /// </summary>
        public string? OptionsFrom { get; set; }

        /// <summary>
        /// Minimum number of selections required.
        /// </summary>
        public int? Min { get; set; }

        /// <summary>
        /// Maximum number of selections allowed.
        /// </summary>
        public int? Max { get; set; }
    }

    /// <summary>
    /// Options for the confirm command (yes/no boolean prompt).
    /// </summary>
    public class ConfirmOptions
    {
        /// <summary>
        /// Optional dialog window title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Prompt text to display to the user.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Variable name to store the result ("true" or "false").
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// Default button: true = Yes focused, false = No focused.
        /// </summary>
        public bool Default { get; set; }
    }

    /// <summary>
    /// Options for the interactive command (embedded SSH terminal).
    /// </summary>
    public class InteractiveOptions
    {
        /// <summary>
        /// Session model. Separate opens a new connection; shared attaches to the current session.
        /// </summary>
        public InteractiveSessionMode Session { get; set; } = InteractiveSessionMode.Separate;

        /// <summary>
        /// Optional custom window title for the interactive terminal form.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Optional long-running command to execute automatically when the terminal opens.
        /// When set, interactive capture mode is enabled.
        /// </summary>
        public string? Command { get; set; }

        /// <summary>
        /// Optional variable name used to capture the terminal transcript when capture mode completes.
        /// </summary>
        public string? Capture { get; set; }

        /// <summary>
        /// Optional safety timeout (seconds) that auto-sends Ctrl+C in capture mode.
        /// </summary>
        public int? MaxSeconds { get; set; }

        /// <summary>
        /// Optional safety limit (lines) that auto-sends Ctrl+C in capture mode.
        /// </summary>
        public int? MaxLines { get; set; }

        /// <summary>
        /// Optional interactive window width in pixels for separate sessions.
        /// Defaults to 980 when not specified.
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Optional interactive window height in pixels for separate sessions.
        /// Defaults to 620 when not specified.
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// Optional terminal width in character columns for separate sessions.
        /// Deprecated in favor of width/height pixel sizing.
        /// </summary>
        public int? Columns { get; set; }

        /// <summary>
        /// Optional terminal height in character rows for separate sessions.
        /// Deprecated in favor of width/height pixel sizing.
        /// </summary>
        public int? Rows { get; set; }

        /// <summary>
        /// When true, captured terminal chunks are mirrored into the main script output stream.
        /// </summary>
        public bool MirrorOutput { get; set; }

        /// <summary>
        /// When true (default), the interactive terminal window is shown.
        /// Set false for headless capture runs controlled by timeout/natural completion.
        /// </summary>
        public bool ShowWindow { get; set; } = true;
    }

    /// <summary>
    /// Session model for interactive terminal steps.
    /// </summary>
    public enum InteractiveSessionMode
    {
        Separate,
        Shared
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
    /// Options for the updateenvironment command.
    /// </summary>
    public class UpdateEnvironmentOptions
    {
        /// <summary>
        /// The environment variable key to update.
        /// </summary>
        public string Variable { get; set; } = string.Empty;

        /// <summary>
        /// The value to persist. Can be a literal string or a variable reference like ${varname}.
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
    /// Options for the sethistorylabel command.
    /// </summary>
    public class SetHistoryLabelOptions
    {
        /// <summary>
        /// The label text to attach. Supports {{variable}} substitution.
        /// Empty or whitespace clears the label.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// How the label change is applied.
        /// Supported values: replace, append, prepend, clear.
        /// </summary>
        public string Mode { get; set; } = HistoryLabelOperation.ReplaceMode;

        /// <summary>
        /// Separator inserted between labels when using append or prepend.
        /// </summary>
        public string Separator { get; set; } = string.Empty;

        /// <summary>
        /// When true, the history entry shows only the label (IP hidden).
        /// When false, it shows "IP - Label".
        /// When omitted for append/prepend, the current replace-address state is preserved.
        /// </summary>
        public bool? Replace { get; set; }
    }

    /// <summary>
    /// Options for the http command.
    /// </summary>
    public class HttpOptions
    {
        /// <summary>
        /// Target URL (must be absolute http:// or https://).
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = "GET";

        /// <summary>
        /// Request body. Supports variable substitution.
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// Optional HTTP headers.
        /// </summary>
        public Dictionary<string, string>? Headers { get; set; }

        /// <summary>
        /// Variable name to capture response body into.
        /// </summary>
        public string? Into { get; set; }

        /// <summary>
        /// Request timeout in seconds.
        /// </summary>
        public int Timeout { get; set; } = 30;

        /// <summary>
        /// Whether to follow HTTP redirects.
        /// </summary>
        public bool FollowRedirects { get; set; } = true;

        /// <summary>
        /// If true, non-2xx responses do not fail the step.
        /// </summary>
        public bool AllowFailure { get; set; }

        /// <summary>
        /// Whether to validate TLS certificates.
        /// </summary>
        public bool VerifyTls { get; set; } = true;

        /// <summary>
        /// Set false by parser when verify_tls value type is invalid.
        /// </summary>
        public bool VerifyTlsTypeValid { get; set; } = true;

        /// <summary>
        /// Auth mode: none, basic, bearer.
        /// </summary>
        public string Auth { get; set; } = "none";

        /// <summary>
        /// Basic auth username (required when auth is basic).
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Basic auth password (required when auth is basic).
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Bearer token (required when auth is bearer).
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Optional content-type shorthand (json/form/text/xml).
        /// </summary>
        public string? ContentType { get; set; }
    }

    /// <summary>
    /// Options for the browser_callback_capture command.
    /// </summary>
    public class BrowserCallbackCaptureOptions
    {
        /// <summary>
        /// The initial URL to open in the system browser.
        /// </summary>
        public string StartUrl { get; set; } = string.Empty;

        /// <summary>
        /// Local callback path to handle browser redirects/posts (for example /oauth_callback).
        /// </summary>
        public string CallbackPath { get; set; } = "/oauth_callback";

        /// <summary>
        /// Local loopback listener port.
        /// </summary>
        public int LocalPort { get; set; } = 8086;

        /// <summary>
        /// Capture mode: auto, fragment, query, post_body.
        /// </summary>
        public string CaptureMode { get; set; } = "auto";

        /// <summary>
        /// Browser launch surface: external or webview2.
        /// </summary>
        public string BrowserMode { get; set; } = "external";

        /// <summary>
        /// When greater than zero in WebView2 mode, keep the embedded browser hidden until the callback is still pending after this many seconds.
        /// </summary>
        public int ShowAfterSeconds { get; set; }

        /// <summary>
        /// Variable name prefix used to persist captured values.
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// Optional list of required callback fields.
        /// </summary>
        public List<string>? RequiredFields { get; set; }

        /// <summary>
        /// Wait timeout in seconds.
        /// </summary>
        public int Timeout { get; set; } = 300;

        /// <summary>
        /// When true, open the start_url in the default browser.
        /// </summary>
        public bool OpenBrowser { get; set; } = true;

        /// <summary>
        /// When true, successful callback pages attempt to close themselves after completion.
        /// </summary>
        public bool AutoCloseBrowser { get; set; } = true;

        /// <summary>
        /// Optional message shown on callback completion page.
        /// </summary>
        public string? CompletionMessage { get; set; }

        /// <summary>
        /// Optional message shown on callback failure page.
        /// </summary>
        public string? FailureMessage { get; set; }

        /// <summary>
        /// When true, suppresses the success summary output line after capture.
        /// </summary>
        public bool Quiet { get; set; } = true;
    }

    /// <summary>
    /// Options for the ping command.
    /// </summary>
    public class PingOptions
    {
        /// <summary>
        /// Target host or IP.
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Number of probes.
        /// </summary>
        public int Count { get; set; } = 4;

        /// <summary>
        /// Timeout in milliseconds per probe.
        /// </summary>
        public int Timeout { get; set; } = 3000;

        /// <summary>
        /// Variable name to capture status/metrics into.
        /// </summary>
        public string? Into { get; set; }
    }

    /// <summary>
    /// Options for the dns command.
    /// </summary>
    public class DnsOptions
    {
        /// <summary>
        /// Target host, record name, or IP (for PTR).
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// DNS type: A, AAAA, PTR.
        /// </summary>
        public string Type { get; set; } = "A";

        /// <summary>
        /// Lookup timeout in seconds.
        /// </summary>
        public int Timeout { get; set; } = 10;

        /// <summary>
        /// Variable name to capture DNS results into.
        /// </summary>
        public string? Into { get; set; }
    }

    /// <summary>
    /// Options for the portcheck command.
    /// </summary>
    public class PortcheckOptions
    {
        /// <summary>
        /// Target host or IP.
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Target TCP port.
        /// </summary>
        public int Port { get; set; } = 22;

        /// <summary>
        /// Connection timeout in seconds.
        /// </summary>
        public int Timeout { get; set; } = 5;

        /// <summary>
        /// Variable name to capture check result into.
        /// </summary>
        public string? Into { get; set; }
    }

    /// <summary>
    /// Options for the sftp command.
    /// </summary>
    public class SftpOptions
    {
        /// <summary>
        /// Transfer action: upload or download.
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Local source/destination path.
        /// </summary>
        public string LocalPath { get; set; } = string.Empty;

        /// <summary>
        /// Remote source/destination path.
        /// </summary>
        public string RemotePath { get; set; } = string.Empty;

        /// <summary>
        /// Optional host override (defaults to current host context).
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// Optional port override (defaults to current host port or 22).
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Optional username override (defaults to current host context).
        /// </summary>
        public string? Username { get; set; }

        /// <summary>
        /// Optional password override (defaults to current host context).
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Whether existing destination files may be overwritten.
        /// </summary>
        public bool Overwrite { get; set; } = true;

        /// <summary>
        /// Transfer timeout in seconds.
        /// </summary>
        public int Timeout { get; set; } = 120;

        /// <summary>
        /// Variable name to capture transfer result into.
        /// </summary>
        public string? Into { get; set; }
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
    /// Options for the notify command. Channel is inferred from the referenced profile's Kind
    /// unless an explicit <see cref="Channel"/> override is set.
    /// </summary>
    public class NotifyOptions
    {
        /// <summary>
        /// Named notification profile (e.g. "ops-alerts"). Required for webhook and SMTP channels.
        /// </summary>
        public string? Profile { get; set; }

        /// <summary>
        /// Explicit channel override: "slack", "teams", "discord", "toast", "smtp".
        /// Must match the profile's kind when both are set. Required for "toast" (no profile exists).
        /// </summary>
        public string? Channel { get; set; }

        /// <summary>
        /// Notification title. Optional — toast/email render it prominently; webhook channels fold it in.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Notification message body. Required.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Severity level: "info" (default), "warn", "error", "success".
        /// Maps to channel-native color/icon/subject-prefix.
        /// </summary>
        public string Level { get; set; } = "info";

        /// <summary>
        /// Optional mentions for webhook channels. Slack and Discord accept channel-specific shorthand;
        /// Teams accepts typed forms such as "upn:user@contoso.com|User" and "entra:<guid>|User".
        /// Ignored for toast/smtp.
        /// </summary>
        public List<string>? Mention { get; set; }

        /// <summary>
        /// Variable name to capture the notification result ({sent, channel, status_code, error}).
        /// </summary>
        public string? Into { get; set; }

        /// <summary>
        /// Error handling: "stop" (default) aborts the script on failure; "continue" logs and proceeds.
        /// </summary>
        public string? OnError { get; set; }
    }

    /// <summary>
    /// Options for the parse command.
    /// </summary>
    public class ParseOptions
    {
        /// <summary>
        /// The configuration format to parse (e.g., "fortigate").
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Source variable containing the raw configuration text.
        /// </summary>
        public string From { get; set; } = string.Empty;

        /// <summary>
        /// Variable name to store the parsed configuration result.
        /// </summary>
        public string Into { get; set; } = string.Empty;

        /// <summary>
        /// Optional list of section paths to parse (e.g., "system interface", "firewall policy").
        /// If not specified, the entire configuration is parsed.
        /// </summary>
        public List<string>? Sections { get; set; }
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
        Try,
        Break,
        Continue,
        Readfile,
        Writefile,
        Exists,
        PlaySound,
        Input,
        UpdateColumn,
        UpdateEnvironment,
        Log,
        Http,
        BrowserCallbackCapture,
        Ping,
        Dns,
        Portcheck,
        Sftp,
        Webhook,
        Parse,
        Choose,
        Multiselect,
        Confirm,
        Interactive,
        Assert,
        Switch,
        Parallel,
        Call,
        Return,
        Table,
        LocalCmd,
        Vault,
        SetHistoryLabel,
        Notify
    }

    /// <summary>
    /// Options for the call command.
    /// </summary>
    public class CallOptions
    {
        /// <summary>
        /// Fully-qualified or local subroutine reference to invoke.
        /// </summary>
        public string Subroutine { get; set; } = string.Empty;

        /// <summary>
        /// Caller expressions resolved before entering the child scope.
        /// </summary>
        public Dictionary<string, string> Args { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Caller variable names that receive declared subroutine outputs.
        /// </summary>
        public Dictionary<string, string> Out { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Options for the assert command.
    /// </summary>
    public class AssertOptions
    {
        /// <summary>
        /// The condition expression to evaluate.
        /// </summary>
        public string Condition { get; set; } = string.Empty;

        /// <summary>
        /// Optional message to display on assertion failure.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Severity: "error" (stops execution) or "warning" (continues with warning).
        /// </summary>
        public string Severity { get; set; } = "error";
    }

    /// <summary>
    /// Represents a single case branch in a switch command.
    /// </summary>
    public class SwitchCase
    {
        /// <summary>
        /// Line number in the original YAML for error reporting.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// The value to match against the switch expression.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Steps to execute when this case matches.
        /// </summary>
        public List<ScriptStep> Do { get; set; } = new();
    }

    /// <summary>
    /// Represents an expect/reply pair for interactive send commands.
    /// </summary>
    public class RespondPair
    {
        /// <summary>
        /// Pattern to wait for in the output.
        /// </summary>
        public string Expect { get; set; } = string.Empty;

        /// <summary>
        /// Text to send when the expect pattern is matched.
        /// </summary>
        public string Reply { get; set; } = string.Empty;
    }

    /// <summary>
    /// Options for the parallel command.
    /// </summary>
    public class ParallelOptions
    {
        /// <summary>
        /// Steps to execute concurrently.
        /// </summary>
        public List<ScriptStep> Steps { get; set; } = new();

        /// <summary>
        /// Maximum number of concurrent steps (0 = unlimited).
        /// </summary>
        public int MaxConcurrent { get; set; }
    }

    /// <summary>
    /// Options for the table command.
    /// </summary>
    public class TableOptions
    {
        /// <summary>
        /// Variable reference containing the data to display.
        /// </summary>
        public string Data { get; set; } = string.Empty;

        /// <summary>
        /// Optional column definitions for formatting.
        /// </summary>
        public List<TableColumn>? Columns { get; set; }

        /// <summary>
        /// Variable name to capture the formatted table output into.
        /// </summary>
        public string? Into { get; set; }

        /// <summary>
        /// Default column alignment: "left", "right", or "center".
        /// </summary>
        public string Align { get; set; } = "left";

        /// <summary>
        /// Whether to display a header row (default: true).
        /// </summary>
        public bool ShowHeader { get; set; } = true;
    }

    /// <summary>
    /// Defines a single column in a table command.
    /// </summary>
    public class TableColumn
    {
        /// <summary>
        /// Column header text.
        /// </summary>
        public string Header { get; set; } = string.Empty;

        /// <summary>
        /// Field name or property to extract from each data item.
        /// </summary>
        public string? Field { get; set; }

        /// <summary>
        /// Column alignment: "left", "right", or "center".
        /// </summary>
        public string Align { get; set; } = "left";

        /// <summary>
        /// Fixed column width (null = auto-size to content).
        /// </summary>
        public int? Width { get; set; }
    }

    public class LocalCmdOptions
    {
        public string? Command { get; set; }

        public string Shell { get; set; } = "powershell";

        public string? ShellPath { get; set; }

        public List<string> Args { get; set; } = new();

        public Dictionary<string, string>? Env { get; set; }

        public string? WorkingDir { get; set; }

        public bool Interactive { get; set; }

        public bool KeepOpen { get; set; }

        public string RunMode { get; set; } = "foreground";

        public string Lifetime { get; set; } = "detached";

        public bool LifetimeSpecified { get; set; }

        public bool KillOnCancel { get; set; }

        public bool FailOnNonZero { get; set; } = true;

        public List<int> SuccessCodes { get; set; } = new() { 0 };

        public int MaxOutputBytes { get; set; } = 1024 * 1024;

        public string Confirm { get; set; } = "always";

        public bool Quiet { get; set; }

        public bool Suppress { get; set; }

        public string? Title { get; set; }

        public string? Into { get; set; }
    }
}
