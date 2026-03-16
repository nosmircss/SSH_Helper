# Phase 4: History & Output - Research

**Researched:** 2026-03-07
**Domain:** Job history persistence, output capture, retention/pruning, query API
**Confidence:** HIGH

## Summary

Phase 4 adds a `JobHistoryService` that subscribes to the existing `JobExecutionService.JobCompleted` event, persists per-run records with full per-host output to disk, enforces dual retention limits (count + age), and exposes a query API for filtering and searching run history. The architecture closely mirrors the existing `HistoryStorageService` (index + payload files, atomic writes, corrupt recovery) but uses per-job subfolders for isolation and cleanup.

The key integration challenge is that the current `JobRunResult` event args do NOT carry per-host output -- it is a lightweight summary with only counts and error messages. The `ExecutionResult` objects containing per-host `Output` strings are created inside `ExecuteJobCoreAsync` and discarded after counting success/failure. Phase 4 must either enhance `JobRunResult` to carry per-host output or introduce a new event/event args type that passes the full `List<ExecutionResult>` alongside the summary.

All storage patterns (atomic JSON writes, index documents, selective deserialization, retention enforcement) are proven in the existing codebase and should be mirrored closely. No new NuGet packages are needed -- Newtonsoft.Json handles all serialization, `System.Text.Json` handles lightweight deserialization, and `System.IO` handles file operations.

**Primary recommendation:** Mirror HistoryStorageService architecture with per-job subfolders; extend JobRunResult (or create new event args) to carry per-host output for the handoff from execution to history.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Dedicated `JobHistoryService` separate from existing `HistoryStorageService` (which handles manual execution history)
- Per-job subfolders: `job-history/{jobId}/` with per-run files inside
- Per-job index files: `job-history/{jobId}/index.json` -- each job has its own small index for fast per-job queries
- Storage location: `%LocalAppData%\SSH_Helper\job-history\` alongside existing `history/` folder
- When a job is deleted, prompt user whether to also delete its history (matches Phase 1 deletion prompt decision)
- Per-host output stored separately in the run payload (matching existing `HostHistoryEntry` pattern from `HistoryRunPayload`)
- Output embedded inline in the run JSON file -- one file per run contains everything (metadata + per-host output)
- Maximum output size: ~1MB per host, truncated with a marker. Configurable in settings
- Event-driven handoff: `JobExecutionService.JobCompleted` event enhanced to also pass per-host output. `JobHistoryService` subscribes and persists. Clean separation from execution pipeline
- Per-job retention limits (each job keeps its own N runs / X days)
- Defaults: 50 runs per job, 30 days -- whichever limit hits first
- Per-job overrides possible (stored on `JobDefinition` or in job history settings)
- Pruning runs after each run save (same pattern as existing `HistoryStorageService.EnforceRetention`)
- Per-job manual clear: "Clear history" capability per job (UI button in Phase 5) -- deletes all run files in that job's subfolder
- Filter runs by job, status (success/fail), and date range
- Simple case-insensitive string match within output of a single selected run (same approach as existing Find dialog)
- Search scope: single run only -- user selects a run, then searches within its output. Filter handles finding the right run
- Query API: methods like `GetRunsForJob(jobId, filter)` returning filtered/sorted results. Filter object with status, dateRange, maxResults. UI calls this API

### Claude's Discretion
- JobHistoryService internal design and method signatures
- Run record model design (new model or extend existing HistoryRunPayload pattern)
- How the enhanced JobCompleted event carries per-host output (new event args type or extended JobRunResult)
- Per-job index file schema
- Default output truncation threshold (around 1MB per host, exact value flexible)
- How per-job pruning limits are stored (on JobDefinition vs separate config)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| HIST-01 | Each job run records start/end time, duration, success state, and per-host success/failure counts | JobRunResult already captures StartedUtc, CompletedUtc, Success, HostsSucceeded, HostsFailed. Duration is derived. New JobRunRecord model wraps this with a unique run ID and persists it to the per-job index. |
| HIST-02 | Full SSH output is persisted per run in dedicated output files | Per-host output captured via enhanced JobCompleted event args. Stored inline in per-run JSON payload file within the job's subfolder. Truncation at ~1MB per host with configurable threshold. |
| HIST-03 | History is automatically pruned by whichever limit hits first: max entries per job OR age-based retention | Dual pruning in EnforceRetention: remove entries exceeding max count (default 50), then remove entries older than retention period (default 30 days). Runs after each SaveRun call. |
| HIST-04 | User can search and filter within stored job output | Query API with JobRunFilter (status, dateRange, maxResults) for filtering runs. Case-insensitive string search within a single run's per-host output for content search. |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Newtonsoft.Json | 13.0.3 | JSON serialization for index and payload files | Already used project-wide, consistent with HistoryStorageService |
| System.Text.Json | built-in | Lightweight/selective deserialization for performance | Already used in HistoryStorageService.DeserializePayloadLightweight |
| System.IO | built-in | File system operations, atomic writes | Standard .NET, proven patterns in existing code |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| HistoryIdGenerator | existing | Unique run ID generation (GUID ToString("N")) | Every SaveRun call |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Per-run JSON files | SQLite/LiteDB | Adds dependency; JSON files match existing pattern and scale fine for 50 entries per job |
| Inline output in JSON | Separate .txt output files | Simpler model with one file per run; separate files would help with very large outputs but adds complexity |

**Installation:**
```bash
# No new packages needed -- all dependencies are already present
```

## Architecture Patterns

### Recommended Project Structure
```
Services/
  JobHistoryService.cs        # Core service: save, load, prune, query, delete
