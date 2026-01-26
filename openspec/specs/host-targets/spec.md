# host-targets Specification

## Purpose
TBD - created by archiving change add-execution-safety-and-ux. Update Purpose after archive.
## Requirements
### Requirement: Hostname support
Host entries SHALL accept valid DNS hostnames with optional ports, in addition to IPv4 addresses.

#### Scenario: Hostname with port
- **WHEN** the user enters `router1.example.com:2222`
- **THEN** the host entry is accepted and parsed into host and port

### Requirement: Invalid host rejection
The system SHALL reject host entries that are neither valid IPv4 addresses nor valid hostnames.

#### Scenario: Invalid host string
- **WHEN** the user enters an invalid host string (e.g., `bad host!!`)
- **THEN** the entry is flagged as invalid and not executed

