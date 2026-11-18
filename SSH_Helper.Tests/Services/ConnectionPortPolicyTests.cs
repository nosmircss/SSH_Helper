using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConnectionPortPolicyTests
{
    private static Dictionary<string, string> Vars(params (string, string)[] values) =>
        values.ToDictionary(v => v.Item1, v => v.Item2);

    [Theory]
    [InlineData("router", " ", "22,222,2222", "22,222,2222", ConnectionPortSource.Environment, 22)]
    [InlineData("router", "22", "222,2222", "22", ConnectionPortSource.Row, 22)]
    [InlineData("router:22", "bad,list", "bad,list", "22", ConnectionPortSource.Endpoint, 22)]
    [InlineData("router", "", "", "2200", ConnectionPortSource.SshConfig, 2200)]
    public void Apply_SelectsSourceWithoutMutatingInputs(string address, string rowPort, string envPort,
        string raw, ConnectionPortSource source, int port)
    {
        var host = HostConnection.Parse(address);
        var row = Vars(("PoRt", rowPort));
        var env = Vars(("port", envPort));
        ConnectionPortPolicyService.Apply(host, row, env, new SshHostConfig { Port = 2200 });
        host.PortPolicy!.RawPort.Should().Be(raw);
        host.PortPolicy.Source.Should().Be(source);
        host.Port.Should().Be(port);
        row["PoRt"].Should().Be(rowPort);
        env["port"].Should().Be(envPort);
    }

    [Fact]
    public void Apply_ExplicitProgrammatic22IsNotAnImplicitDefault()
    {
        var explicitHost = new HostConnection { Port = 22 };
        explicitHost.ApplySshConfig(new SshHostConfig { Port = 2200 });
        explicitHost.Port.Should().Be(22);
        var implicitHost = new HostConnection();
        implicitHost.ApplySshConfig(new SshHostConfig { Port = 2200 });
        implicitHost.Port.Should().Be(2200);
        ConnectionPortPolicyService.Apply(explicitHost, Vars(), Vars(), new SshHostConfig { Port = 2200 });
        explicitHost.PortPolicy!.Source.Should().Be(ConnectionPortSource.Programmatic);
    }

    [Theory]
    [InlineData("22,")]
    [InlineData(",22")]
    [InlineData("22,,2222")]
    [InlineData("22,nope")]
    [InlineData("22,0")]
    [InlineData("22,65536")]
    [InlineData("22,2147483648")]
    public void ValidateDiscovery_ValidatesEntireList(string raw)
    {
        var action = () => ConnectionPortPolicyService.ValidateDiscovery(new(raw, ConnectionPortSource.Row, null, null));
        action.Should().Throw<InvalidOperationException>().WithMessage("*Row*");
        InputValidator.TryParsePortCandidates(raw, out var ports, out _).Should().BeFalse();
        ports.Should().BeEmpty();
    }

    [Fact]
    public void ValidateDiscovery_PreservesOrderWhitespaceAndDuplicates()
    {
        var result = ConnectionPortPolicyService.ValidateDiscovery(new(" 2222, 22,2222 ", ConnectionPortSource.Row, null, null));
        result.Ports.Should().Equal(2222, 22, 2222);
        result.Mode.Should().Be("ssh");
        result.Timeout.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Apply_SettingsInheritIndependentlyAndPreserveAlgorithms()
    {
        var host = HostConnection.Parse("router");
        ConnectionPortPolicyService.Apply(host,
            Vars(("port", "22,222"), ("port_check", " "), ("port_check_timeout", "3")),
            Vars(("PORT_CHECK", " TCP "), ("port_check_timeout", "9")),
            new SshHostConfig { User = "user", IdentityFile = "identity", Ciphers = new[] { "cipher" } });
        var result = ConnectionPortPolicyService.ValidateDiscovery(host.PortPolicy!);
        result.Mode.Should().Be("tcp");
        result.Timeout.Should().Be(TimeSpan.FromSeconds(3));
        host.Username.Should().Be("user");
        host.IdentityFile.Should().Be("identity");
        host.Ciphers.Should().Equal("cipher");
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("2147483", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("2147484", false)]
    [InlineData("2.5", false)]
    [InlineData("2147483648", false)]
    [InlineData("bad", false)]
    public void ValidateDiscovery_TimeoutBounds(string raw, bool valid) =>
        InputValidator.TryParsePortCheckTimeout(raw, out _).Should().Be(valid);

    [Theory]
    [InlineData("router:22", "bad,list", null)]
    [InlineData("router", "bad,list", "Row port")]
    [InlineData("router", "22", null)]
    public void Advisory_IsNonblockingAndValidatesOnlySelectedPolicy(string address, string port, string? expected)
    {
        var host = HostConnection.Parse(address);
        ConnectionPortPolicyService.Apply(host, Vars(("port", port)), Vars(), null);
        var advisory = ConnectionPortPolicyService.GetAdvisory(host);
        if (expected == null) advisory.Should().BeNull();
        else advisory.Should().Contain(expected).And.Contain("preconnect");
    }
}
