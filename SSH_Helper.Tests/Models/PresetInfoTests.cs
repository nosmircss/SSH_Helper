using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class PresetInfoTests
{
    [Fact]
    public void Commands_LfLineEndings_AreNormalizedToCrLf()
    {
        var preset = new PresetInfo
        {
            Commands = "line1\nline2\nline3"
        };

        preset.Commands.Should().Be("line1\r\nline2\r\nline3");
    }

    [Fact]
    public void Commands_CrLineEndings_AreNormalizedToCrLf()
    {
        var preset = new PresetInfo
        {
            Commands = "line1\rline2\rline3"
        };

        preset.Commands.Should().Be("line1\r\nline2\r\nline3");
    }

    [Fact]
    public void Commands_CrLfLineEndings_RemainCrLf()
    {
        var preset = new PresetInfo
        {
            Commands = "line1\r\nline2\r\nline3"
        };

        preset.Commands.Should().Be("line1\r\nline2\r\nline3");
    }

    [Fact]
    public void Commands_LiteralEscapedNewlineSequence_IsNotConverted()
    {
        var preset = new PresetInfo
        {
            Commands = "echo \\n"
        };

        preset.Commands.Should().Be("echo \\n");
    }
}
