using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1FlowCanvasPresetSyncTests : IDisposable
{
    private readonly string _testDirectory;

    public Form1FlowCanvasPresetSyncTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Form1FlowCanvasPresetSyncTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [WinFormsFact]
    public void SelectingDifferentPreset_WithOpenFlowCanvas_QueuesUpdatedGraphForNewPreset()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var config = new AppConfiguration
        {
            Presets = new Dictionary<string, PresetInfo>
            {
                ["Alpha"] = new() { Commands = BuildYamlScript("alpha-sync-token") },
                ["Beta"] = new() { Commands = BuildYamlScript("beta-sync-token") }
            }
        };

        PointFormAtTemporaryConfig(form, config);

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var alphaNode = FindNodeByTag(presetsTree.Nodes, "Alpha", isFolder: false);
        var betaNode = FindNodeByTag(presetsTree.Nodes, "Beta", isFolder: false);
        alphaNode.Should().NotBeNull();
        betaNode.Should().NotBeNull();

        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);
        SetField(form, "_flowCanvasForm", flowCanvas);

        var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");

        presetsTree.SelectedNode = alphaNode;
        InvokePrivateMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(alphaNode!));
        DrainQueue(pendingMessages);

        presetsTree.SelectedNode = betaNode;
        InvokePrivateMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(betaNode!));
        Application.DoEvents();

        pendingMessages
            .ToArray()
            .Should()
            .Contain(message => IsLoadGraphMessageContaining(message, "beta-sync-token"),
                "switching presets while Flow Canvas is open should push the new preset graph into the existing canvas window");
    }

    [WinFormsFact]
    public void ReopeningExistingFlowCanvas_AfterPresetSwitch_QueuesCurrentPresetGraph()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;
        form.PerformLayout();

        var config = new AppConfiguration
        {
            Presets = new Dictionary<string, PresetInfo>
            {
                ["Alpha"] = new() { Commands = BuildYamlScript("alpha-reopen-token") },
                ["Beta"] = new() { Commands = BuildYamlScript("beta-reopen-token") }
            }
        };

        PointFormAtTemporaryConfig(form, config);

        var presetsTree = GetField<TreeView>(form, "trvPresets");
        var alphaNode = FindNodeByTag(presetsTree.Nodes, "Alpha", isFolder: false);
        var betaNode = FindNodeByTag(presetsTree.Nodes, "Beta", isFolder: false);
        alphaNode.Should().NotBeNull();
        betaNode.Should().NotBeNull();

        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);
        SetField(form, "_flowCanvasForm", flowCanvas);

        var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");

        presetsTree.SelectedNode = alphaNode;
        InvokePrivateMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(alphaNode!));
        DrainQueue(pendingMessages);

        presetsTree.SelectedNode = betaNode;
        InvokePrivateMethod(form, "trvPresets_AfterSelect", presetsTree, new TreeViewEventArgs(betaNode!));
        DrainQueue(pendingMessages);

        InvokePrivateMethod(form, "OpenFlowCanvas");
        Application.DoEvents();

        pendingMessages
            .ToArray()
            .Should()
            .Contain(message => IsLoadGraphMessageContaining(message, "beta-reopen-token"),
                "reopening the existing Flow Canvas window should rehydrate the graph for the currently selected preset");
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

    private static string BuildYamlScript(string marker)
    {
        return $"""
                name: sync-test
                steps:
                  - print: {marker}
                """;
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

        InvokePrivateMethod(form, "RefreshPresetList", true, null, null, config);
        InvokePrivateMethod(form, "RefreshFavoritesList", new object?[] { null });
    }

    private static bool IsLoadGraphMessageContaining(string json, string expectedToken)
    {
        var message = JObject.Parse(json);
        if (!string.Equals(message["type"]?.ToString(), "load-graph", StringComparison.Ordinal))
        {
            return false;
        }

        var nodesJson = message["nodes"]?.ToString();
        return !string.IsNullOrEmpty(nodesJson) &&
            nodesJson.Contains(expectedToken, StringComparison.Ordinal);
    }

    private static void DrainQueue(ConcurrentQueue<string> queue)
    {
        while (queue.TryDequeue(out _))
        {
        }
    }

    private static void SetField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        field!.SetValue(instance, value);
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
}
