using System.Text;

namespace SSH_Helper.Services.Editor
{
    public readonly struct EditorTextEdit
    {
        public EditorTextEdit(string text, int selectionStart, int selectionLength)
        {
            Text = text;
            SelectionStart = selectionStart;
            SelectionLength = selectionLength;
        }

        public string Text { get; }
        public int SelectionStart { get; }
        public int SelectionLength { get; }
    }

    internal static class EditorTextUtilities
    {
        public static EditorTextEdit ApplyIndentation(
            string text,
            int selectionStart,
            int selectionLength,
            int indentSize,
            bool outdent,
            bool useSpacesForTab)
        {
            text ??= string.Empty;
            var safeIndentSize = Math.Clamp(indentSize, 1, 8);
            var safeSelectionStart = Math.Clamp(selectionStart, 0, text.Length);
            var safeSelectionEnd = Math.Clamp(safeSelectionStart + Math.Max(0, selectionLength), 0, text.Length);

            var lineStarts = GetLineStartIndices(text);
            var firstLineIndex = GetLineIndex(lineStarts, safeSelectionStart);
            var lastLineSourceIndex = Math.Max(safeSelectionStart, safeSelectionEnd - 1);
            var lastLineIndex = GetLineIndex(lineStarts, lastLineSourceIndex);

            var blockStart = lineStarts[firstLineIndex];
            var blockEnd = lastLineIndex + 1 < lineStarts.Count ? lineStarts[lastLineIndex + 1] : text.Length;
            var blockText = text.Substring(blockStart, blockEnd - blockStart);
            var originalSegments = SplitLinesPreserveTerminators(blockText);
            var transformedSegments = new List<string>(originalSegments.Count);

            var lineDeltas = new List<int>(originalSegments.Count);
            foreach (var segment in originalSegments)
            {
                SplitLineAndTerminator(segment, out var lineText, out var terminator);
                var transformedLine = outdent
                    ? OutdentLine(lineText, safeIndentSize, useSpacesForTab)
                    : IndentLine(lineText, safeIndentSize, useSpacesForTab);

                transformedSegments.Add(transformedLine + terminator);
                lineDeltas.Add(transformedLine.Length - lineText.Length);
            }

            var transformedBlock = string.Concat(transformedSegments);
            var newText = text.Substring(0, blockStart) + transformedBlock + text.Substring(blockEnd);

            var firstLineDelta = lineDeltas.Count > 0 ? lineDeltas[0] : 0;
            var totalDelta = transformedBlock.Length - blockText.Length;

            if (selectionLength <= 0)
            {
                var newCaret = Math.Max(0, safeSelectionStart + firstLineDelta);
                return new EditorTextEdit(newText, newCaret, 0);
            }

            var newSelectionStart = Math.Max(0, safeSelectionStart + firstLineDelta);
            var newSelectionLength = Math.Max(0, selectionLength + totalDelta);
            return new EditorTextEdit(newText, newSelectionStart, newSelectionLength);
        }

        public static EditorTextEdit ApplySmartEnter(
            string text,
            int selectionStart,
            int selectionLength,
            int indentSize,
            bool preserveBlankLineBetweenSteps)
        {
            text ??= string.Empty;
            var safeIndentSize = Math.Clamp(indentSize, 1, 8);
            var safeSelectionStart = Math.Clamp(selectionStart, 0, text.Length);
            var safeSelectionLength = Math.Max(0, selectionLength);

            var baseText = text;
            if (safeSelectionLength > 0)
            {
                baseText = text.Remove(safeSelectionStart, Math.Min(safeSelectionLength, text.Length - safeSelectionStart));
            }

            var caretIndex = safeSelectionStart;
            var currentLineStart = FindLineStart(baseText, caretIndex);
            var currentLineEnd = FindLineEnd(baseText, caretIndex);
            var currentLine = baseText.Substring(currentLineStart, currentLineEnd - currentLineStart);
            var textBeforeCaretOnLine = baseText.Substring(currentLineStart, caretIndex - currentLineStart);
            var lineIndent = GetLeadingWhitespace(currentLine);
            var trimmedBeforeCaret = textBeforeCaretOnLine.Trim();
            var lineEnding = ResolveLineEnding(baseText, currentLineEnd);

            var insertion = lineEnding + lineIndent;
            if (!string.IsNullOrEmpty(trimmedBeforeCaret))
            {
                if (trimmedBeforeCaret.StartsWith("- ", StringComparison.Ordinal))
                {
                    if (trimmedBeforeCaret.Contains(':'))
                    {
                        insertion += new string(' ', safeIndentSize);
                    }
                    else
                    {
                        insertion += "- ";
                    }
                }
                else if (trimmedBeforeCaret.EndsWith(":", StringComparison.Ordinal))
                {
                    insertion += new string(' ', safeIndentSize);
                }
            }
            else if (preserveBlankLineBetweenSteps && IsLineBetweenStepItems(baseText, currentLineStart, currentLineEnd))
            {
                insertion = lineEnding + lineIndent;
            }

            var newText = baseText.Insert(caretIndex, insertion);
            var newCaret = caretIndex + insertion.Length;
            return new EditorTextEdit(newText, newCaret, 0);
        }

