## Context
Cancellation already exists as a cooperative signal through per-run `CancellationTokenSource` instances, but downstream code collapses the result into ordinary failure states. The fix spans manual execution, folder execution, scheduler aggregation, persisted history payloads, and UI rendering.

## Goals / Non-Goals
- Goals:
  - Preserve cancellation as an explicit outcome from low-level execution through UI and history.
  - Keep partial output and execution details for cancelled runs.
  - Add scheduled-job cancellation only in the scheduler Job List UI.
- Non-Goals:
  - Add transport-level hard abort for synchronous SSH connect/login work.
  - Redesign scheduler list layout or introduce broad visual restyling.
  - Route scheduled-job cancellation through the main-form Stop button.

## Decisions
- Decision: Add `WasCancelled` as an additive flag on execution, history, and scheduler result models.
  - Alternatives considered: reusing `Success = false` with string parsing, or introducing new enums across all payloads. String parsing is brittle, and a broad enum migration is larger risk than needed for this pass.
- Decision: Treat a user stop request as an overall cancelled run even if some hosts had already failed.
  - Alternatives considered: prioritizing failure when any host fails. That hides the user intent and makes stop behavior unpredictable in mixed-result runs.
- Decision: Keep scheduler cancellation scoped to the Job List `Cancel` action.
  - Alternatives considered: reusing the main-form Stop button for scheduled runs. That creates ambiguous ownership between manual and scheduled execution paths.

## Risks / Trade-offs
- Persisted JSON payloads must remain backward compatible with older history files. Additive bool fields mitigate this, but custom lazy-load readers need explicit updates.
- Scheduler failure streak and one-time auto-disable logic currently key off `Success`; cancelled runs must bypass failure-specific handling without weakening real failure behavior.
- Folder execution still stops cooperatively, so some already-started work can complete after cancel is requested.

## Migration Plan
1. Add additive cancellation flags to the relevant models and persistence readers.
2. Propagate `WasCancelled` from SSH execution through manual/folder and scheduler completion paths.
3. Update history rendering and scheduler UI/actions to distinguish cancelled runs.
4. Add focused tests for real cancellation paths and persistence round-trips.

## Open Questions
- None for this pass.
