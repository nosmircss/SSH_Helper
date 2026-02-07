# M2: Multi-Protocol Unified Step Model

**Status**: NOT STARTED

**Why**: The app currently does SSH + basic webhooks. Making HTTP, ping, DNS, port checks, and SFTP all first-class workflow steps transforms it from "SSH tool" into "network operations workbench."

---

## Progress Checklist

- [ ] Add `StepType` enum entries for Http, Ping, Dns, Portcheck, Sftp
- [ ] Create options classes in `ScriptStep.cs` (HttpOptions, PingOptions, DnsOptions, PortcheckOptions, SftpOptions)
- [ ] Add properties and `GetStepType()` cases to `ScriptStep`
- [ ] Implement `HttpCommand.cs` (full HTTP client)
- [ ] Implement `PingCommand.cs` (ICMP ping)
- [ ] Implement `DnsCommand.cs` (DNS lookup)
- [ ] Implement `PortcheckCommand.cs` (TCP port check)
- [ ] Implement `SftpCommand.cs` (SFTP upload/download via Rebex)
- [ ] Update `ScriptParser.cs` — add to `KnownStepKeys`, parsing methods, validation
- [ ] Register all 5 commands in `ScriptExecutor.cs` constructor
- [ ] Verify existing `webhook` command still works unchanged
- [ ] Write tests for each new command
- [ ] Manual smoke test: mixed-protocol workflow (SSH + HTTP + ping + portcheck)

---

## New StepType Entries

Add to `StepType` enum in `Models/ScriptStep.cs`:

```csharp
Http,       // Full HTTP client (supersedes webhook)
Ping,       // ICMP ping
Dns,        // DNS lookup
Portcheck,  // TCP port check
Sftp,       // SFTP file transfer
```

---

## HTTP Command (`HttpCommand.cs`)

The most important new command. Full HTTP client replacing/superseding the basic `webhook`.

### Options Class: `HttpOptions`

```csharp
public class HttpOptions
{
    public string Url { get; set; } = string.Empty;          // Supports ${var} substitution
    public string Method { get; set; } = "GET";              // GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS
    public string? Body { get; set; }                         // Request body (string or variable ref)
    public Dictionary<string, string>? Headers { get; set; }  // Custom headers
    public string? Auth { get; set; }                         // "none", "basic", "bearer"
    public string? Username { get; set; }                     // For basic auth
    public string? Password { get; set; }                     // For basic auth
    public string? Token { get; set; }                        // For bearer auth
    public string? Into { get; set; }                         // Capture: body→${into}, status→${into}_status, headers→${into}_headers
    public int Timeout { get; set; } = 30;                    // Seconds
    public bool FollowRedirects { get; set; } = true;
    public bool AllowFailure { get; set; } = false;           // Don't fail on non-2xx
    public string? ContentType { get; set; }                  // Shorthand: "json", "form", "text", "xml"
}
```

### YAML Syntax

```yaml
# Simple GET
- http:
    url: "https://api.example.com/health"
    into: response

# POST with JSON body and basic auth
- http:
    url: "https://api.example.com/devices/${Host_IP}/config"
    method: POST
    content_type: json
    body: '{"hostname": "${hostname}", "version": "${firmware_version}"}'
    auth: basic
    username: "${api_user}"
    password: "${api_pass}"
    into: api_result
    timeout: 60

# Bearer token auth
- http:
    url: "https://forticloud.fortinet.com/api/v1/devices"
    auth: bearer
    token: "${api_token}"
    headers:
      Accept: "application/json"
    into: devices

# Check response status
- if: "${api_result_status} != 200"
  then:
    - log:
        message: "API call failed with status ${api_result_status}"
        level: error
```

### Variable Capture

When `into: result` is specified:
- `${result}` = response body
- `${result_status}` = HTTP status code (int as string)
- `${result_headers}` = response headers as JSON string

### `http` vs `webhook` Comparison

| Feature | `webhook` | `http` |
|---------|-----------|--------|
| Default method | POST | GET |
| Auth support | Manual via headers | `auth: basic/bearer` |
| Follow redirects | HttpClient default | Configurable |
| Allow non-2xx | No (fails) | `allow_failure: true` |
| Content-Type shorthand | No | `content_type: json/form/text/xml` |
| Response headers | No | `${into}_headers` |

