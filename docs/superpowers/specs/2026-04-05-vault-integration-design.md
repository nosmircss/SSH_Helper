# HashiCorp Vault Integration Design

**Date:** 2026-04-05
**Status:** Draft
**Feature:** HashiCorp Vault secret management with named profiles

---

## Summary

Add HashiCorp Vault as an additive secret source alongside the existing Windows Credential Manager. Vault is accessed on demand through three surfaces: a `vault:` script command (read, write, and patch), `{{vault:...}}` inline variable syntax, and an `ICredentialProvider` backend for host/job credential resolution. Multiple named vault profiles support different AppRole identities against the same or different Vault clusters. Scripts can perform full credential rotation workflows — generate, push to devices, store in Vault, and verify.

## Goals

- Team credential sharing via centralized Vault instead of per-machine Windows Credential Manager
- Automatic secret rotation — scripts always pull the current value
- Zero impact on existing credential flows when vault is not configured or not referenced

## Non-Goals

- Vault policy management or administration UI
- Replacing Windows Credential Manager — vault is additive, not a mode switch
- Dynamic secrets (database engine, AWS engine) — fundamentally different API pattern, future work
- Vault SSH certificate signing — changes the auth model entirely, future work
- Vault path browsing/discovery UI — nice-to-have, not essential for v1

---

## Architecture

### Core Service

**`VaultService`** — stateless-style HTTP client wrapping the Vault KV API. Supports both KV v1 and KV v2 engines. One logical instance manages all configured profiles.

- No third-party Vault SDK. Uses `HttpClient` against the Vault HTTP API directly (.NET 8 built-in). Avoids transitive dependencies.
- Each profile gets its own lazy authentication and cached token.
- Injected into scripting context, job execution, and credential resolution.

### Vault Profiles

Named configurations allowing different auth identities:

```
Profile "network":  vault.company.com:8200 / AppRole / net-role-id
Profile "servers":  vault.company.com:8200 / AppRole / srv-role-id
Profile "default":  vault.company.com:8200 / Token
```

One profile is marked as default — used when no profile is specified in script references.

### Authentication Methods

| Method | Fields | Use Case |
|--------|--------|----------|
| Token | Token | Dev/personal use |
| AppRole | Role ID + Secret ID | Service accounts, team use |
| LDAP | Username + Password | Org directory auth |

Sensitive auth values (tokens, secret IDs, LDAP passwords) are stored in Windows Credential Manager under dedicated targets (`SSH_Helper:vault:{profileName}:{authType}`), not in `config.json`.

### KV Engine Version Support

The profile config includes a `KvVersion` setting (auto-detect, v1, or v2). On first access, if set to auto-detect, `VaultService` reads the mount's tune info (`GET /v1/sys/mounts/{mount}/tune`) to determine the engine version and caches the result. If the call is forbidden (restricted policy), falls back to heuristic: attempt a KV v2 read — if it returns the `data.data` wrapper, it's v2; otherwise treat as v1.

**KV v1 vs v2 differences handled internally:**

| Operation | KV v1 | KV v2 |
|-----------|-------|-------|
| Read | `GET /v1/{mount}/{path}` | `GET /v1/{mount}/data/{path}?version={n}` |
| Write | `POST /v1/{mount}/{path}` | `POST /v1/{mount}/data/{path}` |
| Patch | Not supported (read-modify-write fallback) | `PATCH /v1/{mount}/data/{path}` |
| List | `LIST /v1/{mount}/{path}` | `LIST /v1/{mount}/metadata/{path}` |
| Versioning | Not available | Supported via `?version=` query param |

KV v1 limitations surfaced clearly: if a script uses `version:` against a v1 mount, the parser warns "version pinning requires KV v2". If `patch:` is used against v1, it silently falls back to read-modify-write.

### Vault HTTP Endpoints Used

