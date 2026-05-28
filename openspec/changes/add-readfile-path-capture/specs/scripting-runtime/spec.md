## MODIFIED Requirements
### Requirement: Readfile manual file picker
The scripting runtime SHALL support an explicit `readfile.select_file` mode that lets an operator choose the file to read during manual execution.

When `readfile.select_file` is `true`:
- the runtime SHALL show a file-selection prompt before reading or path capture;
- `readfile.autobrowse`, when `true`, SHALL immediately open the native browse dialog for that prompt, SHALL bypass any intermediate custom path-entry form, and SHALL treat the browse result as the prompt result;
- when `readfile.path_only` is `true` and `readfile.autobrowse` is omitted, the runtime SHALL treat `readfile.autobrowse` as `true` by default;
- when `readfile.path_only` is `true` and `readfile.autobrowse` is explicitly `false`, the runtime SHALL keep the custom path-entry form;
- `readfile.message`, when provided, SHALL replace the default prompt text shown in that file-selection prompt;
- `readfile.path`, when provided, SHALL be used only as the initial seed for the prompt;
- `readfile.fileext`, when provided, SHALL restrict the browse dialog to those extensions and SHALL reject any final resolved path that does not match one of the allowed extensions;
- the selected file SHALL still pass the existing read-path validation rules before any captured path or file contents are stored;
- `readfile.path_into`, when provided, SHALL receive the resolved absolute path;
- when `readfile.path_only` is `true`, the step SHALL stop after path validation and SHALL NOT read the file contents;
- when `readfile.path_only` is not `true`, the step SHALL preserve normal line-processing behavior and SHALL also expose the resolved absolute path through `readfile.path_into` when provided or a predictable companion variable derived from `readfile.into` when omitted.

#### Scenario: Manual run selects a file for path-only capture
- **WHEN** a manual script execution runs a `readfile` step with `select_file: true`, `path_only: true`, and `path_into: chosen_path`
- **AND** the operator chooses a file
- **THEN** the runtime stores the resolved absolute path in `chosen_path`
- **AND** the runtime does not read the file contents

#### Scenario: Manual run selects a file to read and capture path
- **WHEN** a manual script execution runs a `readfile` step with `select_file: true` and `into: selected_lines`
- **AND** the operator chooses a file
- **THEN** the runtime reads the selected file
- **AND** stores the processed lines into `selected_lines`
- **AND** stores the resolved absolute path in a predictable companion variable derived from `selected_lines`

#### Scenario: File selection is cancelled
- **WHEN** a `readfile` step with `select_file: true` is prompted during a manual run
- **AND** the operator cancels the prompt
- **THEN** the runtime clears any configured path output and read output variables deterministically
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

#### Scenario: Manual run path-only picker auto-opens the browse dialog by default
- **WHEN** a manual script execution runs a `readfile` step with `select_file: true` and `path_only: true`
- **AND** the step omits `autobrowse`
- **AND** the operator chooses a file from the browse dialog
- **THEN** the runtime uses that chosen file without showing the intermediate custom picker form or requiring an extra confirmation click
- **AND** cancelling the browse dialog cancels the prompt