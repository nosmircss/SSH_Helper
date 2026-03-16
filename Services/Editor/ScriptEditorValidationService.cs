using System.Text.RegularExpressions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;

namespace SSH_Helper.Services.Editor
{
    public sealed class ScriptEditorValidationService : IDisposable
    {
        private static readonly Regex LineMessageRegex =
            new(@"Line\s+(?<line>\d+)\s*:\s*(?<message>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex LooseLineRegex =
            new(@"line\s+(?<line>\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex TokenRegex =
            new(@"['""](?<token>[^'""]+)['""]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex DuplicateKeyRegex =
            new(@"^(?<indent>[ \t]*)(?<key>[A-Za-z_][A-Za-z0-9_-]*)\s*:",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SequenceItemRegex =
            new(@"^(?<indent>[ \t]*)-\s+",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly object _sync = new();
        private CancellationTokenSource? _pendingValidationCts;
        private readonly SemaphoreSlim _inFlightValidationGate = new(1, 1);

        public event EventHandler<IReadOnlyList<EditorDiagnostic>>? DiagnosticsUpdated;

        public int DebounceMilliseconds { get; set; } = 400;
        public bool ShowInlineWarnings { get; set; } = true;
        public bool EnableYamlHygieneWarnings { get; set; } = true;

        public void ApplySettings(CommandEditorSettings settings)
        {
            settings ??= new CommandEditorSettings();
            DebounceMilliseconds = Math.Clamp(settings.ValidationDebounceMs, CommandEditorSettings.MinValidationDebounceMs, CommandEditorSettings.MaxValidationDebounceMs);
            ShowInlineWarnings = settings.ShowInlineWarnings;
            EnableYamlHygieneWarnings = settings.EnableYamlHygieneWarnings;
        }

        public void RequestValidation(string text)
        {
            var safeText = text ?? string.Empty;
            CancellationTokenSource validationCts;

            lock (_sync)
            {
                _pendingValidationCts?.Cancel();
                _pendingValidationCts?.Dispose();
                _pendingValidationCts = new CancellationTokenSource();
                validationCts = _pendingValidationCts;
            }

            _ = ValidateWithDebounceAsync(safeText, validationCts.Token);
        }

        public Task<IReadOnlyList<EditorDiagnostic>> ValidateNowAsync(string text, CancellationToken cancellationToken = default)
        {
            return ValidateCoreAsync(text ?? string.Empty, cancellationToken);
        }

        public void CancelPendingValidation()
        {
            lock (_sync)
            {
                _pendingValidationCts?.Cancel();
            }
        }

        private async Task ValidateWithDebounceAsync(string text, CancellationToken cancellationToken)
        {
            try
            {
                var debounceMs = Math.Clamp(
                    DebounceMilliseconds,
                    CommandEditorSettings.MinValidationDebounceMs,
                    CommandEditorSettings.MaxValidationDebounceMs);

                await Task.Delay(debounceMs, cancellationToken).ConfigureAwait(false);
                var diagnostics = await ValidateCoreAsync(text, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                    return;

                DiagnosticsUpdated?.Invoke(this, diagnostics);
            }
            catch (OperationCanceledException)
            {
                // Last-edit-wins cancellation path.
            }
        }

        private async Task<IReadOnlyList<EditorDiagnostic>> ValidateCoreAsync(string text, CancellationToken cancellationToken)
        {
            await _inFlightValidationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return ValidateInternal(text, cancellationToken);
            }
            finally
            {
                _inFlightValidationGate.Release();
            }
        }

        private IReadOnlyList<EditorDiagnostic> ValidateInternal(string text, CancellationToken cancellationToken)
        {
            if (!ScriptParser.IsYamlScript(text))
            {
                return Array.Empty<EditorDiagnostic>();
            }

            cancellationToken.ThrowIfCancellationRequested();

            var diagnostics = new List<EditorDiagnostic>();
            var parser = new ScriptParser();
            var lines = SplitLines(text);

            try
            {
                var script = parser.Parse(text);
                var errors = parser.Validate(
                    script,
                    text,
                    enforceCanonicalSyntax: true,
                    allowLibraryDefinitions: true);
                foreach (var error in errors)
                {
                    diagnostics.Add(MapMessageToDiagnostic(error, DiagnosticSeverity.Error, lines));
                }

                if (ShowInlineWarnings)
                {
                    foreach (var warning in parser.Warnings)
                    {
                        diagnostics.Add(MapMessageToDiagnostic(warning, DiagnosticSeverity.Warning, lines));
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(MapExceptionToDiagnostic(ex, lines));
            }

            if (EnableYamlHygieneWarnings)
            {
                diagnostics.AddRange(BuildYamlHygieneDiagnostics(lines));
            }

            return diagnostics
                .OrderBy(d => d.LineNumber)
                .ThenBy(d => d.ColumnStart)
                .ThenBy(d => d.Severity)
                .ToList();
        }

        private static EditorDiagnostic MapMessageToDiagnostic(
            string message,
            DiagnosticSeverity severity,
            IReadOnlyList<string> lines)
        {
            var lineNumber = TryExtractLineNumber(message, out var explicitLine) ? explicitLine : 1;
            lineNumber = Math.Clamp(lineNumber, 1, Math.Max(1, lines.Count));
            var lineText = lineNumber - 1 < lines.Count ? lines[lineNumber - 1] : string.Empty;
            var normalizedMessage = NormalizeMessage(message);

            var token = TryExtractToken(message);
            if (!string.IsNullOrEmpty(token))
            {
                var tokenIndex = lineText.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                if (tokenIndex >= 0)
                {
                    return new EditorDiagnostic
                    {
                        LineNumber = lineNumber,
                        ColumnStart = tokenIndex + 1,
                        ColumnEnd = tokenIndex + token.Length,
                        Severity = severity,
                        Message = normalizedMessage
                    };
                }
            }

            return EditorDiagnostic.CreateLineSpan(
                lineNumber,
                lineText.Length,
                severity,
                normalizedMessage);
        }

        private static EditorDiagnostic MapExceptionToDiagnostic(Exception exception, IReadOnlyList<string> lines)
        {
            var message = exception.Message ?? "Script validation failed.";
            var lineNumber = TryExtractLineNumber(message, out var parsedLine) ? parsedLine : 1;
            lineNumber = Math.Clamp(lineNumber, 1, Math.Max(1, lines.Count));
            var lineLength = lineNumber - 1 < lines.Count ? lines[lineNumber - 1].Length : 1;

            return EditorDiagnostic.CreateLineSpan(
                lineNumber,
                lineLength,
                DiagnosticSeverity.Error,
                message);
        }

        private static bool TryExtractLineNumber(string message, out int lineNumber)
        {
            lineNumber = 1;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var strict = LineMessageRegex.Match(message);
            if (strict.Success && int.TryParse(strict.Groups["line"].Value, out lineNumber))
                return true;

            var loose = LooseLineRegex.Match(message);
            if (loose.Success && int.TryParse(loose.Groups["line"].Value, out lineNumber))
                return true;

            return false;
        }

        private static string NormalizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Validation message";

            var match = LineMessageRegex.Match(message);
            if (match.Success)
            {
                return match.Groups["message"].Value.Trim();
            }

            return message.Trim();
        }

        private static string TryExtractToken(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return string.Empty;

            var quoted = TokenRegex.Match(message);
            if (quoted.Success)
            {
                return quoted.Groups["token"].Value.Trim();
            }

            var keyMatch = Regex.Match(message, @"Unknown\s+[A-Za-z_]+\s+key\s+(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (keyMatch.Success)
            {
                return keyMatch.Groups["token"].Value.Trim();
            }

            return string.Empty;
        }

        private static IReadOnlyList<EditorDiagnostic> BuildYamlHygieneDiagnostics(IReadOnlyList<string> lines)
        {
            var diagnostics = new List<EditorDiagnostic>();
            diagnostics.AddRange(BuildTabAndMixedIndentDiagnostics(lines));
            diagnostics.AddRange(BuildDuplicateKeyDiagnostics(lines));
            return diagnostics;
        }

        private static IEnumerable<EditorDiagnostic> BuildTabAndMixedIndentDiagnostics(IReadOnlyList<string> lines)
        {
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (string.IsNullOrEmpty(line))
                    continue;

                var leading = GetLeadingWhitespace(line);
                var firstTab = leading.IndexOf('\t');
                if (firstTab >= 0)
                {
                    yield return new EditorDiagnostic
                    {
                        LineNumber = index + 1,
                        ColumnStart = firstTab + 1,
                        ColumnEnd = firstTab + 1,
                        Severity = DiagnosticSeverity.Warning,
                        Message = "Tab indentation detected; use spaces for YAML indentation."
                    };
                }

                if (leading.Contains('\t') && leading.Contains(' '))
                {
                    var mixedStart = FindFirstMixedIndentColumn(leading);
                    yield return new EditorDiagnostic
                    {
                        LineNumber = index + 1,
                        ColumnStart = mixedStart,
                        ColumnEnd = Math.Max(mixedStart, leading.Length),
                        Severity = DiagnosticSeverity.Warning,
                        Message = "Mixed indentation detected (tabs + spaces)."
                    };
                }
            }
        }

        private static IEnumerable<EditorDiagnostic> BuildDuplicateKeyDiagnostics(IReadOnlyList<string> lines)
        {
            var seenByIndent = new Dictionary<int, HashSet<string>>();
            for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
            {
                var line = lines[lineIndex];

                // A new sequence item starts a new mapping scope beneath this indentation.
                // Reset deeper seen-key scopes so sibling step items don't report false duplicates.
                var sequenceMatch = SequenceItemRegex.Match(line);
                if (sequenceMatch.Success)
                {
                    var sequenceIndent = NormalizeIndent(sequenceMatch.Groups["indent"].Value);
                    foreach (var level in seenByIndent.Keys.Where(level => level > sequenceIndent).ToList())
                    {
                        seenByIndent.Remove(level);
                    }
                }

                var match = DuplicateKeyRegex.Match(line);
                if (!match.Success)
                    continue;

                var indent = NormalizeIndent(match.Groups["indent"].Value);
                var key = match.Groups["key"].Value;

                foreach (var level in seenByIndent.Keys.Where(level => level > indent).ToList())
                {
                    seenByIndent.Remove(level);
                }

                if (!seenByIndent.TryGetValue(indent, out var keysAtLevel))
                {
                    keysAtLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    seenByIndent[indent] = keysAtLevel;
                }

                if (!keysAtLevel.Add(key))
                {
                    var keyStart = match.Groups["key"].Index + 1;
                    yield return new EditorDiagnostic
                    {
                        LineNumber = lineIndex + 1,
                        ColumnStart = keyStart,
                        ColumnEnd = keyStart + key.Length - 1,
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"Duplicate key '{key}' detected at this indentation level."
                    };
                }
            }
        }

        private static int NormalizeIndent(string whitespace)
        {
            var indent = 0;
            foreach (var ch in whitespace)
            {
                indent += ch == '\t' ? 2 : 1;
            }
            return indent;
        }

        private static string GetLeadingWhitespace(string line)
        {
            var length = 0;
            while (length < line.Length && (line[length] == ' ' || line[length] == '\t'))
            {
                length++;
            }

            return length == 0 ? string.Empty : line.Substring(0, length);
        }

        private static int FindFirstMixedIndentColumn(string leadingWhitespace)
        {
            if (string.IsNullOrEmpty(leadingWhitespace))
                return 1;

            var firstSpace = leadingWhitespace.IndexOf(' ');
            var firstTab = leadingWhitespace.IndexOf('\t');
            if (firstSpace < 0)
                return firstTab + 1;
            if (firstTab < 0)
                return firstSpace + 1;

            return Math.Min(firstSpace, firstTab) + 1;
        }

        private static string[] SplitLines(string text)
        {
            return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _pendingValidationCts?.Cancel();
                _pendingValidationCts?.Dispose();
                _pendingValidationCts = null;
            }

            _inFlightValidationGate.Dispose();
        }
    }
}
