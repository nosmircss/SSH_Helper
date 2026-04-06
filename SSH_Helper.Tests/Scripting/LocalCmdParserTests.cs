using System;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class LocalCmdParserTests
{
    private readonly ScriptParser _parser = new();

    [Fact]
    public void Parse_Shorthand_SetsCommand()
    {
        var yaml = """
            ---
            steps:
              - localcmd: Get-Process
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.LocalCmd);
        script.Steps[0].LocalCmd.Should().NotBeNull();
        script.Steps[0].LocalCmd!.Command.Should().Be("Get-Process");
        script.Steps[0].LocalCmd!.Shell.Should().Be("powershell");
    }

    [Fact]
    public void Parse_FullForm_ParsesAllOptions()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "dotnet build"
                  shell: powershell
                  working_dir: "C:\\Projects"
                  interactive: true
                  keep_open: true
                  quiet: true
                  suppress: true
                  into: build
                  timeout: 60
                  on_error: continue
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        var step = script.Steps[0];
        step.GetStepType().Should().Be(StepType.LocalCmd);
        var opts = step.LocalCmd!;
        opts.Command.Should().Be("dotnet build");
        opts.Shell.Should().Be("powershell");
        opts.WorkingDir.Should().Be("C:\\Projects");
        opts.Interactive.Should().BeTrue();
        opts.KeepOpen.Should().BeTrue();
        opts.Quiet.Should().BeTrue();
        opts.Suppress.Should().BeTrue();
        opts.Into.Should().Be("build");
        step.Timeout.Should().Be(60);
        step.OnError.Should().Be("continue");
    }

    [Fact]
    public void Parse_ArgsAsList()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "test.ps1"
                  args:
                    - "-NoProfile"
                    - "-ExecutionPolicy"
                    - "Bypass"
            """;

        var script = _parser.Parse(yaml);
        var opts = script.Steps[0].LocalCmd!;
        opts.Args.Should().HaveCount(3);
        opts.Args[0].Should().Be("-NoProfile");
    }

    [Fact]
    public void Parse_ArgsAsScalar()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "test.ps1"
                  args: "-NoProfile"
            """;

        var script = _parser.Parse(yaml);
        var opts = script.Steps[0].LocalCmd!;
        opts.Args.Should().HaveCount(1);
        opts.Args[0].Should().Be("-NoProfile");
    }

    [Fact]
    public void Parse_EnvDictionary()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "dotnet build"
                  env:
                    CONFIGURATION: Release
                    DOTNET_CLI_TELEMETRY_OPTOUT: "1"
            """;

        var script = _parser.Parse(yaml);
        var opts = script.Steps[0].LocalCmd!;
        opts.Env.Should().NotBeNull();
        opts.Env.Should().ContainKey("CONFIGURATION");
        opts.Env!["CONFIGURATION"].Should().Be("Release");
    }

    [Fact]
    public void Parse_BackgroundMode()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "server.exe"
                  run_mode: background
                  lifetime: script
                  kill_on_cancel: true
                  into: bg
            """;

        var script = _parser.Parse(yaml);
        var opts = script.Steps[0].LocalCmd!;
        opts.RunMode.Should().Be("background");
        opts.Lifetime.Should().Be("script");
        opts.KillOnCancel.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExitCodePolicy()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "installer.exe"
                  fail_on_nonzero: true
                  success_codes: [0, 3010]
            """;

        var script = _parser.Parse(yaml);
        var opts = script.Steps[0].LocalCmd!;
        opts.FailOnNonZero.Should().BeTrue();
        opts.SuccessCodes.Should().BeEquivalentTo(new[] { 0, 3010 });
    }

    [Fact]
    public void Parse_ConfirmPolicy()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "safe-cmd"
                  confirm: never
            """;

        var script = _parser.Parse(yaml);
        script.Steps[0].LocalCmd!.Confirm.Should().Be("never");
    }

    [Fact]
    public void Parse_CustomShell()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "script.py"
                  shell: custom
                  shell_path: "python"
            """;

        var script = _parser.Parse(yaml);
        var opts = script.Steps[0].LocalCmd!;
        opts.Shell.Should().Be("custom");
        opts.ShellPath.Should().Be("python");
    }

    [Fact]
    public void Parse_MaxOutputBytes()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "echo hi"
                  max_output_bytes: 2048
            """;

        var script = _parser.Parse(yaml);
        script.Steps[0].LocalCmd!.MaxOutputBytes.Should().Be(2048);
    }

    [Fact]
    public void Parse_FoldedMultilineCommand_DoesNotConcatenateAdjacentTokens()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: >-
                    "This is the text I want to see in Notepad" | Out-File -FilePath temp.txt -Encoding utf8

                    notepad temp.txt
                  shell: powershell
            """;

        var script = _parser.Parse(yaml);
        var command = script.Steps[0].LocalCmd!.Command;

        command.Should().NotContain("utf8notepad");
        command.Should().MatchRegex(@"utf8(?:\s|;|\r|\n)+notepad");
    }

    [Fact]
    public void Validate_CustomShellWithoutShellPath_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "script.py"
                  shell: custom
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("shell_path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_KeepOpenWithoutInteractive_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "date"
                  keep_open: true
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("keep_open", StringComparison.OrdinalIgnoreCase) &&
                                         error.Contains("interactive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InteractiveAndBackgroundMutuallyExclusive_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "date"
                  interactive: true
                  run_mode: background
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("mutually exclusive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MaxOutputBytesNonPositive_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "date"
                  max_output_bytes: 0
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("max_output_bytes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PowerShellExeShellAlias_DoesNotReturnShellValidationError()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "Get-Date"
                  shell: powershell.exe
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().NotContain(error => error.Contains("shell", StringComparison.OrdinalIgnoreCase) &&
                                             error.Contains("must be one of", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_CmdShell_ReturnsShellValidationError()
    {
        var yaml = """
            ---
            steps:
              - localcmd:
                  command: "dir"
                  shell: cmd
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("localcmd 'shell' must be one of powershell, custom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsYamlScript_LocalCmd_ReturnsTrue()
    {
        ScriptParser.IsYamlScript("- localcmd: echo hello").Should().BeTrue();
    }
}
