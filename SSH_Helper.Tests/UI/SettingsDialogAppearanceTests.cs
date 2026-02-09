using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class SettingsDialogAppearanceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configService;

    public SettingsDialogAppearanceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(),
            $"SettingsDialogTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _configService = new ConfigurationService(_configPath);
        _configService.Load(); // Initialize default config
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch { /* best-effort cleanup */ }
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }

    private static void InvokeMethod(object obj, string methodName, params object[] args)
    {
        var method = obj.GetType().GetMethod(methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"method '{methodName}' should exist on {obj.GetType().Name}");
        method!.Invoke(obj, args);
    }

    #region Constructor Tests

    [WinFormsFact]
    public void Constructor_DefaultConfig_LoadsDefaultFontSettings()
    {
        using var dialog = new SettingsDialog(_configService);

        var numSectionTitleSize = GetField<NumericUpDown>(dialog, "_numSectionTitleSize");
        numSectionTitleSize.Value.Should().Be(9.5m);

        var numTreeViewSize = GetField<NumericUpDown>(dialog, "_numTreeViewSize");
        numTreeViewSize.Value.Should().Be(9.5m);

        var numCodeEditorSize = GetField<NumericUpDown>(dialog, "_numCodeEditorSize");
        numCodeEditorSize.Value.Should().Be(9.75m);

        var trkGlobalScale = GetField<TrackBar>(dialog, "_trkGlobalScale");
        trkGlobalScale.Value.Should().Be(100);
    }

    [WinFormsFact]
    public void Constructor_CustomConfig_LoadsCustomValues()
    {
        _configService.Update(c =>
        {
            c.FontSettings.GlobalScaleFactor = 1.3f;
            c.FontSettings.SectionTitleFontSize = 12f;
            c.FontSettings.CodeEditorWordWrap = true;
            c.FontSettings.TreeViewRowHeight = 25;
        });

        using var dialog = new SettingsDialog(_configService);

        var trkGlobalScale = GetField<TrackBar>(dialog, "_trkGlobalScale");
        trkGlobalScale.Value.Should().Be(130);

        var numSectionTitleSize = GetField<NumericUpDown>(dialog, "_numSectionTitleSize");
        numSectionTitleSize.Value.Should().Be(12m);

        var chkCodeEditorWordWrap = GetField<CheckBox>(dialog, "_chkCodeEditorWordWrap");
        chkCodeEditorWordWrap.Checked.Should().BeTrue();

        var numTreeViewRowHeight = GetField<NumericUpDown>(dialog, "_numTreeViewRowHeight");
        numTreeViewRowHeight.Value.Should().Be(25m);
    }

    [WinFormsFact]
    public void Constructor_DarkMode_DoesNotThrow()
    {
        var action = () =>
        {
            using var dialog = new SettingsDialog(_configService, darkMode: true);
        };
        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void Constructor_LightMode_DoesNotThrow()
    {
        var action = () =>
        {
            using var dialog = new SettingsDialog(_configService, darkMode: false);
        };
        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void GeneralTab_ScrollExtent_ContainsLastCredentialOption_AfterFontChange()
    {
        using var dialog = new SettingsDialog(_configService);
        using var font = new Font("Segoe UI Semibold", 9f);
        DialogTheme.SetDialogFont(dialog, font);

        dialog.Show();
        Application.DoEvents();
        InvokeMethod(dialog, "RefreshScrollableFlowExtents");

        var tabControl = GetField<TabControl>(dialog, "_tabControl");
        FlowLayoutPanel? generalFlow = null;
        foreach (TabPage tabPage in tabControl.TabPages)
        {
            if (!string.Equals(tabPage.Text, "General", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Control control in tabPage.Controls)
            {
                if (control is FlowLayoutPanel flowPanel)
                {
                    generalFlow = flowPanel;
                    break;
                }
            }

            break;
        }

        generalFlow.Should().NotBeNull();

        var preferSshAgent = GetField<CheckBox>(dialog, "_chkPreferSshAgent");
        var expectedBottom = preferSshAgent.Bottom + preferSshAgent.Margin.Bottom;

        generalFlow!.AutoScrollMinSize.Height.Should().BeGreaterOrEqualTo(expectedBottom);
    }

    #endregion

    #region UpdatePreview Tests

    [WinFormsFact]
    public void UpdatePreview_RapidCalls_DoNotThrow()
    {
        using var dialog = new SettingsDialog(_configService);
        var trkGlobalScale = GetField<TrackBar>(dialog, "_trkGlobalScale");

        // Simulate rapid slider changes (each triggers UpdatePreview via ValueChanged)
        var action = () =>
        {
            for (int i = 80; i <= 150; i++)
            {
                trkGlobalScale.Value = i;
            }
        };

        action.Should().NotThrow();
    }

    [WinFormsFact]
    public void UpdatePreview_FontSizeChanges_DoNotThrow()
    {
        using var dialog = new SettingsDialog(_configService);
        var numSectionTitleSize = GetField<NumericUpDown>(dialog, "_numSectionTitleSize");
        var numCodeEditorSize = GetField<NumericUpDown>(dialog, "_numCodeEditorSize");

        var action = () =>
        {
            for (decimal size = 7m; size <= 16m; size += 0.25m)
            {
                numSectionTitleSize.Value = size;
                numCodeEditorSize.Value = size;
            }
        };

        action.Should().NotThrow();
    }

    #endregion

    #region Save Tests

    [WinFormsFact]
    public void SaveButton_CollectsAllAppearanceSettings()
    {
        using var dialog = new SettingsDialog(_configService);

        // Set non-default values on all appearance controls
        var numSectionTitleSize = GetField<NumericUpDown>(dialog, "_numSectionTitleSize");
        numSectionTitleSize.Value = 12m;

        var numTreeViewSize = GetField<NumericUpDown>(dialog, "_numTreeViewSize");
        numTreeViewSize.Value = 11m;

        var numCodeEditorSize = GetField<NumericUpDown>(dialog, "_numCodeEditorSize");
        numCodeEditorSize.Value = 14m;

        var chkCodeEditorWordWrap = GetField<CheckBox>(dialog, "_chkCodeEditorWordWrap");
        chkCodeEditorWordWrap.Checked = true;

        var numHostListRowHeight = GetField<NumericUpDown>(dialog, "_numHostListRowHeight");
        numHostListRowHeight.Value = 35m;

        // Trigger save via reflection
        InvokeMethod(dialog, "BtnSave_Click", null!, EventArgs.Empty);

        // Reload and verify
        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.FontSettings.SectionTitleFontSize.Should().Be(12f);
        reloaded.FontSettings.TreeViewFontSize.Should().Be(11f);
        reloaded.FontSettings.CodeEditorFontSize.Should().Be(14f);
        reloaded.FontSettings.CodeEditorWordWrap.Should().BeTrue();
        reloaded.FontSettings.HostListRowHeight.Should().Be(35);
    }

    [WinFormsFact]
    public void SaveButton_GlobalScaleFactor_ConvertsFromTrackBarCorrectly()
    {
        using var dialog = new SettingsDialog(_configService);

        var trkGlobalScale = GetField<TrackBar>(dialog, "_trkGlobalScale");
        trkGlobalScale.Value = 130;

        InvokeMethod(dialog, "BtnSave_Click", null!, EventArgs.Empty);

        var reloaded = new ConfigurationService(_configPath).Load();
        reloaded.FontSettings.GlobalScaleFactor.Should().Be(1.3f);
    }

    #endregion

    #region Reset Defaults Tests

    [WinFormsFact]
    public void ApplyFontSettingsToControls_WithDefaults_RestoresAllControlsToDefaults()
    {
        using var dialog = new SettingsDialog(_configService);

        // Modify some controls
        var numSectionTitleSize = GetField<NumericUpDown>(dialog, "_numSectionTitleSize");
        numSectionTitleSize.Value = 14m;

        var trkGlobalScale = GetField<TrackBar>(dialog, "_trkGlobalScale");
        trkGlobalScale.Value = 130;

        // Apply defaults via the internal method
        InvokeMethod(dialog, "ApplyFontSettingsToControls", FontSettings.CreateDefault());

        numSectionTitleSize.Value.Should().Be(9.5m);
        trkGlobalScale.Value.Should().Be(100);
    }

    #endregion
}
