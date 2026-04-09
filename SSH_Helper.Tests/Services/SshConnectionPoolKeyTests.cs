using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class SshConnectionPoolKeyTests
{
    [Fact]
    public void CreateConnectionKey_SameInputs_ReturnsSameKey()
    {
        var host = new HostConnection
        {
            IpAddress = "10.0.0.1",
            Port = 22,
            IdentityFile = @"C:\\certs\\id_rsa",
            IdentityFilePassphrase = "phrase-1"
        };

        var left = SshConnectionPool.CreateConnectionKey(host, "admin", "password-1");
        var right = SshConnectionPool.CreateConnectionKey(host, "admin", "password-1");

        left.Should().Be(right);
    }

    [Fact]
    public void CreateConnectionKey_DifferentPassword_ReturnsDifferentKey()
    {
        var host = new HostConnection
        {
            IpAddress = "10.0.0.1",
            Port = 22,
            IdentityFile = @"C:\\certs\\id_rsa",
            IdentityFilePassphrase = "phrase-1"
        };

        var left = SshConnectionPool.CreateConnectionKey(host, "admin", "password-1");
        var right = SshConnectionPool.CreateConnectionKey(host, "admin", "password-2");

        left.Should().NotBe(right);
    }

    [Fact]
    public void CreateConnectionKey_DifferentIdentityFile_ReturnsDifferentKey()
    {
        var leftHost = new HostConnection
        {
            IpAddress = "10.0.0.1",
            Port = 22,
            IdentityFile = @"C:\\certs\\id_rsa_a",
            IdentityFilePassphrase = "phrase-1"
        };

        var rightHost = new HostConnection
        {
            IpAddress = "10.0.0.1",
            Port = 22,
            IdentityFile = @"C:\\certs\\id_rsa_b",
            IdentityFilePassphrase = "phrase-1"
        };

        var left = SshConnectionPool.CreateConnectionKey(leftHost, "admin", "password-1");
        var right = SshConnectionPool.CreateConnectionKey(rightHost, "admin", "password-1");

        left.Should().NotBe(right);
    }

    [Fact]
    public void CreateConnectionKey_DifferentIdentityPassphrase_ReturnsDifferentKey()
    {
        var leftHost = new HostConnection
        {
            IpAddress = "10.0.0.1",
            Port = 22,
            IdentityFile = @"C:\\certs\\id_rsa",
            IdentityFilePassphrase = "phrase-1"
        };

        var rightHost = new HostConnection
        {
            IpAddress = "10.0.0.1",
            Port = 22,
            IdentityFile = @"C:\\certs\\id_rsa",
            IdentityFilePassphrase = "phrase-2"
        };

        var left = SshConnectionPool.CreateConnectionKey(leftHost, "admin", "password-1");
        var right = SshConnectionPool.CreateConnectionKey(rightHost, "admin", "password-1");

        left.Should().NotBe(right);
    }
}
