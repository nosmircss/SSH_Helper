# job-scheduler Specification

## Purpose
TBD - created by archiving change replace-scheduler-drift-with-save-warning. Update Purpose after archive.
## Requirements
### Requirement: Preset save warning for referenced scheduled jobs
When an operator saves changes to an existing preset that is used by scheduled jobs, the system SHALL present a single save confirmation that identifies the affected jobs and explains that future scheduled and Run Now executions will use the updated preset contents.

#### Scenario: Save an existing referenced preset
- **WHEN** an operator saves changes to an existing preset that is referenced by one or more scheduled jobs
- **THEN** the save flow shows one confirmation dialog before saving
- **AND** the dialog preserves the unsaved preset diff as the primary review surface
- **AND** the dialog includes the affected job count and job names
- **AND** the dialog explains that future scheduled and Run Now executions will use the updated preset

#### Scenario: Review impacted scheduled jobs without hiding the diff
- **WHEN** the save confirmation warns that scheduled jobs are affected
- **THEN** the impacted-job count and warning text remain visible immediately
- **AND** the affected job list is available from a collapsed section without replacing the diff view

#### Scenario: Save a referenced preset after changing its name
- **WHEN** an operator edits an existing referenced preset and changes its name before saving
- **THEN** the save flow uses one dialog that offers `Rename Existing`, `Create New`, and `Cancel`
- **AND** the dialog explains that `Rename Existing` will carry affected scheduled jobs forward to the renamed preset

#### Scenario: Save a new unreferenced preset
- **WHEN** an operator saves a brand-new preset that does not mutate an existing preset already used by scheduled jobs
- **THEN** the system does not need to show the scheduled-job impact warning

### Requirement: Scheduler execution ignores legacy drift state
The system SHALL NOT block scheduled execution, Run Now execution, or scheduler list presentation based on legacy drift state persisted in job files.

#### Scenario: Legacy job retains a drift flag
- **WHEN** a job file still contains `HasDriftWarning = true`
- **THEN** scheduled evaluation still considers the job eligible based on its real enablement and schedule
- **AND** Run Now is not blocked by the legacy drift flag
- **AND** the scheduler UI does not show a drift-specific warning or name indicator for that job

### Requirement: Scheduler stored credentials round-trip
Scheduler jobs using stored credentials SHALL save credentials to Windows Credential Manager keyed by job ID and SHALL NOT persist plaintext passwords in scheduler job JSON.

#### Scenario: Save a new stored-credential job
- **WHEN** an operator saves a scheduler job in Stored credential mode with a username and password
- **THEN** the scheduler persists the job definition without plaintext password fields
- **AND** the username and password are written to Windows Credential Manager for that job ID

#### Scenario: Edit an existing stored-credential job without replacing the password
- **WHEN** an operator reopens a stored-credential job that already has saved credentials
- **THEN** the editor shows the stored username and indicates that a password is already stored
- **AND** leaving the password field blank on save preserves the existing stored password

### Requirement: Scheduler drift activation on target change
The system SHALL recompute scheduler drift state when a referenced preset or folder changes after a job was saved and SHALL block scheduled and run-now execution until the operator re-acknowledges or re-saves the job.

#### Scenario: Preset content change marks a preset job drifted
- **WHEN** a scheduler job targets a preset and that preset's commands change after the job was saved
- **THEN** the job is marked with `HasDriftWarning`
- **AND** the job list shows the drift state before the next execution attempt

#### Scenario: Folder content change marks a folder job drifted
- **WHEN** a scheduler job targets a folder and the current direct-child preset set or saved preset content hashes no longer match the job snapshot
- **THEN** the job is marked drifted
- **AND** scheduled and run-now execution are skipped until the operator reviews the job

### Requirement: Safe scheduler import for missing targets
Imported scheduler jobs with missing preset or folder targets SHALL be persisted in a disabled state with an explicit disabled reason.

#### Scenario: Import a job whose preset target is missing
- **WHEN** an accepted import entry references a preset that does not exist locally
- **THEN** the saved job is disabled
- **AND** its disabled reason identifies the missing preset target

#### Scenario: Import a job whose folder target is missing
- **WHEN** an accepted import entry references a preset folder that does not exist locally
- **THEN** the saved job is disabled
- **AND** its disabled reason identifies the missing folder target

