## ADDED Requirements
### Requirement: Readfile manual file picker
The scripting runtime SHALL support an explicit `readfile.select_file` mode that lets an operator choose the file to read during manual execution.

When `readfile.select_file` is `true`:
- the runtime SHALL show a file-selection prompt before reading;
- `readfile.message`, when provided, SHALL replace the default prompt text shown in that file-selection prompt;
- `readfile.path`, when provided, SHALL be used only as the initial seed for the prompt;
- `readfile.fileext`, when provided, SHALL restrict the browse dialog to those extensions and SHALL reject any final resolved path that does not match one of the allowed extensions;
- the selected file SHALL still pass the existing read-path validation and line-processing rules.

#### Scenario: Manual run selects a file to read
- **WHEN** a manual script execution runs a `readfile` step with `select_file: true`
- **AND** the operator chooses a file
- **THEN** the runtime reads the selected file
- **AND** stores the processed lines into the configured `into` variable

#### Scenario: File selection is cancelled
- **WHEN** a `readfile` step with `select_file: true` is prompted during a manual run
- **AND** the operator cancels the prompt
- **THEN** the runtime sets the `into` variable to an empty list
- **AND** the script stops immediately with a cancelled status

#### Scenario: Scheduler-triggered execution reaches picker mode
- **WHEN** a scheduler-triggered execution runs a `readfile` step with `select_file: true`
- **THEN** the runtime does not open a file-selection prompt
- **AND** the step fails with a manual-run-only error unless `on_error: continue` is configured

#### Scenario: Manual run customizes the picker prompt and file types
- **WHEN** a manual script execution runs a `readfile` step with `select_file: true`
- **AND** the step provides `message` and `fileext`
- **THEN** the prompt shows the custom message text
- **AND** the picker accepts only files matching the configured extensions
