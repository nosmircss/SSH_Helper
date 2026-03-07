# Phase 4: History & Output - Context

**Gathered:** 2026-03-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Every job run produces a complete record with full output, and old history is automatically pruned to prevent unbounded storage growth. This phase delivers the job history service, per-run output persistence, dual pruning (count + age), and a query API for filtering and searching. The management UI that displays this data is Phase 5.

</domain>

<decisions>
## Implementation Decisions

### Storage approach
- Dedicated `JobHistoryService` separate from existing `HistoryStorageService` (which handles manual execution history)
- Per-job subfolders: `job-history/{jobId}/` with per-run files inside
- Per-job index files: `job-history/{jobId}/index.json` — each job has its own small index for fast per-job queries
- Storage location: `%LocalAppData%\SSH_Helper\job-history\` alongside existing `history/` folder
- When a job is deleted, prompt user whether to also delete its history (matches Phase 1 deletion prompt decision)

### Output capture
- Per-host output stored separately in the run payload (matching existing `HostHistoryEntry` pattern from `HistoryRunPayload`)
- Output embedded inline in the run JSON file — one file per run contains everything (metadata + per-host output)
- Maximum output size: ~1MB per host, truncated with a marker. Configurable in settings
- Event-driven handoff: `JobExecutionService.JobCompleted` event enhanced to also pass per-host output. `JobHistoryService` subscribes and persists. Clean separation from execution pipeline

### Pruning strategy
- Per-job retention limits (each job keeps its own N runs / X days)
- Defaults: 50 runs per job, 30 days — whichever limit hits first
- Per-job overrides possible (stored on `JobDefinition` or in job history settings)
- Pruning runs after each run save (same pattern as existing `HistoryStorageService.EnforceRetention`)
- Per-job manual clear: "Clear history" capability per job (UI button in Phase 5) — deletes all run files in that job's subfolder

### Search & filter
- Filter runs by job, status (success/fail), and date range
- Simple case-insensitive string match within output of a single selected run (same approach as existing Find dialog)
- Search scope: single run only — user selects a run, then searches within its output. Filter handles finding the right run
- Query API: methods like `GetRunsForJob(jobId, filter)` returning filtered/sorted results. Filter object with status, dateRange, maxResults. UI calls this API

### Claude's Discretion
- JobHistoryService internal design and method signatures
- Run record model design (new model or extend existing HistoryRunPayload pattern)
- How the enhanced JobCompleted event carries per-host output (new event args type or extended JobRunResult)
- Per-job index file schema
- Default output truncation threshold (around 1MB per host, exact value flexible)
- How per-job pruning limits are stored (on JobDefinition vs separate config)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `HistoryStorageService`: Pattern for index + per-run payload files, atomic writes, corrupt recovery, retention enforcement — mirror architecture for job history
- `HistoryRunPayload`: Model with Id, Output, HostResults (List<HostHistoryEntry>), Details — reference for job run payload design
- `HistoryIndexEntry`: Lightweight metadata (Id, Label, CreatedAtUtc, HasHostResults, HasDetails, RunFileName) — reference for job run index entries
- `HistoryIdGenerator`: Unique ID generation for history entries — reuse directly
- `HistoryResultStore`: In-memory cache pattern — reference for job history caching if needed
- `JobRunResult`: Already captures JobId, JobName, StartedUtc, CompletedUtc, Success, HostsSucceeded, HostsFailed, ErrorMessage — the handoff point from Phase 3
- `JobExecutionService.JobCompleted` event: Fires after each run with `JobRunResult` — subscribe here for history recording

### Established Patterns
- Atomic JSON writes with `.tmp` + `.bak` (HistoryStorageService.WriteJsonAtomic)
- Selective deserialization for performance (DeserializePayloadLightweight skips large fields)
- Index + payload separation (lightweight index for list views, full payload loaded on demand)
- Event-driven service-to-UI communication (EventHandler<T>)
- `sealed` service classes with constructor injection
- `%LocalAppData%\SSH_Helper\` as storage root

### Integration Points
- `JobExecutionService`: Subscribe to `JobCompleted` event for automatic history recording
- `JobStorageService`: Read job definitions for pruning settings, deletion coordination
- `AppConfiguration`: Global defaults for retention limits, output size caps
- `Form1` constructor: Wire `JobHistoryService` via manual DI
- Phase 5 UI: Will consume the query API to display run history

</code_context>

<specifics>
## Specific Ideas

- The existing `HistoryStorageService` architecture (index + payload files, atomic writes, selective deserialization) is proven and should be mirrored closely for job history
- Per-job subfolders make job deletion cleanup trivial — just delete the folder
- The 1MB per-host output cap prevents a chatty command (e.g., large show-run on many hosts) from creating multi-GB history files
- STATE.md flags "Output file performance with 1000+ files per job needs benchmarking" — per-job pruning to 50 entries addresses this concern

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 04-history-output*
*Context gathered: 2026-03-07*
