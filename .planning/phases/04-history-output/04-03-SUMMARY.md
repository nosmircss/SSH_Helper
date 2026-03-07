---
phase: 04-history-output
plan: 03
subsystem: testing
tags: [history, testing, xunit, fluent-assertions, temp-directory-isolation]

# Dependency graph
requires:
  - phase: 04-history-output
    provides: JobHistoryService with save, load, prune, query, search, and delete operations
  - phase: 04-history-output
    provides: JobHostOutput, JobRunRecord, JobRunPayload, JobRunFilter models
provides:
  - Comprehensive test coverage for JobHistoryService (26 tests across all 4 HIST requirements)
  - Regression safety for all history persistence, pruning, query, and search behaviors
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns: [age-based retention testing with explicit past timestamps, count-based pruning verification via file system checks]

key-files:
  created:
    - SSH_Helper.Tests/Services/JobHistoryServiceTests.cs
  modified: []

key-decisions:
  - "Age-based pruning tests use explicit CompletedUtc 40 days in past with retentionDays=30 for deterministic behavior"
  - "Dual pruning test combines old entries (40 days) with recent entries to verify both age and count limits apply"
  - "Payload file cleanup verified via Directory.GetFiles count rather than tracking individual file names"

patterns-established:
  - "JobHistoryService test pattern: temp directory with GUID isolation, IDisposable cleanup, CreateTestResult helper"

requirements-completed: [HIST-01, HIST-02, HIST-03, HIST-04]

# Metrics
duration: 3min
completed: 2026-03-07
---

# Phase 04 Plan 03: JobHistoryService Tests Summary

**26 unit tests covering run persistence, output truncation, dual retention pruning, query filtering, case-insensitive search, deletion, and corrupt index recovery**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-07T21:58:45Z
- **Completed:** 2026-03-07T22:02:16Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- Created 26 comprehensive tests for JobHistoryService covering all 4 HIST requirements
- Tests verify run record persistence, payload file creation, output truncation with markers, and null HostOutputs handling
- Tests verify count-based pruning, age-based pruning, dual pruning, and payload file cleanup on disk
- Tests verify query filtering (success/failure, date range, max results), case-insensitive search, deletion, and corrupt index recovery
- Full test suite passes at 1157 tests with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Test run record persistence and output capture (HIST-01, HIST-02)** - `d535b6e` (test)
2. **Task 2: Test pruning, query/filter, search, and deletion (HIST-03, HIST-04)** - `62c0527` (test)

## Files Created/Modified
- `SSH_Helper.Tests/Services/JobHistoryServiceTests.cs` - 26 unit tests for JobHistoryService across all HIST requirements

## Decisions Made
- Age-based pruning tests use explicit CompletedUtc timestamps set 40 days in the past with retentionDays=30, avoiding flaky timing with retentionDays=0 which can prune the newest entry due to UtcNow drift between SaveRun and EnforceRetention
- Dual pruning test creates 3 old entries (40 days) + 2 recent entries, then saves with maxRuns=2 and retentionDays=30 to verify both limits apply in sequence
- Payload file cleanup verification counts all .json files in job directory (index.json + N payloads) rather than tracking individual file names

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed age-based pruning test timing issue**
- **Found during:** Task 2
- **Issue:** Using retentionDays=0 caused the newly saved entry to also be pruned, because DateTime.UtcNow in EnforceRetention runs slightly after CompletedUtc was set
- **Fix:** Changed to use explicit CompletedUtc 40 days in past with retentionDays=30 for deterministic behavior
- **Files modified:** SSH_Helper.Tests/Services/JobHistoryServiceTests.cs
- **Verification:** All 26 tests pass reliably
- **Committed in:** 62c0527 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug in test timing)
**Impact on plan:** Test fix necessary for deterministic test behavior. No scope creep.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All Phase 4 HIST requirements fully tested and verified
- Phase 4 complete: models, service, and tests all delivered
- Ready for Phase 5 (UI integration) with full regression safety

## Self-Check: PASSED

- SSH_Helper.Tests/Services/JobHistoryServiceTests.cs exists (26 tests)
- Both task commits verified (d535b6e, 62c0527)
- All 1157 tests pass (1131 existing + 26 new)

---
*Phase: 04-history-output*
*Completed: 2026-03-07*
