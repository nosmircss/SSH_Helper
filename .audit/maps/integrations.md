# Subsystem map: Vault, credentials & notifications ("integrations")

Scope: `Services/Vault/` (6 files), `Services/Credentials/` (3 files), `Services/Notifications/` (6 files),
`Services/Scripting/Commands/{VaultCommand,WebhookCommand,NotifyCommand,BrowserCallbackCaptureCommand,BrowserCallbackFocusRestorer}.cs`,
`Services/Scripting/{BrowserCallbackUiHost,BrowserCallbackWebViewProfileManager}.cs`, `Services/Scripting/Functions/VaultFunctions.cs`,
plus the Form1/SettingsDialog/JobExecutionService wiring that makes them reachable.
Discovered beyond the given scope list: `BrowserCallbackFocusRestorer.cs`, `BrowserCallbackWebViewProfileManager.cs`,
`NotifyCommand.cs` (the actual consumer of NotificationService), `UI/BrowserCallbackWebViewDialog.cs`,
`Models/VaultSettings.cs`, `Models/NotificationSettings.cs`.

---

## 1. Feature inventory

### 1.1 HashiCorp Vault client — `Services/Vault/VaultService.cs` (1,142 LOC)

Core HTTP client over Vault's REST API. One `VaultProfile` runtime object per configured
`VaultProfileConfig` (lazy, race-safe creation `VaultService.cs:1001-1033`; own `HttpClient` per profile,
`VaultProfile.cs:29-36`).

**Reachable from:** Settings → Vault tab (profiles CRUD + Test Connection), host-grid `vault_path` column,
job `CredentialMode.Vault`, script `vault:` step, inline `{{vault:...}}` expressions, `vault()/vault_list()` functions.

| Capability | Where | Notes |
|---|---|---|
| Profile resolution | `VaultService.cs:75-84` | order: explicit override → env-profile override → `VaultSettings.DefaultProfileName` → first profile |
| Read single key | `ReadSecretAsync` `:86-107` | per-key cache; missing key throws with **list of available keys** in message (`:99-101`) |
| Read multiple keys | `ReadSecretKeysAsync` `:109-148` | **all-or-nothing**: any missing key throws (`:134-139`) |
| Write secret | `WriteSecretAsync` `:150-178` | KV2 wraps in `{data}`; invalidates path cache |
| Patch secret | `PatchSecretAsync` `:180-194` | KV2 merge-patch (`TryPatchV2Async` `:711-753`), 405 → read-modify-write fallback (`:755-798`) |
| List secrets | `ListSecretsAsync` `:196-224` | KV2 uses `metadata/` prefix; custom `LIST` HTTP method |
| Test connection | `TestConnectionAsync` `:230-252` | auth + `sys/health`; 200/429/472/473 considered healthy; 501 "not initialized", 503 "sealed" |
| Secret value cache | `:1037-1086` | key = `profile|path|v=…|key`; TTL = `CacheTtlSeconds` (default 300, `VaultSettings.cs:110`); invalidated on write/patch; `ClearCache()` exposed + `vault_clear_cache()` function |
| Error translation | `:951-997` | Vault `errors[]` array extracted; 400/401/403/404/503 → user-friendly `VaultException`; connection refused detected via inner `SocketException` (`:848-853`) |
| TLS options | `CreateDefaultHandler` `:1090-1117` | `SkipTlsVerification` → `DangerousAcceptAnyServerCertificateValidator`; or custom CA via `CustomRootTrust` chain build |
| Vault Enterprise namespace | `ApplyNamespaceHeader` `:833-837` | `X-Vault-Namespace` on every request |
| KV version autodetect | `:577-666` | `sys/mounts/{mount}/tune` first; on 403/error falls back to probing `…/data/detect-kv-version-probe` (v2) then `…/detect-kv-version-probe` (v1); **both-404 defaults to V2** (`:664-665`) |

**Auth methods** (`GetAuthenticatedProfileAsync` `:282-313`, dispatched on `VaultAuthMethod`, `Models/VaultSettings.cs:131-147`):

- **Token** (`:315-325`) — token pulled from injected `tokenProvider` (Credential Manager in prod), validated via `auth/token/lookup-self`.
- **AppRole** (`:327-366`) — `role_id` from config, `secret_id` from provider.
- **LDAP** (`:368-404`) — username in config, password from provider.
- **Userpass** (`:406-446`) — username in config (validated non-empty `:413-416`), password from provider.
- **OIDC** (`:448-573`) — see 1.2.

