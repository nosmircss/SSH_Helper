---
phase: 5
slug: scheduler-ui-integration
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-03-07
revised: 2026-03-07
---

# Phase 5 -- Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| **Config file** | SSH_Helper.Tests/SSH_Helper.Tests.csproj |
| **Quick run command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Phase05" -x` |
| **Full suite command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Phase05" -x`
- **After every plan wave:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 05-01-T1 | 01 | 1 | JMGT-05, JMGT-06 | unit (TDD) | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExportService" -x` | No - W0 | pending |
| 05-01-T2 | 01 | 1 | UI-02, UI-03 | unit (TDD) | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobEditorValidation\|FullyQualifiedName~SchedulerNotification" -x` | No - W0 | pending |
| 05-02-T1 | 02 | 1 | UI-01 | build | `dotnet build SSH_Helper.sln` | n/a (UI dialog) | pending |
| 05-02-T2 | 02 | 1 | JMGT-06 | build | `dotnet build SSH_Helper.sln` | n/a (UI dialog) | pending |
| 05-03-T1 | 03 | 1 | UI-02 | build | `dotnet build SSH_Helper.sln` | n/a (UI dialog) | pending |
| 05-03-T2 | 03 | 1 | UI-02 | unit+build | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobEditorValidation" -x && dotnet build SSH_Helper.sln` | Yes (from 01-T2) | pending |
| 05-04-T1 | 04 | 2 | UI-01, JMGT-05 | build | `dotnet build SSH_Helper.sln` | n/a (UI dialog) | pending |
| 05-04-T2 | 04 | 2 | JMGT-06 | build | `dotnet build SSH_Helper.sln` | n/a (UI dialog) | pending |
| 05-05-T1 | 05 | 3 | UI-03 | unit+build | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotification" -x && dotnet build SSH_Helper.sln` | Yes (from 01-T2) | pending |
| 05-05-T2 | 05 | 3 | UI-03 | unit+build | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotification" -x && dotnet build SSH_Helper.sln` | Yes (from 01-T2) | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

All Wave 0 test files are created in **Plan 01**:

- [ ] `SSH_Helper.Tests/Services/JobExportServiceTests.cs` -- Plan 01, Task 1 (TDD: tests for JMGT-05, JMGT-06 export/import serialization)
- [ ] `SSH_Helper.Tests/UI/SchedulerNotificationTests.cs` -- Plan 01, Task 2 (TDD: tests for UI-03 notification string formatting via SchedulerNotificationFormatter)
- [ ] `SSH_Helper.Tests/UI/JobEditorValidationTests.cs` -- Plan 01, Task 2 (TDD: tests for UI-02 validation logic via JobEditorValidator)

**Extracted helper classes (also Plan 01, Task 2):**
- [ ] `Utilities/JobEditorValidator.cs` -- pure static validation methods consumed by JobEditorDialog (Plan 03)
- [ ] `Utilities/SchedulerNotificationFormatter.cs` -- pure static formatting methods consumed by Form1 (Plan 05)

*Note: UI dialogs (Plans 02, 03, 04) contain layout/control code that is not unit-testable. Testable logic (validation, formatting) has been extracted into static helpers. UI behavior is covered by the Plan 05 checkpoint:human-verify task.*

---

## Nyquist Sampling Continuity

| Consecutive Tasks | Automated Test Coverage |
|-------------------|------------------------|
| 01-T1, 01-T2 | Both run unit tests (TDD) |
| 02-T1, 02-T2, 03-T1 | Build-only (UI dialogs -- no testable logic remaining) |
| 03-T2 | Unit test (JobEditorValidation) + build -- breaks build-only streak |
| 04-T1, 04-T2 | Build-only (UI dialog) |
| 05-T1 | Unit test (SchedulerNotification) + build -- breaks build-only streak |
| 05-T2 | Unit test (SchedulerNotification) + build |

**Max consecutive build-only tasks:** 3 (02-T1, 02-T2, 03-T1) -- acceptable, as these are pure UI layout with all testable logic extracted.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Job list grid renders correctly with proper columns and styling | UI-01 | Visual layout verification | Open Job List dialog, verify columns (Name, On, Schedule, Next Run, Last Result, Target) display correctly with dark/light theme |
| Job editor tabs navigate and display controls properly | UI-02 | Visual layout verification | Open Job Editor, switch between General/Hosts/Credentials/Advanced tabs, verify all controls render |
| Status bar click opens Job List dialog | UI-03 | UI interaction verification | Click scheduler status in status bar, verify Job List dialog opens or comes to front |
| CronBuilderControl embeds correctly in editor | UI-02 | Visual integration verification | Open Job Editor General tab, verify CronBuilderControl renders and syncs with cron expression |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3+ consecutive tasks without automated verify (or only pure UI layout tasks)
- [x] Wave 0 covers all MISSING references -- all created in Plan 01
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** revised -- checker issues addressed