**Backward compatibility**: `webhook` stays exactly as-is. Docs mark it deprecated in favor of `http`.

### Implementation

Uses `System.Net.Http.HttpClient` (already used by `WebhookCommand`). Static shared instance with configurable handler.

---

## Ping Command (`PingCommand.cs`)

### Options Class: `PingOptions`

```csharp
public class PingOptions
{
    public string Host { get; set; } = string.Empty;   // Target host/IP
    public int Count { get; set; } = 4;                 // Number of pings
    public int Timeout { get; set; } = 3000;             // Per-ping timeout (ms)
    public string? Into { get; set; }                     // "success"/"failure", {into}_avg, {into}_loss
}
```

### YAML Syntax

```yaml
# Simple form (string)
- ping: "192.168.1.1"

# Detailed form
- ping:
    host: "${Host_IP}"
    count: 10
    timeout: 2000
    into: ping_result

- if: "${ping_result} == success"
  then:
    - print: "Host reachable, avg RTT: ${ping_result_avg}ms, loss: ${ping_result_loss}%"
```

### Implementation

Uses `System.Net.NetworkInformation.Ping` (built-in .NET). No extra dependencies.

---

## DNS Command (`DnsCommand.cs`)

### Options Class: `DnsOptions`

```csharp
public class DnsOptions
{
    public string Host { get; set; } = string.Empty;   // Hostname to resolve
    public string Type { get; set; } = "A";             // A, AAAA, PTR (more later with DnsClient)
    public string? Into { get; set; }                     // List of addresses, {into}_count
    public int Timeout { get; set; } = 10;               // Seconds
}
```

### YAML Syntax

```yaml
- dns:
    host: "fw01.example.com"
    type: A
    into: resolved_ips

- print: "Resolved to: ${resolved_ips[0]} (${resolved_ips_count} addresses)"
```

### Implementation

Uses `System.Net.Dns.GetHostAddressesAsync` for A/AAAA, `GetHostEntryAsync` for PTR. Initially supports A/AAAA/PTR only. MX/TXT/NS/CNAME can be added later via optional `DnsClient` NuGet package.

---

## Portcheck Command (`PortcheckCommand.cs`)

### Options Class: `PortcheckOptions`

```csharp
public class PortcheckOptions
{
    public string Host { get; set; } = string.Empty;   // Target host/IP
    public int Port { get; set; } = 22;                 // TCP port
    public int Timeout { get; set; } = 5;               // Seconds
    public string? Into { get; set; }                     // "open"/"closed"/"timeout", {into}_latency (ms)
}
```

### YAML Syntax

```yaml
- portcheck:
    host: "${Host_IP}"
    port: 443
    timeout: 3
    into: https_check

- if: "${https_check} == open"
  then:
    - print: "HTTPS accessible (${https_check_latency}ms)"
```

### Implementation

Uses `System.Net.Sockets.TcpClient.ConnectAsync` with timeout. No extra dependencies.

---

## SFTP Command (`SftpCommand.cs`)

### Options Class: `SftpOptions`

```csharp
public class SftpOptions
{
    public string Action { get; set; } = string.Empty;    // "upload" or "download"
    public string LocalPath { get; set; } = string.Empty;  // Local file path
    public string RemotePath { get; set; } = string.Empty; // Remote file path
    public string? Host { get; set; }                       // Override (default: current ${Host_IP})
    public int? Port { get; set; }                          // Override (default: current port or 22)
    public string? Username { get; set; }                   // Override
    public string? Password { get; set; }                   // Override
    public bool Overwrite { get; set; } = true;
    public string? Into { get; set; }                       // "success"/"failure", {into}_bytes
    public int Timeout { get; set; } = 120;                 // Seconds
}
```

### YAML Syntax

```yaml
# Download config backup
- sftp:
    action: download
    remote_path: "/etc/config/running.cfg"
    local_path: "C:\\backups\\${Host_IP}_config.cfg"
    into: transfer

# Upload firmware
- sftp:
    action: upload
    local_path: "C:\\firmware\\v7.2.img"
    remote_path: "/tmp/firmware.img"
    into: upload_result

- if: "${upload_result} == success"
  then:
    - print: "Uploaded ${upload_result_bytes} bytes"
```

