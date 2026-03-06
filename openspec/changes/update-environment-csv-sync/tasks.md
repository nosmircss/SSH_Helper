# Tasks: Add environment CSV freshness detection

## 1. Persistence
- [x] 1.1 Extend environment and saved application state with persisted CSV fingerprint metadata
- [x] 1.2 Preserve and normalize the fingerprint metadata through environment save/load/import flows

## 2. Environment switching behavior
- [x] 2.1 Evaluate a target environment's remembered CSV reference before loading it into the grid
- [x] 2.2 Prompt to reload hosts from disk when the backing CSV changed since the environment snapshot was captured
- [x] 2.3 Keep the saved snapshot active but visibly marked when the operator declines reload or the file is missing

## 3. UI feedback
- [x] 3.1 Extend the hosts header indicator to show disk-changed and missing-file states
- [x] 3.2 Report reload and stale-snapshot outcomes through status-bar messages

## 4. Verification
- [x] 4.1 Add focused regression tests for fingerprint persistence and stale-file evaluation
- [x] 4.2 Add focused regression tests for hosts-file indicator text
- [x] 4.3 Run `dotnet test` for the touched suites
- [x] 4.4 Run `dotnet build SSH_Helper.csproj`
- [x] 4.5 Run `openspec validate update-environment-csv-sync --strict --no-interactive`
