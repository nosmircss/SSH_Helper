## MODIFIED Requirements
### Requirement: Dynamic built-in variables
Built-in runtime variables SHALL be resolved dynamically at substitution time.

- `${_timestamp}` SHALL resolve to the current time at substitution time.
- `${_prompt}` SHALL resolve to the current detected SSH shell prompt when an SSH shell session is active.
- `${_prompt}` SHALL resolve to an empty string when no SSH shell prompt is available.

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