Token expiry for all methods = `lease_duration * 0.75` (lines 359, 397, 439, 563, 941); `lease_duration <= 0`
or absent → `DateTime.MaxValue` (never re-auth). There is **no `renew-self`** — expiry always triggers a full re-auth.

### 1.2 Vault OIDC browser login

- `VaultService.AuthenticateWithOidcAsync` (`VaultService.cs:448-573`):
  1. Fast path: persisted token from Credential Manager validated via lookup-self (`TryAuthenticateWithPersistedOidcTokenAsync` `:882-921`); 401/403 clears it and falls through to browser.
  2. Generates `state`/`nonce` (32B) + PKCE S256 verifier/challenge (64B) (`:469-472`, `:1119-1135`).
  3. POST `v1/auth/{mount}/oidc/auth_url` with redirect_uri/state/nonce/code_challenge (`:474-497`).
  4. Browser flow via `IVaultOidcLoginFlow` (seam for tests).
  5. Validates `state` equality (`:522-523`), exchanges code at `v1/auth/{mount}/oidc/callback` with `code_verifier` (`:528-552`).
  6. New client token persisted back through `tokenSaver` → Credential Manager (`Form1.cs:1416-1420`).
- `VaultOidcLoginFlow.cs` — `HttpListener` on a **root prefix** `http://{host}:{port}/` (`:42`), opens the system browser
  (`Process.Start UseShellExecute`, `:57-61`), loops serving 404/405 for non-callback requests, returns
  state/code/error from the first GET on the callback path, writes a static "login complete/failed" HTML page (`:95-99`).
  Timeout = `max(15, OidcTimeoutSeconds)` (`:69`, default 180, `VaultSettings.cs:105`).
- `VaultOidcCallbackSettings.cs` — callback binding validation: **loopback-only hosts** (127.0.0.1 / localhost / ::1,
  `:76-93`), port 1-65535 (default 8250), path normalized to leading `/` (default `/oidc/callback`).

### 1.3 Vault → SSH credential adapter — `VaultCredentialProvider.cs`

- Implements `ICredentialProvider` over `VaultService`; `vault_path` grammar
  **`[profile@]path[#usernameKey,passwordKey]`** with key defaults `username`/`password` (`ParseVaultPath` `:78-124`).
- `TryGetPassword` is **sync-over-async** (`.GetAwaiter().GetResult()` `:48-49`); success requires non-empty
  password (`:57`); **all exceptions swallowed** to `Debug.WriteLine` (`:59-63`).
- `SavePassword`/`DeletePassword` are intentional no-ops returning false (`:66-68`).

### 1.4 Credential resolution order (the actual contract)

**Main-form grid** (`Form1.GetHostConnections`, `Form1.cs:13301-13366`):
1. `vault_path` cell → `VaultCredentialProvider.TryGetPassword(vaultPath, …, _sshService.EnvironmentVaultProfile)` (`:13332-13343`). On success both username AND password come from Vault.
2. else `username`/`password` cells.
3. If a password was typed in the grid and Credential Manager is available, it is **auto-saved** under `SSH_Helper:host:{host}|user:{user}` (`:13349-13352`); if the cell is empty, the stored per-host password is auto-loaded (`:13353-13356`).
4. Toolbar username/password as the global default; default password persisted under `SSH_Helper:default` when `Credentials.UseCredentialManager` is on (`TryLoadDefaultPassword`/`StoreDefaultPassword` `Form1.cs:1454-1480`).

**Scheduled jobs** (`Services/JobExecutionService.cs`):
- Per-row: `vault_path` column first (`BuildHostConnections` `:508-523`, default profile = job `VaultProfileName` → environment profile, `:647-655`), else `username`/`password` columns (`:525-532`).
- Job-level `CredentialMode` (`ResolveCredentials` `:592-645`): `Stored` (Credential Manager `SSH_Helper:job:{jobId}`), `InheritFromApp` (config username + `SSH_Helper:default` password), `PerHostColumn`, `Vault` (`job.VaultCredentialPath` through the same provider). All failure paths return `("", "")` with only a `Debug.WriteLine` warning.

### 1.5 Windows Credential Manager — `Services/Credentials/`

