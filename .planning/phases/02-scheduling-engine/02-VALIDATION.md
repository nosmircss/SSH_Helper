---
phase: 2
slug: scheduling-engine
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-07
validated: 2026-03-07
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| **Config file** | SSH_Helper.Tests/SSH_Helper.Tests.csproj |
| **Quick run command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Scheduling" -x` |
| **Full suite command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Scheduling" -x`
- **After every plan wave:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | SCHD-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests" -x` | ✅ | ✅ green |
| 02-01-02 | 01 | 1 | SCHD-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests" -x` | ✅ | ✅ green |
| 02-01-03 | 01 | 1 | SCHD-03 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.MarkOneTimeCompleted" -x` | ✅ | ✅ green |
| 02-01-04 | 01 | 1 | SCHD-04 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.GetDescription" -x` | ✅ | ✅ green |
| 02-01-05 | 01 | 1 | SCHD-06 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.GetNextRun" -x` | ✅ | ✅ green |
| 02-01-06 | 01 | 1 | SCHD-07 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.DetectMissedRuns" -x` | ✅ | ✅ green |
| 02-02-01 | 02 | 2 | SCHD-05 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CronBuilderTests" -x` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [x] `SSH_Helper.Tests/Services/SchedulingServiceTests.cs` — 28 tests for SCHD-01, SCHD-02, SCHD-03, SCHD-04, SCHD-06, SCHD-07
- [x] `SSH_Helper.Tests/UI/CronBuilderControlTests.cs` — 33 tests for SCHD-05
- [x] `SSH_Helper.Tests/Utilities/InputValidatorCronTests.cs` — 16 tests for cron validation and future-date validation
- [x] NuGet install: Cronos 0.11.1 and CronExpressionDescriptor 2.45.0

*Additional coverage: `SSH_Helper.Tests/Services/SchedulingServiceMissedRunIntegrationTests.cs` — 14 integration tests for missed-run detection and persistence*

*Existing test infrastructure (xUnit, FluentAssertions, Moq) covers all framework needs.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Cron builder visual layout renders correctly | SCHD-05 | WinForms visual rendering | Open CronBuilderControl in test harness, verify dropdowns and presets display correctly |
| DateTimePicker blocks past dates visually | SCHD-02 | DateTimePicker min-date enforcement is visual | Open one-time schedule picker, verify past dates are greyed out |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 15s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** complete

---

## Validation Audit 2026-03-07
| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
| Total automated tests | 91 |
| Test files | 4 |
