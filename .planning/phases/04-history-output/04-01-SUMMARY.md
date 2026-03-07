---
phase: 04-history-output
plan: 01
subsystem: models
tags: [history, models, configuration, json, atomic-write]

# Dependency graph
requires:
  - phase: 03-execution-pipeline
    provides: JobRunResult event args, ExecutionResult model, JobExecutionService pipeline
provides:
  - JobHostOutput per-host output model for history payload
  - JobRunRecord and JobRunIndexDocument for per-job run history index
  - JobRunPayload full run data model with host outputs
  - JobRunFilter query filter for history API
  - Per-job retention overrides on JobDefinition
  - Global history defaults on AppConfiguration
  - JsonFileWriter shared atomic JSON write utility
  - HostOutputs populated on JobRunResult in JobCompleted events
affects: [04-02-PLAN, 04-03-PLAN]

# Tech tracking
tech-stack:
  added: []
  patterns: [shared utility extraction with delegation pattern, per-host output handoff via event args]

key-files:
  created:
    - Models/JobHostOutput.cs
    - Models/JobRunRecord.cs
    - Models/JobRunPayload.cs
    - Models/JobRunFilter.cs
    - Utilities/JsonFileWriter.cs
  modified:
    - Models/JobRunResult.cs
    - Models/JobDefinition.cs
    - Models/AppConfiguration.cs
    - Services/HistoryStorageService.cs
    - Services/JobExecutionService.cs

key-decisions:
  - "WriteJsonAtomic extracted with delegation pattern preserving all existing call sites unchanged"
  - "HostOutputs uses HostConnection.IpAddress (not Address/Hostname) matching actual model property"
  - "Error-path JobCompleted events retain null HostOutputs since no hosts were reached"

patterns-established:
  - "Utility extraction: delegate original private method to new shared static utility"
  - "Event handoff: map ExecutionResult to lightweight DTO (JobHostOutput) at event boundary"

requirements-completed: [HIST-01, HIST-02, HIST-03]

# Metrics
duration: 4min
completed: 2026-03-07
---

# Phase 04 Plan 01: History Data Models Summary

**History data models, retention config, atomic JSON utility extraction, and per-host output handoff via JobCompleted events**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-07T21:46:53Z
- **Completed:** 2026-03-07T21:51:35Z
- **Tasks:** 2
- **Files modified:** 10

## Accomplishments
- Created four new history data models (JobHostOutput, JobRunRecord/IndexDocument, JobRunPayload, JobRunFilter)
- Extended JobDefinition with per-job retention overrides and AppConfiguration with global history defaults
- Extracted WriteJsonAtomic to shared Utilities/JsonFileWriter.cs, preserving HistoryStorageService behavior
- Enhanced JobExecutionService to populate HostOutputs on JobRunResult from ExecutionResult list
- All 1131 existing tests pass with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Create history data models and extend configuration** - `d12946d` (feat)
2. **Task 2: Extract WriteJsonAtomic utility and enhance execution handoff** - `812f406` (feat)

## Files Created/Modified
- `Models/JobHostOutput.cs` - Per-host output record for history payload
- `Models/JobRunRecord.cs` - Lightweight index entry + JobRunIndexDocument wrapper
- `Models/JobRunPayload.cs` - Full run payload with metadata and per-host output
- `Models/JobRunFilter.cs` - Query filter object for history API
- `Models/JobRunResult.cs` - Added HostOutputs property for event handoff
- `Models/JobDefinition.cs` - Added MaxHistoryRuns and HistoryRetentionDays overrides
- `Models/AppConfiguration.cs` - Added DefaultMaxHistoryRuns, DefaultHistoryRetentionDays, MaxJobOutputCharsPerHost
- `Utilities/JsonFileWriter.cs` - Shared atomic JSON write utility
- `Services/HistoryStorageService.cs` - Delegated WriteJsonAtomic to shared utility
- `Services/JobExecutionService.cs` - Populated HostOutputs in ExecuteJobCoreAsync

## Decisions Made
- WriteJsonAtomic extracted with delegation pattern (HistoryStorageService keeps a private wrapper that delegates), preserving all existing call sites unchanged and avoiding a large refactor
- Used HostConnection.IpAddress for HostOutputs mapping, which is the actual property on the model (plan referenced Address/Hostname which do not exist)
- Error-path JobCompleted events (OnJobFailed) intentionally leave HostOutputs null since no hosts were reached in error scenarios

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Running SSH_Helper.exe process locked the Debug output directory, preventing Debug builds. Used Release mode for verification. No impact on correctness -- zero compilation errors in both modes.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All data models and contracts ready for Plan 02 (JobHistoryService)
- JsonFileWriter available for both HistoryStorageService and future JobHistoryService
- JobRunResult.HostOutputs populated in JobCompleted events, ready for history recording
- AppConfiguration defaults and JobDefinition overrides in place for retention logic

## Self-Check: PASSED

- All 5 new files exist (4 models + 1 utility)
- Both task commits verified (d12946d, 812f406)
- All 6 new properties confirmed in modified files
- Build succeeds (Release mode)
- All 1131 tests pass

---
*Phase: 04-history-output*
*Completed: 2026-03-07*
