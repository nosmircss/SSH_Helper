using System.Reflection;
using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class PresetDeleteUndoServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ConfigurationService _configService;
    private readonly PresetManager _presetManager;
    private readonly Mock<ICredentialProvider> _credentialProviderMock;
    private readonly JobStorageService _jobStorageService;

    public PresetDeleteUndoServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PresetDeleteUndo_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _configService = new ConfigurationService(Path.Combine(_tempDir, "config.json"));
        _presetManager = new PresetManager(_configService);
        _credentialProviderMock = new Mock<ICredentialProvider>();
        _jobStorageService = new JobStorageService(_credentialProviderMock.Object, Path.Combine(_tempDir, "jobs.json"));
        _jobStorageService.Load();
    }

    [Fact]
    public void UndoLatest_RestoresMostRecentDeleteBeforeEarlierDelete()
    {
        _presetManager.SetJobStorageService(_jobStorageService);

        var config = new AppConfiguration
        {
            Presets = new Dictionary<string, PresetInfo>
            {
                ["Alpha"] = new() { Commands = "echo alpha", IsFavorite = true },
                ["LegacyPreset"] = new() { Commands = "echo legacy", Folder = "Ops/Legacy" }
            },
            PresetFolders = new Dictionary<string, FolderInfo>
            {
                ["Ops"] = new() { IsExpanded = true },
                ["Ops/Legacy"] = new() { IsExpanded = false, IsFavorite = true }
            },
            ManualPresetOrderByFolder = new Dictionary<string, List<string>>
            {
                [""] = new() { "Alpha" },
                ["Ops/Legacy"] = new() { "LegacyPreset" }
            },
            ManualFolderOrder = new List<string> { "Ops", "Ops/Legacy" },
            ManualFavoriteOrder = new List<string> { "preset:Alpha", "folder:Ops/Legacy" }
        };

        _configService.Save(config);
        _presetManager.Load(config);

        var presetJob = new JobDefinition
        {
            Id = "presetjob000000000000000000000001",
            Name = "Preset Alpha Job",
            TargetType = JobTargetType.Preset,
            TargetName = "Alpha",
            IsEnabled = true,
            DisabledReason = null
        };

        var folderPresetJob = new JobDefinition
        {
            Id = "presetjob000000000000000000000002",
            Name = "Legacy Preset Job",
            TargetType = JobTargetType.Preset,
            TargetName = "LegacyPreset",
            IsEnabled = true,
            DisabledReason = null
        };

        _jobStorageService.Save(presetJob);
        _jobStorageService.Save(folderPresetJob);

        var undoService = CreateUndoService();

        RecordDelete(
            undoService,
            "Alpha",
            isFolder: false,
            _configService.GetCurrent(),
            CaptureJobs(presetJob.Id));
        _presetManager.Delete("Alpha").Should().BeTrue();

        RecordDelete(
            undoService,
            "Ops/Legacy",
            isFolder: true,
            _configService.GetCurrent(),
            CaptureJobs(folderPresetJob.Id));
        _presetManager.DeleteFolder("Ops/Legacy", deletePresets: true).Should().BeTrue();

        _presetManager.Presets.Should().NotContainKey("Alpha");
        _presetManager.Presets.Should().NotContainKey("LegacyPreset");

        var firstUndoResult = UndoLatest(undoService);

        GetResultProperty<string>(firstUndoResult, "TargetName").Should().Be("Ops/Legacy");
        GetResultProperty<bool>(firstUndoResult, "IsFolder").Should().BeTrue();
        _presetManager.Get("LegacyPreset")!.Commands.Should().Be("echo legacy");
        _presetManager.Folders.Should().ContainKey("Ops/Legacy");
        _jobStorageService.Get(folderPresetJob.Id)!.IsEnabled.Should().BeTrue();
        _jobStorageService.Get(folderPresetJob.Id)!.DisabledReason.Should().BeNull();

        var secondUndoResult = UndoLatest(undoService);

        GetResultProperty<string>(secondUndoResult, "TargetName").Should().Be("Alpha");
        GetResultProperty<bool>(secondUndoResult, "IsFolder").Should().BeFalse();
        _presetManager.Get("Alpha")!.Commands.Should().Be("echo alpha");
        _presetManager.Get("Alpha")!.IsFavorite.Should().BeTrue();
        _jobStorageService.Get(presetJob.Id)!.IsEnabled.Should().BeTrue();
        _jobStorageService.Get(presetJob.Id)!.DisabledReason.Should().BeNull();
    }

    [Fact]
    public void Clear_RemovesPendingUndoHistory()
    {
        var undoService = CreateUndoService();
        var config = new AppConfiguration
        {
            Presets = new Dictionary<string, PresetInfo>
            {
                ["Alpha"] = new() { Commands = "echo alpha" }
            }
        };

        _configService.Save(config);

        RecordDelete(undoService, "Alpha", isFolder: false, _configService.GetCurrent(), Array.Empty<JobDefinition>());

        GetUndoServiceProperty<bool>(undoService, "CanUndo").Should().BeTrue();
        GetUndoServiceProperty<string>(undoService, "PendingActionText").Should().Contain("Alpha");

        InvokeUndoServiceMethod(undoService, "Clear");

        GetUndoServiceProperty<bool>(undoService, "CanUndo").Should().BeFalse();
        GetUndoServiceProperty<string>(undoService, "PendingActionText").Should().Be("Undo Delete");
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

    private static object CreateUndoService()
    {
        var type = Type.GetType("SSH_Helper.Services.PresetDeleteUndoService, SSH_Helper");
        type.Should().NotBeNull("the delete undo implementation should live in a dedicated service");
        return Activator.CreateInstance(type!)!;
    }

    private static void RecordDelete(object undoService, string targetName, bool isFolder, AppConfiguration configBeforeDelete, IReadOnlyCollection<JobDefinition> affectedJobs)
    {
        var configClone = JsonConvert.DeserializeObject<AppConfiguration>(JsonConvert.SerializeObject(configBeforeDelete))!;
        var jobClones = affectedJobs.Select(CloneJob).ToArray();

        InvokeUndoServiceMethod(undoService, "RecordDelete", targetName, isFolder, configClone, jobClones);
    }

    private object UndoLatest(object undoService)
    {
        var result = InvokeUndoServiceMethod(undoService, "UndoLatest", _presetManager, _jobStorageService);
        result.Should().NotBeNull("undoing a recorded delete should return the restored target metadata");
        return result!;
    }

    private IReadOnlyCollection<JobDefinition> CaptureJobs(params string[] jobIds)
    {
        return jobIds
            .Select(id => _jobStorageService.Get(id))
            .Where(job => job != null)
            .Select(job => CloneJob(job!))
            .ToArray();
    }

    private static JobDefinition CloneJob(JobDefinition job)
    {
        return JsonConvert.DeserializeObject<JobDefinition>(JsonConvert.SerializeObject(job))!;
    }

    private static object? InvokeUndoServiceMethod(object undoService, string methodName, params object?[] args)
    {
        var method = undoService.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on the delete undo service");
        return method!.Invoke(undoService, args);
    }

    private static T GetUndoServiceProperty<T>(object undoService, string propertyName)
    {
        var property = undoService.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull($"{propertyName} should exist on the delete undo service");
        return (T)property!.GetValue(undoService)!;
    }

    private static T GetResultProperty<T>(object result, string propertyName)
    {
        var property = result.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull($"{propertyName} should exist on the delete undo result");
        return (T)property!.GetValue(result)!;
    }
}
