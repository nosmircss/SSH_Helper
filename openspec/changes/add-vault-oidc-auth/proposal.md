# Change: Add Vault OIDC Authentication

## Why
Vault integration currently supports Token, AppRole, LDAP, and Userpass authentication, but many organizations standardize on OIDC SSO and do not issue long-lived static credentials for desktop tools.

Without native OIDC support, users must acquire a Vault token outside SSH Helper and paste/store it manually, which creates friction and inconsistent login behavior.

## What Changes
- Add `OIDC` as a first-class Vault auth method in profile configuration.
- Add Vault settings UI for OIDC fields (auth mount, role, callback host/port/path) and an interactive browser sign-in action.
- Implement OIDC login flow in `VaultService` using PKCE, state validation, and localhost callback capture.
- Persist the resulting Vault token in Windows Credential Manager and reuse existing token lifecycle handling for re-authentication.
- Provide clear error messages for OIDC login cancellation, timeout, state mismatch, callback failure, and Vault auth exchange failure.
- Keep existing Token, AppRole, LDAP, and Userpass behaviors unchanged.

## Impact
- Affected specs: `credentials`
- Affected code:
  - `Models/VaultSettings.cs`
  - `SettingsDialog.cs`
  - `Services/Vault/VaultService.cs`
  - `Services/Vault/*` (new OIDC helper)
  - `UI/BrowserCallbackWebViewDialog.cs` or equivalent browser callback capture integration
  - `SSH_Helper.Tests/Vault/*`
  - `SSH_Helper.Tests/UI/*`
  - `CHANGELOG.md`
  - `SCRIPTING.md` (auth method reference)
