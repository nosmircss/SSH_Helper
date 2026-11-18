using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SSH_Helper.Tests.Services;

public class SshExecutionServicePreconnectTests
{
    [Fact]
    public async Task ExecuteScriptAsync_PreconnectDiscovery_UsesRuntimeListAndDoesNotAffectNextRun()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        using var service = new SshExecutionService();
        var host = new HostConnection
        {
            IpAddress = "127.0.0.1",
            Port = 22,
            Variables = new Dictionary<string, string> { ["Host_IP"] = "127.0.0.1", ["port"] = "22" }
        };
        var script = $$"""
            vars:
              candidates: [{{port}}, 22]
            preconnect:
              - portcheck:
                  host: "${Host_IP}"
                  ports: "${candidates}"
                  mode: tcp
                  into: found
              - set: _ssh_port = found_port
            steps:
              - print: "selected=${port}"
            """;

        var results = await service.ExecuteScriptAsync(new[] { host }, script, "", "", SshTimeoutOptions.Default);
        results[0].Success.Should().BeTrue();
        results[0].Output.Should().Contain($"selected={port}");
        host.Port.Should().Be(22);
        host.Variables["port"].Should().Be("22");
        var nextRun = await service.ExecuteScriptAsync(new[] { host },
            "steps:\n  - print: 'selected=${port}'", "", "", SshTimeoutOptions.Default);
        nextRun[0].Output.Should().Contain("selected=22");
    }

    [Theory]
    [InlineData("2222", true)]
    [InlineData("0", false)]
    [InlineData("65536", false)]
    [InlineData("\"bad\"", false)]
    [InlineData("22.5", false)]
    [InlineData("\"\"", false)]
    public async Task ExecuteScriptAsync_PreconnectPortOverride_IsValidatedAndRunLocal(string value, bool valid)
    {
        using var service = new SshExecutionService();
        var host = new HostConnection
        {
            IpAddress = "127.0.0.1",
            Port = 22,
            Variables = new Dictionary<string, string> { ["port"] = "22" }
        };
        var script = $$"""
            preconnect:
              - set: _ssh_port = {{value}}
            steps:
              - print: "selected=${port}"
            """;

        var results = await service.ExecuteScriptAsync(new[] { host }, script, "", "", SshTimeoutOptions.Default);

        results[0].Success.Should().Be(valid);
        if (valid)
            results[0].Output.Should().Contain("selected=2222");
        else
            results[0].ErrorMessage.Should().Contain("_ssh_port");
        host.Port.Should().Be(22);
        host.Variables.Should().ContainKey("port").WhoseValue.Should().Be("22");
        host.Variables.Should().NotContainKey("_ssh_port");
    }

    [Fact]
    public void BuildEffectiveHostVariables_PromptBuiltIn_IsNotPropagatedToMergedHostVariables()
    {
        var host = new HostConnection
        {
            IpAddress = "batch-001",
            Port = 22,
            Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["existing"] = "value"
            }
        };
        var context = new SSH_Helper.Services.Scripting.ScriptContext(host.Variables)
        {
            Session = CreateSessionWithPrompt("router#")
        };
        context.SetVariable("bootstrap", "ready");
        context.SetOutputWindowText("header\r\nline");

        var method = typeof(SshExecutionService).GetMethod(
            "BuildEffectiveHostVariables",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        var merged = (Dictionary<string, string>)method!.Invoke(null, new object[] { host, context })!;

        merged.Should().Contain(new KeyValuePair<string, string>("existing", "value"));
        merged.Should().Contain(new KeyValuePair<string, string>("bootstrap", "ready"));
        merged.Should().NotContainKey("_prompt");
        merged.Should().NotContainKey("_outputwindow");
    }

    [Fact]
    public async Task ExecuteScriptAsync_LocalScriptWithPreconnect_RunsSuccessfully()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 }
        };

        var script = """
            ---
            preconnect:
              - set: bootstrap = "ready"
            steps:
              - print: "state={{bootstrap}}"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "",
            defaultPassword: "",
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        results[0].Output.Should().Contain("state=ready");
    }

    [Fact]
    public async Task ExecuteScriptAsync_PreconnectWithSend_FailsValidation()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "127.0.0.1", Port = 22 }
        };

        var script = """
            ---
            preconnect:
              - send: echo should_fail
            steps:
              - print: "won't run"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "user",
            defaultPassword: "pass",
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("not allowed in preconnect");
    }

    [Fact]
    public async Task ExecuteScriptAsync_Preconnect_EmitsProgressMessagesInNonDebugRuns()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 }
        };

        var progressMessages = new List<string>();
        var outputMessages = new List<string>();

        service.ProgressChanged += (_, e) => progressMessages.Add(e.Message);
        service.OutputReceived += (_, e) => outputMessages.Add(e.Output);

        var script = """
            ---
            preconnect:
              - set: bootstrap = "ready"
            steps:
              - print: "state={{bootstrap}}"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: string.Empty,
            defaultPassword: string.Empty,
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        progressMessages.Should().Contain(m => m.Contains("Running preconnect", StringComparison.OrdinalIgnoreCase));
        progressMessages.Should().Contain(m => m.Contains("Preconnect completed", StringComparison.OrdinalIgnoreCase));
        outputMessages.Should().NotContain(m => m.Contains("Preconnect started", StringComparison.OrdinalIgnoreCase));
        outputMessages.Should().NotContain(m => m.Contains("Preconnect completed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteScriptAsync_Preconnect_EmitsStartAndCompletionOutputWhenDebugEnabled()
    {
        using var service = new SshExecutionService
        {
            DebugMode = true
        };
        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 }
        };

        var progressMessages = new List<string>();
        var outputMessages = new List<string>();

        service.ProgressChanged += (_, e) => progressMessages.Add(e.Message);
        service.OutputReceived += (_, e) => outputMessages.Add(e.Output);

        var script = """
            ---
            preconnect:
              - set: bootstrap = "ready"
            steps:
              - print: "state={{bootstrap}}"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: string.Empty,
            defaultPassword: string.Empty,
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        progressMessages.Should().Contain(m => m.Contains("Running preconnect", StringComparison.OrdinalIgnoreCase));
        progressMessages.Should().Contain(m => m.Contains("Preconnect completed", StringComparison.OrdinalIgnoreCase));
        outputMessages.Should().Contain(m => m.Contains("Preconnect started", StringComparison.OrdinalIgnoreCase));
        outputMessages.Should().Contain(m => m.Contains("Preconnect completed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteScriptAsync_Preconnect_PreservesStructuredVariablesIntoMainSteps()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 }
        };

        var script = """
            ---
            preconnect:
              - set: items = push(items, "alpha")
              - set: items = push(items, "beta")
            steps:
              - foreach: item in items
                do:
                  - print: "item={{item}}"
              - print: "count=${items.length}"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: string.Empty,
            defaultPassword: string.Empty,
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        results[0].Output.Should().Contain("item=alpha");
        results[0].Output.Should().Contain("item=beta");
        results[0].Output.Should().Contain("count=2");
        results[0].Output.Should().NotContain("item=alpha, beta");
    }

    [Fact]
    public async Task ExecuteScriptAsync_PreconnectMissingBootstrapOutput_FailsBeforeSsh()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "127.0.0.1", Port = 22 }
        };

        var script = """
            ---
            preconnect:
                            - assert: "1 == 2"
            steps:
              - send: whoami
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "user",
            defaultPassword: "pass",
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("Assertion failed");
    }

    [Fact]
    public async Task ExecuteScriptAsync_CancelDuringPreconnect_MarksResultCancelled()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 }
        };

        var script = """
            ---
            preconnect:
              - wait: 10
            steps:
              - print: "should-not-run"
            """;

        var runTask = service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: string.Empty,
            defaultPassword: string.Empty,
            timeouts: SshTimeoutOptions.Default);

        await Task.Delay(250);
        service.Stop();

        var results = await runTask;

        results.Should().ContainSingle();
        results[0].Success.Should().BeFalse();
        results[0].WasCancelled.Should().BeTrue();
        results[0].ErrorMessage.Should().Be("Operation cancelled");
    }

    private static SshShellSession CreateSessionWithPrompt(string prompt)
    {
        var session = (SshShellSession)RuntimeHelpers.GetUninitializedObject(typeof(SshShellSession));
        var field = typeof(SshShellSession).GetField("_currentPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(session, prompt);
        return session;
    }
}
