## ADDED Requirements

### Requirement: Environment CSV freshness awareness
The system SHALL persist enough metadata with each environment's remembered CSV reference to determine whether that environment's stored host snapshot still matches the file on disk when the environment becomes active.

#### Scenario: Switching to an environment whose CSV changed on disk
- **WHEN** environment `Lab A` remembers `fortigate.csv`
- **AND** `fortigate.csv` has changed on disk since `Lab A` last captured its host snapshot
- **AND** an operator switches to `Lab A`
- **THEN** the system detects that the remembered host snapshot is stale before loading it into the grid
- **AND** the operator is offered a reload-from-disk choice

#### Scenario: Switching to an environment whose CSV is missing on disk
- **WHEN** an environment remembers `fortigate.csv`
- **AND** that file no longer exists on disk
- **AND** an operator switches to the environment
- **THEN** the environment's stored host snapshot remains available
- **AND** the hosts header indicates that the remembered file is missing on disk

#### Scenario: Switching to an environment whose CSV still matches disk
- **WHEN** an environment remembers `fortigate.csv`
- **AND** the file on disk still matches the environment's remembered host snapshot
- **AND** an operator switches to the environment
- **THEN** the host grid loads without any stale-file warning
- **AND** the hosts header shows the file reference without a disk-drift warning
