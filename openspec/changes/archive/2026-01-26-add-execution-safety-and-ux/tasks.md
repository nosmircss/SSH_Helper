## 1. History identity and cleanup
- [x] 1.1 Add HistoryEntryId to models/state and migrate legacy history on load
- [x] 1.2 Update history storage to use IDs internally and clean per-host results on delete/clear
- [x] 1.3 Add tests for history collisions and cleanup

## 2. Execution cancellation lifecycle
- [x] 2.1 Add per-run CTS setup/teardown helper in SshExecutionService
- [x] 2.2 Ensure all execution paths dispose CTS and reset state

## 3. Stop-on-first-error reliability
- [x] 3.1 Make error flag thread-safe in folder execution and cancel on first error
- [x] 3.2 Add tests covering Stop-on-first-error with parallel presets

## 4. SSH pooling parity and disposal
- [x] 4.1 Use UTF-8 terminal options in pooled sessions
- [x] 4.2 Dispose scripting resources from pool health checks
- [x] 4.3 Add tests for pooled/non-pooled encoding parity (where feasible)

## 5. Hostname validation
- [x] 5.1 Add IsValidHostOrIp to InputValidator and update UI validation paths
- [x] 5.2 Update HostConnection parsing/validation to accept hostnames with optional port
- [x] 5.3 Add tests for hostname and IP validation

## 6. Credential provider + SSH agent
- [x] 6.1 Add credential provider abstraction and Windows Credential Manager implementation
- [x] 6.2 Add config/UI settings to enable credential manager storage
- [x] 6.3 Add SSH agent preference setting and best-effort agent detection/fallback
- [x] 6.4 Add tests for credential provider behavior (mocked)

## 7. Output UX improvements
- [x] 7.1 Remove output toolbar controls (copy, auto-scroll)
- [x] 7.2 Always auto-scroll output without a user toggle

## 8. Script validation
- [x] 8.1 Add "Validate Script" action using ScriptParser.Validate
- [x] 8.2 Display validation results in output and dialog
- [x] 8.3 Add tests for validation output formatting
