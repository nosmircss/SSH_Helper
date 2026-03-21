using Newtonsoft.Json;
using SSH_Helper.Models;

namespace SSH_Helper.Services;

internal sealed class PresetDeleteUndoService
{
    private const int MaxEntries = 50;
    private readonly LinkedList<PresetDeleteUndoEntry> _entries = new();

    public bool CanUndo => _entries.Count > 0;

    public string PendingActionText => _entries.Last?.Value.ActionText ?? "Undo Delete";

    public void RecordDelete(
        string targetName,
        bool isFolder,
        AppConfiguration configBeforeDelete,
        IReadOnlyCollection<JobDefinition>? affectedJobsBeforeDelete)
    {
        ArgumentNullException.ThrowIfNull(configBeforeDelete);

        var entry = new PresetDeleteUndoEntry(
            targetName,
            isFolder,
            PresetLibrarySnapshot.Capture(configBeforeDelete),
            CloneJobs(affectedJobsBeforeDelete));

        _entries.AddLast(entry);
        while (_entries.Count > MaxEntries)
        {
            _entries.RemoveFirst();
        }
    }

    public void Clear()
    {
        _entries.Clear();
    }

    public PresetDeleteUndoResult? UndoLatest(PresetManager presetManager, JobStorageService? jobStorageService)
    {
        ArgumentNullException.ThrowIfNull(presetManager);

        var entry = _entries.Last?.Value;
        if (entry == null)
        {
            return null;
        }

        presetManager.RestoreLibrarySnapshot(entry.LibrarySnapshot);
        if (jobStorageService != null && entry.AffectedJobs.Count > 0)
        {
            jobStorageService.RestoreSnapshots(entry.AffectedJobs);
        }

        _entries.RemoveLast();
        return new PresetDeleteUndoResult(entry.TargetName, entry.IsFolder);
    }

    private static IReadOnlyList<JobDefinition> CloneJobs(IReadOnlyCollection<JobDefinition>? jobs)
    {
        if (jobs == null || jobs.Count == 0)
        {
            return Array.Empty<JobDefinition>();
        }

        return jobs.Select(CloneJob).ToArray();
    }

    private static JobDefinition CloneJob(JobDefinition job)
    {
        return JsonConvert.DeserializeObject<JobDefinition>(JsonConvert.SerializeObject(job))!;
    }
}

internal sealed class PresetDeleteUndoEntry
{
    public PresetDeleteUndoEntry(
        string targetName,
        bool isFolder,
        PresetLibrarySnapshot librarySnapshot,
        IReadOnlyList<JobDefinition> affectedJobs)
    {
        TargetName = targetName;
        IsFolder = isFolder;
        LibrarySnapshot = librarySnapshot;
        AffectedJobs = affectedJobs;
    }

    public string TargetName { get; }
    public bool IsFolder { get; }
    public PresetLibrarySnapshot LibrarySnapshot { get; }
    public IReadOnlyList<JobDefinition> AffectedJobs { get; }
    public string ActionText => $"Undo Delete {(IsFolder ? "Folder" : "Preset")} '{TargetName}'";
}

internal sealed class PresetDeleteUndoResult
{
    public PresetDeleteUndoResult(string targetName, bool isFolder)
    {
        TargetName = targetName;
        IsFolder = isFolder;
    }

    public string TargetName { get; }
    public bool IsFolder { get; }
}

internal sealed class PresetLibrarySnapshot
{
    public required Dictionary<string, PresetInfo> Presets { get; init; }
    public required Dictionary<string, FolderInfo> PresetFolders { get; init; }
    public required List<string> ManualPresetOrder { get; init; }
    public required Dictionary<string, List<string>> ManualPresetOrderByFolder { get; init; }
    public required List<string> ManualFolderOrder { get; init; }
    public required List<string> ManualFavoriteOrder { get; init; }

    public static PresetLibrarySnapshot Capture(AppConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new PresetLibrarySnapshot
        {
            Presets = config.Presets.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone(), StringComparer.Ordinal),
            PresetFolders = config.PresetFolders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone(), StringComparer.Ordinal),
            ManualPresetOrder = new List<string>(config.ManualPresetOrder),
            ManualPresetOrderByFolder = config.ManualPresetOrderByFolder.ToDictionary(
                kvp => kvp.Key,
                kvp => new List<string>(kvp.Value),
                StringComparer.Ordinal),
            ManualFolderOrder = new List<string>(config.ManualFolderOrder),
            ManualFavoriteOrder = new List<string>(config.ManualFavoriteOrder)
        };
    }
}
