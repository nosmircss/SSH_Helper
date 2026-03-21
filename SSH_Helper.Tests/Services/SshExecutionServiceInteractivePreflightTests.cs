using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class SshExecutionServiceInteractivePreflightTests
{
    [Fact]
    public async Task ExecuteScriptAsync_InteractiveWithMultipleHosts_FailsPreflight()
    {
        var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "127.0.0.1", Port = 22 },
            new HostConnection { IpAddress = "127.0.0.2", Port = 22 }
        };

        var script = """
            ---
            steps:
              - interactive:
                  session: separate
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "user",
            defaultPassword: "pass",
            timeouts: SshTimeoutOptions.Default);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => !r.Success);
        results.Should().OnlyContain(r => r.ErrorMessage != null && r.ErrorMessage.Contains("interactive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteScriptAsync_BrowserCallbackWithMultipleHosts_FailsPreflight()
    {
        var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "127.0.0.1", Port = 22 },
            new HostConnection { IpAddress = "127.0.0.2", Port = 22 }
        };

        var script = """
            ---
            steps:
              - browser_callback_capture:
                  start_url: "https://idp.example.com/start"
                  callback_path: "/oauth_callback"
                  into: callback_data
            """;

        var results = await service.ExecuteScriptAsync(
            hosts,
            script,
            defaultUsername: "user",
            defaultPassword: "pass",
            timeouts: SshTimeoutOptions.Default);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => !r.Success);
        results.Should().OnlyContain(r => r.ErrorMessage != null && r.ErrorMessage.Contains("browser_callback_capture", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteFolderAsync_InteractivePreset_FailsPreflight()
    {
        var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "127.0.0.1", Port = 22 }
        };

        var interactivePreset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - interactive:
                      session: separate
                """
        };

        var presets = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["InteractivePreset"] = interactivePreset
        };

        var options = new FolderExecutionOptions
        {
            SelectedPresets = new List<string> { "InteractivePreset" },
            ParallelHostCount = 1,
            RunPresetsInParallel = false
        };

        var results = await service.ExecuteFolderAsync(
            hosts,
            presets,
            defaultUsername: "user",
            defaultPassword: "pass",
            timeouts: SshTimeoutOptions.Default,
            options: options);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("folder or multi-host runs");
    }

    [Fact]
    public async Task ExecuteFolderAsync_MixedPresetsWithInteractive_BlocksWholeRun()
    {
        var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "127.0.0.1", Port = 22 }
        };

        var interactivePreset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - interactive:
                      session: separate
                """
        };

        var simplePreset = new PresetInfo
        {
            Commands = "show system status"
        };

        var presets = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["SimplePreset"] = simplePreset,
            ["InteractivePreset"] = interactivePreset
        };

        var options = new FolderExecutionOptions
        {
            SelectedPresets = new List<string> { "SimplePreset", "InteractivePreset" },
            ParallelHostCount = 1,
            RunPresetsInParallel = false
        };

        var results = await service.ExecuteFolderAsync(
            hosts,
            presets,
            defaultUsername: "user",
            defaultPassword: "pass",
            timeouts: SshTimeoutOptions.Default,
            options: options);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("folder or multi-host runs");
        results[0].ErrorMessage.Should().Contain("InteractivePreset");
    }

    [Fact]
    public async Task ExecuteFolderAsync_BrowserCallbackPreset_FailsPreflight()
    {
        var service = new SshExecutionService();
        var hosts = new[]
        {
            new HostConnection { IpAddress = "127.0.0.1", Port = 22 }
        };

        var callbackPreset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - browser_callback_capture:
                      start_url: "https://idp.example.com/start"
                      callback_path: "/oauth_callback"
                      into: callback_data
                """
        };

        var presets = new Dictionary<string, PresetInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["CallbackPreset"] = callbackPreset
        };

        var options = new FolderExecutionOptions
        {
            SelectedPresets = new List<string> { "CallbackPreset" },
            ParallelHostCount = 1,
            RunPresetsInParallel = false
        };

        var results = await service.ExecuteFolderAsync(
            hosts,
            presets,
            defaultUsername: "user",
            defaultPassword: "pass",
            timeouts: SshTimeoutOptions.Default,
            options: options);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeFalse();
        results[0].ErrorMessage.Should().Contain("folder or multi-host runs");
        results[0].ErrorMessage.Should().Contain("CallbackPreset");
    }
}
