using FluentAssertions;
using Moq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class PresetManagerDeleteBehaviorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigurationService _configService;
    private readonly PresetManager _presetManager;
    private readonly Mock<ICredentialProvider> _credentialProviderMock;
    private readonly JobStorageService _jobStorageService;

    public PresetManagerDeleteBehaviorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PresetMgrDeleteBehavior_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _configService = new ConfigurationService(Path.Combine(_tempDir, "config.json"));
        _presetManager = new PresetManager(_configService);

        _credentialProviderMock = new Mock<ICredentialProvider>();
        _jobStorageService = new JobStorageService(_credentialProviderMock.Object, Path.Combine(_tempDir, "jobs.json"));
        _jobStorageService.Load();
    }

    [Fact]
    public void DeleteFolder_MoveToParent_PreservesDescendantFolderStructure()
    {
        _presetManager.Load();
        _presetManager.CreateFolder("Network/Cisco/Switches");
        _presetManager.Save("CorePreset", new PresetInfo { Commands = "echo core", Folder = "Network/Cisco" });
        _presetManager.Save("LeafPreset", new PresetInfo { Commands = "echo leaf", Folder = "Network/Cisco/Switches" });

        _presetManager.DeleteFolder("Network/Cisco", deletePresets: false);

        _presetManager.Folders.Should().ContainKey("Network");
        _presetManager.Folders.Should().ContainKey("Network/Switches",
            "moving folder children to the parent should preserve the subtree instead of flattening descendant presets into the parent/root");
        _presetManager.Folders.Should().NotContainKey("Network/Cisco");
        _presetManager.Folders.Should().NotContainKey("Network/Cisco/Switches");
        _presetManager.Get("CorePreset")!.Folder.Should().Be("Network");
        _presetManager.Get("LeafPreset")!.Folder.Should().Be("Network/Switches");
    }

    [Fact]
    public void DeleteFolder_RecursiveDelete_DisablesPresetTargetJobsForRemovedSubtreePresets()
    {
        _presetManager.SetJobStorageService(_jobStorageService);
        _presetManager.Load();
        _presetManager.CreateFolder("Network/Legacy");
        _presetManager.Save("LegacyPreset", new PresetInfo { Commands = "echo legacy", Folder = "Network/Legacy" });

        var presetJob = new JobDefinition
        {
            Name = "Legacy Preset Job",
            TargetType = JobTargetType.Preset,
            TargetName = "LegacyPreset",
            IsEnabled = true
        };

        _jobStorageService.Save(presetJob);

        _presetManager.DeleteFolder("Network", deletePresets: true);

        var updatedPresetJob = _jobStorageService.Get(presetJob.Id);
        updatedPresetJob.Should().NotBeNull();
        updatedPresetJob!.IsEnabled.Should().BeFalse();
        updatedPresetJob.DisabledReason.Should().Be("Preset 'LegacyPreset' was deleted");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
        }
    }
}
