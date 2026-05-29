## ADDED Requirements

### Requirement: Named capture group extraction
The `extract` command SHALL surface named regular-expression capture groups as named variables while preserving existing positional extraction behavior.

#### Scenario: Named groups populate named variables
- **WHEN** an `extract` step uses a pattern containing named groups (for example `(?<iface>\w+)`)
- **THEN** each named group value is exposed as a variable under its group name

#### Scenario: Positional behavior preserved
- **WHEN** an `extract` step uses a pattern with no named groups
- **THEN** extraction behaves byte-for-byte as it did before this change
