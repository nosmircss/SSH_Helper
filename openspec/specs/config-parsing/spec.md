# config-parsing Specification

## Purpose
TBD - created by archiving change add-config-parsing. Update Purpose after archive.
## Requirements
### Requirement: Configuration parsing command
The system SHALL provide a `parse` scripting command that transforms raw device configuration text into structured JSON data stored in a script variable.

#### Scenario: Parse FortiGate configuration
- **WHEN** a script executes a parse step with format "fortigate" and raw config text in the source variable
- **THEN** the destination variable contains a JSON object representing the hierarchical config structure

#### Scenario: Parse with section filter
- **WHEN** a script specifies `sections` parameter with a list of config paths
- **THEN** only the specified sections are parsed and included in the output

### Requirement: FortiGate parser format
The FortiGate parser SHALL transform `config/edit/set/next/end` directive syntax into nested JSON objects where:
- `config <path>` creates nested objects along the path
- `edit "name"` creates named entries within the current config section
- `set key value` assigns string values to keys
- Multi-value sets (e.g., `set member "a" "b"`) become arrays
- `unset` directives are omitted from output

#### Scenario: Parse interface table
- **WHEN** parsing FortiGate config containing `config system interface` with multiple `edit` blocks
- **THEN** the result contains `system.interface` as an object with interface names as keys

#### Scenario: Parse nested config
- **WHEN** parsing FortiGate config with nested `config` blocks inside `edit` blocks
- **THEN** the nested config is represented as a nested object under the parent entry

#### Scenario: Parse multi-value set
- **WHEN** parsing a set directive with multiple quoted values like `set member "obj1" "obj2"`
- **THEN** the key is assigned an array containing all values

### Requirement: Parsed data integration with JSON functions
Parsed configuration data SHALL be stored in a format compatible with the existing `json.*` functions, enabling:
- `json.get(var, "path")` to retrieve specific values
- `json.keys(var, "path")` to list section entries
- `json.items(var, "path")` to iterate over entries
- `writefile` with format `csv` to export data

#### Scenario: Access parsed config value
- **WHEN** a script uses `json.get(config, "system.global.hostname")`
- **THEN** the hostname value from the parsed FortiGate config is returned

#### Scenario: Iterate parsed config section
- **WHEN** a script uses `foreach iface in json.keys(config, "system.interface")`
- **THEN** the loop iterates over each interface name in the parsed config

### Requirement: Parser format extensibility
The parsing system SHALL support multiple configuration formats through a pluggable parser architecture, with format selection via the `format` parameter.

#### Scenario: Unknown format error
- **WHEN** a script specifies an unsupported format value
- **THEN** the parse command fails with a clear error message listing available formats

#### Scenario: Format parameter required
- **WHEN** a script omits the format parameter
- **THEN** the parse command fails with an error indicating format is required

