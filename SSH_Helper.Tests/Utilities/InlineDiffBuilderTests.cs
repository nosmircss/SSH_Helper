using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class InlineDiffBuilderTests
{
    [Fact]
    public void Build_IdenticalContentWithDifferentLineEndings_ReturnsEmpty()
    {
        var diff = InlineDiffBuilder.Build("line1\r\nline2", "line1\nline2");

        diff.Should().BeEmpty();
    }

    [Fact]
    public void Build_ReplacedLine_ContainsRemovedAndAddedLines()
    {
        var diff = InlineDiffBuilder.Build("line1\nline2", "line1\nlineX");

        diff.Should().Contain(line => line.Kind == InlineDiffLineKind.Removed && line.Text == "- line2");
        diff.Should().Contain(line => line.Kind == InlineDiffLineKind.Added && line.Text == "+ lineX");
    }

    [Fact]
    public void Build_DistantChange_AddsCollapsedMarker()
    {
        var original = string.Join('\n', Enumerable.Range(1, 12).Select(index => $"line{index}"));
        var updatedLines = Enumerable.Range(1, 12).Select(index => $"line{index}").ToArray();
        updatedLines[10] = "line11-updated";
        var updated = string.Join('\n', updatedLines);

        var diff = InlineDiffBuilder.Build(original, updated, contextLines: 1, maxOutputLines: 200);

        diff.Should().Contain(line => line.Kind == InlineDiffLineKind.Meta && line.Text == "  ...");
    }

    [Fact]
    public void Build_IncludeAllLines_ShowsEntireScriptWithoutCollapsedMarker()
    {
        var original = string.Join('\n', Enumerable.Range(1, 12).Select(index => $"line{index}"));
        var updatedLines = Enumerable.Range(1, 12).Select(index => $"line{index}").ToArray();
        updatedLines[10] = "line11-updated";
        var updated = string.Join('\n', updatedLines);

        var diff = InlineDiffBuilder.Build(
            original,
            updated,
            contextLines: 1,
            maxOutputLines: 500,
            includeAllLines: true);

        diff.Should().NotContain(line => line.Kind == InlineDiffLineKind.Meta && line.Text == "  ...");
        diff.Should().Contain(line => line.Kind == InlineDiffLineKind.Context && line.Text == "  line1");
        diff.Should().Contain(line => line.Kind == InlineDiffLineKind.Context && line.Text == "  line12");
        diff.Should().Contain(line => line.Kind == InlineDiffLineKind.Removed && line.Text == "- line11");
        diff.Should().Contain(line => line.Kind == InlineDiffLineKind.Added && line.Text == "+ line11-updated");
    }
}