- `ICredentialProvider.cs` — 4-member abstraction (IsAvailable/TryGet/Save/Delete).
- `CredentialManagerProvider.cs` — P/Invoke advapi32 `CredRead`/`CredWrite`/`CredDelete`/`CredFree`,
  `CRED_TYPE_GENERIC`, `CRED_PERSIST_LOCAL_MACHINE` (`:11-12`); password blob stored UTF-16 (`:59`). Boolean
  results only — no error codes surfaced.
- `CredentialTargets.cs` — single source of target names, prefix `SSH_Helper` vs `SSH_Helper_Portable`
  (portable build keeps separate secrets, `:10-11,75-78`). Targets: `:default`, `:host:{host}|user:{user}`,
  `:job:{jobId}`, `:vault:{profile}:{authType}`, `:notify:{profile}:{webhook_url|smtp_password}`.
- SettingsDialog persists Vault secrets with authType literals `token` / `approle_secret` / `ldap_password` /
  `userpass_password` (`SettingsDialog.cs:1013-1027`), deletes them when a profile is removed (`:854-857`),
  and migrates notify secrets on profile rename (`:1562-1563`).

### 1.6 Notification orchestration — `Services/Notifications/NotificationService.cs`

- Channel kinds: Slack / Teams / Discord / Toast / Smtp (`Models/NotificationSettings.cs:83-99`); profile model
  holds non-secret config; webhook URL and SMTP password live in Credential Manager only (comment `:27`).
- Resolution rules (`SendAsync` `:76-181`): channel+profile must agree; profile-only infers channel; channel-only
  is valid **only for toast** (non-toast returns "requires a profile" `:179-180`); neither → `DefaultProfileName`.
- Channel aliases: `smtp`/`email`/`mail` (`:209-212`).
- Master `Enabled` gate blocks Slack/Teams/Discord/SMTP but **never toast** (`:163-177`) — toasts work with
  notifications disabled, by design.
- Owns a 30s-timeout `HttpClient` (`:34-46`); always disposes it (`_ownsHttpClient` is true on both branches).
- Returns `NotificationResult` (`NotificationResult.cs`) — `Sent`/`Channel`/`StatusCode`/`ErrorMessage` — consumed
  by the script `into:` capture.

### 1.7 Channel dispatchers

- **WebhookDispatcher.cs** (Slack/Teams/Discord share POST-JSON transport `:29-76`):
  - Slack: legacy `attachments` + hardcoded hex colors per level (`:78-106`); mention normalization for
    `@here/@channel/@everyone`, raw member IDs `U…/W…` → `<@ID>` (`:108-130`).
  - Teams: Power-Automate-style `message` + Adaptive Card 1.2 (`TeamsAdaptiveCardPayloadBuilder.cs:43-74`);
    typed mentions `upn:user@x|Display` and `entra:{guid}|Display` become real `msteams.entities` mentions
    (`:79-162`); untyped mentions degrade to literal text **with a warning** surfaced pre-dispatch
    (`NotifyCommand.cs:57-61` via `CollectWarnings`).
  - Discord: `embeds` + decimal colors; typed mentions `user:/role:/channel:{id}`, `@here/@everyone` (`:132-191`).
  - Failure body trimmed into `NotificationResult.ErrorMessage` with HTTP status (`:64-66`).
- **SmtpDispatcher.cs** — `System.Net.Mail.SmtpClient` (`:86-95`), subject prefix `[INFO]/[WARN]/[ERROR]/[OK]`
  (`:41-48`), `EnableSsl = profile.UseStartTls`, optional auth (password from provider), plain-text body,
  attachments validated for existence — a missing file **fails the whole send** (`:72-73`).
- **ToastDispatcher.cs** — `Microsoft.Toolkit.Uwp.Notifications` `ToastContentBuilder`; optional level
  attribution line; comment documents that unpackaged apps can't handle click activation (`:8-9`).

### 1.8 `notify` script command — `Services/Scripting/Commands/NotifyCommand.cs`

- YAML options: message (required), profile, channel, title, level (info/warn/error/success + aliases),
  mention[], attachments[], into, on_error. All values variable-substituted.
