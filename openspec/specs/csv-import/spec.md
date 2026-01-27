# csv-import Specification

## Purpose
TBD - created by archiving change refactor-execution-and-io. Update Purpose after archive.
## Requirements
### Requirement: Multiline CSV fields
The system SHALL correctly import CSV files with embedded newlines inside quoted fields.

#### Scenario: Quoted multiline field
- **WHEN** a CSV file contains a quoted field with a newline
- **THEN** the imported value preserves the newline and row boundaries remain correct

