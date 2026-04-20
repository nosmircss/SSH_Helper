## MODIFIED Requirements
### Requirement: Context-aware autocomplete
When autocomplete is enabled in Command Editor settings, the editor SHALL provide context-aware completion for:
- top-level script keys
- step command keys at step-start positions
- step option keys and enum-like option values
- interpolation placeholders `${...}` and `{{...}}` using one symmetric symbol suggestion set.

Interpolation symbol suggestions SHALL include:
- names discovered from current script text (`vars`, `set`, `capture`, and `into` outputs)
- runtime built-ins (`_prompt`, `_output`, `_outputwindow`, `_timestamp`, `_iteration`, `_last_error`)
- host-grid column placeholders.

The symmetric interpolation behavior SHALL remain the forward-compatible default for new editor iterations.

#### Scenario: Step completion inside steps block
- **WHEN** the caret is at the start of a step item in the `steps` list
- **THEN** completion suggestions include valid step command names

#### Scenario: Variable completion after interpolation start
- **WHEN** the operator types `${`
- **THEN** completion suggestions include script variables, runtime built-ins, and host-grid columns
- **AND** suggestion membership matches the `{{` trigger for the same document/grid state

#### Scenario: Grid placeholder completion
- **WHEN** the operator types `{{`
- **THEN** completion suggestions include script variables, runtime built-ins, and host-grid columns
- **AND** suggestion membership matches the `${` trigger for the same document/grid state

### Requirement: Variable inspector hover contract
When variable inspector tooltips are enabled in Command Editor settings, hover tooltips for variable tokens SHALL resolve values from deterministic sources:
- `${var}`: script/default/runtime symbol maps
- `{{column}}`: selected grid row value, with fallback to first non-new row when no row is selected.

Missing variables or columns SHALL be shown as unresolved in tooltip content.

#### Scenario: Column hover preview from selected row
- **WHEN** the operator hovers `{{column}}` and a host row is selected
- **THEN** tooltip preview uses that selected row value

#### Scenario: Runtime built-in hover preview
- **WHEN** the operator hovers `${_outputwindow}` in the editor without an active execution relay
- **THEN** the tooltip shows a deterministic built-in preview value instead of unresolved content