- `into:` capture writes **dotted** variables: `x.sent`, `x.channel`, `x.status_code`, `x.error` (`:98-110`).
- `on_error: continue` (or step-level) → `CommandResult.Suppressed` + `_last_error` (`:89-93`).
- **When notifications fire:** only here. UI runs and scheduled jobs both pass `NotificationService` into
  `ScriptContext` (`SshExecutionService.cs:1772-1774`, `JobExecutionService.cs:410`), so `notify:` steps work in
  both contexts. There is **no automatic run/job-completion notification** anywhere — scheduler completion is an
  output-panel text line only (`SchedulerNotificationFormatter`, see `Form1.cs:15250-15256`).

### 1.9 `webhook` script command — `Services/Scripting/Commands/WebhookCommand.cs`

- Generic HTTP step: any method (default POST), headers (substituted, `TryAddWithoutValidation` `:73`), body for
  POST/PUT/PATCH with Content-Type detection from headers else `application/json` (`:78-98`), timeout default 30s
  (`ScriptStep.cs:1019`), http/https scheme enforcement (`:52-53`).
- Capture: `into` → response body, `into_status` → status code; both **pre-cleared** so failures never leave stale
  values (`:36-37`, `:156-163`).
- Explicit design note: **no SSRF/private-range filtering** so scripts can target localhost/RFC1918 (`:55-58`).
- Static shared `HttpClient` (`:16`) + internal handler-factory ctor for tests.
- Non-2xx → `CommandResult.ApplyOnError` (honors step on_error); timeout/HTTP/exception paths likewise.

### 1.10 `browser_callback_capture` command — `BrowserCallbackCaptureCommand.cs` (713 LOC)

OAuth-style local capture of values from a browser redirect.

- Options (`ScriptStep.cs:1070-1141`): `start_url`*, `callback_path`* (default `/oauth_callback`), `into`*,
  `local_port` (default **8086**), `capture_mode` auto/query/fragment/post_body, `browser_mode`
  external/webview2, `show_after_seconds`, `required_fields[]`, `timeout` (default 300s), `open_browser` (true),
  `auto_close_browser` (true), `completion_message`/`failure_message`, `quiet` (default **true**).
- Listener bound to `http://127.0.0.1:{port}/` only (`CreateListener` `:219-224`). Three endpoints:
  callback GET serves a JS bridge page that re-posts query (`q:` prefix) and fragment (`h:` prefix) params to
  `{callback}/capture`, then signals `{callback}/complete` (`:226-308`, page HTML `:544-592`); `capture_mode=query`
  short-circuits on the GET itself (`:249-260`). Dark-mode-aware result pages (`:630-657`, `IsDarkModeEnabled` `:687-703`).
- Capture persisted as: `into` = sorted JSON object, `into_count`, `into_keys`, plus one `into_{sanitizedKey}`
  variable per field (`PersistCapture` `:436-451`); previous `into_*` variables are swept first (`ClearCapture` `:453-473`).
- `required_fields` validated post-capture (`:152-168`).
- **External mode**: default browser via `Process.Start`; after capture the app forcibly re-foregrounds itself via
  `BrowserCallbackFocusRestorer` (`:146-149`) — `AttachThreadInput` + retry loop at 350/650/1000/1500/2200 ms
  (`BrowserCallbackFocusRestorer.cs:56-119,142-182`).
- **WebView2 mode** (`BrowserCallbackUiHost.cs`): embedded dialog owned by an eligible app form (`:151-183`);
  `show_after_seconds` keeps the window hidden unless the callback is still pending (`:383-413`); user closing the
  window fails the step (`ClosedByUser` raced against capture, command `:127-135`); `auto_close_browser:false`
  keeps the window open and marks a completion state (`:176-189`).
- WebView2 profile isolation: dedicated user-data dir `%LocalAppData%\SSH_Helper\WebView2\BrowserCallback`
  (`BrowserCallbackWebViewProfileManager.cs:25-26`); Settings → General → "Clear Embedded Browser Data…" wipes it,
  refused while a session is active (`SettingsDialog.cs:2466-2495`, manager `:46-62`).

### 1.11 Vault scripting surfaces

- **`vault:` step** (`VaultCommand.cs`): modes are mutually exclusive in priority order write → patch →
  keys (multi-read into mapped variables) → key+into (single read) (`:32-44`); `version:` pin for KV2 reads;
  per-step `profile:`; values for write/patch parse structured JSON, including a recovery pass for
  previously-stringified escaped JSON (`TryParseStructuredJson` `:145-198`); `on_error: continue` →
  `Suppressed` + `_last_error` (`:50-55`).
