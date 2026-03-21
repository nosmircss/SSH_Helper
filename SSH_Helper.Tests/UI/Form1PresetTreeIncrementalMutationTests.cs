using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1PresetTreeIncrementalMutationTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1PresetTreeIncrementalMutationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1PresetTreeIncrementalMutationTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void AddPreset_PreservesViewportAndExistingNodeInstances()
    {
        using var form = CreateLoadedForm(CreatePresetConfig());
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        SetInputBoxResponse(form, "Preset 03.5");

        var anchorNode = FindNodeByTag(presetsTree.Nodes, "Preset 01", isFolder: false);
        var selectedNode = FindNodeByTag(presetsTree.Nodes, "Preset 03", isFolder: false);
        anchorNode.Should().NotBeNull();
        selectedNode.Should().NotBeNull();

        presetsTree.TopNode = anchorNode;
        presetsTree.SelectedNode = selectedNode;
        InvokeMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(selectedNode!));
        var topNodeBefore = presetsTree.TopNode;

        InvokeMethod(form, "AddPreset");

        topNodeBefore.Should().NotBeNull();
        topNodeBefore!.TreeView.Should().BeSameAs(presetsTree);
        presetsTree.SelectedNode.Should().NotBeNull();
        ((PresetNodeTag)presetsTree.SelectedNode!.Tag!).Name.Should().Be("Preset 03.5");
        FindNodeByTag(presetsTree.Nodes, "Preset 03", isFolder: false)
            .Should().BeSameAs(selectedNode,
                "unrelated nodes should survive a local preset insertion instead of being recreated");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void AddPreset_WhenNewNodeLandsBelowFold_MakesItFullyVisible()
    {
        using var form = CreateLoadedForm(CreatePresetConfig());
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();

        form.Height = 420;
        form.PerformLayout();
        Application.DoEvents();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var topNode = FindNodeByTag(presetsTree.Nodes, "Preset 01", isFolder: false);
        topNode.Should().NotBeNull();

        presetsTree.TopNode = topNode;
        Application.DoEvents();

        var lastFullyVisibleNode = FindLastFullyVisiblePresetNode(presetsTree);
        lastFullyVisibleNode.Should().NotBeNull();
        lastFullyVisibleNode!.NextVisibleNode.Should().NotBeNull(
            "the test setup needs an off-screen row to prove add-preset scrolls just enough");
        IsNodeFullyVisible(presetsTree, lastFullyVisibleNode.NextVisibleNode!).Should().BeFalse(
            "the next row should start below the current viewport before the insert");

        var newPresetName = ((PresetNodeTag)lastFullyVisibleNode.Tag!).Name + ".5";
        SetInputBoxResponse(form, newPresetName);

        presetsTree.SelectedNode = lastFullyVisibleNode;
        InvokeMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(lastFullyVisibleNode));

        InvokeMethod(form, "AddPreset");

        var insertedNode = FindNodeByTag(presetsTree.Nodes, newPresetName, isFolder: false);
        insertedNode.Should().NotBeNull();
        IsNodeFullyVisible(presetsTree, insertedNode!).Should().BeTrue(
            "the newly created preset should be fully visible even when inserted just below the old viewport");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void AddPreset_RestoresBaseEnvironmentForNewPresetWithoutExplicitEnvironment()
    {
        using var form = CreateLoadedForm(CreatePresetConfigWithEnvironmentPreset());
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var environmentService = GetField<EnvironmentService>(form, "_environmentService");

        var environmentPresetNode = FindNodeByTag(presetsTree.Nodes, "Preset With Environment", isFolder: false);
        environmentPresetNode.Should().NotBeNull();

        presetsTree.SelectedNode = environmentPresetNode;
        InvokeMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(environmentPresetNode!));
        environmentService.GetActiveEnvironmentName().Should().Be("blah");

        SetInputBoxResponse(form, "Brand New Preset");
        InvokeMethod(form, "AddPreset");

        environmentService.GetActiveEnvironmentName().Should().Be(EnvironmentConfig.DefaultName,
            "a new preset without an explicit environment should restore the base environment instead of keeping the previous preset's environment");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void UndoLatestPresetDelete_PreservesViewportAndExistingNodeInstances()
    {
        using var form = CreateLoadedForm(CreatePresetConfig());
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var editor = GetField<ScintillaScriptEditorControl>(form, "txtCommand");

        var anchorNode = FindNodeByTag(presetsTree.Nodes, "Preset 10", isFolder: false);
        var deletedNode = FindNodeByTag(presetsTree.Nodes, "Preset 12", isFolder: false);
        anchorNode.Should().NotBeNull();
        deletedNode.Should().NotBeNull();

        presetsTree.TopNode = anchorNode;
        presetsTree.SelectedNode = deletedNode;
        InvokeMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(deletedNode!));

        InvokeMethod(form, "DeletePreset", false);
        var topNodeAfterDelete = presetsTree.TopNode;
        var survivorNodeAfterDelete = FindNodeByTag(presetsTree.Nodes, "Preset 15", isFolder: false);
        InvokeMethod(form, "UndoLatestPresetDelete");

        presetsTree.TopNode.Should().BeSameAs(topNodeAfterDelete,
            "undoing a single preset delete should restore the node without rebuilding the full tree");
        topNodeAfterDelete.Should().NotBeNull();
        topNodeAfterDelete!.TreeView.Should().BeSameAs(presetsTree);
        FindNodeByTag(presetsTree.Nodes, "Preset 15", isFolder: false)
            .Should().BeSameAs(survivorNodeAfterDelete,
                "undoing one preset delete should keep unrelated nodes alive");
        presetsTree.SelectedNode.Should().NotBeNull();
        ((PresetNodeTag)presetsTree.SelectedNode!.Tag!).Name.Should().Be("Preset 12");
        editor.Text.Should().Be("echo 12");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void RenamePreset_PreservesViewportAndUpdatesExistingNodeInPlace()
    {
        using var form = CreateLoadedForm(CreatePresetConfig());
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        SetInputBoxResponse(form, "Preset 12 Renamed");

        var anchorNode = FindNodeByTag(presetsTree.Nodes, "Preset 10", isFolder: false);
        var renamedNode = FindNodeByTag(presetsTree.Nodes, "Preset 12", isFolder: false);
        anchorNode.Should().NotBeNull();
        renamedNode.Should().NotBeNull();

        presetsTree.TopNode = anchorNode;
        presetsTree.SelectedNode = renamedNode;
        InvokeMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(renamedNode!));
        var topNodeBefore = presetsTree.TopNode;

        InvokeMethod(form, "RenamePreset", false);

        presetsTree.TopNode.Should().BeSameAs(topNodeBefore,
            "renaming one preset should not recreate the viewport anchor node");
        presetsTree.SelectedNode.Should().BeSameAs(renamedNode,
            "the renamed node should be relabeled in place instead of replaced");
        ((PresetNodeTag)renamedNode!.Tag!).Name.Should().Be("Preset 12 Renamed");
        renamedNode.Text.Should().Be("Preset 12 Renamed");
        FindNodeByTag(presetsTree.Nodes, "Preset 12", isFolder: false).Should().BeNull();
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void RenamePreset_WhenPresetIsFavorite_RefreshesFavoritesTreeLabel()
    {
        var config = CreatePresetConfig();
        config.Presets["Preset 12"].IsFavorite = true;

        using var form = CreateLoadedForm(config);
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var favoritesTree = GetField<TreeView>(form, "trvFavorites");
        SetInputBoxResponse(form, "Preset 12 Renamed");

        var renamedNode = FindNodeByTag(presetsTree.Nodes, "Preset 12", isFolder: false);
        renamedNode.Should().NotBeNull();
        FindNodeByTag(favoritesTree.Nodes, "Preset 12", isFolder: false).Should().NotBeNull();

        presetsTree.SelectedNode = renamedNode;
        InvokeMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(renamedNode!));

        InvokeMethod(form, "RenamePreset", false);

        FindNodeByTag(favoritesTree.Nodes, "Preset 12", isFolder: false).Should().BeNull();
        var renamedFavoriteNode = FindNodeByTag(favoritesTree.Nodes, "Preset 12 Renamed", isFolder: false);
        renamedFavoriteNode.Should().NotBeNull();
        renamedFavoriteNode!.Text.Should().Contain("Preset 12 Renamed");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void AddPreset_WhenFilterIsActive_FallsBackToRebuildButPreservesFilter()
    {
        using var form = CreateLoadedForm(CreatePresetConfig());
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        SetInputBoxResponse(form, "Preset 12.6");

        InvokeMethod(form, "RefreshPresetList", true, null, "Preset 12", null);
        var filteredNode = FindNodeByTag(presetsTree.Nodes, "Preset 12", isFolder: false);
        filteredNode.Should().NotBeNull();

        presetsTree.SelectedNode = filteredNode;
        InvokeMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(filteredNode!));

        InvokeMethod(form, "AddPreset");

        var refreshedNode = FindNodeByTag(presetsTree.Nodes, "Preset 12", isFolder: false);
        refreshedNode.Should().NotBeNull();
        refreshedNode.Should().NotBeSameAs(filteredNode,
            "filtered add should stay on the intentional rebuild fallback path instead of mutating the partial tree");
        FindNodeByTag(presetsTree.Nodes, "Preset 12.6", isFolder: false).Should().NotBeNull();
        FindNodeByTag(presetsTree.Nodes, "Preset 30", isFolder: false).Should().BeNull(
            "the active preset filter should remain applied after the fallback rebuild");
        ((PresetNodeTag)presetsTree.SelectedNode!.Tag!).Name.Should().Be("Preset 12.6");
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
        var presets = new Dictionary<string, PresetInfo>();
        var manualOrder = new List<string>();
        for (int i = 1; i <= 30; i++)
        {
            var name = $"Preset {i:00}";
            presets[name] = new PresetInfo { Commands = $"echo {i:00}" };
            manualOrder.Add(name);
        }

        return new AppConfiguration
        {
            Presets = presets,
            ManualPresetOrder = manualOrder,
            PresetSortMode = PresetSortMode.Ascending
        };
    }

    private static AppConfiguration CreatePresetConfigWithEnvironmentPreset()
    {
        var config = CreatePresetConfig();
        config.Presets["Preset With Environment"] = new PresetInfo
        {
            Commands = """
                       environment: blah
                       steps:
                         - print:
                             message: "hello"
                       """
        };
        config.ManualPresetOrder.Add("Preset With Environment");
        config.Environments = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase)
        {
            [EnvironmentConfig.DefaultName] = new() { Name = EnvironmentConfig.DefaultName },
            ["blah"] = new() { Name = "blah" }
        };
        config.ActiveEnvironment = EnvironmentConfig.DefaultName;
        config.BaseEnvironment = EnvironmentConfig.DefaultName;
        return config;
    }

    private static void SetInputBoxResponse(SSH_Helper.Form1 form, string response)
    {
        var field = typeof(SSH_Helper.Form1).GetField("_inputBoxPromptOverrideForTests", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull("Form1 should expose an input-box override seam for WinForms regression tests");
        field!.SetValue(form, new Func<string, string, string, string>((_, __, ___) => response));
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

    private static TreeNode? FindLastFullyVisiblePresetNode(TreeView treeView)
    {
        return EnumerateNodes(treeView.Nodes)
            .Where(node => node.Tag is PresetNodeTag tag && !tag.IsFolder && IsNodeFullyVisible(treeView, node))
            .LastOrDefault();
    }

    private static IEnumerable<TreeNode> EnumerateNodes(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;

            foreach (var child in EnumerateNodes(node.Nodes))
            {
                yield return child;
            }
        }
    }

    private static bool IsNodeFullyVisible(TreeView treeView, TreeNode node)
    {
        var bounds = node.Bounds;
        return bounds.Height > 0 &&
            bounds.Top >= 0 &&
            bounds.Bottom <= treeView.ClientSize.Height;
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
