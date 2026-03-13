## ADDED Requirements
### Requirement: Collection-aware conditional membership
The scripting runtime SHALL support collection membership checks in conditions.

#### Scenario: Case-insensitive list membership
- **WHEN** a script evaluates `svc_key in exclude_service_matches_norm`
- **THEN** the runtime treats the right-hand side as a collection
- **AND** membership comparison is case-insensitive by default

#### Scenario: Negated collection membership
- **WHEN** a script evaluates `svc_key not in exclude_service_matches_norm`
- **THEN** the runtime returns true when the value is absent from the collection

### Requirement: Expression-backed foreach collections
The scripting runtime SHALL let `foreach` iterate any resolved collection expression, not only a named variable lookup.

#### Scenario: Foreach over split expression
- **WHEN** a script uses `foreach` with `iterator: item in split(csv_ports, ",")`
- **THEN** the loop iterates the resolved collection items in order

#### Scenario: Foreach over json.items expression
- **WHEN** a script uses `foreach` with `iterator: entry in json.items(response, "data.tags")`
- **THEN** the loop iterates the resolved JSON-derived items without requiring a temporary variable

### Requirement: Structural collection semantics
The scripting runtime SHALL treat lists and JSON containers as structural collections for length, emptiness, and truthiness checks.

#### Scenario: JSON array length uses element count
- **WHEN** a script evaluates `length(json.items(response, "check"))` or `${check_items.length}`
- **THEN** the result reflects the number of elements rather than the raw JSON string length

#### Scenario: Empty JSON collection is empty
- **WHEN** a script evaluates `items is empty` where `items` is an empty JSON array or object
- **THEN** the runtime treats the collection as empty
