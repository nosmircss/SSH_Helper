## ADDED Requirements

### Requirement: Unified interpolation scanner
Variable interpolation SHALL use a single balanced-brace scanner for both `{{ }}` and `${ }` forms, with `{{ }}` as the canonical form and `${ }` as a supported alias.

#### Scenario: Consistent nested and adjacent handling
- **WHEN** a value contains nested or adjacent interpolations in either `{{ }}` or `${ }` form
- **THEN** both forms are scanned with identical balanced-brace rules (there is no escape syntax; backslashes pass through literally in both forms)

#### Scenario: Canonical and alias forms are equivalent
- **WHEN** the same expression is written as `{{ expr }}` or as `${ expr }`
- **THEN** both forms resolve to the same value
