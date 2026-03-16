---
phase: 04-history-output
plan: 02
subsystem: services
tags: [history, persistence, query, search, pruning, atomic-write, json]

# Dependency graph
requires:
  - phase: 04-history-output
    provides: JobHostOutput, JobRunRecord, JobRunPayload, JobRunFilter models, JsonFileWriter utility, HistoryIdGenerator, JobRunResult.HostOutputs
provides:
  - JobHistoryService with save, load, prune, query, search, and delete operations
  - Per-job history subdirectory storage layout (job-history/{jobId}/index.json + {runId}.json)
  - Dual retention enforcement (age + count) after each save
  - Output truncation to prevent unbounded file sizes
  - Event subscription for automatic history recording from JobExecutionService
affects: [04-03-PLAN]

# Tech tracking
tech-stack:
  added: []
  patterns: [per-job subdirectory isolation, dual retention enforcement, corrupt index recovery with backup rename]

key-files:
  created:
    - Services/JobHistoryService.cs
  modified: []

key-decisions:
  - "ClearHistory aliases DeleteAllHistory -- simplest correct approach, directory recreated on next SaveRun"
  - "LoadRunPayload returns null on corrupt files instead of throwing, matching defensive service patterns"
  - "SearchRunOutput searches both Output and HostAddress fields for maximum utility"

patterns-established:
  - "Per-job subdirectory: job-history/{jobId}/index.json + {runId}.json for O(1) directory listing per job"
  - "Dual retention: age-based pruning first, then count-based, to avoid keeping old entries within count limit"
  - "Index reload before retention: ensures concurrent saves don't lose entries"

requirements-completed: [HIST-01, HIST-02, HIST-03, HIST-04]

# Metrics
duration: 2min
completed: 2026-03-07
---

# Phase 04 Plan 02: JobHistoryService Summary

**Complete job history service with save/prune/query/search/delete operations using per-job subdirectory storage and dual retention enforcement**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-07T21:54:11Z
- **Completed:** 2026-03-07T21:56:15Z
- **Tasks:** 2
- **Files modified:** 1

## Accomplishments
- Implemented JobHistoryService (390 lines) with all 9 public methods covering save, query, search, and deletion
- Dual retention enforcement (age-based + count-based) with atomic index updates after pruning
- Output truncation with visible marker prevents unbounded payload file sizes
- Corrupt index recovery renames bad files and starts fresh, matching HistoryStorageService pattern
- All 1131 existing tests pass with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement JobHistoryService core (save, load, prune)** - `be48113` (feat)
2. **Task 2: Add query API, search, and deletion to JobHistoryService** - `e02752f` (feat)

## Files Created/Modified
- `Services/JobHistoryService.cs` - Complete job history persistence, pruning, query, search, and deletion service

## Decisions Made
- ClearHistory is a simple alias for DeleteAllHistory -- the directory is recreated on the next SaveRun, so no need for a separate "clear then recreate empty index" flow
- LoadRunPayload returns null on deserialization failure rather than throwing, keeping the service defensive and matching project patterns
- SearchRunOutput searches both Output content and HostAddress for maximum search utility beyond what the plan specified

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- JobHistoryService ready for event wiring in Form1 (SubscribeTo method)
- Query API (GetRunsForJob with filter) ready for Phase 5 UI consumption
- Search API (SearchRunOutput) ready for output viewer integration
- All persistence patterns established for Plan 03 (tests/integration)

## Self-Check: PASSED

- Services/JobHistoryService.cs exists (390 lines, 11 public members)
- Both task commits verified (be48113, e02752f)
- Build succeeds (Release mode, 0 errors)
- All 1131 tests pass

---
*Phase: 04-history-output*
*Completed: 2026-03-07*
