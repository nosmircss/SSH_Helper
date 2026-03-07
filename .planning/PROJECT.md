# SSH_Helper Job Scheduler

## What This Is

An in-app job scheduling system for SSH_Helper that allows users to schedule preset execution against dedicated host lists on cron or one-time schedules. It enables automated operational workflows — health checks, backups, auditing, report generation — without manual intervention while the application is running.

## Core Value

Users can define scheduled jobs that automatically execute SSH presets against specific hosts on a recurring or one-time basis, with full run history and output retention.

## Requirements

### Validated

- SSH command execution against multiple hosts via presets — existing
- Preset management (save, load, rename, delete, duplicate, folder organization) — existing
- CSV-based host grid with custom columns and variable substitution — existing
- YAML-based scripting engine with 37+ command types — existing
- SSH connection pooling with health checks — existing
- Environment profile management — existing
- Execution history with per-host output retention — existing

### Active

- [ ] Persisted scheduled job definitions (preset or folder target, schedule, host list, credentials, notifications)
- [ ] Cron-based recurring schedules with next-run preview
- [ ] One-time schedules with auto-disable after execution
- [ ] Folder jobs that run all presets in the folder (sequential or parallel, configurable per job)
- [ ] Dedicated host list per job (import from CSV, copy from main grid, or manual entry)
- [ ] Missed-run startup policy: always skip, record as skipped entries
- [ ] Bounded concurrent job execution with user-configurable concurrency limit
- [ ] Run-now and cancellation controls
- [ ] Per-run history with start/end time, duration, success state, host success/failure counts
- [ ] Full output retention per run with configurable pruning (max entries AND time-based, whichever hits first)
- [ ] Configurable credentials per job (stored creds, inherit from preset, or per-host grid column)
- [ ] In-app log/popup notifications for run completion and failures
- [ ] Status bar integration showing scheduler state
- [ ] Scheduler management UI (job list, run history, run-now actions)
- [ ] Job editor UI with cron preview and host/credential options

### Out of Scope

- Email/SMTP notifications — complexity and external dependency not justified for v1
- Running jobs while app is closed — this is an in-app scheduler, not a system service
- Catch-up/auto-execute of missed jobs on startup — always skip policy
- Windows Task Scheduler integration — keep scheduling self-contained
- Remote job triggering / API — desktop app only

## Context

- SSH_Helper is a .NET 8 WinForms application (~10K lines in Form1 alone)
- Service-oriented architecture: services handle business logic, Form1 handles UI
- Event-driven communication between services and UI
- Existing execution infrastructure: `SshExecutionService`, `SshConnectionPool`, `PresetManager`, `ConfigurationService`
- Existing execution history system in `ExecutionHistoryService` — job history should follow similar patterns
- OpenSpec proposal exists at `openspec/changes/add-job-scheduler/` with initial spec, proposal, and task breakdown
- Codebase map available at `.planning/codebase/`

## Constraints

- **Platform**: Windows Forms on .NET 8.0 — no cross-platform concerns
- **Architecture**: Follow existing service-oriented pattern — new services, not logic in Form1
- **Persistence**: Use existing `ConfigurationService` pattern (JSON in `%LocalAppData%\SSH_Helper\`)
- **SSH Libraries**: Use existing Rebex (primary) + SSH.NET infrastructure
- **Threading**: WinForms STA thread model — async execution with UI marshalling via events
- **Theming**: New dialogs must support existing dark/light theme system (`DialogTheme`)

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Dedicated host list per job | Jobs should be self-contained; main grid changes shouldn't break scheduled jobs | — Pending |
| Folder jobs run all presets | Enables batch workflows like "run all backup presets" as one scheduled job | — Pending |
| Always skip missed jobs | Predictable behavior; catch-up could cause unexpected bulk execution | — Pending |
| No email notifications in v1 | Reduces external dependencies; in-app notifications sufficient for desktop use | — Pending |
| User-configurable concurrency | Different users have different host counts and network constraints | — Pending |
| Configurable credentials per job | Flexibility: some jobs use stored creds, others inherit from preset/grid | — Pending |
| Per-job sequential/parallel choice for folders | Different workflows need different execution patterns | — Pending |
| Dual pruning (max entries + time-based) | Prevents unbounded disk usage while keeping recent history accessible | — Pending |

---
*Last updated: 2026-03-07 after initialization*
