## ADDED Requirements
### Requirement: Readfile picker-aware validation
Script validation SHALL treat `readfile.path` as conditionally required based on `readfile.select_file`, and SHALL accept picker-specific customization keys on `readfile` steps.

#### Scenario: Standard readfile still requires path
- **WHEN** a `readfile` step omits `path`
- **AND** `select_file` is not `true`
- **THEN** validation reports that `readfile` requires `path`

#### Scenario: Picker mode allows omitted path
- **WHEN** a `readfile` step sets `select_file: true`
- **AND** omits `path`
- **THEN** validation accepts the step if `into` is present

#### Scenario: Picker customization keys are accepted
- **WHEN** a `readfile` step includes `message` and `fileext`
- **THEN** validation accepts those keys as part of the supported `readfile` contract
