## ADDED Requirements

### Requirement: Scheduler host-grid column parity
The scheduler Hosts tab SHALL support adding, renaming, deleting, and reordering host columns using the same protected-column rules as the main host grid.

#### Scenario: Add manual credential columns to a new job
- **WHEN** an operator creates a new scheduler job and adds `username` and `password` columns in the Hosts tab
- **THEN** the new columns appear in the scheduler grid without requiring import or copy-from-main first

#### Scenario: Protected host column cannot be deleted
- **WHEN** an operator attempts to delete the `Host_IP` column from the scheduler Hosts tab
- **THEN** the action is rejected
- **AND** the scheduler grid keeps the protected host column intact

### Requirement: Scheduler host-grid editing parity
The scheduler Hosts tab SHALL support the same keyboard and clipboard editing workflow as the main host grid for selection, copy, paste, delete, keypress-to-edit, and double-click edit initiation.

#### Scenario: Paste a host matrix into the scheduler grid
- **WHEN** an operator pastes tabular host data into the scheduler Hosts tab
- **THEN** the scheduler grid expands rows and columns as needed
- **AND** the pasted values populate the corresponding cells

#### Scenario: Clear selected scheduler cells with the keyboard
- **WHEN** an operator selects scheduler host cells and presses `Delete` or `Backspace`
- **THEN** the selected cell values are cleared using the same semantics as the main host grid

### Requirement: Scheduler host-grid visual parity
The scheduler Hosts tab SHALL present host rows with the same operator-facing visual cues as the main host grid, including row sizing, row-header/row-number affordances, themed scrolling treatment, and selection styling appropriate to the scheduler grid's controls.

#### Scenario: Scheduler host grid matches main-grid row presentation
- **WHEN** an operator opens the scheduler Hosts tab after using the main hosts grid
- **THEN** row height, row-header presentation, and overall grid chrome are visually consistent with the main hosts grid

#### Scenario: Scheduler host grid respects dark and light theme styling
- **WHEN** the application theme changes between dark and light modes
- **THEN** the scheduler Hosts tab updates its scrolling, selection, and grid styling to match the themed main hosts grid presentation

### Requirement: Scheduler host import and copy parity
The scheduler Hosts tab SHALL use the shared CSV import semantics already accepted by the main host grid and SHALL copy checked rows from the main grid when any are checked, otherwise copy all eligible host rows.

#### Scenario: Import a CSV accepted by the main grid
- **WHEN** an operator imports a host CSV file that the main host grid accepts
- **THEN** the scheduler Hosts tab parses the same headers and row values
- **AND** the resulting scheduler grid matches the imported host data

#### Scenario: Copy from main grid prefers checked rows
- **WHEN** the main host grid contains checked rows and the operator clicks Copy from Main Grid in the scheduler editor
- **THEN** only the checked rows are copied into the scheduler Hosts tab

### Requirement: Scheduler host-count freshness
The scheduler Hosts tab SHALL refresh its host-count label whenever inline edits change whether a row has a non-empty `Host_IP`.

#### Scenario: Clearing a host address decrements the count
- **WHEN** an operator clears the `Host_IP` value from an existing scheduler host row
- **THEN** the displayed scheduler host count updates immediately to exclude that row
