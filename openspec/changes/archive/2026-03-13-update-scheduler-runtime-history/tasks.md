## 1. Missed-run lifecycle
- [x] 1.1 Persist the scheduler shutdown anchor and detect recurring runs missed during downtime on startup.
- [x] 1.2 Record skipped scheduler events/history entries for missed recurring runs without auto-executing them.

## 2. History policy enforcement
- [x] 2.1 Apply per-job retention overrides when saving scheduler history.
- [x] 2.2 Fall back to configured global scheduler history defaults and output caps when no per-job override exists.

## 3. History presentation
- [x] 3.1 Correct scheduler history rows to show actual run start time and duration from persisted timestamps.

## 4. Verification
- [x] 4.1 Add focused tests for missed-run recording and history retention policy selection.
- [x] 4.2 Add focused verification for scheduler history timestamp display.
- [x] 4.3 Validate change with `openspec validate update-scheduler-runtime-history --strict --no-interactive`.
