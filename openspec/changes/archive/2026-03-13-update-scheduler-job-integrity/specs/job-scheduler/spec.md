## ADDED Requirements

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
