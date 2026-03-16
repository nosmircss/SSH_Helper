## ADDED Requirements
### Requirement: Subroutine syntax editor vocabulary
The script editor autocomplete and syntax-highlighting vocabulary SHALL include the reusable-script syntax surface.

The editor SHALL surface:
- top-level keys: `imports`, `subroutines`, `library`
- step commands: `call`, `return`
- `call` option keys: `subroutine`, `args`, `out`, `on_error`

#### Scenario: Completion suggests call command and fields
- **WHEN** the caret is at a step-command completion position
- **THEN** autocomplete suggestions include `call` and `return`
- **AND** inside a `call` map the editor suggests `subroutine`, `args`, `out`, and `on_error`

#### Scenario: Syntax highlighter recognizes reusable-script keys
- **WHEN** a script contains `imports`, `subroutines`, `library`, `call`, or `return`
- **THEN** the editor highlights those recognized keys using the existing parser-driven vocabulary flow