- **Inline `{{vault:[profile@]path#key}}`** (`ScriptContext.cs:651-756`): requires `#`; errors set `_last_error`
  and resolve to **empty string** (never throw); sync-over-async read (`:748`).
- **Functions** (`VaultFunctions.cs`): `vault(path, key[, profile])`, `vault_list(prefix[, profile])`,
  `vault_clear_cache()`; all failures return `null`/`false` silently — **no `_last_error`** is set (`:38-41,65-68`).

### 1.12 Settings UI

- Vault tab: profile list CRUD, all `VaultProfileConfig` fields incl. OIDC callback host/port/path/timeout,
  KV version, cache TTL, CA cert browse (`SettingsDialog.cs:1029-1039`), TLS skip, default-profile checkbox;
  "Test Connection" builds a throwaway `VaultService` fed from the **in-dialog textboxes** (not saved creds)
  and even persists an OIDC token acquired during the test (`:1041-1080`).
- Notifications tab: profile CRUD per channel, webhook URL + SMTP password fields round-tripped through
  Credential Manager (`:1501-1503`, `:1579-1580`), rename migration (`:1562-1563`), deletion cleanup (`:1453-1454`).

### 1.13 Service lifecycle wiring (Form1)

- `InitializeCredentials` (`Form1.cs:1358-1366`) → `CredentialManagerProvider`.
- `InitializeVault` (`:1373-1425`) — disposes/rebuilds `VaultService` with credential-manager-backed providers
  (token/approle_secret/ldap_password/userpass_password) + tokenSaver; injects into `_sshService.VaultService` and
  `_jobExecutionService.VaultCredentialProvider`; skipped entirely when `Vault.Enabled` false or no profiles (`:1391-1392`).
- `InitializeNotifications` (`:1427-1452`) — same pattern for `NotificationService` (always constructed; the
  Enabled gate is inside `SendAsync`).
- Both re-run after the Settings dialog saves (`:5747-5748`); environment switch rewires only
  `EnvironmentVaultProfile` (`:1897-1903`).

---

## 2. Integration points

| Connection | Mechanism | Evidence |
|---|---|---|
| Vault/Notify → script engine | `ScriptContext.VaultService` / `.NotificationService` / `.EnvironmentVaultProfile` properties | `ScriptContext.cs:364-376`; set by `SshExecutionService.cs:1772-1774` and Form1 test-step path `Form1.cs:7198-7203` |
| Command registration | enum-keyed dictionary in executor ctor | `ScriptExecutor.cs:152` (BrowserCallbackCapture), `:157` (Webhook), `:170` (Vault), `:172` (Notify) |
| Scheduler → vault/notify | `JobExecutionService.VaultCredentialProvider` / `.NotificationService` / `.EnvironmentVaultProfile` props (`:70-80`), copied onto the per-run `SshExecutionService` (`:405-410`) | `Services/JobExecutionService.cs` |
| Environment system | `EnvironmentChanged` event updates `EnvironmentVaultProfile` on SSH + job services | `Form1.cs:1897-1903`; per-environment `VaultProfileName` on the environment config |
| Settings persistence | `AppConfiguration.Vault` (`VaultSettings`) and `.Notifications` (`NotificationSettings`) in `config.json`; secrets exclusively in Credential Manager via `CredentialTargets` | `Models/VaultSettings.cs`, `Models/NotificationSettings.cs`, `SettingsDialog.cs:1013-1027,1579-1580` |
| Job storage | `JobStorageService` ctor takes `ICredentialProvider` (Stored-mode job creds) | `Form1.cs:15059` |
| WebView2 data root | `AppDataPaths.GetAppFolder()` + `WebView2/BrowserCallback` (portable-build aware) | `BrowserCallbackWebViewProfileManager.cs:25-26` |
| Test seams | internal ctors: `VaultService(…, IVaultOidcLoginFlow)`, `WebhookCommand(handlerFactory)`, `BrowserCallbackCaptureCommand(uiHost, listenerFactory)`, `NotificationService(httpHandler, toast/smtp dispatcher overrides)` | respective files; suites exist under `SSH_Helper.Tests/Vault/`, `Scripting/`, `Services/`, `UI/` (VaultService, VaultCredentialProvider, NotificationService, NotifyCommand, BrowserCallback*, CredentialTargets, CredentialManagerProvider, SettingsDialogVault, Form1NotificationInitialization) |

