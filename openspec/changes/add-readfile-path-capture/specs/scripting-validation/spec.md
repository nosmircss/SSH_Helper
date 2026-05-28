## MODIFIED Requirements
### Requirement: Readfile picker-aware validation
Script validation SHALL treat `readfile.path` as conditionally required based on `readfile.select_file`, and SHALL accept picker-specific customization keys on `readfile` steps.

Validation rules for `readfile`:
- `readfile.path` is required unless `select_file: true`.
- `readfile.into` is required when `path_only` is not `true`.
- `readfile.path_into` is required when `path_only: true`.
- allowed `readfile` keys include `path`, `select_file`, `autobrowse`, `message`, `fileext`, `into`, `path_into`, `path_only`, `skip_empty_lines`, `trim_lines`, `max_lines`, `encoding`, and `on_error`.

#### Scenario: Standard readfile still requires path and into
- **WHEN** a `readfile` step omits `path`
- **AND** `select_file` is not `true`
- **THEN** validation reports that `readfile` requires `path`
- **AND** validation still requires `into` when `path_only` is not `true`

#### Scenario: Picker mode allows omitted path
- **WHEN** a `readfile` step sets `select_file: true`
- **AND** omits `path`
- **THEN** validation accepts the step if the required output key for the chosen mode is present

#### Scenario: Path-only mode requires path target
- **WHEN** a `readfile` step sets `path_only: true`
- **AND** omits `path_into`
- **THEN** validation reports that `readfile` requires `path_into` for path-only mode

#### Scenario: Picker customization and path-capture keys are accepted
- **WHEN** a `readfile` step includes `autobrowse`, `message`, `fileext`, `path_into`, and `path_only`
- **THEN** validation accepts those keys as part of the supported `readfile` contract