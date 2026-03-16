# Phase 1: Job Definitions & Persistence - Research

**Researched:** 2026-03-07
**Domain:** .NET 8 WinForms data model design, JSON persistence, Windows Credential Manager integration
**Confidence:** HIGH

## Summary

Phase 1 is primarily a data model and service layer task within an existing, well-established .NET 8 WinForms codebase. The project already has mature patterns for JSON persistence (`ConfigurationService`), credential management (`ICredentialProvider` + `CredentialManagerProvider` + `CredentialTargets`), CSV operations (`CsvManager`), and service-to-UI event communication (`PresetManager`). The new job system can be built almost entirely by mirroring these existing patterns with a dedicated `jobs.json` file and `JobStorageService`.

The key technical challenges are: (1) designing a job model that supports three credential modes and preset drift detection via content hashing, (2) extending `CredentialTargets` for per-job credential storage, and (3) ensuring `PresetManager` checks for job references before destructive preset/folder operations.

**Primary recommendation:** Mirror `ConfigurationService` for persistence and `PresetManager` for CRUD patterns. Use SHA256 for content hashing. Store jobs in a separate `jobs.json` to avoid bloating the existing config file.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Store jobs in a dedicated `jobs.json` file in `%LocalAppData%\SSH_Helper\`, separate from `config.json`
- New `JobStorageService` mirroring `ConfigurationService` patterns: cached read, atomic write with `.bak` backup, corrupt file recovery
- Each job has a stable GUID-based ID (like `HistoryEntry.Id`); name is a display field, not the key
- On job deletion, prompt user whether to also delete run history (Phase 4 concern, but deletion flag stored now)
- Jobs reference presets by name + a content hash computed at link time
- Bidirectional drift awareness: editing a preset informs user which jobs reference it; job blocks execution when hash changes
- Folder jobs check all presets in folder for drift; if any changed, block and list which ones
- Deleted preset warns which jobs use it; if user proceeds, those jobs auto-disable with "preset not found" error
- Same behavior for folder jobs if target folder is deleted
- Full column support matching main grid structure (Host_IP, port, username, password, custom variable columns)
- Stored as `List<Dictionary<string, string>>` per job (same format as `ApplicationState.Hosts`)
- Column names list stored alongside host data for schema preservation
- "Copy from main grid" copies only checked (selected) rows, preserving all columns
- CSV import uses existing `CsvManager` patterns
- Inline mini-grid (DataGridView) in job editor for direct add/remove/edit of hosts
- Three credential modes per job: `Stored`, `InheritFromApp`, `PerHostColumn`
- **Stored mode**: Single username/password per job stored via existing `ICredentialProvider` (Windows Credential Manager). Per-job target naming via `CredentialTargets` pattern using job GUID
- **InheritFromApp mode**: Job uses the app's current username at execution time + Credential Manager lookup for that username
- **PerHostColumn mode**: Uses `username` and `password` columns from the job's own host grid
- No DPAPI -- reuse existing Windows Credential Manager infrastructure

### Claude's Discretion
- Job model class design and property naming
- Exact content hash algorithm (SHA256, MD5, etc.)
- JobStorageService internal caching strategy
- Event patterns for job CRUD notifications
- Validation rules for job names (uniqueness, length, characters)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| JMGT-01 | Create a scheduled job with name, target preset/folder, schedule, host list, credential config | Job model design with all required fields; JobStorageService.Save(); schedule field is a placeholder for Phase 2 |
| JMGT-02 | Edit an existing scheduled job's definition | JobStorageService.Update() with GUID-based lookup; dirty-tracking optional |
| JMGT-03 | Delete a scheduled job | JobStorageService.Delete() with deletion flag for future history cleanup |
| JMGT-04 | Enable or disable a job without deleting it | `IsEnabled` boolean on job model; toggle via JobStorageService.Update() |
| HOST-01 | Each job maintains its own dedicated host list | `List<Dictionary<string, string>>` field on job model + `List<string>` column schema |
| HOST-02 | Populate job host list by CSV import | Reuse CsvManager.LoadFromFile(), convert DataTable to list-of-dictionaries |
| HOST-03 | Populate job host list by copying from main grid | Extract checked rows from DataGridView, convert to list-of-dictionaries |
| HOST-04 | Manually enter hosts in job editor | Mini DataGridView in job editor dialog (Phase 5 UI, but model supports it now) |
| CRED-01 | Configure credential mode per job | `CredentialMode` enum on job model (Stored/InheritFromApp/PerHostColumn) |
| CRED-02 | Stored credentials persisted securely for unattended execution | CredentialTargets.JobPasswordTarget(jobId) + CredentialManagerProvider |
| RELY-01 | Job definitions persist across application restarts | JobStorageService with jobs.json persistence, atomic writes, backup recovery |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Newtonsoft.Json | 13.0.3 | JSON serialization for jobs.json | Already used throughout project for all persistence |
| System.Security.Cryptography | .NET 8 built-in | SHA256 content hashing for preset drift detection | No external dependency needed |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| SSH.NET | 2024.0.0 | Already in project | Not directly used in Phase 1 (execution is Phase 3) |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Newtonsoft.Json | System.Text.Json | Project is standardized on Newtonsoft; switching would be inconsistent |
| SHA256 | MD5 | MD5 is faster but SHA256 is more collision-resistant; performance difference negligible for hashing preset text |
| Separate jobs.json | Embedded in config.json | Separate file avoids bloating config, allows independent backup/recovery -- locked decision |

## Architecture Patterns

### Recommended Project Structure
```
SSH_Helper/
  Models/
    JobDefinition.cs          # Job model + CredentialMode enum + JobHostData
  Services/
    JobStorageService.cs      # CRUD + persistence (mirrors ConfigurationService)
    Credentials/
      CredentialTargets.cs    # Extend with JobPasswordTarget method
  Utilities/
    ContentHasher.cs          # SHA256 hashing utility for preset drift detection