---

## 3. Observed gaps & quirks

### Correctness / encoding
1. **Mojibake in user-facing OIDC errors** — `VaultOidcLoginFlow.cs:37,51,65` contain `â€”` (double-encoded em
   dash) in `VaultException` messages ("Vault OIDC login failed â€” cannot open browser…"). Verified on-disk bytes,
   not a display artifact. All other Vault files use a clean `—`.
2. **Multi-key read is all-or-nothing** — `ReadSecretKeysAsync` throws if *any* requested key is absent
   (`VaultService.cs:134-139`). `VaultCredentialProvider` always asks for username+password, so a Vault secret
   holding only a `password` field can never resolve (the `result.TryGetValue` tolerance at
   `VaultCredentialProvider.cs:51-52` is unreachable for the missing-key case). No username-optional path.
3. **KV autodetect both-404 defaults to V2** (`VaultService.cs:664-665`) — an empty/locked-down KV-v1 mount probes
   404 on both paths and is then driven with `data/`-prefixed URLs; every subsequent read fails with a misleading
   "No secret found".

### UX / responsiveness
4. **Sync-over-async Vault reads on the UI thread** — `Form1.GetHostConnections` resolves `vault_path` via
   `VaultCredentialProvider.TryGetPassword` → `.GetAwaiter().GetResult()` (`Form1.cs:13334`,
   `VaultCredentialProvider.cs:48-49`). If the profile is OIDC and the cached token has expired, this launches a
   full browser login **synchronously**, freezing the UI for up to `OidcTimeoutSeconds` (180s default). Same
   pattern in `ScriptContext.ResolveVaultExpression` (`ScriptContext.cs:748`) and `VaultFunctions.cs:36,63`.
5. **Silent vault_path failure falls through to wrong credentials** — provider swallows every exception to
   `Debug.WriteLine` (`VaultCredentialProvider.cs:59-63`); a typo'd path/profile silently falls back to grid
   columns / stored / toolbar password and the SSH attempt proceeds with the wrong identity. Job-side mirror:
   `ResolveCredentials` returns `("","")` with only debug output (`JobExecutionService.cs:605,639`).
6. **Inconsistent error surfacing across the three vault scripting surfaces** — inline `{{vault:…}}` sets
   `_last_error` + empty string; the `vault:` step fails (or `Suppressed` + `_last_error`); `vault()`/`vault_list()`
   return `null` and set **nothing** (`VaultFunctions.cs:38-41,65-68`). Scripts can't distinguish "no secret" from
   "vault down" via functions.
7. **Implicit credential persistence from the grid** — any password typed into the grid's `password` column is
   auto-saved to Windows Credential Manager keyed `host|user` (`Form1.cs:13346-13352`) without prompt, and silently
   auto-loaded later (`:13353-13356`). There is no UI to enumerate/clear per-host stored passwords (only the default
   password has a clear path, `:1474-1480`).
8. **No automatic scheduler/run notifications** — `NotificationService` fires only from explicit `notify:` script
   steps; job success/failure produces just an output-panel line + status bar update (`Form1.cs:15250-15256`).
   A per-job "notify on failure" toggle is the obvious missing affordance for a multi-host scheduler. (Matches the
   standing enhancement-audit theme.)
9. **OIDC re-auth mid-run pops a browser without warning** — token expiry during a long script triggers
   `AuthenticateWithOidcAsync` from whatever thread requested the secret; the user gets a surprise browser window
   and the step blocks until login/timeout.

### Security
10. **Local callback endpoints have no anti-spoofing token** — `browser_callback_capture`'s listener accepts any
    local POST to `{callback}/capture` (`BrowserCallbackCaptureCommand.cs:282-295`); first non-empty payload wins.
    Bound to 127.0.0.1 only, but any local process — or any web page already open in the user's browser issuing
    `fetch('http://127.0.0.1:8086/…')` — can inject values into script variables. No state/nonce equivalent of the
    Vault OIDC flow.
11. **Captured callback values & vault secrets land in plain script variables** — `PersistCapture`
    (`:436-451`) and `vault` reads put secrets into the ordinary variable map where later `print`/history/debug
    panels can echo them. No secret-tagging/redaction concept exists.
