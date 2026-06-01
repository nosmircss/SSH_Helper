## ADDED Requirements

### Requirement: Loop variable scoping
`foreach` and `while` loops SHALL scope their iteration variables to the loop body and restore prior values on exit.

#### Scenario: Iterator does not clobber an outer variable
- **WHEN** a `foreach` loop uses an iterator name that matches an existing outer variable
- **THEN** the outer variable's value is restored after the loop completes

#### Scenario: Scope restored on early exit
- **WHEN** a loop exits early via `break` or `return`
- **THEN** loop-scoped variables are still restored to their prior state

### Requirement: Loop iteration metadata variables
`foreach` loops SHALL expose iteration metadata as flat scalar variables prefixed with the iterator name, resolvable by the existing variable resolver.

#### Scenario: Metadata scalars available in the body
- **WHEN** a `foreach` loop iterates with item variable `host`
- **THEN** `${host_index}` (zero-based), `${host_number}` (one-based), `${host_first}`, `${host_last}`, and `${host_count}` are available within the body

#### Scenario: Metadata reflects position
- **WHEN** the loop is on its first iteration
- **THEN** `${host_first}` is true and `${host_last}` is true only on the final iteration

### Requirement: Dictionary iteration in foreach
`foreach` SHALL support iterating the entries of an object/map value using a two-variable form.

#### Scenario: Iterate key and value pairs
- **WHEN** a script authors `foreach: k, v in {{map}}` and `{{map}}` resolves to a JSON object
- **THEN** each iteration sets `k` to the entry key and `v` to the entry value

#### Scenario: Single-variable form unchanged
- **WHEN** a script authors `foreach: item in {{collection}}`
- **THEN** iteration behaves exactly as before this change
