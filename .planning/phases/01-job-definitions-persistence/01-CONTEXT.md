# Phase 1: Job Definitions & Persistence - Context

**Gathered:** 2026-03-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can create, edit, and manage self-contained job definitions with dedicated host lists and credential configurations that persist across restarts. This phase delivers the data model, CRUD operations, storage, and service layer. Scheduling, execution, history, and UI are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Job storage approach
- Store jobs in a dedicated `jobs.json` file in `%LocalAppData%\SSH_Helper\`, separate from `config.json`
- New `JobStorageService` mirroring `ConfigurationService` patterns: cached read, atomic write with `.bak` backup, corrupt file recovery
- Each job has a stable GUID-based ID (like `HistoryEntry.Id`); name is a display field, not the key
- On job deletion, prompt user whether to also delete run history (Phase 4 concern, but deletion flag stored now)

### Job-preset relationship
- Jobs reference presets by name + a content hash computed at link time
- Bidirectional drift awareness:
  - When editing a preset, inform user which scheduled jobs reference it; user must acknowledge before saving
  - When a job detects its preset's content hash has changed, block execution (scheduled or manual) until user reviews and re-acknowledges
- Folder jobs check all presets in the folder for drift; if any changed, block and list which ones
- If a referenced preset is deleted: warn user at delete time showing which jobs use it; if user proceeds, those jobs auto-disable with a "preset not found" error; user must re-link to re-enable
- Same behavior for folder jobs if the target folder is deleted

### Host list format
- Full column support matching the main grid structure (Host_IP, port, username, password, custom variable columns)
- Stored as `List<Dictionary<string, string>>` per job (same format as `ApplicationState.Hosts`)
- Column names list stored alongside host data for schema preservation
- "Copy from main grid" copies only checked (selected) rows, preserving all columns
- CSV import uses existing `CsvManager` patterns
- Inline mini-grid (DataGridView) in the job editor for direct add/remove/edit of hosts

### Credential handling
- Three credential modes per job: `Stored`, `InheritFromApp`, `PerHostColumn`
- **Stored mode**: Single username/password per job stored via existing `ICredentialProvider` (Windows Credential Manager). Per-job target naming via `CredentialTargets` pattern using job GUID
- **InheritFromApp mode**: Job uses the app's current username at execution time + Credential Manager lookup for that username. Same behavior as manual execution
- **PerHostColumn mode**: Uses `username` and `password` columns from the job's own host grid. Each row provides its own credentials
- No DPAPI — reuse existing Windows Credential Manager infrastructure

### Claude's Discretion
- Job model class design and property naming
- Exact content hash algorithm (SHA256, MD5, etc.)
- JobStorageService internal caching strategy
- Event patterns for job CRUD notifications
- Validation rules for job names (uniqueness, length, characters)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ConfigurationService`: Pattern for JSON persistence with `.bak` backup, corrupt recovery, caching — mirror for `JobStorageService`
- `ICredentialProvider` + `CredentialManagerProvider`: Windows Credential Manager P/Invoke wrapper — reuse directly for stored job credentials
- `CredentialTargets`: Target name generation — extend for per-job credential targets using job GUID
- `CsvManager`: CSV import/export — reuse for job host list CSV import
- `HostConnection.Parse()`: IP/port validation — reuse for job host validation
- `InputValidator`: Centralized validation — extend for job-specific validation
- `PresetManager`: Preset CRUD with events — reference pattern for job CRUD service design

### Established Patterns
- Service-oriented: Business logic in services, not in Form1
- Event-driven communication: Services raise events, UI subscribes (`EventHandler<T>`)
- Manual DI: Services instantiated in Form1 constructor, no container
- Guard clauses with `ArgumentNullException` for constructor params
- `sealed` for leaf service classes
- Newtonsoft.Json for persistence, `Formatting.Indented` for readability
- `CancellationToken` as last async parameter
- Sub-models grouped in parent file (like `AppConfiguration.cs` contains `WindowState`, `FontSettings`, etc.)

### Integration Points
- `PresetManager`: Must be extended to check for job references before preset rename/delete
- `ConfigurationService`: Jobs stored in separate file, but same `%LocalAppData%\SSH_Helper\` directory
- `Form1` constructor: New `JobStorageService` will be wired here
- Future phases will add `SchedulerService` and `JobExecutionService` that consume the job model

</code_context>

<specifics>
## Specific Ideas

- Preset editing should show a list of jobs that reference the preset, requiring acknowledgment before save — prevents silent breakage of scheduled workflows
- Folder job drift detection should list exactly which presets changed, not just "something changed"
- Job deletion prompt about history cleanup gives users control over data retention

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 01-job-definitions-persistence*
*Context gathered: 2026-03-07*
