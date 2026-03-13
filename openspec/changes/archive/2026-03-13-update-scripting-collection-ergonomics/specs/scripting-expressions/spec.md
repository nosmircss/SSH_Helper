## ADDED Requirements
### Requirement: Collection construction helper
Set expressions SHALL support concise list construction without repeated push steps.

#### Scenario: Create list from expression arguments
- **WHEN** a script evaluates `list("Cloudflare-CDN", "Amazon-AWS", "Cloudflare-Web")`
- **THEN** the resulting variable contains a native ordered list of those values

### Requirement: Collection normalization and dedupe helpers
Set expressions SHALL support collection cleanup helpers for common normalization flows.

#### Scenario: Compact removes empty items
- **WHEN** a script evaluates `compact(values)`
- **THEN** empty and whitespace-only items are removed from the resulting list

#### Scenario: Case-insensitive distinct by default
- **WHEN** a script evaluates `distinct(values)`
- **THEN** duplicate items are removed using case-insensitive comparison while preserving first-seen order

#### Scenario: Push unique preserves existing entry
- **WHEN** a script evaluates `push_unique(values, candidate)`
- **THEN** the candidate is appended only when it is not already present under the selected comparison mode

### Requirement: Collection-wide case and trim transforms
Set expressions SHALL support list-wide normalization transforms.

#### Scenario: Normalize every list item
- **WHEN** a script evaluates `trim_all(values)`, `lower_all(values)`, or `upper_all(values)`
- **THEN** the resulting list applies the transformation to each item in order
