## ADDED Requirements

### Requirement: Local path existence command
The scripting runtime SHALL support an `exists` command that checks whether a local path exists.

The command SHALL resolve paths using script substitution and Windows environment-variable expansion before evaluation.

Supported options:
- `path` (required): input path expression.
- `into` (required): output variable name for boolean result.
- `type` (optional): `any` (default), `file`, or `directory`.
- `on_error` (optional): `stop` (default) or `continue`.

#### Scenario: Existing file returns true
- **WHEN** an `exists` step targets a path that resolves to an existing file
- **AND** the step type is `any` or `file`
- **THEN** `${into}` is set to `true`
- **AND** `${into}_meta.exists` is `true`
- **AND** `${into}_meta.is_file` is `true`

#### Scenario: Existing directory returns true
- **WHEN** an `exists` step targets a path that resolves to an existing directory
- **AND** the step type is `any` or `directory`
- **THEN** `${into}` is set to `true`
- **AND** `${into}_meta.exists` is `true`
- **AND** `${into}_meta.is_directory` is `true`

#### Scenario: Missing path returns false without failing
- **WHEN** an `exists` step targets a path that does not exist
- **THEN** `${into}` is set to `false`
- **AND** `${into}_meta.exists` is `false`
- **AND** the step completes successfully

### Requirement: Exists metadata output contract
The runtime SHALL publish metadata to `${into}_meta` for every `exists` step completion.

The metadata object SHALL include:
- `exists` (boolean)
- `is_file` (boolean)
- `is_directory` (boolean)
- `path` (resolved path string)
- `type` (effective type mode)
- `error` (string, only when an operational error is suppressed)

#### Scenario: Metadata includes resolved path
- **WHEN** an `exists` step runs with path variables
- **THEN** `${into}_meta.path` stores the fully resolved path used by the check

### Requirement: Exists error handling behavior
The runtime SHALL apply standard `on_error` behavior for operational errors in `exists`.

Operational errors include invalid path normalization failures and I/O exceptions raised while checking existence.

#### Scenario: Suppressed operational error preserves outputs
- **WHEN** an `exists` step encounters an operational error
- **AND** `on_error: continue` is configured
- **THEN** execution continues
- **AND** `${into}` is set to `false`
- **AND** `${into}_meta.error` contains the error summary
- **AND** `_last_error` is set according to suppressed-error behavior
