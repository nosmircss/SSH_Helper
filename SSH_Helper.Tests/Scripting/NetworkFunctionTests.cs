using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class NetworkFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    [Fact]
    public async Task IsValidIp_ValidIPv4_True()
    {
        (await Eval("is_valid_ip(\"192.168.1.1\")")).Should().Be("True");
    }

    [Fact]
    public async Task IsValidIp_ValidIPv6_True()
    {
        (await Eval("is_valid_ip(\"2001:db8::1\")")).Should().Be("True");
    }

    [Fact]
    public async Task IsValidIp_Malformed_False()
    {
        (await Eval("is_valid_ip(\"999.1.1.1\")")).Should().Be("False");
        (await Eval("is_valid_ip(\"not-an-ip\")")).Should().Be("False");
    }

    [Fact]
    public async Task IpVersion_ReportsFamily()
    {
        (await Eval("ip_version(\"10.0.0.1\")")).Should().Be("4");
        (await Eval("ip_version(\"2001:db8::1\")")).Should().Be("6");
    }

    [Fact]
    public async Task IpVersion_Malformed_Empty()
    {
        (await Eval("ip_version(\"nope\")")).Should().BeEmpty();
    }

    [Fact]
    public async Task IpInCidr_InsideRange_True()
    {
        (await Eval("ip_in_cidr(\"10.1.2.3\", \"10.0.0.0/8\")")).Should().Be("True");
    }

    [Fact]
    public async Task IpInCidr_OutsideRange_False()
    {
        (await Eval("ip_in_cidr(\"192.168.1.1\", \"10.0.0.0/8\")")).Should().Be("False");
    }

    [Fact]
    public async Task IpInCidr_BoundaryAddresses()
    {
        (await Eval("ip_in_cidr(\"10.0.0.0\", \"10.0.0.0/8\")")).Should().Be("True");
        (await Eval("ip_in_cidr(\"10.255.255.255\", \"10.0.0.0/8\")")).Should().Be("True");
        (await Eval("ip_in_cidr(\"11.0.0.0\", \"10.0.0.0/8\")")).Should().Be("False");
    }

    [Fact]
    public async Task IpInCidr_NonCanonicalBase_StillWorks()
    {
        // base address has host bits set; membership math must still be correct
        (await Eval("ip_in_cidr(\"10.1.2.3\", \"10.9.9.9/8\")")).Should().Be("True");
    }

    [Fact]
    public async Task IpInCidr_Malformed_False()
    {
        (await Eval("ip_in_cidr(\"garbage\", \"10.0.0.0/8\")")).Should().Be("False");
        (await Eval("ip_in_cidr(\"10.0.0.1\", \"not-a-cidr\")")).Should().Be("False");
    }

    [Fact]
    public async Task UrlHost_And_Port()
    {
        (await Eval("url_host(\"https://example.com:8443/path\")")).Should().Be("example.com");
        (await Eval("url_port(\"https://example.com:8443/path\")")).Should().Be("8443");
    }

    [Fact]
    public async Task UrlHost_Malformed_Empty()
    {
        (await Eval("url_host(\"::::\")")).Should().BeEmpty();
    }
}
