## ADDED Requirements
### Requirement: Vault OIDC Authentication
The application SHALL support HashiCorp Vault authentication using OIDC as a selectable Vault profile auth method.

#### Scenario: User signs in via OIDC successfully
- **GIVEN** Vault is enabled and a profile is configured with auth method `OIDC`
- **AND** the OIDC auth mount and role are valid
- **WHEN** the user initiates Vault authentication for that profile and completes browser sign-in
- **THEN** the application obtains a Vault client token from Vault
- **AND** stores the token in Windows Credential Manager for that profile
- **AND** Vault read/write/list operations for that profile can proceed without additional credential prompts

#### Scenario: OIDC callback fails validation
- **GIVEN** a Vault profile configured for OIDC
- **WHEN** the callback payload has an invalid or mismatched state
- **THEN** the application rejects the login attempt
- **AND** surfaces a clear, actionable error
- **AND** does not persist a Vault token

#### Scenario: OIDC login times out or is cancelled
- **GIVEN** a Vault profile configured for OIDC
- **WHEN** the user closes the browser flow or no valid callback is received before timeout
- **THEN** the application reports login cancellation/timeout without crashing
- **AND** does not persist a Vault token
- **AND** existing non-OIDC Vault profiles remain unaffected

## MODIFIED Requirements
### Requirement: Credential Manager storage
When enabled, the application SHALL store and retrieve passwords using Windows Credential Manager and SHALL NOT persist plaintext passwords in config.

For Vault integration, the application SHALL store Vault auth artifacts needed at runtime (including profile-scoped tokens for Token and OIDC flows) in Windows Credential Manager and SHALL NOT persist those secrets in config.

#### Scenario: Credential storage enabled
- **WHEN** the user enables credential storage and saves credentials
- **THEN** the password is stored in Credential Manager and the config contains only a reference key

#### Scenario: Vault OIDC token persistence
- **WHEN** a user successfully authenticates a Vault OIDC profile
- **THEN** the profile-scoped Vault token is stored in Credential Manager
- **AND** no OIDC secrets or Vault token are written as plaintext to config
