using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class JobEditorDialogCustomPresetTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;

    public JobEditorDialogCustomPresetTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobEditorCustomPreset_{Guid.NewGuid():N}");
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
    public void Save_CustomPresetJob_PersistsSchedulerLocalContent()
    {
        using var dialog = CreateDialog();

        GetField<TextBox>(dialog, "_txtName").Text = "Custom Job";
        GetField<RadioButton>(dialog, "_rbCustomPreset").Checked = true;
        GetField<ScintillaScriptEditorControl>(dialog, "_txtCustomPresetCommands").Text = "---\nsteps:\n  - wait: 1\n";

        var hostsGrid = GetField<DataGridView>(dialog, "_gridHosts");
        var rowIndex = hostsGrid.Rows.Add();
        hostsGrid.Rows[rowIndex].Cells[CsvManager.HostColumnName].Value = "10.0.0.1";

        InvokeMethod(dialog, "ValidateAndSave");

        dialog.Result.Should().NotBeNull();
        dialog.Result!.TargetType.Should().Be(JobTargetType.CustomPreset);
        dialog.Result.TargetName.Should().BeEmpty();
        dialog.Result.CustomPresetCommands.Should().Be("---\r\nsteps:\r\n  - wait: 1\r\n");
    }

    [WinFormsFact]
    public void SelectingCustomPreset_HidesTargetPickerAndEnablesEditor()
    {
        using var dialog = CreateDialog();
        dialog.Show();
        Application.DoEvents();

        var customRadio = GetField<RadioButton>(dialog, "_rbCustomPreset");
        var targetCombo = GetField<ComboBox>(dialog, "_cboTarget");
        var targetInfo = GetField<Label>(dialog, "_lblCustomTargetInfo");
        var editor = GetField<ScintillaScriptEditorControl>(dialog, "_txtCustomPresetCommands");

        customRadio.Checked = true;
        Application.DoEvents();

        targetCombo.Visible.Should().BeFalse();
        targetInfo.Visible.Should().BeTrue();
        editor.ReadOnly.Should().BeFalse();
    }

    [WinFormsFact]
    public void ExistingCustomPresetJob_PreloadsStoredContent()
    {
        var existingJob = new JobDefinition
        {
            Name = "Existing Custom",
            TargetType = JobTargetType.CustomPreset,
            TargetName = string.Empty,
            CustomPresetCommands = "echo existing"
        };
        existingJob.HostColumns.Add(CsvManager.HostColumnName);
        existingJob.Hosts.Add(new Dictionary<string, string>
        {
            [CsvManager.HostColumnName] = "10.0.0.3"
        });

        using var dialog = CreateDialog(existingJob);

        GetField<RadioButton>(dialog, "_rbCustomPreset").Checked.Should().BeTrue();
        GetField<ScintillaScriptEditorControl>(dialog, "_txtCustomPresetCommands").Text.Should().Be("echo existing");
    }

    private JobEditorDialog CreateDialog(JobDefinition? job = null)
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

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
