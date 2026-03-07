---
phase: 4
slug: history-output
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-07
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.7.0 with FluentAssertions 6.12.0 and Moq 4.20.70 |
| **Config file** | SSH_Helper.Tests/SSH_Helper.Tests.csproj |
| **Quick run command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryService" -x` |
| **Full suite command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobHistoryService" -x`
- **After every plan wave:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 04-01-01 | 01 | 1 | HIST-01 | unit | `dotnet test --filter "FullyQualifiedName~JobRunRecord"` | No - W0 | pending |
| 04-01-02 | 01 | 1 | HIST-02 | unit | `dotnet test --filter "FullyQualifiedName~JobHistoryService"` | No - W0 | pending |
| 04-01-03 | 01 | 1 | HIST-03 | unit | `dotnet test --filter "FullyQualifiedName~JobHistoryService"` | No - W0 | pending |
| 04-01-04 | 01 | 1 | HIST-04 | unit | `dotnet test --filter "FullyQualifiedName~JobHistoryService"` | No - W0 | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `SSH_Helper.Tests/Services/JobHistoryServiceTests.cs` — stubs for HIST-01, HIST-02, HIST-03, HIST-04
- [ ] `SSH_Helper.Tests/Models/JobRunRecordTests.cs` — model validation tests
- [ ] Framework install: None needed — xUnit, FluentAssertions, Moq already in test project
- [ ] Test pattern: Follow existing `HistoryStorageServiceTests` pattern — temp directory isolation, IDisposable cleanup

*Existing infrastructure covers framework requirements.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| History panel UI renders run records | HIST-01 | WinForms visual | Open app, run a job, verify history panel shows record |
| Output viewer displays full SSH output | HIST-02 | WinForms visual | Click a history entry, verify output is readable |
| Search/filter UI in output viewer | HIST-04 | WinForms interactive | Use search box to filter output results |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
