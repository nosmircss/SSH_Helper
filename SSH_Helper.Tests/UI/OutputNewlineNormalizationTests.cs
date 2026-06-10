using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

/// <summary>
/// Verifies Form1.NormalizeNewlinesForDisplay converts lone LF to CRLF so multiline
/// output (e.g. print: "\n\n...") renders line breaks in the WinForms TextBox sink.
/// </summary>
public class OutputNewlineNormalizationTests
{
    [Fact]
    public void LoneLf_IsConvertedToCrlf()
    {
        SSH_Helper.Form1.NormalizeNewlinesForDisplay("\n\nHELLO")
            .Should().Be("\r\n\r\nHELLO");
    }

    [Fact]
    public void ExistingCrlf_IsLeftUnchanged()
    {
        SSH_Helper.Form1.NormalizeNewlinesForDisplay("a\r\nb\r\nc")
            .Should().Be("a\r\nb\r\nc");
    }

    [Fact]
    public void MixedEndings_OnlyLoneLfConverted()
    {
        SSH_Helper.Form1.NormalizeNewlinesForDisplay("a\r\nb\nc")
            .Should().Be("a\r\nb\r\nc");
    }

    [Fact]
    public void LoneCr_IsLeftUnchanged()
    {
        // bare CR (e.g. terminal carriage returns) must not be touched
        SSH_Helper.Form1.NormalizeNewlinesForDisplay("a\rb")
            .Should().Be("a\rb");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no breaks here")]
    public void NoLf_ReturnsInputUnchanged(string? input)
    {
        SSH_Helper.Form1.NormalizeNewlinesForDisplay(input!)
            .Should().Be(input);
    }
}
