# Roadmap: SSH_Helper Job Scheduler

## Overview

This roadmap delivers an in-app job scheduling system for SSH_Helper. The journey moves from establishing the job data model and persistence, through scheduling logic and execution wiring, to history tracking and the management UI. Each phase delivers a coherent, testable capability that the next phase builds on.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Job Definitions & Persistence** - Job model, CRUD, host lists, credentials, and storage
- [ ] **Phase 2: Scheduling Engine** - Cron parsing, one-time schedules, next-run preview, and missed-run handling
- [ ] **Phase 3: Execution Pipeline** - Automatic and manual job execution, cancellation, concurrency, and folder jobs
- [ ] **Phase 4: History & Output** - Per-run records, output file retention, pruning, and search
- [ ] **Phase 5: Scheduler UI & Integration** - Job list, editor dialog, notifications, and export/import

## Phase Details

### Phase 1: Job Definitions & Persistence
**Goal**: Users can create, edit, and manage self-contained job definitions with dedicated host lists and credential configurations that persist across restarts
**Depends on**: Nothing (first phase)
**Requirements**: JMGT-01, JMGT-02, JMGT-03, JMGT-04, HOST-01, HOST-02, HOST-03, HOST-04, CRED-01, CRED-02, RELY-01
**Success Criteria** (what must be TRUE):
  1. User can create a job definition specifying a name, target preset, host list, and credential mode, and find it intact after restarting the application
  2. User can edit any field of an existing job and see the changes persisted
  3. User can delete a job and confirm it no longer appears in the stored definitions
  4. User can enable or disable a job without losing its configuration
  5. User can populate a job's host list via CSV import, main grid copy, or manual entry, and each job's hosts are independent of the main grid
**Plans**: TBD

Plans:
- [ ] 01-01: TBD
- [ ] 01-02: TBD

### Phase 2: Scheduling Engine
**Goal**: Users can attach cron or one-time schedules to jobs and see when they will next run, with predictable handling of missed runs
**Depends on**: Phase 1
**Requirements**: SCHD-01, SCHD-02, SCHD-03, SCHD-04, SCHD-05, SCHD-06, SCHD-07
**Success Criteria** (what must be TRUE):
  1. User can assign a cron expression to a job and see a human-readable description of the schedule alongside the expression
  2. User can build a cron expression using a visual point-and-click builder without writing cron syntax manually
  3. User can assign a one-time schedule to a job, and after it executes, the job auto-disables
  4. User can see upcoming next-run times for all scheduled jobs
  5. After restarting the application, any jobs that were due while it was closed are recorded as skipped (never auto-executed)
**Plans**: TBD

Plans:
- [ ] 02-01: TBD
- [ ] 02-02: TBD

### Phase 3: Execution Pipeline
**Goal**: Scheduled jobs execute automatically at their due times, with manual run-now, cancellation, concurrency control, and folder job support
**Depends on**: Phase 2
**Requirements**: EXEC-01, EXEC-02, EXEC-03, EXEC-04, EXEC-05, EXEC-06, EXEC-07, RELY-02, RELY-03
**Success Criteria** (what must be TRUE):
  1. A job with a due cron schedule executes automatically against its host list without user intervention while the app is running
  2. User can trigger any job immediately via run-now and cancel a running job mid-execution
  3. When more jobs are due than the configured concurrency limit, excess jobs queue and execute as slots free up
  4. A folder job executes all presets in the target folder, respecting the per-job sequential or parallel configuration
  5. If the application crashes during a job run, the orphaned run is detected and marked as failed on next startup
**Plans**: TBD

Plans:
- [ ] 03-01: TBD
- [ ] 03-02: TBD

### Phase 4: History & Output
**Goal**: Every job run produces a complete record with full output, and old history is automatically pruned to prevent unbounded storage growth
**Depends on**: Phase 3
**Requirements**: HIST-01, HIST-02, HIST-03, HIST-04
**Success Criteria** (what must be TRUE):
  1. After a job completes, the user can see a run record showing start/end time, duration, success state, and per-host success/failure counts
  2. Full SSH output from each run is persisted to disk and viewable after the run completes
  3. History entries are automatically pruned when either the max-entries-per-job limit or the age-based retention limit is hit (whichever comes first)
  4. User can search and filter within stored job output to find specific results
**Plans**: TBD

Plans:
- [ ] 04-01: TBD

### Phase 5: Scheduler UI & Integration
**Goal**: Users can manage the full job lifecycle through dedicated management dialogs with in-app notifications and job portability via export/import
**Depends on**: Phase 4
**Requirements**: UI-01, UI-02, UI-03, JMGT-05, JMGT-06
**Success Criteria** (what must be TRUE):
  1. User can view all jobs in a list showing each job's enabled/disabled status, next run time, and last run result
  2. User can create and edit jobs through a dedicated editor dialog that includes cron preview, host list management, and credential options
  3. User receives in-app notifications (popup or log entry) when a job completes or fails
  4. User can export job definitions to a file and import them on another instance of SSH_Helper
**Plans**: TBD

Plans:
- [ ] 05-01: TBD
- [ ] 05-02: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3 -> 4 -> 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Job Definitions & Persistence | 0/2 | Not started | - |
| 2. Scheduling Engine | 0/2 | Not started | - |
| 3. Execution Pipeline | 0/2 | Not started | - |
| 4. History & Output | 0/1 | Not started | - |
| 5. Scheduler UI & Integration | 0/2 | Not started | - |
