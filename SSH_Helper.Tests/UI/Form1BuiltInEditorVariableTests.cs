using System.Reflection;
using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

public sealed class Form1BuiltInEditorVariableTests
{
    [WinFormsFact]
    public void ResolveEditorVariableValue_PromptBuiltIn_ReturnsEditorPreviewValue()
    {
        using var form = new global::SSH_Helper.Form1();
        _ = form.Handle;

        var method = typeof(global::SSH_Helper.Form1).GetMethod(
            "ResolveEditorVariableValue",
            BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        var value = (string?)method!.Invoke(form, new object?[] { "_prompt" });

        value.Should().NotBeNull();
    }
}
