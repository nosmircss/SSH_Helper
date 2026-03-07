---
phase: 03-execution-pipeline
plan: 01
subsystem: models
tags: [execution-pipeline, job-queue, crash-recovery, models, enums]

# Dependency graph
requires:
  - phase: 01-job-definition
    provides: JobDefinition model, CredentialMode/ScheduleType/JobTargetType enums
  - phase: 02-scheduling-engine
    provides: LastAppShutdownUtc on AppConfiguration
provides:
  - RunningJobState model for crash recovery persistence
  - QueuedJob model for FIFO execution queue
  - JobRunResult model for Phase 4 history handoff
  - FolderExecutionMode enum (Sequential, Parallel)
  - JobExecutionState enum (Queued, Started, Completed, Failed, Cancelled, Skipped)
  - JobDefinition.RunningState, FolderExecutionMode, StopOnError properties
  - AppConfiguration.MaxConcurrentJobs property (default 3)
affects: [03-execution-pipeline, 04-run-history]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "File-scoped namespace for new model files (namespace SSH_Helper.Models;)"
    - "POCO models with auto-serialization via Newtonsoft.Json (no attributes needed)"
    - "In-memory-only models (QueuedJob) vs persisted models (RunningJobState)"

key-files:
  created:
    - Models/RunningJobState.cs
    - Models/QueuedJob.cs
    - Models/JobRunResult.cs
    - SSH_Helper.Tests/Models/ExecutionPipelineModelTests.cs
    - SSH_Helper.Tests/Models/MaxConcurrentJobsTests.cs
  modified:
    - Models/JobDefinition.cs
    - Models/AppConfiguration.cs

key-decisions:
  - "FolderExecutionMode enum placed before JobTargetType in JobDefinition.cs, after existing enums"
  - "RunningJobState is minimal POCO (StartedUtc only) -- expanded as needed during service implementation"
  - "QueuedJob uses constructor for required properties; in-memory only, not persisted"
  - "MaxConcurrentJobs defaults to 3 per user decision, validation at service level not model level"

patterns-established:
  - "Execution pipeline models use file-scoped namespaces matching existing convention"
  - "New enums defined in same file as consuming class (JobDefinition.cs) for discoverability"

requirements-completed: [EXEC-04, EXEC-07, RELY-02]

# Metrics
duration: 4min
completed: 2026-03-07
---

# Phase 3 Plan 01: Execution Pipeline Models Summary

**RunningJobState, QueuedJob, JobRunResult models with FolderExecutionMode/JobExecutionState enums and MaxConcurrentJobs config for execution pipeline data contracts**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-07T20:20:39Z
- **Completed:** 2026-03-07T20:24:19Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- Created three new model types (RunningJobState, QueuedJob, JobRunResult) establishing data contracts for the execution pipeline
- Added FolderExecutionMode and JobExecutionState enums covering all execution lifecycle states
- Extended JobDefinition with RunningState, FolderExecutionMode, and StopOnError properties
- Added MaxConcurrentJobs property to AppConfiguration with default of 3
- 30 new unit tests (23 + 7) covering all types, defaults, edge values, and JSON serialization
- Full test suite (1095 tests) passes with zero regressions

## Task Commits

Each task was committed atomically:

1. **Task 1: Create execution pipeline model types and enums** - `ab36a2c` (feat)
2. **Task 2: Add MaxConcurrentJobs to AppConfiguration** - `24548bc` (feat)

## Files Created/Modified
- `Models/RunningJobState.cs` - Crash recovery state model tracking StartedUtc
- `Models/QueuedJob.cs` - FIFO queue entry model with JobId and QueuedUtc
- `Models/JobRunResult.cs` - Lightweight execution result for Phase 4 history handoff
- `Models/JobDefinition.cs` - Extended with RunningState, FolderExecutionMode, StopOnError properties; added FolderExecutionMode and JobExecutionState enums
- `Models/AppConfiguration.cs` - Added MaxConcurrentJobs property (default 3)
- `SSH_Helper.Tests/Models/ExecutionPipelineModelTests.cs` - 23 tests for new model types and enums
- `SSH_Helper.Tests/Models/MaxConcurrentJobsTests.cs` - 7 tests for MaxConcurrentJobs property

## Decisions Made
- FolderExecutionMode enum placed in JobDefinition.cs after existing enums for discoverability
- RunningJobState kept minimal (StartedUtc only) -- can be expanded during service implementation
- QueuedJob constructor enforces required properties; model is in-memory only
- MaxConcurrentJobs validation deferred to service level per plan specification

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Intermittent MSB3492 build cache error on SSH_Helper.csproj resolved by full rebuild (pre-existing cache corruption, not caused by changes)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- All execution pipeline model types ready for JobExecutionService consumption in Plan 02
- FolderExecutionMode and JobExecutionState enums available for service logic
- MaxConcurrentJobs available for semaphore-based concurrency control
- No blockers for Plan 02

## Self-Check: PASSED

- All 7 files verified present on disk
- Both commits (ab36a2c, 24548bc) verified in git log
- Full test suite (1095 tests) passes with zero failures

---
*Phase: 03-execution-pipeline*
*Completed: 2026-03-07*
