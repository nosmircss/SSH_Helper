using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptPrettyFormatterTests
{
    [Fact]
    public void Format_WithDocumentMarker_PreservesImmediateBlankLineAfterMarker()
    {
        var input = """
---

name: Test
steps:
  - send: show version
""";

        var result = ScriptPrettyFormatter.Format(input);
        var lines = SplitLines(result);

        lines[0].Should().Be("---");
        lines[1].Should().BeEmpty();
        lines[2].TrimStart().Should().StartWith("name:");
    }

    [Fact]
    public void Format_PreservesBlankLinesBeforeTopLevelSections()
    {
        var input = """
name: Test


vars:
  retries: 3



steps:
  - send: show version
""";

        var result = ScriptPrettyFormatter.Format(input);
        var lines = SplitLines(result);

        var varsIndex = FindLineIndex(lines, "vars:");
        var stepsIndex = FindLineIndex(lines, "steps:");

        CountBlankLinesBefore(lines, varsIndex).Should().Be(2);
        CountBlankLinesBefore(lines, stepsIndex).Should().Be(3);
    }

    [Fact]
    public void Format_PreservesCommentAndBlankLineBeforeSecondStepItem()
    {
        var input = """
steps:
  - send: one
  # keep with second step

  - send: two
""";

        var result = ScriptPrettyFormatter.Format(input);
        var lines = SplitLines(result);
        var secondItemIndex = FindLineIndex(lines, "- send: two");

        lines[secondItemIndex - 1].Should().BeEmpty();
        lines[secondItemIndex - 2].TrimStart().Should().Be("# keep with second step");
    }

    [Fact]
    public void Format_PreservesCommentBlockBeforeTopLevelSection()
    {
        var input = """
name: Test
# keep with steps

steps:
  - send: test
""";

        var result = ScriptPrettyFormatter.Format(input);
        var lines = SplitLines(result);
        var stepsIndex = FindLineIndex(lines, "steps:");

        lines[stepsIndex - 1].Should().BeEmpty();
        lines[stepsIndex - 2].TrimStart().Should().Be("# keep with steps");
    }

    [Fact]
    public void Format_InlineCommentsAreNotGuaranteedToBePreserved()
    {
        var input = """
name: Test # inline comment
steps:
  - send: test
""";

        var result = ScriptPrettyFormatter.Format(input);

        result.Should().NotContain("# inline comment");
    }

    [Fact]
    public void Format_PreservesOriginalCrLfNewLineStyle()
    {
        var input = "---\r\n\r\nname: Test\r\nsteps:\r\n  - send: test\r\n";

        var result = ScriptPrettyFormatter.Format(input);

        result.Should().Contain("\r\n");
        result.Replace("\r\n", string.Empty).Should().NotContain("\n");
    }

    [Fact]
    public void Format_DropsUnmatchedSectionPrefixInsteadOfRelocatingIt()
    {
        var input = """
name: first

# should-not-move
name: second

steps:
  - send: test
""";

        var result = ScriptPrettyFormatter.Format(input);

        result.Should().NotContain("# should-not-move");
    }

    [Fact]
    public void Format_InvalidYaml_Throws()
    {
        Action act = () => ScriptPrettyFormatter.Format("steps:\n  - send: [");

        act.Should().Throw<Exception>();
    }

    private static List<string> SplitLines(string text)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return normalized.Split('\n', StringSplitOptions.None).ToList();
    }

    private static int FindLineIndex(IReadOnlyList<string> lines, string startsWithTrimmed)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith(startsWithTrimmed, StringComparison.Ordinal))
                return i;
        }

        throw new InvalidOperationException($"Line starting with '{startsWithTrimmed}' not found.");
    }

    private static int CountBlankLinesBefore(IReadOnlyList<string> lines, int index)
    {
        int count = 0;
        for (int i = index - 1; i >= 0; i--)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
                break;
            count++;
        }

        return count;
    }
}