### Requirement: Run-now attribution and single-instance scheduler window
The system SHALL preserve manual run-now attribution for scheduler notifications and SHALL reuse the existing modeless scheduler dialog instance when the operator reopens it from the menu or status bar.

#### Scenario: Run now from the scheduler dialog emits a run-now prefix
- **WHEN** the operator triggers Run Now from the scheduler dialog
- **THEN** the output panel uses the `[Run Now: JobName]` notification prefix for the resulting scheduler state and completion lines

#### Scenario: Reopen scheduler while it is already visible
- **WHEN** the operator clicks the Scheduler menu item or status-bar link while the scheduler dialog is already open
- **THEN** the existing modeless dialog is brought to the front
- **AND** no duplicate scheduler window is created

### Requirement: Scheduler missed-run lifecycle recording
The scheduler SHALL persist the last application shutdown timestamp and, on startup, detect recurring jobs missed during downtime and record one skipped scheduler summary event per affected job/startup window without executing those jobs automatically.

#### Scenario: Startup records a compact skipped summary from downtime
- **WHEN** the application restarts after downtime and a recurring scheduler job should have run one or more times while the app was closed
- **THEN** the scheduler does not execute the missed run immediately
- **AND** one skipped scheduler history event is recorded for that job summarizing the missed run count and downtime range

#### Scenario: First launch does not fabricate missed runs
- **WHEN** the application starts without any previously persisted shutdown timestamp
- **THEN** the scheduler does not create skipped events for historical time windows it cannot verify

### Requirement: Scheduler history retention policy enforcement
Scheduler run persistence SHALL honor per-job max-runs and retention-day overrides when present and SHALL otherwise use the configured global scheduler history defaults and per-host output-size cap.

#### Scenario: Per-job retention override prunes that job's history
- **WHEN** a scheduler job defines a smaller max-runs or retention-days override than the global defaults
- **THEN** history pruning for that job uses the per-job override values

#### Scenario: Global defaults apply when no override is set
- **WHEN** a scheduler job leaves retention overrides unset
- **THEN** the scheduler history store uses the configured global defaults for run count, retention days, and per-host output size

### Requirement: Scheduler history timestamp accuracy
The scheduler history list SHALL display each run's actual start time and duration derived from the persisted run start and completion timestamps.

#### Scenario: History row uses the stored start timestamp
- **WHEN** a scheduler run is shown in the scheduler history list
- **THEN** the `Started` column reflects the persisted run start time rather than the completion time
- **AND** the duration matches the difference between the stored start and completion timestamps

### Requirement: Scheduled job cancellation outcome and controls
The scheduler SHALL provide a Job List cancel action for running jobs and SHALL persist cancelled scheduled runs distinctly from failed or skipped runs.

#### Scenario: Operator cancels a running scheduled job from the Job List
- **WHEN** a scheduled job is running and the operator clicks `Cancel` in the scheduler Job List toolbar or context menu
- **THEN** the scheduler requests cancellation for that job without affecting unrelated runs
- **AND THEN** the job's final state is recorded as cancelled when execution unwinds

#### Scenario: Cancelled scheduled run is shown distinctly in history
- **WHEN** a scheduled run is cancelled after partial host output has been produced
- **THEN** scheduler history persists the run with a cancelled outcome and retained partial per-host output
- **AND THEN** scheduler notifications and result text distinguish the run from failed and skipped entries

### Requirement: Scheduler-local custom presets
The scheduler SHALL support a job-local custom preset target whose command or YAML script content is stored in the job definition instead of the shared preset library.

#### Scenario: Save a custom preset job
- **WHEN** an operator selects `Custom Preset` in the scheduler job editor, enters job-local content, and saves
- **THEN** the job definition is persisted with that custom preset content
- **AND** the job does not require a shared preset or folder target name

#### Scenario: Execute a custom YAML preset job
- **WHEN** a scheduler job with `Custom Preset` content contains a valid YAML script and the job runs
- **THEN** the scheduler executes it through the same script-capable preset execution pipeline used by shared presets
- **AND** the job uses the application default timeout when no shared preset is involved

#### Scenario: Import or export a custom preset job
- **WHEN** a scheduler job with a custom preset is exported and later imported
- **THEN** the job-local custom preset content round-trips with the job definition
- **AND** the imported job is not treated as missing a preset or folder target

