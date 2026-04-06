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

        private static readonly Regex StepCommandWithoutListMarkerRegex =
            new(@"^\s*(?<token>[A-Za-z0-9_-]*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        private static readonly Regex SequenceScalarRegex =
            new(@"^\s*-\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)\s*$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SimpleVariableValueMappingRegex =
            new(@"^\s*[A-Za-z_][A-Za-z0-9_-]*\s*:\s*(?<value>[A-Za-z_][A-Za-z0-9_]*)\s*$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        private static readonly Dictionary<string, string> CommandDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["send"] = "SSH command",
            ["print"] = "Output text",
            ["wait"] = "Delay/wait",
            ["set"] = "Set variable",
            ["exit"] = "Exit script",
            ["extract"] = "Regex extract",
            ["if"] = "Conditional",
            ["foreach"] = "Iterate",
            ["while"] = "Loop",
            ["updatecolumn"] = "Modify column",
            ["updateenvironment"] = "Modify env",
            ["readfile"] = "Read file",
            ["writefile"] = "Write file",
            ["exists"] = "File check",
            ["playsound"] = "Play audio",
            ["input"] = "User input",
            ["log"] = "Log message",
            ["localcmd"] = "run local cmd",
            ["http"] = "HTTP request",
            ["browser_callback_capture"] = "OAuth capture",
            ["ping"] = "Ping host",
            ["dns"] = "DNS lookup",
            ["portcheck"] = "Port check",
            ["sftp"] = "SFTP transfer",
            ["webhook"] = "Webhook",
            ["parse"] = "Parse output",
            ["choose"] = "Selection",
            ["multiselect"] = "Multi-select",
            ["confirm"] = "Yes/no",
            ["interactive"] = "Terminal",
            ["break"] = "Exit loop",
            ["continue"] = "Next iteration",
            ["try"] = "Error handling",
            ["assert"] = "Assert",
            ["switch"] = "Case match",
            ["parallel"] = "Concurrent",
            ["call"] = "Call sub",
            ["return"] = "Return value",
            ["table"] = "Data table",
            ["vault"] = "Read, write, or patch secrets from HashiCorp Vault"
        };

        private static readonly Dictionary<string, string> TopLevelKeyDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = "Script name",
            ["description"] = "Description",
            ["version"] = "Version",
            ["environment"] = "Target env",
            ["debug"] = "Debug mode",
            ["nobanner"] = "Hide banner",
            ["suppress_missing_column_warning"] = "Suppress warns",
            ["library"] = "Library import",
            ["vars"] = "Variables",
            ["imports"] = "Imports",
            ["subroutines"] = "Procedures",
            ["steps"] = "Script body"
        };

        private static readonly Dictionary<string, string[]> RequiredOptionKeysByCommand = new(StringComparer.OrdinalIgnoreCase)
        {
            ["send"] = ["command"],
            ["print"] = ["message"],
            ["wait"] = ["seconds"],
            ["set"] = ["expression"],
            ["if"] = ["condition", "then"],
            ["foreach"] = ["iterator", "do"],
            ["while"] = ["condition", "do"],
            ["try"] = ["do"],
            ["call"] = ["subroutine"],
            ["parallel"] = ["steps"],
            ["table"] = ["data"],
            ["switch"] = ["value", "cases"],
            ["extract"] = ["from", "pattern", "into"],
            ["readfile"] = ["path", "into"],
            ["writefile"] = ["path"],
            ["exists"] = ["path", "into"],
            ["playsound"] = ["path"],
            ["input"] = ["into"],
            ["updatecolumn"] = ["column", "value"],
            ["updateenvironment"] = ["variable", "value"],
            ["http"] = ["url"],
            ["ping"] = ["host"],
            ["dns"] = ["host"],
            ["portcheck"] = ["host"],
            ["sftp"] = ["action", "local_path", "remote_path"],
            ["webhook"] = ["url"],
            ["parse"] = ["format", "from", "into"],
            ["choose"] = ["into", "options"],
            ["multiselect"] = ["into", "options"],
            ["confirm"] = ["into"],
            ["assert"] = ["condition"],
            ["browser_callback_capture"] = ["start_url", "callback_path", "into"],
            ["localcmd"] = ["command"]
        };

        private static readonly Dictionary<string, string> BuiltInSymbolDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["_output"] = "Last output",
            ["_timestamp"] = "Timestamp",
            ["_iteration"] = "Loop counter",
            ["_last_error"] = "Last error",
            ["_host"] = "Host address",
            ["_port"] = "Port number",
            ["_username"] = "Username",
            ["_password"] = "Password"
        };

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

        public CompletionResult GetCompletion(string text, int caretIndex, bool manualRequest = false)
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
                    FilterValues(_stepCommands, token, kind: "command", CommandDescriptions));
            }

            var commandWithoutListMarker = BuildStepCommandCompletionWithoutListMarker(
                text,
                safeCaret,
                lineStart,
                linePrefix,
                currentIndent,
                manualRequest);
            if (commandWithoutListMarker != null)
            {
                return commandWithoutListMarker;
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
            if (optionKeyMatch.Success && currentIndent > 0 &&
                !HasBlankLineSeparator(text, lineStart))
            {
                var token = optionKeyMatch.Groups["token"].Value;
                var optionCandidates = ResolveOptionKeyCandidates(text, lineStart, currentIndent);
                var parentCommand = FindEnclosingCommandName(text, lineStart, currentIndent);
                return BuildCompletion(
                    CompletionContextKind.StepOptionKey,
                    safeCaret - token.Length,
                    token.Length,
                    FilterOptionKeys(optionCandidates, token, parentCommand));
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
                        FilterValues(_topLevelKeys, topLevelToken, kind: "top-level", TopLevelKeyDescriptions));
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

        private CompletionResult? BuildStepCommandCompletionWithoutListMarker(
            string text,
            int safeCaret,
            int lineStart,
            string linePrefix,
            int currentIndent,
            bool manualRequest)
        {
            var commandMatch = StepCommandWithoutListMarkerRegex.Match(linePrefix);
            if (!commandMatch.Success)
            {
                return null;
            }

            var token = commandMatch.Groups["token"].Value;
            if (!TryResolveStepSequenceContext(
                    text,
                    lineStart,
                    currentIndent,
                    manualRequest,
                    out var stepIndent,
                    out var inferredFromPreviousLine))
            {
                return null;
            }

            var canBridgeIndentForManualBlank = manualRequest &&
                                                token.Length == 0 &&
                                                currentIndent == 0;
            if (currentIndent != stepIndent && !canBridgeIndentForManualBlank)
            {
                return null;
            }

            var indentationPrefix = inferredFromPreviousLine
                ? new string(' ', stepIndent)
                : string.Empty;
            var replaceStart = inferredFromPreviousLine
                ? lineStart
                : safeCaret - token.Length;
            var replaceLength = inferredFromPreviousLine
                ? linePrefix.Length
                : token.Length;

            var items = FilterValues(_stepCommands, token, kind: "command", CommandDescriptions)
                .Select(item => new CompletionItem(
                    item.Label,
                    indentationPrefix + "- " + item.InsertText,
                    item.Kind,
                    item.Detail))
                .ToList();

            if (items.Count == 0)
            {
                return null;
            }

            return BuildCompletion(
                CompletionContextKind.StepCommand,
                replaceStart,
                replaceLength,
                items);
        }

        private bool TryResolveStepSequenceContext(
            string text,
            int currentLineStart,
            int currentIndent,
            bool manualRequest,
            out int stepIndent,
            out bool inferredFromPreviousLine)
        {
            stepIndent = 0;
            inferredFromPreviousLine = false;
            if (currentIndent > 0 &&
                IsStepSequenceCommandIndent(text, currentLineStart, currentIndent))
            {
                stepIndent = currentIndent;
                return true;
            }

            if (TryResolveStepSequenceIndentFromPreviousLine(text, currentLineStart, out var inferredIndent))
            {
                stepIndent = inferredIndent;
                inferredFromPreviousLine = currentIndent != inferredIndent;
                return true;
            }

            return false;
        }

        private bool TryResolveStepSequenceIndentFromPreviousLine(string text, int currentLineStart, out int stepIndent)
        {
            stepIndent = 0;
            var searchEnd = Math.Clamp(currentLineStart, 0, text.Length);
            while (TryReadPreviousLine(text, searchEnd, out var previousLineStart, out var previousLineEnd))
            {
                searchEnd = previousLineStart;
                var candidate = text.Substring(previousLineStart, previousLineEnd - previousLineStart).TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                var trimmedStart = candidate.TrimStart();
                if (trimmedStart.StartsWith('#'))
                {
                    continue;
                }

                var candidateIndent = CountIndent(candidate);
                if (TryParseStepCommandLine(candidate, out var directStepCommand, out _) &&
                    IsKnownStepCommand(directStepCommand))
                {
                    stepIndent = candidateIndent;
                    return true;
                }

                if (TryFindAncestorStepCommand(
                        text,
                        previousLineStart,
                        candidateIndent,
                        out var ancestorStepCommand,
                        out var ancestorStepIndent) &&
                    IsKnownStepCommand(ancestorStepCommand))
                {
                    stepIndent = ancestorStepIndent;
                    return true;
                }

                if (TryParseMappingKeyLine(candidate, out var mappingKey) &&
                    string.Equals(CanonicalizeKey(mappingKey), "steps", StringComparison.OrdinalIgnoreCase))
                {
                    stepIndent = candidateIndent + 2;
                    return true;
                }

                if (TryFindAncestorMappingKey(text, previousLineStart, candidateIndent, out var ancestorKey, out var ancestorIndent) &&
                    string.Equals(CanonicalizeKey(ancestorKey), "steps", StringComparison.OrdinalIgnoreCase) &&
                    candidateIndent >= ancestorIndent + 2)
                {
                    stepIndent = ancestorIndent + 2;
                    return true;
                }

                return false;
            }

            return false;
        }

        private bool IsKnownStepCommand(string command)
        {
            return _stepCommands.Contains(command, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsStepSequenceCommandIndent(string text, int currentLineStart, int currentIndent)
        {
            if (!TryFindAncestorMappingKey(text, currentLineStart, currentIndent, out var ancestorKey, out var ancestorIndent))
            {
                return false;
            }

            return string.Equals(
                    CanonicalizeKey(ancestorKey),
                    "steps",
                    StringComparison.OrdinalIgnoreCase) &&
                currentIndent == ancestorIndent + 2;
        }

        public IReadOnlyList<string> ExtractDynamicSymbols(string text)
        {
            text ??= string.Empty;
            var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = SplitLines(text);

            var inVarsSection = false;
            var varsIndent = 0;
            var inSubroutinesSection = false;
            var subroutinesIndent = 0;
            var currentSubroutineIndent = -1;
            var activeSymbolListIndent = -1;
            var activeCallOutIndent = -1;

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

                if (!inSubroutinesSection && trimmed.StartsWith("subroutines:", StringComparison.OrdinalIgnoreCase))
                {
                    inSubroutinesSection = true;
                    subroutinesIndent = indent;
                    currentSubroutineIndent = -1;
                    activeSymbolListIndent = -1;
                    continue;
                }

                if (inVarsSection && indent <= varsIndent)
                {
                    inVarsSection = false;
                }

                if (inSubroutinesSection && indent <= subroutinesIndent)
                {
                    inSubroutinesSection = false;
                    currentSubroutineIndent = -1;
                    activeSymbolListIndent = -1;
                }

                if (inVarsSection)
                {
                    var varsMatch = Regex.Match(line, @"^\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:");
                    if (varsMatch.Success)
                    {
                        symbols.Add(varsMatch.Groups["name"].Value);
                    }
                }

                if (inSubroutinesSection)
                {
                    if (indent == subroutinesIndent + 2 &&
                        TryParseMappingKeyLine(line, out var subroutineName) &&
                        !string.Equals(CanonicalizeKey(subroutineName), "params", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(CanonicalizeKey(subroutineName), "outputs", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(CanonicalizeKey(subroutineName), "steps", StringComparison.OrdinalIgnoreCase))
                    {
                        currentSubroutineIndent = indent;
                        activeSymbolListIndent = -1;
                    }

                    if (currentSubroutineIndent >= 0 && indent <= currentSubroutineIndent)
                    {
                        activeSymbolListIndent = -1;
                    }

                    if (currentSubroutineIndent >= 0 && indent > currentSubroutineIndent)
                    {
                        if (TryReadSimpleListHeader(line, "params", out var paramsInlineValue) ||
                            TryReadSimpleListHeader(line, "outputs", out paramsInlineValue))
                        {
                            activeSymbolListIndent = indent;
                            foreach (var symbol in ParseBracketedSymbols(paramsInlineValue))
                            {
                                symbols.Add(symbol);
                            }
                        }
                        else if (activeSymbolListIndent >= 0 && indent > activeSymbolListIndent)
                        {
                            var seqMatch = SequenceScalarRegex.Match(line);
                            if (seqMatch.Success)
                            {
                                symbols.Add(seqMatch.Groups["value"].Value);
                            }
                        }
                        else if (activeSymbolListIndent >= 0 && indent <= activeSymbolListIndent)
                        {
                            activeSymbolListIndent = -1;
                        }
                    }
                }

                if (trimmed.StartsWith("out:", StringComparison.OrdinalIgnoreCase))
                {
                    activeCallOutIndent = indent;
                    continue;
                }

                if (activeCallOutIndent >= 0 && indent <= activeCallOutIndent)
                {
                    activeCallOutIndent = -1;
                }

                if (activeCallOutIndent >= 0 && indent > activeCallOutIndent)
                {
                    var outMatch = SimpleVariableValueMappingRegex.Match(line);
                    if (outMatch.Success)
                    {
                        symbols.Add(outMatch.Groups["value"].Value);
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

        private static bool TryReadSimpleListHeader(string line, string key, out string inlineValue)
        {
            inlineValue = string.Empty;
            if (!TryParseMappingKeyLine(line, out var parsedKey))
                return false;

            if (!string.Equals(CanonicalizeKey(parsedKey), CanonicalizeKey(key), StringComparison.OrdinalIgnoreCase))
                return false;

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 0)
                return false;

            inlineValue = line[(colonIndex + 1)..].Trim();
            return true;
        }

        private static IEnumerable<string> ParseBracketedSymbols(string inlineValue)
        {
            if (string.IsNullOrWhiteSpace(inlineValue))
                yield break;

            var trimmed = inlineValue.Trim();
            if (!(trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)))
                yield break;

            foreach (var part in trimmed[1..^1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = part.Trim();
                if (Regex.IsMatch(candidate, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    yield return candidate;
                }
            }
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
                FilterValues(GetInterpolationSymbols(text), token, kind: "symbol", BuiltInSymbolDescriptions));
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
            return TryFindAncestorStepCommand(
                text,
                currentLineStart,
                startIndent,
                out command,
                out _);
        }

        private static bool TryFindAncestorStepCommand(
            string text,
            int currentLineStart,
            int startIndent,
            out string command,
            out int commandIndent)
        {
            command = string.Empty;
            commandIndent = 0;

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
                    commandIndent = indent;
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
            string kind,
            IReadOnlyDictionary<string, string>? descriptions = null)
        {
            var safeToken = token ?? string.Empty;
            return values
                .Where(value => value.StartsWith(safeToken, StringComparison.OrdinalIgnoreCase))
                .Select(value => new CompletionItem(value, value, kind,
                    descriptions != null && descriptions.TryGetValue(value, out var desc) ? desc : null))
                .ToList();
        }

        private static IReadOnlyList<CompletionItem> FilterOptionKeys(
            IReadOnlyList<string> values,
            string token,
            string? parentCommand)
        {
            var safeToken = token ?? string.Empty;
            HashSet<string>? requiredKeys = null;
            if (parentCommand != null &&
                RequiredOptionKeysByCommand.TryGetValue(parentCommand, out var required))
            {
                requiredKeys = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);
            }

            var items = values
                .Where(value => value.StartsWith(safeToken, StringComparison.OrdinalIgnoreCase))
                .Select(value => new CompletionItem(value, value, "option",
                    requiredKeys?.Contains(value) == true ? "required" : null))
                .OrderBy(item => item.Detail == null ? 1 : 0)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return items;
        }

        private static string? FindEnclosingCommandName(string text, int currentLineStart, int currentIndent)
        {
            var threshold = currentIndent;
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

                threshold = indent;
                if (TryParseStepCommandLine(candidate, out var command, out _))
                    return CanonicalizeKey(command);
            }

            return null;
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

        private static bool HasBlankLineSeparator(string text, int currentLineStart)
        {
            if (!TryReadPreviousLine(text, currentLineStart, out var prevLineStart, out var prevLineEnd))
                return false;

            var prevLine = text.Substring(prevLineStart, prevLineEnd - prevLineStart);
            return string.IsNullOrWhiteSpace(prevLine);
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
