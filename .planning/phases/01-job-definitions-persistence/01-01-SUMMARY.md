---
phase: 01-job-definitions-persistence
plan: 01
subsystem: models
tags: [job-definition, sha256, credential-targets, data-model]

# Dependency graph
requires: []
provides:
  - "JobDefinition model with GUID id, target type, hosts, credential mode"
  - "CredentialMode and JobTargetType enums"
  - "ContentHasher SHA256 utility for preset drift detection"
  - "CredentialTargets.JobPasswordTarget for per-job credential storage"
affects: [01-job-definitions-persistence, 02-job-scheduling-engine]

# Tech tracking
tech-stack:
  added: []
  patterns: [TDD red-green for model classes, static utility pattern for hashing]

key-files:
  created:
    - Models/JobDefinition.cs
    - Utilities/ContentHasher.cs
    - SSH_Helper.Tests/Models/JobDefinitionTests.cs
    - SSH_Helper.Tests/Utilities/ContentHasherTests.cs
  modified:
    - Services/Credentials/CredentialTargets.cs
    - SSH_Helper.Tests/Services/CredentialTargetsTests.cs

key-decisions:
  - "GUID Id uses ToString(N) for 32-char hex without dashes"
  - "ContentHasher returns uppercase hex via Convert.ToHexString"
  - "JobPasswordTarget follows existing CredentialTargets null-safe trim pattern"

patterns-established:
  - "Job model uses simple properties with defaults, matching PresetInfo convention"
  - "ContentHasher is static utility matching existing Utilities namespace pattern"

requirements-completed: [HOST-01, HOST-04, CRED-01, CRED-02]

# Metrics
duration: 3min
completed: 2026-03-07
---

# Phase 1 Plan 1: Job Model & Content Hasher Summary

**JobDefinition model with GUID ids, dual enums, SHA256 content hasher for drift detection, and per-job credential target naming**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-07T15:44:40Z
- **Completed:** 2026-03-07T15:47:28Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments
- JobDefinition model with all 16 properties including hosts grid, credential mode, drift warning, and scheduling placeholders
- ContentHasher producing deterministic uppercase SHA256 hex strings with null/empty safety
- CredentialTargets extended with JobPasswordTarget for per-job credential storage
- 24 tests passing (12 JobDefinition, 6 ContentHasher, 6 CredentialTargets)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create JobDefinition model with enums and ContentHasher utility**
   - `4160a9b` (test: failing tests for JobDefinition and ContentHasher)
   - `a5ec83f` (feat: implement JobDefinition model, enums, and ContentHasher)
2. **Task 2: Extend CredentialTargets with JobPasswordTarget**
   - `6811012` (test: failing tests for JobPasswordTarget)
   - `98e6676` (feat: add JobPasswordTarget to CredentialTargets)

_TDD tasks each have RED and GREEN commits._

## Files Created/Modified
- `Models/JobDefinition.cs` - JobDefinition class, CredentialMode enum, JobTargetType enum
- `Utilities/ContentHasher.cs` - Static SHA256 content hashing utility
- `Services/Credentials/CredentialTargets.cs` - Added JobPasswordTarget method
- `SSH_Helper.Tests/Models/JobDefinitionTests.cs` - 12 tests for model defaults, properties, enums
- `SSH_Helper.Tests/Utilities/ContentHasherTests.cs` - 6 tests for hashing behavior
- `SSH_Helper.Tests/Services/CredentialTargetsTests.cs` - 4 new tests for JobPasswordTarget

## Decisions Made
- GUID Id stored as 32-char hex (no dashes) for compact, filesystem-safe identifiers
- ContentHasher returns uppercase hex via Convert.ToHexString (consistent with .NET convention)
- JobPasswordTarget follows the existing null-safe trim pattern from HostPasswordTarget

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- JobDefinition model ready for Plan 02 (JobStorageService) to persist and load
- ContentHasher ready for drift detection in Plan 03 (PresetManager integration)
- CredentialTargets.JobPasswordTarget ready for credential storage in Plan 02

---
*Phase: 01-job-definitions-persistence*
*Completed: 2026-03-07*
