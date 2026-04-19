## ADDED Requirements

### Requirement: Two-phase script runtime execution
The scripting runtime SHALL support two ordered phases per host: `preconnect` then `steps`.

When a script does not define `preconnect`, the runtime SHALL execute only the `steps` phase.

#### Scenario: Ordered phase execution
- **WHEN** a script defines both `preconnect` and `steps`
- **THEN** `preconnect` executes fully before `steps`
- **AND** variables set in `preconnect` are available to `steps` for that host

### Requirement: Preconnect output and cancellation semantics
Preconnect phase execution SHALL follow existing command result semantics for success, failure, `on_error`, and cancellation.

#### Scenario: Preconnect cancelled by operator
- **WHEN** execution is cancelled during preconnect
- **THEN** host execution ends as cancelled
- **AND** main steps do not start for that host

### Requirement: Sensitive override value handling
The runtime SHALL treat reserved SSH override variables as sensitive and SHALL avoid emitting their raw values in normal script output or debug traces.

#### Scenario: Override variable is set in preconnect
- **WHEN** preconnect sets `_ssh_password` or `_ssh_identity_passphrase`
- **THEN** output/debug streams do not include the raw secret value
