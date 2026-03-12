using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class JobEditorDialogTimeoutOverrideTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;

    public JobEditorDialogTimeoutOverrideTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobEditorTimeoutOverrides_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
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
    public void CommandTimeoutGuidance_TracksPresetAndCustomTargets()
    {
        using var dialog = CreateDialog();
        dialog.Show();
        Application.DoEvents();

        var targetCombo = GetField<ComboBox>(dialog, "_cboTarget");
        var commandLabel = GetField<Label>(dialog, "_lblCommandTimeoutSource");
        var customRadio = GetField<RadioButton>(dialog, "_rbCustomPreset");

        targetCombo.SelectedIndex = targetCombo.Items.IndexOf("Nightly");
        Application.DoEvents();

        commandLabel.Text.Should().Be("Inherited: preset 'Nightly' timeout (75 sec)");

        customRadio.Checked = true;
        Application.DoEvents();

        commandLabel.Text.Should().Be("Inherited: app default command timeout (22 sec)");
    }

    [WinFormsFact]
    public void EnableTimeoutOverrides_SeedsNumericValuesFromInheritedValues()
    {
        using var dialog = CreateDialog();
        dialog.Show();
        Application.DoEvents();

        var targetCombo = GetField<ComboBox>(dialog, "_cboTarget");
        var commandCheck = GetField<CheckBox>(dialog, "_chkOverrideCommandTimeout");
        var commandNumeric = GetField<NumericUpDown>(dialog, "_numCommandTimeoutOverride");
        var connectionCheck = GetField<CheckBox>(dialog, "_chkOverrideConnectionTimeout");
        var connectionNumeric = GetField<NumericUpDown>(dialog, "_numConnectionTimeoutOverride");

        targetCombo.SelectedIndex = targetCombo.Items.IndexOf("Nightly");
        Application.DoEvents();

        commandCheck.Checked = true;
        connectionCheck.Checked = true;
        Application.DoEvents();

        commandNumeric.Enabled.Should().BeTrue();
        connectionNumeric.Enabled.Should().BeTrue();
        commandNumeric.Value.Should().Be(75);
        connectionNumeric.Value.Should().Be(44);
    }

    [WinFormsFact]
    public void ExistingJob_TimeoutOverridesPrepopulateAndCanBeClearedOnSave()
    {
        var existingJob = new JobDefinition
        {
            Name = "Existing Timeout Job",
            TargetType = JobTargetType.Preset,
            TargetName = "Nightly",
            TargetContentHash = "hash",
            CommandTimeoutOverrideSeconds = 88,
            ConnectionTimeoutOverrideSeconds = 55,
            HostColumns = new List<string> { CsvManager.HostColumnName },
            Hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "10.0.0.7"
                }
            }
        };

        using var dialog = CreateDialog(existingJob);

        var commandCheck = GetField<CheckBox>(dialog, "_chkOverrideCommandTimeout");
        var commandNumeric = GetField<NumericUpDown>(dialog, "_numCommandTimeoutOverride");
        var connectionCheck = GetField<CheckBox>(dialog, "_chkOverrideConnectionTimeout");
        var connectionNumeric = GetField<NumericUpDown>(dialog, "_numConnectionTimeoutOverride");

        commandCheck.Checked.Should().BeTrue();
        connectionCheck.Checked.Should().BeTrue();
        commandNumeric.Value.Should().Be(88);
        connectionNumeric.Value.Should().Be(55);

        commandCheck.Checked = false;
        connectionCheck.Checked = false;

        InvokeMethod(dialog, "ValidateAndSave");

        dialog.Result.Should().NotBeNull();
        dialog.Result!.CommandTimeoutOverrideSeconds.Should().BeNull();
        dialog.Result.ConnectionTimeoutOverrideSeconds.Should().BeNull();
    }

    private JobEditorDialog CreateDialog(JobDefinition? job = null)
    {
        var configService = new ConfigurationService(_configPath);
        configService.Update(config =>
        {
            config.Timeout = 22;
            config.ConnectionTimeout = 44;
        });

        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo
        {
            Commands = "echo nightly",
            Timeout = 75
        });

        return new JobEditorDialog(
            job,
            presetManager,
            new SchedulingService(),
            credentialProvider: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);
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
