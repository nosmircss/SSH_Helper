## ADDED Requirements

### Requirement: Environment dialog layout persistence
The system SHALL persist Manage Environments dialog layout changes so user-adjusted size and split layout are restored on subsequent openings.

#### Scenario: Reopen dialog after resize
- **WHEN** an operator resizes the Manage Environments dialog and closes it
- **THEN** reopening the dialog restores the prior width and height

#### Scenario: Reopen dialog after splitter adjustment
- **WHEN** an operator adjusts the Manage Environments left-panel splitter and closes the dialog
- **THEN** reopening the dialog restores the prior splitter distance