        public static EditorTextEdit ApplySiblingStepEnter(
            string text,
            int selectionStart,
            int selectionLength,
            int indentSize)
        {
            text ??= string.Empty;
            var safeIndentSize = Math.Clamp(indentSize, 1, 8);
            var safeSelectionStart = Math.Clamp(selectionStart, 0, text.Length);
            var safeSelectionLength = Math.Max(0, selectionLength);

            var baseText = text;
            if (safeSelectionLength > 0)
            {
                baseText = text.Remove(safeSelectionStart, Math.Min(safeSelectionLength, text.Length - safeSelectionStart));
            }

            var caretIndex = safeSelectionStart;
            var currentLineStart = FindLineStart(baseText, caretIndex);
            var currentLineEnd = FindLineEnd(baseText, caretIndex);
            var currentLine = baseText.Substring(currentLineStart, currentLineEnd - currentLineStart);
            var lineEnding = ResolveLineEnding(baseText, currentLineEnd);
            var stepIndent = ResolveSiblingStepIndent(baseText, currentLineStart, currentLine, safeIndentSize);

            var insertion = lineEnding + new string(' ', stepIndent) + "- ";
            var newText = baseText.Insert(caretIndex, insertion);
            var newCaret = caretIndex + insertion.Length;
            return new EditorTextEdit(newText, newCaret, 0);
        }

        private static string IndentLine(string lineText, int indentSize, bool useSpacesForTab)
        {
            if (lineText.Length == 0)
                return useSpacesForTab ? new string(' ', indentSize) : "\t";

            return (useSpacesForTab ? new string(' ', indentSize) : "\t") + lineText;
        }

        private static string OutdentLine(string lineText, int indentSize, bool useSpacesForTab)
        {
            if (lineText.Length == 0)
                return lineText;

            if (lineText.StartsWith('\t') && !useSpacesForTab)
                return lineText.Substring(1);

            var removable = 0;
            while (removable < lineText.Length && removable < indentSize && lineText[removable] == ' ')
            {
                removable++;
            }

            if (removable > 0)
                return lineText.Substring(removable);

            if (lineText.StartsWith('\t'))
                return lineText.Substring(1);

            return lineText;
        }

        private static bool IsLineBetweenStepItems(string text, int lineStart, int lineEnd)
        {
            var previousLine = GetNearestNonEmptyLine(text, lineStart - 1, searchBackward: true);
            var nextLine = GetNearestNonEmptyLine(text, lineEnd, searchBackward: false);

            return IsStepLine(previousLine) && IsStepLine(nextLine);
        }

        private static string GetNearestNonEmptyLine(string text, int startIndex, bool searchBackward)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (searchBackward)
            {
                var index = Math.Clamp(startIndex, 0, text.Length);
                while (index > 0)
                {
                    var lineStart = FindLineStart(text, index);
                    var lineEnd = FindLineEnd(text, index);
                    var line = text.Substring(lineStart, lineEnd - lineStart).Trim();
                    if (!string.IsNullOrEmpty(line))
                        return line;

                    index = lineStart - 1;
                }
                return string.Empty;
            }

            var forwardIndex = Math.Clamp(startIndex, 0, text.Length);
            while (forwardIndex < text.Length)
            {
                var lineStart = FindLineStart(text, forwardIndex);
                var lineEnd = FindLineEnd(text, forwardIndex);
                var line = text.Substring(lineStart, lineEnd - lineStart).Trim();
                if (!string.IsNullOrEmpty(line))
                    return line;

                forwardIndex = lineEnd < text.Length ? lineEnd + 1 : text.Length;
            }

