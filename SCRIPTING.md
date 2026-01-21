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
   - [log](#log---output-with-log-level)
   - [webhook](#webhook---http-requests)
3. [Variables](#variables)
4. [Expressions and Conditions](#expressions-and-conditions)
5. [Error Handling](#error-handling)
6. [Debug Mode](#debug-mode)
7. [Working with JSON](#working-with-json)
8. [Examples](#examples)

---

## Script Structure

Scripts are YAML documents with the following structure:

```yaml
---
name: "Script Name"              # Optional: human-readable name
description: "Description"       # Optional: what the script does
version: 1                       # Optional: script version (default: 1)
debug: false                     # Optional: enable debug output (default: false)
nobanner: false                  # Optional: suppress script execution banner (default: false)

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
- Keywords: `name:`, `description:`, `vars:`, `steps:`, `version:`, `nobanner:`
- Step keywords: `- send:`, `- print:`, `- wait:`, `- set:`, `- exit:`, `- extract:`, `- if:`, `- foreach:`, `- while:`, `- readfile:`, `- writefile:`, `- input:`, `- updatecolumn:`, `- log:`, `- webhook:`

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
| Multiplication | `total = count * 10` | Numeric multiplication |
| Division | `avg = total / count` | Numeric division |
| Modulo | `remainder = value % 10` | Numeric modulo |
| length() | `len = length(text)` | String or list length |
| trim() | `clean = trim(input)` | Remove whitespace |
| upper() | `caps = upper(text)` | Convert to uppercase |
| lower() | `small = lower(text)` | Convert to lowercase |
| push() | `arr = push(arr, item)` | Add item to array |
| json() | `obj = json("k1", v1, "k2", v2)` | Create JSON object or array |
| json.get() | `val = json.get(data, "path", default)` | Extract value with optional default |
| json.set() | `obj = json.set(obj, "path", value)` | Set value at path |
| json.delete() | `obj = json.delete(obj, "path")` | Remove key/element at path |
| json.merge() | `merged = json.merge(obj1, obj2, ...)` | Merge multiple objects |
| json.format() | `pretty = json.format(data)` | Format JSON (pretty/compact) |
| json.exists() | `bool = json.exists(data, "path")` | Check if path exists |
| json.len() | `count = json.len(data, "path")` | Get array/object length |
| json.type() | `type = json.type(data, "path")` | Get value type |
| json.keys() | `keys = json.keys(obj)` | Get object keys as list |
| json.values() | `vals = json.values(obj)` | Get object values as list |
| json.items() | `items = json.items(data, "path")` | Extract array/object entries |
| json.push() | `arr = json.push(arr, value)` | Append to JSON array |
| json.pop() | `last = json.pop(arr)` | Get last element |
| json.unshift() | `arr = json.unshift(arr, value)` | Prepend to JSON array |
| json.shift() | `first = json.shift(arr)` | Get first element |
| json.slice() | `sub = json.slice(arr, 0, 3)` | Extract array subset |
| json.concat() | `all = json.concat(arr1, arr2)` | Concatenate arrays |
| json.indexOf() | `idx = json.indexOf(arr, value)` | Find element index |
| Nested assignment | `obj.key.subkey = value` | Assign to nested path |

**Basic Examples:**
```yaml
# Literal values
- set: timeout = 30
- set: interface = "eth0"

# Arithmetic
- set: i = 0
- set: i = i + 1
- set: remaining = total - processed
- set: doubled = count * 2
- set: average = total / count

# String manipulation
- set: message = "Device: ${Host_IP}"
- set: trimmed = trim(raw_input)
- set: upper_name = upper(hostname)

# Get length
- set: line_count = length(output)
- set: num_items = length(items)
```

**Array Functions:**
```yaml
# Create and build an array
- set: results = push(results, ${Host_IP})
- set: results = push(results, ${status})

# Get array length
- set: count = length(results)
```

**JSON Functions:**

All JSON functions use dot notation (`json.get`, `json.set`, etc.) for a clean, consistent API.

**Type Detection:** When creating JSON, values are automatically converted:
- Strings `"true"` or `"false"` → booleans
- Numeric strings → numbers
- Strings starting with `{` or `[` → parsed as JSON objects/arrays
- Everything else → strings

---

**`json(...)` - Universal Constructor**

Creates JSON objects or arrays. Add `pretty` anywhere for formatted output.

```yaml
# Create object from key-value pairs
- set: data = json("host", ${Host_IP}, "status", "up", "port", 22)
# Result: {"host":"192.168.1.1","status":"up","port":22}

# Pretty-printed object
- set: data = json("host", ${Host_IP}, "status", "up", pretty)

# Create array from list variable
- set: items = push(items, "a")
- set: items = push(items, "b")
- set: arr = json(items)
# Result: ["a","b"]

# Create array inline with []
- set: arr = json([], "item1", "item2", "item3")
# Result: ["item1","item2","item3"]

# Nested objects
- set: data = json("server", json("host", ${ip}, "port", 22), "active", true)
```

---

**`json.get(json, path, default?)` - Extract Value**

Extracts a value using dot notation path. Supports optional default value.

```yaml
# Basic extraction
- set: name = json.get(response, "data.user.name")
- set: email = json.get(response, "data.user.email")

# With default value (returned if path doesn't exist)
- set: port = json.get(config, "server.port", 22)
- set: timeout = json.get(config, "settings.timeout", 30)

# Array indexing with [n] syntax
- set: first = json.get(data, "items[0].name")
- set: nested = json.get(data, "users[0].addresses[1].city")
```

---

**`json.set(json, path, value)` - Set Value at Path**

Sets a value at a path, creating intermediate objects as needed.

```yaml
# Update a nested value
- set: config = json.set(config, "server.port", 8080)

# Add new keys
- set: data = json.set(data, "metadata.created", ${_timestamp})

# Set array element
- set: data = json.set(data, "users[0].active", true)
```

---

**`json.delete(json, path)` - Remove Key or Element**

Removes a key from an object or element from an array.

```yaml
# Remove a key
- set: user = json.delete(user, "password")
- set: data = json.delete(data, "sensitive.ssn")

# Remove array element by index
- set: arr = json.delete(arr, "items[2]")
```

---

**`json.merge(obj1, obj2, ...)` - Deep Merge Objects**

Merges multiple objects. Later objects override earlier ones.

```yaml
# Merge two objects
- set: base = json("name", "server1", "type", "linux")
- set: updates = json("status", "active", "type", "ubuntu")
- set: merged = json.merge(base, updates)
# Result: {"name":"server1","type":"ubuntu","status":"active"}

# Merge multiple objects (variadic)
- set: final = json.merge(defaults, env_config, user_overrides)
```

---

**`json.format(json, style?)` - Format JSON**

Formats JSON for display. Default is pretty-printed.

```yaml
# Pretty print (default)
- set: formatted = json.format(data)
- set: formatted = json.format(data, pretty)

# Compact (single line)
- set: compact = json.format(data, compact)
```

---

**`json.exists(json, path)` - Check Path Existence**

Returns true/false indicating whether a path exists. Distinguishes between null values and missing keys.

```yaml
# Check for error response
- if: json.exists(response, "error.code")
  then:
    - set: err = json.get(response, "error.code")
    - log: "Error: ${err}"
      level: error

# Safer than checking for empty
- if: json.exists(config, "optional") and json.get(config, "optional") != null
  then:
    - print: "Optional is set and not null"
```

---

**`json.len(json, path?)` - Get Length**

Returns array length or object key count.

```yaml
# Array length
- set: count = json.len(response, "data.items")

# Object key count
- set: num_keys = json.len(config)

# Use in loops
- if: json.len(users) > 0
  then:
    - print: "Found ${json.len(users)} users"
```

---

**`json.type(json, path?)` - Get Value Type**

Returns the type: `"object"`, `"array"`, `"string"`, `"number"`, `"boolean"`, or `"null"`.

```yaml
- set: t = json.type(response, "data")
- if: t == "array"
  then:
    - print: "Data is an array"
```

---

**`json.keys(json, path?)` - Get Object Keys**

Returns object keys as a list for iteration.

```yaml
- set: fields = json.keys(user)
- foreach: field in fields
  do:
    - set: val = json.get(user, field)
    - print: "${field}: ${val}"
```

---

**`json.values(json, path?)` - Get Object Values**

Returns object values as a list.

```yaml
- set: ips = json.values(servers)
- foreach: ip in ips
  do:
    - send: ping ${ip} -c 1
```

---

**`json.items(json, path?)` - Extract Array or Object Entries**

For arrays: returns elements as a list.
For objects: returns `{"key": k, "value": v}` entries.

```yaml
# Array iteration
- foreach: user in json.items(response, "data.users")
  do:
    - set: name = json.get(user, "name")
    - set: email = json.get(user, "email")
    - print: "User: ${name} (${email})"

# Object iteration (key-value pairs)
- set: servers = json("web", "10.0.0.1", "db", "10.0.0.2")
- foreach: entry in json.items(servers)
  do:
    - set: name = json.get(entry, "key")
    - set: ip = json.get(entry, "value")
    - print: "${name}: ${ip}"
```

---

**Array Manipulation Functions**

```yaml
# Append to array
- set: arr = json.push(arr, "new_item")
- set: arr = json.push(arr, json("key", "value"))

# Get last element
- set: last = json.pop(arr)

# Prepend to array
- set: arr = json.unshift(arr, "first")

# Get first element
- set: first = json.shift(arr)

# Slice array (supports negative indices)
- set: first_three = json.slice(arr, 0, 3)
- set: last_two = json.slice(arr, -2)

# Concatenate arrays
- set: all = json.concat(arr1, arr2, arr3)

# Find element index (-1 if not found)
- set: idx = json.indexOf(arr, "search_value")
- if: idx >= 0
  then:
    - print: "Found at index ${idx}"
```

**Nested Assignment (Dot Notation):**

You can build nested JSON objects using dot notation. Intermediate objects are created automatically:

```yaml
# Build a nested structure
- set: data.server.name = ${hostname}
- set: data.server.ip = ${Host_IP}
- set: data.server.port = 22
- set: data.metadata.timestamp = ${_timestamp}
- set: data.metadata.scanned_by = "SSH Helper"

# The 'data' variable now contains:
# {
#   "server": {
#     "name": "router1",
#     "ip": "192.168.1.1",
#     "port": 22
#   },
#   "metadata": {
#     "timestamp": "2024-01-15 10:30:00",
#     "scanned_by": "SSH Helper"
#   }
# }

# Write the nested object to a file
- writefile:
    path: "C:\\output\\${Host_IP}.json"
    format: json
    content: "${data}"
    pretty: true
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

**Pattern Notes:**
- Pattern delimiters (`/pattern/`, `"pattern"`, `'pattern'`) are automatically stripped
- Patterns are matched case-insensitively and support multiline mode
- Debug output truncates extracted values at 50 characters for readability

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
- `${item_index}`: Zero-based index of current item (uses your iterator name, e.g., `${line_index}` if you use `foreach: line in ...`)

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

# Loop with index (index variable uses your iterator name)
- foreach: line in output
  do:
    - print: "Line ${line_index}: ${line}"

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
- **failure** (alias: `fail`): Script detected a failure condition
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
| `encoding` | No | `utf-8` | File encoding: `utf-8`, `ascii`, `utf-16`, `utf-16be`, `utf-32`, `latin1` (aliases: `unicode` for utf-16, `iso-8859-1` for latin1) |

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

Writes content to a text file. Supports multiple formats including text, JSON, JSON Lines (JSONL), and CSV.

**Syntax:**
```yaml
- writefile:
    path: "C:\\path\\to\\file.txt"
    content: "text to write"
    mode: overwrite            # Optional: overwrite (default) or append
    format: text               # Optional: text (default), json, jsonl, or csv
    pretty: true               # Optional: pretty-print JSON (default: true)
    headers: [col1, col2]      # Optional: CSV headers
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | - | Path to the file (supports variable substitution) |
| `content` | No | `""` | Content to write (use `${varname}` for variables) |
| `mode` | No | `overwrite` | Write mode: `overwrite` or `append` |
| `format` | No | `text` | Output format: `text`, `json`, `jsonl`, or `csv` |
| `pretty` | No | `true` | Pretty-print JSON with indentation |
| `headers` | No | - | CSV column headers (list of strings) |

**Security:**
- **Blocked paths**: Cannot write to system directories or Program Files
- **Blocked extensions**: Cannot write executable files (`.exe`, `.dll`, `.bat`, `.ps1`, `.cmd`, etc.)
- **Allowed paths**: User profile, Documents, Desktop, AppData, Temp only

**Format Details:**

| Format | Description | Append Behavior |
|--------|-------------|-----------------|
| `text` | Plain text output | Appends content with newline |
| `json` | JSON with automatic type detection | **Merges** with existing file (arrays concatenated, objects deep-merged) |
| `jsonl` | JSON Lines (one JSON object per line) | Appends single JSON line |
| `csv` | Comma-separated values | Appends rows |

**Basic Examples:**
```yaml
# Log results to a file
- writefile:
    path: "C:\\Users\\me\\output.log"
    content: "${_timestamp} - Processed ${Host_IP}: ${status}"
    mode: append

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
        mode: append

# Create file with path from variable
- writefile:
    path: "${output_dir}\\results.csv"
    content: "${Host_IP},${status},${version}"
```

**JSON Format:**

When using `format: json`, the content is automatically serialized with type detection:
- Numbers, booleans, and null are preserved as their JSON types
- Strings are properly escaped
- Lists become JSON arrays
- Objects (from `json()` or dot notation) become JSON objects

```yaml
# Write a JSON array from a list variable
- set: hosts = push(hosts, ${Host_IP})
- set: hosts = push(hosts, ${other_ip})
- writefile:
    path: "C:\\output\\hosts.json"
    format: json
    content: "${hosts}"
    pretty: true

# Write a JSON object
- set: data = json("host", ${Host_IP}, "status", "success", "port", 22)
- writefile:
    path: "C:\\output\\result.json"
    format: json
    content: "${data}"
    pretty: true

# Write nested object built with dot notation
- set: result.server.ip = ${Host_IP}
- set: result.server.hostname = ${hostname}
- set: result.scan.timestamp = ${_timestamp}
- writefile:
    path: "C:\\output\\scan.json"
    format: json
    content: "${result}"
```

**JSON Append Merging:**

When using `format: json` with `mode: append`, the new content is intelligently merged with existing file content:

- **Arrays**: New items are concatenated to the existing array
- **Objects**: Properties are deep-merged (new values override existing)

```yaml
# First write creates the file with an array
- set: item = json("ip", "192.168.1.1", "status", "up")
- writefile:
    path: "C:\\output\\results.json"
    format: json
    content: "[${item}]"
    mode: overwrite

# Subsequent writes append to the array
- set: item = json("ip", "192.168.1.2", "status", "down")
- writefile:
    path: "C:\\output\\results.json"
    format: json
    content: "[${item}]"
    mode: append
# File now contains: [{"ip":"192.168.1.1","status":"up"},{"ip":"192.168.1.2","status":"down"}]
```

**JSON Lines (JSONL) Format:**

JSONL format writes one compact JSON object per line, ideal for log files and streaming data:

```yaml
# Write events as JSON Lines
- set: event = json("timestamp", ${_timestamp}, "host", ${Host_IP}, "action", "scanned")
- writefile:
    path: "C:\\logs\\events.jsonl"
    format: jsonl
    content: "${event}"
    mode: append

# Each execution appends a line like:
# {"timestamp":"2024-01-15 10:30:00","host":"192.168.1.1","action":"scanned"}
# {"timestamp":"2024-01-15 10:30:05","host":"192.168.1.2","action":"scanned"}
```

**CSV Format:**

CSV format with optional headers:

```yaml
# Write CSV with headers (first write)
- writefile:
    path: "C:\\output\\inventory.csv"
    format: csv
    content: "${hosts}"
    headers: [IP, Hostname, Version, Status]
    mode: overwrite

# Append data rows
- set: row = "${Host_IP},${hostname},${version},${status}"
- writefile:
    path: "C:\\output\\inventory.csv"
    format: csv
    content: "${row}"
    mode: append
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

### log - Output with Log Level

Outputs a message with a specific log level for categorized output. Unlike `print`, log messages are styled based on their level and can be filtered.

**Simple Syntax:**
```yaml
- log: "message text"
```

**With Options:**
```yaml
- log:
    message: "message text"
    level: warning
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `message` | Yes | - | The message to output (supports variable substitution) |
| `level` | No | `info` | Log level: `debug`, `info`, `warning`, `error`, `success` |

**Log Levels:**

| Level | Aliases | Description |
|-------|---------|-------------|
| `debug` | - | Debug information (only shown when debug mode is enabled) |
| `info` | - | General information (default level) |
| `warning` | `warn` | Warning messages |
| `error` | `err` | Error messages |
| `success` | - | Success/completion messages |

**Examples:**
```yaml
# Simple info message (default level)
- log: "Processing ${Host_IP}..."

# Warning level
- log:
    message: "Configuration may be outdated"
    level: warning

# Error level with variable
- log:
    message: "Failed to connect: ${error_msg}"
    level: error

# Success level
- log:
    message: "All checks passed for ${Host_IP}"
    level: success

# Debug (only visible with debug: true in script header or global debug mode)
- log:
    message: "Variable state: count=${count}, status=${status}"
    level: debug

# Using warn/err aliases
- log:
    message: "Disk space low"
    level: warn

- log:
    message: "Connection timeout"
    level: err
```

**Note:** The `log` command always succeeds and never causes script failure, similar to `print`.

---

### webhook - HTTP Requests

Makes HTTP requests to external APIs and captures responses. Useful for integrating with webhooks, REST APIs, notification services, or logging platforms.

**Syntax:**
```yaml
- webhook:
    url: "https://api.example.com/endpoint"
    method: POST
    body: '{"key": "value"}'
    headers:
      Content-Type: "application/json"
      Authorization: "Bearer ${token}"
    into: response
    timeout: 30
    on_error: continue
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `url` | Yes | - | Target URL (must be `http://` or `https://`) |
| `method` | No | `POST` | HTTP method: `GET`, `POST`, `PUT`, `PATCH`, `DELETE` |
| `body` | No | - | Request body (for POST, PUT, PATCH). Supports variable substitution |
| `headers` | No | - | Custom HTTP headers as key-value pairs |
| `into` | No | - | Variable to capture response body |
| `timeout` | No | `30` | Request timeout in seconds |
| `on_error` | No | `stop` | Error handling: `continue` or `stop` |

**Response Capture:**

When using the `into` parameter, two variables are created:
- `${varname}` - The response body content
- `${varname}_status` - The HTTP status code (e.g., 200, 404, 500)

**Examples:**

```yaml
# Simple GET request
- webhook:
    url: "https://api.example.com/status"
    method: GET
    into: api_response

- if: api_response_status == 200
  then:
    - print: "API is healthy"
  else:
    - print: "API returned status: ${api_response_status}"

# POST with JSON body
- set: payload = json("host", ${Host_IP}, "status", ${status})
- webhook:
    url: "https://hooks.slack.com/services/xxx/yyy/zzz"
    method: POST
    body: "${payload}"
    headers:
      Content-Type: "application/json"
    into: result
    on_error: continue

# Send notification with authentication
- webhook:
    url: "https://api.pagerduty.com/incidents"
    method: POST
    body: '{"incident": {"title": "Alert from ${Host_IP}", "service": {"id": "PXXXXXX"}}}'
    headers:
      Authorization: "Token token=${api_key}"
      Content-Type: "application/json"
    timeout: 10

# Log events to external service
- set: event = json("timestamp", ${_timestamp}, "host", ${Host_IP}, "event", "scan_complete")
- webhook:
    url: "https://logs.example.com/ingest"
    body: "${event}"
    headers:
      Content-Type: "application/json"
      X-API-Key: "${logging_api_key}"

# GET request with query parameters in URL
- webhook:
    url: "https://api.example.com/lookup?ip=${Host_IP}&format=json"
    method: GET
    into: lookup_result
    timeout: 15

# Check response and handle errors
- webhook:
    url: "https://api.example.com/validate"
    method: POST
    body: '{"device": "${Host_IP}"}'
    headers:
      Content-Type: "application/json"
    into: validation
    on_error: continue

- if: validation_status == 200
  then:
    - print: "Validation successful"
  else:
    - if: validation_status is defined
      then:
        - log:
            message: "Validation failed with status ${validation_status}"
            level: warning
      else:
        - log:
            message: "Webhook request failed (network error)"
            level: error
```

**Security Notes:**
- URLs must use `http://` or `https://` protocol
- Consider using `on_error: continue` when webhook failures shouldn't stop script execution
- Sensitive data in headers (like API keys) should be stored in script variables or CSV columns, not hardcoded

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

| Variable | Description | Available |
|----------|-------------|-----------|
| `${_output}` | Last command output | After any `send` command |
| `${_timestamp}` | Script start time (yyyy-MM-dd HH:mm:ss) | Always |
| `${_iteration}` | Current iteration count (0-based) | Inside `while` loops |
| `${item_index}` | Current item index (0-based) | Inside `foreach` loops |
| `${Host_IP}` | Current host IP address | Always (from grid) |
| `${port}` | SSH port for current host | Always (from grid, default 22) |

**Note:** Any column in the host grid becomes available as a variable. For example, if you have a column named `location`, you can use `${location}` in your scripts.

### Variable Substitution Syntax

Variables are substituted using `${variable_name}`:

```yaml
- print: "Host ${Host_IP} has IP ${ip_address}"
- send: show interface ${interface_name}
- set: message = "Status: ${status}"
```

### Array Access and Properties

Lists support index-based access and properties:

```yaml
- extract:
    from: output
    pattern: '(\d+)'
    into: numbers
    match: all

# Access by index
- print: "First: ${numbers[0]}"
- print: "Second: ${numbers[1]}"

# Get list length
- print: "Total count: ${numbers.length}"

# Use length in conditions
- if: numbers.length > 0
  then:
    - print: "Found ${numbers.length} items"

# Dynamic index access
- set: i = 0
- print: "Item at index ${i}: ${numbers[i]}"
```

**Array Properties:**

| Property | Description | Example |
|----------|-------------|---------|
| `.length` | Number of items in list | `${my_list.length}` |

**Note:** The `.length` property works on lists created by `extract` (with `match: all`) or `readfile`.

### Variable Precedence

When multiple sources define the same variable name, the following precedence applies (highest to lowest):

1. **CSV Grid Columns** - Values from the host table (highest priority)
2. **Set/Extract/Captured Variables** - Variables modified during script execution
3. **Script `vars:` Section** - Default values (only set if not already defined)

This means:
- A column named `timeout` in your CSV will override a `timeout` defined in `vars:`
- Using `set: myvar = value` during execution will override the `vars:` default
- You can use `vars:` to provide fallback defaults when columns don't exist

**Example:**
```yaml
---
vars:
  timeout: 30        # Default timeout
  interface: "eth0"  # Default interface

steps:
  # If CSV has a 'timeout' column with value 60, ${timeout} will be 60, not 30
  - print: "Using timeout: ${timeout}"
```

### Variable Type System

Variables can hold different types of data:

| Type | Source | Example |
|------|--------|---------|
| `string` | CSV columns, `set`, `input`, `extract` | `"hello"`, `"192.168.1.1"` |
| `List<string>` | `readfile`, `extract` (with `match: all`), `push()` | `["item1", "item2"]` |
| `int` / `double` | Arithmetic operations | `42`, `3.14` |
| `JsonObject` | `json()`, nested dot assignment | `{"key": "value"}` |

**Type Coercion:**
- String variables containing numbers are automatically converted for arithmetic operations
- List variables can be iterated with `foreach`
- JSON objects can be written to files using `format: json`

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

### Truthy/Falsy Evaluation

When a condition has no comparison operator, it's evaluated as truthy or falsy:

**Falsy Values:**
- `null` or undefined variables
- Empty string `""`
- The string `"false"` (case-insensitive)
- The number `0`
- Empty lists

**Truthy Values:**
- Non-empty strings (except `"false"`)
- Non-zero numbers
- Non-empty lists
- The string `"true"` (case-insensitive)

**Example:**
```yaml
# These all evaluate as truthy
- if: some_variable
  then:
    - print: "Variable has a value"

# Check if variable has content
- if: output
  then:
    - print: "Got output: ${output}"
  else:
    - print: "No output received"
```

### Numeric Comparison Details

When comparing values with `==`, `!=`, `>`, `>=`, `<`, `<=`:

- Both values must parse as numbers for numeric comparison
- If either value cannot be parsed as a number, string comparison is used
- Numeric equality uses a small tolerance (`0.0001`) to handle floating-point precision
- String comparisons are case-insensitive

**Example:**
```yaml
# These are numeric comparisons
- if: count > 10
- if: version >= 2.0
- if: result == 0

# These are string comparisons (because "active" isn't a number)
- if: status == "active"
```

### Regex Pattern Syntax

When using the `matches` operator:

- Patterns can be enclosed in `/pattern/`, `"pattern"`, or `'pattern'`
- Delimiters are automatically stripped
- Patterns are always case-insensitive
- Invalid regex patterns silently return false (they don't cause errors)

**Example:**
```yaml
# All these are equivalent
- if: output matches '/error|warning/'
- if: output matches "error|warning"
- if: output matches 'error|warning'

# Using regex special characters
- if: version matches '^\d+\.\d+\.\d+$'
- if: line matches '^interface\s+\S+'
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

## Output Options

### NoBanner Mode

By default, script execution displays a banner header showing the host, prompt, and script name:

```
############################################################################################################
#################### SCRIPT: 192.168.1.1 FortiGate-VM64-KVM # My Script Name ###############################
############################################################################################################
```

To suppress this banner, set `nobanner: true` in your script header:

```yaml
---
name: Clean Output Script
nobanner: true

steps:
  - send: show version
```

This is useful when:
- You want cleaner output for reports or logs
- You're processing many hosts and don't need the visual separators
- You're using `writefile` to save output and don't want the banner included

---

## Debug Mode

Enable debug mode to see detailed execution information. There are two ways to enable debugging:

### Per-Script Debug Mode

Add `debug: true` to your script header:

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

### Global Debug Mode

Enable via menu: **Edit > Debug Mode**. This affects all script executions until disabled.

**Debug Output Includes:**
- Variable assignments from `set` (shows variable name and value)
- Extracted values from `extract` (shows pattern matches)
- Condition evaluation results in `if` (shows true/false)
- Loop iteration counts in `foreach` and `while`
- File operation results from `readfile` and `writefile`

Debug messages are prefixed with `[DEBUG]` for easy identification.

---

## Working with JSON

SSH Helper provides comprehensive support for creating, manipulating, and writing JSON data. This is useful for generating reports, logging structured data, or integrating with other tools.

### Building JSON Objects

There are two ways to build JSON objects:

**1. Using `json()` function:**
```yaml
- set: data = json("key1", value1, "key2", value2, ...)
```

**2. Using dot notation (nested assignment):**
```yaml
- set: data.key1 = value1
- set: data.nested.key2 = value2
```

### Building JSON Arrays

Use `push()` to build a list, then convert with `json()`:

```yaml
- set: items = push(items, "first")
- set: items = push(items, "second")
- set: arr = json(items)
```

Or create inline arrays:
```yaml
- set: arr = json([], "item1", "item2", "item3")
```

### Automatic Type Detection

When converting to JSON, values are automatically typed:

| Input | JSON Type | Example |
|-------|-----------|---------|
| `"true"` or `"false"` | boolean | `true` |
| Numeric string | number | `42`, `3.14` |
| `"null"` | null | `null` |
| String starting with `{` or `[` | parsed JSON | `{"nested": "object"}` |
| Everything else | string | `"text value"` |

### JSON Workflow Examples

**Collecting Data from Multiple Hosts:**
```yaml
---
name: Collect Host Inventory as JSON
steps:
  # Initialize the results array on first host
  - if: _host_index == 0
    then:
      - writefile:
          path: "C:\\output\\inventory.json"
          format: json
          content: "[]"
          mode: overwrite

  # Gather data
  - send: show version
    capture: version_output
  - extract:
      from: version_output
      pattern: 'Version (\S+)'
      into: version

  # Build this host's record
  - set: record = json("ip", ${Host_IP}, "version", ${version}, "timestamp", ${_timestamp})

  # Append to the JSON array
  - writefile:
      path: "C:\\output\\inventory.json"
      format: json
      content: "[${record}]"
      mode: append
```

**Building Nested Reports:**
```yaml
---
name: Generate Nested JSON Report
steps:
  - send: show version
    capture: version_out
  - extract:
      from: version_out
      pattern: 'Version (\S+)'
      into: sw_version

  - send: show ip interface brief
    capture: interfaces

  # Build nested structure using dot notation
  - set: report.host.ip = ${Host_IP}
  - set: report.host.port = ${port}
  - set: report.software.version = ${sw_version}
  - set: report.metadata.scanned_at = ${_timestamp}
  - set: report.metadata.scanned_by = "SSH Helper"

  # Write the nested JSON
  - writefile:
      path: "C:\\reports\\${Host_IP}.json"
      format: json
      content: "${report}"
      pretty: true
```

**Logging Events as JSON Lines:**
```yaml
---
name: Structured Event Logging
steps:
  - set: event.type = "connection"
  - set: event.host = ${Host_IP}
  - set: event.timestamp = ${_timestamp}
  - set: event.status = "started"

  - writefile:
      path: "C:\\logs\\events.jsonl"
      format: jsonl
      content: "${event}"
      mode: append

  # ... do work ...

  - set: event.status = "completed"
  - set: event.result = "success"

  - writefile:
      path: "C:\\logs\\events.jsonl"
      format: jsonl
      content: "${event}"
      mode: append
```

**Merging Configuration Objects:**
```yaml
---
name: Merge Configurations
vars:
  defaults: '{"timeout": 30, "retries": 3, "debug": false}'

steps:
  # Build host-specific overrides
  - set: overrides = json("host", ${Host_IP}, "timeout", 60)

  # Merge defaults with overrides (supports multiple objects)
  - set: config = json.merge(${defaults}, overrides)

  # config now has: {"timeout": 60, "retries": 3, "debug": false, "host": "192.168.1.1"}
  - print: "Using config: ${config}"
```

**Processing API Responses:**
```yaml
---
name: Process API Response
steps:
  - webhook:
      url: "https://api.example.com/users"
      method: GET
      into: response

  # Check for errors first
  - if: json.exists(response, "error")
    then:
      - set: err = json.get(response, "error.message", "Unknown error")
      - exit: failure "API Error: ${err}"

  # Get array length
  - set: count = json.len(response, "data.users")
  - print: "Found ${count} users"

  # Iterate over users
  - foreach: user in json.items(response, "data.users")
    do:
      - set: name = json.get(user, "name")
      - set: email = json.get(user, "email", "no email")
      - print: "${name}: ${email}"

**Modifying JSON Data:**
```yaml
---
name: Update Configuration
steps:
  # Start with base config
  - set: config = json("server", json("host", "localhost", "port", 8080))

  # Update values
  - set: config = json.set(config, "server.port", 443)
  - set: config = json.set(config, "server.ssl", true)

  # Remove sensitive data before logging
  - set: safe = json.delete(config, "server.password")
  - print: "Config: ${safe}"
```

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

### Example 9: Process List with Array Properties

```yaml
---
name: Interface Inventory
description: Collects all interfaces and stores count in host table

steps:
  - send: show ip interface brief
    capture: output

  # Extract all interface names
  - extract:
      from: output
      pattern: '^(\S+)\s+\d'
      into: interfaces
      match: all

  # Check if we found any interfaces
  - if: interfaces.length == 0
    then:
      - exit: failure "No interfaces found"

  - print: "Found ${interfaces.length} interfaces"

  # Store count back to host table
  - updatecolumn:
      column: interface_count
      value: ${interfaces.length}

  # Process each interface
  - set: up_count = 0
  - foreach: iface in interfaces
    do:
      - send: show interface ${iface} | include line protocol
        capture: status
      - if: status contains "up"
        then:
          - set: up_count = up_count + 1
          - print: "${iface}: UP"

  - print: "${up_count} of ${interfaces.length} interfaces are up"

  # Store results
  - updatecolumn:
      column: interfaces_up
      value: ${up_count}
```

---

### Example 10: Export Network Inventory to JSON

```yaml
---
name: Network Inventory Export
description: Collects device info and exports to a structured JSON file

steps:
  # Gather device information
  - send: show version
    capture: version_output

  - extract:
      from: version_output
      pattern: 'Version (\S+)'
      into: sw_version

  - extract:
      from: version_output
      pattern: 'uptime is (.+?)$'
      into: uptime

  - send: show running-config | include hostname
    capture: hostname_output

  - extract:
      from: hostname_output
      pattern: 'hostname (\S+)'
      into: hostname

  # Build nested JSON structure using dot notation
  - set: device.identity.hostname = ${hostname}
  - set: device.identity.ip = ${Host_IP}
  - set: device.identity.port = ${port}

  - set: device.software.version = ${sw_version}
  - set: device.software.uptime = ${uptime}

  - set: device.metadata.scanned_at = ${_timestamp}
  - set: device.metadata.scanned_by = "SSH Helper"

  # Write individual device JSON file
  - writefile:
      path: "C:\\inventory\\devices\\${Host_IP}.json"
      format: json
      content: "${device}"
      pretty: true

  # Also append to master inventory (JSON array)
  - set: summary = json("ip", ${Host_IP}, "hostname", ${hostname}, "version", ${sw_version})
  - writefile:
      path: "C:\\inventory\\all_devices.json"
      format: json
      content: "[${summary}]"
      mode: append

  # Log the event
  - set: log_entry = json("timestamp", ${_timestamp}, "host", ${Host_IP}, "action", "inventory_collected")
  - writefile:
      path: "C:\\inventory\\audit.jsonl"
      format: jsonl
      content: "${log_entry}"
      mode: append

  - print: "Exported ${hostname} (${Host_IP}) to JSON"
```

---

### Example 11: Compliance Check with JSON Report

```yaml
---
name: Security Compliance Check
description: Checks security settings and generates JSON compliance report

vars:
  required_version: "15.0"

steps:
  # Initialize compliance status
  - set: checks_passed = 0
  - set: checks_failed = 0

  # Check 1: Software version
  - send: show version
    capture: version_output
  - extract:
      from: version_output
      pattern: 'Version (\d+\.\d+)'
      into: current_version

  - set: result.checks.version.current = ${current_version}
  - set: result.checks.version.required = ${required_version}

  - if: current_version >= required_version
    then:
      - set: result.checks.version.status = "PASS"
      - set: checks_passed = checks_passed + 1
    else:
      - set: result.checks.version.status = "FAIL"
      - set: checks_failed = checks_failed + 1

  # Check 2: SSH enabled
  - send: show ip ssh
    capture: ssh_output
    on_error: continue

  - if: ssh_output contains "SSH Enabled"
    then:
      - set: result.checks.ssh.status = "PASS"
      - set: result.checks.ssh.detail = "SSH is enabled"
      - set: checks_passed = checks_passed + 1
    else:
      - set: result.checks.ssh.status = "FAIL"
      - set: result.checks.ssh.detail = "SSH not enabled or not available"
      - set: checks_failed = checks_failed + 1

  # Build final report
  - set: result.host = ${Host_IP}
  - set: result.timestamp = ${_timestamp}
  - set: result.summary.passed = ${checks_passed}
  - set: result.summary.failed = ${checks_failed}

  - if: checks_failed == 0
    then:
      - set: result.summary.overall = "COMPLIANT"
    else:
      - set: result.summary.overall = "NON-COMPLIANT"

  # Write compliance report
  - writefile:
      path: "C:\\compliance\\${Host_IP}_report.json"
      format: json
      content: "${result}"
      pretty: true

  # Update host table
  - updatecolumn:
      column: compliance_status
      value: ${result.summary.overall}

  - print: "${Host_IP}: ${result.summary.overall} (${checks_passed} passed, ${checks_failed} failed)"
```

---

## Tips and Best Practices

### General
1. **Use `capture` and `extract` together**: Capture output first, then extract specific data
2. **Enable `debug: true` while developing**: Helps troubleshoot variable values and conditions
3. **Use `suppress: true` for sensitive commands**: Prevents credentials from appearing in output
4. **Set appropriate timeouts**: Long-running commands may need custom timeout values
5. **Use `on_error: continue` cautiously**: Only when you handle the error case explicitly
6. **Initialize variables in `vars:`**: Provides default values and documents expected variables
7. **Use meaningful variable names**: Makes scripts easier to read and maintain
8. **Test with single host first**: Verify script works before running on multiple hosts

### Working with Data
9. **Use `updatecolumn` for inventory**: Store extracted data back to the host grid for export
10. **Check list length before processing**: Use `${list.length}` to validate data before loops
11. **Use array indexing for specific items**: Access `${list[0]}` for first item, `${list[1]}` for second, etc.

### Working with JSON
12. **Use dot notation for nested structures**: Build complex objects with `data.level1.level2 = value`
13. **Choose the right format for your use case**:
    - `json` for structured reports (supports pretty-printing)
    - `jsonl` for log files and streaming data (one object per line)
14. **Leverage JSON append merging**: Use `mode: append` with `format: json` to build arrays across multiple hosts
15. **Use `json()` for inline objects**: Quick way to create JSON without building nested structures
16. **Automatic type detection**: Numbers and booleans in arrays are preserved (e.g., `"42"` becomes `42` in JSON)
17. **Use `json.merge()` for configuration overrides**: Combine base settings with host-specific overrides
