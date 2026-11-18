using System.Reflection;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ConnectionPortPreviewTests
{
    [Theory]
    [InlineData("router", " ", "22,222,2222")]
    [InlineData("router:22", "bad,list", "22")]
    public void Preview_PreservesSelectedRawPolicy(string address, string rowPort, string expected)
    {
        var host = HostConnection.Parse(address);
        ConnectionPortPolicyService.Apply(host, new Dictionary<string, string> { ["port"] = rowPort },
            new Dictionary<string, string> { ["port"] = "22,222,2222" }, null);
        // The variables map is the existing bridge contract, not the legacy numeric compatibility field.
        host.Variables["port"].Should().Be(expected);
        host.IsPortResolvedForRun.Should().BeFalse();
    }

    [Fact]
    public void Help_ExplainsBlankInheritanceAndDefaults()
    {
        var help = ConnectionPortPolicyService.HelpText;
        help.Should().Contain("22,222,2222").And.Contain("Blank inherits environment")
            .And.Contain("ssh").And.Contain("tcp").And.Contain("default 2");
        var hints = (Dictionary<string, string>)typeof(Form1).GetField(
            "SpecialHostGridColumnTooltips", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        hints["port"].Should().Contain("22,222,2222").And.Contain("Blank inherits");
    }
}
