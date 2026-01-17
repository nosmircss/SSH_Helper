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
├── README.md           # This file
├── bash/               # Linux/Unix examples
│   ├── loop_interfaces.yaml
│   └── interface_ip_check.yaml
├── cisco/              # Cisco device examples
│   ├── ios_foreach_interfaces.yaml
│   ├── ios_show_version.yaml
│   ├── asa_shun_ip.yaml
│   └── asa_unshun_ip.yaml
├── checkpoint/         # Checkpoint firewall examples
│   └── block_ip.yaml
└── fortigate/          # FortiGate examples
    ├── block_ip.yaml
    └── unblock_ip.yaml
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
