using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class JobEditorDialogLayoutTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;

    public JobEditorDialogLayoutTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobEditorDialogLayout_{Guid.NewGuid():N}");
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
    public void RecurringScheduleSection_UsesCronBuilderMeasuredHeightAtDefaultDialogSize()
    {
        using var dialog = CreateDialog();

        dialog.Show();
        Application.DoEvents();

        var scheduleCombo = GetField<ComboBox>(dialog, "_cboScheduleType");
        scheduleCombo.SelectedIndex = 1;
        Application.DoEvents();

        var panel = GetField<Panel>(dialog, "_panelCron");
        var builder = GetField<CronBuilderControl>(dialog, "_cronBuilder");
        var requiredContentHeight = MeasureVisibleChildBottom(builder);

        panel.Visible.Should().BeTrue();
        panel.Height.Should().BeGreaterOrEqualTo(builder.MinimumSize.Height);
        builder.ClientSize.Height.Should().BeGreaterOrEqualTo(requiredContentHeight);
    }

    private JobEditorDialog CreateDialog()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        return new JobEditorDialog(
            null,
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

    private static int MeasureVisibleChildBottom(Control control)
    {
        var contentBottom = 0;
        foreach (Control child in control.Controls)
        {
            if (!child.Visible)
            {
                continue;
            }

            var candidateBottom = child.Bottom + child.Margin.Bottom;
            if (candidateBottom > contentBottom)
            {
                contentBottom = candidateBottom;
            }
        }

        return contentBottom;
    }
}
