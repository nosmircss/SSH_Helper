---
phase: 1
slug: job-definitions-persistence
status: complete
nyquist_compliant: true
wave_0_complete: true
created: 2026-03-07
validated: 2026-03-07
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

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | Test File | Status |
|---------|------|------|-------------|-----------|-------------------|-----------|--------|
| 01-01-01 | 01 | 1 | JMGT-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Save_NewJob" --no-build` | JobStorageServiceTests.cs | COVERED |
| 01-01-02 | 01 | 1 | JMGT-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Save_ExistingJob" --no-build` | JobStorageServiceTests.cs | COVERED |
| 01-01-03 | 01 | 1 | JMGT-03 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Delete" --no-build` | JobStorageServiceTests.cs | COVERED |
| 01-01-04 | 01 | 1 | JMGT-04 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~PresetManagerJobReferenceTests.DeletePreset_Disables" --no-build` | PresetManagerJobReferenceTests.cs | COVERED |
| 01-01-05 | 01 | 1 | HOST-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobDefinitionTests.NewJobDefinition_Hosts" --no-build` | JobDefinitionTests.cs | COVERED |
| 01-01-06 | 01 | 1 | HOST-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.ImportHostsFromCsv" --no-build` | JobStorageServiceTests.cs | COVERED |
| 01-01-07 | 01 | 1 | HOST-03 | integration | manual-only (requires DataGridView) | N/A | MANUAL |
| 01-01-08 | 01 | 1 | HOST-04 | integration | manual-only (requires DataGridView) | N/A | MANUAL |
| 01-01-09 | 01 | 1 | CRED-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobDefinitionTests.NewJobDefinition_CredentialMode" --no-build` | JobDefinitionTests.cs | COVERED |
| 01-01-10 | 01 | 1 | CRED-02 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CredentialTargetsTests.JobPasswordTarget" --no-build` | CredentialTargetsTests.cs | COVERED |
| 01-01-11 | 01 | 1 | RELY-01 | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobStorageServiceTests.Save_ThenLoad" --no-build` | JobStorageServiceTests.cs | COVERED |

---

## Wave 0 Requirements

- [x] `SSH_Helper.Tests/Services/JobStorageServiceTests.cs` — 36 tests for JMGT-01 through JMGT-04, HOST-02, RELY-01
- [x] `SSH_Helper.Tests/Models/JobDefinitionTests.cs` — 12 tests for HOST-01, CRED-01, model validation
- [x] `SSH_Helper.Tests/Services/CredentialTargetsTests.cs` — 4 tests for CRED-02 (JobPasswordTarget)
- [x] `SSH_Helper.Tests/Utilities/ContentHasherTests.cs` — 6 tests for drift detection hashing
- [x] `SSH_Helper.Tests/Services/PresetManagerJobReferenceTests.cs` — 18 tests for referential integrity

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Copy hosts from main grid to job | HOST-03 | Requires DataGridView UI component | 1. Open app 2. Add hosts to main grid 3. Create job 4. Copy from main grid 5. Verify hosts appear in job |
| Manual host entry in job editor | HOST-04 | Requires DataGridView in editor dialog | 1. Open job editor 2. Manually type host entries 3. Save and verify persistence |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 15s (78 tests in 307ms)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** PASSED

## Validation Audit 2026-03-07
| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |
| Total automated tests | 78 |
| Requirements COVERED | 9 |
| Requirements MANUAL | 2 |
