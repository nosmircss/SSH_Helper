## Context
Flow Canvas currently mixes raw YAML snippet reuse with partial generated YAML paths, and maps debug/runtime state using a flat step index that does not uniquely identify nested steps.

## Goals / Non-Goals
- Goals:
  - Eliminate silent export loss.
  - Guarantee deterministic node correlation for nested runtime/debug events.
  - Ensure run/test-step triggers are unified and atomic.
  - Restore expected interaction behavior in the canvas editor.
- Non-Goals:
  - Replacing WinForms/WebView2 architecture.
  - Multi-host test-step execution.

## Decisions
- Decision: Add a canonical execution identity (`StepPath`) carried through executor and debug events.
  - Alternatives considered: Continue flat index mapping; rejected due nested collisions.
- Decision: Add `execute-canvas` as the single execution trigger message.
  - Alternatives considered: Keep staged `apply-yaml` + `run/test-step`; rejected due race/parity issues.
- Decision: Export returns structured diagnostics and host enforces fail-fast on invalid graph export.
  - Alternatives considered: Best-effort export with warning logs; rejected due silent data loss.

## Risks / Trade-offs
- Risk: Container/nested authoring parity may need incremental migration from snippet-backed behavior.
  - Mitigation: Keep explicit diagnostics and prevent silent drops; add targeted tests before broad expansion.
- Risk: Event contract expansion can break older callers.
  - Mitigation: Keep compatibility fields temporarily (`StepIndex`) during transition.

## Migration Plan
1. Add compatibility-aware contracts (`StepPath` plus legacy fields).
2. Route canvas run/test through `execute-canvas` while keeping legacy aliases temporarily.
3. Remove old aliases in a follow-up cleanup once parity is verified.

## Open Questions
- None blocking implementation in this change.
