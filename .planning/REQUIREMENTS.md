# Requirements: SSH_Helper Job Scheduler

**Defined:** 2026-03-07
**Core Value:** Users can define scheduled jobs that automatically execute SSH presets against specific hosts on a recurring or one-time basis, with full run history and output retention.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Job Management

- [x] **JMGT-01**: User can create a scheduled job with a name, target preset or preset folder, schedule, host list, and credential configuration
- [x] **JMGT-02**: User can edit an existing scheduled job's definition
- [x] **JMGT-03**: User can delete a scheduled job
- [x] **JMGT-04**: User can enable or disable a job without deleting it
- [ ] **JMGT-05**: User can export job definitions to a file for sharing between instances
- [ ] **JMGT-06**: User can import job definitions from an exported file

### Scheduling

- [x] **SCHD-01**: User can configure a cron-based recurring schedule with standard cron expressions
- [x] **SCHD-02**: User can configure a one-time schedule at a specific date and time
- [x] **SCHD-03**: One-time jobs auto-disable after successful execution
- [x] **SCHD-04**: Scheduler displays human-readable cron text alongside the expression (e.g., "Every day at 3:00 AM")
- [x] **SCHD-05**: User can build cron expressions via a visual point-and-click builder
- [x] **SCHD-06**: Scheduler shows next upcoming run times as a preview
- [x] **SCHD-07**: Jobs missed while the application was closed are recorded as skipped, never auto-executed

### Host Targets

- [x] **HOST-01**: Each job maintains its own dedicated host list, independent of the main grid
- [x] **HOST-02**: User can populate a job's host list by importing from a CSV file
- [x] **HOST-03**: User can populate a job's host list by copying from the current main grid
- [x] **HOST-04**: User can manually enter hosts directly in the job editor

### Credentials

- [x] **CRED-01**: User can configure credential mode per job: stored credentials, inherit from preset, or per-host grid column
- [x] **CRED-02**: Stored credentials are persisted securely for unattended execution

### Execution

- [ ] **EXEC-01**: Scheduler evaluates due jobs and executes them automatically while the application is running
- [ ] **EXEC-02**: User can trigger a job immediately via run-now action
- [ ] **EXEC-03**: User can cancel a running job mid-execution
- [ ] **EXEC-04**: User can configure the maximum number of concurrent jobs
- [ ] **EXEC-05**: Excess due jobs queue until execution slots become available
- [ ] **EXEC-06**: Folder jobs execute all presets in the target folder
- [ ] **EXEC-07**: User can configure folder job execution order per job (sequential or parallel)

### History & Output

- [ ] **HIST-01**: Each job run records start/end time, duration, success state, and per-host success/failure counts
- [ ] **HIST-02**: Full SSH output is persisted per run in dedicated output files
- [ ] **HIST-03**: History is automatically pruned by whichever limit hits first: max entries per job OR age-based retention
- [ ] **HIST-04**: User can search and filter within stored job output

### UI

- [ ] **UI-01**: User can view all jobs in a list showing status, next run time, and last result
- [ ] **UI-02**: User can create and edit jobs via a dedicated editor dialog with cron preview and host/credential options
- [ ] **UI-03**: User receives in-app notifications (popup or log) on job completion and failures

### Reliability

- [x] **RELY-01**: Job definitions persist across application restarts
- [ ] **RELY-02**: Jobs orphaned by application crash are detected and marked as failed on next startup
- [ ] **RELY-03**: Scheduler timer operates independently of UI thread (no freezing during modal dialogs)

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Enhanced UI

- **UI-04**: Status bar integration showing scheduler state in main form
- **UI-05**: Job templates with pre-built configurations for common workflows (health check, backup verification)

### Advanced Scheduling

- **SCHD-08**: Per-job catch-up option to run missed jobs on startup
- **SCHD-09**: Job dependency chains (run job B after job A completes)

### Notifications

- **NOTF-01**: Windows toast desktop notifications on completion/failure
- **NOTF-02**: Email/SMTP notifications for critical job failures

## Out of Scope

| Feature | Reason |
|---------|--------|
| Background service / Windows Service mode | Desktop app scheduler; jobs only run while app is open |
| Windows Task Scheduler integration | Keep scheduling self-contained within the app |
| Remote job triggering / API | Desktop app only, no server component |
| Workflow designer (visual DAG) | Over-engineering; presets and folders are sufficient |
| Real-time log streaming to UI during scheduled runs | Complexity vs value; output is available after completion |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| JMGT-01 | Phase 1 | Complete |
| JMGT-02 | Phase 1 | Complete |
| JMGT-03 | Phase 1 | Complete |
| JMGT-04 | Phase 1 | Complete |
| JMGT-05 | Phase 5 | Pending |
| JMGT-06 | Phase 5 | Pending |
| SCHD-01 | Phase 2 | Complete |
| SCHD-02 | Phase 2 | Complete |
| SCHD-03 | Phase 2 | Complete |
| SCHD-04 | Phase 2 | Complete |
| SCHD-05 | Phase 2 | Complete |
| SCHD-06 | Phase 2 | Complete |
| SCHD-07 | Phase 2 | Complete |
| HOST-01 | Phase 1 | Complete |
| HOST-02 | Phase 1 | Complete |
| HOST-03 | Phase 1 | Complete |
| HOST-04 | Phase 1 | Complete |
| CRED-01 | Phase 1 | Complete |
| CRED-02 | Phase 1 | Complete |
| EXEC-01 | Phase 3 | Pending |
| EXEC-02 | Phase 3 | Pending |
| EXEC-03 | Phase 3 | Pending |
| EXEC-04 | Phase 3 | Pending |
| EXEC-05 | Phase 3 | Pending |
| EXEC-06 | Phase 3 | Pending |
| EXEC-07 | Phase 3 | Pending |
| HIST-01 | Phase 4 | Pending |
| HIST-02 | Phase 4 | Pending |
| HIST-03 | Phase 4 | Pending |
| HIST-04 | Phase 4 | Pending |
| UI-01 | Phase 5 | Pending |
| UI-02 | Phase 5 | Pending |
| UI-03 | Phase 5 | Pending |
| RELY-01 | Phase 1 | Complete |
| RELY-02 | Phase 3 | Pending |
| RELY-03 | Phase 3 | Pending |

**Coverage:**
- v1 requirements: 36 total
- Mapped to phases: 36
- Unmapped: 0

---
*Requirements defined: 2026-03-07*
*Last updated: 2026-03-07 after roadmap creation*
