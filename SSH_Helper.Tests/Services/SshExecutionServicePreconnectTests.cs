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

        var method = typeof(SshExecutionService).GetMethod(
            "BuildEffectiveHostVariables",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        var merged = (Dictionary<string, string>)method!.Invoke(null, new object[] { host, context })!;

        merged.Should().Contain(new KeyValuePair<string, string>("existing", "value"));
        merged.Should().Contain(new KeyValuePair<string, string>("bootstrap", "ready"));
        merged.Should().NotContainKey("_prompt");
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
