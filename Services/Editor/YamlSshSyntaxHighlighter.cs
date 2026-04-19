using System.Drawing;
using System.Text.RegularExpressions;
using SSH_Helper.Services.Scripting;

namespace SSH_Helper.Services.Editor
{
    public readonly struct EditorHighlightSpan
    {
        public EditorHighlightSpan(int start, int length, Color color)
        {
            Start = start;
            Length = length;
            Color = color;
        }

        public int Start { get; }
        public int Length { get; }
        public Color Color { get; }
    }

    public sealed class YamlSshSyntaxHighlighter
    {
        private static readonly string[] NestedTableColumnOptionKeys =
        {
            "header",
            "field"
        };

        private static readonly Regex TopLevelRegex =
            new(@"^\s*(?<key>[A-Za-z_][A-Za-z0-9_-]*)\s*:", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex StepCommandRegex =
            new(@"^\s*-\s*(?<key>[A-Za-z_][A-Za-z0-9_-]*)\s*:", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex OptionKeyRegex =
            new(@"^\s*(?<key>[A-Za-z_][A-Za-z0-9_-]*)\s*:", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex VariableRegex =
            new(@"\$\{[^}]+\}|\{\{[^}]+\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex StringRegex =
            new(@"""[^""]*""|'[^']*'", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex NumberRegex =
            new(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex BooleanRegex =
            new(@"\b(true|false|yes|no|null)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly HashSet<string> _topLevelKeys;
        private readonly HashSet<string> _stepCommands;
        private readonly HashSet<string> _highlightedOptionKeys;

        public YamlSshSyntaxHighlighter()
        {
            _topLevelKeys = new HashSet<string>(ScriptParser.GetKnownTopLevelKeys(), StringComparer.OrdinalIgnoreCase);
            _stepCommands = new HashSet<string>(ScriptParser.GetKnownStepCommands(), StringComparer.OrdinalIgnoreCase);
            _highlightedOptionKeys = new HashSet<string>(ScriptParser.GetKnownStepOptionKeys(), StringComparer.OrdinalIgnoreCase);
            _highlightedOptionKeys.UnionWith(NestedTableColumnOptionKeys);
        }

        public IReadOnlyList<EditorHighlightSpan> BuildHighlights(
            string text,
            IEnumerable<int> changedLineIndices,
            bool darkMode)
        {
            text ??= string.Empty;
            var lines = SplitLines(text);
            var lineStarts = BuildLineStartIndices(text);

            var spans = new List<EditorHighlightSpan>();
            foreach (var lineIndex in changedLineIndices.Distinct().Where(index => index >= 0 && index < lines.Length))
            {
                spans.AddRange(BuildLineHighlights(lines[lineIndex], lineStarts[lineIndex], darkMode));
            }

            return spans;
        }

        public IReadOnlyList<EditorHighlightSpan> BuildLineHighlights(string lineText, int lineStartIndex, bool darkMode)
        {
            var spans = new List<EditorHighlightSpan>();
            if (string.IsNullOrEmpty(lineText))
                return spans;

            var palette = darkMode ? ColorPalette.Dark : ColorPalette.Light;
            var commentStart = FindCommentStartIndex(lineText);
            var codeText = commentStart >= 0 ? lineText[..commentStart] : lineText;

            var topLevelMatch = TopLevelRegex.Match(codeText);
            if (topLevelMatch.Success && _topLevelKeys.Contains(topLevelMatch.Groups["key"].Value))
            {
                spans.Add(new EditorHighlightSpan(
                    lineStartIndex + topLevelMatch.Groups["key"].Index,
                    topLevelMatch.Groups["key"].Length,
                    palette.TopLevelKey));
            }

            var stepMatch = StepCommandRegex.Match(codeText);
            var stepKey = stepMatch.Success ? stepMatch.Groups["key"].Value : string.Empty;
            var isRecognizedStepCommand = stepMatch.Success && _stepCommands.Contains(stepKey);

            if (isRecognizedStepCommand)
            {
                spans.Add(new EditorHighlightSpan(
                    lineStartIndex + stepMatch.Groups["key"].Index,
                    stepMatch.Groups["key"].Length,
                    palette.StepCommand));
            }
            else if (stepMatch.Success && _highlightedOptionKeys.Contains(stepKey))
            {
                spans.Add(new EditorHighlightSpan(
                    lineStartIndex + stepMatch.Groups["key"].Index,
                    stepMatch.Groups["key"].Length,
                    palette.StepOption));
            }

            var optionMatch = OptionKeyRegex.Match(codeText);
            if (optionMatch.Success &&
                _highlightedOptionKeys.Contains(optionMatch.Groups["key"].Value))
            {
                spans.Add(new EditorHighlightSpan(
                    lineStartIndex + optionMatch.Groups["key"].Index,
                    optionMatch.Groups["key"].Length,
                    palette.StepOption));
            }

            foreach (Match match in VariableRegex.Matches(codeText))
            {
                spans.Add(new EditorHighlightSpan(lineStartIndex + match.Index, match.Length, palette.Variable));
            }

            foreach (Match match in StringRegex.Matches(codeText))
            {
                spans.Add(new EditorHighlightSpan(lineStartIndex + match.Index, match.Length, palette.StringLiteral));
            }

            foreach (Match match in NumberRegex.Matches(codeText))
            {
                spans.Add(new EditorHighlightSpan(lineStartIndex + match.Index, match.Length, palette.Number));
            }

            foreach (Match match in BooleanRegex.Matches(codeText))
            {
                spans.Add(new EditorHighlightSpan(lineStartIndex + match.Index, match.Length, palette.BooleanOrNull));
            }

            if (commentStart >= 0)
            {
                spans.Add(new EditorHighlightSpan(lineStartIndex + commentStart, lineText.Length - commentStart, palette.Comment));
            }

            return spans;
        }

        private static int FindCommentStartIndex(string lineText)
        {
            var inSingleQuote = false;
            var inDoubleQuote = false;

            for (var i = 0; i < lineText.Length; i++)
            {
                var current = lineText[i];

                if (current == '"' && !inSingleQuote)
                {
                    if (!inDoubleQuote)
                    {
                        inDoubleQuote = true;
                    }
                    else if (!IsEscapedByBackslash(lineText, i))
                    {
                        inDoubleQuote = false;
                    }

                    continue;
                }

                if (current == '\'' && !inDoubleQuote)
                {
                    if (!inSingleQuote)
                    {
                        inSingleQuote = true;
                        continue;
                    }

                    if (i + 1 < lineText.Length && lineText[i + 1] == '\'')
                    {
                        i++;
                        continue;
                    }

                    inSingleQuote = false;
                    continue;
                }

                if (current == '#' && !inSingleQuote && !inDoubleQuote)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsEscapedByBackslash(string text, int index)
        {
            var backslashCount = 0;
            for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
            {
                backslashCount++;
            }

            return backslashCount % 2 == 1;
        }

        private static string[] SplitLines(string text)
        {
            return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        }

        private static int[] BuildLineStartIndices(string text)
        {
            var starts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    starts.Add(i + 1);
                    continue;
                }

                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }
            return starts.ToArray();
        }

        private sealed class ColorPalette
        {
            public required Color TopLevelKey { get; init; }
            public required Color StepCommand { get; init; }
            public required Color StepOption { get; init; }
            public required Color Variable { get; init; }
            public required Color StringLiteral { get; init; }
            public required Color Number { get; init; }
            public required Color BooleanOrNull { get; init; }
            public required Color Comment { get; init; }

            public static ColorPalette Light { get; } = new()
            {
                TopLevelKey = Color.FromArgb(0, 0, 180),
                StepCommand = Color.FromArgb(128, 0, 128),
                StepOption = Color.FromArgb(0, 128, 128),
                Variable = Color.FromArgb(255, 140, 0),
                StringLiteral = Color.FromArgb(163, 21, 21),
                Number = Color.FromArgb(9, 134, 88),
                BooleanOrNull = Color.FromArgb(0, 0, 180),
                Comment = Color.FromArgb(0, 128, 0)
            };

            public static ColorPalette Dark { get; } = new()
            {
                TopLevelKey = Color.FromArgb(86, 156, 214),
                StepCommand = Color.FromArgb(197, 134, 192),
                StepOption = Color.FromArgb(78, 201, 176),
                Variable = Color.FromArgb(215, 186, 125),
                StringLiteral = Color.FromArgb(206, 145, 120),
                Number = Color.FromArgb(181, 206, 168),
                BooleanOrNull = Color.FromArgb(86, 156, 214),
                Comment = Color.FromArgb(106, 153, 85)
            };
        }
    }
}
