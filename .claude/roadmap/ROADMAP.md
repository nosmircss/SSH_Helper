# SSH_Helper Evolution Roadmap: Network Operations Workbench

> Transform SSH_Helper from "run commands on hosts" into a comprehensive network operations workbench — the Postman/Bruno of infrastructure automation.

## Decisions

- **Platform**: Stay on WinForms .NET 8 (faster development, Windows-focused users)
- **Audience**: Both network engineers and DevOps/SREs
- **Priority themes**: Environments & DX, Multi-Protocol, Scheduling & Jobs
- **Scheduler**: Built into app (missed jobs skipped when app is closed)
- **HTTP/REST**: Unified workflow steps alongside SSH
- **Environments**: Swap host lists + variables per environment (presets stay global)
- **Editor**: Syntax highlighting, autocomplete, inline validation

## Milestone Status

| # | Milestone | Status | Design Doc | Dependencies |
|---|-----------|--------|------------|--------------|
| M1 | [Environments](M1-environments.md) | NOT STARTED | Complete | None |
| M2 | [Multi-Protocol Steps](M2-multi-protocol.md) | NOT STARTED | Complete | None |
| M3 | [Script Editor DX](M3-editor-dx.md) | NOT STARTED | Complete | None |
| M4 | [Job Scheduler](M4-scheduler.md) | NOT STARTED | Complete | Best after M1 |
| M5 | [Test Assertions](M5-assertions.md) | NOT STARTED | Sketch | After M2 + M4 |

## Implementation Order

```
M1: Environments ──────┐
                       ├──→ M4: Job Scheduler ──→ M5: Test Assertions
M2: Multi-Protocol ────┘         │
                                 │
M3: Script Editor DX ────────────┘ (independent, slot anywhere)
```

- **M1 and M2** have zero code dependencies and can be developed **in parallel**
- **M3** is fully independent and can slot in anywhere
- **M4** benefits from M1 (jobs target environments) but can work without it
- **M5** leverages M2 + M4 (assert in scheduled multi-protocol workflows)

## New Dependencies

| Package | Milestone | Purpose |
|---------|-----------|---------|
| `FastColoredTextBox` | M3 | WinForms code editor control |
| `Cronos` | M4 | Cron expression parsing |

All other features use built-in .NET libraries or existing Rebex/Json.NET/YamlDotNet.

## File Impact Summary

| Milestone | New Files | Modified Files |
|-----------|-----------|----------------|
| M1 | 3 | 5 |
| M2 | 5 | 3 |
| M3 | 6 | 4 |
| M4 | 6 | 4 |
| **Total** | **20 new files** | **~10 unique modified files** |

## Future Ideas (Parked)

These ideas were brainstormed but deferred beyond the current 5 milestones:

- **Collaboration**: Git-backed collection sharing, team library, audit trails
- **Monitoring**: Health check probes, alerting (Slack/Teams/PagerDuty), uptime dashboards
- **Visualization**: Diff views, table output parsing, topology viewer, performance charts
- **Network-specific**: Device type profiles, config backup/drift detection, firmware management
- **Advanced DX**: Script debugger (breakpoints/step-through), plugin system, CLI mode, REST API server
- **Security**: Compliance templates (CIS/NIST), approval workflows, command blocklist, session recording
- **Platform**: WPF migration, cross-platform via Avalonia, web UI via Blazor
