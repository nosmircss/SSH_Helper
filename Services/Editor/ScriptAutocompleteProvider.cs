using System.Text.RegularExpressions;
using SSH_Helper.Services.Scripting;

namespace SSH_Helper.Services.Editor
{
    public enum CompletionContextKind
    {
        None,
        TopLevelKey,
        StepCommand,
        StepOptionKey,
        OptionValue,
        Interpolation
    }

    public sealed class CompletionItem
    {
        public CompletionItem(string label, string insertText, string kind, string? detail = null)
        {
            Label = label;
            InsertText = insertText;
            Kind = kind;
            Detail = detail;
        }

        public string Label { get; }
        public string InsertText { get; }
        public string Kind { get; }
        public string? Detail { get; }

        public override string ToString() => Label;
    }

    public sealed class CompletionResult
    {
        public CompletionContextKind Context { get; init; } = CompletionContextKind.None;
        public int ReplaceStart { get; init; }
        public int ReplaceLength { get; init; }
        public IReadOnlyList<CompletionItem> Items { get; init; } = Array.Empty<CompletionItem>();
    }

    public sealed class ScriptAutocompleteProvider
    {
        private static readonly Regex InterpolationPrefixRegex =
            new(@"(?<trigger>\$\{|{{)(?<token>[A-Za-z0-9_.-]*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex StepCommandRegex =
            new(@"^\s*-\s+(?<token>[A-Za-z0-9_-]*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex StepCommandLineRegex =
            new(@"^\s*-\s*(?<command>[A-Za-z_][A-Za-z0-9_-]*)\s*:\s*(?<value>.*)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex OptionValueRegex =
            new(@"^\s*(?<key>[A-Za-z_][A-Za-z0-9_-]*)\s*:\s*(?<token>[A-Za-z0-9_-]*)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex OptionKeyRegex =
            new(@"^\s+(?<token>[A-Za-z0-9_-]*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex MappingKeyLineRegex =
            new(@"^\s*(?<key>[A-Za-z_][A-Za-z0-9_-]*)\s*:\s*(?<value>.*)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SetAssignmentRegex =
            new(@"^\s*-\s*set:\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex SetExpressionAssignmentRegex =
            new(@"^\s*expression:\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex CaptureRegex =
            new(@"^\s*capture:\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex IntoRegex =
            new(@"^\s*into:\s*(?<value>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> BuiltInSymbols = new(StringComparer.OrdinalIgnoreCase)
        {
            "_output",
            "_timestamp",
            "_iteration",
            "_last_error",
            "_host",
            "_port",
            "_username",
            "_password"
        };

        private static readonly string[] IntoDerivedSuffixes =
        [
            "_status",
            "_headers",
            "_count",
            "_avg",
            "_min",
            "_max"
        ];

        private readonly Func<IReadOnlyCollection<string>> _getHostColumns;
        private readonly IReadOnlyList<string> _topLevelKeys;
        private readonly IReadOnlyList<string> _stepCommands;
        private readonly IReadOnlyList<string> _commonStepOptionKeys;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _stepRootOptionKeysByCommand;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _stepOptionKeysByCommand;
        private readonly IReadOnlyList<string> _stepOptionKeys;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _enumLikeOptionValues;

        public ScriptAutocompleteProvider(Func<IReadOnlyCollection<string>>? getHostColumns = null)
        {
            _getHostColumns = getHostColumns ?? (() => Array.Empty<string>());
            _topLevelKeys = ScriptParser.GetKnownTopLevelKeys()
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _stepCommands = ScriptParser.GetKnownStepCommands()
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _commonStepOptionKeys = ScriptParser.GetKnownCommonStepOptionKeys()
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _stepRootOptionKeysByCommand = ScriptParser.GetKnownStepRootOptionKeysByCommand()
                .ToDictionary(
                    pair => CanonicalizeKey(pair.Key),
                    pair => (IReadOnlyList<string>)pair.Value
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            _stepOptionKeysByCommand = ScriptParser.GetKnownStepOptionKeysByCommand()
                .ToDictionary(
                    pair => CanonicalizeKey(pair.Key),
                    pair => (IReadOnlyList<string>)pair.Value
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);

            _stepOptionKeys = ScriptParser.GetKnownStepOptionKeys()
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _enumLikeOptionValues = ScriptParser.GetEnumLikeOptionValues()
                .ToDictionary(
                    pair => CanonicalizeKey(pair.Key),
                    pair => (IReadOnlyList<string>)pair.Value
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<string> GetTopLevelKeys() => _topLevelKeys;

        public IReadOnlyList<string> GetStepCommands() => _stepCommands;

        public IReadOnlyList<string> GetStepOptionKeys() => _stepOptionKeys;

        public IReadOnlyDictionary<string, IReadOnlyList<string>> GetEnumLikeOptionValues() => _enumLikeOptionValues;

        public CompletionResult GetCompletion(string text, int caretIndex)
        {
            text ??= string.Empty;
            var safeCaret = Math.Clamp(caretIndex, 0, text.Length);
            var lineStart = FindLineStart(text, safeCaret);
            var linePrefix = text.Substring(lineStart, safeCaret - lineStart);
            var currentIndent = CountIndent(linePrefix);

            var interpolation = BuildInterpolationCompletion(text, safeCaret, linePrefix);
            if (interpolation != null)
                return interpolation;

            var commandMatch = StepCommandRegex.Match(linePrefix);
            if (commandMatch.Success)
            {
                var token = commandMatch.Groups["token"].Value;
                return BuildCompletion(
                    CompletionContextKind.StepCommand,
                    safeCaret - token.Length,
                    token.Length,
                    FilterValues(_stepCommands, token, kind: "command"));
            }

            var optionValueMatch = OptionValueRegex.Match(linePrefix);
            if (optionValueMatch.Success)
            {
                var key = CanonicalizeKey(optionValueMatch.Groups["key"].Value);
                var token = optionValueMatch.Groups["token"].Value;
                if (_enumLikeOptionValues.TryGetValue(key, out var values))
                {
                    return BuildCompletion(
                        CompletionContextKind.OptionValue,
                        safeCaret - token.Length,
                        token.Length,
                        FilterValues(values, token, kind: "value"));
                }
            }

            var optionKeyMatch = OptionKeyRegex.Match(linePrefix);
            if (optionKeyMatch.Success && currentIndent > 0)
            {
                var token = optionKeyMatch.Groups["token"].Value;
                var optionCandidates = ResolveOptionKeyCandidates(text, lineStart, currentIndent);
                return BuildCompletion(
                    CompletionContextKind.StepOptionKey,
                    safeCaret - token.Length,
                    token.Length,
                    FilterValues(optionCandidates, token, kind: "option"));
            }

            if (currentIndent == 0)
            {
                var topLevelToken = linePrefix.Trim();
                if (topLevelToken.Length == 0 &&
                    !ShouldAutoSuggestBlankTopLevelKeys(text, lineStart))
                {
                    return new CompletionResult();
                }

                if (IsIdentifierLike(topLevelToken))
                {
                    return BuildCompletion(
                        CompletionContextKind.TopLevelKey,
                        safeCaret - topLevelToken.Length,
                        topLevelToken.Length,
                        FilterValues(_topLevelKeys, topLevelToken, kind: "top-level"));
                }
            }

            return new CompletionResult();
        }

        private static bool ShouldAutoSuggestBlankTopLevelKeys(string text, int currentLineStart)
        {
            var safeEnd = Math.Clamp(currentLineStart, 0, text.Length);
            if (safeEnd == 0)
            {
                return true;
            }

            foreach (var line in SplitLines(text.Substring(0, safeEnd)))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var trimmedStart = line.TrimStart();
                if (trimmedStart.StartsWith('#'))
                {
                    continue;
                }

                if (CountIndent(line) != 0)
                {
                    continue;
                }

                if (!TryParseMappingKeyLine(line, out var key))
                {
                    continue;
                }

                var canonicalKey = CanonicalizeKey(key);
                if (canonicalKey is "vars" or "steps")
                {
                    return false;
                }
            }

            return true;
        }

        public IReadOnlyList<string> GetInterpolationSymbols(string text)
        {
            var symbols = new HashSet<string>(BuiltInSymbols, StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in ExtractDynamicSymbols(text))
            {
                symbols.Add(symbol);
            }

            foreach (var column in _getHostColumns())
            {
                if (!string.IsNullOrWhiteSpace(column))
                {
                    symbols.Add(column.Trim());
                }
            }

            return symbols.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public IReadOnlyList<string> ExtractDynamicSymbols(string text)
        {
            text ??= string.Empty;
            var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = SplitLines(text);

            var inVarsSection = false;
            var varsIndent = 0;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var indent = CountIndent(line);
                if (!inVarsSection && trimmed.StartsWith("vars:", StringComparison.OrdinalIgnoreCase))
                {
                    inVarsSection = true;
                    varsIndent = indent;
                    continue;
                }

                if (inVarsSection && indent <= varsIndent)
                {
                    inVarsSection = false;
                }

                if (inVarsSection)
                {
                    var varsMatch = Regex.Match(line, @"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:");
                    if (varsMatch.Success)
                    {
                        symbols.Add(varsMatch.Groups["name"].Value);
                    }
                }

                var setMatch = SetAssignmentRegex.Match(line);
                if (setMatch.Success)
                {
                    symbols.Add(setMatch.Groups["name"].Value);
                }

                var setExpressionMatch = SetExpressionAssignmentRegex.Match(line);
                if (setExpressionMatch.Success)
                {
                    symbols.Add(setExpressionMatch.Groups["name"].Value);
                }

                var captureMatch = CaptureRegex.Match(line);
                if (captureMatch.Success)
                {
                    symbols.Add(captureMatch.Groups["name"].Value);
                }

                var intoMatch = IntoRegex.Match(line);
                if (intoMatch.Success)
                {
                    foreach (var intoTarget in ParseIntoTargets(intoMatch.Groups["value"].Value))
                    {
                        symbols.Add(intoTarget);
                        foreach (var suffix in IntoDerivedSuffixes)
                        {
                            symbols.Add(intoTarget + suffix);
                        }
                    }
                }
            }

            return symbols.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private CompletionResult? BuildInterpolationCompletion(string text, int safeCaret, string linePrefix)
        {
            var interpolationMatch = InterpolationPrefixRegex.Match(linePrefix);
            if (!interpolationMatch.Success)
                return null;

            var token = interpolationMatch.Groups["token"].Value;
            return BuildCompletion(
                CompletionContextKind.Interpolation,
                safeCaret - token.Length,
                token.Length,
                FilterValues(GetInterpolationSymbols(text), token, kind: "symbol"));
        }

        private IReadOnlyList<string> ResolveOptionKeyCandidates(string text, int currentLineStart, int currentIndent)
        {
            var nestedCandidates = ResolveNestedOptionKeyCandidates(text, currentLineStart, currentIndent);
            if (nestedCandidates.Count > 0)
                return nestedCandidates;

            if (!TryGetImmediateParentLine(text, currentLineStart, currentIndent, out var parentLine, out var parentIndent))
                return Array.Empty<string>();

            if (!TryParseStepCommandLine(parentLine, out var command, out var hasInlineValue))
                return Array.Empty<string>();

            var canonicalCommand = CanonicalizeKey(command);
            if (!hasInlineValue &&
                currentIndent > parentIndent + 2 &&
                _stepOptionKeysByCommand.TryGetValue(canonicalCommand, out var commandOptions))
            {
                return commandOptions;
            }

            if (_stepRootOptionKeysByCommand.TryGetValue(canonicalCommand, out var stepRootOptions))
            {
                return stepRootOptions;
            }

            return _commonStepOptionKeys;
        }

        private IReadOnlyList<string> ResolveNestedOptionKeyCandidates(string text, int currentLineStart, int currentIndent)
        {
            if (!TryFindAncestorMappingKey(text, currentLineStart, currentIndent, out var ancestorKey, out var ancestorIndent))
                return Array.Empty<string>();

            if (!string.Equals(CanonicalizeKey(ancestorKey), "respond", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<string>();

            if (!TryFindAncestorStepCommand(text, currentLineStart, ancestorIndent, out var command))
                return Array.Empty<string>();

            if (!string.Equals(CanonicalizeKey(command), "send", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<string>();

            return new[] { "expect", "reply" };
        }

        private static bool TryGetImmediateParentLine(
            string text,
            int currentLineStart,
            int currentIndent,
            out string parentLine,
            out int parentIndent)
        {
            parentLine = string.Empty;
            parentIndent = 0;

            var searchEnd = Math.Clamp(currentLineStart, 0, text.Length);
            while (TryReadPreviousLine(text, searchEnd, out var previousLineStart, out var previousLineEnd))
            {
                searchEnd = previousLineStart;
                var candidate = text.Substring(previousLineStart, previousLineEnd - previousLineStart).TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var trimmedStart = candidate.TrimStart();
                if (trimmedStart.StartsWith('#'))
                    continue;

                var indent = CountIndent(candidate);
                if (indent < currentIndent)
                {
                    parentLine = candidate;
                    parentIndent = indent;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindAncestorMappingKey(
            string text,
            int currentLineStart,
            int startIndent,
            out string key,
            out int keyIndent)
        {
            key = string.Empty;
            keyIndent = 0;

            var threshold = startIndent;
            var searchEnd = Math.Clamp(currentLineStart, 0, text.Length);
            while (TryReadPreviousLine(text, searchEnd, out var previousLineStart, out var previousLineEnd))
            {
                searchEnd = previousLineStart;
                var candidate = text.Substring(previousLineStart, previousLineEnd - previousLineStart).TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var trimmedStart = candidate.TrimStart();
                if (trimmedStart.StartsWith('#'))
                    continue;

                var indent = CountIndent(candidate);
                if (indent >= threshold)
                    continue;

                if (TryParseMappingKeyLine(candidate, out var parsedKey))
                {
                    key = parsedKey;
                    keyIndent = indent;
                    return true;
                }

                threshold = indent;
            }

            return false;
        }

        private static bool TryFindAncestorStepCommand(
            string text,
            int currentLineStart,
            int startIndent,
            out string command)
        {
            command = string.Empty;

            var threshold = startIndent;
            var searchEnd = Math.Clamp(currentLineStart, 0, text.Length);
            while (TryReadPreviousLine(text, searchEnd, out var previousLineStart, out var previousLineEnd))
            {
                searchEnd = previousLineStart;
                var candidate = text.Substring(previousLineStart, previousLineEnd - previousLineStart).TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                var trimmedStart = candidate.TrimStart();
                if (trimmedStart.StartsWith('#'))
                    continue;

                var indent = CountIndent(candidate);
                if (indent >= threshold)
                    continue;

                if (TryParseStepCommandLine(candidate, out var parsedCommand, out _))
                {
                    command = parsedCommand;
                    return true;
                }

                threshold = indent;
            }

            return false;
        }

        private static bool TryReadPreviousLine(string text, int endExclusive, out int lineStart, out int lineEnd)
        {
            lineStart = 0;
            lineEnd = 0;

            var cursor = Math.Clamp(endExclusive, 0, text.Length);
            if (cursor == 0)
                return false;

            if (cursor > 0 && text[cursor - 1] == '\n')
                cursor--;

            lineEnd = cursor;
            while (cursor > 0 && text[cursor - 1] != '\n')
            {
                cursor--;
            }

            lineStart = cursor;
            return lineEnd >= lineStart;
        }

        private static bool TryParseStepCommandLine(string line, out string command, out bool hasInlineValue)
        {
            command = string.Empty;
            hasInlineValue = false;

            var match = StepCommandLineRegex.Match(line);
            if (!match.Success)
                return false;

            command = match.Groups["command"].Value;
            hasInlineValue = HasInlineValue(match.Groups["value"].Value);
            return true;
        }

        private static bool TryParseMappingKeyLine(string line, out string key)
        {
            key = string.Empty;
            var match = MappingKeyLineRegex.Match(line);
            if (!match.Success)
                return false;

            key = match.Groups["key"].Value;
            return true;
        }

        private static bool HasInlineValue(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return false;

            var trimmed = rawValue.Trim();
            return !trimmed.StartsWith('#');
        }

        private static CompletionResult BuildCompletion(
            CompletionContextKind context,
            int replaceStart,
            int replaceLength,
            IReadOnlyList<CompletionItem> items)
        {
            if (items.Count == 0)
                return new CompletionResult();

            return new CompletionResult
            {
                Context = context,
                ReplaceStart = Math.Max(0, replaceStart),
                ReplaceLength = Math.Max(0, replaceLength),
                Items = items
            };
        }

        private static IReadOnlyList<CompletionItem> FilterValues(
            IEnumerable<string> values,
            string token,
            string kind)
        {
            var safeToken = token ?? string.Empty;
            return values
                .Where(value => value.StartsWith(safeToken, StringComparison.OrdinalIgnoreCase))
                .Select(value => new CompletionItem(value, value, kind))
                .ToList();
        }

        private static IEnumerable<string> ParseIntoTargets(string value)
        {
            var trimmed = value.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var inner = trimmed.Substring(1, trimmed.Length - 2);
                foreach (var part in inner.Split(','))
                {
                    var candidate = SanitizeSymbolName(part);
                    if (!string.IsNullOrEmpty(candidate))
                        yield return candidate;
                }
                yield break;
            }

            var single = SanitizeSymbolName(trimmed);
            if (!string.IsNullOrEmpty(single))
                yield return single;
        }

        private static string SanitizeSymbolName(string raw)
        {
            var trimmed = raw.Trim().Trim('"', '\'');
            if (Regex.IsMatch(trimmed, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
                return trimmed;

            return string.Empty;
        }

        private static bool IsIdentifierLike(string token)
        {
            if (string.IsNullOrEmpty(token))
                return true;

            return Regex.IsMatch(token, @"^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);
        }

        private static int FindLineStart(string text, int index)
        {
            var i = Math.Clamp(index, 0, text.Length);
            while (i > 0)
            {
                if (text[i - 1] == '\n')
                    break;
                i--;
            }
            return i;
        }

        private static int CountIndent(string line)
        {
            var count = 0;
            foreach (var ch in line)
            {
                if (ch == ' ')
                {
                    count++;
                    continue;
                }
                if (ch == '\t')
                {
                    count += 2;
                    continue;
                }
                break;
            }
            return count;
        }

        private static string[] SplitLines(string text)
        {
            return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        private static string CanonicalizeKey(string key)
        {
            return (key ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "_", StringComparison.Ordinal);
        }
    }
}