| Endpoint | Purpose |
|----------|---------|
| `POST /v1/auth/token/lookup-self` | Validate token |
| `POST /v1/auth/approle/login` | AppRole authentication |
| `POST /v1/auth/ldap/login/{username}` | LDAP authentication |
| `GET /v1/sys/mounts/{mount}/tune` | Auto-detect KV engine version |
| `GET /v1/{mount}/data/{path}?version={n}` | Read KV v2 secret (optional version pin) |
| `GET /v1/{mount}/{path}` | Read KV v1 secret |
| `POST /v1/{mount}/data/{path}` | Write KV v2 secret (full replace, creates new version) |
| `POST /v1/{mount}/{path}` | Write KV v1 secret (full replace) |
| `PATCH /v1/{mount}/data/{path}` | Patch KV v2 secret (merge update, preserves other keys) |
| `LIST /v1/{mount}/metadata/{path}` | List secret paths under prefix (KV v2) |
| `LIST /v1/{mount}/{path}` | List secret paths under prefix (KV v1) |
| `GET /v1/sys/health` | Test connection (settings UI) |

---

## Three Access Surfaces

### Surface 1: `vault:` Script Command

Explicit step for fetching secrets into variables.

```yaml
# Single key (path is relative to the profile's mount, e.g., "secret" mount → /v1/secret/data/ssh/prod-switches)
- vault:
    profile: network           # optional, uses default if omitted
    path: "ssh/prod-switches"
    key: "password"
    into: switch_password

# Multiple keys
- vault:
    path: "ssh/prod-switches"
    keys:
      password: switch_pass
      username: switch_user
      enable_secret: enable_pass

# Pinned to a specific secret version (KV v2 versioning)
- vault:
    path: "ssh/prod-switches"
    version: 3
    key: "password"
    into: switch_password
```

**Writing secrets (full replace — overwrites entire secret, creates new version):**

```yaml
- vault:
    profile: network
    path: "ssh/prod-switches"
    write:
      password: "{{new_pass}}"
      rotated_by: "ssh_helper"
      rotated_at: "{{now()}}"
```

`write:` replaces all keys at the path. Any keys not included are removed. Uses `POST /v1/{mount}/data/{path}`.

**Patching secrets (merge — updates specified keys, preserves others):**

```yaml
- vault:
    path: "ssh/prod-switches"
    patch:
      password: "{{new_pass}}"
```

`patch:` only updates the specified keys. All other existing keys are preserved. Uses `PATCH /v1/{mount}/data/{path}` (Vault 1.9+). If the server does not support PATCH, falls back to read-modify-write (read current secret, merge keys, write full result).

**Command details:**

- New `VaultCommand` in `Services/Scripting/Commands/`.
- Modes are mutually exclusive: a step uses `key`/`keys` (read), `write` (full replace), or `patch` (merge). Parser rejects combinations.
- Supports `on_error: continue/fail` (default: fail).
- Sets `_last_error` on failure (vault sealed, path not found, auth expired).
- Write and patch operations invalidate the cache entry for that path.

### Surface 2: `{{vault:...}}` Inline Syntax

Transparent resolution anywhere variables work — `send:`, `set:`, `if:` conditions, subroutine `args:`, etc.

**Syntax:** `{{vault:[profile@]path#key}}`

```yaml
# With explicit profile
- send: "{{vault:network@ssh/switches#password}}"

# With default profile
- send: "{{vault:servers/web01#password}}"
```

Implemented in `ScriptContext.ResolveVariableExpression()` with a `vault:` prefix check. The `#` delimiter separates path from key. The `@` delimiter separates profile from path.

### Surface 3: Credential Backend (Additive)

Vault as an additional credential source — does not replace Windows Credential Manager.

- **Per-host:** Optional `vault_path` CSV column (e.g., `network@ssh/switches` or `servers/web01`). Path is relative to the profile's mount. Only consulted when present and non-empty.
- **Per-job:** New `CredentialMode.Vault` with a `VaultCredentialPath` property on `JobDefinition`.
- **App-level default:** Not implemented. The existing default password flow via Windows Credential Manager remains unchanged.

