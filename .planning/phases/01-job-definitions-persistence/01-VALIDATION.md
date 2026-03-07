---
phase: 1
slug: job-definitions-persistence
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-07
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| **Config file** | `SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| **Quick run command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Job" --no-build` |
| **Full suite command** | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Job" --no-build`
- **After every plan wave:** Run `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Before `/gsd:verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 01-01-01 | 01 | 1 | JMGT-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Create" --no-build` | Wave 0 | pending |
| 01-01-02 | 01 | 1 | JMGT-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Update" --no-build` | Wave 0 | pending |
| 01-01-03 | 01 | 1 | JMGT-03 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Delete" --no-build` | Wave 0 | pending |
| 01-01-04 | 01 | 1 | JMGT-04 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.EnableDisable" --no-build` | Wave 0 | pending |
| 01-01-05 | 01 | 1 | HOST-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobDefinitionTests.HostList" --no-build` | Wave 0 | pending |
| 01-01-06 | 01 | 1 | HOST-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.CsvImport" --no-build` | Wave 0 | pending |
| 01-01-07 | 01 | 1 | HOST-03 | integration | manual-only (requires DataGridView) | N/A | pending |
| 01-01-08 | 01 | 1 | HOST-04 | integration | manual-only (requires DataGridView) | N/A | pending |
| 01-01-09 | 01 | 1 | CRED-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobDefinitionTests.CredentialMode" --no-build` | Wave 0 | pending |
| 01-01-10 | 01 | 1 | CRED-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CredentialTargetsTests.JobTarget" --no-build` | Wave 0 | pending |
| 01-01-11 | 01 | 1 | RELY-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Persistence" --no-build` | Wave 0 | pending |

---

## Wave 0 Requirements

- [ ] `SSH_Helper.Tests/Services/JobStorageServiceTests.cs` — stubs for JMGT-01 through JMGT-04, HOST-02, RELY-01
- [ ] `SSH_Helper.Tests/Models/JobDefinitionTests.cs` — stubs for HOST-01, CRED-01, model validation
- [ ] `SSH_Helper.Tests/Services/CredentialTargetsTests.cs` — extend existing file for CRED-02 (JobPasswordTarget)
- [ ] `SSH_Helper.Tests/Utilities/ContentHasherTests.cs` — stubs for drift detection hashing

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Copy hosts from main grid to job | HOST-03 | Requires DataGridView UI component | 1. Open app 2. Add hosts to main grid 3. Create job 4. Copy from main grid 5. Verify hosts appear in job |
| Manual host entry in job editor | HOST-04 | Requires DataGridView in editor dialog | 1. Open job editor 2. Manually type host entries 3. Save and verify persistence |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