```

### Pattern 1: Service with Cached State + Event Notification (from PresetManager)
**What:** Service holds an in-memory dictionary of jobs, persists on mutation, raises events for UI.
**When to use:** For all job CRUD operations.
**Example:**
```csharp
// Mirrors PresetManager pattern exactly
public sealed class JobStorageService
{
    private readonly Dictionary<string, JobDefinition> _jobs = new();
    private readonly string _jobsFilePath;
    private bool _loaded;

    public event EventHandler? JobsChanged;
    public event EventHandler<JobChangedEventArgs>? JobChanged;

    public IReadOnlyDictionary<string, JobDefinition> Jobs => _jobs;

    public void Load() { /* read jobs.json, populate _jobs, raise JobsChanged */ }
    public void Save(JobDefinition job) { /* upsert by job.Id, persist, raise events */ }
    public bool Delete(string jobId) { /* remove, persist, raise events */ }
    public JobDefinition? Get(string jobId) { /* lookup by GUID */ }
}
```

### Pattern 2: Atomic Write with Backup (from ConfigurationService)
**What:** Before writing, copy current file to `.bak`. On corrupt load, rename to `.corrupt` and start fresh.
**When to use:** Every persist operation in JobStorageService.
**Example:**
```csharp
// Direct mirror of ConfigurationService.Save() pattern
private void PersistToDisk()
{
    if (File.Exists(_jobsFilePath))
    {
        try { File.Copy(_jobsFilePath, _jobsFilePath + ".bak", overwrite: true); }
        catch { /* best-effort backup */ }
    }

    var wrapper = new JobsFileWrapper
    {
        Version = 1,
        Jobs = _jobs.Values.ToList()
    };

    string json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
    File.WriteAllText(_jobsFilePath, json);
}
```

### Pattern 3: GUID-Based Identity (from HistoryEntry)
**What:** Each job gets a stable `Guid.NewGuid().ToString("N")` at creation. Name is mutable display field.
**When to use:** All job identification, credential target naming, future history linkage.
**Example:**
```csharp
public class JobDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    // ... other fields
}
```

### Pattern 4: Credential Target Extension (from CredentialTargets)
**What:** Extend `CredentialTargets` with a job-specific target name pattern.
**When to use:** When storing/retrieving credentials for Stored mode jobs.
**Example:**
```csharp
// In CredentialTargets.cs
public static string JobPasswordTarget(string jobId)
{
    var safeId = (jobId ?? string.Empty).Trim();
    return $"{Prefix}:job:{safeId}";
}
```

### Pattern 5: Content Hash for Drift Detection
**What:** Compute SHA256 of preset content at link time, store on job, compare at execution time.
**When to use:** When linking a job to a preset, and before any job execution.
**Example:**
```csharp
public static class ContentHasher
{
    public static string ComputeHash(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
```

### Anti-Patterns to Avoid
- **Storing jobs inside AppConfiguration:** Bloats config.json, risks corruption of unrelated settings during job writes. Keep jobs.json separate (locked decision).
- **Using job name as key:** Names change; use GUID. Name collisions are a display concern only.
- **Storing passwords in jobs.json:** Credentials go to Windows Credential Manager. Never persist secrets in plain JSON.
- **Tight coupling between JobStorageService and PresetManager:** Use events or a lightweight check method. Don't make JobStorageService depend on PresetManager directly; instead, have PresetManager query JobStorageService for references.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Credential storage | Custom encryption/DPAPI | `ICredentialProvider` + `CredentialManagerProvider` | Already built, tested, uses OS-level security |
| CSV import | Custom parser | `CsvManager.LoadFromFile()` | Handles quoting, multiline, BOM, edge cases |
| JSON persistence | Custom file I/O | Mirror `ConfigurationService` patterns | Atomic write, backup, corrupt recovery already proven |
| Content hashing | Custom hash | `SHA256.HashData()` from .NET BCL | One-liner, no NuGet needed |
| Input validation | Per-field checks scattered in UI | `InputValidator` extensions | Centralized, testable, existing pattern |

**Key insight:** This phase is almost entirely about composing existing project patterns into a new domain. The codebase has solved persistence, credentials, CSV, and validation already. The job system layers on top.

## Common Pitfalls

### Pitfall 1: Preset Reference Integrity on Rename
**What goes wrong:** Presets are referenced by name. If a preset is renamed, job references break silently.
**Why it happens:** PresetManager.Rename() doesn't know about jobs.
**How to avoid:** When PresetManager.Rename() is called, update all jobs that reference the old preset name. Add a method like `JobStorageService.GetJobsReferencingPreset(string presetName)` and wire it into the rename flow.
**Warning signs:** Jobs showing "preset not found" after a rename.

### Pitfall 2: Folder Deletion Cascade
**What goes wrong:** Deleting a preset folder should disable all folder-type jobs targeting it, but the folder deletion flow in PresetManager doesn't know about jobs.
**Why it happens:** PresetManager.DeleteFolder() is unaware of job references.
**How to avoid:** Before folder deletion, query JobStorageService for folder-type jobs referencing the folder. Show warning, auto-disable affected jobs if user proceeds.
**Warning signs:** Folder jobs executing against non-existent folders.

### Pitfall 3: Content Hash Mismatch from Line Ending Normalization
**What goes wrong:** PresetInfo normalizes line endings to CRLF via `NormalizeToWindowsLineEndings()`. If hash is computed at different stages (before vs. after normalization), hashes won't match.
**How to avoid:** Always compute hash on the normalized `Commands` property value (post-normalization), never on raw input.
**Warning signs:** Jobs always showing "drift detected" even when preset hasn't changed.

### Pitfall 4: Stale Cache After External File Edit
**What goes wrong:** If user manually edits jobs.json, the in-memory cache in JobStorageService is stale.
**Why it happens:** No file watcher, service only reads on startup.
**How to avoid:** Accept this limitation (same as ConfigurationService). Document that external edits require app restart. Optionally add a Reload() method.
**Warning signs:** Edits to jobs.json not appearing in the UI.

### Pitfall 5: Credential Cleanup on Job Deletion
**What goes wrong:** Deleting a job with Stored credentials leaves orphaned entries in Windows Credential Manager.
**Why it happens:** Delete only removes from jobs.json, forgets to clean up credentials.
**How to avoid:** In JobStorageService.Delete(), if the job's credential mode is Stored, call `ICredentialProvider.DeletePassword(CredentialTargets.JobPasswordTarget(jobId))`.
**Warning signs:** Accumulating stale entries in Windows Credential Manager.

### Pitfall 6: Job Name Uniqueness Edge Cases
**What goes wrong:** Two jobs with the same display name cause user confusion.
**Why it happens:** Name is a display field, GUID is the key, so the system allows duplicate names.
**How to avoid:** Enforce unique names at the service level. Use case-insensitive comparison. The `GetUniqueName()` pattern from PresetManager works well.
**Warning signs:** User sees two identically named jobs.

## Code Examples

### Job Model Design
```csharp
// Source: Derived from existing project patterns (HistoryEntry, PresetInfo, ApplicationState)
public enum CredentialMode
{
    InheritFromApp,
    Stored,
    PerHostColumn
}

public enum JobTargetType
{
    Preset,
    Folder
}

public class JobDefinition
{
    // Identity
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    // Target preset/folder
    public JobTargetType TargetType { get; set; } = JobTargetType.Preset;
    public string TargetName { get; set; } = string.Empty; // preset name or folder path
    public string TargetContentHash { get; set; } = string.Empty; // SHA256 at link time

    // For folder targets: per-preset hashes
    public Dictionary<string, string>? FolderPresetHashes { get; set; }

    // Host list (mirrors ApplicationState.Hosts format)
    public List<Dictionary<string, string>> Hosts { get; set; } = new();
    public List<string> HostColumns { get; set; } = new();

    // Credentials
    public CredentialMode CredentialMode { get; set; } = CredentialMode.InheritFromApp;

    // Schedule placeholder (Phase 2 populates this)
    // Stored as string to avoid Phase 1 depending on cron libraries
    public string? CronExpression { get; set; }
    public DateTime? OneTimeScheduleUtc { get; set; }

    // Drift/error state
    public bool HasDriftWarning { get; set; }
    public string? DisabledReason { get; set; } // e.g., "preset not found"
}
```

### JobStorageService Skeleton
```csharp
// Source: Mirrors ConfigurationService + PresetManager patterns
public sealed class JobStorageService
{
    private readonly string _jobsFilePath;
    private readonly Dictionary<string, JobDefinition> _jobs = new();
    private readonly ICredentialProvider _credentialProvider;

    public event EventHandler? JobsChanged;

    public JobStorageService(ICredentialProvider credentialProvider, string? jobsFilePath = null)
    {
        _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));

        if (string.IsNullOrWhiteSpace(jobsFilePath))
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SSH_Helper");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _jobsFilePath = Path.Combine(folder, "jobs.json");
        }
        else
        {
            _jobsFilePath = jobsFilePath;
        }
    }

    public string JobsFilePath => _jobsFilePath;
    public IReadOnlyDictionary<string, JobDefinition> Jobs => _jobs;

    public void Load() { /* mirror ConfigurationService.Load() */ }
    public void Save(JobDefinition job) { /* upsert, persist, raise event */ }
    public bool Delete(string jobId, bool cleanupCredentials = true) { /* remove + credential cleanup */ }
    public JobDefinition? Get(string jobId) => _jobs.TryGetValue(jobId, out var job) ? job : null;
    public IReadOnlyList<JobDefinition> GetJobsReferencingPreset(string presetName) { /* query */ }
    public IReadOnlyList<JobDefinition> GetJobsReferencingFolder(string folderPath) { /* query */ }
}
```

### Extending CredentialTargets
```csharp
// Source: Existing CredentialTargets pattern
// Add to existing CredentialTargets.cs
public static string JobPasswordTarget(string jobId)
{
    var safeId = (jobId ?? string.Empty).Trim();
    return $"{Prefix}:job:{safeId}";
}
```

### Converting DataGridView Rows to Job Host Format
```csharp
// Source: Mirrors ApplicationState.Hosts format
public static (List<Dictionary<string, string>> hosts, List<string> columns)
    ExtractCheckedRows(DataGridView grid)
{
    var columns = new List<string>();
    for (int i = 1; i < grid.Columns.Count; i++) // skip checkbox column
        columns.Add(grid.Columns[i].Name);

    var hosts = new List<Dictionary<string, string>>();
    foreach (DataGridViewRow row in grid.Rows)
    {
        if (row.IsNewRow) continue;
        var cell = row.Cells[0] as DataGridViewCheckBoxCell;
        if (cell?.Value is not true) continue;

        var dict = new Dictionary<string, string>();
        foreach (var col in columns)
            dict[col] = row.Cells[col].Value?.ToString() ?? string.Empty;
        hosts.Add(dict);
    }

    return (hosts, columns);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| DPAPI for credential encryption | Windows Credential Manager via P/Invoke | Already in codebase | No DPAPI spike needed (STATE.md concern resolved) |
| String-keyed presets | GUID-keyed jobs with display name | This phase | Jobs are rename-safe, linkable by stable ID |

**Deprecated/outdated:**
- DPAPI approach flagged in STATE.md is superseded by the locked decision to reuse `ICredentialProvider` + `CredentialManagerProvider`. No validation spike needed.

## Open Questions

1. **Folder job hash granularity**
   - What we know: Folder jobs should store per-preset hashes to identify which specific preset drifted
   - What's unclear: Should `FolderPresetHashes` be a flat `Dictionary<string, string>` (preset name -> hash) or something more structured?
   - Recommendation: Simple `Dictionary<string, string>` is sufficient. Preset names within a folder are unique.

2. **Job name validation rules**
   - What we know: Names should be unique (case-insensitive), non-empty
   - What's unclear: Max length? Allowed characters?
   - Recommendation: Max 100 characters, same sanitization as column names (no leading/trailing whitespace). No special character restrictions beyond what Newtonsoft.Json handles.

3. **Schedule fields in Phase 1 model**
   - What we know: Phase 2 adds scheduling, but the model is created now
   - What's unclear: How much schedule structure to include in the Phase 1 model
   - Recommendation: Include `CronExpression` (nullable string) and `OneTimeScheduleUtc` (nullable DateTime) as placeholder fields. Phase 2 will populate them. This avoids model migration later.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| Config file | `SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| Quick run command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Job" -x --no-build` |
| Full suite command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| JMGT-01 | Create job with all fields | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Create" --no-build` | Wave 0 |
| JMGT-02 | Edit existing job | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Update" --no-build` | Wave 0 |
| JMGT-03 | Delete job | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Delete" --no-build` | Wave 0 |
| JMGT-04 | Enable/disable job | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.EnableDisable" --no-build` | Wave 0 |
| HOST-01 | Independent host list per job | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobDefinitionTests.HostList" --no-build` | Wave 0 |
| HOST-02 | CSV import to job host list | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.CsvImport" --no-build` | Wave 0 |
| HOST-03 | Copy from main grid | integration | manual-only -- requires DataGridView (WinForms UI component) | Wave 0 |
| HOST-04 | Manual host entry | integration | manual-only -- requires DataGridView in editor dialog | N/A (Phase 5 UI) |
| CRED-01 | Credential mode configuration | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobDefinitionTests.CredentialMode" --no-build` | Wave 0 |
| CRED-02 | Stored credentials via Credential Manager | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CredentialTargetsTests.JobTarget" --no-build` | Wave 0 |
| RELY-01 | Persistence across restarts | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Persistence" --no-build` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Job" --no-build`
- **Per wave merge:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `SSH_Helper.Tests/Services/JobStorageServiceTests.cs` -- covers JMGT-01 through JMGT-04, HOST-02, RELY-01
- [ ] `SSH_Helper.Tests/Models/JobDefinitionTests.cs` -- covers HOST-01, CRED-01, model validation
- [ ] `SSH_Helper.Tests/Services/CredentialTargetsTests.cs` -- extend existing file for CRED-02 (JobPasswordTarget)
- [ ] `SSH_Helper.Tests/Utilities/ContentHasherTests.cs` -- covers drift detection hashing

## Sources

### Primary (HIGH confidence)
- Existing codebase: `ConfigurationService.cs` -- persistence pattern with backup/recovery
- Existing codebase: `PresetManager.cs` -- CRUD + event pattern for service layer
- Existing codebase: `ICredentialProvider.cs` + `CredentialManagerProvider.cs` + `CredentialTargets.cs` -- credential storage infrastructure
- Existing codebase: `CsvManager.cs` -- CSV import/export patterns
- Existing codebase: `AppConfiguration.cs` + `ApplicationState.cs` -- host data storage format (`List<Dictionary<string, string>>`)
- Existing codebase: `HostConnection.cs` + `InputValidator.cs` -- validation patterns

### Secondary (MEDIUM confidence)
- .NET 8 BCL: `System.Security.Cryptography.SHA256.HashData()` -- verified available in .NET 8

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all libraries already in project, no new dependencies needed
- Architecture: HIGH -- directly mirrors proven existing patterns in the codebase
- Pitfalls: HIGH -- identified from code inspection of actual integration points

**Research date:** 2026-03-07
**Valid until:** 2026-04-07 (stable -- no external dependencies or fast-moving APIs)
