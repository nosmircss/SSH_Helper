using System.Net;
using System.Net.Http;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Notifications;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class SshExecutionServiceOutputWindowTests
{
    [Fact]
    public async Task ExecuteScriptAsync_LocalScript_OutputWindowBuiltIn_IncludesHeaderAndPriorHostOutput()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 }
        };

        var script = """
            ---
            steps:
              - print: "before {{Host_IP}}"
              - print: "__CAPTURE_START__"
              - print: "${_outputwindow}"
              - print: "__CAPTURE_END__"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "",
            defaultPassword: "",
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();

        var captured = ExtractCapturedBlock(results[0].Output, "__CAPTURE_START__", "__CAPTURE_END__");
        captured.Should().Contain("LOCAL SCRIPT: batch-001");
        captured.Should().Contain("before batch-001");
        captured.Should().Contain("__CAPTURE_START__");
    }

    [Fact]
    public async Task ExecuteScriptAsync_LocalScript_OutputWindowBuiltIn_RemainsHostScopedInMultiHostRuns()
    {
        using var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 },
            new HostConnection { IpAddress = "batch-002", Port = 22 }
        };

        var script = """
            ---
            steps:
              - print: "before {{Host_IP}}"
              - print: "__CAPTURE_START__"
              - print: "${_outputwindow}"
              - print: "__CAPTURE_END__"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "",
            defaultPassword: "",
            timeouts: SshTimeoutOptions.Default);

        results.Should().HaveCount(2);

        var first = results.Single(result => result.Host.IpAddress == "batch-001");
        var second = results.Single(result => result.Host.IpAddress == "batch-002");

        var firstCaptured = ExtractCapturedBlock(first.Output, "__CAPTURE_START__", "__CAPTURE_END__");
        var secondCaptured = ExtractCapturedBlock(second.Output, "__CAPTURE_START__", "__CAPTURE_END__");

        firstCaptured.Should().Contain("before batch-001");
        firstCaptured.Should().NotContain("before batch-002");
        secondCaptured.Should().Contain("before batch-002");
        secondCaptured.Should().NotContain("before batch-001");
    }

    [Fact]
    public async Task ExecuteScriptAsync_NotifyMessage_UsesOutputWindowWithoutOwnNotifyDebugLine()
    {
        var settings = new NotificationSettings { Enabled = true };
        var toast = new CapturingToastDispatcher();
        using var notificationService = new NotificationService(
            settings,
            webhookUrlProvider: _ => "https://hooks.example.com/test",
            smtpPasswordProvider: _ => "smtp-pw",
            httpHandler: new StubHandler(),
            toastDispatcher: toast,
            smtpDispatcher: new SmtpDispatcher());
        using var service = new SshExecutionService
        {
            NotificationService = notificationService
        };

        var hosts = new[]
        {
            new HostConnection { IpAddress = "batch-001", Port = 22 }
        };

        var script = """
            ---
            debug: true
            steps:
              - print: "before notify {{Host_IP}}"
              - notify:
                  channel: toast
                  message: "${_outputwindow}"
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "",
            defaultPassword: "",
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        results[0].Success.Should().BeTrue();
        toast.LastMessage.Should().Contain("before notify batch-001");
        toast.LastMessage.Should().Contain("LOCAL SCRIPT: batch-001");
        toast.LastMessage.Should().NotContain("[notify]");
    }

    private static string ExtractCapturedBlock(string output, string startMarker, string endMarker)
    {
        var start = output.IndexOf(startMarker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);

        start += startMarker.Length;
        var end = output.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start);

        return output[start..end];
    }

    private sealed class CapturingToastDispatcher : ToastDispatcher
    {
        public string? LastMessage { get; private set; }

        public override Task<NotificationResult> SendAsync(
            string? title,
            string message,
            NotificationLevel level,
            CancellationToken cancellationToken)
        {
            LastMessage = message;
            return Task.FromResult(NotificationResult.Success("toast"));
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
