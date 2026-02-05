# Design: Configuration Parsing

## Context
SSH_Helper users work with FortiGate firewalls and need to extract configuration data for reporting, auditing, and automation. The existing `extract` command uses regex, which cannot handle FortiGate's hierarchical config format.

## Goals / Non-Goals

**Goals:**
- Parse FortiGate `config/edit/set/next/end` syntax into JSON
- Integrate with existing `json.*` functions for data access
- Support iteration over config sections (interfaces, policies, etc.)
- Enable CSV export via existing `writefile` command

**Non-Goals:**
- Config validation (we parse, not validate)
- Config modification/generation
- Real-time config monitoring

## Decisions

### Decision 1: New `parse` command vs extending existing

**Choice:** New `parse` command

**Rationale:**
- `extract` is regex-focused; config parsing is structural
- `set` is already 1900+ lines; adding parsing would bloat it further
- Clear separation of concerns

### Decision 2: Parser architecture

**Choice:** Pluggable `IConfigParser` interface with factory pattern

**Rationale:**
- Enables future parsers (Cisco IOS, Juniper, PAN-OS)
- Each parser encapsulates vendor-specific logic
- Easy to test parsers in isolation

### Decision 3: Output format

**Choice:** JSON (Dictionary<string, object> and List<object>)

**Rationale:**
- Direct compatibility with existing `json.*` functions
- No new syntax needed for access
- Users already familiar with JSON path notation

### Decision 4: Value type handling

**Choice:** Keep all values as strings

**Rationale:**
- FortiGate values have ambiguous types (is "30" a number or string?)
- Avoids type-guessing errors
- Users can convert with existing functions if needed

### Decision 5: `unset` directive handling

**Choice:** Omit from output

**Rationale:**
- Simpler, cleaner JSON output
- `unset` resets to default; absence of key is semantically equivalent

### Decision 6: Format detection

**Choice:** Explicit only - user must specify `format: fortigate`

**Rationale:**
- Clear and predictable behavior
- Avoids misdetection issues
- User knows their device type

## Data Model

**FortiGate config input:**
```
config system interface
    edit "wan1"
        set vdom "root"
        set ip 10.0.0.1 255.255.255.0
    next
end
```

**Parsed JSON output:**
```json
{
  "system": {
    "interface": {
      "wan1": {
        "vdom": "root",
        "ip": "10.0.0.1 255.255.255.0"
      }
    }
  }
}
```

## Command Syntax

```yaml
- parse:
    format: fortigate
    from: _output          # Source variable with raw config
    into: config           # Destination variable name
    sections:              # Optional: only parse these sections
      - system interface
      - firewall policy
```

## Architecture

```
Services/Scripting/
├── Commands/
│   └── ParseCommand.cs          # New command handler
├── Parsers/
│   ├── IConfigParser.cs         # Parser interface
│   ├── FortiGateParser.cs       # FortiGate-specific parser
│   └── ParserFactory.cs         # Factory for parser selection
```

## Parser Interface

```csharp
public interface IConfigParser
{
    string FormatName { get; }
    Dictionary<string, object> Parse(string configText);
    Dictionary<string, object> Parse(string configText, IEnumerable<string>? sections);
}
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Large configs (50K+ lines) | Optional `sections` filter for targeted parsing |
| FortiOS version variations | Test against multiple versions |
| Nested config blocks | Stack-based parser handles arbitrary depth |

## Example Workflow

```yaml
name: FortiGate Interface Report
description: Export interface configuration to CSV

steps:
  # Capture configuration
  - send: show full-configuration system interface
    capture: raw_config
    suppress: true

  # Parse the configuration
  - parse:
      format: fortigate
      from: raw_config
      into: config

  # Get interface list and iterate
  - set: interfaces = json.keys(config, "system.interface")
  - set: report = json([])

  - foreach: iface in interfaces
    do:
      - set: ip = json.get(config, "system.interface.${iface}.ip", "N/A")
      - set: row = json("name", "${iface}", "ip", "${ip}")
      - set: report = json.push(report, ${row})

  # Write CSV
  - writefile:
      path: "C:/reports/interfaces.csv"
      format: csv
      headers: [name, ip]
      content: ${report}
```
