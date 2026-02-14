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
   - [break](#break---exit-current-loop)
   - [continue](#continue---next-loop-iteration)
   - [foreach](#foreach---loop-over-collections)
   - [while](#while---conditional-loop)
   - [try](#try---structured-error-handling)
   - [exit](#exit---terminate-script)
   - [readfile](#readfile---read-text-files)
   - [writefile](#writefile---write-text-files)
   - [input](#input---prompt-for-user-input)
   - [choose](#choose---single-select-from-list)
   - [multiselect](#multiselect---multiple-select-from-list)
   - [confirm](#confirm---yesno-confirmation)
   - [interactive](#interactive---in-app-ssh-terminal)
   - [updatecolumn](#updatecolumn---update-host-table-column)
   - [updateenvironment](#updateenvironment---update-active-environment-variable)
   - [log](#log---output-with-log-level)
   - [http](#http---http-requests-preferred)
   - [ping](#ping---icmp-reachability-checks)
   - [dns](#dns---dns-lookups)
   - [portcheck](#portcheck---tcp-port-checks)
   - [sftp](#sftp---sftp-upload-and-download)
   - [webhook](#webhook---legacy-http-requests)
   - [parse](#parse---configuration-parsing)
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
  - send:
      command: "command"
  - print:
      message: "message"
```

### Auto-Detection

The system automatically detects YAML scripts by looking for:
- Document marker `---` at the start
- Distinctive top-level sections: `vars:`, `steps:`
- Step keywords: `- send:`, `- print:`, `- wait:`, `- set:`, `- exit:`, `- extract:`, `- if:`, `- break:`, `- continue:`, `- foreach:`, `- while:`, `- try:`, `- readfile:`, `- writefile:`, `- input:`, `- choose:`, `- multiselect:`, `- confirm:`, `- interactive:`, `- updatecolumn:`, `- updateenvironment:`, `- log:`, `- http:`, `- ping:`, `- dns:`, `- portcheck:`, `- sftp:`, `- webhook:`, `- parse:`

Metadata-only keys (for example `name:` or `description:`) are not treated as strong YAML indicators by themselves.

Plain text (without YAML markers) is treated as simple commands to execute line by line.

---

## Commands

### send - Execute SSH Commands

Executes a command on the SSH session.

**Basic Syntax:**
```yaml
- send:
    command: command_text
```

**Shorthand Syntax:**
```yaml
- send: command_text
```

**With Options:**
```yaml
- send:
    command: command_text
    capture: variable_name      # Store output in variable
    suppress: true              # Hide command and output from display
    expect: '/regex_pattern/'   # Regex to wait for in output
    timeout: 30                 # Timeout in seconds for this command
    on_error: continue          # continue or stop (default)
```

Use the map form when you need options (`capture`, `suppress`, `expect`, `timeout`, `on_error`).

**Options:**

| Option | Type | Description |
|--------|------|-------------|
| `capture` | string | Variable name to store command output |
| `suppress` | boolean | When true, hides both command and output from display |
| `expect` | string | Regex pattern to wait for in output (case-insensitive, multiline). When matched, the send completes immediately. |
| `timeout` | integer | Command-specific timeout in seconds |
| `on_error` | string | `continue` to proceed on error, `stop` to halt (default) |

**Notes:**
- `expect` supports `/pattern/`, `"pattern"`, or `'pattern'` delimiters (they are stripped automatically).
- When `expect` is set, the command stops as soon as the pattern matches; it does not automatically wait for the prompt. Omit `expect` to wait for the prompt, or include the prompt in your regex if needed.

**Examples:**
```yaml
# Simple command
- send:
    command: show version

# Capture output for later use
- send:
    command: show ip interface brief
    capture: interfaces

# Hide sensitive command
- send:
    command: show running-config
    suppress: true
    capture: config

# Handle interactive prompts
- send:
    command: enable
    expect: '/Password:/'

- send:
    command: ${enable_password}
    expect: '/#/'

# Continue even if command fails
- send:
    command: ping 192.168.1.1 count 3
    on_error: continue
    capture: ping_result
```

---

### print - Output Messages

Outputs a message to the script output.

**Syntax:**
```yaml
- print:
    message: "message with ${variable} substitution"
```

**Shorthand Syntax:**
```yaml
- print: "message with ${variable} substitution"
```

**Features:**
- Supports variable substitution with `${variable}` syntax
- Always succeeds (never causes script failure)

**Examples:**
```yaml
- print:
    message: "Starting configuration..."
- print:
    message: "Host: ${Host_IP}"
- print:
    message: "Found ${count} interfaces"
- print:
    message: "Status: ${status}"
```

---

### wait - Pause Execution

Pauses script execution for a specified number of seconds.

**Syntax:**
```yaml
- wait:
    seconds: seconds
```

**Shorthand Syntax:**
```yaml
- wait: 5
```

**Examples:**
```yaml
# Wait 5 seconds
- wait:
    seconds: 5

# Wait after reboot command
- send:
    command: reload
- wait:
    seconds: 30
- send:
    command: show version
```

---

### set - Variable Assignment

Sets or modifies variable values with expression support.

**Syntax:**
```yaml
- set:
    expression: variable_name = expression
```

**Shorthand Syntax:**
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
| replace() | `out = replace(text, "old", "new")` | Replace string content |
| split() | `arr = split(text, ",")` | Split string into list |
| join() | `text = join(arr, ",")` | Join list into string |
| substring() | `part = substring(text, 0, 5)` | Extract string segment |
| sort() | `sorted = sort(arr, "desc")` | Sort list values |
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
| json.pop() | `last = json.pop(arr)` | Remove and return last element |
| json.last() | `last = json.last(arr)` | Get last element (non-destructive) |
| json.unshift() | `arr = json.unshift(arr, value)` | Prepend to JSON array |
| json.shift() | `first = json.shift(arr)` | Remove and return first element |
| json.first() | `first = json.first(arr)` | Get first element (non-destructive) |
| json.slice() | `sub = json.slice(arr, 0, 3)` | Extract array subset |
| json.concat() | `all = json.concat(arr1, arr2)` | Concatenate arrays |
| json.indexOf() | `idx = json.indexOf(arr, value)` | Find element index |
| Nested assignment | `obj.key.subkey = value` | Assign to nested path |

**Basic Examples:**
```yaml
# Literal values
- set:
    expression: timeout = 30
- set:
    expression: interface = "eth0"

# Arithmetic
- set:
    expression: i = 0
- set:
    expression: i = i + 1
- set:
    expression: remaining = total - processed
- set:
    expression: doubled = count * 2
- set:
    expression: average = total / count

# String manipulation
- set:
    expression: message = "Device: ${Host_IP}"
- set:
    expression: trimmed = trim(raw_input)
- set:
    expression: upper_name = upper(hostname)

# Get length
- set:
    expression: line_count = length(output)
- set:
    expression: num_items = length(items)
```

**Array Functions:**
```yaml
# Create and build an array
- set:
    expression: results = push(results, ${Host_IP})
- set:
    expression: results = push(results, ${status})

# Get array length
- set:
    expression: count = length(results)
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
- set:
    expression: data = json("host", ${Host_IP}, "status", "up", "port", 22)
# Result: {"host":"192.168.1.1","status":"up","port":22}

# Pretty-printed object
- set:
    expression: data = json("host", ${Host_IP}, "status", "up", pretty)

# Create array from list variable
- set:
    expression: items = push(items, "a")
- set:
    expression: items = push(items, "b")
- set:
    expression: arr = json(items)
# Result: ["a","b"]

# Create array inline with []
- set:
    expression: arr = json([], "item1", "item2", "item3")
# Result: ["item1","item2","item3"]

# Nested objects
- set:
    expression: data = json("server", json("host", ${ip}, "port", 22), "active", true)
```

---

**`json.get(json, path, default?)` - Extract Value**

Extracts a value using dot notation path. Supports optional default value.

```yaml
# Basic extraction
- set:
    expression: name = json.get(response, "data.user.name")
- set:
    expression: email = json.get(response, "data.user.email")

# With default value (returned if path doesn't exist)
- set:
    expression: port = json.get(config, "server.port", 22)
- set:
    expression: timeout = json.get(config, "settings.timeout", 30)

# Array indexing with [n] syntax
- set:
    expression: first = json.get(data, "items[0].name")
- set:
    expression: nested = json.get(data, "users[0].addresses[1].city")
```

---

**`json.set(json, path, value)` - Set Value at Path**

Sets a value at a path, creating intermediate objects as needed.

```yaml
# Update a nested value
- set:
    expression: config = json.set(config, "server.port", 8080)

# Add new keys
- set:
    expression: data = json.set(data, "metadata.created", ${_timestamp})

# Set array element
- set:
    expression: data = json.set(data, "users[0].active", true)
```

---

**`json.delete(json, path)` - Remove Key or Element**

Removes a key from an object or element from an array.

```yaml
# Remove a key
- set:
    expression: user = json.delete(user, "password")
- set:
    expression: data = json.delete(data, "sensitive.ssn")

# Remove array element by index
- set:
    expression: arr = json.delete(arr, "items[2]")
```

---

**`json.merge(obj1, obj2, ...)` - Deep Merge Objects**

Merges multiple objects. Later objects override earlier ones.

```yaml
# Merge two objects
- set:
    expression: base = json("name", "server1", "type", "linux")
- set:
    expression: updates = json("status", "active", "type", "ubuntu")
- set:
    expression: merged = json.merge(base, updates)
# Result: {"name":"server1","type":"ubuntu","status":"active"}

# Merge multiple objects (variadic)
- set:
    expression: final = json.merge(defaults, env_config, user_overrides)
```

---

**`json.format(json, style?)` - Format JSON**

Formats JSON for display. Default is pretty-printed.

```yaml
# Pretty print (default)
- set:
    expression: formatted = json.format(data)
- set:
    expression: formatted = json.format(data, pretty)

# Compact (single line)
- set:
    expression: compact = json.format(data, compact)
```

---

**`json.exists(json, path)` - Check Path Existence**

Returns true/false indicating whether a path exists. Distinguishes between null values and missing keys.

```yaml
# Check for error response
- if:
    condition: json.exists(response, "error.code")
    then:
      - set:
          expression: err = json.get(response, "error.code")
      - log:
          message: "Error: ${err}"
          level: error

# Safer than checking for empty
- if:
    condition: json.exists(config, "optional") and json.get(config, "optional") != null
    then:
      - print:
          message: "Optional is set and not null"
```

---

**`json.len(json, path?)` - Get Length**

Returns array length or object key count.

```yaml
# Array length
- set:
    expression: count = json.len(response, "data.items")

# Object key count
- set:
    expression: num_keys = json.len(config)

# Use in loops
- if:
    condition: json.len(users) > 0
    then:
      - print:
          message: "Found ${json.len(users)} users"
```

---

**`json.type(json, path?)` - Get Value Type**

Returns the type: `"object"`, `"array"`, `"string"`, `"number"`, `"boolean"`, or `"null"`.

```yaml
- set:
    expression: t = json.type(response, "data")
- if:
    condition: t == "array"
    then:
      - print:
          message: "Data is an array"
```

---

**`json.keys(json, path?)` - Get Object Keys**

Returns object keys as a list for iteration.

```yaml
- set:
    expression: fields = json.keys(user)
- foreach:
    iterator: field in fields
    do:
      - set:
          expression: val = json.get(user, field)
      - print:
          message: "${field}: ${val}"
```

---

**`json.values(json, path?)` - Get Object Values**

Returns object values as a list.

```yaml
- set:
    expression: ips = json.values(servers)
- foreach:
    iterator: ip in ips
    do:
      - send:
          command: ping ${ip} -c 1
```

---

**`json.items(json, path?)` - Extract Array or Object Entries**

For arrays: returns elements as a list.
For objects: returns `{"key": k, "value": v}` entries.

```yaml
# Array iteration
- foreach:
    iterator: user in json.items(response, "data.users")
    do:
      - set:
          expression: name = json.get(user, "name")
      - set:
          expression: email = json.get(user, "email")
      - print:
          message: "User: ${name} (${email})"

# Object iteration (key-value pairs)
- set:
    expression: servers = json("web", "10.0.0.1", "db", "10.0.0.2")
- foreach:
    iterator: entry in json.items(servers)
    do:
      - set:
          expression: name = json.get(entry, "key")
      - set:
          expression: ip = json.get(entry, "value")
      - print:
          message: "${name}: ${ip}"
```

---

**Array Manipulation Functions**

```yaml
# Append to array
- set:
    expression: arr = json.push(arr, "new_item")
- set:
    expression: arr = json.push(arr, json("key", "value"))

# Remove and return last element (destructive)
- set:
    expression: last = json.pop(arr)

# Get last element without modifying the array
- set:
    expression: last_view = json.last(arr)

# Prepend to array
- set:
    expression: arr = json.unshift(arr, "first")

# Remove and return first element (destructive)
- set:
    expression: first = json.shift(arr)

# Get first element without modifying the array
- set:
    expression: first_view = json.first(arr)

# Slice array (supports negative indices)
- set:
    expression: first_three = json.slice(arr, 0, 3)
- set:
    expression: last_two = json.slice(arr, -2)

# Concatenate arrays
- set:
    expression: all = json.concat(arr1, arr2, arr3)

# Find element index (-1 if not found)
- set:
    expression: idx = json.indexOf(arr, "search_value")
- if:
    condition: idx >= 0
    then:
      - print:
          message: "Found at index ${idx}"
```

> Migration note: If older scripts used `json.pop()` or `json.shift()` as non-destructive reads, switch those calls to `json.last()` and `json.first()`.
> Destructive note: `json.pop()` and `json.shift()` only mutate writable top-level array variables (`arr` or `${arr}`); non-writable expressions return `null` with a warning.

**Nested Assignment (Dot Notation):**

You can build nested JSON objects using dot notation. Intermediate objects are created automatically:

```yaml
# Build a nested structure
- set:
    expression: data.server.name = ${hostname}
- set:
    expression: data.server.ip = ${Host_IP}
- set:
    expression: data.server.port = 22
- set:
    expression: data.metadata.timestamp = ${_timestamp}
- set:
    expression: data.metadata.scanned_by = "SSH Helper"

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
- foreach:
    iterator: ip in ip_addresses
    do:
      - print:
          message: "Found IP: ${ip}"
```

**Examples:**
```yaml
# Extract version number
- send:
    command: show version
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
- if:
    condition: condition
    then:
      - step1
      - step2
    elif:           # Optional ordered list
      - if:
          condition: other_condition
          then:
            - step3
    else:           # Optional
      - step4
```

**Shorthand Header:**
```yaml
- if: condition
  then:
    - step1
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
- if:
    condition: status == "up"
    then:
      - print:
          message: "Interface is up"
    else:
      - print:
          message: "Interface is down"

# Regex match
- if:
    condition: output matches 'error|failed'
    then:
      - exit:
          message: failure "Error detected in output"

# Multiple conditions
- if:
    condition: count > threshold and status == "active"
    then:
      - print:
          message: "Threshold exceeded on active device"

# Check if variable is defined
- if:
    condition: custom_timeout is defined
    then:
      - set:
          expression: timeout = custom_timeout
    else:
      - set:
          expression: timeout = 30

# Nested conditions
- if:
    condition: type == "router"
    then:
      - if:
          condition: vendor == "cisco"
          then:
            - send:
                command: show ip route
          else:
            - send:
                command: get router info routing
```

---

### foreach - Loop Over Collections

Iterates over items in a collection.

**Syntax:**
```yaml
- foreach:
    iterator: item in collection
    do:
      - step1
      - step2
    when: optional_filter     # Optional filter condition
```

**Shorthand Header:**
```yaml
- foreach: item in collection
  do:
    - step1
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

- foreach:
    iterator: iface in interfaces
    do:
      - send:
          command: show interface ${iface}
      - print:
          message: "Checked ${iface}"

# Loop with index (index variable uses your iterator name)
- foreach:
    iterator: line in output
    do:
      - print:
          message: "Line ${line_index}: ${line}"

# Loop with filter
- foreach:
    iterator: iface in interfaces
    when: iface startswith "eth"
    do:
      - print:
          message: "Ethernet interface: ${iface}"

# Loop over lines in output
- send:
    command: show ip interface brief
    capture: output

- foreach:
    iterator: line in output
    when: line contains "up"
    do:
      - print:
          message: "Active: ${line}"
```

---

### while - Conditional Loop

Repeatedly executes a block while a condition is true.

**Syntax:**
```yaml
- while:
    condition: condition
    do:
      - step1
      - step2
```

**Shorthand Header:**
```yaml
- while: condition
  do:
    - step1
```

**Features:**
- Condition re-evaluated each iteration
- Maximum 10,000 iterations (safety limit)
- Optional per-loop override: `max_iterations`
- `${_iteration}` variable tracks iteration count (0-based)
- Supports explicit `break` and `continue` loop control

**Examples:**
```yaml
# Counter-based loop
- set:
    expression: i = 0
- while:
    condition: i < 5
    do:
      - print:
          message: "Iteration ${i}"
      - set:
          expression: i = i + 1

# Retry loop
- set:
    expression: retry = 0
- set:
    expression: success = ""
- while:
    condition: retry < 3 and success is empty
    max_iterations: 20
    do:
      - send:
          command: ping 192.168.1.1 count 1
          capture: result
          on_error: continue
      - if:
          condition: result contains "1 received"
          then:
            - set:
                expression: success = "yes"
          else:
            - set:
                expression: retry = retry + 1
            - wait:
                seconds: 2

# Poll for condition
- set:
    expression: ready = ""
- while:
    condition: ready is empty
    do:
      - send:
          command: show status
          capture: status
      - if:
          condition: status contains "ready"
          then:
            - set:
                expression: ready = "yes"
          else:
            - wait:
                seconds: 5
      - if:
          condition: _iteration > 60
          then:
            - exit:
                message: failure "Timeout waiting for ready state"
```

---

### break - Exit Current Loop

Exits the current `foreach` or `while` loop immediately.

**Syntax:**
```yaml
- break: true
```

`break` is only valid inside loop bodies.

---

### continue - Next Loop Iteration

Skips the rest of the current `foreach` or `while` iteration.

**Syntax:**
```yaml
- continue: true
```

`continue` is only valid inside loop bodies.

---

### try - Structured Error Handling

Runs steps in a `try` block, optionally handles failures in `catch`, and always runs `finally`.

**Syntax:**
```yaml
- try:
    do:
      - send:
          command: risky command
      catch:
        - print:
            message: "Caught error: ${_last_error}"
      finally:
        - log: "Cleanup complete"
```

---

### exit - Terminate Script

Ends script execution with a status and message.

**Syntax:**
```yaml
- exit:
    status: success
    message: "message"
- exit:
    status: failure
    message: "message"
- exit:
    status: error
    message: "message"
```

**Shorthand Syntax:**
```yaml
- exit: "message"                 # Defaults to success
- exit: success "message"
- exit: failure "message"
- exit: error "message"
```

**Status Types:**
- **success**: Script completed successfully
- **failure** (alias: `fail`): Script detected a failure condition
- **error**: An unexpected error occurred

**Examples:**
```yaml
# Success exit
- exit:
    status: success
    message: "Configuration applied successfully"

# Failure exit
- if:
    condition: status != "up"
    then:
      - exit:
          status: failure
          message: "Interface failed to come up"

# Error with variable
- exit:
    status: error
    message: "Unexpected response: ${output}"

# Simple exit (defaults to success)
- exit:
    status: success
    message: "Task completed"
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

- print:
    message: "Found ${blocked_ips.length} IPs to process"

- foreach:
    iterator: ip in blocked_ips
    do:
      - print:
          message: "Processing: ${ip}"

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
- foreach:
    iterator: ip in processed_ips
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
- set:
    expression: hosts = push(hosts, ${Host_IP})
- set:
    expression: hosts = push(hosts, ${other_ip})
- writefile:
    path: "C:\\output\\hosts.json"
    format: json
    content: "${hosts}"
    pretty: true

# Write a JSON object
- set:
    expression: data = json("host", ${Host_IP}, "status", "success", "port", 22)
- writefile:
    path: "C:\\output\\result.json"
    format: json
    content: "${data}"
    pretty: true

# Write nested object built with dot notation
- set:
    expression: result.server.ip = ${Host_IP}
- set:
    expression: result.server.hostname = ${hostname}
- set:
    expression: result.scan.timestamp = ${_timestamp}
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
- set:
    expression: item = json("ip", "192.168.1.1", "status", "up")
- writefile:
    path: "C:\\output\\results.json"
    format: json
    content: "[${item}]"
    mode: overwrite

# Subsequent writes append to the array
- set:
    expression: item = json("ip", "192.168.1.2", "status", "down")
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
- set:
    expression: event = json("timestamp", ${_timestamp}, "host", ${Host_IP}, "action", "scanned")
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
- set:
    expression: row = "${Host_IP},${hostname},${version},${status}"
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

- if:
    condition: confirm != "yes"
    then:
      - exit:
          message: "Operation cancelled"
```

---

### choose - Single-Select from List

Prompts the user to select one option from a list during script execution.

**Syntax:**
```yaml
- choose:
    prompt: "Select an option:"
    into: variable_name
    options:
      - option1
      - option2
    default: "option1"             # Optional
```

**With Label/Value Pairs:**
```yaml
- choose:
    prompt: "Select protocol:"
    into: port
    options:
      - label: "SSH (22)"
        value: "22"
      - label: "HTTPS (443)"
        value: "443"
    default: "22"
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `prompt` | No | `"Select an option:"` | Text to display to the user |
| `into` | Yes | - | Variable name to store the selected value |
| `options` | Yes | - | List of options (strings or label/value pairs) |
| `default` | No | - | Pre-selected option (matched against values) |

**Features:**
- Options can be simple strings (label and value are the same) or label/value pairs
- Dialog appears during script execution with a selection list
- Double-click selects and confirms
- User can cancel (script will fail unless `on_error: continue`)
- Variables can be used in `prompt`, `default`, and option labels/values

**Examples:**
```yaml
# Simple string options
- choose:
    prompt: "Select device type:"
    into: device_type
    options:
      - router
      - switch
      - firewall
    default: router

- print:
    message: "Selected: ${device_type}"

# Label/value pairs (display differs from stored value)
- choose:
    prompt: "Select management protocol:"
    into: mgmt_port
    options:
      - label: "SSH (22)"
        value: "22"
      - label: "HTTPS (443)"
        value: "443"
      - label: "HTTP (80)"
        value: "80"
    default: "22"

- print:
    message: "Will connect on port ${mgmt_port}"

# Dynamic options with variable substitution
- choose:
    prompt: "Configure ${Host_IP} as:"
    into: role
    options:
      - primary
      - secondary
      - standby
```

---

### multiselect - Multiple-Select from List

Prompts the user to select multiple options from a checklist during script execution.

**Syntax:**
```yaml
- multiselect:
    prompt: "Select options:"
    into: variable_name
    options:
      - option1
      - option2
      - option3
    min: 1                         # Optional: minimum selections
    max: 3                         # Optional: maximum selections
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `prompt` | No | `"Select options:"` | Text to display to the user |
| `into` | Yes | - | Variable name to store selected values |
| `options` | Yes | - | List of options (strings or label/value pairs) |
| `min` | No | - | Minimum number of selections required |
| `max` | No | - | Maximum number of selections allowed |

**Features:**
- Stores result as a list: use `${into[0]}`, `${into[1]}`, `${into.length}` for indexing
- Also sets `${into}_count` with the number of selected items
- `${into}` in print renders as comma-separated text
- Works with `foreach: item in ${into}` for iteration
- Supports label/value pairs (same as choose)
- User can cancel (script will fail unless `on_error: continue`)
- Variables can be used in `prompt` and option labels/values

**Examples:**
```yaml
# Select interfaces to configure
- multiselect:
    prompt: "Select interfaces to configure:"
    into: selected_interfaces
    options:
      - GigabitEthernet0/0
      - GigabitEthernet0/1
      - GigabitEthernet0/2
      - Loopback0
    min: 1
    max: 3

- print:
    message: "Selected ${selected_interfaces_count} interfaces: ${selected_interfaces}"

# Iterate over selections
- foreach: iface in ${selected_interfaces}
    do:
      - print:
          message: "Configuring ${iface}..."
      - send:
          command: "interface ${iface}"

# Access by index
- print:
    message: "First selected: ${selected_interfaces[0]}"

# With label/value pairs
- multiselect:
    prompt: "Select services to restart:"
    into: services
    options:
      - label: "Web Server (nginx)"
        value: "nginx"
      - label: "Database (postgresql)"
        value: "postgresql"
      - label: "Cache (redis)"
        value: "redis"
    min: 1
```

---

### confirm - Yes/No Confirmation

Prompts the user with a yes/no confirmation dialog during script execution.

**Syntax:**
```yaml
- confirm:
    prompt: "Are you sure?"
    into: variable_name
    default: false                 # Optional (default: false)
```

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `prompt` | No | `"Are you sure?"` | Text to display to the user |
| `into` | Yes | - | Variable name to store result (`"true"` or `"false"`) |
| `default` | No | `false` | Which button is focused: `true` = Yes, `false` = No |

**Features:**
- Stores `"true"` (Yes) or `"false"` (No/Escape) as a string
- Default controls which button is pre-focused
- Escape key acts as No
- Confirm never fails — it always stores a value (unlike `input` which fails on cancel)
- Variables can be used in `prompt`

**Examples:**
```yaml
# Simple confirmation
- confirm:
    prompt: "Apply configuration changes?"
    into: confirmed

- if:
    condition: confirmed == "true"
    then:
      - print:
          message: "Applying changes..."
    else:
      - exit:
          message: "Operation cancelled"

# With variable substitution and default
- confirm:
    prompt: "Reload ${Host_IP}? This will cause a brief outage."
    into: do_reload
    default: false

- if:
    condition: do_reload == "true"
    then:
      - send:
          command: reload
          expect: '/confirm/'
      - send:
          command: "y"

# Destructive action confirmation
- confirm:
    prompt: "Delete all backup files older than 30 days?"
    into: delete_confirmed
    default: false

- if:
    condition: delete_confirmed == "false"
    then:
      - exit:
          message: "Deletion cancelled"
```

---

### interactive - In-App SSH Terminal

Opens an in-app SSH terminal window and pauses the script until the terminal window is closed.

**Syntax (map only):**
```yaml
- interactive:
    session: separate    # Optional: separate|shared (default: separate)
    on_error: stop       # Optional: continue|stop (default: stop)
```

**Important behavior:**
- `interactive` is map-only. Scalar shorthand (for example `- interactive`) is invalid.
- Closing the terminal window by the user is treated as success and script execution continues.
- `session: separate` opens a new SSH terminal connection using current host credentials/settings.
- `session: shared` attaches to the active script SSH session. If unavailable, the step fails with `InteractiveSharedUnavailable`.
- Stop/cancel while terminal is open force-closes the terminal/session and cancels script execution.
- `interactive` is single-host only:
  - Multi-host script runs are rejected in preflight.
  - Folder runs are rejected in preflight.

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `session` | No | `separate` | Session model: `separate` or `shared` |
| `on_error` | No | `stop` | Error handling: `continue` or `stop` |

**Examples:**
```yaml
# Separate connection (defaults)
- interactive: {}

# Explicit separate
- interactive:
    session: separate

# Shared session
- interactive:
    session: shared
    on_error: continue
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
- set:
    expression: status_msg = "OK - ${interface_count} interfaces"
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

### updateenvironment - Update Active Environment Variable

Persists a value into the active environment profile's variable set. The updated value is also available immediately for later steps in the same script execution.

**Syntax:**
```yaml
- updateenvironment:
    variable: "variable_name"
    value: "value_or_${variable}"
```

**Parameters:**

| Parameter | Required | Description |
|-----------|----------|-------------|
| `variable` | Yes | Environment variable name to update |
| `value` | Yes | Value to persist (supports variable substitution) |

**Behavior:**
- Updates the active environment profile (for example `Default`, `prod`, `staging`)
- Persists immediately to configuration
- Makes the new value available to later steps in the same script
- Affects subsequent executions that use the same active environment
- If multiple hosts update the same variable in one run, the last processed update wins

**Examples:**
```yaml
# Save a refreshed token
- send:
    command: refresh-token
    capture: token_output
- extract:
    from: token_output
    pattern: 'token=(\S+)'
    into: new_token
- updateenvironment:
    variable: api_token
    value: ${new_token}

# Persist last successful scan time
- updateenvironment:
    variable: last_scan_utc
    value: ${_timestamp}
```

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

### http - HTTP Requests (Preferred)

Makes HTTP requests with explicit controls for authentication, redirect behavior, TLS verification, and response capture.

**Syntax:**
```yaml
- http:
    url: "https://api.example.com/endpoint"
    method: GET
    headers:
      Accept: "application/json"
    into: api_response
    on_error: continue    # Optional alias for step-level on_error
```

`on_error` can be set either inside the `http` map or at step level. If both are provided, the step-level value wins.

**Locked Defaults:**
- `method: GET`
- `timeout: 30`
- `follow_redirects: true`
- `allow_failure: false`
- `verify_tls: true`
- `auth: none`

**Parameters:**

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `url` | Yes | - | Target URL (`http://` or `https://`) |
| `method` | No | `GET` | `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS` |
| `body` | No | - | Request body |
| `headers` | No | - | HTTP headers map |
| `into` | No | - | Variable prefix for response capture |
| `timeout` | No | `30` | Request timeout in seconds |
| `follow_redirects` | No | `true` | Follow HTTP redirects |
| `allow_failure` | No | `false` | Treat non-2xx response as success |
| `verify_tls` | No | `true` | Validate TLS certificates |
| `auth` | No | `none` | `none`, `basic`, `bearer` |
| `username` | No | - | Required for `auth: basic` |
| `password` | No | - | Required for `auth: basic` |
| `token` | No | - | Required for `auth: bearer` |
| `content_type` | No | - | `json`, `form`, `text`, `xml` shorthand |

**Capture Variables:**
- `${into}`: response body
- `${into}_status`: numeric status code
- `${into}_headers`: response headers as JSON

**Failure Semantics:**
- Non-2xx responses: fail unless `allow_failure: true`
- Transport/runtime failures: handled by `on_error` (`stop`/`continue`)
- `into` variables are reset before execution to prevent stale values

**Case-Insensitive Option Handling:**
- `method`, `auth`, and `content_type` accept any case and are normalized internally.
- Example: `method: post`, `auth: BEARER`, `content_type: XML` are valid.

**Content-Type Rules:**
- Shorthand mappings:
  - `json` -> `application/json`
  - `form` -> `application/x-www-form-urlencoded`
  - `text` -> `text/plain`
  - `xml` -> `application/xml`
- If `headers.Content-Type` is provided, it overrides `content_type`.

**TLS Certificate Behavior (Secure Default):**
- `verify_tls: true` (default) enforces certificate validation.
- `verify_tls: false` disables certificate validation for that step only.
- Keep TLS validation enabled in production; disable only in controlled environments (for example lab/self-signed endpoints you trust).

**Examples:**
```yaml
# Bearer auth with JSON response capture
- http:
    url: "https://api.example.com/devices/${Host_IP}"
    method: get
    auth: BEARER
    token: "${api_token}"
    into: api_result

# Explicit Content-Type header overrides shorthand
- http:
    url: "https://api.example.com/submit"
    method: POST
    content_type: json
    headers:
      Content-Type: "text/plain"
    body: "raw text payload"
```

---

### ping - ICMP Reachability Checks

Performs ICMP checks and captures availability metrics.

**Syntax:**
```yaml
- ping: "8.8.8.8"     # Shorthand

- ping:
    host: "8.8.8.8"
    count: 4
    timeout: 3000
    into: ping_state
```

**Defaults:**
- `count: 4`
- `timeout: 3000` (milliseconds per probe)

**Capture Variables:**
- `${into}`: `success` or `failure`
- `${into}_avg`: average latency in ms (empty when complete failure)
- `${into}_loss`: packet loss percentage

**Notes:**
- String fields support variable substitution.
- Complete failure returns `${into}=failure`, `${into}_avg=""`, `${into}_loss=100`.

---

### dns - DNS Lookups

Resolves DNS records and captures results as a list.

**Syntax:**
```yaml
- dns:
    host: "example.com"
    type: A
    timeout: 10
    into: dns_records
```

**Defaults:**
- `type: A`
- `timeout: 10` (seconds)

**Accepted `type` values:**
- `A`
- `AAAA`
- `PTR`

`type` is case-insensitive (`aaaa`, `Ptr`, etc. are accepted).

**Capture Variables:**
- `${into}`: `List<string>`
- `${into}_count`: number of records

**No-Record Behavior:**
- No records is treated as success with:
  - `${into}` = empty list
  - `${into}_count` = `0`

This enables safe indexing/length checks:
```yaml
- if:
    condition: dns_records_count > 0
    then:
      - print:
          message: "First record: ${dns_records[0]}"
```

---

### portcheck - TCP Port Checks

Checks TCP reachability for a host/port.

**Syntax:**
```yaml
- portcheck:
    host: "10.0.0.10"
    port: 22
    timeout: 5
    into: port_state
```

**Defaults:**
- `port: 22`
- `timeout: 5` (seconds)

**Capture Variables:**
- `${into}`: `open`, `closed`, or `timeout`
- `${into}_latency`: connection latency in ms when available (empty on timeout)

---

### sftp - SFTP Upload and Download

Transfers files using SFTP (SSH.NET backend).

**Syntax:**
```yaml
- sftp:
    action: upload
    local_path: "C:\\exports\\report.txt"
    remote_path: "/tmp/report.txt"
    into: sftp_result
```

**Defaults:**
- `overwrite: true`
- `timeout: 120` (seconds)

**Parameters:**
- Required: `action`, `local_path`, `remote_path`
- Optional overrides: `host`, `port`, `username`, `password`
- Fallback host/credentials come from current host context when overrides are omitted

**Accepted `action` values:**
- `upload`
- `download`

`action` is case-insensitive (`UPLOAD`, `DoWnLoAd`, etc. are accepted).

**Capture Variables:**
- `${into}`: `success` or `failure`
- `${into}_bytes`: transferred bytes (`0` on failure)

**`overwrite: false` Behavior:**
- Download fails if destination local file already exists.
- Upload fails if destination remote path already exists.

---

### webhook - Legacy HTTP Requests

Makes HTTP requests to external APIs and captures responses. `webhook` remains supported for compatibility, but `http` is the preferred command for new scripts.

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
| `on_error` | No | `stop` | Error handling: `continue` or `stop` (also accepted at step level) |

**Security note:** URL scheme is restricted to `http`/`https`, but internal/private destination filtering is intentionally not enforced. Only run trusted scripts when webhook targets are user-controlled.

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

- if:
    condition: api_response_status == 200
    then:
      - print:
          message: "API is healthy"
    else:
      - print:
          message: "API returned status: ${api_response_status}"

# POST with JSON body
- set:
    expression: payload = json("host", ${Host_IP}, "status", ${status})
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
- set:
    expression: event = json("timestamp", ${_timestamp}, "host", ${Host_IP}, "event", "scan_complete")
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

- if:
    condition: validation_status == 200
    then:
      - print:
          message: "Validation successful"
    else:
      - if:
          condition: validation_status is defined
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

### parse - Configuration Parsing

Parses device configuration text into structured JSON data. Currently supports FortiGate/FortiOS configuration format. The parsed data can be accessed using the `json.*` functions and exported to CSV or JSON files.

**Syntax:**
```yaml
- parse:
    format: fortigate
    from: source_variable
    into: destination_variable
    sections:              # Optional: limit parsing to specific sections
      - system interface
      - firewall policy
```

**Parameters:**

| Parameter | Required | Description |
|-----------|----------|-------------|
| `format` | Yes | Configuration format: `fortigate` (or `fortios`) |
| `from` | Yes | Variable containing raw configuration text |
| `into` | Yes | Variable name to store the parsed result |
| `sections` | No | List of section paths to parse (for performance with large configs) |

**Supported Formats:**

| Format | Aliases | Description |
|--------|---------|-------------|
| `fortigate` | `fortios` | FortiGate/FortiOS configuration (`config`/`edit`/`set`/`next`/`end` syntax) |

**FortiGate Configuration Structure:**

FortiGate configs use a hierarchical structure:

```
config system interface
    edit "wan1"
        set vdom "root"
        set ip 10.0.0.1 255.255.255.0
    next
    edit "lan1"
        set vdom "root"
    next
end
```

This is parsed into JSON:

```json
{
  "system": {
    "interface": {
      "wan1": {
        "vdom": "root",
        "ip": "10.0.0.1 255.255.255.0"
      },
      "lan1": {
        "vdom": "root"
      }
    }
  }
}
```

**Parsing Rules:**
- `config <path>` creates nested objects along the path
- `edit "name"` creates named entries within the current section
- `set key value` assigns string values to keys
- Multi-value sets (e.g., `set member "a" "b"`) become arrays
- `unset` directives are omitted from the output
- Comments (lines starting with `#`) are ignored

**Accessing Parsed Data:**

After parsing, use the `json.*` functions to access the data:

```yaml
# Get a specific value
- set:
    expression: hostname = json.get(config, "system.global.hostname")

# List all interface names
- set:
    expression: interfaces = json.keys(config, "system.interface")

# Iterate over interfaces
- foreach:
    iterator: iface in json.keys(config, "system.interface")
    do:
      - set:
          expression: ip = json.get(config, "system.interface.${iface}.ip", "N/A")
      - print:
          message: "${iface}: ${ip}"
```

**Examples:**

```yaml
# Basic parsing
- send:
    command: show full-configuration
    capture: raw_config
    suppress: true
    timeout: 120

- parse:
    format: fortigate
    from: raw_config
    into: config

- set:
    expression: hostname = json.get(config, "system.global.hostname", "unknown")
- print:
    message: "Hostname: ${hostname}"

# Parse only specific sections (faster for large configs)
- send:
    command: show full-configuration
    capture: raw_config
    suppress: true

- parse:
    format: fortigate
    from: raw_config
    into: config
    sections:
      - system interface
      - system global

# Get all interfaces as a list
- set:
    expression: interfaces = json.keys(config, "system.interface")
- print:
    message: "Found ${interfaces.length} interfaces"

# Check if a section exists
- if:
    condition: json.exists(config, "firewall.policy")
    then:
      - print:
          message: "Firewall policies configured"
```

**Building Reports from Parsed Config:**

```yaml
# Export interface inventory to CSV
- send:
    command: show full-configuration system interface
    capture: raw_config
    suppress: true

- parse:
    format: fortigate
    from: raw_config
    into: config

- set:
    expression: interfaces = json.keys(config, "system.interface")
- set:
    expression: report = json([])

- foreach:
    iterator: iface in interfaces
    do:
      - set:
          expression: ip = json.get(config, "system.interface.${iface}.ip", "N/A")
      - set:
          expression: vdom = json.get(config, "system.interface.${iface}.vdom", "root")
      - set:
          expression: type = json.get(config, "system.interface.${iface}.type", "unknown")
      - set:
          expression: row = json("name", "${iface}", "ip", "${ip}", "vdom", "${vdom}", "type", "${type}")
      - set:
          expression: report = json.push(report, ${row})

- writefile:
    path: "C:\\reports\\${Host_IP}_interfaces.csv"
    format: csv
    headers: [name, ip, vdom, type]
    content: "${report}"

- print:
    message: "Exported ${interfaces.length} interfaces to CSV"
```

**Nested Config Blocks:**

FortiGate configs can have nested `config` blocks inside `edit` entries:

```
config firewall policy
    edit 1
        set srcintf "wan1"
        config dstaddr
            edit "server1"
                set ip 192.168.1.10
            next
        end
    next
end
```

These are accessible via nested paths:

```yaml
- set:
    expression: addr = json.get(config, "firewall.policy.1.dstaddr.server1.ip")
```

---

## Variables

### Variable Sources

1. **CSV Grid Columns**: Any column in the host grid
   ```yaml
   - print:
       message: "Host: ${Host_IP}"      # Required column
   - print:
       message: "User: ${username}"     # Custom column
   ```

2. **Active Environment Variables**: Variables from the currently selected environment profile
   ```yaml
   - print:
       message: "Token: ${api_token}"
   ```

3. **Script Variables** (from `vars:` section):
   ```yaml
   vars:
     timeout: 30
     interface: "eth0"
   steps:
     - print:
         message: "Timeout: ${timeout}"
   ```

4. **Captured Variables**:
   ```yaml
   - send:
       command: show version
       capture: version_output
   - print:
       message: "${version_output}"
   ```

5. **Extracted Variables**:
   ```yaml
   - extract:
       from: output
       pattern: 'IP: (.+?)$'
       into: ip_address
   - print:
       message: "IP: ${ip_address}"
   ```

6. **Set Variables**:
   ```yaml
   - set:
       expression: counter = 0
   - print:
       message: "Counter: ${counter}"
   ```

### Built-in Variables

| Variable | Description | Available |
|----------|-------------|-----------|
| `${_output}` | Last command output | After any `send` command |
| `${_timestamp}` | Current timestamp at substitution time (yyyy-MM-dd HH:mm:ss) | Always |
| `${_iteration}` | Current iteration count (0-based) | Inside `while` loops |
| `${item_index}` | Current item index (0-based) | Inside `foreach` loops |
| `${Host_IP}` | Current host IP address | Always (from grid) |
| `${port}` | SSH port for current host | Always (from grid, default 22) |

**Note:** Any column in the host grid becomes available as a variable. For example, if you have a column named `location`, you can use `${location}` in your scripts.

### Variable Substitution Syntax

Variables are substituted using `${variable_name}`:

```yaml
- print:
    message: "Host ${Host_IP} has IP ${ip_address}"
- send:
    command: show interface ${interface_name}
- set:
    expression: message = "Status: ${status}"
```

### Quoting and Escaping

Both single quotes (`'...'`) and double quotes (`"..."`) are valid string delimiters in script expressions. They usually evaluate to the same final string, but YAML escaping rules differ:

- Double quotes process escape sequences like `\n`, `\t`, `\\`, and `\"`
- Single quotes treat backslashes literally; to include a single quote, use `''`

**Examples:**

```yaml
# 1) Newline escape (\n)
- set:
    expression: msg1 = "Line1\nLine2"   # newline between Line1 and Line2
- set:
    expression: msg2 = 'Line1\nLine2'   # literal backslash + n

# 2) Regex with backslashes
- if:
    condition: output matches '^\d+\s+(\w+)$'
- if:
    condition: output matches "^\\d+\\s+(\\w+)$"

# 3) Apostrophes
- set:
    expression: owner1 = "Bob's router"
- set:
    expression: owner2 = 'Bob''s router'
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
- print:
    message: "First: ${numbers[0]}"
- print:
    message: "Second: ${numbers[1]}"

# Get list length
- print:
    message: "Total count: ${numbers.length}"

# Use length in conditions
- if:
    condition: numbers.length > 0
    then:
      - print:
          message: "Found ${numbers.length} items"

# Dynamic index access
- set:
    expression: i = 0
- print:
    message: "Item at index ${i}: ${numbers[i]}"
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
3. **Active Environment Variables** - Fallback values from the selected environment profile
4. **Script `vars:` Section** - Default values (only set if not already defined)

This means:
- A column named `timeout` in your CSV will override a `timeout` defined in `vars:`
- A value from `set`, `extract`, `capture`, or `updateenvironment` will override the earlier runtime value
- Environment variables fill missing host values before `vars:` defaults are applied
- You can use `vars:` to provide fallback defaults when columns don't exist

**Example:**
```yaml
---
vars:
  timeout: 30        # Default timeout
  interface: "eth0"  # Default interface

steps:
  # If CSV has a 'timeout' column with value 60, ${timeout} will be 60, not 30
  - print:
      message: "Using timeout: ${timeout}"
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
- if:
    condition: (status == "up" or status == "active") and count > 0
    then:
      - print:
          message: "System is operational"
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
- if:
    condition: some_variable
    then:
      - print:
          message: "Variable has a value"

# Check if variable has content
- if:
    condition: output
    then:
      - print:
          message: "Got output: ${output}"
    else:
      - print:
          message: "No output received"
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
- if:
    condition: count > 10
- if:
    condition: version >= 2.0
- if:
    condition: result == 0

# These are string comparisons (because "active" isn't a number)
- if:
    condition: status == "active"
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
- if:
    condition: output matches '/error|warning/'
- if:
    condition: output matches "error|warning"
- if:
    condition: output matches 'error|warning'

# Using regex special characters
- if:
    condition: version matches '^\d+\.\d+\.\d+$'
- if:
    condition: line matches '^interface\s+\S+'
```

---

## Error Handling

### Command-Level Error Handling

```yaml
# Stop on error (default)
- send:
    command: critical_command
    on_error: stop

# Continue on error
- send:
    command: optional_command
    on_error: continue
    capture: result

# Map-style steps support both forms
- http:
    url: "https://api.example.com/status"
    on_error: continue

- http:
    url: "https://api.example.com/status"
    on_error: continue
```

For map-style steps (`http`, `ping`, `dns`, `portcheck`, `sftp`, `webhook`), nested `on_error` is an alias for step-level `on_error`. If both are present, the step-level value is used.

### Checking for Errors

```yaml
- send:
    command: some_command
    capture: output
    on_error: continue

- if:
    condition: output contains "error" or output is empty
    then:
      - exit:
          message: failure "Command failed"
```

### Retry Pattern

```yaml
- set:
    expression: retry = 0
- set:
    expression: success = ""

- while:
    condition: retry < 3 and success is empty
    do:
      - send:
          command: unreliable_command
          capture: result
          on_error: continue

      - if:
          condition: result contains "OK"
          then:
            - set:
                expression: success = "yes"
          else:
            - set:
                expression: retry = retry + 1
            - wait:
                seconds: 5

- if:
    condition: success is empty
    then:
      - exit:
          message: failure "Command failed after 3 retries"
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
  - send:
      command: show version
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
  - send:
      command: show version
      capture: output
  - extract:
      from: output
      pattern: 'Version (.+?)$'
      into: version
  - set:
      expression: msg = "Found version: ${version}"
  - print:
      message: "${msg}"
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
- set:
    expression: data = json("key1", value1, "key2", value2, ...)
```

**2. Using dot notation (nested assignment):**
```yaml
- set:
    expression: data.key1 = value1
- set:
    expression: data.nested.key2 = value2
```

### Building JSON Arrays

Use `push()` to build a list, then convert with `json()`:

```yaml
- set:
    expression: items = push(items, "first")
- set:
    expression: items = push(items, "second")
- set:
    expression: arr = json(items)
```

Or create inline arrays:
```yaml
- set:
    expression: arr = json([], "item1", "item2", "item3")
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

**Collecting Data from Multiple Hosts (JSON Lines):**
```yaml
---
name: Collect Host Inventory as JSON
steps:
  # Gather data
  - send:
      command: show version
      capture: version_output
  - extract:
      from: version_output
      pattern: 'Version (\S+)'
      into: version

  # Build this host's record
  - set:
      expression: record = json("ip", ${Host_IP}, "version", ${version}, "timestamp", ${_timestamp})

  # Append as JSON Lines (one object per line)
  - writefile:
      path: "C:\\output\\inventory.jsonl"
      format: jsonl
      content: "${record}"
      mode: append
```

**Tip:** If you need a single JSON array, create the file with `[]` before running the script across hosts, then use `format: json` with `mode: append`.

**Building Nested Reports:**
```yaml
---
name: Generate Nested JSON Report
steps:
  - send:
      command: show version
      capture: version_out
  - extract:
      from: version_out
      pattern: 'Version (\S+)'
      into: sw_version

  - send:
      command: show ip interface brief
      capture: interfaces

  # Build nested structure using dot notation
  - set:
      expression: report.host.ip = ${Host_IP}
  - set:
      expression: report.host.port = ${port}
  - set:
      expression: report.software.version = ${sw_version}
  - set:
      expression: report.metadata.scanned_at = ${_timestamp}
  - set:
      expression: report.metadata.scanned_by = "SSH Helper"

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
  - set:
      expression: event.type = "connection"
  - set:
      expression: event.host = ${Host_IP}
  - set:
      expression: event.timestamp = ${_timestamp}
  - set:
      expression: event.status = "started"

  - writefile:
      path: "C:\\logs\\events.jsonl"
      format: jsonl
      content: "${event}"
      mode: append

  # ... do work ...

  - set:
      expression: event.status = "completed"
  - set:
      expression: event.result = "success"

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
  - set:
      expression: overrides = json("host", ${Host_IP}, "timeout", 60)

  # Merge defaults with overrides (supports multiple objects)
  - set:
      expression: config = json.merge(${defaults}, overrides)

  # config now has: {"timeout": 60, "retries": 3, "debug": false, "host": "192.168.1.1"}
  - print:
      message: "Using config: ${config}"
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
  - if:
      condition: json.exists(response, "error")
      then:
        - set:
            expression: err = json.get(response, "error.message", "Unknown error")
        - exit:
            message: failure "API Error: ${err}"

  # Get array length
  - set:
      expression: count = json.len(response, "data.users")
  - print:
      message: "Found ${count} users"

  # Iterate over users
  - foreach:
      iterator: user in json.items(response, "data.users")
      do:
        - set:
            expression: name = json.get(user, "name")
        - set:
            expression: email = json.get(user, "email", "no email")
        - print:
            message: "${name}: ${email}"

**Modifying JSON Data:**
```yaml
---
name: Update Configuration
steps:
  # Start with base config
  - set:
      expression: config = json("server", json("host", "localhost", "port", 8080))

  # Update values
  - set:
      expression: config = json.set(config, "server.port", 443)
  - set:
      expression: config = json.set(config, "server.ssl", true)

  # Remove sensitive data before logging
  - set:
      expression: safe = json.delete(config, "server.password")
  - print:
      message: "Config: ${safe}"
```

---

## Examples

### Example 1: Device Information Collection

```yaml
---
name: Device Info Collection
description: Collects version and interface information

steps:
  - print:
      message: "=== Device: ${Host_IP} ==="

  - send:
      command: show version
      capture: version_output

  - extract:
      from: version_output
      pattern: 'Version (\S+)'
      into: version

  - print:
      message: "Software Version: ${version}"

  - send:
      command: show ip interface brief
      capture: interfaces

  - print:
      message: "Interface Status:"
  - foreach:
      iterator: line in interfaces
      when: line contains "up"
      do:
        - print:
            message: "  ${line}"

  - exit:
      message: success "Information collected"
```

### Example 2: Configuration Backup

```yaml
---
name: Config Backup
vars:
  backup_cmd: "show running-config"

steps:
  - print:
      message: "Backing up ${Host_IP}..."

  - send:
      command: terminal length 0

  - send:
      command: ${backup_cmd}
      capture: config
      timeout: 120

  - if:
      condition: config is empty
      then:
        - exit:
            message: failure "Failed to retrieve configuration"

  - extract:
      from: config
      pattern: 'hostname (\S+)'
      into: hostname

  - print:
      message: "Backup complete for ${hostname}"
  - exit:
      message: success "Configuration captured"
```

### Example 3: Interface Status Check with Retry

```yaml
---
name: Interface Check
vars:
  target_interface: "GigabitEthernet0/0"
  max_retries: 3

steps:
  - print:
      message: "Checking ${target_interface} on ${Host_IP}"

  - set:
      expression: retry = 0
  - set:
      expression: is_up = ""

  - while:
      condition: retry < max_retries and is_up is empty
      do:
        - send:
            command: show interface ${target_interface}
            capture: status

        - if:
            condition: status matches 'line protocol is up'
            then:
              - set:
                  expression: is_up = "yes"
              - print:
                  message: "Interface is UP"
            else:
              - print:
                  message: "Interface down, retry ${retry}..."
              - set:
                  expression: retry = retry + 1
              - wait:
                  seconds: 10

  - if:
      condition: is_up is empty
      then:
        - exit:
            message: failure "${target_interface} failed to come up"
      else:
        - exit:
            message: success "Interface verified"
```

### Example 4: Bulk IP Block (FortiGate)

```yaml
---
name: Block IP Address
description: Adds IP to firewall block list

steps:
  - if:
      condition: block is not defined or block is empty
      then:
        - exit:
            message: error "No IP address specified in 'block' column"

  - print:
      message: "Blocking ${block} on ${Host_IP}"

  - send:
      command: config firewall address
  - send:
      command: edit "BLOCK_${block}"
  - send:
      command: set subnet ${block} 255.255.255.255
  - send:
      command: set comment "Blocked by SSH Helper"
  - send:
      command: next
  - send:
      command: end

  - send:
      command: show firewall address BLOCK_${block}
      capture: verify

  - if:
      condition: verify contains "BLOCK_${block}"
      then:
        - exit:
            message: success "IP ${block} blocked successfully"
      else:
        - exit:
            message: failure "Failed to verify block for ${block}"
```

### Example 5: Multi-Vendor Support

```yaml
---
name: Get Version (Multi-Vendor)
vars:
  vendor: "cisco"

steps:
  - if:
      condition: vendor == "cisco"
      then:
        - send:
            command: show version
            capture: output
        - extract:
            from: output
            pattern: 'Version (\S+)'
            into: version

  - if:
      condition: vendor == "juniper"
      then:
        - send:
            command: show version
            capture: output
        - extract:
            from: output
            pattern: 'Junos: (\S+)'
            into: version

  - if:
      condition: vendor == "fortigate"
      then:
        - send:
            command: get system status
            capture: output
        - extract:
            from: output
            pattern: 'Version: (.+?)$'
            into: version

  - if:
      condition: version is defined
      then:
        - print:
            message: "Version: ${version}"
      else:
        - print:
            message: "Could not determine version"
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

  - if:
      condition: blocked_ips is empty
      then:
        - exit:
            message: failure "No IPs found in file"

  - print:
      message: "Found ${blocked_ips.length} IPs to block"

  # Process each IP
  - foreach:
      iterator: ip in blocked_ips
      do:
        - print:
            message: "Blocking: ${ip}"
        - send:
            command: config firewall address
        - send:
            command: edit "BLOCK_${ip}"
        - send:
            command: set subnet ${ip} 255.255.255.255
        - send:
            command: next
        - send:
            command: end

        # Log result
        - writefile:
            path: "C:\\logs\\blocked_ips.log"
            content: "${_timestamp} - Blocked ${ip} on ${Host_IP}"

  - exit:
      message: success "Blocked ${blocked_ips.length} IPs"
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

  - if:
      condition: confirm != "yes"
      then:
        - exit:
            message: "Configuration cancelled by user"

  # Apply configuration
  - print:
      message: "Configuring ${interface}..."
  - send:
      command: configure terminal
  - send:
      command: interface ${interface}
  - send:
      command: ip address ${ip_address} ${subnet_mask}
  - send:
      command: no shutdown
  - send:
      command: end

  - print:
      message: "Interface ${interface} configured with ${ip_address}"
```

### Example 8: Extract and Store Data to Host Table

```yaml
---
name: Extract Device Info to Columns
description: Extracts device information and stores it back to the host table

steps:
  - print:
      message: "Collecting info from ${Host_IP}..."

  # Get version info
  - send:
      command: show version
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
  - send:
      command: show running-config | include hostname
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

  - print:
      message: "Updated columns for ${Host_IP}: version=${version}, hostname=${hostname}"
  - exit:
      message: success "Device info collected and stored"
```

---

### Example 9: Process List with Array Properties

```yaml
---
name: Interface Inventory
description: Collects all interfaces and stores count in host table

steps:
  - send:
      command: show ip interface brief
      capture: output

  # Extract all interface names
  - extract:
      from: output
      pattern: '^(\S+)\s+\d'
      into: interfaces
      match: all

  # Check if we found any interfaces
  - if:
      condition: interfaces.length == 0
      then:
        - exit:
            message: failure "No interfaces found"

  - print:
      message: "Found ${interfaces.length} interfaces"

  # Store count back to host table
  - updatecolumn:
      column: interface_count
      value: ${interfaces.length}

  # Process each interface
  - set:
      expression: up_count = 0
  - foreach:
      iterator: iface in interfaces
      do:
        - send:
            command: show interface ${iface} | include line protocol
            capture: status
        - if:
            condition: status contains "up"
            then:
              - set:
                  expression: up_count = up_count + 1
              - print:
                  message: "${iface}: UP"

  - print:
      message: "${up_count} of ${interfaces.length} interfaces are up"

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
  - send:
      command: show version
      capture: version_output

  - extract:
      from: version_output
      pattern: 'Version (\S+)'
      into: sw_version

  - extract:
      from: version_output
      pattern: 'uptime is (.+?)$'
      into: uptime

  - send:
      command: show running-config | include hostname
      capture: hostname_output

  - extract:
      from: hostname_output
      pattern: 'hostname (\S+)'
      into: hostname

  # Build nested JSON structure using dot notation
  - set:
      expression: device.identity.hostname = ${hostname}
  - set:
      expression: device.identity.ip = ${Host_IP}
  - set:
      expression: device.identity.port = ${port}

  - set:
      expression: device.software.version = ${sw_version}
  - set:
      expression: device.software.uptime = ${uptime}

  - set:
      expression: device.metadata.scanned_at = ${_timestamp}
  - set:
      expression: device.metadata.scanned_by = "SSH Helper"

  # Write individual device JSON file
  - writefile:
      path: "C:\\inventory\\devices\\${Host_IP}.json"
      format: json
      content: "${device}"
      pretty: true

  # Also append to master inventory (JSON array)
  - set:
      expression: summary = json("ip", ${Host_IP}, "hostname", ${hostname}, "version", ${sw_version})
  - writefile:
      path: "C:\\inventory\\all_devices.json"
      format: json
      content: "[${summary}]"
      mode: append

  # Log the event
  - set:
      expression: log_entry = json("timestamp", ${_timestamp}, "host", ${Host_IP}, "action", "inventory_collected")
  - writefile:
      path: "C:\\inventory\\audit.jsonl"
      format: jsonl
      content: "${log_entry}"
      mode: append

  - print:
      message: "Exported ${hostname} (${Host_IP}) to JSON"
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
  - set:
      expression: checks_passed = 0
  - set:
      expression: checks_failed = 0

  # Check 1: Software version
  - send:
      command: show version
      capture: version_output
  - extract:
      from: version_output
      pattern: 'Version (\d+\.\d+)'
      into: current_version

  - set:
      expression: result.checks.version.current = ${current_version}
  - set:
      expression: result.checks.version.required = ${required_version}

  - if:
      condition: current_version >= required_version
      then:
        - set:
            expression: result.checks.version.status = "PASS"
        - set:
            expression: checks_passed = checks_passed + 1
      else:
        - set:
            expression: result.checks.version.status = "FAIL"
        - set:
            expression: checks_failed = checks_failed + 1

  # Check 2: SSH enabled
  - send:
      command: show ip ssh
      capture: ssh_output
      on_error: continue

  - if:
      condition: ssh_output contains "SSH Enabled"
      then:
        - set:
            expression: result.checks.ssh.status = "PASS"
        - set:
            expression: result.checks.ssh.detail = "SSH is enabled"
        - set:
            expression: checks_passed = checks_passed + 1
      else:
        - set:
            expression: result.checks.ssh.status = "FAIL"
        - set:
            expression: result.checks.ssh.detail = "SSH not enabled or not available"
        - set:
            expression: checks_failed = checks_failed + 1

  # Build final report
  - set:
      expression: result.host = ${Host_IP}
  - set:
      expression: result.timestamp = ${_timestamp}
  - set:
      expression: result.summary.passed = ${checks_passed}
  - set:
      expression: result.summary.failed = ${checks_failed}

  - if:
      condition: checks_failed == 0
      then:
        - set:
            expression: result.summary.overall = "COMPLIANT"
      else:
        - set:
            expression: result.summary.overall = "NON-COMPLIANT"

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

  - print:
      message: "${Host_IP}: ${result.summary.overall} (${checks_passed} passed, ${checks_failed} failed)"
```

---

### Example 12: FortiGate Configuration Parsing

```yaml
---
name: FortiGate Interface Report
description: Parses FortiGate config and exports interface inventory to CSV

steps:
  - print:
      message: "Collecting interface configuration from ${Host_IP}..."

  # Capture the full interface configuration
  - send:
      command: show full-configuration system interface
      capture: raw_config
      suppress: true
      timeout: 120

  # Parse the FortiGate configuration
  - parse:
      format: fortigate
      from: raw_config
      into: config

  # Get hostname for report
  - send:
      command: get system status
      capture: status_output
      suppress: true

  - extract:
      from: status_output
      pattern: 'Hostname: (\S+)'
      into: hostname

  # Get list of all interfaces
  - set:
      expression: interfaces = json.keys(config, "system.interface")

  - if:
      condition: interfaces.length == 0
      then:
        - exit:
            message: failure "No interfaces found in configuration"

  - print:
      message: "Found ${interfaces.length} interfaces on ${hostname}"

  # Build report data as JSON array
  - set:
      expression: report = json([])

  - foreach:
      iterator: iface in interfaces
      do:
        # Extract interface properties with defaults
        - set:
            expression: ip = json.get(config, "system.interface.${iface}.ip", "N/A")
        - set:
            expression: vdom = json.get(config, "system.interface.${iface}.vdom", "root")
        - set:
            expression: type = json.get(config, "system.interface.${iface}.type", "unknown")
        - set:
            expression: status = json.get(config, "system.interface.${iface}.status", "up")
        - set:
            expression: allowaccess = json.get(config, "system.interface.${iface}.allowaccess", "")

        # Create row object
        - set:
            expression: row = json("hostname", "${hostname}", "interface", "${iface}", "ip", "${ip}", "vdom", "${vdom}", "type", "${type}", "status", "${status}", "access", "${allowaccess}")
        - set:
            expression: report = json.push(report, ${row})

        # Log each interface
        - log:
            message: "  ${iface}: ${ip} (${type})"
            level: debug

  # Write CSV report
  - writefile:
      path: "C:\\reports\\${hostname}_interfaces.csv"
      format: csv
      headers: [hostname, interface, ip, vdom, type, status, access]
      content: "${report}"

  # Also write JSON for programmatic access
  - writefile:
      path: "C:\\reports\\${hostname}_interfaces.json"
      format: json
      content: "${report}"
      pretty: true

  # Update host table with summary
  - updatecolumn:
      column: interface_count
      value: ${interfaces.length}

  - updatecolumn:
      column: last_scanned
      value: ${_timestamp}

  - print:
      message: "Exported ${interfaces.length} interfaces to CSV and JSON"
  - exit:
      message: success "Interface report generated for ${hostname}"
```

---

### Example 13: FortiGate Firewall Policy Audit

```yaml
---
name: Firewall Policy Audit
description: Parses FortiGate firewall policies and identifies potential issues

steps:
  - print:
      message: "Auditing firewall policies on ${Host_IP}..."

  # Capture firewall policy configuration
  - send:
      command: show full-configuration firewall policy
      capture: raw_config
      suppress: true
      timeout: 180

  - parse:
      format: fortigate
      from: raw_config
      into: config

  # Get all policy IDs
  - set:
      expression: policies = json.keys(config, "firewall.policy")

  - if:
      condition: policies.length == 0
      then:
        - log:
            message: "No firewall policies found"
            level: warning
        - exit:
            message: success "No policies to audit"

  - print:
      message: "Analyzing ${policies.length} firewall policies..."

  # Initialize counters
  - set:
      expression: any_any_count = 0
  - set:
      expression: disabled_count = 0
  - set:
      expression: no_logging_count = 0
  - set:
      expression: issues = json([])

  # Analyze each policy
  - foreach:
      iterator: pid in policies
      do:
        - set:
            expression: policy_path = "firewall.policy.${pid}"

        # Get policy attributes
        - set:
            expression: srcaddr = json.get(config, "${policy_path}.srcaddr", "")
        - set:
            expression: dstaddr = json.get(config, "${policy_path}.dstaddr", "")
        - set:
            expression: service = json.get(config, "${policy_path}.service", "")
        - set:
            expression: action = json.get(config, "${policy_path}.action", "deny")
        - set:
            expression: status = json.get(config, "${policy_path}.status", "enable")
        - set:
            expression: logtraffic = json.get(config, "${policy_path}.logtraffic", "")
        - set:
            expression: name = json.get(config, "${policy_path}.name", "Policy ${pid}")

        # Check for any-any rules
        - if:
            condition: srcaddr contains "all" and dstaddr contains "all" and action == "accept"
            then:
              - set:
                  expression: any_any_count = any_any_count + 1
              - set:
                  expression: issue = json("policy", "${pid}", "name", "${name}", "issue", "any-any-accept", "severity", "high")
              - set:
                  expression: issues = json.push(issues, ${issue})
              - log:
                  message: "Policy ${pid} (${name}): ANY-ANY ACCEPT rule detected"
                  level: warning

        # Check for disabled rules
        - if:
            condition: status == "disable"
            then:
              - set:
                  expression: disabled_count = disabled_count + 1

        # Check for no logging
        - if:
            condition: logtraffic is empty or logtraffic == "disable"
            then:
              - set:
                  expression: no_logging_count = no_logging_count + 1

  # Build audit report
  - set:
      expression: audit.host = ${Host_IP}
  - set:
      expression: audit.timestamp = ${_timestamp}
  - set:
      expression: audit.summary.total_policies = ${policies.length}
  - set:
      expression: audit.summary.any_any_rules = ${any_any_count}
  - set:
      expression: audit.summary.disabled_rules = ${disabled_count}
  - set:
      expression: audit.summary.no_logging = ${no_logging_count}
  - set:
      expression: audit.issues = ${issues}

  # Determine overall status
  - if:
      condition: any_any_count > 0
      then:
        - set:
            expression: audit.status = "NEEDS_REVIEW"
      else:
        - set:
            expression: audit.status = "OK"

  # Write audit report
  - writefile:
      path: "C:\\audits\\${Host_IP}_policy_audit.json"
      format: json
      content: "${audit}"
      pretty: true

  # Update host table
  - updatecolumn:
      column: policy_status
      value: ${audit.status}

  - updatecolumn:
      column: policy_count
      value: ${policies.length}

  # Summary output
  - print:
      message: "Audit complete: ${policies.length} policies analyzed"
  - print:
      message: "  - Any-Any Accept rules: ${any_any_count}"
  - print:
      message: "  - Disabled rules: ${disabled_count}"
  - print:
      message: "  - Rules without logging: ${no_logging_count}"

  - if:
      condition: any_any_count > 0
      then:
        - log:
            message: "Review required: ${any_any_count} any-any rules found"
            level: warning

  - exit:
      message: success "Policy audit complete"
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

### Working with Configuration Parsing
18. **Use `parse` for structured device configs**: Parse FortiGate configs instead of regex for reliable data extraction
19. **Filter sections for large configs**: Use the `sections` parameter to only parse what you need
20. **Access parsed data with `json.get()`**: Use default values to handle missing keys gracefully
21. **Iterate with `json.keys()`**: Get all interface names, policy IDs, etc. for processing in loops
