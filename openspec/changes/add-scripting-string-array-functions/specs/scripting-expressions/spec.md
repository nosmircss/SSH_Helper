## ADDED Requirements

### Requirement: String transformation functions
Set expressions SHALL support common string transformation functions.

#### Scenario: replace and substring operations
- **WHEN** a script evaluates `replace()` or `substring()` in a set expression
- **THEN** the resulting variable contains the transformed string

#### Scenario: split and join operations
- **WHEN** a script evaluates `split()` and `join()` in set expressions
- **THEN** values are converted between string and list forms predictably

### Requirement: Array sorting function
Set expressions SHALL support sorting list-like values.

#### Scenario: ascending sort by default
- **WHEN** a script evaluates `sort(values)`
- **THEN** the resulting list is sorted in ascending lexical order

#### Scenario: descending sort when requested
- **WHEN** a script evaluates `sort(values, "desc")`
- **THEN** the resulting list is sorted in descending lexical order