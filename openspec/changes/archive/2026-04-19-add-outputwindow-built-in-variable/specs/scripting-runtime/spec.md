## MODIFIED Requirements
### Requirement: Dynamic built-in variables
Built-in runtime variables SHALL be resolved dynamically at substitution time.

- `${_timestamp}` SHALL resolve to the current time at substitution time.
- `${_prompt}` SHALL resolve to the current detected SSH shell prompt when an SSH shell session is active.
- `${_prompt}` SHALL resolve to an empty string when no SSH shell prompt is available.
- `${_outputwindow}` SHALL resolve to the current host's pane-formatted transcript accumulated so far for the active run.
- `${_outputwindow}` SHALL use the same per-host formatted text that is appended to the operator-facing output pane, including script headers, separators, warnings, debug lines, mirrored interactive output, and command output.
- `${_outputwindow}` SHALL resolve to an empty string when no live per-host output relay is attached.

#### Scenario: timestamp changes during long script
- **WHEN** `${_timestamp}` is substituted in two different steps at different times
- **THEN** the values reflect current execution time at each substitution point

#### Scenario: prompt tracks current SSH shell prompt
- **WHEN** `${_prompt}` is substituted during SSH-backed execution
- **AND** the detected shell prompt changes during that session
- **THEN** the substituted value reflects the current detected prompt at each substitution point

#### Scenario: prompt is unavailable without SSH session
- **WHEN** `${_prompt}` is substituted before an SSH shell prompt is available
- **THEN** the substituted value is an empty string

#### Scenario: outputwindow uses per-host pane transcript
- **WHEN** a script step substitutes `${_outputwindow}` during a multi-host run
- **THEN** the substituted value contains only the current host's pane-formatted transcript accumulated so far
- **AND** it excludes output from other hosts in the same run

#### Scenario: outputwindow is unavailable without live relay
- **WHEN** `${_outputwindow}` is substituted outside a live execution relay
- **THEN** the substituted value is an empty string
