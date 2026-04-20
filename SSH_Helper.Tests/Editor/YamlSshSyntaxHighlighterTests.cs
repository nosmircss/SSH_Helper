using System.Drawing;
using FluentAssertions;
using SSH_Helper.Services.Editor;
using Xunit;

namespace SSH_Helper.Tests.Editor;

public class YamlSshSyntaxHighlighterTests
{
    [Fact]
    public void BuildHighlights_ChangedLineOnly_ReturnsSpansWithinRequestedLine()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var lines = new List<string> { "steps:" };
        lines.AddRange(Enumerable.Range(1, 550).Select(index => $"  - send: item_{index} # comment"));
        var text = string.Join("\n", lines);

        var changedLineIndex = 250;
        var spans = highlighter.BuildHighlights(text, new[] { changedLineIndex }, darkMode: false);

        var lineStart = GetLineStart(text, changedLineIndex);
        var lineEnd = GetLineEnd(text, changedLineIndex);

        spans.Should().NotBeEmpty();
        spans.Should().OnlyContain(span => span.Start >= lineStart && span.Start + span.Length <= lineEnd);
    }

    [Fact]
    public void BuildLineHighlights_RecognizesCommandVariableAndCommentTokens()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var line = "  - send: ${token} # sample";

        var spans = highlighter.BuildLineHighlights(line, lineStartIndex: 0, darkMode: true);

        spans.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void BuildLineHighlights_RecognizesEnvironmentTopLevelKey()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var line = "environment: prod";

        var spans = highlighter.BuildLineHighlights(line, lineStartIndex: 0, darkMode: false);

        spans.Should().Contain(span => span.Start == 0 && span.Length == "environment".Length);
    }

    [Fact]
    public void BuildLineHighlights_RecognizesNestedTableColumnSequenceKey()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var line = "      - header: Port";
        var keyStart = line.IndexOf("header", StringComparison.Ordinal);

        var spans = highlighter.BuildLineHighlights(line, lineStartIndex: 0, darkMode: true);

        spans.Should().Contain(span => span.Start == keyStart && span.Length == "header".Length);
    }

    [Fact]
    public void BuildLineHighlights_RecognizesNestedTableColumnMappingKey()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var line = "        field: Port";
        var keyStart = line.IndexOf("field", StringComparison.Ordinal);

        var spans = highlighter.BuildLineHighlights(line, lineStartIndex: 0, darkMode: true);

        spans.Should().Contain(span => span.Start == keyStart && span.Length == "field".Length);
    }

    [Fact]
    public void BuildLineHighlights_FullyCommentedLine_WithQuotedText_UsesOnlyCommentColor()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var line = "  #prompt: \"Select interfaces to configure:\"";
        var commentStart = line.IndexOf('#');

        var spans = highlighter.BuildLineHighlights(line, lineStartIndex: 0, darkMode: true);

        spans.Should().ContainSingle();
        spans[0].Start.Should().Be(commentStart);
        spans[0].Length.Should().Be(line.Length - commentStart);
        spans[0].Color.Should().Be(GetDarkCommentColor());
    }

    [Fact]
    public void BuildLineHighlights_QuotedHashDoesNotStartComment_ButLaterUnquotedHashDoes()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var line = "prompt: \"Select #interfaces\" # \"comment text\"";
        var stringStart = line.IndexOf('"');
        var stringEnd = line.IndexOf("\" #", StringComparison.Ordinal);
        var stringLength = stringEnd - stringStart + 1;
        var commentStart = line.LastIndexOf('#');

        var spans = highlighter.BuildLineHighlights(line, lineStartIndex: 0, darkMode: true);

        spans.Should().Contain(span =>
            span.Start == stringStart &&
            span.Length == stringLength &&
            span.Color == GetDarkStringLiteralColor());

        spans.Should().Contain(span =>
            span.Start == commentStart &&
            span.Length == line.Length - commentStart &&
            span.Color == GetDarkCommentColor());

        spans.Should().NotContain(span =>
            span.Color == GetDarkStringLiteralColor() &&
            span.Start >= commentStart);
    }

    [Fact]
    public void BuildLineHighlights_QuotedHashWithoutUnquotedHash_DoesNotCreateCommentSpan()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var line = "prompt: \"Select #interfaces\"";
        var stringStart = line.IndexOf('"');
        var stringLength = line.Length - stringStart;

        var spans = highlighter.BuildLineHighlights(line, lineStartIndex: 0, darkMode: true);

        spans.Should().Contain(span =>
            span.Start == stringStart &&
            span.Length == stringLength &&
            span.Color == GetDarkStringLiteralColor());

        spans.Should().NotContain(span => span.Color == GetDarkCommentColor());
    }

    private static int GetLineStart(string text, int lineIndex)
    {
        var currentLine = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (currentLine == lineIndex)
                return i;

            if (text[i] == '\n')
                currentLine++;
        }

        return text.Length;
    }

    private static int GetLineEnd(string text, int lineIndex)
    {
        var start = GetLineStart(text, lineIndex);
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == '\n')
                return i;
        }
        return text.Length;
    }

    private static Color GetDarkCommentColor()
    {
        return Color.FromArgb(106, 153, 85);
    }

    private static Color GetDarkStringLiteralColor()
    {
        return Color.FromArgb(206, 145, 120);
    }
}
