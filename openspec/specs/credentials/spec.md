# credentials Specification

## Purpose
TBD - created by archiving change add-execution-safety-and-ux. Update Purpose after archive.
## Requirements
### Requirement: Credential Manager storage
When enabled, the application SHALL store and retrieve passwords using Windows Credential Manager and SHALL NOT persist plaintext passwords in config.

#### Scenario: Credential storage enabled
- **WHEN** the user enables credential storage and saves credentials
- **THEN** the password is stored in Credential Manager and the config contains only a reference key

### Requirement: SSH agent preference
When SSH agent preference is enabled, the application SHALL attempt agent-backed authentication before falling back to identity file or password.

#### Scenario: Agent unavailable
- **WHEN** agent preference is enabled but no agent is available
- **THEN** the application falls back to identity file or password authentication and reports the fallback non-blockingly

