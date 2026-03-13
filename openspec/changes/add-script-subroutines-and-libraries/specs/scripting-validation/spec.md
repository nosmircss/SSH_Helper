## ADDED Requirements
### Requirement: Subroutine and library shape validation
Script validation SHALL enforce the structural contract for executable scripts, library files, subroutines, and calls.

#### Scenario: Library file rejects executable sections
- **WHEN** a script sets `library: true`
- **AND** also includes `steps`, `vars`, or `imports`
- **THEN** validation reports that those sections are not allowed in library files

#### Scenario: Executable script still requires steps
- **WHEN** a script does not set `library: true`
- **AND** omits `steps`
- **THEN** validation reports that executable scripts require `steps`

#### Scenario: Call contract rejects unknown bindings
- **WHEN** a `call` step includes an unknown argument name or an output binding not declared by the target subroutine
- **THEN** validation reports the invalid binding before execution

#### Scenario: Return outside subroutine is rejected
- **WHEN** a script uses `return: true` outside subroutine execution context
- **THEN** validation reports that `return` is only valid inside subroutine steps

### Requirement: Import path restrictions
Script validation SHALL enforce the v1 import path policy.

#### Scenario: Relative import path is rejected
- **WHEN** an import path is not absolute
- **THEN** validation reports that imports must use absolute paths

#### Scenario: Blocked absolute import path is rejected
- **WHEN** an import path is absolute but fails existing script read-path validation
- **THEN** validation reports the read-path validation error for that import