### Implementation

Uses Rebex SFTP (check if `Rebex.SshShell` package includes `Rebex.Net.Sftp`; if not, add `Rebex.Sftp` package). Reuses host credentials from script context.

---

## Mixed Workflow Example

```yaml
---
name: Full Infrastructure Health Check
description: SSH + HTTP + Ping + DNS + SFTP in one workflow
vars:
  api_base: "https://cmdb.example.com/api/v1"
  api_token: ""

steps:
  # 1. Pre-flight: check if host is reachable
  - ping:
      host: "${Host_IP}"
      count: 3
      into: reachability

  - if: "${reachability} != success"
    then:
      - updatecolumn:
          column: status
          value: "UNREACHABLE"
      - exit: "failure Host unreachable"

  # 2. Check HTTPS management port
  - portcheck:
      host: "${Host_IP}"
      port: 443
      timeout: 3
      into: https_status

  # 3. SSH in and grab info
  - send: "get system status"
    capture: status_output

  - extract:
      from: status_output
      pattern: "Version:\\s+(\\S+)"
      into: firmware_version

  # 4. Report to CMDB API
  - http:
      url: "${api_base}/devices/${Host_IP}"
      method: PUT
      auth: bearer
      token: "${api_token}"
      content_type: json
      body: '{"firmware": "${firmware_version}", "https_open": "${https_status}"}'
      into: api_response
      on_error: continue

  # 5. DNS verification
  - dns:
      host: "${hostname}.example.com"
      type: A
      into: dns_result

  # 6. Download logs via SFTP
  - sftp:
      action: download
      remote_path: "/var/log/messages"
      local_path: "C:\\audit\\${Host_IP}_messages.log"
      into: sftp_result
      on_error: continue

  - updatecolumn:
      column: status
      value: "OK - ${firmware_version}"
```

---

## ScriptParser Changes

In `Services/Scripting/ScriptParser.cs`:

1. Add to `KnownStepKeys`: `"http"`, `"ping"`, `"dns"`, `"portcheck"`, `"sftp"`
2. Add parsing cases in `ParseStep` switch for each new type
3. Add validation rules:
   - HTTP requires `url`
   - Ping requires `host` (or is a string)
   - DNS requires `host`
   - Portcheck requires `host` and `port`
   - SFTP requires `action`, `local_path`, `remote_path`

## ScriptExecutor Registration

In `Services/Scripting/ScriptExecutor.cs` constructor:

```csharp
{ StepType.Http, new HttpCommand() },
{ StepType.Ping, new PingCommand() },
{ StepType.Dns, new DnsCommand() },
{ StepType.Portcheck, new PortcheckCommand() },
{ StepType.Sftp, new SftpCommand() },
```

---

## Dependencies

**No new NuGet packages required** for initial implementation:
- HTTP: `System.Net.Http.HttpClient` (built-in)
- Ping: `System.Net.NetworkInformation.Ping` (built-in)
- DNS: `System.Net.Dns` (built-in)
- Portcheck: `System.Net.Sockets.TcpClient` (built-in)
- SFTP: Rebex (already referenced)

Optional future: `DnsClient` NuGet for MX/TXT/NS/CNAME support.

---

## Key Files

| File | Action |
|------|--------|
| `Services/Scripting/Commands/HttpCommand.cs` | CREATE |
| `Services/Scripting/Commands/PingCommand.cs` | CREATE |
| `Services/Scripting/Commands/DnsCommand.cs` | CREATE |
| `Services/Scripting/Commands/PortcheckCommand.cs` | CREATE |
| `Services/Scripting/Commands/SftpCommand.cs` | CREATE |
| `Services/Scripting/Models/ScriptStep.cs` | MODIFY — add StepType entries, options classes, properties |
| `Services/Scripting/ScriptParser.cs` | MODIFY — parsing, KnownStepKeys, validation |
| `Services/Scripting/ScriptExecutor.cs` | MODIFY — register 5 new commands |
