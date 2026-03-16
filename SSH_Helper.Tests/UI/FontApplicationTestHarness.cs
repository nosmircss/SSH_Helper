using System.Drawing;
using System.Windows.Forms;
using SSH_Helper.Models;
using SSH_Helper.UI;

namespace SSH_Helper.Tests.UI;

/// <summary>
/// Lightweight surrogate for Form1.ApplyFontSettings that creates the same set of controls
/// without requiring the full Form1 startup (SSH services, P/Invoke calls, etc.).
/// The font creation/tracking/disposal pattern mirrors Form1.cs lines 987-1134 exactly.
/// </summary>
internal sealed class FontApplicationTestHarness : IDisposable
{
    // Controls matching what ApplyFontSettings targets
    public Label lblHostsTitle { get; } = new();
    public Label lblPresetsTitle { get; } = new();
    public Label lblScriptTitle { get; } = new();
    public Label lblHistoryTitle { get; } = new();
    public Label lblHostsListTitle { get; } = new();
    public TreeView trvPresets { get; } = new();
    public TreeView trvFavorites { get; } = new();
    public Label lblFavoritesEmpty { get; } = new();
    public Button btnExecuteAll { get; } = new();
    public Button btnExecuteSelected { get; } = new();
    public Button btnStopAll { get; } = new();
    public Button btnSavePreset { get; } = new();
    public TextBox txtCommand { get; } = new();
    public TextBox txtOutput { get; } = new();
    public TabControl presetsTabControl { get; } = new();
    public DataGridView dgv_variables { get; } = new();
    public HistoryListBox lstOutput { get; } = new();
    public ListBox lstHosts { get; } = new();
    public MenuStrip menuStrip1 { get; } = new();
    public ToolStrip mainToolStrip { get; } = new();
    public ToolStrip presetsToolStrip { get; } = new();
    public StatusStrip statusStrip { get; } = new();
    public ContextMenuStrip contextMenuStrip1 { get; } = new();

    /// <summary>Fonts created by the most recent ApplyFontSettings call.</summary>
    public List<Font> ManagedFonts { get; private set; } = new();

    /// <summary>Fonts from the previous ApplyFontSettings call, collected for disposal.</summary>
    public List<Font> PreviousFonts { get; private set; } = new();

    /// <summary>The accent color that was last applied (null if no custom accent).</summary>
    public int? LastAppliedAccentColor { get; private set; }

