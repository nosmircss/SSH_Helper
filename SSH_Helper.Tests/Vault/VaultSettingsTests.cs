using FluentAssertions;
using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Vault;

public class VaultSettingsTests
{
    [Fact]
    public void VaultSettings_Defaults_AreCorrect()
    {
        var settings = new VaultSettings();

        settings.Enabled.Should().BeFalse();
        settings.Profiles.Should().NotBeNull().And.BeEmpty();
        settings.DefaultProfileName.Should().BeEmpty();
    }

    [Fact]
    public void VaultProfileConfig_Defaults_AreCorrect()
    {
        var profile = new VaultProfileConfig();

        profile.Name.Should().BeEmpty();
        profile.Address.Should().BeEmpty();
        profile.Namespace.Should().BeEmpty();
        profile.MountPath.Should().Be("secret");
        profile.AuthMethod.Should().Be(VaultAuthMethod.Token);
        profile.AppRoleRoleId.Should().BeEmpty();
        profile.LdapUsername.Should().BeEmpty();
        profile.CacheTtlSeconds.Should().Be(300);
        profile.CaCertificatePath.Should().BeEmpty();
        profile.SkipTlsVerification.Should().BeFalse();
        profile.KvVersion.Should().Be(VaultKvVersion.AutoDetect);
    }

    [Fact]
    public void VaultSettings_JsonRoundTrip_PreservesAllValues()
    {
        var original = new VaultSettings
        {
            Enabled = true,
            DefaultProfileName = "prod",
            Profiles =
            [
                new VaultProfileConfig
                {
                    Name = "prod",
                    Address = "https://vault.example.com:8200",
                    Namespace = "admin",
                    MountPath = "kv",
                    AuthMethod = VaultAuthMethod.AppRole,
                    AppRoleRoleId = "my-role-id",
                    LdapUsername = "svc-account",
                    CacheTtlSeconds = 600,
                    CaCertificatePath = "/etc/ssl/vault-ca.pem",
                    SkipTlsVerification = true,
                    KvVersion = VaultKvVersion.V2
                }
            ]
        };

        var json = JsonConvert.SerializeObject(original);
        var restored = JsonConvert.DeserializeObject<VaultSettings>(json)!;

        restored.Enabled.Should().BeTrue();
        restored.DefaultProfileName.Should().Be("prod");
        restored.Profiles.Should().HaveCount(1);

        var profile = restored.Profiles[0];
        profile.Name.Should().Be("prod");
        profile.Address.Should().Be("https://vault.example.com:8200");
        profile.Namespace.Should().Be("admin");
        profile.MountPath.Should().Be("kv");
        profile.AuthMethod.Should().Be(VaultAuthMethod.AppRole);
        profile.AppRoleRoleId.Should().Be("my-role-id");
        profile.LdapUsername.Should().Be("svc-account");
        profile.CacheTtlSeconds.Should().Be(600);
        profile.CaCertificatePath.Should().Be("/etc/ssl/vault-ca.pem");
        profile.SkipTlsVerification.Should().BeTrue();
        profile.KvVersion.Should().Be(VaultKvVersion.V2);
    }

    [Fact]
    public void AppConfiguration_Vault_DefaultsToNewVaultSettings()
    {
        var config = new AppConfiguration();

        config.Vault.Should().NotBeNull();
        config.Vault.Enabled.Should().BeFalse();
        config.Vault.Profiles.Should().BeEmpty();
    }

    [Fact]
    public void CredentialMode_Vault_HasValueThree()
    {
        ((int)CredentialMode.Vault).Should().Be(3);
    }

    [Fact]
    public void JobDefinition_VaultCredentialPath_DefaultsToEmpty()
    {
        var job = new JobDefinition();

        job.VaultCredentialPath.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentConfig_VaultProfileName_DefaultsToNull()
    {
        var env = new EnvironmentConfig();

        env.VaultProfileName.Should().BeNull();
    }

    [Fact]
    public void EnvironmentConfig_Clone_PreservesVaultProfileName()
    {
        var original = new EnvironmentConfig
        {
            Name = "prod",
            VaultProfileName = "vault-prod"
        };

        var cloned = original.Clone();

        cloned.VaultProfileName.Should().Be("vault-prod");
    }

    [Fact]
    public void EnvironmentConfig_Clone_WithNullVaultProfileName_PreservesNull()
    {
        var original = new EnvironmentConfig
        {
            Name = "dev",
            VaultProfileName = null
        };

        var cloned = original.Clone();

        cloned.VaultProfileName.Should().BeNull();
    }

    [Fact]
    public void VaultAuthTarget_ReturnsExpectedFormat()
    {
        var target = CredentialTargets.BuildVaultAuthTarget(portableBuild: false, "my-profile", "token");

        target.Should().Be("SSH_Helper:vault:my-profile:token");
    }

    [Fact]
    public void VaultAuthTarget_PortableBuild_UsesPortablePrefix()
    {
        var target = CredentialTargets.BuildVaultAuthTarget(portableBuild: true, "my-profile", "approle");

        target.Should().Be("SSH_Helper_Portable:vault:my-profile:approle");
    }

    [Fact]
    public void VaultAuthMethod_HasExpectedValues()
    {
        ((int)VaultAuthMethod.Token).Should().Be(0);
        ((int)VaultAuthMethod.AppRole).Should().Be(1);
        ((int)VaultAuthMethod.Ldap).Should().Be(2);
    }

    [Fact]
    public void VaultKvVersion_HasExpectedValues()
    {
        ((int)VaultKvVersion.AutoDetect).Should().Be(0);
        ((int)VaultKvVersion.V1).Should().Be(1);
        ((int)VaultKvVersion.V2).Should().Be(2);
    }
}
