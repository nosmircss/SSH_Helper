using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class FlowCanvasFormLayoutTests
{
    [WinFormsFact]
    public void SavePanelSizes_WithHeatmapEnabled_PersistsValue()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"FlowCanvasLayoutTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        try
        {
            var configService = new ConfigurationService(Path.Combine(testDir, "config.json"));

            using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: configService);

            InvokePrivateMethod(
                flowCanvas,
                "SavePanelSizes",
                JObject.FromObject(new { type = "layout-save", heatmapEnabled = true }));

            configService.GetCurrent().WindowState?.FlowCanvasHeatmapEnabled.Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { /* best-effort cleanup */ }
        }
    }

    [WinFormsFact]
    public void SendPersistedLayout_WithHeatmapEnabled_PostsLayoutRestore()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"FlowCanvasLayoutTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        try
        {
            var configService = new ConfigurationService(Path.Combine(testDir, "config.json"));
            configService.Update(c =>
            {
                c.WindowState ??= new SSH_Helper.Models.WindowState();
                c.WindowState.FlowCanvasHeatmapEnabled = true;
            });

            using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: configService);

            var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
            InvokePrivateMethod(flowCanvas, "SendPersistedLayout");

            var restore = ReadMessageOfType(pendingMessages, "layout-restore");
            restore.Should().NotBeNull();
            restore!["heatmapEnabled"]?.Value<bool>().Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { /* best-effort cleanup */ }
        }
    }

    [WinFormsFact]
    public void SavePanelSizes_WithNullConfigService_IsNoOp()
    {
        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);

        var act = () => InvokePrivateMethod(
            flowCanvas,
            "SavePanelSizes",
            JObject.FromObject(new { type = "layout-save", heatmapEnabled = true }));

        act.Should().NotThrow();
    }

    [WinFormsFact]
    public void LoadGraph_includes_layoutMode_action_and_newNodeIds()
    {
        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);

        var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        flowCanvas.LoadGraph(new JArray(), new JArray(), LayoutMode.Manual, "keep", new JArray { "node-2" });

        var load = ReadMessageOfType(pendingMessages, "load-graph");
        load.Should().NotBeNull();
        load!["layoutMode"]?.Value<string>().Should().Be("manual");
        load["layoutAction"]?.Value<string>().Should().Be("keep");
        load["newNodeIds"]?.Values<string>().Should().Contain("node-2");
    }

    private static JObject? ReadMessageOfType(ConcurrentQueue<string> queue, string expectedType)
    {
        foreach (var json in queue.ToArray())
        {
            var parsed = JObject.Parse(json);
            if (string.Equals(parsed["type"]?.ToString(), expectedType, StringComparison.Ordinal))
            {
                return parsed;
            }
        }

        return null;
    }

    private static void InvokePrivateMethod(object instance, string methodName, params object?[] args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
        method!.Invoke(instance, args);
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");

        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized on {instance.GetType().Name}");
        return value!;
    }
}
