## RENAMED Requirements
- FROM: `### Requirement: Non-fatal parser warnings`
- TO: `### Requirement: Unknown-key diagnostic severity`

## MODIFIED Requirements

### Requirement: Unknown-key diagnostic severity
Script parsing and validation SHALL classify unrecognized keys by severity: clearly-unrecognized (typo-class) keys are reported as errors that block execution, while recognized deprecation notices are reported as non-fatal warnings with line context.

#### Scenario: Unknown step key is a blocking error
- **WHEN** a script includes an unrecognized (typo-class) key in a step mapping
- **THEN** validation reports an error containing the key name and line number
- **AND** script execution does not proceed with that step

#### Scenario: Recognized deprecation remains a warning
- **WHEN** a script uses a recognized deprecated key (for example `interactive.columns`, `interactive.rows`, or `interactive.emulation`)
- **THEN** validation reports a non-fatal warning with line context
- **AND** the script remains parseable for execution

## ADDED Requirements

### Requirement: Did-you-mean suggestions for unknown keys and commands
Parser and validation diagnostics SHALL append a closest-match suggestion to unknown-key and unknown-command messages when a sufficiently close known candidate exists.

#### Scenario: Suggest closest known key
- **WHEN** a step contains an unrecognized key that closely matches a known key (for example `tieout` vs `timeout`)
- **THEN** the diagnostic message appends `Did you mean 'timeout'?`
- **AND** the offending token remains at the start of the message for editor squiggle positioning

#### Scenario: Suggest closest known command
- **WHEN** a step uses an unrecognized command that closely matches a known command (for example `snd` vs `send`)
- **THEN** the diagnostic message appends `Did you mean 'send'?`

#### Scenario: No suggestion for distant or ambiguous tokens
- **WHEN** the unrecognized token has no sufficiently close known candidate, or is a short/ambiguous key such as `mode`, `host`, or `port`
- **THEN** no suggestion is appended

### Requirement: Parse-time grammar validation for shorthand forms
Validation SHALL reject malformed `set`, `foreach`, and `exit` shorthand forms at parse time rather than deferring failure to runtime.

#### Scenario: Malformed foreach shorthand rejected at parse time
- **WHEN** a script authors a `foreach` shorthand that does not match the `item in collection` grammar
- **THEN** validation reports a grammar error with line context before execution

#### Scenario: Malformed set shorthand rejected at parse time
- **WHEN** a script authors a `set` shorthand that omits a target name or expression
- **THEN** validation reports a grammar error before execution

#### Scenario: Well-formed shorthand still accepted
- **WHEN** a script authors a well-formed `set`, `foreach`, or `exit` shorthand
- **THEN** validation accepts it with unchanged runtime semantics
