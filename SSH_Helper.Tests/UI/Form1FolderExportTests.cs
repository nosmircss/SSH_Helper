using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1FolderExportTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1FolderExportTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1FolderExportTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void ContextMenuOpening_FolderSelection_ShowsExportFolderAndHidesExportPreset()
    {
        using var form = CreateLoadedForm(CreatePresetConfig());

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var contextMenu = GetField<ContextMenuStrip>(form, "contextPresetLst");
        var exportFolderMenuItem = GetField<ToolStripMenuItem>(form, "ctxExportFolder");
        var exportPresetMenuItem = GetField<ToolStripMenuItem>(form, "ctxExportPreset");

        var folderNode = FindNodeByTag(presetsTree.Nodes, "Network/Prod", isFolder: true);
        folderNode.Should().NotBeNull();

        presetsTree.Focus().Should().BeTrue();
        presetsTree.SelectedNode = folderNode;
        contextMenu.Show(presetsTree, new Point(1, 1));
        Application.DoEvents();

        exportFolderMenuItem.Visible.Should().BeTrue();
        exportPresetMenuItem.Visible.Should().BeFalse();
        contextMenu.Close();
    }

    [WinFormsFact]
    public void ExportFolder_WithSavePathOverride_WritesSubtreeJsonFile()
    {
        using var form = CreateLoadedForm(CreatePresetConfig());
        var shownPrompts = new List<(string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon)>();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var exportFolderMenuItem = GetField<ToolStripMenuItem>(form, "ctxExportFolder");
        var exportPath = Path.Combine(_testDirectory, "prod-export.json");

        SetField(
            form,
            "_saveFilePathPickerOverrideForTests",
            new Func<IWin32Window?, string, string, string?>((_, _, _) => exportPath));
        SetDialogResponse(form, DialogResult.OK, shownPrompts);

        var folderNode = FindNodeByTag(presetsTree.Nodes, "Network/Prod", isFolder: true);
        folderNode.Should().NotBeNull();

        presetsTree.SelectedNode = folderNode;
        presetsTree.Focus().Should().BeTrue();
        SetField(form, "_contextMenuSourceTreeView", presetsTree);

        exportFolderMenuItem.PerformClick();

        File.Exists(exportPath).Should().BeTrue();
        var exportData = JObject.Parse(File.ReadAllText(exportPath));
        var exportedFolders = exportData["folders"]!.ToObject<Dictionary<string, FolderInfo>>();
        var exportedPresets = exportData["presets"]!.ToObject<Dictionary<string, PresetInfo>>();

        exportedFolders.Should().NotBeNull();
        exportedPresets.Should().NotBeNull();
        exportedFolders!.Keys.Should().BeEquivalentTo("Prod", "Prod/Core");
        exportedPresets!.Keys.Should().BeEquivalentTo("Alpha", "Beta");
        shownPrompts.Should().ContainSingle();
        shownPrompts[0].Title.Should().Be("Export Folder");
        shownPrompts[0].Message.Should().Contain("Network/Prod");
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
        configPathField.Should().NotBeNull();

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
                ["Alpha"] = new() { Commands = "echo alpha", Folder = "Network/Prod" },
                ["Beta"] = new() { Commands = "echo beta", Folder = "Network/Prod/Core" },
                ["Gamma"] = new() { Commands = "echo gamma", Folder = "Network/Lab" }
            },
            PresetFolders = new Dictionary<string, FolderInfo>
            {
                ["Network"] = new() { IsExpanded = true },
                ["Network/Prod"] = new() { IsExpanded = true },
                ["Network/Prod/Core"] = new() { IsExpanded = true },
                ["Network/Lab"] = new() { IsExpanded = true }
            }
        };
    }

    private static void SetDialogResponse(
        SSH_Helper.Form1 form,
        DialogResult result,
        List<(string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon)>? shownPrompts = null)
    {
        SetField(
            form,
            "_dialogPromptOverrideForTests",
            new Func<IWin32Window?, string, string, MessageBoxButtons, MessageBoxIcon, DialogResult>(
                (_, message, title, buttons, icon) =>
                {
                    shownPrompts?.Add((message, title, buttons, icon));
                    return result;
                }));
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static void SetField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        field!.SetValue(instance, value);
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
}
