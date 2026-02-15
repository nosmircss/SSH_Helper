using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class PromptDetectorTests
{
    [Fact]
    public void BuildPromptRegex_ZshPrompt_MatchesDirectoryChange()
    {
        var regex = PromptDetector.BuildPromptRegex("chris@Chriss-Mac-mini ~ %");

        regex.IsMatch("chris@Chriss-Mac-mini /tmp %").Should().BeTrue();
        regex.IsMatch("chris@Chriss-Mac-mini /tmp % ").Should().BeTrue();
        regex.IsMatch("chris@Chriss-Mac-mini ~ %").Should().BeTrue();
        regex.IsMatch("Filesystem used 98%").Should().BeFalse();
    }

    [Fact]
    public void BuildPromptRegex_UserHostColonPrompt_MatchesDifferentPath()
    {
        var regex = PromptDetector.BuildPromptRegex("chris@mac:~$");

        regex.IsMatch("chris@mac:/tmp$").Should().BeTrue();
        regex.IsMatch("chris@mac:/Users/chris/src$").Should().BeTrue();
    }

    [Fact]
    public void BuildPromptRegex_ZshPrompt_DoesNotMatchDifferentHost()
    {
        var regex = PromptDetector.BuildPromptRegex("chris@Chriss-Mac-mini ~ %");

        regex.IsMatch("alex@Chriss-Mac-mini /tmp %").Should().BeFalse();
        regex.IsMatch("chris@Other-Mac-mini /tmp %").Should().BeFalse();
    }

    [Fact]
    public void BuildPromptRegex_CiscoPrompt_MatchesConfigModeChange()
    {
        var regex = PromptDetector.BuildPromptRegex("MSD903-DFWB#");

        regex.IsMatch("MSD903-DFWB (setting)#").Should().BeTrue();
    }
}