**Key naming convention:** The vault secret is expected to contain `username` and `password` keys by default. Custom key names can be specified with hash syntax in the `vault_path` column: `ssh/switches#user_field,pass_field`. When no keys are specified, `username` and `password` are assumed. This convention keeps the common case simple while allowing flexibility for non-standard vault secret layouts.

**Resolution priority** (most specific wins):
1. Per-host `vault_path` column (if present and non-empty)
2. Per-host `password` column (existing behavior)
3. Job-level vault path (for scheduled jobs with `CredentialMode.Vault`)
4. Toolbar username/password (existing fallback)

New `VaultCredentialProvider : ICredentialProvider` used alongside (not instead of) `CredentialManagerProvider`.

---

## Caching

In-memory cache within `VaultService`, keyed by `profile + path + key`.

- **TTL:** Configurable per-profile, default 300 seconds.
- **Scope:** Per app session. Cleared on restart.
- **Manual clear:** `vault_clear_cache()` function available in scripts for forced refresh.
- **List results not cached.** `vault_list()` always hits Vault to ensure fresh path listings.
- **No background refresh.** Cache entries simply expire and are re-fetched on next access.

---

## Token Lifecycle

Matches the stateless, on-demand pattern of the existing credential system. No background threads or renewal timers.

- **Authenticate lazily** on first vault access per profile.
- **Cache the token** with its expiry timestamp.
- **On each request**, check if token is expired. If yes, re-authenticate, then proceed.
- **On auth failure mid-request**, retry once with fresh auth. If that fails, surface the error.

---

## Error Handling

### Per-Surface Behavior

| Surface | Behavior on Failure |
|---------|---------------------|
| `vault:` command | Sets `_last_error`, respects `on_error: continue/fail` (default: fail) |
| `{{vault:path#key}}` | Resolves to empty string, sets `_last_error`. Downstream failure is traceable |
| `vault_path` column | Falls back to existing credential resolution (password column, then default). Logs warning |
| Job `CredentialMode.Vault` | Job fails with clear error in execution history |

No startup connectivity check. Vault is only contacted when something accesses it.

### Friendly Error Messages

Raw Vault HTTP errors are translated to actionable, user-facing messages:

| HTTP Status / Condition | User-Facing Message |
|-------------------------|---------------------|
| Connection refused / timeout | "Cannot reach Vault at {address} — check the address, port, and network connectivity" |
| TLS handshake failure | "TLS error connecting to {address} — if using an internal CA, configure the CA certificate path in the vault profile" |
| 401 Unauthorized | "Vault authentication failed for profile '{name}' — check your token or AppRole credentials" |
| 403 Forbidden | "Permission denied — check that your Vault policy grants '{capability}' on path '{path}'" |
| 404 Not Found (path) | "No secret found at '{path}' — verify the path exists in Vault" |
| 404 Not Found (key) | "Secret at '{path}' exists but has no key '{key}' — available keys: {list}" |
| 503 Service Unavailable | "Vault is sealed — it needs to be unsealed before SSH_Helper can access secrets" |
| Token expired mid-request | "Vault token expired — re-authenticating with profile '{name}'..." (auto-retry, only shown if retry also fails) |

**Key-not-found includes available keys:** When a specific key is missing from a secret, the error lists the keys that do exist. This catches typos (e.g., `pasword` vs `password`) and helps users discover the secret's structure without leaving SSH_Helper.

### Audit Logging

Vault operations are logged in the script execution output and execution history:

- **Logged:** profile name, path, operation (read/write/patch/list), success/failure, HTTP status on error
- **Never logged:** secret values, tokens, or any sensitive data
- **Format:** `[vault] READ network@ssh/prod-switches#password → ok` or `[vault] WRITE network@ssh/prod-switches → 403 Permission denied`

