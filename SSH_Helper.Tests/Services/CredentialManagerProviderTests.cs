using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class CredentialManagerProviderTests
{
    [Fact]
    public void TryGetPassword_EmptyTarget_ReturnsFalse()
    {
        var provider = new CredentialManagerProvider();

        var result = provider.TryGetPassword("", out _, out _);

        result.Should().BeFalse();
    }

    [Fact]
    public void SavePassword_EmptyTarget_ReturnsFalse()
    {
        var provider = new CredentialManagerProvider();

        var result = provider.SavePassword("", "user", "secret");

        result.Should().BeFalse();
    }

    [Fact]
    public void DeletePassword_EmptyTarget_ReturnsFalse()
    {
        var provider = new CredentialManagerProvider();

        var result = provider.DeletePassword("");

        result.Should().BeFalse();
    }
}
