using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConfigurationServiceLayoutModeTests
{
    private static string TempConfig() =>
        Path.Combine(Path.GetTempPath(), "sshhelper-layoutmode-" + Path.GetRandomFileName(), "config.json");

    [Fact]
    public void Legacy_autoReflow_true_migrates_to_AutoFlow_default()
    {
        var path = TempConfig();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            WindowState = new { FlowCanvasAutoReflow = true }
        }));

        var loaded = new ConfigurationService(path).Load();

        loaded.WindowState.FlowCanvasDefaultLayoutMode.Should().Be(LayoutMode.AutoFlow);
        loaded.WindowState.FlowCanvasAutoReflow.Should().BeNull("migration consumes and clears the legacy field");
    }

    [Fact]
    public void Legacy_autoReflow_false_migrates_to_Manual_default()
    {
        var path = TempConfig();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            WindowState = new { FlowCanvasAutoReflow = false }
        }));

        var loaded = new ConfigurationService(path).Load();

        loaded.WindowState.FlowCanvasDefaultLayoutMode.Should().Be(LayoutMode.Manual);
        loaded.WindowState.FlowCanvasAutoReflow.Should().BeNull("migration consumes and clears the legacy field");
    }

    [Fact]
    public void Explicit_default_is_not_clobbered_by_legacy_autoReflow()
    {
        var path = TempConfig();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Config carries BOTH the legacy bool (false => would migrate to Manual) AND an explicit
        // new default (AutoFlow). The explicit value must win — migration only fills a null default.
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            WindowState = new { FlowCanvasAutoReflow = false, FlowCanvasDefaultLayoutMode = LayoutMode.AutoFlow }
        }));

        var loaded = new ConfigurationService(path).Load();

        loaded.WindowState.FlowCanvasDefaultLayoutMode.Should().Be(LayoutMode.AutoFlow,
            "an explicitly-set default must never be overwritten by the legacy migration");
    }

    [Fact]
    public void DefaultLayoutMode_roundTrips()
    {
        var path = TempConfig();
        var svc = new ConfigurationService(path);
        svc.Update(c => c.WindowState.FlowCanvasDefaultLayoutMode = LayoutMode.Manual);

        new ConfigurationService(path).Load().WindowState.FlowCanvasDefaultLayoutMode
            .Should().Be(LayoutMode.Manual);
    }
}
