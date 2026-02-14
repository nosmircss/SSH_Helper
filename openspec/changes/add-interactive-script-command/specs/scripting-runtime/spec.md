## ADDED Requirements
### Requirement: Interactive terminal step execution
The scripting runtime SHALL support an `interactive` step that opens an in-app terminal against the current SSH host and blocks script execution until the terminal window closes.

The step SHALL support:
- `session: separate|shared` (default `separate`)
- `emulation: full` (default `full`)

In `emulation: full`, the terminal SHALL render screen updates with terminal palette colors (foreground/background).

`session: separate` SHALL create a new SSH terminal connection using the current host execution context.

`session: shared` SHALL attach to the current script SSH shell session. The runtime SHALL NOT silently fall back to `separate` when shared attachment is unavailable.

Closing the interactive terminal window by the user SHALL be treated as successful step completion and script execution SHALL continue to the next step.

#### Scenario: User closes interactive terminal and script continues
- **WHEN** a script executes an `interactive` step and the operator closes the terminal window
- **THEN** the step is marked successful
- **AND** the next script step executes

#### Scenario: Shared session is unavailable
- **WHEN** a script executes `interactive` with `session: shared` and no shared shell session can be attached
- **THEN** the step fails with an explicit `InteractiveSharedUnavailable` error
- **AND** existing `on_error` step behavior is applied

#### Scenario: Script cancellation while interactive is open
- **WHEN** execution is canceled while an `interactive` window is open
- **THEN** the interactive window and backing terminal session are force-closed
- **AND** script execution ends with cancellation status
