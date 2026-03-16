## Context
The scheduler already stores credential mode, target hashes, and import preview metadata, but several flows stop short of the final persisted behavior. The follow-up change needs to preserve secure credential handling, activate drift blocking from real preset/folder mutations, and keep Form1 integration consistent with modeless scheduler UX.

## Goals / Non-Goals
- Goals:
  - Persist scheduler stored credentials securely through Windows Credential Manager.
  - Mark jobs drifted when saved target snapshots no longer match current preset or folder content.
  - Save imported missing-target jobs safely in a disabled state.
  - Keep run-now notifications and the scheduler window lifecycle consistent across entry points.
- Non-Goals:
  - Change scheduler timing, concurrency, or retention semantics.
  - Introduce a new credential backend beyond Windows Credential Manager.
  - Reconcile or archive older scheduler proposal artifacts.

## Decisions
- Decision: Scheduler stored credentials remain keyed by job ID in Windows Credential Manager.
  - The job editor loads the stored username.
  - The editor never reveals the stored password text.
  - Saving with a blank password preserves the existing stored password for that job.
- Decision: Drift state is recomputed from the existing saved content hashes.
  - Preset jobs drift when the target preset content hash changes.
  - Folder jobs drift when the saved preset-hash map no longer matches the current direct-child preset set or any saved preset content changes.
- Decision: Imported jobs with missing targets are saved disabled with a `DisabledReason` that identifies the missing preset or folder.
- Decision: Form1 owns both run-now attribution and the reusable modeless scheduler dialog instance so all entry points share the same notification and window lifecycle behavior.

## Risks / Trade-offs
- Risk: Blank-password-save semantics can be misunderstood.
  - Mitigation: the editor should explicitly communicate that leaving the password field blank preserves the current stored secret.
- Risk: Drift recomputation for folder jobs can become noisy if every preset mutation scans all jobs.
  - Mitigation: only reevaluate jobs affected by the changed preset or folder path.

## Migration Plan
- Existing stored-mode jobs without credentials remain loadable but continue failing until credentials are saved once through the updated editor flow.
- Existing imports are unaffected; new imports and optional repair paths normalize missing-target jobs into disabled state.

## Open Questions
- None.
