## ADDED Requirements

### Requirement: Unique history identifiers
History entries SHALL include a stable unique identifier that is independent of the display label.

#### Scenario: Two runs in the same second
- **WHEN** two executions complete within the same second
- **THEN** each history entry has a distinct identifier and associated per-host data is stored against that identifier

### Requirement: History deletion cleanup
Deleting a history entry or clearing history SHALL remove any associated per-host results.

#### Scenario: Delete history entry
- **WHEN** the user deletes a history entry
- **THEN** any stored per-host results for that entry are removed and cannot be displayed
