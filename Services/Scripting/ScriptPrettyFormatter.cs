using YamlDotNet.Serialization;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Formats YAML scripts and preserves user layout only at stable anchors
    /// (document marker gap, top-level section prefixes, and step-item prefixes).
    /// </summary>
    public static class ScriptPrettyFormatter
    {
        public static string Format(string yamlText)
        {
            if (yamlText == null)
                throw new ArgumentNullException(nameof(yamlText));

            var snapshot = CaptureLayout(yamlText);
            var canonicalLines = Canonicalize(yamlText);
            var canonicalBlocks = BuildTopLevelBlocks(canonicalLines);

            if (canonicalBlocks.Count == 0)
                return RebuildFallback(snapshot, canonicalLines);

            var outputLines = new List<string>();

            if (snapshot.HasDocumentMarker)
            {
                outputLines.Add("---");
                for (int i = 0; i < snapshot.BlankLinesAfterDocumentMarker; i++)
                    outputLines.Add(string.Empty);
            }

            var sectionMatches = AlignMatches(
                snapshot.TopLevelSections.Select(s => s.AnchorKey).ToList(),
                canonicalBlocks.Select(s => s.AnchorKey).ToList(),
                StringComparer.OrdinalIgnoreCase);

            var snapshotByCanonicalIndex = sectionMatches.ToDictionary(
                pair => pair.right,
                pair => snapshot.TopLevelSections[pair.left]);

            for (int i = 0; i < canonicalBlocks.Count; i++)
            {
                snapshotByCanonicalIndex.TryGetValue(i, out var sectionLayout);
                if (sectionLayout != null && sectionLayout.PrefixLines.Count > 0)
                    outputLines.AddRange(sectionLayout.PrefixLines);

                var canonicalBlock = canonicalBlocks[i];
                if (sectionLayout != null &&
                    string.Equals(canonicalBlock.AnchorKey, "steps", StringComparison.OrdinalIgnoreCase))
                {
                    outputLines.AddRange(RebuildStepsBlock(canonicalBlock.Lines, sectionLayout));
                }
                else
                {
                    outputLines.AddRange(canonicalBlock.Lines);
                }
            }

            return JoinLines(outputLines, snapshot.NewLine);
        }

        private static PrettyLayoutSnapshot CaptureLayout(string yamlText)
        {
            var lines = SplitLines(yamlText);
            var snapshot = new PrettyLayoutSnapshot
            {
                NewLine = DetectNewLine(yamlText)
            };

            var docMarkerIndex = FindDocumentMarkerIndex(lines);
            if (docMarkerIndex >= 0)
            {
                snapshot.HasDocumentMarker = true;
                snapshot.BlankLinesAfterDocumentMarker = CountBlankLinesAfter(lines, docMarkerIndex);
            }

            var topLevelBlocks = BuildTopLevelBlocks(lines);
            for (int i = 0; i < topLevelBlocks.Count; i++)
            {
                var block = topLevelBlocks[i];
                var sectionLayout = new TopLevelSectionLayout
                {
                    AnchorKey = block.AnchorKey
                };

                var prefixLines = GetContiguousBlankOrCommentPrefix(lines, block.StartIndex);

                // When a document marker exists, preserve its immediate blank run via
                // BlankLinesAfterDocumentMarker and keep only the remaining prefix here.
                if (i == 0 && snapshot.HasDocumentMarker && snapshot.BlankLinesAfterDocumentMarker > 0)
                    RemoveLeadingBlankLines(prefixLines, snapshot.BlankLinesAfterDocumentMarker);

                sectionLayout.PrefixLines.AddRange(prefixLines);

                if (string.Equals(block.AnchorKey, "steps", StringComparison.OrdinalIgnoreCase))
                    CaptureStepLayouts(block.Lines, sectionLayout);

                snapshot.TopLevelSections.Add(sectionLayout);
            }

            return snapshot;
        }

        private static void CaptureStepLayouts(IReadOnlyList<string> stepSectionLines, TopLevelSectionLayout sectionLayout)
        {
            var itemStarts = FindStepItemStarts(stepSectionLines);
            if (itemStarts.Count < 2)
                return;

            for (int i = 1; i < itemStarts.Count; i++)
            {
                var itemStart = itemStarts[i];
                var prefixLines = GetContiguousBlankOrCommentPrefix(stepSectionLines, itemStart);
                if (prefixLines.Count == 0)
                    continue;

                sectionLayout.StepLayouts.Add(new StepLayout
                {
                    TargetItemToken = NormalizeStepToken(stepSectionLines[itemStart]),
                    PrefixLines = prefixLines
                });
            }
        }

        private static List<string> RebuildStepsBlock(IReadOnlyList<string> canonicalStepSectionLines, TopLevelSectionLayout sectionLayout)
        {
            var parts = ParseStepSectionParts(canonicalStepSectionLines);
            if (parts.ItemBlocks.Count == 0 || sectionLayout.StepLayouts.Count == 0)
                return canonicalStepSectionLines.ToList();

            var stepMatches = AlignMatches(
                sectionLayout.StepLayouts.Select(s => s.TargetItemToken).ToList(),
                parts.ItemBlocks.Select(block => NormalizeStepToken(block[0])).ToList(),
                StringComparer.Ordinal);

            var layoutByCanonicalItemIndex = stepMatches.ToDictionary(
                pair => pair.right,
                pair => sectionLayout.StepLayouts[pair.left]);

            var rebuilt = new List<string>
            {
                parts.HeaderLine
            };
            rebuilt.AddRange(parts.PreambleLines);

            for (int i = 0; i < parts.ItemBlocks.Count; i++)
            {
                if (i > 0 && layoutByCanonicalItemIndex.TryGetValue(i, out var stepLayout))
                    rebuilt.AddRange(stepLayout.PrefixLines);

                rebuilt.AddRange(parts.ItemBlocks[i]);
            }

            rebuilt.AddRange(parts.EpilogueLines);
            return rebuilt;
        }

        private static StepSectionParts ParseStepSectionParts(IReadOnlyList<string> lines)
        {
            if (lines.Count == 0)
                return StepSectionParts.Empty;

            var itemStarts = FindStepItemStarts(lines);
            if (itemStarts.Count == 0)
            {
                return new StepSectionParts
                {
                    HeaderLine = lines[0],
                    PreambleLines = lines.Skip(1).ToList()
                };
            }

            var firstItemStart = itemStarts[0];
            var parts = new StepSectionParts
            {
                HeaderLine = lines[0],
                PreambleLines = lines.Skip(1).Take(firstItemStart - 1).ToList()
            };

            for (int i = 0; i < itemStarts.Count; i++)
            {
                var start = itemStarts[i];
                var end = (i + 1 < itemStarts.Count) ? itemStarts[i + 1] : lines.Count;
                parts.ItemBlocks.Add(lines.Skip(start).Take(end - start).ToList());
            }

            var lastItemEnd = itemStarts[^1] + parts.ItemBlocks[^1].Count;
            if (lastItemEnd < lines.Count)
                parts.EpilogueLines = lines.Skip(lastItemEnd).ToList();

            return parts;
        }

        private static List<int> FindStepItemStarts(IReadOnlyList<string> lines)
        {
            var starts = new List<int>();
            if (lines.Count < 2)
                return starts;

            var itemIndent = FindStepItemIndent(lines);
            if (itemIndent < 0)
                return starts;

            for (int i = 1; i < lines.Count; i++)
            {
                if (TryGetSequenceItemIndent(lines[i], out var indent) && indent == itemIndent)
                    starts.Add(i);
            }

            return starts;
        }

        private static int FindStepItemIndent(IReadOnlyList<string> lines)
        {
            for (int i = 1; i < lines.Count; i++)
            {
                if (TryGetSequenceItemIndent(lines[i], out var indent))
                    return indent;
            }

            return -1;
        }

        private static bool TryGetSequenceItemIndent(string line, out int indent)
        {
            indent = 0;
            if (string.IsNullOrWhiteSpace(line) || IsFullLineComment(line))
                return false;

            indent = GetIndent(line);
            var trimmed = line.TrimStart();
            return trimmed == "-" || trimmed.StartsWith("- ", StringComparison.Ordinal);
        }

        private static List<string> Canonicalize(string yamlText)
        {
            var deserializer = new DeserializerBuilder().Build();
            var serializer = new SerializerBuilder()
                .WithIndentedSequences()
                .Build();

            var yamlObject = deserializer.Deserialize<object>(yamlText);
            var canonical = serializer.Serialize(yamlObject);
            var lines = SplitLines(canonical);

            TrimTrailingBlankTail(lines);

            if (lines.Count > 0 && string.Equals(lines[^1].Trim(), "...", StringComparison.Ordinal))
            {
                lines.RemoveAt(lines.Count - 1);
                TrimTrailingBlankTail(lines);
            }

            if (lines.Count > 0 && string.Equals(lines[0].Trim(), "---", StringComparison.Ordinal))
                lines.RemoveAt(0);

            return lines;
        }

        private static List<TopLevelBlock> BuildTopLevelBlocks(IReadOnlyList<string> lines)
        {
            var starts = new List<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (IsTopLevelKeyLine(lines[i]))
                    starts.Add(i);
            }

            var blocks = new List<TopLevelBlock>();
            for (int i = 0; i < starts.Count; i++)
            {
                var start = starts[i];
                var end = (i + 1 < starts.Count) ? starts[i + 1] : lines.Count;
                var blockLines = lines.Skip(start).Take(end - start).ToList();
                blocks.Add(new TopLevelBlock
                {
                    StartIndex = start,
                    AnchorKey = ExtractTopLevelKey(blockLines[0]),
                    Lines = blockLines
                });
            }

            return blocks;
        }

        private static bool IsTopLevelKeyLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || IsFullLineComment(line))
                return false;

            if (GetIndent(line) != 0)
                return false;

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("-", StringComparison.Ordinal))
                return false;

            var colonIndex = trimmed.IndexOf(':');
            return colonIndex > 0;
        }

        private static string ExtractTopLevelKey(string line)
        {
            var trimmed = line.TrimStart();
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                return string.Empty;

            var key = trimmed[..colonIndex].Trim();
            if ((key.StartsWith("\"", StringComparison.Ordinal) && key.EndsWith("\"", StringComparison.Ordinal)) ||
                (key.StartsWith("'", StringComparison.Ordinal) && key.EndsWith("'", StringComparison.Ordinal)))
            {
                key = key[1..^1];
            }

            return key.ToLowerInvariant();
        }

        private static string NormalizeStepToken(string line)
        {
            return line.Trim();
        }

        private static List<string> GetContiguousBlankOrCommentPrefix(IReadOnlyList<string> lines, int anchorIndex)
        {
            var prefix = new List<string>();
            for (int i = anchorIndex - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (!IsBlankOrComment(line))
                    break;
                prefix.Add(line);
            }

            prefix.Reverse();
            return prefix;
        }

        private static bool IsBlankOrComment(string line)
        {
            return string.IsNullOrWhiteSpace(line) || IsFullLineComment(line);
        }

        private static bool IsFullLineComment(string line)
        {
            return line.TrimStart().StartsWith("#", StringComparison.Ordinal);
        }

        private static int GetIndent(string line)
        {
            int indent = 0;
            while (indent < line.Length && char.IsWhiteSpace(line[indent]) && line[indent] != '\r' && line[indent] != '\n')
                indent++;
            return indent;
        }

        private static int FindDocumentMarkerIndex(IReadOnlyList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || IsFullLineComment(line))
                    continue;

                return string.Equals(line.Trim(), "---", StringComparison.Ordinal) ? i : -1;
            }

            return -1;
        }

        private static int CountBlankLinesAfter(IReadOnlyList<string> lines, int lineIndex)
        {
            int count = 0;
            for (int i = lineIndex + 1; i < lines.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                    break;
                count++;
            }

            return count;
        }

        private static void RemoveLeadingBlankLines(List<string> lines, int maxToRemove)
        {
            int removed = 0;
            while (removed < maxToRemove && lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            {
                lines.RemoveAt(0);
                removed++;
            }
        }

        private static void TrimTrailingBlankTail(List<string> lines)
        {
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                lines.RemoveAt(lines.Count - 1);
        }

        private static string DetectNewLine(string text)
        {
            return text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        }

        private static List<string> SplitLines(string text)
        {
            var normalized = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
            return normalized.Split('\n', StringSplitOptions.None).ToList();
        }

        private static string JoinLines(IReadOnlyList<string> lines, string newLine)
        {
            return string.Join(newLine, lines);
        }

        private static string RebuildFallback(PrettyLayoutSnapshot snapshot, IReadOnlyList<string> canonicalLines)
        {
            var result = new List<string>();
            if (snapshot.HasDocumentMarker)
            {
                result.Add("---");
                for (int i = 0; i < snapshot.BlankLinesAfterDocumentMarker; i++)
                    result.Add(string.Empty);
            }

            result.AddRange(canonicalLines);
            return JoinLines(result, snapshot.NewLine);
        }

        private static List<(int left, int right)> AlignMatches(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right,
            IEqualityComparer<string> comparer)
        {
            int n = left.Count;
            int m = right.Count;
            var dp = new int[n + 1, m + 1];

            for (int i = n - 1; i >= 0; i--)
            {
                for (int j = m - 1; j >= 0; j--)
                {
                    if (comparer.Equals(left[i], right[j]))
                    {
                        dp[i, j] = dp[i + 1, j + 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                    }
                }
            }

            var matches = new List<(int left, int right)>();
            int x = 0;
            int y = 0;
            while (x < n && y < m)
            {
                if (comparer.Equals(left[x], right[y]) && dp[x, y] == dp[x + 1, y + 1] + 1)
                {
                    matches.Add((x, y));
                    x++;
                    y++;
                    continue;
                }

                if (dp[x + 1, y] >= dp[x, y + 1])
                    x++;
                else
                    y++;
            }

            return matches;
        }
    }

    internal sealed class PrettyLayoutSnapshot
    {
        public string NewLine { get; set; } = "\n";
        public bool HasDocumentMarker { get; set; }
        public int BlankLinesAfterDocumentMarker { get; set; }
        public List<TopLevelSectionLayout> TopLevelSections { get; } = new();
    }

    internal sealed class TopLevelSectionLayout
    {
        public string AnchorKey { get; set; } = string.Empty;
        public List<string> PrefixLines { get; } = new();
        public List<StepLayout> StepLayouts { get; } = new();
    }

    internal sealed class StepLayout
    {
        public string TargetItemToken { get; set; } = string.Empty;
        public List<string> PrefixLines { get; set; } = new();
    }

    internal sealed class TopLevelBlock
    {
        public int StartIndex { get; set; }
        public string AnchorKey { get; set; } = string.Empty;
        public List<string> Lines { get; set; } = new();
    }

    internal sealed class StepSectionParts
    {
        public static readonly StepSectionParts Empty = new();

        public string HeaderLine { get; set; } = string.Empty;
        public List<string> PreambleLines { get; set; } = new();
        public List<List<string>> ItemBlocks { get; } = new();
        public List<string> EpilogueLines { get; set; } = new();
    }
}
