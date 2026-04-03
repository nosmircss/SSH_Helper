## ADDED Requirements

### Requirement: Local audio playback command
The scripting runtime SHALL support a `playsound` command that plays a local audio file.

The command SHALL resolve the playback path using script substitution and Windows environment-variable expansion before playback.

Supported options:
- `path` (required): input file path expression.
- `wait` (optional): whether to block step completion until playback finishes, default `true`.
- `volume` (optional): playback volume percentage from `0` to `100`, default `100`.
- `max_seconds` (optional): positive playback timeout when `wait=true`.
- `into` (optional): output variable name for success status and metadata.
- `on_error` (optional): `stop` (default) or `continue`.

#### Scenario: Wait mode blocks until playback completes
- **WHEN** a `playsound` step resolves to an existing supported file
- **AND** `wait` is omitted or `true`
- **THEN** the step completes only after playback completion
- **AND** execution proceeds to the next step

#### Scenario: Background mode returns immediately
- **WHEN** a `playsound` step sets `wait: false`
- **THEN** playback is started
- **AND** the step completes without waiting for playback to finish

#### Scenario: Missing file follows on_error behavior
- **WHEN** a `playsound` step resolves to a non-existent file
- **THEN** the step is treated as a failure condition
- **AND** runtime applies standard `on_error` behavior

### Requirement: Playsound metadata output contract
When `into` is provided, the runtime SHALL publish command outputs for `playsound`.

The runtime SHALL set:
- `${into}` to a boolean success value
- `${into}_meta` to an object including:
  - `path` (resolved playback path)
  - `wait` (effective wait mode)
  - `volume` (effective volume)
  - `backend` (playback backend identifier)
  - `error` (string, only when an operational error occurs)

#### Scenario: Into outputs are published on success
- **WHEN** a `playsound` step succeeds with `into` configured
- **THEN** `${into}` is set to `true`
- **AND** `${into}_meta.path` contains the resolved path

#### Scenario: Into outputs are published on suppressed error
- **WHEN** playback fails and `on_error: continue` is configured with `into`
- **THEN** `${into}` is set to `false`
- **AND** `${into}_meta.error` contains the error summary
- **AND** execution continues
