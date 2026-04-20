## ADDED Requirements

### Requirement: Folder subtree export
The system SHALL allow an operator to export a selected preset folder and all descendant folders and presets to a JSON file from the preset-folder context menu.

#### Scenario: Export selected folder subtree only
- **WHEN** an operator right-clicks preset folder `Switches` and chooses `Export Folder...`
- **THEN** the system prompts for a JSON save path
- **AND** the exported document includes folder `Switches`, its descendant folders, and presets within that subtree
- **AND** the exported document excludes presets and folders outside the selected subtree

#### Scenario: Export nested folder rebases to bundle root
- **WHEN** an operator exports nested preset folder `Network/Prod`
- **THEN** the exported document represents `Prod` as the bundle root
- **AND** descendant folder `Network/Prod/Core` is exported as `Prod/Core`
- **AND** presets in the subtree keep their relative placement beneath `Prod`

#### Scenario: Exported subtree remains import-compatible
- **WHEN** an operator imports a previously exported folder subtree document into destination folder `Archive`
- **THEN** the selected folder and descendants are recreated under `Archive/<selected-folder-name>`
- **AND** preset-name collision handling continues to follow the existing import rules
