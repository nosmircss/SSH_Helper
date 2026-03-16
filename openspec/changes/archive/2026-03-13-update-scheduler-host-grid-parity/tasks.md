## 1. Column-management parity
- [x] 1.1 Add scheduler Hosts-tab column add, rename, delete, and reorder support with the same protected-column rules as the main grid.

## 2. Editing and clipboard parity
- [x] 2.1 Reuse the main grid's keyboard and clipboard behaviors for select-all, copy, paste, delete, keypress-to-edit, and double-click edit initiation.
- [x] 2.2 Keep scheduler host-count feedback in sync with inline `Host_IP` edits.

## 3. Visual parity
- [x] 3.1 Align scheduler host-grid row sizing, row-header/row-number behavior, themed scroll affordances, and selection styling with the main hosts grid.

## 4. Import and copy parity
- [x] 4.1 Route scheduler CSV import through the shared CSV parsing semantics already used by the main grid.
- [x] 4.2 Make Copy from Main Grid prefer checked rows when any are checked, otherwise copy all eligible rows.

## 5. Verification
- [x] 5.1 Add focused tests for scheduler host-grid parity helpers and CSV/copy semantics.
- [x] 5.2 Run manual verification covering column operations, clipboard workflows, host-count refresh, and visual parity.
- [x] 5.3 Validate change with `openspec validate update-scheduler-host-grid-parity --strict --no-interactive`.