This provides a traceable audit trail for troubleshooting and compliance without exposing secret content.

---

## Secret Rotation

A primary use case: scripts that rotate credentials on devices and store the new value in Vault.

### Full Rotation Workflow

```yaml
steps:
  # 1. Generate new credential
  - set:
      expression: new_pass = random_string(24)

  # 2. Push to devices via SSH
  - send: "configure terminal"
  - send: "username admin secret {{new_pass}}"
  - send: "end"
  - send: "write memory"

  # 3. Store in Vault (patch preserves other keys like 'enable_secret')
  - vault:
      profile: network
      path: "ssh/prod-switches"
      patch:
        password: "{{new_pass}}"
        rotated_by: "ssh_helper"
        rotated_at: "{{now()}}"

  # 4. Verify the write
  - vault:
      path: "ssh/prod-switches"
      key: "password"
      into: verify_pass
  - assert:
      that: verify_pass == new_pass
      message: "Vault rotation verification failed"
```

### Mid-Execution Consistency

When running across many hosts, a cached secret could expire mid-run. If rotation happened between fetches, some hosts get the old password and others get the new one. To avoid this, pre-fetch credentials at script start:

```yaml
steps:
  # Pin credentials for the entire run
  - vault:
      path: "ssh/prod-switches"
      keys:
        username: cred_user
        password: cred_pass

  # All hosts use the same fetched values
  - send: "{{cred_user}}"
  - send: "{{cred_pass}}"
```

### Force Cache Refresh

After a known rotation, bust the cache before fetching:

```yaml
- set:
    expression: dummy = vault_clear_cache()
- vault:
    path: "ssh/prod-switches"
    key: "password"
    into: fresh_password
```

### Batch Rotation with `vault_list()`

Rotate all secrets under a prefix without hardcoding paths:

```yaml
steps:
  # List all secret paths under ssh/
  - set:
      expression: paths = vault_list("ssh/")

  - foreach:
      items: paths
      as: secret_path
      steps:
        - vault:
            path: "{{secret_path}}"
            key: "password"
            into: current_pass

        - set:
            expression: new_pass = random_string(24)

        # ... push new_pass to device ...

        - vault:
            path: "{{secret_path}}"
            patch:
              password: "{{new_pass}}"
              rotated_at: "{{now()}}"
```

`vault_list(prefix)` accepts an optional profile as a second argument: `vault_list("ssh/", "network")`. Returns a list of path strings (not full secret data).

### Scheduled Rotation Jobs

Cron jobs that rotate secrets on a schedule work naturally — each job execution starts with a fresh session (or expired cache), fetches the current secret, rotates, and writes back. No special configuration needed beyond the `vault:` steps in the script.

---

## Environment-Aware Profiles

Vault profile selection integrates with the existing Environment system (`EnvironmentService`). Each named environment (dev, staging, prod) can specify a default vault profile:

```
Environment "Development"  → vault profile: "dev"
Environment "Production"   → vault profile: "prod"
```

**Behavior:**
- `EnvironmentConfig` gets a new optional `VaultProfileName` property.
- When an environment is active and specifies a vault profile, that profile becomes the default for all vault operations (overriding `VaultSettings.DefaultProfileName`).
- Scripts that explicitly specify `profile:` always use the named profile regardless of environment.
- Switching environments via `EnvironmentService.EnvironmentChanged` also switches the active vault profile.

This means the same script works across environments without changes:

```yaml
# No profile specified — uses whatever the active environment maps to
- vault:
    path: "ssh/core-switches"
    key: "password"
    into: switch_pass
```

In dev, this hits the "dev" vault profile. In prod, it hits the "prod" vault profile. No script edits needed.

**Settings UI:** The `EnvironmentDialog` gets a new optional "Vault Profile" dropdown per environment, populated from the configured vault profiles.

---

## Script Validation

The parser validates vault references at parse time (before execution):

