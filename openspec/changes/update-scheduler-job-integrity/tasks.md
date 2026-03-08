## 1. Credential integrity
- [x] 1.1 Update scheduler job editing to save stored credentials into Windows Credential Manager while keeping plaintext out of `jobs.json`.
- [x] 1.2 Reload stored-credential jobs with username and stored-password-presence state so blank password saves preserve the current secret.

## 2. Drift and import safety
- [x] 2.1 Recompute scheduler drift state when referenced presets or folder membership/content changes after a job was saved.
- [x] 2.2 Normalize imported jobs with missing targets into a disabled state with explicit disabled reasons.

## 3. Form integration
- [x] 3.1 Route job-list Run Now through Form1 attribution tracking so notifications use the correct manual-run prefix.
- [x] 3.2 Enforce single-instance reuse for the modeless scheduler dialog opened from the menu or status bar.

## 4. Verification
- [x] 4.1 Add focused tests for stored credential round-trip, drift activation, and missing-target import normalization.
- [x] 4.2 Add focused verification for run-now attribution and single-instance scheduler dialog behavior.
- [x] 4.3 Validate change with `openspec validate update-scheduler-job-integrity --strict --no-interactive`.