Models/
  JobRunRecord.cs             # Per-run metadata for the index
  JobRunPayload.cs            # Full run payload (metadata + per-host output)
  JobRunFilter.cs             # Query filter object (status, dateRange, maxResults)
  JobRunResult.cs             # MODIFIED: add HostOutputs property for event handoff
Models/
  AppConfiguration.cs         # MODIFIED: add job history default settings
Models/
  JobDefinition.cs            # MODIFIED: optional per-job retention overrides
```

### Storage Layout
```
%LocalAppData%\SSH_Helper\
  config.json                 # Existing
  history/                    # Existing manual execution history
  history.index.json          # Existing
  jobs.json                   # Existing
  job-history/                # NEW: Phase 4
    {jobId}/                  # Per-job subfolder
      index.json              # Per-job run index (lightweight metadata)
      {runId}.json            # Per-run payload (metadata + per-host output)
    {jobId}/
      index.json
      {runId}.json
```

### Pattern 1: Event-Driven History Recording
**What:** JobHistoryService subscribes to JobExecutionService.JobCompleted event and automatically persists run records.
**When to use:** Every time a job completes (success or failure).
**Example:**
```csharp
// Source: Existing pattern in HistoryStorageService + JobExecutionService
public sealed class JobHistoryService
{
    private readonly string _baseDirectory;

    public JobHistoryService(string? basePath = null)
    {
        _baseDirectory = basePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SSH_Helper", "job-history");
    }

    // Called from Form1 to wire up event subscription
    public void SubscribeTo(JobExecutionService executionService)
    {
        executionService.JobCompleted += OnJobCompleted;
    }

    private void OnJobCompleted(object? sender, JobRunResult result)
    {
        // result now carries HostOutputs (per-host output data)
        SaveRun(result);
    }
}
```

### Pattern 2: Per-Job Index + Payload Files
**What:** Each job gets its own subfolder with a lightweight index.json for fast listing and individual {runId}.json files for full payloads loaded on demand.
**When to use:** All history storage and retrieval operations.
**Example:**
```csharp
// Source: Mirror of HistoryStorageService index/payload pattern
// Per-job index document
public sealed class JobRunIndexDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<JobRunRecord> Entries { get; set; } = new();
}

// Lightweight index entry (loaded for list views)
public sealed class JobRunRecord
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }
    public bool Success { get; set; }
    public int HostsSucceeded { get; set; }
    public int HostsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public string RunFileName { get; set; } = string.Empty;
}

