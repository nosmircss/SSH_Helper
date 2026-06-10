using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class FlowCanvasFormRunOutputTests
{
    [WinFormsFact]
    public void SendRunOutputAppend_QueuesRunOutputMessageWithChunk()
    {
        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);

        flowCanvas.SendRunOutputAppend("### CONNECTED ###\nhello\n");

        var queue = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        var msg = ReadMessageOfType(queue, "run-output");
        msg.Should().NotBeNull();
        msg!["chunk"]?.ToString().Should().Be("### CONNECTED ###\nhello\n");
    }

    [WinFormsFact]
    public void SendRunOutputClear_QueuesRunOutputClearMessage()
    {
        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);

        flowCanvas.SendRunOutputClear();

        var queue = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        ReadMessageOfType(queue, "run-output-clear").Should().NotBeNull();
    }

    private static JObject? ReadMessageOfType(ConcurrentQueue<string> queue, string expectedType)
    {
        foreach (var json in queue.ToArray())
        {
            var parsed = JObject.Parse(json);
            if (string.Equals(parsed["type"]?.ToString(), expectedType, StringComparison.Ordinal))
                return parsed;
        }
        return null;
    }

    private static T GetField<T>(object instance, string fieldName) where T : class
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on {instance.GetType().Name}");
        var value = field!.GetValue(instance) as T;
        value.Should().NotBeNull($"{fieldName} should be initialized");
        return value!;
    }
}