- **`vault:` command:** Validates required fields (`path` present; one of `key`/`keys`/`write`/`patch` present; `into` present for single-key read). Rejects mutually exclusive mode combinations (e.g., `key` + `write`). Warns if `profile` references a name not in config.
- **`{{vault:...}}` syntax:** Validates structure matches `[profile@]path#key` pattern. Missing `#key` delimiter produces a parse warning. Empty path or key segments produce errors.
- **Autocomplete:** `ScriptAutocompleteProvider` offers `vault:` as a step command with its options (`path`, `key`, `keys`, `into`, `write`, `patch`, `profile`, `version`, `on_error`).

---

## Flow Canvas

New `vault` block definition in `FlowCanvas/src/blockDefs/registry.ts`:

- **Category:** Data / Secrets (or alongside `http`, `readfile`, etc.)
- **Mode selector:** Read / Write / Patch — controls which fields are shown in the properties panel
- **Properties panel fields (read):** profile (dropdown), path (text), key (text), keys (key-value map), version (optional number), into (variable name), on_error (dropdown)
- **Properties panel fields (write/patch):** profile (dropdown), path (text), data (key-value map of values to write/patch), on_error (dropdown)
- **Block appearance:** Distinct icon/color indicating external secret fetch
- **YAML round-trip:** `FlowCanvasBridge` handles `vault:` steps in import/export like other commands

---

## Documentation

`SCRIPTING.md` additions:

- **`vault:` command section** — full syntax reference with examples for read (single key, multiple keys, version pinning), write (full replace), patch (merge update), profile selection, and error handling
- **Secret rotation recipe** — complete rotation workflow example (generate, push, store, verify)
- **`{{vault:...}}` inline syntax** — documented alongside existing `{{variable}}` and `${expression}` syntax sections
- **`vault()` function** — documented in the built-in functions reference
- **`vault_list()` function** — documented in the built-in functions reference (returns list of secret paths under a prefix)
- **`vault_clear_cache()` function** — documented in the built-in functions reference
- **`vault_path` column** — documented in the CSV Grid Columns section with key naming convention
- **Vault policy reference** — minimum required policies for read-only and read-write access
- **Batch rotation recipe** — example using `vault_list()` to rotate all secrets under a prefix

---

## Settings UI

New "Vault" section in the Settings dialog General tab, below the existing Credentials section.

### Profile List

Add/Remove/Edit list of named profiles. Each profile editor contains:

- **Profile Name** — text field (unique identifier)
- **Default** checkbox — one profile marked as default
- **Address** — text field (`https://vault.company.com:8200`)
- **Namespace** — text field (optional, Vault Enterprise)
- **Mount Path** — text field (defaults to `secret`)
- **KV Version** — dropdown: Auto-detect, v1, v2 (default: Auto-detect)
- **Auth Method** — dropdown: Token, AppRole, LDAP
- **Auth fields** (change based on dropdown):
  - Token: single password field
  - AppRole: Role ID text field + Secret ID password field
  - LDAP: Username + Password fields
- **CA Certificate Path** — file picker (optional, for self-signed/internal CA certs)
- **Skip TLS Verification** — checkbox (dev/lab environments only, shows warning)
- **Cache TTL** — numeric field (seconds, default 300)
- **Test Connection** button — attempts auth + health check, reports inline

---

## Configuration Model

```csharp
public class VaultSettings
{
    public bool Enabled { get; set; } = false;
    public List<VaultProfileConfig> Profiles { get; set; } = new();
    public string DefaultProfileName { get; set; } = "";
}

public class VaultProfileConfig
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string MountPath { get; set; } = "secret";
    public VaultAuthMethod AuthMethod { get; set; } = VaultAuthMethod.Token;
    public string AppRoleRoleId { get; set; } = "";
    public string LdapUsername { get; set; } = "";
    public int CacheTtlSeconds { get; set; } = 300;
    public string CaCertificatePath { get; set; } = "";
    public bool SkipTlsVerification { get; set; } = false;
    public VaultKvVersion KvVersion { get; set; } = VaultKvVersion.AutoDetect;
}

public enum VaultKvVersion
{
    AutoDetect = 0,
    V1 = 1,
    V2 = 2
}

public enum VaultAuthMethod
{
    Token = 0,
    AppRole = 1,
    Ldap = 2
}
```

