---
phase: 3
slug: execution-pipeline
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-07
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| **Config file** | `SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| **Quick run command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecution" --no-build -q` |
| **Full suite command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExecution" --no-build -q`
- **After every plan wave:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 0 | EXEC-01 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~JobExecutionServiceTests" --no-build -q` | Wave 0 | pending |
| 03-01-02 | 01 | 0 | EXEC-02 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~RunNow" --no-build -q` | Wave 0 | pending |
| 03-01-03 | 01 | 0 | EXEC-03 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~Cancel" --no-build -q` | Wave 0 | pending |
| 03-01-04 | 01 | 0 | EXEC-04 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~Concurrency" --no-build -q` | Wave 0 | pending |
| 03-01-05 | 01 | 0 | EXEC-05 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~Queue" --no-build -q` | Wave 0 | pending |
| 03-01-06 | 01 | 0 | EXEC-06 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~FolderJob" --no-build -q` | Wave 0 | pending |
| 03-01-07 | 01 | 0 | EXEC-07 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~FolderExecution" --no-build -q` | Wave 0 | pending |
| 03-01-08 | 01 | 0 | RELY-02 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~CrashRecovery" --no-build -q` | Wave 0 | pending |
| 03-01-09 | 01 | 0 | RELY-03 | unit | `dotnet test SSH_Helper.Tests --filter "FullyQualifiedName~TimerIndependent" --no-build -q` | Wave 0 | pending |

*Status: pending / green / red / flaky*

---

## Wave 0 Requirements

- [ ] `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs` — stubs for EXEC-01 through EXEC-07, RELY-02, RELY-03
- [ ] Test infrastructure: Mock/stub for SshExecutionService (Moq already available)
- [ ] Test infrastructure: Mock for ICredentialProvider (already used in JobStorageServiceTests)

*Testing strategy: Unit-test the orchestration logic — timer evaluation, concurrency gating, queue FIFO, duplicate detection, crash recovery, credential resolution — using mocked SshExecutionService and JobStorageService. No real SSH connections needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Timer fires and triggers jobs in real time | RELY-03 | Requires WinForms message pump and real timer | Start app, create job with 1-min cron, observe automatic execution |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
