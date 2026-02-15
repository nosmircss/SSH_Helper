# Change: Refactor history persistence to external lazy-loaded run files

## Why
Execution history currently lives inside `SavedState` in `config.json`, is loaded fully into memory at startup, and can be trimmed/cropped by memory guards. This blocks full-fidelity run review and causes avoidable memory pressure.

## What Changes
- Persist history metadata in `history.index.json` and full run payloads in `history/<run-id>.json` alongside `config.json`.
- Load only history metadata at startup; lazy-load run payloads when a run is selected/exported/viewed.
- Keep full run payload content without persistence-time trimming/cropping.
- Migrate legacy `SavedState.History` entries into the new store once when no external history index exists.
- Keep `ApplicationState.History` as legacy migration data only (not primary persistence path).

## Impact
- Affected specs:
  - `execution-history`
- Affected code:
  - `Form1.cs`
  - `Models/HistoryListItem.cs`
  - `Models/HistoryIndex.cs` (new)
  - `Models/HistoryRunPayload.cs` (new)
  - `Models/AppConfiguration.cs`
  - `Services/HistoryStorageService.cs` (new)
  - `Services/ConfigurationService.cs`
  - `README.md`
  - `SSH_Helper.Tests/Services/HistoryStorageServiceTests.cs` (new)
