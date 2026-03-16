---
phase: 01-job-definitions-persistence
plan: 02
subsystem: services
tags: [job-storage, json-persistence, crud, csv-import, credential-cleanup]

# Dependency graph
requires:
  - phase: 01-job-definitions-persistence/01
    provides: "JobDefinition model, ContentHasher, CredentialTargets.JobPasswordTarget"
provides:
  - "JobStorageService with full CRUD (Save, Delete, Get, Load, Reload)"
  - "JSON persistence to jobs.json with .bak atomic writes and .corrupt recovery"
  - "Case-insensitive unique name validation with 100-char max"
  - "Credential cleanup on job deletion for Stored credential mode"
  - "Preset and folder reference query methods"
  - "CSV host import and grid extraction helper"
affects: [02-job-scheduling-engine, 03-execution-engine, 05-ui]

# Tech tracking
tech-stack:
  added: []
  patterns: [service-with-file-persistence mirroring ConfigurationService, TDD red-green for service layer]

key-files:
  created:
    - Services/JobStorageService.cs
  modified:
    - SSH_Helper.Tests/Services/JobStorageServiceTests.cs

key-decisions:
  - "Jobs persisted in wrapper format { Version: 1, Jobs: [...] } for forward compatibility"
  - "CSV parsing implemented inline rather than using CsvManager to avoid DataTable/WinForms dependency"
  - "ExtractHostDataFromRows is static for use without service instance from UI layer"

patterns-established:
  - "JobStorageService mirrors ConfigurationService pattern: constructor with optional path, Load/Save, .bak backup, .corrupt recovery"
  - "Name validation: trim + empty check + max length + case-insensitive uniqueness"

requirements-completed: [JMGT-01, JMGT-02, JMGT-03, JMGT-04, HOST-02, HOST-03, RELY-01]

# Metrics
duration: 4min
completed: 2026-03-07
---

# Phase 1 Plan 2: JobStorageService Summary

**Job CRUD service with JSON persistence, atomic .bak writes, corrupt file recovery, credential cleanup, and CSV host import**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-07T15:49:54Z
- **Completed:** 2026-03-07T15:53:48Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- JobStorageService with Save/Delete/Get/Load/Reload and .bak atomic writes
- Corrupt file recovery (renames to .corrupt, starts fresh with LoadError)
- Case-insensitive unique name validation, credential cleanup on delete for Stored mode
- Preset and folder reference query methods for impact analysis
- CSV import with quoted field support, static grid extraction helper
- 36 tests passing (28 CRUD + persistence, 8 CSV import + extraction)

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement JobStorageService CRUD and persistence**
   - `de3bb3c` (test: failing tests for JobStorageService CRUD)
   - `660157f` (feat: implement JobStorageService CRUD and persistence)
2. **Task 2: Add CSV import and grid extraction helper methods**
   - `f30066a` (test: failing tests for CSV import and grid extraction)
   - `4490f9b` (feat: add CSV import and grid extraction)

_TDD tasks each have RED and GREEN commits._

## Files Created/Modified
- `Services/JobStorageService.cs` - Full CRUD service with JSON persistence, CSV import, reference queries
- `SSH_Helper.Tests/Services/JobStorageServiceTests.cs` - 36 tests covering all CRUD, persistence, edge cases

## Decisions Made
- Jobs file uses `{ Version: 1, Jobs: [...] }` wrapper format for future schema migration support
- CSV parsing done inline (not via CsvManager) to avoid DataTable/WinForms coupling in the service layer
- ExtractHostDataFromRows is a static method so the UI can use it without a service instance

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- JobStorageService ready for Phase 2 (scheduling) to add schedule-related operations
- Reference queries ready for Phase 3 (PresetManager integration) impact detection
- CSV import ready for Phase 5 (UI) to populate job host lists

## Self-Check: PASSED

- All files exist (JobStorageService.cs: 366 lines, tests: 601 lines)
- All 4 commits verified
- 36 tests passing, 956 total suite passing
- Min line counts exceeded (150/200 required)

---
*Phase: 01-job-definitions-persistence*
*Completed: 2026-03-07*
