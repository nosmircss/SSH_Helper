## MODIFIED Requirements

### Requirement: YAML detection guidance alignment
Operator-facing scripting documentation SHALL match runtime YAML detection behavior.

#### Scenario: metadata-only command text
- **WHEN** command text contains metadata words like `name:`, `description:`, or `environment:` without strong script indicators
- **THEN** documentation states that metadata words alone do not guarantee YAML script detection
