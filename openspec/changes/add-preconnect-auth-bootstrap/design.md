## Context

The current script execution flow decides whether SSH is required and, when required, logs in before running any script steps. This prevents mixed scripts from running local setup actions that must happen first (for example obtaining an ephemeral cert/key pair and using it to authenticate). The change crosses parser, validation, runtime orchestration, and both pooled/non-pooled SSH login paths.

## Goals / Non-Goals

**Goals:**
- Add a first-class pre-SSH execution phase for YAML scripts.
- Support dynamic per-host SSH auth overrides produced by preconnect logic.
- Preserve existing script semantics when `preconnect` is omitted.
- Keep behavior consistent across pooled and non-pooled execution.
- Ensure observability without leaking secrets.

**Non-Goals:**
- Reworking the full script command model or introducing arbitrary multi-phase execution beyond preconnect + main steps.
- Replacing existing SSH auth precedence outside preconnect override hooks.
- Supporting SSH session-dependent commands inside `preconnect`.

## Decisions

- Decision: Add top-level `preconnect` section to script model.
  - Rationale: Explicit author intent and clear ordering guarantee before SSH login.
  - Alternatives considered: Infer phase from command types; rejected due to ambiguity and backward-compatibility risk.

- Decision: Execute `preconnect` per host using local-only context (`Session = null`) before SSH login for SSH-required scripts.
  - Rationale: Cert and token bootstrap commonly depends on host variables and must remain host-scoped.
  - Alternatives considered: One global pre-step per run; rejected because host-specific cert issuance is a core use case.

- Decision: Define reserved override variables consumed by SSH login (`_ssh_identity_file`, `_ssh_identity_passphrase`, `_ssh_username`, `_ssh_password`).
  - Rationale: Minimal model churn, no mandatory schema changes to host rows, and easy scripting ergonomics.
  - Alternatives considered: Mutate `HostConnection` directly from commands; rejected as too coupled and harder to validate.

- Decision: Restrict `preconnect` to commands that do not require an SSH session.
  - Rationale: Prevent recursive/invalid execution states and keep phase deterministic.
  - Alternatives considered: Allow all commands and fail at runtime; rejected in favor of early validation.

- Decision: Keep connection pooling keying/auth behavior aligned with resolved override values.
  - Rationale: Prevent stale-session reuse across hosts/runs when preconnect yields different credentials.
  - Alternatives considered: Disable pooling for preconnect scripts; rejected because it regresses performance.

## Risks / Trade-offs

- [Auth override leakage in logs/history] -> Redact reserved override variables and never emit raw values in debug/output.
- [Pooling mismatch with dynamic auth] -> Include effective auth identity inputs in pool key/session selection logic.
- [User confusion about phase boundaries] -> Document `preconnect` constraints and add validation errors for unsupported commands.
- [Execution overhead] -> Preconnect adds an extra pass; mitigated by running only when section is present.

## Migration Plan

- Implement parser/model updates for optional `preconnect`.
- Add validation for supported preconnect command set and shape.
- Add orchestration path that executes preconnect and computes effective auth before login.
- Wire effective auth into pooled and non-pooled login paths.
- Add tests and docs, then ship as backward-compatible enhancement.

Rollback:
- If severe regression occurs, disable preconnect execution gate in orchestration and ignore section until fixed.

## Open Questions

- Should preconnect be allowed for scripts that do not require SSH (for consistency), or ignored in local-only mode?
- Do we need optional cleanup/postconnect hooks for temporary cert files in this same change, or defer?
