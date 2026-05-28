using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.UI;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Reads a text file line by line into a list variable.
    /// </summary>
    public class ReadFileCommand : IScriptCommand
    {
        private const string ManualOnlySelectionMessage = "Readfile file selection is only available during manual main-window runs.";
        private readonly Func<ReadFileOpenPathRequest, CancellationToken, Task<string?>> _openPathPrompt;

        internal static Func<ReadFileOpenPathRequest, IWin32Window?, (DialogResult dialogResult, string? fileName)>? OpenFileDialogOverrideForTests { get; set; }

        public ReadFileCommand(Func<ReadFileOpenPathRequest, CancellationToken, Task<string?>>? openPathPrompt = null)
        {
            _openPathPrompt = openPathPrompt ?? PromptForOpenPathAsync;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Readfile == null)
                return CommandResult.Fail("Readfile command has no options");

            var shouldReadContents = ShouldReadContents(step);
            var readOutputVariable = shouldReadContents ? step.Readfile.Into : null;
            var pathOutputVariable = ResolvePathOutputVariable(step);

            if (!step.Readfile.SelectFile && string.IsNullOrEmpty(step.Readfile.Path))
                return CommandResult.Fail("Readfile command requires a 'path' property");

            if (shouldReadContents && string.IsNullOrEmpty(step.Readfile.Into))
                return CommandResult.Fail("Readfile command requires an 'into' property");

            if (!shouldReadContents && string.IsNullOrWhiteSpace(pathOutputVariable))
                return CommandResult.Fail("Readfile command requires a 'path_into' property when 'path_only' is true");

            if (shouldReadContents &&
                !string.IsNullOrWhiteSpace(pathOutputVariable) &&
                string.Equals(pathOutputVariable, step.Readfile.Into, StringComparison.OrdinalIgnoreCase))
            {
                return CommandResult.Fail("Readfile 'path_into' must differ from 'into' unless 'path_only' is true");
            }

            try
            {
                var allowedExtensions = ResolveAllowedExtensions(step, context, out var extensionConfigError);
                if (!string.IsNullOrWhiteSpace(extensionConfigError))
                {
                    context.EmitOutput(extensionConfigError!, ScriptOutputType.Error);

                    if (IsContinueOnError(step))
                        return CommandResult.Ok(extensionConfigError);

                    return CommandResult.Fail(extensionConfigError!);
                }

                var filePath = await ResolveFilePathAsync(step, context, allowedExtensions, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return HandleSelectionCancellation(
                        context,
                        readOutputVariable,
                        pathOutputVariable,
                        "Readfile file selection cancelled by user",
                        ScriptOutputType.Warning);
                }

                if (!ReadFileSelectionOptions.IsPathAllowed(filePath, allowedExtensions))
                {
                    var extensionError = $"File '{filePath}' does not match the allowed file types ({ReadFileSelectionOptions.FormatAllowedExtensions(allowedExtensions)}).";
                    context.EmitOutput(extensionError, ScriptOutputType.Error);

                    if (IsContinueOnError(step))
                        return CommandResult.Ok(extensionError);

                    return CommandResult.Fail(extensionError);
                }

                // Validate path for security
                if (!ScriptFileAccessValidator.ValidateReadPath(filePath, out var pathError))
                {
                    context.EmitOutput(pathError!, ScriptOutputType.Error);

                    if (IsContinueOnError(step))
                        return CommandResult.Ok(pathError);

                    return CommandResult.Fail(pathError!);
                }

                SetPathOutput(context, pathOutputVariable, filePath);

                // Check if file exists
                if (!File.Exists(filePath))
                {
                    if (!string.IsNullOrWhiteSpace(readOutputVariable))
                    {
                        context.SetVariable(readOutputVariable, new List<string>());
                    }

                    context.EmitOutput(BuildMissingFileMessage(filePath, readOutputVariable, pathOutputVariable), ScriptOutputType.Warning);

                    if (IsContinueOnError(step))
                        return CommandResult.Ok();

                    return CommandResult.Fail($"File not found: {filePath}");
                }

                if (!shouldReadContents)
                {
                    context.EmitOutput($"Captured resolved file path '{filePath}' into '{pathOutputVariable}'", ScriptOutputType.Debug);
                    return CommandResult.Ok();
                }

                // Get encoding
                var encoding = GetEncoding(step.Readfile.Encoding);

                // Read lines with max limit
                var maxLines = step.Readfile.MaxLines > 0 ? step.Readfile.MaxLines : int.MaxValue;
                var lines = new List<string>();
                var lineCount = 0;
                var truncated = false;

                using (var reader = new StreamReader(filePath, encoding))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var processedLine = step.Readfile.TrimLines ? line.Trim() : line;

                        // Skip empty lines if configured
                        if (step.Readfile.SkipEmptyLines && string.IsNullOrEmpty(processedLine))
                            continue;

                        lines.Add(processedLine);
                        lineCount++;

                        // Check max lines limit
                        if (lineCount >= maxLines)
                        {
                            truncated = true;
                            break;
                        }
                    }
                }

                // Store the lines in the variable
                context.SetVariable(readOutputVariable!, lines);

                var message = $"Read {lines.Count} lines from '{filePath}' into '{readOutputVariable}'";
                if (truncated)
                    message += $" (truncated at {maxLines} lines)";

                if (!string.IsNullOrWhiteSpace(pathOutputVariable))
                    message += $"; resolved path stored in '{pathOutputVariable}'";

                context.EmitOutput(message, ScriptOutputType.Debug);

                return CommandResult.Ok();
            }
            catch (InvalidOperationException ex) when (string.Equals(ex.Message, ManualOnlySelectionMessage, StringComparison.Ordinal))
            {
                return HandleSelectionFailure(context, readOutputVariable, pathOutputVariable, ex.Message, ScriptOutputType.Error, IsContinueOnError(step));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMsg = $"Access denied reading file: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (IsContinueOnError(step))
                    return CommandResult.Suppressed(errorMsg);

                return CommandResult.Fail(errorMsg);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error reading file: {ex.Message}";
                context.EmitOutput(errorMsg, ScriptOutputType.Error);

                if (IsContinueOnError(step))
                    return CommandResult.Suppressed(errorMsg);

                return CommandResult.Fail(errorMsg);
            }
        }

        private static IReadOnlyList<string> ResolveAllowedExtensions(
            ScriptStep step,
            ScriptContext context,
            out string? errorMessage)
        {
            var rawExtensions = Environment.ExpandEnvironmentVariables(
                context.SubstituteVariables(step.Readfile!.FileExt ?? string.Empty));

            var allowedExtensions = ReadFileSelectionOptions.NormalizeAllowedExtensions(rawExtensions);
            if (!string.IsNullOrWhiteSpace(rawExtensions) && allowedExtensions.Count == 0)
            {
                errorMessage = "Readfile 'fileext' must include at least one extension like 'txt,json'.";
                return Array.Empty<string>();
            }

            errorMessage = null;
            return allowedExtensions;
        }

        private async Task<string?> ResolveFilePathAsync(
            ScriptStep step,
            ScriptContext context,
            IReadOnlyList<string> allowedExtensions,
            CancellationToken cancellationToken)
        {
            var requestedPath = Environment.ExpandEnvironmentVariables(
                context.SubstituteVariables(step.Readfile!.Path ?? string.Empty));

            if (!step.Readfile.SelectFile)
            {
                if (string.IsNullOrWhiteSpace(requestedPath))
                    return requestedPath;

                return Path.GetFullPath(requestedPath);
            }

            if (!context.AllowFileSelectionDialogs)
                throw new InvalidOperationException(ManualOnlySelectionMessage);

            var promptRequest = new ReadFileOpenPathRequest(
                requestedPath,
                ResolvePromptMessage(step, context),
                allowedExtensions,
                ResolveAutoBrowse(step.Readfile));

            var promptedPath = await _openPathPrompt(promptRequest, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(promptedPath))
                return null;

            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(promptedPath));
        }

        private static string ResolvePromptMessage(ScriptStep step, ScriptContext context)
        {
            var rawMessage = Environment.ExpandEnvironmentVariables(
                context.SubstituteVariables(step.Readfile!.Message ?? string.Empty));
            return ReadFileSelectionOptions.ResolvePromptMessage(rawMessage);
        }

        private static bool ResolveAutoBrowse(ReadfileOptions options)
        {
            if (options.AutoBrowse.HasValue)
                return options.AutoBrowse.Value;

            return options.SelectFile && options.PathOnly;
        }

        private static Task<string?> PromptForOpenPathAsync(ReadFileOpenPathRequest request, CancellationToken cancellationToken)
        {
            if (request.AutoBrowse)
            {
                return ScriptPromptDialogRunner.RunOnUiThreadAsync(
                    owner => PromptForOpenPathWithNativeDialog(request, owner),
                    cancellationToken);
            }

            return ScriptPromptDialogRunner.ShowAsync<ScriptReadFileOpenPathDialog, string?>(
                () => new ScriptReadFileOpenPathDialog(request.SuggestedPath, request.PromptMessage, request.AllowedExtensions),
                dialog => dialog.DialogResult == DialogResult.OK ? dialog.SelectedPath : null,
                cancellationToken);
        }

        private static string? PromptForOpenPathWithNativeDialog(ReadFileOpenPathRequest request, IWin32Window? owner)
        {
            var (dialogResult, fileName) = ShowOpenFileDialog(request, owner);
            if (dialogResult != DialogResult.OK || string.IsNullOrWhiteSpace(fileName))
                return null;

            return fileName;
        }

        private static (DialogResult dialogResult, string? fileName) ShowOpenFileDialog(
            ReadFileOpenPathRequest request,
            IWin32Window? owner)
        {
            var dialogOverride = OpenFileDialogOverrideForTests;
            if (dialogOverride != null)
                return dialogOverride(request, owner);

            using var dialog = new OpenFileDialog
            {
                Title = request.PromptMessage,
                CheckFileExists = true,
                CheckPathExists = true,
                FileName = ReadFileSelectionOptions.GetSuggestedFileName(request.SuggestedPath),
                Filter = ReadFileSelectionOptions.BuildDialogFilter(request.AllowedExtensions)
            };

            var defaultExtension = ReadFileSelectionOptions.GetDefaultExtension(request.AllowedExtensions);
            if (!string.IsNullOrWhiteSpace(defaultExtension))
            {
                dialog.DefaultExt = defaultExtension;
            }

            var initialDirectory = ReadFileSelectionOptions.ResolveInitialDirectory(request.SuggestedPath);
            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            var result = owner == null
                ? dialog.ShowDialog()
                : dialog.ShowDialog(owner);

            return (result, result == DialogResult.OK ? dialog.FileName : null);
        }

        private static bool IsContinueOnError(ScriptStep step)
        {
            return string.Equals(step.OnError, "continue", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldReadContents(ScriptStep step)
        {
            return step.Readfile is not { PathOnly: true };
        }

        private static string? ResolvePathOutputVariable(ScriptStep step)
        {
            if (step.Readfile == null)
                return null;

            if (!string.IsNullOrWhiteSpace(step.Readfile.PathInto))
                return step.Readfile.PathInto.Trim();

            if (step.Readfile.PathOnly || string.IsNullOrWhiteSpace(step.Readfile.Into))
                return null;

            return step.Readfile.Into.Trim() + "_path";
        }

        private static void SetPathOutput(ScriptContext context, string? pathOutputVariable, string filePath)
        {
            if (string.IsNullOrWhiteSpace(pathOutputVariable))
                return;

            context.SetVariable(pathOutputVariable, filePath);
        }

        private static CommandResult HandleSelectionCancellation(
            ScriptContext context,
            string? readOutputVariable,
            string? pathOutputVariable,
            string message,
            ScriptOutputType outputType)
        {
            ResetOutputs(context, readOutputVariable, pathOutputVariable);
            context.EmitOutput(BuildResetMessage(message, readOutputVariable, pathOutputVariable), outputType);
            return CommandResult.Exit(ScriptExitStatus.Cancelled, message);
        }

        private static CommandResult HandleSelectionFailure(
            ScriptContext context,
            string? readOutputVariable,
            string? pathOutputVariable,
            string message,
            ScriptOutputType outputType,
            bool continueOnError)
        {
            ResetOutputs(context, readOutputVariable, pathOutputVariable);
            context.EmitOutput(BuildResetMessage(message, readOutputVariable, pathOutputVariable), outputType);

            if (continueOnError)
                return CommandResult.Suppressed(message);

            return CommandResult.Fail(message);
        }

        private static void ResetOutputs(
            ScriptContext context,
            string? readOutputVariable,
            string? pathOutputVariable)
        {
            if (!string.IsNullOrWhiteSpace(readOutputVariable))
                context.SetVariable(readOutputVariable, new List<string>());

            if (!string.IsNullOrWhiteSpace(pathOutputVariable))
                context.SetVariable(pathOutputVariable, string.Empty);
        }

        private static string BuildResetMessage(
            string message,
            string? readOutputVariable,
            string? pathOutputVariable)
        {
            var resets = new List<string>();

            if (!string.IsNullOrWhiteSpace(readOutputVariable))
                resets.Add($"variable '{readOutputVariable}' set to empty list");

            if (!string.IsNullOrWhiteSpace(pathOutputVariable))
                resets.Add($"variable '{pathOutputVariable}' set to empty string");

            if (resets.Count == 0)
                return message;

            return $"{message} - {string.Join(", ", resets)}";
        }

        private static string BuildMissingFileMessage(
            string filePath,
            string? readOutputVariable,
            string? pathOutputVariable)
        {
            var details = new List<string>();

            if (!string.IsNullOrWhiteSpace(readOutputVariable))
                details.Add($"variable '{readOutputVariable}' set to empty list");

            if (!string.IsNullOrWhiteSpace(pathOutputVariable))
                details.Add($"variable '{pathOutputVariable}' retained resolved path");

            return details.Count == 0
                ? $"File not found: {filePath}"
                : $"File not found: {filePath} - {string.Join(", ", details)}";
        }

        private static Encoding GetEncoding(string? encodingName)
        {
            return encodingName?.ToLowerInvariant() switch
            {
                "ascii" => Encoding.ASCII,
                "utf-16" or "unicode" => Encoding.Unicode,
                "utf-16be" => Encoding.BigEndianUnicode,
                "utf-32" => Encoding.UTF32,
                "latin1" or "iso-8859-1" => Encoding.Latin1,
                _ => Encoding.UTF8 // Default to UTF-8
            };
        }
    }

    public sealed class ReadFileOpenPathRequest
    {
        public ReadFileOpenPathRequest(string suggestedPath, string promptMessage, IReadOnlyList<string> allowedExtensions, bool autoBrowse = false)
        {
            SuggestedPath = suggestedPath ?? string.Empty;
            PromptMessage = ReadFileSelectionOptions.ResolvePromptMessage(promptMessage);
            AllowedExtensions = allowedExtensions?.ToArray() ?? Array.Empty<string>();
            AutoBrowse = autoBrowse;
        }

        public string SuggestedPath { get; }

        public string PromptMessage { get; }

        public IReadOnlyList<string> AllowedExtensions { get; }

        public bool AutoBrowse { get; }
    }

    internal static class ReadFileSelectionOptions
    {
        internal const string DefaultPromptMessage = "Select the file to read into this variable:";

        internal static string ResolvePromptMessage(string? promptMessage)
        {
            var trimmed = (promptMessage ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(trimmed)
                ? DefaultPromptMessage
                : trimmed;
        }

        internal static IReadOnlyList<string> NormalizeAllowedExtensions(string? rawExtensions)
        {
            var trimmed = (rawExtensions ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return Array.Empty<string>();

            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var token in trimmed.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var candidate = token.Trim();
                while (candidate.StartsWith('*'))
                {
                    candidate = candidate[1..].TrimStart();
                }

                candidate = candidate.TrimStart('.');
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var normalizedToken = "." + candidate;
                if (!normalizedToken.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_'))
                    continue;

                if (seen.Add(normalizedToken))
                {
                    normalized.Add(normalizedToken);
                }
            }

            return normalized;
        }

        internal static bool IsPathAllowed(string path, IReadOnlyList<string> allowedExtensions)
        {
            if (allowedExtensions == null || allowedExtensions.Count == 0)
                return true;

            var fileName = Path.GetFileName(path ?? string.Empty);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return allowedExtensions.Any(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        internal static string BuildDialogFilter(IReadOnlyList<string> allowedExtensions)
        {
            if (allowedExtensions == null || allowedExtensions.Count == 0)
                return "All Files (*.*)|*.*";

            var patterns = string.Join(";", allowedExtensions.Select(extension => "*" + extension));
            return $"Allowed Files ({FormatAllowedExtensions(allowedExtensions)})|{patterns}";
        }

        internal static string? GetDefaultExtension(IReadOnlyList<string> allowedExtensions)
        {
            if (allowedExtensions == null || allowedExtensions.Count == 0)
                return null;

            return allowedExtensions[0].TrimStart('.');
        }

        internal static string FormatAllowedExtensions(IReadOnlyList<string> allowedExtensions)
        {
            if (allowedExtensions == null || allowedExtensions.Count == 0)
                return "*.*";

            return string.Join(", ", allowedExtensions.Select(extension => "*" + extension));
        }

        internal static string BuildRestrictionErrorMessage(IReadOnlyList<string> allowedExtensions)
        {
            return $"File type must match one of: {FormatAllowedExtensions(allowedExtensions)}.";
        }

        internal static string GetSuggestedFileName(string path)
        {
            var trimmed = (path ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            if (Directory.Exists(trimmed))
                return string.Empty;

            var fileName = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName;
        }

        internal static string ResolveInitialDirectory(string path)
        {
            var trimmed = (path ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            try
            {
                if (Directory.Exists(trimmed))
                    return trimmed;

                var directory = Path.GetDirectoryName(trimmed);
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    return directory;
            }
            catch
            {
                // Fall back to Documents if the suggested path is invalid.
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    internal sealed class ScriptReadFileOpenPathDialog : Form
    {
        private readonly IReadOnlyList<string> _allowedExtensions;
        private readonly Label _lblPrompt;
        private readonly TextBox _txtPath;
        private readonly Label _lblError;
        private readonly Button _btnBrowse;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public string SelectedPath => _txtPath.Text.Trim();

        public ScriptReadFileOpenPathDialog(
            string suggestedPath,
            string? promptMessage = null,
            IReadOnlyList<string>? allowedExtensions = null)
        {
            _allowedExtensions = allowedExtensions?.ToArray() ?? Array.Empty<string>();
            var resolvedPromptMessage = ReadFileSelectionOptions.ResolvePromptMessage(promptMessage);

            Text = "Choose File To Read";
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            const int leftMargin = 15;
            const int topMargin = 15;
            const int contentWidth = 515;
            const int browseButtonWidth = 95;
            const int buttonWidth = 80;
            const int buttonHeight = 28;

            var promptHeight = MeasurePromptHeight(resolvedPromptMessage, contentWidth);
            _lblPrompt = new Label
            {
                Text = resolvedPromptMessage,
                Location = new Point(leftMargin, topMargin),
                Size = new Size(contentWidth, promptHeight),
                AutoSize = false
            };

            var textTop = _lblPrompt.Bottom + 6;
            _txtPath = new TextBox
            {
                Text = BuildDefaultPath(suggestedPath),
                Location = new Point(leftMargin, textTop),
                Size = new Size(420, 23)
            };

            _btnBrowse = new Button
            {
                Text = "Browse...",
                Size = new Size(browseButtonWidth, buttonHeight),
                Location = new Point(440, textTop - 3)
            };
            _btnBrowse.Click += (_, _) => BrowseForPath();

            _lblError = new Label
            {
                Text = string.Empty,
                Location = new Point(leftMargin, _txtPath.Bottom + 6),
                Size = new Size(525, 34),
                ForeColor = Color.Red,
                Visible = false
            };

            var buttonTop = _lblError.Bottom + 10;
            _btnOk = new Button
            {
                Text = "OK",
                Size = new Size(buttonWidth, buttonHeight),
                Location = new Point(365, buttonTop)
            };
            _btnOk.Click += BtnOk_Click;

            _btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(buttonWidth, buttonHeight),
                Location = new Point(450, buttonTop),
                DialogResult = DialogResult.Cancel
            };

            ClientSize = new Size(545, _btnOk.Bottom + 15);

            Controls.Add(_lblPrompt);
            Controls.Add(_txtPath);
            Controls.Add(_btnBrowse);
            Controls.Add(_lblError);
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            var isDark = mainForm != null && mainForm.BackColor.GetBrightness() < 0.2f;
            if (isDark)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.StyleButton(_btnBrowse, true);
                DialogTheme.StyleButton(_btnOk, true, isPrimary: true);
                DialogTheme.StyleButton(_btnCancel, true);
                DialogTheme.SetDarkTitleBar(this, true);
            }

            Load += (_, _) =>
            {
                if (isDark)
                    DialogTheme.ApplyNativeTheme(this, true);
                _txtPath.Focus();
                _txtPath.SelectAll();
            };

            _txtPath.TextChanged += (_, _) => _lblError.Visible = false;
        }

        private void BrowseForPath()
        {
            using var dialog = new OpenFileDialog
            {
                Title = Text,
                CheckFileExists = true,
                CheckPathExists = true,
                FileName = ReadFileSelectionOptions.GetSuggestedFileName(_txtPath.Text),
                Filter = ReadFileSelectionOptions.BuildDialogFilter(_allowedExtensions)
            };

            var defaultExtension = ReadFileSelectionOptions.GetDefaultExtension(_allowedExtensions);
            if (!string.IsNullOrWhiteSpace(defaultExtension))
            {
                dialog.DefaultExt = defaultExtension;
            }

            var initialDirectory = ReadFileSelectionOptions.ResolveInitialDirectory(_txtPath.Text);
            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _txtPath.Text = dialog.FileName;
            }
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (!TryAcceptSelectedPath())
                return;

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool TryAcceptSelectedPath()
        {
            var selectedPath = _txtPath.Text.Trim();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                _lblError.Text = "A file path is required.";
                _lblError.Visible = true;
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(selectedPath);
                if (!ReadFileSelectionOptions.IsPathAllowed(fullPath, _allowedExtensions))
                {
                    _lblError.Text = ReadFileSelectionOptions.BuildRestrictionErrorMessage(_allowedExtensions);
                    _lblError.Visible = true;
                    return false;
                }

                _txtPath.Text = fullPath;
            }
            catch (Exception ex)
            {
                _lblError.Text = ex.Message;
                _lblError.Visible = true;
                return false;
            }

            return true;
        }

        private static string BuildDefaultPath(string suggestedPath)
        {
            return (suggestedPath ?? string.Empty).Trim();
        }

        private int MeasurePromptHeight(string promptMessage, int width)
        {
            var measured = TextRenderer.MeasureText(
                promptMessage,
                Font,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            return Math.Max(34, measured.Height);
        }
    }
}
