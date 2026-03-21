using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1DeleteUndoTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1DeleteUndoTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1DeleteUndoTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void DeletePreset_EnablesUndoMenuAndShowsPendingActionText()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateLoadedForm(CreatePresetConfig());

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var undoMenuItem = GetField<ToolStripMenuItem>(form, "undoDeleteToolStripMenuItem");
        SelectPreset(form, presetsTree, "Alpha");

        undoMenuItem.Enabled.Should().BeFalse();
        undoMenuItem.Text.Should().Be("Undo Delete");

        InvokeMethod(form, "DeletePreset", false);

        undoMenuItem.Enabled.Should().BeTrue("deleting a preset should create a pending session-scoped undo step");
        undoMenuItem.Text.Should().Be("Undo Delete Preset 'Alpha'");
        GetField<PresetManager>(form, "_presetManager").Presets.Should().NotContainKey("Alpha");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void UndoDeleteMenuClick_RestoresDeletedPresetAndSelection()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateLoadedForm(CreatePresetConfig());

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var undoMenuItem = GetField<ToolStripMenuItem>(form, "undoDeleteToolStripMenuItem");
        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");

        SelectPreset(form, presetsTree, "Alpha");
        InvokeMethod(form, "DeletePreset", false);

        undoMenuItem.PerformClick();

        var selectedTag = presetsTree.SelectedNode!.Tag.Should().BeOfType<PresetNodeTag>().Subject;
        selectedTag.Name.Should().Be("Alpha");
        selectedTag.IsFolder.Should().BeFalse();
        editor.Text.Should().Be("echo alpha");
        GetField<PresetManager>(form, "_presetManager").Presets.Should().ContainKey("Alpha");
        undoMenuItem.Enabled.Should().BeFalse();
        undoMenuItem.Text.Should().Be("Undo Delete");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void CtrlZ_WhenPresetTreeFocused_UndoesDelete()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateLoadedForm(CreatePresetConfig());

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        SelectPreset(form, presetsTree, "Alpha");
        InvokeMethod(form, "DeletePreset", false);

        presetsTree.Focus().Should().BeTrue();

        InvokeProcessCmdKey(form, Keys.Control | Keys.Z).Should().BeTrue();

        GetField<PresetManager>(form, "_presetManager").Presets.Should().ContainKey("Alpha");
        ((PresetNodeTag)presetsTree.SelectedNode!.Tag!).Name.Should().Be("Alpha");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void CtrlZ_WhenTextBoxFocused_DoesNotConsumeDeleteUndoShortcut()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateLoadedForm(CreatePresetConfig());

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var presetNameTextBox = GetField<TextBox>(form, "txtPreset");
        var undoMenuItem = GetField<ToolStripMenuItem>(form, "undoDeleteToolStripMenuItem");

        SelectPreset(form, presetsTree, "Alpha");
        InvokeMethod(form, "DeletePreset", false);

        presetNameTextBox.Focus().Should().BeTrue();

        InvokeProcessCmdKey(form, Keys.Control | Keys.Z).Should().BeFalse(
            "Ctrl+Z inside a textbox should stay with normal textbox undo behavior instead of consuming the delete undo stack");

        GetField<PresetManager>(form, "_presetManager").Presets.Should().NotContainKey("Alpha");
        undoMenuItem.Enabled.Should().BeTrue();
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void CtrlZ_WhenScriptEditorFocused_DoesNotConsumeDeleteUndoShortcut()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateLoadedForm(CreatePresetConfig());

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");
        var undoMenuItem = GetField<ToolStripMenuItem>(form, "undoDeleteToolStripMenuItem");

        SelectPreset(form, presetsTree, "Alpha");
        InvokeMethod(form, "DeletePreset", false);

        editor.Focus().Should().BeTrue();
        editor.Focused.Should().BeTrue();

        InvokeProcessCmdKey(form, Keys.Control | Keys.Z).Should().BeFalse(
            "Ctrl+Z inside the script editor should stay with the editor's own undo behavior instead of consuming the delete undo stack");

        GetField<PresetManager>(form, "_presetManager").Presets.Should().NotContainKey("Alpha");
        undoMenuItem.Enabled.Should().BeTrue();
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void NonDeletePresetMutation_ClearsPendingDeleteUndo()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateLoadedForm(CreatePresetConfig());

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var undoMenuItem = GetField<ToolStripMenuItem>(form, "undoDeleteToolStripMenuItem");

        SelectPreset(form, presetsTree, "Alpha");
        InvokeMethod(form, "DeletePreset", false);

        InvokeMethod(form, "TogglePresetFavorite", "Beta");

        undoMenuItem.Enabled.Should().BeFalse("any later non-delete preset-library mutation should invalidate stale delete undo history");
        undoMenuItem.Text.Should().Be("Undo Delete");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
        }
    }

    private SSH_Helper.Form1 CreateLoadedForm(AppConfiguration config)
    {
        var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();
        PointFormAtTemporaryConfig(form, config);
        form.Show();
        Application.DoEvents();
        return form;
    }

    private void PointFormAtTemporaryConfig(SSH_Helper.Form1 form, AppConfiguration config)
    {
        var configService = GetField<ConfigurationService>(form, "_configService");
        var configPathField = typeof(ConfigurationService).GetField("_configFilePath", BindingFlags.Instance | BindingFlags.NonPublic);
        configPathField.Should().NotBeNull("ConfigurationService should keep the config path in a private field for test redirection");

        var configPath = Path.Combine(_testDirectory, "config.json");
        configPathField!.SetValue(configService, configPath);
        configService.Save(config);

        var presetManager = GetField<PresetManager>(form, "_presetManager");
        presetManager.Load(config);

        InvokeMethod(form, "RefreshPresetList", true, null, null, config);
        InvokeMethod(form, "RefreshFavoritesList", new object?[] { null });
    }

    private static AppConfiguration CreatePresetConfig()
    {
        return new AppConfiguration
        {
            Presets = new Dictionary<string, PresetInfo>
            {
                ["Alpha"] = new() { Commands = "echo alpha" },
                ["Beta"] = new() { Commands = "echo beta" }
            },
            ManualPresetOrder = new List<string> { "Alpha", "Beta" }
        };
    }

    private static void SelectPreset(SSH_Helper.Form1 form, TreeView treeView, string presetName)
    {
        var node = FindNodeByTag(treeView.Nodes, presetName, isFolder: false);
        node.Should().NotBeNull($"the Presets tree should contain preset '{presetName}'");
        treeView.SelectedNode = node;
        InvokeMethod(form, "trvPresets_AfterSelect", treeView, new TreeViewEventArgs(node!));
    }

    private static bool InvokeProcessCmdKey(SSH_Helper.Form1 form, Keys keyData)
    {
        var method = typeof(SSH_Helper.Form1).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("Form1 should override ProcessCmdKey for top-level preset shortcut handling");

        var args = new object[] { Message.Create(form.Handle, 0, IntPtr.Zero, IntPtr.Zero), keyData };
        return (bool)method!.Invoke(form, args)!;
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
        return method!.Invoke(instance, args);
    }

    private static TreeNode? FindNodeByTag(TreeNodeCollection nodes, string name, bool isFolder)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is PresetNodeTag tag && tag.IsFolder == isFolder && tag.Name == name)
            {
                return node;
            }

            var childMatch = FindNodeByTag(node.Nodes, name, isFolder);
            if (childMatch != null)
            {
                return childMatch;
            }
        }

        return null;
    }

    private static Form[] SnapshotVisibleOpenForms()
    {
        return Application.OpenForms.Cast<Form>().Where(form => form.Visible).ToArray();
    }

    private static void AssertNoNewVisibleOpenForms(IEnumerable<Form> openFormsBefore)
    {
        Application.OpenForms.Cast<Form>()
            .Where(form => form.Visible && form is not SSH_Helper.Form1)
            .Except(openFormsBefore.Where(form => form is not SSH_Helper.Form1))
            .Should()
            .BeEmpty();
    }
}
