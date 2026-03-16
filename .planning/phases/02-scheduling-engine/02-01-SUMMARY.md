---
phase: 02-scheduling-engine
plan: 01
subsystem: scheduling
tags: [cronos, cron-expression-descriptor, cron, scheduling, missed-run-detection]

# Dependency graph
requires:
  - phase: 01-job-definitions
    provides: "JobDefinition model with CronExpression, OneTimeScheduleUtc, IsEnabled, DisabledReason fields"
provides:
  - "SchedulingService with 7 public methods: ValidateCronExpression, GetDescription, GetNextRunLocal, GetNextRunUtc, GetMissedOccurrences, DetectMissedRuns, MarkOneTimeCompleted"
  - "ScheduleType enum (None, Recurring, OneTime) on JobDefinition"
  - "SkippedRunEntry model for missed-run tracking"
  - "InputValidator.ValidateCronExpression and IsFutureDate static methods"
  - "Cronos 0.11.1 and CronExpressionDescriptor 2.45.0 NuGet packages"
affects: [02-scheduling-engine, 03-timer-execution]

# Tech tracking
tech-stack:
  added: [Cronos 0.11.1, CronExpressionDescriptor 2.45.0]
  patterns: [stateless scheduling service, UTC-internal with local-display conversion, missed-run detection via cron occurrence enumeration]

key-files:
  created:
    - Services/SchedulingService.cs
    - Models/SkippedRunEntry.cs
    - SSH_Helper.Tests/Services/SchedulingServiceTests.cs
    - SSH_Helper.Tests/Utilities/InputValidatorCronTests.cs
  modified:
    - Models/JobDefinition.cs
    - Utilities/InputValidator.cs
    - SSH_Helper.csproj

key-decisions:
  - "SchedulingService is sealed and stateless -- no timer or execution logic (Phase 3 scope)"
  - "5-field cron only, CronFormat.IncludeSeconds never passed to Cronos"
  - "GetMissedOccurrences uses exclusive bounds on both start and end"
  - "MarkOneTimeCompleted preserves OneTimeScheduleUtc as visible record per user decision"

patterns-established:
  - "Stateless scheduling service: all cron logic via SchedulingService, no direct Cronos usage in UI"
  - "UTC-internal pattern: all DateTime calculations in UTC, only GetNextRunLocal converts for display"
  - "Missed-run detection: enumerate cron occurrences between shutdown and startup, record as SkippedRunEntry"

requirements-completed: [SCHD-01, SCHD-02, SCHD-03, SCHD-04, SCHD-06, SCHD-07]

# Metrics
duration: 4min
completed: 2026-03-07
---

# Phase 2 Plan 1: Scheduling Service Foundation Summary

**Cronos-backed SchedulingService with cron validation, human-readable descriptions, next-run calculation, missed-run detection, and one-time job completion logic**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-07T16:39:23Z
- **Completed:** 2026-03-07T16:44:12Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments
- Installed Cronos 0.11.1 and CronExpressionDescriptor 2.45.0 as scheduling foundation
- Created SchedulingService with all 7 public methods for cron and one-time schedule operations
- Added ScheduleType enum (None/Recurring/OneTime) and SkippedRunEntry model
- Extended InputValidator with ValidateCronExpression and IsFutureDate
- Full TDD: 44 new tests (16 InputValidator/model + 28 SchedulingService), full suite at 1018 tests green

## Task Commits

Each task was committed atomically:

1. **Task 1: Install NuGet packages, add ScheduleType enum, create SkippedRunEntry, and extend InputValidator** - `612f682` (feat)
2. **Task 2: Create SchedulingService with cron, one-time, and missed-run logic** - `bcf0e67` (feat)

_Both tasks followed TDD: tests written first (RED confirmed), then implementation (GREEN confirmed)._

## Files Created/Modified
- `SSH_Helper.csproj` - Added Cronos 0.11.1 and CronExpressionDescriptor 2.45.0 package references
- `Models/JobDefinition.cs` - Added ScheduleType enum and ScheduleType property (defaults to None)
- `Models/SkippedRunEntry.cs` - New model for recording missed job runs (JobId, JobName, ScheduledTimeUtc, DetectedUtc)
- `Utilities/InputValidator.cs` - Added ValidateCronExpression (5-field, Cronos-backed) and IsFutureDate methods
- `Services/SchedulingService.cs` - New sealed service with ValidateCronExpression, GetDescription, GetNextRunLocal, GetNextRunUtc, GetMissedOccurrences, DetectMissedRuns, MarkOneTimeCompleted
- `SSH_Helper.Tests/Utilities/InputValidatorCronTests.cs` - 16 tests covering cron validation, future-date, ScheduleType enum, and SkippedRunEntry
- `SSH_Helper.Tests/Services/SchedulingServiceTests.cs` - 28 tests covering all 7 SchedulingService methods and edge cases

## Decisions Made
- SchedulingService is sealed and stateless -- consistent with existing service patterns, timer/execution is Phase 3 scope
- 5-field cron only enforced at both InputValidator and SchedulingService levels for defense in depth
- GetMissedOccurrences uses exclusive bounds (fromInclusive: false, toInclusive: false) to avoid double-counting
- MarkOneTimeCompleted preserves OneTimeScheduleUtc per user decision (keeps visible as reusable template record)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- SchedulingService is ready for CronBuilderControl (Plan 02) to consume for validation, description, and next-run preview
- ScheduleType enum and SkippedRunEntry model ready for startup integration (Plan 03)
- All 7 SchedulingService methods tested and available for UI and timer layers

## Self-Check: PASSED

All 7 created/modified files verified on disk. Both task commits (612f682, bcf0e67) verified in git log.

---
*Phase: 02-scheduling-engine*
*Completed: 2026-03-07*
