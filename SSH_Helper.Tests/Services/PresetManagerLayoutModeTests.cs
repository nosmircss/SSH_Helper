using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class PresetManagerLayoutModeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigurationService _configService;
    private readonly PresetManager _presetManager;

    public PresetManagerLayoutModeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PresetMgrLayoutMode_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _configService = new ConfigurationService(Path.Combine(_tempDir, "config.json"));
        _presetManager = new PresetManager(_configService);
        _presetManager.Load();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void UpdateLayoutMode_sets_mode_on_existing_preset()
    {
        _presetManager.Save("p1", new PresetInfo { Commands = "print: hi" });

        _presetManager.UpdateLayoutMode("p1", LayoutMode.Manual);

        _presetManager.Get("p1")!.LayoutMode.Should().Be(LayoutMode.Manual);
    }

    [Fact]
    public void UpdateLayoutMode_unknown_preset_is_noop()
    {
        var act = () => _presetManager.UpdateLayoutMode("missing", LayoutMode.Manual);
        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateLayoutMode_persists_across_reload()
    {
        _presetManager.Save("p1", new PresetInfo { Commands = "print: hi" });
        _presetManager.UpdateLayoutMode("p1", LayoutMode.Manual);

        var reloaded = new PresetManager(new ConfigurationService(Path.Combine(_tempDir, "config.json")));
        reloaded.Load();

        reloaded.Get("p1")!.LayoutMode.Should().Be(LayoutMode.Manual);
    }

    [Fact]
    public void UpdateLayoutMode_null_clears_the_mode()
    {
        _presetManager.Save("p1", new PresetInfo { Commands = "print: hi" });
        _presetManager.UpdateLayoutMode("p1", LayoutMode.Manual);

        _presetManager.UpdateLayoutMode("p1", null);

        _presetManager.Get("p1")!.LayoutMode.Should().BeNull();
    }
}
