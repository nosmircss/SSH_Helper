## 1. Implementation
- [x] 1.1 Add OpenSpec delta for scheduler per-job timeout overrides and validate the change.
- [x] 1.2 Extend scheduler job models plus storage/export round-trip coverage for nullable command and connection timeout overrides.
- [x] 1.3 Add job-editor timeout override controls, inherited timeout guidance, prepopulation, and save/reset behavior.
- [x] 1.4 Extend job-editor validation and job execution timeout resolution precedence for all scheduler target types.
- [x] 1.5 Add focused automated coverage for timeout override validation, UI behavior, storage/export round-trip, and execution precedence.

## 2. Verification
- [x] 2.1 Run focused automated verification for scheduler timeout override tests.
- [x] 2.2 Validate change with `openspec validate update-scheduler-job-timeouts --strict --no-interactive`.
- [x] 2.3 Manual interactive verification of create/edit/reopen/Run Now timeout behavior remains pending from this CLI environment.