            return string.Empty;
        }

        private static bool IsStepLine(string line)
        {
            return line.StartsWith("- ", StringComparison.Ordinal);
        }

        private static int ResolveSiblingStepIndent(
            string text,
            int currentLineStart,
            string currentLine,
            int indentSize)
        {
            if (TryGetStepLineIndent(currentLine, out var currentStepIndent))
            {
                return currentStepIndent;
            }

            var currentIndent = CountIndent(currentLine);
            var index = Math.Max(0, currentLineStart - 1);
            while (index >= 0)
            {
                var lineStart = FindLineStart(text, index);
                var lineEnd = FindLineEnd(text, lineStart);
                var line = text.Substring(lineStart, lineEnd - lineStart);
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    if (TryGetStepLineIndent(line, out var stepIndent))
                    {
                        return stepIndent;
                    }
                }

                if (lineStart == 0)
                {
                    break;
                }

                index = lineStart - 1;
            }

            return Math.Max(0, currentIndent - indentSize);
        }

        private static bool TryGetStepLineIndent(string line, out int indent)
        {
            indent = 0;
            if (line == null)
            {
                return false;
            }

            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                return false;
            }

            indent = CountIndent(line);
            return true;
        }

        private static string ResolveLineEnding(string text, int currentLineEnd)
        {
            if (!string.IsNullOrEmpty(text) && currentLineEnd >= 0 && currentLineEnd < text.Length)
            {
                if (text[currentLineEnd] == '\n')
                {
                    return "\n";
                }

                if (text[currentLineEnd] == '\r')
                {
                    return currentLineEnd + 1 < text.Length && text[currentLineEnd + 1] == '\n'
                        ? "\r\n"
                        : "\r";
                }
            }

            if (text.Contains("\r\n", StringComparison.Ordinal))
                return "\r\n";
            if (text.Contains('\n'))
                return "\n";
            if (text.Contains('\r'))
                return "\r";

            return Environment.NewLine;
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

        private static int FindLineEnd(string text, int index)
        {
            var i = Math.Clamp(index, 0, text.Length);
            while (i < text.Length)
            {
                if (text[i] == '\n')
                    break;
                i++;
            }
            return i;
        }

        private static string GetLeadingWhitespace(string line)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var c in line)
            {
                if (c == ' ' || c == '\t')
                {
                    builder.Append(c == '\t' ? "  " : " ");
                    continue;
                }
                break;
            }
            return builder.ToString();
        }

        private static int CountIndent(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return 0;
            }

            var count = 0;
            foreach (var c in line)
            {
                if (c == ' ')
                {
                    count++;
                    continue;
                }

                if (c == '\t')
                {
                    count += 2;
                    continue;
                }

                break;
            }

            return count;
        }

        private static List<int> GetLineStartIndices(string text)
        {
            var starts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }
            return starts;
        }

        private static int GetLineIndex(IReadOnlyList<int> lineStarts, int charIndex)
        {
            for (var i = lineStarts.Count - 1; i >= 0; i--)
            {
                if (charIndex >= lineStarts[i])
                    return i;
            }
            return 0;
        }

        private static List<string> SplitLinesPreserveTerminators(string block)
        {
            var segments = new List<string>();
            if (block.Length == 0)
            {
                segments.Add(string.Empty);
                return segments;
            }

            var currentStart = 0;
            for (var i = 0; i < block.Length; i++)
            {
                if (block[i] == '\n')
                {
                    segments.Add(block.Substring(currentStart, i - currentStart + 1));
                    currentStart = i + 1;
                }
            }

            if (currentStart < block.Length)
            {
                segments.Add(block.Substring(currentStart));
            }

            return segments;
        }

        private static void SplitLineAndTerminator(string segment, out string lineText, out string terminator)
        {
            if (segment.EndsWith("\r\n", StringComparison.Ordinal))
            {
                lineText = segment.Substring(0, segment.Length - 2);
                terminator = "\r\n";
                return;
            }

            if (segment.EndsWith('\n'))
            {
                lineText = segment.Substring(0, segment.Length - 1);
                terminator = "\n";
                return;
            }

            lineText = segment;
            terminator = string.Empty;
        }
    }
}