12. **`SkipTlsVerification` accepts any cert** (`VaultService.cs:1094-1098`) — documented as dev-only in the model
    (`VaultSettings.cs:118-120`) but there is no UI warning badge when enabled.
13. **Webhook step is SSRF-capable by design** (documented `WebhookCommand.cs:55-58`); additionally the full URL —
    including any query-string secrets — is echoed to debug output (`:100`), and header values (e.g. Authorization
    built from variables) are not redacted anywhere downstream.
14. **Missing-key VaultException enumerates all available secret keys** (`VaultService.cs:99-101,136-139`) —
    helpful, but leaks secret structure into script output/history/notifications.
15. **Unchecked `SavePassword` returns** — `CredWrite` failures (e.g. blob size limits) are silently ignored at the
    OIDC token-saver (`Form1.cs:1419`) and notification secret save (`SettingsDialog.cs:1026`), so a failed persist
    only shows up as a future re-login/re-prompt.

### Consistency / latent traps
16. **Vault provider authType parameter is decorative** — `VaultService` passes `"token"/"approle"/"ldap"/"userpass"`
    as the providers' second arg (`VaultService.cs:317,329,370,408`) but Form1's lambdas ignore it and hardcode the
    *different* literals `approle_secret`/`ldap_password`/`userpass_password` (`Form1.cs:1396-1415`). Consistent
    today only because SettingsDialog uses the same hardcoded strings; any future caller trusting the parameter
    writes/reads the wrong credential slot. Tests even exercise the never-used `"approle"` literal
    (`SSH_Helper.Tests/Vault/VaultSettingsTests.cs:186`).
17. **OIDC and Token auth share the same `:token` credential slot** — switching a profile's AuthMethod between
    Token and Oidc silently reuses/overwrites the same stored token (`Form1.cs:1396-1400,1416-1420`).
18. **Capture-variable naming is inconsistent** — `notify` `into:` writes dotted names (`x.sent`,
    `NotifyCommand.cs:104-109`) while `webhook` uses `_status` and `browser_callback_capture` uses underscore
    suffixes; dotted variable names interact awkwardly with any expression syntax that treats `.` as a path separator.
19. **SMTP**: built on the deprecated `System.Net.Mail.SmtpClient`; `EnableSsl` only does STARTTLS — implicit-TLS
    port 465 servers are unsupported (`SmtpDispatcher.cs:86-90`); a single missing attachment fails the entire
    notification (`:72-73`); no per-send timeout beyond SmtpClient's 100s default.
20. **Slack payload uses the legacy `attachments` API** (deprecated by Slack in favor of blocks;
    `WebhookDispatcher.cs:93-105`) and hardcoded hex colors; Teams card is fixed at schema 1.2.
21. **KV autodetect probes pollute audit logs** — reads of `detect-kv-version-probe` (`VaultService.cs:629,648`)
    show up as 404 read attempts in Vault audit devices.
22. **Toast bypasses the master Enabled switch** (`NotificationService.cs:163-177`) — deliberate, but the Settings
    toggle reads as global; a quiet/"do not disturb" expectation is violated.
23. **`browser_callback_capture` `quiet` defaults to true** (`ScriptStep.cs:1140`) — the success summary is
    suppressed by default, inverted from every other command's verbosity default.
24. **Hardcoded defaults**: callback port 8086 (`ScriptStep.cs:1085`), OIDC port 8250, listener prefixes — port
    conflicts produce a step failure with no automatic fallback port selection (command `:67-74`,
    `VaultOidcLoginFlow.cs:44-53`).
25. **`SetCacheValue` ignores invalid TTLs** — `CacheTtlSeconds` ≤ 0 silently produces an always-expired cache
    (`VaultService.cs:1060-1071`); no validation in the settings UI numeric.

### Maturity assessment
Well-tested core (dedicated suites for VaultService auth/KV/cache, VaultCredentialProvider parsing,
NotificationService routing, NotifyCommand, the entire browser-callback stack, CredentialTargets incl. portable
prefix). The Vault client is the most mature piece (friendly error translation, corruption-tolerant autodetect,
race-safe profiles). The weakest seams are the *silent-fallback* credential resolution paths and the absence of any
event/notification on scheduler outcomes.
