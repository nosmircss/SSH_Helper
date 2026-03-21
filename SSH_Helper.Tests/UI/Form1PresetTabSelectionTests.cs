using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1PresetTabSelectionTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1PresetTabSelectionTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1PresetTabSelectionTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void SwitchingPresetTabs_RestoresPerTabSelectionAndEditorContents()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var config = new AppConfiguration
        {
            Presets = new Dictionary<string, PresetInfo>
            {
                ["Alpha"] = new() { Commands = "echo alpha" },
                ["Beta"] = new() { Commands = "echo beta", IsFavorite = true },
                ["Gamma"] = new() { Commands = "echo gamma", IsFavorite = true }
            },
            ManualFavoriteOrder = new List<string> { "preset:Beta", "preset:Gamma" }
        };

        PointFormAtTemporaryConfig(form, config);

        var presetsTabControl = GetField<TabControl>(form, "presetsTabControl");
        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var favoritesTree = GetField<TreeView>(form, "trvFavorites");
        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");

        var alphaNode = FindNodeByTag(presetsTree.Nodes, "Alpha", isFolder: false);
        alphaNode.Should().NotBeNull("the Presets tree should contain Alpha after the test configuration is loaded");
        presetsTree.SelectedNode = alphaNode;
        InvokePrivateMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(alphaNode!));
        editor.Text.Should().Be("echo alpha");

        presetsTabControl.SelectedIndex = 1;
        InvokePrivateMethod(form, "presetsTabControl_SelectedIndexChanged", presetsTabControl, EventArgs.Empty);
        var betaNode = FindNodeByTag(favoritesTree.Nodes, "Beta", isFolder: false);
        betaNode.Should().NotBeNull("the Favorites tree should contain Beta after the test configuration is loaded");
        favoritesTree.SelectedNode = betaNode;
        InvokePrivateMethod(form, "trvFavorites_AfterSelect", favoritesTree, new TreeViewEventArgs(betaNode!));
        editor.Text.Should().Be("echo beta");

        presetsTabControl.SelectedIndex = 0;
        InvokePrivateMethod(form, "presetsTabControl_SelectedIndexChanged", presetsTabControl, EventArgs.Empty);
        editor.Text.Should().Be("echo alpha",
            "switching back to Presets should re-apply the preset already selected in the Presets tree");

        presetsTabControl.SelectedIndex = 1;
        InvokePrivateMethod(form, "presetsTabControl_SelectedIndexChanged", presetsTabControl, EventArgs.Empty);
        favoritesTree.SelectedNode.Should().NotBeNull("the Favorites tree should keep track of the previously selected favorite across refreshes");
        favoritesTree.SelectedNode!.Tag.Should().BeOfType<PresetNodeTag>();
        ((PresetNodeTag)favoritesTree.SelectedNode.Tag).Name.Should().Be("Beta",
            "returning to Favorites should restore the last selected favorite instead of clearing selection");
        editor.Text.Should().Be("echo beta",
            "returning to Favorites should reload the commands for the restored favorite selection");

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

        InvokePrivateMethod(form, "RefreshPresetList", true, null, null, config);
        InvokePrivateMethod(form, "RefreshFavoritesList", new object?[] { null });
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }

    private static object? InvokePrivateMethod(object instance, string methodName, params object?[] args)
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
            .Where(form => form.Visible)
            .Except(openFormsBefore)
            .Should()
            .BeEmpty();
    }
}
