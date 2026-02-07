## ADDED Requirements

### Requirement: Non-fatal parser warnings
Script validation SHALL report unknown YAML keys as warnings with line context while preserving parse success.

#### Scenario: unknown step key warning
- **WHEN** a script includes an unrecognized key in a step mapping
- **THEN** validation reports a warning containing the key name and line number
- **AND** the script remains parseable for execution

### Requirement: YAML detection guidance alignment
Operator-facing scripting documentation SHALL match runtime YAML detection behavior.

#### Scenario: metadata-only command text
- **WHEN** command text contains metadata words like `name:` or `description:` without strong script indicators
- **THEN** documentation states that metadata words alone do not guarantee YAML script detection