## ADDED Requirements

### Requirement: License-free SFTP runtime backend
The scripting runtime SHALL execute `sftp` steps using a backend that does not require Rebex SFTP licensing.

The implementation SHALL use `SSH.NET` (`Renci.SshNet`) for SFTP transfer operations while preserving the existing `sftp` step contract and failure semantics.

#### Scenario: SFTP step runs without Rebex SFTP package
- **WHEN** an operator runs a script with an `sftp` step in a build that does not include `Rebex.Sftp`
- **THEN** the runtime can still execute the transfer using `SSH.NET`
- **AND** the step continues to populate `${into}` and `${into}_bytes` according to existing behavior