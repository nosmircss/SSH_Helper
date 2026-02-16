namespace SSH_Helper.Utilities;

internal enum InlineDiffLineKind
{
    Context,
    Added,
    Removed,
    Meta
}

internal readonly record struct InlineDiffLine(InlineDiffLineKind Kind, string Text);

internal static class InlineDiffBuilder
{
    private const int MaxLcsCells = 2_000_000;
    private const string CollapsedMarker = "  ...";
    private const string TruncatedMarker = "  ... diff truncated";

    public static IReadOnlyList<InlineDiffLine> Build(
        string? originalText,
        string? updatedText,
        int contextLines = 2,
        int maxOutputLines = 350,
        bool includeAllLines = false)
    {
        if (contextLines < 0)
            throw new ArgumentOutOfRangeException(nameof(contextLines));
        if (maxOutputLines < 1)
            throw new ArgumentOutOfRangeException(nameof(maxOutputLines));

        var originalLines = SplitLines(NormalizeLineEndings(originalText));
        var updatedLines = SplitLines(NormalizeLineEndings(updatedText));

        if (originalLines.SequenceEqual(updatedLines, StringComparer.Ordinal))
            return [];

        var operations = (long)originalLines.Length * updatedLines.Length > MaxLcsCells
            ? BuildFallbackOperations(originalLines, updatedLines)
            : BuildLcsOperations(originalLines, updatedLines);

        return RenderOperations(operations, contextLines, maxOutputLines, includeAllLines);
    }

    private static string NormalizeLineEndings(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string[] SplitLines(string text)
    {
        if (text.Length == 0)
            return [];

        return text.Split('\n');
    }

    private static List<DiffOperation> BuildFallbackOperations(string[] originalLines, string[] updatedLines)
    {
        var maxLength = Math.Max(originalLines.Length, updatedLines.Length);
        var operations = new List<DiffOperation>(maxLength * 2);

        for (var index = 0; index < maxLength; index++)
        {
            var hasOriginal = index < originalLines.Length;
            var hasUpdated = index < updatedLines.Length;

            if (hasOriginal && hasUpdated)
            {
                if (string.Equals(originalLines[index], updatedLines[index], StringComparison.Ordinal))
                {
                    operations.Add(new DiffOperation(DiffOperationType.Equal, originalLines[index]));
                }
                else
                {
                    operations.Add(new DiffOperation(DiffOperationType.Removed, originalLines[index]));
                    operations.Add(new DiffOperation(DiffOperationType.Added, updatedLines[index]));
                }

                continue;
            }

            if (hasOriginal)
            {
                operations.Add(new DiffOperation(DiffOperationType.Removed, originalLines[index]));
                continue;
            }

            operations.Add(new DiffOperation(DiffOperationType.Added, updatedLines[index]));
        }

        return operations;
    }

    private static List<DiffOperation> BuildLcsOperations(string[] originalLines, string[] updatedLines)
    {
        var originalLength = originalLines.Length;
        var updatedLength = updatedLines.Length;
        var lcs = new int[originalLength + 1, updatedLength + 1];

        for (var i = originalLength - 1; i >= 0; i--)
        {
            for (var j = updatedLength - 1; j >= 0; j--)
            {
                lcs[i, j] = string.Equals(originalLines[i], updatedLines[j], StringComparison.Ordinal)
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var operations = new List<DiffOperation>(originalLength + updatedLength);
        var originalIndex = 0;
        var updatedIndex = 0;

        while (originalIndex < originalLength && updatedIndex < updatedLength)
        {
            if (string.Equals(originalLines[originalIndex], updatedLines[updatedIndex], StringComparison.Ordinal))
            {
                operations.Add(new DiffOperation(DiffOperationType.Equal, originalLines[originalIndex]));
                originalIndex++;
                updatedIndex++;
                continue;
            }

            if (lcs[originalIndex + 1, updatedIndex] >= lcs[originalIndex, updatedIndex + 1])
            {
                operations.Add(new DiffOperation(DiffOperationType.Removed, originalLines[originalIndex]));
                originalIndex++;
            }
            else
            {
                operations.Add(new DiffOperation(DiffOperationType.Added, updatedLines[updatedIndex]));
                updatedIndex++;
            }
        }

        while (originalIndex < originalLength)
        {
            operations.Add(new DiffOperation(DiffOperationType.Removed, originalLines[originalIndex]));
            originalIndex++;
        }

        while (updatedIndex < updatedLength)
        {
            operations.Add(new DiffOperation(DiffOperationType.Added, updatedLines[updatedIndex]));
            updatedIndex++;
        }

        return operations;
    }

    private static IReadOnlyList<InlineDiffLine> RenderOperations(
        IReadOnlyList<DiffOperation> operations,
        int contextLines,
        int maxOutputLines,
        bool includeAllLines)
    {
        if (operations.Count == 0)
            return [];

        if (includeAllLines)
        {
            var fullLines = new List<InlineDiffLine>(Math.Min(maxOutputLines + 1, operations.Count));
            foreach (var operation in operations)
            {
                if (!TryAppend(fullLines, FormatOperation(operation), maxOutputLines))
                    return fullLines;
            }

            return fullLines;
        }

        var include = new bool[operations.Count];
        var hasChanges = false;

        for (var index = 0; index < operations.Count; index++)
        {
            if (operations[index].Kind == DiffOperationType.Equal)
                continue;

            hasChanges = true;
            var start = Math.Max(0, index - contextLines);
            var end = Math.Min(operations.Count - 1, index + contextLines);
            for (var includeIndex = start; includeIndex <= end; includeIndex++)
            {
                include[includeIndex] = true;
            }
        }

        if (!hasChanges)
            return [];

        var lines = new List<InlineDiffLine>(Math.Min(maxOutputLines + 1, operations.Count));
        var skipped = false;

        for (var index = 0; index < operations.Count; index++)
        {
            if (!include[index])
            {
                skipped = true;
                continue;
            }

            if (skipped)
            {
                if (!TryAppend(lines, new InlineDiffLine(InlineDiffLineKind.Meta, CollapsedMarker), maxOutputLines))
                    return lines;
                skipped = false;
            }

            if (!TryAppend(lines, FormatOperation(operations[index]), maxOutputLines))
                return lines;
        }

        if (skipped)
        {
            TryAppend(lines, new InlineDiffLine(InlineDiffLineKind.Meta, CollapsedMarker), maxOutputLines);
        }

        return lines;
    }

    private static bool TryAppend(List<InlineDiffLine> lines, InlineDiffLine line, int maxOutputLines)
    {
        if (lines.Count < maxOutputLines)
        {
            lines.Add(line);
            return true;
        }

        lines[^1] = new InlineDiffLine(InlineDiffLineKind.Meta, TruncatedMarker);
        return false;
    }

    private static InlineDiffLine FormatOperation(DiffOperation operation)
    {
        return operation.Kind switch
        {
            DiffOperationType.Equal => new InlineDiffLine(InlineDiffLineKind.Context, $"  {operation.Text}"),
            DiffOperationType.Added => new InlineDiffLine(InlineDiffLineKind.Added, $"+ {operation.Text}"),
            DiffOperationType.Removed => new InlineDiffLine(InlineDiffLineKind.Removed, $"- {operation.Text}"),
            _ => new InlineDiffLine(InlineDiffLineKind.Context, $"  {operation.Text}")
        };
    }

    private readonly record struct DiffOperation(DiffOperationType Kind, string Text);

    private enum DiffOperationType
    {
        Equal,
        Added,
        Removed
    }
}