### Requirement: Scheduler host-grid column parity
The scheduler Hosts tab SHALL support adding, renaming, deleting, and reordering host columns using the same protected-column rules as the main host grid.

#### Scenario: Add manual credential columns to a new job
- **WHEN** an operator creates a new scheduler job and adds `username` and `password` columns in the Hosts tab
- **THEN** the new columns appear in the scheduler grid without requiring import or copy-from-main first

#### Scenario: Protected host column cannot be deleted
- **WHEN** an operator attempts to delete the `Host_IP` column from the scheduler Hosts tab
- **THEN** the action is rejected
- **AND** the scheduler grid keeps the protected host column intact

### Requirement: Scheduler host-grid editing parity
The scheduler Hosts tab SHALL support the same keyboard and clipboard editing workflow as the main host grid for selection, copy, paste, delete, keypress-to-edit, and double-click edit initiation.

#### Scenario: Paste a host matrix into the scheduler grid
- **WHEN** an operator pastes tabular host data into the scheduler Hosts tab
- **THEN** the scheduler grid expands rows and columns as needed
- **AND** the pasted values populate the corresponding cells

#### Scenario: Clear selected scheduler cells with the keyboard
- **WHEN** an operator selects scheduler host cells and presses `Delete` or `Backspace`
- **THEN** the selected cell values are cleared using the same semantics as the main host grid

### Requirement: Scheduler host-grid visual parity
The scheduler Hosts tab SHALL present host rows with the same operator-facing visual cues as the main host grid, including row sizing, row-header/row-number affordances, themed scrolling treatment, and selection styling appropriate to the scheduler grid's controls.

#### Scenario: Scheduler host grid matches main-grid row presentation
- **WHEN** an operator opens the scheduler Hosts tab after using the main hosts grid
- **THEN** row height, row-header presentation, and overall grid chrome are visually consistent with the main hosts grid

#### Scenario: Scheduler host grid respects dark and light theme styling
- **WHEN** the application theme changes between dark and light modes
- **THEN** the scheduler Hosts tab updates its scrolling, selection, and grid styling to match the themed main hosts grid presentation

### Requirement: Scheduler host import and copy parity
The scheduler Hosts tab SHALL use the shared CSV import semantics already accepted by the main host grid and SHALL copy checked rows from the main grid when any are checked, otherwise copy all eligible host rows.

#### Scenario: Import a CSV accepted by the main grid
- **WHEN** an operator imports a host CSV file that the main host grid accepts
- **THEN** the scheduler Hosts tab parses the same headers and row values
- **AND** the resulting scheduler grid matches the imported host data

#### Scenario: Copy from main grid prefers checked rows
- **WHEN** the main host grid contains checked rows and the operator clicks Copy from Main Grid in the scheduler editor
- **THEN** only the checked rows are copied into the scheduler Hosts tab

### Requirement: Scheduler host-count freshness
The scheduler Hosts tab SHALL refresh its host-count label whenever inline edits change whether a row has a non-empty `Host_IP`.

#### Scenario: Clearing a host address decrements the count
- **WHEN** an operator clears the `Host_IP` value from an existing scheduler host row
- **THEN** the displayed scheduler host count updates immediately to exclude that row

### Requirement: Scheduler per-job timeout overrides
The scheduler SHALL support optional per-job command and connection timeout overrides that apply to scheduled execution and `Run Now` without requiring preset changes.

#### Scenario: Preset-backed job overrides inherited timeouts
- **WHEN** an operator enables per-job command or connection timeout overrides for a scheduled job targeting a preset or folder and saves the job
- **THEN** the saved job persists only those override values that were enabled
- **AND** later scheduled or `Run Now` execution uses the per-job override instead of the inherited timeout for that dimension

#### Scenario: Unset override keeps inherited timeout behavior
- **WHEN** a scheduled job leaves one or both timeout overrides disabled
- **THEN** execution continues to inherit command timeout from the preset timeout or application default and connection timeout from the application default
- **AND** existing jobs without the new fields continue to behave as they did before the feature was added

#### Scenario: Custom preset job shows app default as inherited source
- **WHEN** an operator edits a scheduled job targeting `Custom Preset` and does not enable a command timeout override
- **THEN** the editor indicates that the inherited command timeout source is the application default
- **AND** execution uses that application default command timeout until the operator enables a per-job override

