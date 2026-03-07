using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class CredentialTargetsTests
{
    [Fact]
    public void DefaultPasswordTarget_UsesExpectedPrefix()
    {
        CredentialTargets.DefaultPasswordTarget.Should().Be("SSH_Helper:default");
    }

    [Fact]
    public void HostPasswordTarget_TrimsAndFormatsValues()
    {
        var target = CredentialTargets.HostPasswordTarget(" host1 ", " admin ");

        target.Should().Be("SSH_Helper:host:host1|user:admin");
    }

    [Fact]
    public void JobPasswordTarget_WithValidGuid_ReturnsExpectedFormat()
    {
        var target = CredentialTargets.JobPasswordTarget("abc123def456");

        target.Should().Be("SSH_Helper:job:abc123def456");
    }

    [Fact]
    public void JobPasswordTarget_TrimsWhitespace()
    {
        var target = CredentialTargets.JobPasswordTarget(" abc123 ");

        target.Should().Be("SSH_Helper:job:abc123");
    }

    [Fact]
    public void JobPasswordTarget_WithNull_ReturnsSafeEmpty()
    {
        var target = CredentialTargets.JobPasswordTarget(null!);

        target.Should().Be("SSH_Helper:job:");
    }

    [Fact]
    public void JobPasswordTarget_WithEmptyString_ReturnsSafeEmpty()
    {
        var target = CredentialTargets.JobPasswordTarget("");

        target.Should().Be("SSH_Helper:job:");
    }
}
