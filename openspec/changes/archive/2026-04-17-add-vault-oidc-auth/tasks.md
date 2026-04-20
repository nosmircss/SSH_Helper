## 1. Implementation
- [x] 1.1 Extend Vault profile model with `OIDC` auth method and required OIDC configuration fields.
- [x] 1.2 Add Vault settings UI controls for OIDC and include profile validation.
- [x] 1.3 Implement OIDC login flow in Vault service (PKCE, state, browser launch, callback capture, token exchange).
- [x] 1.4 Persist OIDC-issued Vault token in Credential Manager and integrate with existing token expiry/re-auth logic.
- [x] 1.5 Add user-facing error handling for OIDC timeout/cancel/failure paths.
- [x] 1.6 Update docs (`SCRIPTING.md`, `CHANGELOG.md`) to include OIDC auth method behavior and constraints.

## 2. Validation
- [x] 2.1 Add unit tests for `VaultSettings` serialization/deserialization with OIDC fields.
- [x] 2.2 Add `VaultService` tests covering OIDC auth success and representative failures.
- [x] 2.3 Add UI tests for OIDC auth method panel visibility and validation behavior.
- [x] 2.4 Run focused test suite for Vault/UI changes.
- [x] 2.5 Run OpenSpec validation for `add-vault-oidc-auth` in strict mode.
