using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1FlowCanvasReducedMotionTests
{
    [WinFormsFact]
    public void SaveReducedMotionPref_PersistsValue_ThenSendPersistedLayoutPostsRestore()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"ReducedMotionTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        try
        {
            var configService = new ConfigurationService(Path.Combine(testDir, "config.json"));

            using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: configService);

            InvokePrivateMethod(
                flowCanvas,
                "SaveReducedMotionPref",
                JObject.FromObject(new { reducedMotion = true }));

            configService.GetCurrent().WindowState?.FlowCanvasReducedMotion.Should().BeTrue();

            var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
            InvokePrivateMethod(flowCanvas, "SendPersistedLayout");

            var restore = ReadMessageOfType(pendingMessages, "pref-restore");
            restore.Should().NotBeNull();
            restore!["reducedMotion"]?.Value<bool>().Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(testDir, true); } catch { /* best-effort cleanup */ }
        }
    }

    [WinFormsFact]
    public void SaveReducedMotionPref_WithNullConfigService_IsNoOp()
    {
        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);

        var act = () => InvokePrivateMethod(
            flowCanvas,
            "SaveReducedMotionPref",
            JObject.FromObject(new { reducedMotion = true }));

        act.Should().NotThrow();

        var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        ReadMessageOfType(pendingMessages, "pref-restore").Should().BeNull();
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
