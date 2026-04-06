using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class JobEditorDialogVaultCredentialTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;

    public JobEditorDialogVaultCredentialTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobEditorVaultCreds_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
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
    public void SaveVaultCredentialJob_PersistsVaultPathAndProfileOverride()
    {
        var presetManager = CreatePresetManagerWithVaultProfiles();

        using var dialog = new JobEditorDialog(
            existingJob: null,
            presetManager: presetManager,
            schedulingService: new SchedulingService(),
            credentialProvider: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        GetField<TextBox>(dialog, "_txtName").Text = "Vault Credential Job";
        GetField<ComboBox>(dialog, "_cboTarget").SelectedIndex = 0;
        GetField<RadioButton>(dialog, "_rbVault").Checked = true;
        GetField<TextBox>(dialog, "_txtVaultPath").Text = "ssh/hosts/router-a#user,pass";

        var profileCombo = GetField<ComboBox>(dialog, "_cboVaultProfileOverride");
        var profileIndex = profileCombo.Items
            .Cast<object>()
            .Select((item, index) => new { Name = item?.ToString(), Index = index })
            .First(item => string.Equals(item.Name, "vault-job", StringComparison.OrdinalIgnoreCase))
            .Index;
        profileCombo.SelectedIndex = profileIndex;

        var hostsGrid = GetField<DataGridView>(dialog, "_gridHosts");
        var rowIndex = hostsGrid.Rows.Add();
        hostsGrid.Rows[rowIndex].Cells[CsvManager.HostColumnName].Value = "10.0.0.20";

        InvokeMethod(dialog, "ValidateAndSave");

        dialog.Result.Should().NotBeNull();
        dialog.Result!.CredentialMode.Should().Be(CredentialMode.Vault);
        dialog.Result.VaultCredentialPath.Should().Be("ssh/hosts/router-a#user,pass");
        dialog.Result.VaultProfileName.Should().Be("vault-job");
    }

    private PresetManager CreatePresetManagerWithVaultProfiles()
    {
        var configService = new ConfigurationService(_configPath);
        configService.Update(config =>
        {
            config.Vault.Enabled = true;
            config.Vault.DefaultProfileName = "vault-app";
            config.Vault.Profiles = new List<VaultProfileConfig>
            {
                new()
                {
                    Name = "vault-app",
                    Address = "https://vault-app:8200",
                    MountPath = "secret",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                },
                new()
                {
                    Name = "vault-job",
                    Address = "https://vault-job:8200",
                    MountPath = "secret",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                }
            };
        });

        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });
        return presetManager;
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }

    private static void InvokeMethod(object obj, string methodName, params object[]? args)
    {
        var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"method '{methodName}' should exist on {obj.GetType().Name}");
        method!.Invoke(obj, args);
    }
}
