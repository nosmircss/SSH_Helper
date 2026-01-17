# SSH Helper Scripting Language Documentation

SSH Helper supports a powerful YAML-based scripting language for automating complex SSH workflows. This document provides complete reference documentation for all scripting features.

## Table of Contents

1. [Script Structure](#script-structure)
2. [Commands](#commands)
   - [send](#send---execute-ssh-commands)
   - [print](#print---output-messages)
   - [wait](#wait---pause-execution)
   - [set](#set---variable-assignment)
   - [extract](#extract---regex-data-extraction)
   - [if](#if---conditional-execution)
   - [foreach](#foreach---loop-over-collections)
   - [while](#while---conditional-loop)
   - [exit](#exit---terminate-script)
   - [readfile](#readfile---read-text-files)
   - [writefile](#writefile---write-text-files)
   - [input](#input---prompt-for-user-input)
   - [updatecolumn](#updatecolumn---update-host-table-column)
3. [Variables](#variables)
4. [Expressions and Conditions](#expressions-and-conditions)
5. [Error Handling](#error-handling)
6. [Debug Mode](#debug-mode)
7. [Examples](#examples)

---

## Script Structure

Scripts are YAML documents with the following structure:

```yaml
---
name: "Script Name"              # Optional: human-readable name
description: "Description"       # Optional: what the script does
version: 1                       # Optional: script version (default: 1)
debug: false                     # Optional: enable debug output (default: false)

vars:                            # Optional: variable declarations
  variable_name: "default_value"
  timeout: 30

steps:                           # Required: list of execution steps
  - send: "command"
  - print: "message"
```

### Auto-Detection

The system automatically detects YAML scripts by looking for:
- Document marker `---` at the start
- Keywords: `name:`, `description:`, `vars:`, `steps:`, `version:`
- Step keywords: `- send:`, `- print:`, `- wait:`, `- set:`, `- exit:`, `- extract:`, `- if:`, `- foreach:`, `- while:`, `- readfile:`, `- writefile:`, `- input:`, `- updatecolumn:`

Plain text (without YAML markers) is treated as simple commands to execute line by line.

---

## Commands

### send - Execute SSH Commands

Executes a command on the SSH session.

**Basic Syntax:**
```yaml
- send: command_text
```

**With Options:**
```yaml
- send: command_text
  capture: variable_name      # Store output in variable
  suppress: true              # Hide command and output from display
  expect: '/regex_pattern/'   # Custom prompt pattern to expect
  timeout: 30                 # Timeout in seconds for this command
  on_error: continue          # continue or stop (default)
```

**Options:**

| Option | Type | Description |
|--------|------|-------------|
| `capture` | string | Variable name to store command output |
| `suppress` | boolean | When true, hides both command and output from display |
| `expect` | string | Regex pattern for custom prompt detection |
| `timeout` | integer | Command-specific timeout in seconds |
| `on_error` | string | `continue` to proceed on error, `stop` to halt (default) |

**Examples:**
```yaml
# Simple command
- send: show version

# Capture output for later use
- send: show ip interface brief
  capture: interfaces

# Hide sensitive command
- send: show running-config
  suppress: true
  capture: config

# Handle interactive prompts
- send: enable
  expect: '/Password:/'

- send: ${enable_password}
  expect: '/#/'

# Continue even if command fails
- send: ping 192.168.1.1 count 3
  on_error: continue
  capture: ping_result
```

---

### print - Output Messages

Outputs a message to the script output.

**Syntax:**
```yaml
- print: "message with ${variable} substitution"
```

**Features:**
- Supports variable substitution with `${variable}` syntax
- Always succeeds (never causes script failure)

**Examples:**
```yaml
- print: "Starting configuration..."
- print: "Host: ${Host_IP}"
- print: "Found ${count} interfaces"
- print: "Status: ${status}"
```

---

### wait - Pause Execution

Pauses script execution for a specified number of seconds.

**Syntax:**
```yaml
- wait: seconds
```

**Examples:**
```yaml
# Wait 5 seconds
- wait: 5

# Wait after reboot command
- send: reload
- wait: 30
- send: show version
```

---

### set - Variable Assignment

Sets or modifies variable values with expression support.

**Syntax:**
```yaml
- set: variable_name = expression
```

**Supported Expressions:**

| Type | Example | Description |
|------|---------|-------------|
| Literal | `timeout = 30` | Assign numeric value |
| String | `name = "value"` | Assign string value |
| Variable | `copy = original` | Copy variable value |
| Substitution | `msg = "Host: ${ip}"` | String with variables |
| Addition | `counter = counter + 1` | Numeric addition |
| Subtraction | `value = total - 5` | Numeric subtraction |
| length() | `len = length(text)` | String or list length |
| trim() | `clean = trim(input)` | Remove whitespace |
| upper() | `caps = upper(text)` | Convert to uppercase |
| lower() | `small = lower(text)` | Convert to lowercase |

**Examples:**
```yaml
# Literal values
- set: timeout = 30
- set: interface = "eth0"

# Arithmetic
- set: i = 0
- set: i = i + 1
- set: remaining = total - processed

# String manipulation
- set: message = "Device: ${Host_IP}"
- set: trimmed = trim(raw_input)
- set: upper_name = upper(hostname)

# Get length
- set: line_count = length(output)
- set: num_items = length(items)
```

---

### extract - Regex Data Extraction

Extracts data from a variable using regex patterns with capture groups.

**Syntax:**
```yaml
- extract:
    from: source_variable
    pattern: 'regex_with_(capture_groups)'
    into: target_variable
    match: first    # first, last, all, or number (default: first)
```

**Parameters:**

| Parameter | Required | Description |
|-----------|----------|-------------|
| `from` | Yes | Source variable to search |
| `pattern` | Yes | Regex pattern (capture groups in parentheses) |
| `into` | Yes | Target variable(s) for extracted data |
| `match` | No | Which match to capture: `first`, `last`, `all`, or index number |

**Match Modes:**
- **first** (default): First match only
- **last**: Last match only
- **all**: All matches as a list
- **0, 1, 2...**: Specific match by zero-based index

**Single vs Multiple Capture Groups:**

```yaml
# Single capture group - into is a string
- extract:
    from: output
    pattern: 'Version: (.+?)$'
    into: version

# Multiple capture groups - into is a list
- extract:
    from: line
    pattern: '(\S+)\s+(\d+)\s+(\w+)'
    into: [name, count, status]
```

**Extracting All Matches:**
```yaml
# Get all IP addresses as a list
- extract:
    from: output
    pattern: '(\d+\.\d+\.\d+\.\d+)'
    into: ip_addresses
    match: all

# Loop over extracted list
- foreach: ip in ip_addresses
  do:
    - print: "Found IP: ${ip}"
```

**Examples:**
```yaml
# Extract version number
- send: show version
  capture: output
- extract:
    from: output
    pattern: 'Version (\d+\.\d+\.\d+)'
    into: version

# Extract interface name and status
- extract:
    from: line
    pattern: '(eth\d+)\s+\S+\s+\S+\s+(\w+)'
    into: [interface, status]

# Get all interface names
- extract:
    from: output
    pattern: '^(eth\d+)'
    into: interfaces
    match: all

# Get specific match (third occurrence)
- extract:
    from: output
    pattern: 'error: (.+?)$'
    into: third_error
    match: 2
```

---

### if - Conditional Execution

Executes a block conditionally based on an expression.

**Syntax:**
```yaml
- if: condition
  then:
    - step1
    - step2
  else:           # Optional
    - step3
```

**Condition Operators:**

| Operator | Example | Description |
|----------|---------|-------------|
| `==` | `status == "up"` | Equality (case-insensitive) |
| `!=` | `result != ""` | Inequality |
| `>` | `count > 10` | Greater than (numeric) |
| `>=` | `value >= 80` | Greater than or equal |
| `<` | `index < 5` | Less than (numeric) |
| `<=` | `score <= 100` | Less than or equal |
| `matches` | `text matches 'pattern'` | Regex match |
| `contains` | `output contains "error"` | Substring check |
| `startswith` | `name startswith "eth"` | Starts with |
| `endswith` | `file endswith ".txt"` | Ends with |
| `is empty` | `result is empty` | Check if empty/null |
| `is not empty` | `value is not empty` | Check if not empty |
| `is defined` | `var is defined` | Variable exists |
| `is not defined` | `var is not defined` | Variable doesn't exist |
| `and` | `a == "x" and b > 5` | Logical AND |
| `or` | `a == "x" or b == "y"` | Logical OR |
| `not` | `not condition` | Logical NOT |

**Examples:**
```yaml
# Simple condition
- if: status == "up"
  then:
    - print: "Interface is up"
  else:
    - print: "Interface is down"

# Regex match
- if: output matches 'error|failed'
  then:
    - exit: failure "Error detected in output"

# Multiple conditions
- if: count > threshold and status == "active"
  then:
    - print: "Threshold exceeded on active device"

# Check if variable is defined
- if: custom_timeout is defined
  then:
    - set: timeout = custom_timeout
  else:
    - set: timeout = 30

# Nested conditions
- if: type == "router"
  then:
    - if: vendor == "cisco"
      then:
        - send: show ip route
      else:
        - send: get router info routing
```

---

### foreach - Loop Over Collections

Iterates over items in a collection.

**Syntax:**
```yaml
- foreach: item in collection
  do:
    - step1
    - step2
  when: optional_filter     # Optional filter condition
```

**Collection Types:**
- **Lists**: Created by `extract` with `match: all`
- **Strings**: Automatically split into lines
- **Single values**: Treated as single-item collection

**Special Variables in Loop:**
- `${item}`: Current item value (or your chosen variable name)
- `${item_index}`: Zero-based index of current item

**Examples:**
```yaml
# Loop over extracted interfaces
- extract:
    from: output
    pattern: '(eth\d+)'
    into: interfaces
    match: all

- foreach: iface in interfaces
  do:
    - send: show interface ${iface}
    - print: "Checked ${iface}"

# Loop with index
- foreach: line in output
  do:
    - print: "Line ${item_index}: ${line}"

# Loop with filter
- foreach: iface in interfaces
  when: iface startswith "eth"
  do:
    - print: "Ethernet interface: ${iface}"

# Loop over lines in output
- send: show ip interface brief
  capture: output

- foreach: line in output
  when: line contains "up"
  do:
    - print: "Active: ${line}"
```

---

### while - Conditional Loop

Repeatedly executes a block while a condition is true.

**Syntax:**
```yaml
- while: condition
  do:
    - step1
    - step2
```

**Features:**
- Condition re-evaluated each iteration
- Maximum 10,000 iterations (safety limit)
- `${_iteration}` variable tracks iteration count (0-based)

**Examples:**
```yaml
# Counter-based loop
- set: i = 0
- while: i < 5
  do:
    - print: "Iteration ${i}"
    - set: i = i + 1

# Retry loop
- set: retry = 0
- set: success = ""
- while: retry < 3 and success is empty
  do:
    - send: ping 192.168.1.1 count 1
      capture: result
      on_error: continue
    - if: result contains "1 received"
      then:
        - set: success = "yes"
      else:
        - set: retry = retry + 1
        - wait: 2

# Poll for condition
- set: ready = ""
- while: ready is empty
  do:
    - send: show status
      capture: status
    - if: status contains "ready"
      then:
        - set: ready = "yes"
      else:
        - wait: 5
    - if: _iteration > 60
      then:
        - exit: failure "Timeout waiting for ready state"
```

---

### exit - Terminate Script

Ends script execution with a status and message.

**Syntax:**
```yaml
- exit: "message"                    # Success (default)
- exit: success "message"            # Explicit success
- exit: failure "message"            # Failure status
- exit: error "message"              # Error status
```

**Status Types:**
- **success**: Script completed successfully
- **failure**: Script detected a failure condition
- **error**: An unexpected error occurred

**Examples:**
```yaml
# Success exit
- exit: success "Configuration applied successfully"

# Failure exit
- if: status != "up"
  then:
    - exit: failure "Interface failed to come up"

# Error with variable
- exit: error "Unexpected response: ${output}"

# Simple exit (defaults to success)
- exit: "Task completed"
```

---

### readfile - Read Text Files

Reads a text file line by line into a list variable. Useful for processing IP lists, configuration data, or any line-based input.

**Syntax:**
```yaml
- readfile:
    path: "C:\\path\\to\\file.txt"
    into: variable_name
    skip_empty_lines: true     # Optional (default: true)
    trim_lines: true           # Optional (default: true)
    max_lines: 10000           # Optional (default: 10000, 0 = unlimited)
    encoding: utf-8            # Optional (default: utf-8)
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | - | Path to the file (supports variable substitution) |
| `into` | Yes | - | Variable name to store the lines as a list |
| `skip_empty_lines` | No | `true` | Skip blank lines |
| `trim_lines` | No | `true` | Remove leading/trailing whitespace from each line |
| `max_lines` | No | `10000` | Maximum lines to read (0 = unlimited) |
| `encoding` | No | `utf-8` | File encoding: `utf-8`, `ascii`, `utf-16`, `utf-32`, `latin1` |

**Security:**
- **Blocked paths**: Cannot read from `C:\Windows`, `C:\Program Files`, `C:\ProgramData`, or other users' directories
- **Allowed paths**: User profile, Documents, Desktop, AppData, Temp, and other non-system locations

**Examples:**
```yaml
# Read IP addresses from a file
- readfile:
    path: "C:\\Users\\me\\blocklist.txt"
    into: blocked_ips

- print: "Found ${blocked_ips.length} IPs to process"

- foreach: ip in blocked_ips
  do:
    - print: "Processing: ${ip}"

# Read with variable in path
- readfile:
    path: "${config_dir}\\hosts.txt"
    into: hosts
    max_lines: 1000

# Read ASCII file with all lines (including empty)
- readfile:
    path: "C:\\data\\log.txt"
    into: log_lines
    skip_empty_lines: false
    trim_lines: false
    encoding: ascii
```

---

### writefile - Write Text Files

Writes content to a text file. Supports append and overwrite modes.

**Syntax:**
```yaml
- writefile:
    path: "C:\\path\\to\\file.txt"
    content: "text to write"
    mode: append               # Optional: append (default) or overwrite
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | - | Path to the file (supports variable substitution) |
| `content` | No | `""` | Content to write (supports variable substitution) |
| `mode` | No | `append` | Write mode: `append` or `overwrite` |

**Security:**
- **Blocked paths**: Cannot write to system directories or Program Files
- **Blocked extensions**: Cannot write executable files (`.exe`, `.dll`, `.bat`, `.ps1`, `.cmd`, etc.)
- **Allowed paths**: User profile, Documents, Desktop, AppData, Temp only

**Examples:**
```yaml
# Log results to a file
- writefile:
    path: "C:\\Users\\me\\output.log"
    content: "${_timestamp} - Processed ${Host_IP}: ${status}"

# Overwrite a file
- writefile:
    path: "C:\\Users\\me\\report.txt"
    content: "Report generated at ${_timestamp}"
    mode: overwrite

# Append multiple lines in a loop
- foreach: ip in processed_ips
  do:
    - writefile:
        path: "C:\\logs\\processed.txt"
        content: "${ip} - completed"

# Create file with path from variable
- writefile:
    path: "${output_dir}\\results.csv"
    content: "${Host_IP},${status},${version}"
```

---

### input - Prompt for User Input

Prompts the user for input during script execution with optional validation.

**Syntax:**
```yaml
- input:
    prompt: "Enter value:"
    into: variable_name
    default: "default_value"   # Optional
    password: false            # Optional (default: false)
    validate: "^regex$"        # Optional regex validation
    validation_error: "Error"  # Optional custom error message
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `prompt` | No | `"Enter value:"` | Text to display to the user |
| `into` | Yes | - | Variable name to store the input |
| `default` | No | `""` | Default value pre-filled in the input |
| `password` | No | `false` | Mask input for sensitive data |
| `validate` | No | - | Regex pattern to validate input |
| `validation_error` | No | `"Input does not match required format."` | Error message when validation fails |

**Features:**
- Dialog appears during script execution
- User can cancel (script will fail unless `on_error: continue`)
- Validation prevents submission until input matches pattern
- Variables can be used in `prompt` and `default`

**Examples:**
```yaml
# Simple input
- input:
    prompt: "Enter the target IP address:"
    into: target_ip

# Input with default value
- input:
    prompt: "Enter timeout (seconds):"
    into: timeout
    default: "30"

# Password input (masked)
- input:
    prompt: "Enter enable password:"
    into: enable_pass
    password: true

# Input with validation
- input:
    prompt: "Enter port number (1-65535):"
    into: port
    validate: "^[0-9]+$"
    validation_error: "Port must be a number"

# IP address validation
- input:
    prompt: "Enter IP address:"
    into: ip_address
    validate: "^\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}$"
    validation_error: "Please enter a valid IP address (e.g., 192.168.1.1)"

# Confirm action
- input:
    prompt: "Type 'yes' to confirm deletion:"
    into: confirm
    validate: "^yes$"
    validation_error: "You must type 'yes' to proceed"

- if: confirm != "yes"
  then:
    - exit: "Operation cancelled"
```

---

### updatecolumn - Update Host Table Column

Writes a value back to a column in the host table for the current host. This allows scripts to store extracted data directly in the grid for later reference or export.

**Syntax:**
```yaml
- updatecolumn:
    column: "column_name"
    value: "value_or_${variable}"
```

**Parameters:**

| Parameter | Required | Description |
|-----------|----------|-------------|
| `column` | Yes | The column name to update (created if it doesn't exist) |
| `value` | Yes | The value to set (supports variable substitution) |

**Features:**
- Automatically creates the column if it doesn't exist
- Updates happen in real-time during script execution
- Matches the host by IP address and port
- Supports variable substitution with `${variable}` syntax
- Values are persisted when you save the configuration or export to CSV

**Examples:**
```yaml
# Store a simple extracted value
- extract:
    from: version_output
    pattern: 'Version: (.+?)$'
    into: version
- updatecolumn:
    column: version
    value: ${version}

# Store the current timestamp
- updatecolumn:
    column: last_scanned
    value: ${_timestamp}

# Store a computed or formatted value
- set: status_msg = "OK - ${interface_count} interfaces"
- updatecolumn:
    column: status
    value: ${status_msg}

# Store multiple values from one script
- updatecolumn:
    column: hostname
    value: ${hostname}
- updatecolumn:
    column: model
    value: ${model}
- updatecolumn:
    column: serial
    value: ${serial_number}
```

**Use Cases:**
- **Inventory collection**: Extract version, hostname, serial number, etc. and store in columns
- **Compliance checking**: Store pass/fail status in a "compliance" column
- **Audit trails**: Record when each host was last checked
- **Network discovery**: Store discovered interface names, IP addresses, or neighbor info

---

## Variables

### Variable Sources

1. **CSV Grid Columns**: Any column in the host grid
   ```yaml
   - print: "Host: ${Host_IP}"      # Required column
   - print: "User: ${username}"     # Custom column
   ```

2. **Script Variables** (from `vars:` section):
   ```yaml
   vars:
     timeout: 30
     interface: "eth0"
   steps:
     - print: "Timeout: ${timeout}"
   ```

3. **Captured Variables**:
   ```yaml
   - send: show version
     capture: version_output
   - print: "${version_output}"
   ```

4. **Extracted Variables**:
   ```yaml
   - extract:
       from: output
       pattern: 'IP: (.+?)$'
       into: ip_address
   - print: "IP: ${ip_address}"
   ```

5. **Set Variables**:
   ```yaml
   - set: counter = 0
   - print: "Counter: ${counter}"
   ```

### Built-in Variables

| Variable | Description |
|----------|-------------|
| `${_output}` | Last command output |
| `${_timestamp}` | Script start time (yyyy-MM-dd HH:mm:ss) |
| `${_iteration}` | Current iteration in while loop |
| `${item_index}` | Current index in foreach loop |

### Variable Substitution Syntax

Variables are substituted using `${variable_name}`:

```yaml
- print: "Host ${Host_IP} has IP ${ip_address}"
- send: show interface ${interface_name}
- set: message = "Status: ${status}"
```

### Array Access

Lists support index-based access:

```yaml
- extract:
    from: output
    pattern: '(\d+)'
    into: numbers
    match: all

- print: "First: ${numbers[0]}"
- print: "Second: ${numbers[1]}"
```

---

## Expressions and Conditions

### Comparison Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `==` | Equal (case-insensitive) | `status == "up"` |
| `!=` | Not equal | `result != ""` |
| `>` | Greater than | `count > 10` |
| `>=` | Greater than or equal | `value >= 0` |
| `<` | Less than | `index < 5` |
| `<=` | Less than or equal | `retry <= 3` |

### String Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `matches` | Regex match | `output matches 'error\|fail'` |
| `contains` | Substring | `text contains "warning"` |
| `startswith` | Starts with | `name startswith "eth"` |
| `endswith` | Ends with | `file endswith ".log"` |

### State Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `is empty` | Empty or null | `result is empty` |
| `is not empty` | Has value | `output is not empty` |
| `is defined` | Variable exists | `var is defined` |
| `is not defined` | Variable missing | `opt is not defined` |

### Logical Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `and` | Both true | `a > 0 and b > 0` |
| `or` | Either true | `status == "up" or status == "active"` |
| `not` | Negation | `not error is empty` |

### Grouping

Use parentheses for complex expressions:

```yaml
- if: (status == "up" or status == "active") and count > 0
  then:
    - print: "System is operational"
```

---

## Error Handling

### Command-Level Error Handling

```yaml
# Stop on error (default)
- send: critical_command
  on_error: stop

# Continue on error
- send: optional_command
  on_error: continue
  capture: result
```

### Checking for Errors

```yaml
- send: some_command
  capture: output
  on_error: continue

- if: output contains "error" or output is empty
  then:
    - exit: failure "Command failed"
```

### Retry Pattern

```yaml
- set: retry = 0
- set: success = ""

- while: retry < 3 and success is empty
  do:
    - send: unreliable_command
      capture: result
      on_error: continue

    - if: result contains "OK"
      then:
        - set: success = "yes"
      else:
        - set: retry = retry + 1
        - wait: 5

- if: success is empty
  then:
    - exit: failure "Command failed after 3 retries"
```

---

## Debug Mode

Enable debug mode to see detailed execution information:

```yaml
---
name: Debug Example
debug: true

steps:
  - send: show version
    capture: output
  - extract:
      from: output
      pattern: 'Version (.+?)$'
      into: version
  - set: msg = "Found version: ${version}"
  - print: "${msg}"
```

**Debug Output Includes:**
- Variable assignments from `set`
- Extracted values from `extract`
- Condition evaluation results in `if`
- Loop iteration counts in `foreach` and `while`

---

## Examples

### Example 1: Device Information Collection

```yaml
---
name: Device Info Collection
description: Collects version and interface information

steps:
  - print: "=== Device: ${Host_IP} ==="

  - send: show version
    capture: version_output

  - extract:
      from: version_output
      pattern: 'Version (\S+)'
      into: version

  - print: "Software Version: ${version}"

  - send: show ip interface brief
    capture: interfaces

  - print: "Interface Status:"
  - foreach: line in interfaces
    when: line contains "up"
    do:
      - print: "  ${line}"

  - exit: success "Information collected"
```

### Example 2: Configuration Backup

```yaml
---
name: Config Backup
vars:
  backup_cmd: "show running-config"

steps:
  - print: "Backing up ${Host_IP}..."

  - send: terminal length 0

  - send: ${backup_cmd}
    capture: config
    timeout: 120

  - if: config is empty
    then:
      - exit: failure "Failed to retrieve configuration"

  - extract:
      from: config
      pattern: 'hostname (\S+)'
      into: hostname

  - print: "Backup complete for ${hostname}"
  - exit: success "Configuration captured"
```

### Example 3: Interface Status Check with Retry

```yaml
---
name: Interface Check
vars:
  target_interface: "GigabitEthernet0/0"
  max_retries: 3

steps:
  - print: "Checking ${target_interface} on ${Host_IP}"

  - set: retry = 0
  - set: is_up = ""

  - while: retry < max_retries and is_up is empty
    do:
      - send: show interface ${target_interface}
        capture: status

      - if: status matches 'line protocol is up'
        then:
          - set: is_up = "yes"
          - print: "Interface is UP"
        else:
          - print: "Interface down, retry ${retry}..."
          - set: retry = retry + 1
          - wait: 10

  - if: is_up is empty
    then:
      - exit: failure "${target_interface} failed to come up"
    else:
      - exit: success "Interface verified"
```

### Example 4: Bulk IP Block (FortiGate)

```yaml
---
name: Block IP Address
description: Adds IP to firewall block list

steps:
  - if: block is not defined or block is empty
    then:
      - exit: error "No IP address specified in 'block' column"

  - print: "Blocking ${block} on ${Host_IP}"

  - send: config firewall address
  - send: edit "BLOCK_${block}"
  - send: set subnet ${block} 255.255.255.255
  - send: set comment "Blocked by SSH Helper"
  - send: next
  - send: end

  - send: show firewall address BLOCK_${block}
    capture: verify

  - if: verify contains "BLOCK_${block}"
    then:
      - exit: success "IP ${block} blocked successfully"
    else:
      - exit: failure "Failed to verify block for ${block}"
```

### Example 5: Multi-Vendor Support

```yaml
---
name: Get Version (Multi-Vendor)
vars:
  vendor: "cisco"

steps:
  - if: vendor == "cisco"
    then:
      - send: show version
        capture: output
      - extract:
          from: output
          pattern: 'Version (\S+)'
          into: version

  - if: vendor == "juniper"
    then:
      - send: show version
        capture: output
      - extract:
          from: output
          pattern: 'Junos: (\S+)'
          into: version

  - if: vendor == "fortigate"
    then:
      - send: get system status
        capture: output
      - extract:
          from: output
          pattern: 'Version: (.+?)$'
          into: version

  - if: version is defined
    then:
      - print: "Version: ${version}"
    else:
      - print: "Could not determine version"
```

### Example 6: Block IPs from File

```yaml
---
name: Block IPs from File
description: Reads IP addresses from a file and blocks each one

steps:
  # Prompt for the blocklist file path
  - input:
      prompt: "Enter path to IP blocklist file:"
      into: blocklist_path
      default: "C:\\Users\\me\\blocklist.txt"

  # Read the file
  - readfile:
      path: "${blocklist_path}"
      into: blocked_ips

  - if: blocked_ips is empty
    then:
      - exit: failure "No IPs found in file"

  - print: "Found ${blocked_ips.length} IPs to block"

  # Process each IP
  - foreach: ip in blocked_ips
    do:
      - print: "Blocking: ${ip}"
      - send: config firewall address
      - send: edit "BLOCK_${ip}"
      - send: set subnet ${ip} 255.255.255.255
      - send: next
      - send: end

      # Log result
      - writefile:
          path: "C:\\logs\\blocked_ips.log"
          content: "${_timestamp} - Blocked ${ip} on ${Host_IP}"

  - exit: success "Blocked ${blocked_ips.length} IPs"
```

### Example 7: Interactive Configuration with Validation

```yaml
---
name: Configure Interface
description: Interactive interface configuration with input validation

steps:
  # Get interface name with validation
  - input:
      prompt: "Enter interface name (e.g., eth0, GigabitEthernet0/1):"
      into: interface
      validate: "^[a-zA-Z][a-zA-Z0-9/]+$"
      validation_error: "Invalid interface name format"

  # Get IP address with validation
  - input:
      prompt: "Enter IP address:"
      into: ip_address
      validate: "^\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}$"
      validation_error: "Please enter a valid IPv4 address"

  # Get subnet mask
  - input:
      prompt: "Enter subnet mask:"
      into: subnet_mask
      default: "255.255.255.0"
      validate: "^\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}$"
      validation_error: "Please enter a valid subnet mask"

  # Confirm before applying
  - input:
      prompt: "Configure ${interface} with ${ip_address}/${subnet_mask}? Type 'yes' to confirm:"
      into: confirm
      validate: "^(yes|no)$"
      validation_error: "Please type 'yes' or 'no'"

  - if: confirm != "yes"
    then:
      - exit: "Configuration cancelled by user"

  # Apply configuration
  - print: "Configuring ${interface}..."
  - send: configure terminal
  - send: interface ${interface}
  - send: ip address ${ip_address} ${subnet_mask}
  - send: no shutdown
  - send: end

  - print: "Interface ${interface} configured with ${ip_address}"
```

### Example 8: Extract and Store Data to Host Table

```yaml
---
name: Extract Device Info to Columns
description: Extracts device information and stores it back to the host table

steps:
  - print: "Collecting info from ${Host_IP}..."

  # Get version info
  - send: show version
    capture: version_output

  - extract:
      from: version_output
      pattern: 'Version (\S+)'
      into: version

  - extract:
      from: version_output
      pattern: 'uptime is (.+?)$'
      into: uptime

  # Get hostname
  - send: show running-config | include hostname
    capture: hostname_output

  - extract:
      from: hostname_output
      pattern: 'hostname (\S+)'
      into: hostname

  # Store extracted values back to the host table columns
  - updatecolumn:
      column: version
      value: ${version}

  - updatecolumn:
      column: hostname
      value: ${hostname}

  - updatecolumn:
      column: uptime
      value: ${uptime}

  - updatecolumn:
      column: last_checked
      value: ${_timestamp}

  - print: "Updated columns for ${Host_IP}: version=${version}, hostname=${hostname}"
  - exit: success "Device info collected and stored"
```

---

## Tips and Best Practices

1. **Use `capture` and `extract` together**: Capture output first, then extract specific data
2. **Enable `debug: true` while developing**: Helps troubleshoot variable values and conditions
3. **Use `suppress: true` for sensitive commands**: Prevents credentials from appearing in output
4. **Set appropriate timeouts**: Long-running commands may need custom timeout values
5. **Use `on_error: continue` cautiously**: Only when you handle the error case explicitly
6. **Initialize variables in `vars:`**: Provides default values and documents expected variables
7. **Use meaningful variable names**: Makes scripts easier to read and maintain
8. **Test with single host first**: Verify script works before running on multiple hosts
