## Context
SSH output is currently appended to the UI on every chunk via OutputReceived. Large or fast outputs can produce thousands of UI updates per second, which stalls rendering. We need to throttle UI updates while preserving full output capture for history.

## Goals / Non-Goals
- Goals:
  - Keep UI responsive by throttling output rendering to 50ms intervals.
  - Flush buffered output immediately when a command completes.
  - Preserve full output capture for history (no data loss).
  - Do not throttle debug output.
- Non-Goals:
  - Changing SSH output semantics or prompt detection.
  - Redesigning output history storage.

## Decisions
- Decision: Buffer UI output in Form1 and flush on a 50ms Windows Forms timer.
  - Rationale: Keeps UI work on the UI thread and reduces AppendText frequency.
- Decision: Append to the history buffer immediately on output receipt.
  - Rationale: History must remain complete and not be delayed or lost by throttling.
- Decision: Add a command-completed signal in SshShellSession and forward it via SshExecutionService.
  - Rationale: UI does not know prompt boundaries; a service-level signal provides a reliable flush trigger.
- Decision: Detect debug output by prefix ([DEBUG], [SSH DEBUG]) and bypass throttling.
  - Rationale: Debug output is low volume and should remain immediate for troubleshooting.

## Risks / Trade-offs
- Increased complexity in output routing and thread-safety.
- If command completion signals are missed, the timer still flushes output within 50ms.

## Migration Plan
- No data migration required.

## Open Questions
- None.
