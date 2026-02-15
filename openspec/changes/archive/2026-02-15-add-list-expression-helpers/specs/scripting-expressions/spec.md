## ADDED Requirements

### Requirement: List prepend and removal helpers
Set expressions SHALL support prepend and removal operations for list variables without requiring JSON helper functions.

#### Scenario: prepend value to list with unshift
- **WHEN** a script evaluates `unshift(interface_list, "any")` in a set expression
- **THEN** the resulting list places `"any"` at index `0`

#### Scenario: destructive pop and shift on writable list variable
- **WHEN** a script evaluates `pop(interface_list)` or `shift(interface_list)` where `interface_list` is a writable top-level list variable
- **THEN** the function returns the removed value
- **AND** the source list variable is updated to reflect the removal

### Requirement: List query and combination helpers
Set expressions SHALL support helper functions for reading and combining list values.

#### Scenario: first and last are non-destructive
- **WHEN** a script evaluates `first(interface_list)` or `last(interface_list)`
- **THEN** the function returns the corresponding element
- **AND** the source list remains unchanged

#### Scenario: index and concat operations
- **WHEN** a script evaluates `indexof(interface_list, "wan1")` and `concat(list_a, list_b)`
- **THEN** `indexof` returns the zero-based index or `-1`
- **AND** `concat` returns a combined list preserving input order
