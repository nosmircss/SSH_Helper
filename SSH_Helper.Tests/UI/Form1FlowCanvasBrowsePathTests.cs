using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1FlowCanvasBrowsePathTests
{
    [WinFormsFact]
    public void HandleFlowCanvasBrowsePathRequest_WithSelectedPath_SendsBrowsePathResult()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;

        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);
        SetField(form, "_flowCanvasForm", flowCanvas);
        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(_ => @"C:\picked\audio.wav"));

        var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        InvokePrivateMethod(
            form,
            "HandleFlowCanvasBrowsePathRequest",
            JObject.FromObject(new
            {
                requestId = "req-1",
                title = "Select File Path",
                currentPath = @"C:\old\value.wav"
            }));

        var response = ReadMessageOfType(pendingMessages, "browse-path-result");
        response.Should().NotBeNull();
        response!["requestId"]?.ToString().Should().Be("req-1");
        response["canceled"]?.Value<bool>().Should().BeFalse();
        response["path"]?.ToString().Should().Be(@"C:\picked\audio.wav");
    }

    [WinFormsFact]
    public void HandleFlowCanvasBrowsePathRequest_WhenCanceled_SendsCanceledResult()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;

        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);
        SetField(form, "_flowCanvasForm", flowCanvas);
        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(_ => null));

        var pendingMessages = GetField<ConcurrentQueue<string>>(flowCanvas, "_pendingMessages");
        InvokePrivateMethod(
            form,
            "HandleFlowCanvasBrowsePathRequest",
            JObject.FromObject(new
            {
                requestId = "req-2",
                title = "Select File Path",
                currentPath = @"C:\old\value.wav"
            }));

        var response = ReadMessageOfType(pendingMessages, "browse-path-result");
        response.Should().NotBeNull();
        response!["requestId"]?.ToString().Should().Be("req-2");
        response["canceled"]?.Value<bool>().Should().BeTrue();
        response["path"]?.ToString().Should().BeEmpty();
    }

    [WinFormsFact]
    public void HandleFlowCanvasBrowsePathRequest_UsesFlowCanvasAsDialogOwner()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;

        using var flowCanvas = new FlowCanvasForm(darkMode: false, configService: null);
        SetField(form, "_flowCanvasForm", flowCanvas);

        IWin32Window? capturedOwner = null;
        SetField(
            form,
            "_filePathPickerOverrideForTests",
            new Func<IWin32Window?, string?>(owner =>
            {
                capturedOwner = owner;
                return @"C:\picked\audio.wav";
            }));

        InvokePrivateMethod(
            form,
            "HandleFlowCanvasBrowsePathRequest",
            JObject.FromObject(new
            {
                requestId = "req-owner",
                title = "Select File Path",
                currentPath = @"C:\old\value.wav"
            }));

        capturedOwner.Should().BeSameAs(flowCanvas);
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
}
