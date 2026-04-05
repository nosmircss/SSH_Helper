using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class SettingsDialogVaultTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configService;

    public SettingsDialogVaultTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SettingsDialogVaultTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _configService = new ConfigurationService(_configPath);
        _configService.Load();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [WinFormsFact]
    public void SwitchingVaultProfiles_PersistsEditsToPreviouslySelectedProfile()
    {
        SeedVaultProfiles(defaultProfileName: "profile-a");

        using var dialog = new SettingsDialog(_configService);
        var list = GetField<ListBox>(dialog, "_lstVaultProfiles");
        var address = GetField<TextBox>(dialog, "_txtVaultAddress");

        list.SelectedIndex = 0;
        address.Text = "https://vault-a-updated:8200";

        list.SelectedIndex = 1;

        InvokeMethod(dialog, "BtnSave_Click", null!, EventArgs.Empty);

        var config = _configService.GetCurrent();
        config.Vault.Profiles.Should().ContainSingle(p =>
            p.Name == "profile-a" &&
            p.Address == "https://vault-a-updated:8200");
        config.Vault.Profiles.Should().ContainSingle(p =>
            p.Name == "profile-b" &&
            p.Address == "https://vault-b:8200");
    }

    [WinFormsFact]
    public void SavingAfterEditingNonDefaultProfile_DoesNotClobberExistingDefaultProfile()
    {
        SeedVaultProfiles(defaultProfileName: "profile-b");

        using var dialog = new SettingsDialog(_configService);
        var list = GetField<ListBox>(dialog, "_lstVaultProfiles");
        var address = GetField<TextBox>(dialog, "_txtVaultAddress");

        list.SelectedIndex = 0;
        address.Text = "https://vault-a-edited:8200";

        InvokeMethod(dialog, "BtnSave_Click", null!, EventArgs.Empty);

        _configService.GetCurrent().Vault.DefaultProfileName.Should().Be("profile-b");
    }

    [WinFormsFact]
    public void RenamingDefaultProfile_UpdatesDefaultProfileName()
    {
        SeedVaultProfiles(defaultProfileName: "profile-a");

        using var dialog = new SettingsDialog(_configService);
        var list = GetField<ListBox>(dialog, "_lstVaultProfiles");
        var profileName = GetField<TextBox>(dialog, "_txtVaultProfileName");

        list.SelectedIndex = 0;
        profileName.Text = "profile-a-renamed";

        InvokeMethod(dialog, "BtnSave_Click", null!, EventArgs.Empty);

        _configService.GetCurrent().Vault.DefaultProfileName.Should().Be("profile-a-renamed");
    }

    [WinFormsFact]
    public void RemovingDefaultProfile_SelectsDeterministicReplacementDefault()
    {
        SeedVaultProfiles(defaultProfileName: "profile-b");

        using var dialog = new SettingsDialog(_configService);
        var list = GetField<ListBox>(dialog, "_lstVaultProfiles");

        list.SelectedIndex = 1;
        InvokeMethod(dialog, "BtnVaultRemove_Click", null!, EventArgs.Empty);
        InvokeMethod(dialog, "BtnSave_Click", null!, EventArgs.Empty);

        var config = _configService.GetCurrent();
        config.Vault.Profiles.Select(p => p.Name).Should().ContainSingle().Which.Should().Be("profile-a");
        config.Vault.DefaultProfileName.Should().Be("profile-a");
    }

    private void SeedVaultProfiles(string defaultProfileName)
    {
        _configService.Update(config =>
        {
            config.Vault.Enabled = true;
            config.Vault.DefaultProfileName = defaultProfileName;
            config.Vault.Profiles = new List<VaultProfileConfig>
            {
                new()
                {
                    Name = "profile-a",
                    Address = "https://vault-a:8200",
                    MountPath = "secret",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                },
                new()
                {
                    Name = "profile-b",
                    Address = "https://vault-b:8200",
                    MountPath = "secret",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                }
            };
        });
    }

    private static T GetField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {instance.GetType().Name}");
        return (T)field!.GetValue(instance)!;
    }

    private static void InvokeMethod(object instance, string methodName, params object[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"method '{methodName}' should exist on {instance.GetType().Name}");
        method!.Invoke(instance, args);
    }
}
