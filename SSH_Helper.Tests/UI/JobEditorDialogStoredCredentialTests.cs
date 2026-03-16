using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class JobEditorDialogStoredCredentialTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly string _jobsPath;

    public JobEditorDialogStoredCredentialTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobEditorStoredCreds_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _jobsPath = Path.Combine(_testDirectory, "jobs.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [WinFormsFact]
    public void SaveNewStoredCredentialJob_WritesCredentialManagerAndKeepsPlaintextOutOfJobsJson()
    {
        var presetManager = CreatePresetManager();
        var credentialProvider = new FakeCredentialProvider();

        using var dialog = new JobEditorDialog(
            null,
            presetManager,
            new SchedulingService(),
            credentialProvider,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        GetField<TextBox>(dialog, "_txtName").Text = "Stored Credential Job";
        GetField<ComboBox>(dialog, "_cboTarget").SelectedIndex = 0;
        GetField<RadioButton>(dialog, "_rbStored").Checked = true;
        GetField<TextBox>(dialog, "_txtUsername").Text = "stored-user-99";
        GetField<TextBox>(dialog, "_txtPassword").Text = "stored-secret-99";

        var hostsGrid = GetField<DataGridView>(dialog, "_gridHosts");
        var rowIndex = hostsGrid.Rows.Add();
        hostsGrid.Rows[rowIndex].Cells[CsvManager.HostColumnName].Value = "10.0.0.1";

        InvokeMethod(dialog, "ValidateAndSave");

        dialog.Result.Should().NotBeNull();
        dialog.Result!.CredentialMode.Should().Be(CredentialMode.Stored);

        credentialProvider.TryGetPassword(
            CredentialTargets.JobPasswordTarget(dialog.Result.Id),
            out var storedUsername,
            out var storedPassword).Should().BeTrue();
        storedUsername.Should().Be("stored-user-99");
        storedPassword.Should().Be("stored-secret-99");

        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();
        jobStorage.Save(dialog.Result);

        var jobsJson = File.ReadAllText(_jobsPath);
        jobsJson.Should().NotContain("stored-user-99");
        jobsJson.Should().NotContain("stored-secret-99");
    }

    [WinFormsFact]
    public void EditStoredCredentialJob_BlankPasswordSavePreservesExistingSecret()
    {
        var presetManager = CreatePresetManager();
        var credentialProvider = new FakeCredentialProvider();
        var existingJob = new JobDefinition
        {
            Name = "Existing Stored Job",
            TargetType = JobTargetType.Preset,
            TargetName = "Nightly",
            TargetContentHash = ContentHasher.ComputeHash("echo nightly"),
            CredentialMode = CredentialMode.Stored,
            HostColumns = new List<string> { CsvManager.HostColumnName },
            Hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "10.0.0.5"
                }
            }
        };

        credentialProvider.SavePassword(
            CredentialTargets.JobPasswordTarget(existingJob.Id),
            "existing-user",
            "existing-secret");

        using var dialog = new JobEditorDialog(
            existingJob,
            presetManager,
            new SchedulingService(),
            credentialProvider,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        var usernameTextBox = GetField<TextBox>(dialog, "_txtUsername");
        var passwordTextBox = GetField<TextBox>(dialog, "_txtPassword");
        var noteLabel = GetField<Label>(dialog, "_lblStoredCredNote");

        usernameTextBox.Text.Should().Be("existing-user");
        passwordTextBox.Text.Should().BeEmpty();
        noteLabel.Text.Should().Be(SchedulerJobIntegrityUtilities.FormatStoredCredentialNote(true));

        usernameTextBox.Text = "updated-user";
        passwordTextBox.Clear();

        InvokeMethod(dialog, "ValidateAndSave");

        dialog.Result.Should().NotBeNull();
        dialog.Result!.CredentialMode.Should().Be(CredentialMode.Stored);

        credentialProvider.TryGetPassword(
            CredentialTargets.JobPasswordTarget(existingJob.Id),
            out var storedUsername,
            out var storedPassword).Should().BeTrue();
        storedUsername.Should().Be("updated-user");
        storedPassword.Should().Be("existing-secret");
    }

    private PresetManager CreatePresetManager()
    {
        var configService = new ConfigurationService(_configPath);
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

    private sealed class FakeCredentialProvider : ICredentialProvider
    {
        private readonly Dictionary<string, (string Username, string Password)> _credentials =
            new(StringComparer.Ordinal);

        public bool IsAvailable => true;

        public bool TryGetPassword(string target, out string username, out string password)
        {
            if (_credentials.TryGetValue(target, out var credential))
            {
                username = credential.Username;
                password = credential.Password;
                return true;
            }

            username = string.Empty;
            password = string.Empty;
            return false;
        }

        public bool SavePassword(string target, string username, string password, string? comment = null)
        {
            _credentials[target] = (username, password);
            return true;
        }

        public bool DeletePassword(string target)
        {
            return _credentials.Remove(target);
        }
    }
}