    /// <summary>
    /// Mirrors Form1.ApplyFontSettings logic (Form1.cs lines 987-1134).
    /// Instead of BeginInvoke for deferred disposal, previous fonts are stored
    /// in PreviousFonts for test verification.
    /// </summary>
    public void ApplyFontSettings(FontSettings fontSettings)
    {
        // Collect previous fonts for disposal after all controls are reassigned
        PreviousFonts = ManagedFonts;
        ManagedFonts = new List<Font>();

        var uiFont = fontSettings.UIFontFamily;
        var codeFont = fontSettings.CodeFontFamily;
        var scale = fontSettings.GlobalScaleFactor;
        var semiboldUiFont = ResolveSemiboldFontFamily(uiFont);

        float Scaled(float size) => size * scale;

        // Section titles (Semibold)
        var sectionTitleFont = new Font(semiboldUiFont, Scaled(fontSettings.SectionTitleFontSize), FontStyle.Bold);
        ManagedFonts.Add(sectionTitleFont);
        lblHostsTitle.Font = sectionTitleFont;
        lblPresetsTitle.Font = sectionTitleFont;
        lblScriptTitle.Font = sectionTitleFont;
        lblHistoryTitle.Font = sectionTitleFont;
        lblHostsListTitle.Font = sectionTitleFont;

        // Tree views
        var treeFont = new Font(uiFont, Scaled(fontSettings.TreeViewFontSize));
        ManagedFonts.Add(treeFont);
        trvPresets.Font = treeFont;
        trvFavorites.Font = treeFont;

        // Apply custom row height for tree views if specified (0 = auto based on font)
        if (fontSettings.TreeViewRowHeight > 0)
        {
            trvPresets.ItemHeight = fontSettings.TreeViewRowHeight;
            trvFavorites.ItemHeight = fontSettings.TreeViewRowHeight;
        }
        else
        {
            int fontHeight;
            try
            {
                fontHeight = treeFont.Height;
            }
            catch (ArgumentException)
            {
                fontHeight = (int)Math.Ceiling(Scaled(fontSettings.TreeViewFontSize) * 1.6f);
            }
            var autoHeight = fontHeight + 4;
            trvPresets.ItemHeight = autoHeight;
            trvFavorites.ItemHeight = autoHeight;
        }

        // Empty labels
        var emptyLabelFont = new Font(uiFont, Scaled(fontSettings.EmptyLabelFontSize));
        ManagedFonts.Add(emptyLabelFont);
        lblFavoritesEmpty.Font = emptyLabelFont;

        // Execute buttons (Semibold)
        var execButtonFont = new Font(semiboldUiFont, Scaled(fontSettings.ExecuteButtonFontSize), FontStyle.Bold);
        ManagedFonts.Add(execButtonFont);
        btnExecuteAll.Font = execButtonFont;
        btnExecuteSelected.Font = execButtonFont;
        btnStopAll.Font = execButtonFont;

        // General buttons
        var buttonFont = new Font(uiFont, Scaled(fontSettings.ButtonFontSize));
        ManagedFonts.Add(buttonFont);
        btnSavePreset.Font = buttonFont;

        // Code editor
        var codeEditorFont = new Font(codeFont, Scaled(fontSettings.CodeEditorFontSize));
        ManagedFonts.Add(codeEditorFont);
        txtCommand.Font = codeEditorFont;
        txtCommand.WordWrap = fontSettings.CodeEditorWordWrap;

        // Output area
        var outputFont = new Font(codeFont, Scaled(fontSettings.OutputAreaFontSize));
        ManagedFonts.Add(outputFont);
        txtOutput.Font = outputFont;
        txtOutput.WordWrap = fontSettings.OutputAreaWordWrap;

        // Tab controls
        var tabFont = new Font(uiFont, Scaled(fontSettings.TabFontSize));
        ManagedFonts.Add(tabFont);
        presetsTabControl.Font = tabFont;

        // Host list (DataGridView) - apply row height setting
        var hostRowHeight = fontSettings.HostListRowHeight > 0 ? fontSettings.HostListRowHeight : 28;
        dgv_variables.RowTemplate.Height = hostRowHeight;
        foreach (DataGridViewRow row in dgv_variables.Rows)
        {
            row.Height = hostRowHeight;
        }

        // History list boxes
        var listFont = new Font(uiFont, Scaled(fontSettings.HostListFontSize));
        ManagedFonts.Add(listFont);
        lstOutput.Font = listFont;
        lstHosts.Font = listFont;

        // Menu strip
        var menuFont = new Font(uiFont, Scaled(fontSettings.MenuFontSize));
        ManagedFonts.Add(menuFont);
        menuStrip1.Font = menuFont;

        // Context menus
        var contextFont = new Font(uiFont, Scaled(fontSettings.MenuFontSize));
        ManagedFonts.Add(contextFont);
        contextMenuStrip1.Font = contextFont;

        // Toolstrips
        var toolStripFont = new Font(uiFont, Scaled(fontSettings.ButtonFontSize));
        ManagedFonts.Add(toolStripFont);
        mainToolStrip.Font = toolStripFont;
        presetsToolStrip.Font = toolStripFont;

        // Status bar
        var statusFont = new Font(uiFont, Scaled(fontSettings.StatusBarFontSize));
        ManagedFonts.Add(statusFont);
        statusStrip.Font = statusFont;

        // Accent color
        LastAppliedAccentColor = fontSettings.CustomAccentColor;
    }

    public void ConfigureVariableHeightHistoryList()
    {
        lstOutput.DrawMode = DrawMode.OwnerDrawVariable;
        lstOutput.ItemHeight = HistoryListLayout.GetMinimumItemHeight(lstOutput.Font);
        lstOutput.RefreshVariableItemHeights();
    }

    private static string ResolveSemiboldFontFamily(string? uiFontFamily)
    {
        if (string.IsNullOrWhiteSpace(uiFontFamily))
        {
            return FontSettings.DefaultUIFontFamily;
        }

        return uiFontFamily.EndsWith("Semibold", StringComparison.OrdinalIgnoreCase)
            ? uiFontFamily
            : $"{uiFontFamily} Semibold";
    }

    /// <summary>
    /// Disposes previous fonts (simulating what BeginInvoke does in production).
    /// </summary>
    public void DisposePreviousFonts()
    {
        foreach (var font in PreviousFonts)
        {
            try { font.Dispose(); } catch { }
        }
        PreviousFonts.Clear();
    }

    public void Dispose()
    {
        foreach (var f in ManagedFonts) { try { f.Dispose(); } catch { } }
        foreach (var f in PreviousFonts) { try { f.Dispose(); } catch { } }

        lblHostsTitle.Dispose();
        lblPresetsTitle.Dispose();
        lblScriptTitle.Dispose();
        lblHistoryTitle.Dispose();
        lblHostsListTitle.Dispose();
        trvPresets.Dispose();
        trvFavorites.Dispose();
        lblFavoritesEmpty.Dispose();
        btnExecuteAll.Dispose();
        btnExecuteSelected.Dispose();
        btnStopAll.Dispose();
        btnSavePreset.Dispose();
        txtCommand.Dispose();
        txtOutput.Dispose();
        presetsTabControl.Dispose();
        dgv_variables.Dispose();
        lstOutput.Dispose();
        lstHosts.Dispose();
        menuStrip1.Dispose();
        mainToolStrip.Dispose();
        presetsToolStrip.Dispose();
        statusStrip.Dispose();
        contextMenuStrip1.Dispose();
    }
}
