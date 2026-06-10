using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class FlowCanvasFormRunOutputPrefsTests
{
    [WinFormsFact]
    public void SavePanelSizes_PersistsRunOutputPrefs_ThenSendPersistedLayoutReplaysThem()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"RunOutputPrefs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        try
        {
            var configService = new ConfigurationService(Path.Combine(testDir, "config.json"));
            using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: configService);

            InvokePrivate(flowCanvas, "SavePanelSizes",
                JObject.FromObject(new { runOutputColor = false, runOutputWrap = true, runOutputFollow = false }));

            var ws = configService.GetCurrent().WindowState!;
            ws.FlowCanvasRunOutputColor.Should().BeFalse();
            ws.FlowCanvasRunOutputWrap.Should().BeTrue();
            ws.FlowCanvasRunOutputFollow.Should().BeFalse();

            var queue = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
            InvokePrivate(flowCanvas, "SendPersistedLayout");
            var restore = ReadMessageOfType(queue, "layout-restore");
            restore.Should().NotBeNull();
            restore!["runOutputColor"]?.Value<bool>().Should().BeFalse();
            restore["runOutputWrap"]?.Value<bool>().Should().BeTrue();
            restore["runOutputFollow"]?.Value<bool>().Should().BeFalse();
        }
        finally { try { Directory.Delete(testDir, true); } catch { } }
    }

    private static JObject? ReadMessageOfType(ConcurrentQueue<string> queue, string type)
    {
        foreach (var json in queue.ToArray())
        {
            var p = JObject.Parse(json);
            if (string.Equals(p["type"]?.ToString(), type, StringComparison.Ordinal)) return p;
        }
        return null;
    }

    private static void InvokePrivate(object instance, string method, params object?[] args)
    {
        var m = instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        m.Should().NotBeNull($"{method} should exist");
        m!.Invoke(instance, args);
    }

    private static T GetField<T>(object instance, string field) where T : class
    {
        var f = instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        f.Should().NotBeNull();
        return (f!.GetValue(instance) as T)!;
    }
}
