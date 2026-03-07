# Phase 2: Scheduling Engine - Context

**Gathered:** 2026-03-07
**Status:** Ready for planning

<domain>
## Phase Boundary

Users can attach cron or one-time schedules to jobs and see when they will next run, with predictable handling of missed runs. This phase delivers the cron builder UI, schedule type selection, next-run preview, human-readable cron descriptions, and missed-run detection on startup. Execution, history, and the full management UI are separate phases.

</domain>

<decisions>
## Implementation Decisions

### Cron builder UX
- Combined approach: preset templates as quick-start buttons at the top, with dropdown selectors below that update live
- Selecting a preset fills the dropdowns; editing dropdowns updates the expression
- Editable raw text field showing the cron expression, synced bidirectionally with the visual builder
- 5-field standard cron format (minute, hour, day-of-month, month, day-of-week) — no seconds field
- Comprehensive preset templates organized by frequency: Every 5/15/30 min, Hourly, Daily at midnight/3am, Weekdays 9am, Weekly Monday, Monthly 1st, Quarterly

### Schedule display
- Human-readable cron description shown inline after the expression: "0 3 * * * — Every day at 3:00 AM"
- Next-run preview shows the next 1 upcoming run time only
- All times displayed in user's local timezone (internal storage remains UTC)

### One-time schedule flow
- Standard WinForms DateTimePicker with calendar dropdown + time spinner for date/time selection
- Past dates are blocked — save/OK disabled with validation message "Schedule time must be in the future"
- After execution, job auto-disables with DisabledReason="One-time schedule completed" but keeps the original schedule time visible as a record
- User can re-enable and set a new time to reuse the job

### Schedule type selection
- ComboBox dropdown in the job editor with "Recurring" and "One-time" options
- Selecting one shows the relevant controls (cron builder or date picker), mutually exclusive

### Missed-run handling
- Already decided (PROJECT.md + REQUIREMENTS.md): always skip, never auto-execute
- Jobs missed while application was closed are recorded as skipped entries on startup
- Claude handles how skipped runs are surfaced to the user

### Claude's Discretion
- Next-run preview placement in the cron builder dialog (below builder or side panel)
- Exact cron library choice (STATE.md notes Cronos + CronExpressionDescriptor as research recommendation)
- Dropdown selector layout and styling within existing dialog patterns
- Validation UX for the raw cron text field (when/how to show errors)
- Missed-run notification approach (log entry, visual indicator, or popup)

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `JobDefinition.CronExpression` and `JobDefinition.OneTimeScheduleUtc`: Placeholder fields already exist from Phase 1
- `JobDefinition.IsEnabled` / `JobDefinition.DisabledReason`: Auto-disable pattern ready for one-time job completion
- `JobStorageService`: Schedule data persists via existing job model — no new storage needed
- `DialogTheme`: New schedule dialogs must use existing dark/light theme system
- `InputValidator`: Extend for cron expression validation

### Established Patterns
- WinForms DateTimePicker: Built-in control, theme-aware, available for one-time scheduling
- Event-driven communication: Services raise events, UI subscribes — schedule changes should follow this
- Newtonsoft.Json for persistence: Schedule fields serialize naturally with existing job model
- Manual DI in Form1 constructor: Any new scheduling service wired here

### Integration Points
- `JobDefinition` model: Add `ScheduleType` enum (Recurring/OneTime) or derive from which field is set
- Job editor dialog (Phase 5 builds the full dialog, but Phase 2 needs at least a schedule panel/control for testing)
- `JobStorageService`: Schedule data saves with existing job CRUD — no separate storage
- Future `SchedulerService` (Phase 3) will consume the schedule data this phase establishes

</code_context>

<specifics>
## Specific Ideas

- Cron builder should feel like a standard DevOps tool — preset templates cover the common SSH operational patterns (health checks every 5 min, daily backups at 3am, weekly reports on Monday)
- The inline human-readable description ("0 3 * * * — Every day at 3:00 AM") keeps the UI compact while being immediately understandable
- One-time jobs that auto-disable but keep their schedule time visible act as reusable templates

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 02-scheduling-engine*
*Context gathered: 2026-03-07*
