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
}
