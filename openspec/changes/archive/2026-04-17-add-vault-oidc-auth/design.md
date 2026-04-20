## Context
SSH Helper already has browser-callback primitives and Vault profile auth infrastructure. The new OIDC mode should integrate with existing Vault token caching/expiry logic instead of introducing a parallel credential path.

## Goals / Non-Goals
- Goals:
  - Support Vault OIDC auth as a profile auth method with interactive user login.
  - Preserve existing auth methods with no behavior regression.
  - Store only resulting Vault tokens in Credential Manager; avoid new plaintext secret storage in config.
  - Use secure OIDC flow controls (PKCE + state).
- Non-Goals:
  - Generic OAuth provider framework unrelated to Vault.
  - Reworking script execution semantics.
  - New external Vault SDK dependency.

## Decisions
- Decision: Add `VaultAuthMethod.Oidc` to the existing enum and profile model.
  - Rationale: Keeps profile-level auth selection consistent across UI and service layers.
  - Alternatives considered: Separate OIDC profile type. Rejected due to extra branching and migration complexity.

- Decision: Use localhost callback listener with randomly generated state and PKCE verifier/challenge.
  - Rationale: Aligns with native desktop app best practices and existing browser callback patterns.
  - Alternatives considered: Embedded WebView-only flow. Rejected as less compatible with enterprise SSO policies and external browser requirements.

- Decision: Exchange OIDC authorization output through Vault auth endpoint, then persist the returned Vault token exactly like other auth methods.
  - Rationale: Reuses existing token lifecycle and minimizes downstream change surface.

## Risks / Trade-offs
- Risk: Corporate endpoint protection may block local callback ports.
  - Mitigation: Configurable callback port with clear diagnostics and timeout handling.

- Risk: SSO policies may require system browser instead of embedded views.
  - Mitigation: Keep flow browser-launch abstraction so default can be system browser with callback capture.

- Risk: Token expiry behavior differs by Vault role/policy.
  - Mitigation: Continue using current TTL-driven refresh threshold and trigger OIDC re-login when needed.

## Migration Plan
1. Add new enum value and OIDC fields with safe defaults to preserve config compatibility.
2. Update settings UI to edit OIDC fields and validate required values.
3. Add OIDC auth branch in Vault service.
4. Add tests for serialization, service auth flow handling, and UI field visibility/validation.
5. Update docs/changelog.

## Open Questions
- Should the default callback port be fixed (for simpler allowlisting) or random (for collision avoidance)?
- Should OIDC re-authentication be automatic when token expires during background jobs, or fail fast with a re-login message?
