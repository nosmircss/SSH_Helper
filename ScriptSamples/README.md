# SSH Helper Script Samples

This directory contains YAML script samples that demonstrate the scripting capabilities of SSH Helper. These scripts were converted from the legacy dudescript format.

## Script Structure

Every YAML script follows this structure:

```yaml
---
name: Script Name
description: What this script does

vars:
  variable_name: "default_value"

steps:
  - send: command
  - print: "message"
  # ... more steps
```

## Available Commands

| Command | Description | Example |
|---------|-------------|---------|
| `send` | Execute SSH command | `send: show version` |
| `print` | Output a message | `print: "Status: ${var}"` |
| `wait` | Pause execution (seconds) | `wait: 5` |
| `set` | Set/modify variables | `set: counter = counter + 1` |
| `exit` | End script with status | `exit: success "Done"` |
| `extract` | Capture data via regex | See below |
| `if/else` | Conditional logic | See below |
| `foreach` | Loop over collection | See below |
| `while` | Loop while condition true | See below |
| `readfile` | Read lines from file | `readfile: path, into` |
| `writefile` | Write to file (text/json/csv) | `writefile: path, content` |
| `input` | Prompt user for input | `input: prompt, into` |
| `updatecolumn` | Update host grid column | `updatecolumn: column, value` |
| `log` | Output with log level | `log: message, level` |
| `webhook` | HTTP requests | `webhook: url, method, body` |

## Variables

### From CSV Grid
Any column in your CSV grid becomes a variable:
- `${Host_IP}` - The host IP address
- `${username}` - Username column
- `${password}` - Password column
- Any custom column like `${block}`, `${interface}`, etc.

### Script Variables
Define defaults in the `vars:` section:
```yaml
vars:
  timeout: 30
  interface: "GigabitEthernet0/0"
```

### Captured Variables
Use `capture:` to save command output:
```yaml
- send: show version
  capture: version_output
```

## Examples

### Simple Command with Capture
```yaml
- send: show ip interface brief
  capture: output
- print: "Output saved to variable"
```

### Extract Data with Regex
```yaml
- extract:
    from: output
    pattern: 'Version (\d+\.\d+)'
    into: version
- print: "Version: ${version}"
```

### Extract Multiple Matches
```yaml
- extract:
    from: output
    pattern: '(GigabitEthernet\S+)'
    into: interfaces
    match: all  # Creates a list

- foreach: iface in interfaces
  do:
    - print: "Found: ${iface}"
```

### Conditional Logic
```yaml
- if: status == "up"
  then:
    - print: "Interface is up"
  else:
    - print: "Interface is down"
```

### Condition Operators
- `==`, `!=` - String equality
- `>`, `>=`, `<`, `<=` - Numeric comparison
- `matches 'pattern'` - Regex match
- `contains "text"` - Substring check
- `is empty`, `is not empty` - Empty check
- `and`, `or`, `not` - Logical operators

### Loop with Counter
```yaml
- set: i = 0
- while: i < 10
  do:
    - print: "Iteration ${i}"
    - set: i = i + 1
```

### Custom Prompt (for login sequences)
```yaml
- send: login
  expect: '/Username:/'
- send: ${username}
  expect: '/Password:/'
```

## Directory Structure

```
ScriptSamples/
├── README.md                # This file
├── generic_health_check.yaml
├── bash/                    # Linux/Unix examples
│   ├── loop_interfaces.yaml
│   ├── interface_ip_check.yaml
│   └── system_info.yaml
├── cisco/                   # Cisco device examples
│   ├── ios_foreach_interfaces.yaml
│   ├── ios_show_version.yaml
│   ├── ios_backup_config.yaml
│   ├── ios_interface_status.yaml
│   ├── asa_shun_ip.yaml
│   └── asa_unshun_ip.yaml
├── checkpoint/              # Checkpoint firewall examples
│   └── block_ip.yaml
├── fortigate/               # FortiGate examples
│   ├── block_ip.yaml
│   ├── unblock_ip.yaml
│   ├── list_blocks.yaml
│   ├── show_local_users.yaml
│   └── system_status.yaml
└── generic/                 # Command reference examples (see below)
```

## Generic Samples (Command Reference)

The `generic/` folder contains samples specifically designed to demonstrate all scripting commands. Each sample shows realistic use cases:

| Sample | Commands Demonstrated | Scenario |
|--------|----------------------|----------|
| `json_inventory_report.yaml` | `json()`, dot notation, `writefile` (json), `log` | Collect device data into structured JSON report |
| `webhook_alerting.yaml` | `webhook`, `json()`, `log` levels, `if/else` | Monitor disk usage and send Slack/Teams alerts |
| `file_ip_processor.yaml` | `readfile`, `foreach`, `writefile` (append), arithmetic | Bulk ping test from IP list file |
| `interactive_config.yaml` | `input` (validation), `if/else`, `exit` statuses | Prompt-driven service configuration |
| `health_check_retry.yaml` | `while`, `wait`, `log` levels, `_iteration` | Retry loop with escalating log messages |
| `data_extraction.yaml` | `extract` (single/all), `updatecolumn`, `foreach` when | Extract system metrics to grid columns |
| `api_lookup.yaml` | `webhook` GET, `json.get/exists/len/items` | Query threat intel API, parse JSON response |
| `event_logging.yaml` | `writefile` (jsonl), `json()`, timestamps | Audit trail in JSON Lines format |
| `csv_export.yaml` | `writefile` (csv), headers, string building | Export metrics to CSV for spreadsheets |
| `compliance_report.yaml` | `json.merge/set/keys`, complex conditionals | Security compliance scan with JSON report |

## JSON Functions Quick Reference

Build and manipulate JSON data in your scripts:

```yaml
# Create JSON object
- set: data = json("key1", value1, "key2", value2)

# Build nested structure with dot notation
- set: report.server.name = ${hostname}
- set: report.server.ip = ${Host_IP}

# Read/write JSON values
- set: value = json.get(data, "path.to.key", "default")
- set: data = json.set(data, "path.to.key", newvalue)

# Merge objects
- set: combined = json.merge(base, overrides)

# Check existence and iterate
- if: json.exists(response, "error")
- foreach: item in json.items(response, "data.items")
```

## Using Scripts

1. Copy the script content into a preset
2. The system auto-detects YAML scripts (looks for `---` or `steps:`)
3. Add required variables as columns in your CSV grid
4. Execute against selected hosts

## Tips

- Use `print:` statements for debugging
- Use `capture:` to save output for later processing
- Use `if:` to handle success/failure conditions
- Use `exit: success "message"` or `exit: failure "message"` for clear status
- Test scripts on a single host first before running on multiple
