# Research Summary: SSH_Helper Job Scheduler

**Domain:** In-app job scheduler for SSH command automation (WinForms desktop)
**Researched:** 2026-03-07
**Overall confidence:** HIGH

## Executive Summary

The job scheduler domain is well-established with clear patterns from tools like Rundeck, Cronicle, and enterprise schedulers. For SSH_Helper's use case -- a WinForms desktop app running SSH presets on cron schedules -- the feature set is straightforward and the existing codebase already provides most of the hard infrastructure (SSH execution, preset management, host grids, execution history).

The .NET ecosystem has excellent cron support through the Cronos library (by the HangfireIO team), which handles timezone/DST edge cases and parses expressions in ~30 nanoseconds. Combined with CronExpressionDescriptor for human-readable previews, the cron UX can match web-based schedulers. No heavy scheduling framework (Quartz.NET, Hangfire) is needed -- a simple System.Threading.Timer evaluating due jobs every 30 seconds is sufficient and avoids introducing database dependencies.

The biggest risks are not in the scheduling logic itself but in the integration points: WinForms thread marshalling (deadlock potential with synchronous Invoke), output file accumulation without pruning, and credential security for persisted job definitions. All are well-understood problems with proven solutions documented in PITFALLS.md.

The existing spec in PROJECT.md aligns well with industry table stakes. The spec's "out of scope" decisions (no email, no background service, no catch-up execution) are the right calls -- they avoid complexity traps that enterprise schedulers solve at significant cost.

## Key Findings

**Stack:** Cronos + CronExpressionDescriptor + System.Threading.Timer. No new heavy dependencies. Two small NuGet packages.

**Architecture:** Five new services (SchedulerService, JobExecutionService, JobStorageService, JobHistoryService, CronService) + three dialogs, following existing event-driven service pattern.

**Critical pitfall:** Timer drift missing minute boundaries -- must evaluate every 30 seconds and track last-evaluated time rather than relying on "is it exactly this minute."

## Implications for Roadmap

Based on research, suggested phase structure:

1. **Core Scheduler Engine** - Foundation services and persistence
   - Addresses: Job CRUD, cron parsing, schedule evaluation, persistence
   - Avoids: Starting with UI before the engine works; credential storage pitfall
   - Rationale: Engine must be solid before layering UI on top

2. **Job Execution Pipeline** - Wire scheduler to existing SSH execution
   - Addresses: Job execution via SshExecutionService, run history, output retention, pruning
   - Avoids: Output accumulation pitfall; thread marshalling deadlocks
   - Rationale: Depends on Phase 1 engine; validates the scheduler actually works

3. **Scheduler UI** - Management dialogs and status integration
   - Addresses: Job editor, job list, history viewer, cron builder UI, status bar, notifications
   - Avoids: Building UI before engine is proven; theme inconsistency
   - Rationale: UI is presentation layer over working services

4. **Advanced Features** - Folder jobs, concurrency, export/import
   - Addresses: Folder job execution, concurrency control, job export/import
   - Avoids: Over-engineering Phase 1; thundering herd on startup
   - Rationale: These are differentiators, not table stakes; defer until core is solid

**Phase ordering rationale:**
- Engine before UI: services must work before dialogs can exercise them
- Execution before history UI: need real run data to test history display
- Core before advanced: folder jobs and concurrency add complexity that should not block basic scheduling

**Research flags for phases:**
- Phase 1: Credential encryption approach needs validation (DPAPI vs alternative)
- Phase 2: Output pruning strategy should be tested with realistic output sizes
- Phase 3: Cron builder UX -- may need iteration based on user feedback
- Phase 4: Concurrency control is standard patterns, unlikely to need research

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Cronos is the clear winner for .NET cron; well-documented, actively maintained |
| Features | HIGH | Table stakes are well-defined across industry; spec aligns with research |
| Architecture | HIGH | Follows existing app patterns; no novel architecture needed |
| Pitfalls | HIGH | Well-documented problems in Rundeck issues, WinForms threading guides |

## Gaps to Address

- DPAPI credential encryption: verify it works smoothly with existing ConfigurationService patterns (quick spike during Phase 1)
- CronExpressionDescriptor localization: confirm English output quality for common expressions
- Output file performance: benchmark directory listing speed with 1000+ files per job to validate pruning thresholds
- System sleep/hibernate behavior: test Timer behavior across sleep/wake cycles on Windows 11