Sensitive fields stored in Windows Credential Manager:
- `SSH_Helper:vault:{profileName}:token`
- `SSH_Helper:vault:{profileName}:approle_secret`
- `SSH_Helper:vault:{profileName}:ldap_password`

---

## New Files

| File | Purpose |
|------|---------|
| `Services/Vault/VaultService.cs` | Core HTTP client, auth, caching, profile management |
| `Services/Vault/VaultProfile.cs` | Runtime profile state (token, expiry, HttpClient) |
| `Models/VaultSettings.cs` | Serializable config model |
| `Services/Vault/VaultCredentialProvider.cs` | `ICredentialProvider` backed by vault |
| `Services/Scripting/Commands/VaultCommand.cs` | `vault:` script step |
| `Services/Scripting/Functions/VaultFunctions.cs` | `vault()`, `vault_list()`, and `vault_clear_cache()` built-in functions |

## Modified Files

| File | Change |
|------|--------|
| `Models/AppConfiguration.cs` | Add `VaultSettings` property |
| `Services/Scripting/ScriptContext.cs` | Add `vault:` prefix handling in `ResolveVariableExpression` |
| `Services/Scripting/ScriptParser.cs` | Register `vault` as valid command, add vault syntax validation |
| `Services/Scripting/ScriptAutocompleteProvider.cs` | Add `vault:` command autocomplete with options |
| `Services/Credentials/CredentialTargets.cs` | Add vault credential target patterns |
| `SettingsDialog.cs` | Add Vault profile list UI section |
| `Models/JobDefinition.cs` | Add `CredentialMode.Vault`, `VaultCredentialPath` property |
| `Services/Scheduling/JobExecutionService.cs` | Handle `CredentialMode.Vault` in `ResolveCredentials` |
| `Form1.cs` | Initialize `VaultService`, pass to scripting context |
| `Models/EnvironmentConfig.cs` | Add optional `VaultProfileName` property |
| `EnvironmentDialog.cs` | Add Vault Profile dropdown per environment |
| `FlowCanvas/src/blockDefs/registry.ts` | Add `vault` block definition |
| `FlowCanvasBridge.cs` | Handle `vault:` steps in import/export |
| `SCRIPTING.md` | Document `vault:` command, inline syntax, functions, `vault_path` column |

## Unchanged Files

`ExpressionParser`, `ExpressionEvaluator`, `SshExecutionService`, `SshConnectionPool` — no modifications needed.

---

## Vault Policy Reference

Minimum Vault policies the Vault admin needs to configure for SSH_Helper access.

**Read-only (fetch secrets):**
```hcl
path "secret/data/ssh/*" {
  capabilities = ["read"]
}
path "secret/metadata/ssh/*" {
  capabilities = ["list"]
}
```

**Read-write (fetch + rotation):**
```hcl
path "secret/data/ssh/*" {
  capabilities = ["read", "create", "update", "patch"]
}
path "secret/metadata/ssh/*" {
  capabilities = ["list"]
}
```

**Notes:**
- Replace `secret` with the actual mount path and `ssh/*` with the appropriate path prefix.
- `patch` capability requires Vault 1.9+. Without it, `patch:` steps fall back to read-modify-write (requires `read` + `create` + `update`).
- `list` is only needed if scripts use `vault_list()`.
- The Test Connection button in Settings requires `read` on `sys/health` (unauthenticated by default on most Vault setups).

This policy reference is included in `SCRIPTING.md` documentation to help users configure their Vault server.

---

## Dependencies

None. Pure `HttpClient` against Vault HTTP API. No new NuGet packages.