// Full payload (loaded when user selects a run)
public sealed class JobRunPayload
{
    public string Id { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public DateTime StartedUtc { get; set; }
    public DateTime CompletedUtc { get; set; }
    public bool Success { get; set; }
    public int HostsSucceeded { get; set; }
    public int HostsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public List<JobHostOutput> HostOutputs { get; set; } = new();
}

public sealed class JobHostOutput
{
    public string HostAddress { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Pattern 3: Dual Retention Enforcement
**What:** After each save, enforce both count-based (max N entries) and age-based (max X days) pruning, whichever limit hits first.
**When to use:** After every SaveRun call.
**Example:**
```csharp
// Source: Extended from HistoryStorageService.EnforceRetention
private void EnforceRetention(
    JobRunIndexDocument document,
    int maxEntries,
    int maxAgeDays)
{
    // 1. Age-based: remove entries older than maxAgeDays
    var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays);
    var expired = document.Entries
        .Where(e => e.CompletedUtc < cutoff)
        .ToList();
    foreach (var entry in expired)
    {
        document.Entries.Remove(entry);
        DeleteRunFile(entry);
    }

    // 2. Count-based: trim to maxEntries (keep newest)
    while (document.Entries.Count > maxEntries)
    {
        var oldest = document.Entries[^1];
        document.Entries.RemoveAt(document.Entries.Count - 1);
        DeleteRunFile(oldest);
    }
}
```

### Pattern 4: Output Truncation
**What:** Truncate per-host output at a configurable threshold (~1MB) with a clear marker indicating truncation occurred.
**When to use:** During SaveRun when building the payload from ExecutionResults.
**Example:**
```csharp
// Source: Existing HistoryStorageService.ReadBoundedStringValue pattern
private const int DefaultMaxOutputBytes = 1_048_576; // 1 MB

private static string TruncateOutput(string output, int maxChars)
{
    if (output.Length <= maxChars)
        return output;

    var marker = $"\n[... output truncated: {output.Length - maxChars:N0} characters removed ...]\n";
    var keepChars = maxChars - marker.Length;
    if (keepChars <= 0)
        return marker;

    return output.Substring(0, keepChars) + marker;
}
```

### Pattern 5: Query/Filter API
**What:** A filter object and query method for retrieving filtered, sorted run records.
**When to use:** Phase 5 UI consumes this to display filtered history lists.
**Example:**
```csharp
// Filter object
public sealed class JobRunFilter
{
    public bool? Success { get; set; }           // null = all, true = success only, false = failed only
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int MaxResults { get; set; } = 50;
}

// Query method on JobHistoryService
public IReadOnlyList<JobRunRecord> GetRunsForJob(string jobId, JobRunFilter? filter = null)
{
    var index = LoadJobIndex(jobId);
    IEnumerable<JobRunRecord> results = index.Entries;

    if (filter != null)
    {
        if (filter.Success.HasValue)
            results = results.Where(r => r.Success == filter.Success.Value);
        if (filter.FromUtc.HasValue)
            results = results.Where(r => r.CompletedUtc >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue)
            results = results.Where(r => r.CompletedUtc <= filter.ToUtc.Value);
    }

    // Newest first (index is already insertion-ordered newest-first)
    return results.Take(filter?.MaxResults ?? 50).ToList();
}
```

### Anti-Patterns to Avoid
- **Storing all runs in a single global index:** Per-job indexes are faster to load and keep cleanup isolated. A single global index would grow linearly with total runs across all jobs.
- **Loading full payloads for list views:** Always use the lightweight index for listing runs. Load payloads on demand when a user selects a specific run.
- **Sharing HistoryStorageService between manual and job history:** These have different storage layouts and lifecycle. Keep them separate to avoid coupling.
- **Synchronous file I/O on UI thread:** All file operations in the event handler should be fast (writing to local disk), but consider wrapping SaveRun in Task.Run if needed to avoid blocking the event raiser.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Unique run IDs | Custom ID generation | HistoryIdGenerator.NewId() | Already exists, uses GUID ToString("N"), consistent format |
| Atomic file writes | Custom temp-file logic | Mirror WriteJsonAtomic from HistoryStorageService | Handles .tmp + .bak, File.Replace fallbacks, cleanup. Proven pattern |
| JSON serialization | Manual string building | Newtonsoft.Json JsonConvert.SerializeObject | Already the project standard, handles DateTime, nulls, formatting |
| Corrupt file recovery | Ignore and crash | Mirror TryBackupCorruptIndex pattern | Saves corrupt file with timestamp suffix, starts fresh |
| Output truncation | Naive string.Substring | Mirror ReadBoundedStringValue pattern | Inserts clear truncation marker, handles edge cases |

**Key insight:** The existing HistoryStorageService contains ~800 lines of battle-tested storage code. Phase 4's JobHistoryService should mirror its proven patterns rather than inventing new approaches. The per-job subfolder structure is the main architectural difference.

## Common Pitfalls

### Pitfall 1: JobRunResult Does Not Carry Per-Host Output
**What goes wrong:** The current `JobRunResult` raised by `JobCompleted` event only contains counts (HostsSucceeded, HostsFailed) and a summary ErrorMessage. The per-host `ExecutionResult.Output` strings are discarded inside `ExecuteJobCoreAsync` after counting.
**Why it happens:** `JobRunResult` was designed as a lightweight Phase 4 handoff point (per the Phase 3 plan comment), but the handoff was never completed -- it was left as a skeleton.
**How to avoid:** Either extend `JobRunResult` to carry a `List<JobHostOutput>` (containing HostAddress, Output, Success, ErrorMessage per host), or create a new event args class that wraps both the summary and the per-host details.
**Warning signs:** JobHistoryService receives JobCompleted event but has no output data to persist.
**Recommendation:** Extend JobRunResult with a `List<JobHostOutput> HostOutputs` property. This is the simplest change -- the event signature stays the same, and only the event args payload grows.

### Pitfall 2: Output Truncation Must Happen BEFORE Serialization
**What goes wrong:** If truncation happens after JSON serialization, the serialized file could already be huge in memory before truncation.
**Why it happens:** Natural instinct is to build the full object then serialize. But with 100 hosts x 1MB each = 100MB in memory before any truncation.
**How to avoid:** Truncate each host's output string BEFORE building the payload object. Apply `TruncateOutput` to each `ExecutionResult.Output` when constructing the `JobHostOutput` list.
**Warning signs:** Large memory spikes during history recording.

### Pitfall 3: File System Race During Concurrent Job Completions
**What goes wrong:** Two jobs completing simultaneously could race on creating the job-history base directory or on writing index files.
**Why it happens:** Each job has its own subfolder so index file races are unlikely between jobs. But the base directory creation could race on first-ever use.
**How to avoid:** Use `Directory.CreateDirectory` (idempotent, no-op if exists). For per-job index writes, there is no race because each job has its own subfolder and index file. Concurrent runs of the same job are already prevented by `_runningJobs.ContainsKey` in JobExecutionService.
**Warning signs:** DirectoryNotFoundException or IOException on first job completion.

### Pitfall 4: Forgetting Age-Based Pruning
**What goes wrong:** Only implementing count-based pruning. The user ends up with old entries from a job that ran frequently months ago but has been disabled since.
**Why it happens:** Count-based is simpler to implement and the existing `HistoryStorageService.EnforceRetention` only does count-based.
**How to avoid:** Phase 4 explicitly requires dual pruning. Implement age-based first (remove entries with CompletedUtc older than cutoff), then count-based (trim remaining to max count).
**Warning signs:** Retention is only partial; old entries accumulate indefinitely as long as they are under the count limit.

### Pitfall 5: Job Deletion Without History Cleanup
**What goes wrong:** Deleting a job in `JobStorageService.Delete()` leaves orphaned history files on disk forever.
**Why it happens:** `JobStorageService` knows nothing about `JobHistoryService`.
**How to avoid:** Per CONTEXT.md: "When a job is deleted, prompt user whether to also delete its history." This means the deletion coordination happens at the UI layer (Form1 or job editor dialog). The UI asks the user, then calls `JobHistoryService.DeleteAllHistory(jobId)` if confirmed. Do NOT wire this inside JobStorageService.
**Warning signs:** Growing `job-history/` folder with subfolders for deleted jobs.

## Code Examples

### Existing Patterns to Mirror

#### Atomic JSON Write (from HistoryStorageService)
```csharp
// Source: Services/HistoryStorageService.cs lines 732-799
// This EXACT pattern should be extracted or duplicated for JobHistoryService
private static void WriteJsonAtomic(string path, string json, bool createBackup)
{
    var tempPath = path + ".tmp";
    try
    {
        File.WriteAllText(tempPath, json, Utf8NoBom);
        if (File.Exists(path))
        {
            if (createBackup)
                File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
            else
                File.Replace(tempPath, path, null, ignoreMetadataErrors: true);
            return;
        }
        File.Move(tempPath, path);
    }
    finally
    {
        if (File.Exists(tempPath))
            try { File.Delete(tempPath); } catch { }
    }
}
```

#### Corrupt Index Recovery (from HistoryStorageService)
```csharp
// Source: Services/HistoryStorageService.cs lines 569-585
private JobRunIndexDocument ReadJobIndex(string jobId)
{
    var indexPath = GetIndexPath(jobId);
    if (!File.Exists(indexPath))
        return new JobRunIndexDocument();
    try
    {
        var json = File.ReadAllText(indexPath);
        var parsed = JsonConvert.DeserializeObject<JobRunIndexDocument>(json);
        return parsed ?? new JobRunIndexDocument();
    }
    catch
    {
        TryBackupCorruptFile(indexPath);
        return new JobRunIndexDocument();
    }
}
```

#### Event Subscription Wiring (from Form1.cs existing pattern)
```csharp
// Source: Form1.cs pattern for service wiring
// In Form1 constructor, after creating services:
_jobHistoryService = new JobHistoryService(_configService.ConfigFilePath);
_jobHistoryService.SubscribeTo(_jobExecutionService);
```

#### How ExecuteJobCoreAsync Currently Discards Output
```csharp
// Source: Services/JobExecutionService.cs lines 427-448
// Currently the 'results' list (List<ExecutionResult>) contains per-host Output
// but the JobRunResult only carries counts:
var runResult = new JobRunResult
{
    // ... summary fields only ...
    HostsSucceeded = succeeded,
    HostsFailed = failed,
};
// Per-host output is lost here
JobCompleted?.Invoke(this, runResult);
```

#### Enhanced JobRunResult for Phase 4 Handoff
```csharp
// Extend existing JobRunResult to carry per-host output
public class JobRunResult
{
    // ... existing properties unchanged ...

    /// <summary>
    /// Per-host execution outputs for history recording.
    /// Populated by ExecuteJobCoreAsync for the JobCompleted event.
    /// May be null for error-path completions where no hosts were reached.
    /// </summary>
    public List<JobHostOutput>? HostOutputs { get; set; }
}

public sealed class JobHostOutput
{
    public string HostAddress { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Global history index | Per-entity indexes | Phase 4 design | Job-scoped indexes are faster to load and easier to clean up |
| Count-only retention | Dual count + age retention | Phase 4 design | Prevents stale entries from lingering indefinitely |
| Lightweight event args | Enhanced event args with output | Phase 4 requirement | Enables history recording without coupling execution and storage |

**Deprecated/outdated:**
- HistoryEntry (in-config storage): Legacy format, already migrated to external files. Not relevant to Phase 4.

## Open Questions

1. **Where to store per-job retention overrides?**
   - What we know: CONTEXT.md says "Per-job overrides possible (stored on JobDefinition or in job history settings)." This is Claude's discretion.
   - What's unclear: Adding properties to JobDefinition means they serialize to jobs.json. Alternatively, storing in the per-job index.json keeps history settings with history data.
   - Recommendation: Add optional `MaxHistoryRuns` (int?) and `HistoryRetentionDays` (int?) to JobDefinition. When null, use global defaults from AppConfiguration. Simpler than a separate settings mechanism. Consistent with how Delay/Timeout overrides work on PresetInfo.

2. **Should WriteJsonAtomic be extracted to a shared utility?**
   - What we know: The exact same 65-line method exists in HistoryStorageService. JobHistoryService needs the same logic.
   - What's unclear: Whether duplicating it or extracting to a Utilities class is preferred.
   - Recommendation: Extract to a static utility class (e.g., `Utilities/JsonFileWriter.cs`) to avoid duplication. Both services can reference it. This is a clean refactor within the phase.

3. **Thread safety of SaveRun during JobCompleted event**
   - What we know: JobCompleted fires from async ExecuteJobCoreAsync, possibly from ThreadPool thread. Multiple jobs can complete near-simultaneously, but each has its own subfolder and index file.
   - What's unclear: Whether SaveRun needs explicit locking.
   - Recommendation: No lock needed because each job's index is independent. The only shared resource is the base directory creation, which is idempotent via Directory.CreateDirectory.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.7.0 with FluentAssertions 6.12.0 and Moq 4.20.70 |
| Config file | SSH_Helper.Tests/SSH_Helper.Tests.csproj |
| Quick run command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryService" -x` |
| Full suite command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HIST-01 | Run record captures start/end time, duration, success, host counts | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests" -x` | No - Wave 0 |
| HIST-02 | Full SSH output persisted per run, per host, with truncation | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests" -x` | No - Wave 0 |
| HIST-03 | Dual pruning: count-based AND age-based retention | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests" -x` | No - Wave 0 |
| HIST-04 | Query API with status/date filter + string search in output | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryServiceTests" -x` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryService" -x`
- **Per wave merge:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `SSH_Helper.Tests/Services/JobHistoryServiceTests.cs` -- covers HIST-01, HIST-02, HIST-03, HIST-04
- [ ] `SSH_Helper.Tests/Models/JobRunRecordTests.cs` -- covers model validation if any
- Framework install: None needed -- xUnit, FluentAssertions, Moq already in test project
- Test pattern: Follow existing `HistoryStorageServiceTests` pattern -- temp directory isolation, IDisposable cleanup

### Test Pattern Reference
```csharp
// Source: SSH_Helper.Tests/Services/HistoryStorageServiceTests.cs
// Follow this exact pattern for JobHistoryServiceTests:
public sealed class JobHistoryServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JobHistoryService _service;

    public JobHistoryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            $"JobHistoryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new JobHistoryService(
            Path.Combine(_tempDir, "job-history"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
```

## Sources

### Primary (HIGH confidence)
- Codebase inspection: Services/HistoryStorageService.cs -- full 800-line reference implementation for index+payload storage
- Codebase inspection: Models/HistoryRunPayload.cs, Models/HistoryIndex.cs -- existing model patterns
- Codebase inspection: Services/JobExecutionService.cs -- JobCompleted event, ExecuteJobCoreAsync showing output gap
- Codebase inspection: Models/JobRunResult.cs -- current event args showing missing per-host output
- Codebase inspection: Models/AppConfiguration.cs -- existing settings structure for adding defaults
- Codebase inspection: Models/JobDefinition.cs -- existing model for optional retention override properties
- Codebase inspection: SSH_Helper.Tests/ -- existing test patterns and framework setup

### Secondary (MEDIUM confidence)
- CONTEXT.md decisions from user discussion session -- all architectural choices locked

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - No new libraries needed; all patterns exist in codebase
- Architecture: HIGH - Directly mirrors proven HistoryStorageService architecture; decisions locked by user
- Pitfalls: HIGH - Identified from direct code inspection; the JobRunResult output gap is visible in source
- Models: HIGH - All referenced types (ExecutionResult, HostHistoryEntry, etc.) inspected directly

**Research date:** 2026-03-07
**Valid until:** Indefinite (stable internal codebase, no external dependency concerns)
