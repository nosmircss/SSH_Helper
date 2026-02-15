## ADDED Requirements
### Requirement: Interactive capture mode for long-running commands
The scripting runtime SHALL support `interactive.command` capture mode for long-running commands that are operator-stopped with `Ctrl+C`.

Capture mode contract:
- `interactive.command` SHALL auto-run once terminal startup is complete.
- Capture mode SHALL be supported only for `interactive.session=separate`.
- Step completion triggers SHALL include user `Ctrl+C`, timeout auto-interrupt (`max_seconds`), natural command completion, and early window close.
- On `Ctrl+C`, timeout, or natural completion, script execution SHALL continue while the terminal window remains open in detached read-only mode.
- Early window close before those triggers SHALL succeed with partial transcript.
- If `interactive.capture` is configured and the step succeeds, the transcript SHALL be stored into that variable and `_output`.
- `interactive.mirror_output` SHALL control whether capture chunks are mirrored into live script command output; default is disabled.

#### Scenario: Ctrl+C completes capture and script continues
- **WHEN** a script runs `interactive` with `command` and the operator presses `Ctrl+C`
- **THEN** the step completes successfully
- **AND** script execution continues to the next step
- **AND** the interactive window remains open in detached read-only mode

#### Scenario: Timeout auto-interrupt completes capture
- **WHEN** `interactive.max_seconds` is configured and elapses during capture mode
- **THEN** the runtime auto-sends `Ctrl+C`
- **AND** the step completes successfully
- **AND** script execution continues

#### Scenario: Early close keeps partial transcript
- **WHEN** the operator closes the interactive capture window before Ctrl+C/timeout/natural completion
- **THEN** the step is treated as successful partial completion
- **AND** captured transcript up to close time is retained

#### Scenario: Capture variable assignment is opt-in
- **WHEN** capture mode succeeds without `interactive.capture`
- **THEN** no user-named capture variable is written
- **AND** runtime completion remains successful
