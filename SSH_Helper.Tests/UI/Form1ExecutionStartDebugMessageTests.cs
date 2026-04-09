using System.Reflection;
using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class Form1ExecutionStartDebugMessageTests
{
    [Fact]
    public void BuildExecutionStartDebugMessage_LocalOnlyScript_UsesLocalExecutionText()
    {
        var method = GetStaticMethod("BuildExecutionStartDebugMessage");
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                - localcmd:
                    command: echo hi
                    shell: cmd
                    interactive: false
                    keep_open: false
                    run_mode: foreground
                    confirm: never
                    quiet: false
                    into: date2
                    suppress: false
                """
        };

        var message = (string)method.Invoke(null, new object?[] { preset })!;

        message.Should().Be("Calling ExecutePresetAsync - Local execution starting");
    }

    [Fact]
    public void BuildExecutionStartDebugMessage_SshScript_UsesSshExecutionText()
    {
        var method = GetStaticMethod("BuildExecutionStartDebugMessage");
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                - send: show version
                """
        };

        var message = (string)method.Invoke(null, new object?[] { preset })!;

        message.Should().Be("Calling ExecutePresetAsync - SSH connection starting");
    }

    private static MethodInfo GetStaticMethod(string name)
    {
        var method = typeof(global::SSH_Helper.Form1).GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull($"Form1 should expose private static helper '{name}' for execution-start debug logging.");
        return method!;
    }
}
