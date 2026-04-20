using System.Reflection;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class SshExecutionServiceBannerFormattingTests
{
    [Fact]
    public async Task ExecuteScriptAsync_LocalScript_HeaderUsesTenHashSidePadding()
    {
        using var service = new SshExecutionService();

        var results = await service.ExecuteScriptAsync(
            new[] { new HostConnection { IpAddress = "batch-001", Port = 22 } },
            """
            ---
            steps:
              - print: "hello"
            """,
            defaultUsername: string.Empty,
            defaultPassword: string.Empty,
            timeouts: SshTimeoutOptions.Default);

        results.Should().ContainSingle();
        var headerLine = results[0].Output
            .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
            .First(line => line.Contains("LOCAL SCRIPT:", StringComparison.Ordinal));

        headerLine.Should().Be("########## LOCAL SCRIPT: batch-001 ##########");
    }

    [Fact]
    public void FormatError_DefaultBanner_UsesTenHashSidePadding()
    {
        using var service = new SshExecutionService();
        var formatError = typeof(SshExecutionService).GetMethod(
            "FormatError",
            BindingFlags.Instance | BindingFlags.NonPublic);

        formatError.Should().NotBeNull();

        var formatted = (string)formatError!.Invoke(
            service,
            new object?[]
            {
                "ERROR",
                new HostConnection { IpAddress = "10.0.0.1", Port = 22 },
                new InvalidOperationException("Boom"),
                false,
                false
            })!;

        var headerLine = formatted
            .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .First();

        headerLine.Should().Be("########## ERROR: 10.0.0.1 ##########");
    }
}
