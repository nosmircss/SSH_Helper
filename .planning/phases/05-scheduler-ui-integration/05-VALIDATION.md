---
phase: 5
slug: scheduler-ui-integration
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-07
---

# Phase 5 — Validation Strategy

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
| 05-01-xx | 01 | 1 | JMGT-05, JMGT-06 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExportService" -x` | No - W0 | pending |
| 05-02-xx | 02 | 1 | UI-03 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotification" -x` | No - W0 | pending |
| 05-03-xx | 03 | 2 | UI-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobEditorDialog" -x` | No - W0 | pending |
| 05-04-xx | 04 | 2 | UI-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialog" -x` | No - W0 | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `SSH_Helper.Tests/Services/JobExportServiceTests.cs` — stubs for JMGT-05, JMGT-06 (export/import serialization)
- [ ] `SSH_Helper.Tests/UI/SchedulerNotificationTests.cs` — stubs for UI-03 (notification string formatting)
- [ ] `SSH_Helper.Tests/UI/JobEditorValidationTests.cs` — stubs for UI-02 (validation logic as static/internal methods)

*Note: UI-01 (Job List grid population) tests require WinForms STA thread. Test data assembly logic as pure methods separate from grid binding.*

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

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
