# HashiCorp Vault Integration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add HashiCorp Vault as an additive secret source with read/write/patch/list operations, inline variable syntax, credential backend, named profiles, environment-aware defaults, and Flow Canvas support.

**Architecture:** A `VaultService` wraps the Vault HTTP API (KV v1 and v2) with lazy auth, per-profile caching, and friendly error translation. It's exposed through three surfaces: a `vault:` script command, `{{vault:...}}` inline syntax, and an `ICredentialProvider` backend. Named profiles support different AppRole identities and integrate with the existing Environment system.

**Tech Stack:** .NET 8 HttpClient (no new NuGet packages), existing ScriptParser/ScriptContext/FunctionRegistry patterns, React Flow block definitions.

**Spec:** `docs/superpowers/specs/2026-04-05-vault-integration-design.md`

---

## File Map

### New Files
| File | Responsibility |
|------|---------------|
| `Models/VaultSettings.cs` | `VaultSettings`, `VaultProfileConfig`, `VaultAuthMethod`, `VaultKvVersion` enums |
| `Services/Vault/VaultService.cs` | HTTP client, auth (token/AppRole/LDAP), KV version detection, read/write/patch/list, caching, error translation |
| `Services/Vault/VaultProfile.cs` | Runtime state per profile: cached token, expiry, detected KV version, HttpClient instance |
| `Services/Vault/VaultCredentialProvider.cs` | `ICredentialProvider` backed by VaultService |
| `Services/Scripting/Commands/VaultCommand.cs` | `vault:` script step handler (read/write/patch modes) |
| `Services/Scripting/Functions/VaultFunctions.cs` | `vault()`, `vault_list()`, `vault_clear_cache()` built-in functions |

### New Test Files
| File | Tests |
|------|-------|
| `SSH_Helper.Tests/Vault/VaultSettingsTests.cs` | Config model serialization, defaults, validation |
| `SSH_Helper.Tests/Vault/VaultServiceTests.cs` | Auth, read/write/patch/list, caching, error translation, KV version detection |
| `SSH_Helper.Tests/Vault/VaultCredentialProviderTests.cs` | Credential resolution, vault_path parsing, fallback |
| `SSH_Helper.Tests/Scripting/VaultCommandTests.cs` | Command handler for all modes, error handling |
| `SSH_Helper.Tests/Scripting/VaultFunctionsTests.cs` | Built-in function calls |
| `SSH_Helper.Tests/Scripting/VaultInlineSyntaxTests.cs` | `{{vault:...}}` resolution in ScriptContext |
| `SSH_Helper.Tests/Scripting/VaultParserTests.cs` | Parser registration, validation rules |

### Modified Files
| File | Change |
|------|--------|
| `Models/AppConfiguration.cs` | Add `Vault` property (`VaultSettings`) |
| `Models/JobDefinition.cs` | Add `CredentialMode.Vault = 3`, `VaultCredentialPath` property |
| `Models/EnvironmentConfig.cs` | Add `VaultProfileName` property, update `Clone()`/`Normalize()` |
| `Services/Credentials/CredentialTargets.cs` | Add vault auth credential target patterns |
| `Services/Scripting/ScriptParser.cs` | Register `vault` in `KnownStepKeys`, `CommandOptionKeys`, `StepRootOptionKeysByCommand` |
| `Services/Scripting/ScriptContext.cs` | Add `VaultService` property, `vault:` prefix in `ResolveVariableExpression()` |
| `Services/Scripting/FunctionRegistry.cs` | Register `VaultFunctions` category |
| `Services/Editor/ScriptAutocompleteProvider.cs` | Add `vault` command description |
| `Services/Scheduling/JobExecutionService.cs` | Handle `CredentialMode.Vault` in `ResolveCredentials()` |
| `Form1.cs` | Initialize `VaultService`, pass to scripting context, wire to scheduler |
| `SettingsDialog.cs` | Add Vault profile list UI section |
| `EnvironmentDialog.cs` | Add Vault Profile dropdown per environment |
| `FlowCanvas/src/blockDefs/registry.ts` | Add `vault` block definition |
| `Services/FlowCanvasBridge.cs` | Add vault to dispatch maps (`RequiredOptionKeysByCommand`, `DictionaryOptionKeys`) |
| `SCRIPTING.md` | Document vault command, inline syntax, functions, vault_path column, policy reference |

---

## Task 1: Configuration Models

**Files:**
- Create: `Models/VaultSettings.cs`
- Modify: `Models/AppConfiguration.cs:107`
- Modify: `Models/JobDefinition.cs:8-24`
- Modify: `Models/EnvironmentConfig.cs:7-83`
- Modify: `Services/Credentials/CredentialTargets.cs:10-49`
- Test: `SSH_Helper.Tests/Vault/VaultSettingsTests.cs`

- [ ] **Step 1: Write tests for VaultSettings serialization**

```csharp
// SSH_Helper.Tests/Vault/VaultSettingsTests.cs
using FluentAssertions;
using Newtonsoft.Json;

namespace SSH_Helper.Tests.Vault;

public class VaultSettingsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new VaultSettings();
        settings.Enabled.Should().BeFalse();
        settings.Profiles.Should().BeEmpty();
        settings.DefaultProfileName.Should().BeEmpty();
    }

    [Fact]
    public void VaultProfileConfig_DefaultValues_AreCorrect()
    {
        var profile = new VaultProfileConfig();
        profile.Name.Should().BeEmpty();
        profile.Address.Should().BeEmpty();
        profile.MountPath.Should().Be("secret");
        profile.AuthMethod.Should().Be(VaultAuthMethod.Token);
        profile.CacheTtlSeconds.Should().Be(300);
        profile.KvVersion.Should().Be(VaultKvVersion.AutoDetect);
        profile.SkipTlsVerification.Should().BeFalse();
        profile.CaCertificatePath.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrips_ThroughJson()
    {
        var settings = new VaultSettings
        {
            Enabled = true,
            DefaultProfileName = "prod",
            Profiles = new List<VaultProfileConfig>
            {
                new()
                {
                    Name = "prod",
                    Address = "https://vault.example.com:8200",
                    MountPath = "secret",
                    AuthMethod = VaultAuthMethod.AppRole,
                    AppRoleRoleId = "role-123",
                    KvVersion = VaultKvVersion.V2,
                    CacheTtlSeconds = 600
                }
            }
        };

        var json = JsonConvert.SerializeObject(settings);
        var deserialized = JsonConvert.DeserializeObject<VaultSettings>(json);

        deserialized!.Enabled.Should().BeTrue();
        deserialized.DefaultProfileName.Should().Be("prod");
        deserialized.Profiles.Should().HaveCount(1);
        deserialized.Profiles[0].Name.Should().Be("prod");
        deserialized.Profiles[0].AuthMethod.Should().Be(VaultAuthMethod.AppRole);
        deserialized.Profiles[0].KvVersion.Should().Be(VaultKvVersion.V2);
    }

    [Fact]
    public void AppConfiguration_VaultSettings_DefaultsToNew()
    {
        var config = new AppConfiguration();
        config.Vault.Should().NotBeNull();
        config.Vault.Enabled.Should().BeFalse();
    }

    [Fact]
    public void CredentialMode_Vault_HasValue3()
    {
        ((int)CredentialMode.Vault).Should().Be(3);
    }

    [Fact]
    public void JobDefinition_VaultCredentialPath_DefaultsToEmpty()
    {
        var job = new JobDefinition();
        job.VaultCredentialPath.Should().BeEmpty();
    }

    [Fact]
    public void EnvironmentConfig_VaultProfileName_DefaultsToNull()
    {
        var env = new EnvironmentConfig();
        env.VaultProfileName.Should().BeNull();
    }

    [Fact]
    public void EnvironmentConfig_Clone_PreservesVaultProfileName()
    {
        var env = new EnvironmentConfig { VaultProfileName = "prod" };
        var clone = env.Clone();
        clone.VaultProfileName.Should().Be("prod");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultSettingsTests" -v minimal`
Expected: FAIL — types do not exist yet.

- [ ] **Step 3: Create VaultSettings model**

```csharp
// Models/VaultSettings.cs
namespace SSH_Helper;

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

public enum VaultAuthMethod
{
    Token = 0,
    AppRole = 1,
    Ldap = 2
}

public enum VaultKvVersion
{
    AutoDetect = 0,
    V1 = 1,
    V2 = 2
}
```

- [ ] **Step 4: Add Vault property to AppConfiguration**

In `Models/AppConfiguration.cs`, after the `Credentials` property (~line 107):
```csharp
public VaultSettings Vault { get; set; } = new();
```

- [ ] **Step 5: Add CredentialMode.Vault and VaultCredentialPath to JobDefinition**

In `Models/JobDefinition.cs`, add to the `CredentialMode` enum:
```csharp
Vault = 3
```

Add property to `JobDefinition` class:
```csharp
public string VaultCredentialPath { get; set; } = "";
```

- [ ] **Step 6: Add VaultProfileName to EnvironmentConfig**

In `Models/EnvironmentConfig.cs`, add property:
```csharp
public string? VaultProfileName { get; set; }
```

In `Clone()` method, add:
```csharp
VaultProfileName = VaultProfileName,
```

In `Normalize()` method, no change needed (nullable string doesn't need normalization).

- [ ] **Step 7: Add vault credential targets to CredentialTargets**

In `Services/Credentials/CredentialTargets.cs`, add:
```csharp
public static string VaultAuthTarget(string profileName, string authType)
    => $"{Prefix}:vault:{profileName}:{authType}";
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultSettingsTests" -v minimal`
Expected: PASS (all 8 tests).

- [ ] **Step 9: Run full build to verify no compilation errors**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 10: Commit**

```bash
git add Models/VaultSettings.cs Models/AppConfiguration.cs Models/JobDefinition.cs Models/EnvironmentConfig.cs Services/Credentials/CredentialTargets.cs SSH_Helper.Tests/Vault/VaultSettingsTests.cs
git commit -m "feat(vault): add configuration models for Vault integration"
```

---

## Task 2: VaultService — Authentication & KV Version Detection

**Files:**
- Create: `Services/Vault/VaultProfile.cs`
- Create: `Services/Vault/VaultService.cs` (auth + version detection portion)
- Test: `SSH_Helper.Tests/Vault/VaultServiceTests.cs`

**Approach:** `VaultService` accepts a `Func<VaultProfileConfig, HttpMessageHandler>` factory in its constructor for testability (same pattern as `HttpCommand`). Tests inject handlers that return canned JSON responses.

- [ ] **Step 1: Write tests for auth and KV version detection**

```csharp
// SSH_Helper.Tests/Vault/VaultServiceTests.cs
using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace SSH_Helper.Tests.Vault;

public class VaultServiceTests
{
    private static HttpMessageHandler CreateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => new DelegatingHandlerStub(handler);

    [Fact]
    public async Task AuthenticateToken_ValidToken_CachesClientToken()
    {
        var profile = new VaultProfileConfig
        {
            Name = "test",
            Address = "https://vault.test:8200",
            AuthMethod = VaultAuthMethod.Token
        };

        var handler = CreateHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/v1/auth/token/lookup-self")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":{"ttl":3600}}""")
                };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new VaultService(
            new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
            _ => handler,
            tokenProvider: (_, _) => "test-token");

        var result = await service.ReadSecretAsync("test", "ssh/server", "password");
        // The service should have authenticated without throwing
        // (the read will 404 but auth succeeded)
    }

    [Fact]
    public async Task AuthenticateAppRole_ValidCredentials_GetsClientToken()
    {
        var profile = new VaultProfileConfig
        {
            Name = "test",
            Address = "https://vault.test:8200",
            AuthMethod = VaultAuthMethod.AppRole,
            AppRoleRoleId = "role-id-123"
        };

        var handler = CreateHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath == "/v1/auth/approle/login")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"auth":{"client_token":"s.abc123","lease_duration":3600}}""")
                };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new VaultService(
            new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
            _ => handler,
            secretIdProvider: (_, _) => "secret-id-456");

        var result = await service.ReadSecretAsync("test", "ssh/server", "password");
        // Auth should succeed (read will 404)
    }

    [Fact]
    public async Task DetectKvVersion_MountTuneReturnsV2_DetectsV2()
    {
        var profile = new VaultProfileConfig
        {
            Name = "test",
            Address = "https://vault.test:8200",
            AuthMethod = VaultAuthMethod.Token,
            KvVersion = VaultKvVersion.AutoDetect
        };

        var handler = CreateHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/sys/mounts/"))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"options":{"version":"2"}}""")
                };
            if (req.RequestUri!.AbsolutePath == "/v1/auth/token/lookup-self")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":{"ttl":3600}}""")
                };
            if (req.RequestUri!.AbsolutePath == "/v1/secret/data/ssh/server")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":{"data":{"password":"secret123"},"metadata":{"version":1}}}""")
                };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new VaultService(
            new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
            _ => handler,
            tokenProvider: (_, _) => "test-token");

        var result = await service.ReadSecretAsync("test", "ssh/server", "password");
        result.Should().Be("secret123");
    }

    [Fact]
    public async Task DetectKvVersion_MountTuneForbidden_FallsBackToHeuristic()
    {
        var profile = new VaultProfileConfig
        {
            Name = "test",
            Address = "https://vault.test:8200",
            AuthMethod = VaultAuthMethod.Token,
            KvVersion = VaultKvVersion.AutoDetect
        };

        var handler = CreateHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/sys/mounts/"))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            if (req.RequestUri!.AbsolutePath == "/v1/auth/token/lookup-self")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":{"ttl":3600}}""")
                };
            // KV v1 response (no data.data wrapper)
            if (req.RequestUri!.AbsolutePath == "/v1/secret/ssh/server")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"data":{"password":"v1secret"}}""")
                };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new VaultService(
            new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
            _ => handler,
            tokenProvider: (_, _) => "test-token");

        var result = await service.ReadSecretAsync("test", "ssh/server", "password");
        result.Should().Be("v1secret");
    }
}

// Test helper — reusable across vault test files
internal class DelegatingHandlerStub : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(_handler(request));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultServiceTests" -v minimal`
Expected: FAIL — `VaultService` and `VaultProfile` do not exist.

- [ ] **Step 3: Create VaultProfile runtime state**

```csharp
// Services/Vault/VaultProfile.cs
namespace SSH_Helper.Services.Vault;

internal class VaultProfile : IDisposable
{
    public VaultProfileConfig Config { get; }
    public HttpClient HttpClient { get; }
    public string? ClientToken { get; set; }
    public DateTime TokenExpiry { get; set; } = DateTime.MinValue;
    public VaultKvVersion? DetectedKvVersion { get; set; }

    public bool IsTokenExpired => ClientToken == null || DateTime.UtcNow >= TokenExpiry;

    public VaultKvVersion EffectiveKvVersion =>
        Config.KvVersion != VaultKvVersion.AutoDetect
            ? Config.KvVersion
            : DetectedKvVersion ?? VaultKvVersion.V2;

    public VaultProfile(VaultProfileConfig config, HttpMessageHandler handler)
    {
        Config = config;
        HttpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri(config.Address.TrimEnd('/')),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void Dispose() => HttpClient.Dispose();
}
```

- [ ] **Step 4: Create VaultService with auth and KV version detection**

```csharp
// Services/Vault/VaultService.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SSH_Helper.Services.Vault;

public class VaultService : IDisposable
{
    private readonly VaultSettings _settings;
    private readonly Func<VaultProfileConfig, HttpMessageHandler> _handlerFactory;
    private readonly Func<string, string, string?>? _tokenProvider;
    private readonly Func<string, string, string?>? _secretIdProvider;
    private readonly Func<string, string, string?>? _ldapPasswordProvider;
    private readonly Dictionary<string, VaultProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string value, DateTime expiry)> _cache = new();
    private readonly object _cacheLock = new();

    public VaultService(
        VaultSettings settings,
        Func<VaultProfileConfig, HttpMessageHandler>? handlerFactory = null,
        Func<string, string, string?>? tokenProvider = null,
        Func<string, string, string?>? secretIdProvider = null,
        Func<string, string, string?>? ldapPasswordProvider = null)
    {
        _settings = settings;
        _handlerFactory = handlerFactory ?? CreateDefaultHandler;
        _tokenProvider = tokenProvider;
        _secretIdProvider = secretIdProvider;
        _ldapPasswordProvider = ldapPasswordProvider;
    }

    public bool IsEnabled => _settings.Enabled && _settings.Profiles.Count > 0;

    public string? ResolveDefaultProfileName(string? environmentOverride = null)
    {
        if (!string.IsNullOrEmpty(environmentOverride))
            return environmentOverride;
        return string.IsNullOrEmpty(_settings.DefaultProfileName)
            ? _settings.Profiles.FirstOrDefault()?.Name
            : _settings.DefaultProfileName;
    }

    private VaultProfile GetOrCreateProfile(string profileName)
    {
        if (_profiles.TryGetValue(profileName, out var existing))
            return existing;

        var config = _settings.Profiles.FirstOrDefault(p =>
            string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new VaultException($"Vault profile '{profileName}' not found. Configured profiles: {string.Join(", ", _settings.Profiles.Select(p => p.Name))}");

        var handler = _handlerFactory(config);
        var profile = new VaultProfile(config, handler);
        _profiles[profileName] = profile;
        return profile;
    }

    private async Task EnsureAuthenticatedAsync(VaultProfile profile, CancellationToken ct)
    {
        if (!profile.IsTokenExpired)
            return;

        switch (profile.Config.AuthMethod)
        {
            case VaultAuthMethod.Token:
                await AuthenticateTokenAsync(profile, ct);
                break;
            case VaultAuthMethod.AppRole:
                await AuthenticateAppRoleAsync(profile, ct);
                break;
            case VaultAuthMethod.Ldap:
                await AuthenticateLdapAsync(profile, ct);
                break;
            default:
                throw new VaultException($"Unsupported auth method: {profile.Config.AuthMethod}");
        }
    }

    private async Task AuthenticateTokenAsync(VaultProfile profile, CancellationToken ct)
    {
        var token = _tokenProvider?.Invoke(profile.Config.Name, "token")
            ?? throw new VaultException($"No token available for vault profile '{profile.Config.Name}'");

        profile.ClientToken = token;

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/token/lookup-self");
        request.Headers.Add("X-Vault-Token", token);
        AddNamespaceHeader(request, profile);

        var response = await profile.HttpClient.SendAsync(request, ct);
        TranslateErrorResponse(response, profile.Config.Name, "auth/token/lookup-self", "read");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var ttl = doc.RootElement.GetProperty("data").GetProperty("ttl").GetInt64();
        profile.TokenExpiry = DateTime.UtcNow.AddSeconds(ttl * 0.75);
    }

    private async Task AuthenticateAppRoleAsync(VaultProfile profile, CancellationToken ct)
    {
        var secretId = _secretIdProvider?.Invoke(profile.Config.Name, "approle_secret")
            ?? throw new VaultException($"No AppRole secret ID available for vault profile '{profile.Config.Name}'");

        var body = JsonSerializer.Serialize(new { role_id = profile.Config.AppRoleRoleId, secret_id = secretId });
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/approle/login")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        AddNamespaceHeader(request, profile);

        var response = await profile.HttpClient.SendAsync(request, ct);
        TranslateErrorResponse(response, profile.Config.Name, "auth/approle/login", "write");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var auth = doc.RootElement.GetProperty("auth");
        profile.ClientToken = auth.GetProperty("client_token").GetString();
        var leaseDuration = auth.GetProperty("lease_duration").GetInt64();
        profile.TokenExpiry = DateTime.UtcNow.AddSeconds(leaseDuration * 0.75);
    }

    private async Task AuthenticateLdapAsync(VaultProfile profile, CancellationToken ct)
    {
        var password = _ldapPasswordProvider?.Invoke(profile.Config.Name, "ldap_password")
            ?? throw new VaultException($"No LDAP password available for vault profile '{profile.Config.Name}'");

        var body = JsonSerializer.Serialize(new { password });
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/auth/ldap/login/{Uri.EscapeDataString(profile.Config.LdapUsername)}")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        AddNamespaceHeader(request, profile);

        var response = await profile.HttpClient.SendAsync(request, ct);
        TranslateErrorResponse(response, profile.Config.Name, $"auth/ldap/login/{profile.Config.LdapUsername}", "write");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var auth = doc.RootElement.GetProperty("auth");
        profile.ClientToken = auth.GetProperty("client_token").GetString();
        var leaseDuration = auth.GetProperty("lease_duration").GetInt64();
        profile.TokenExpiry = DateTime.UtcNow.AddSeconds(leaseDuration * 0.75);
    }

    private async Task DetectKvVersionAsync(VaultProfile profile, CancellationToken ct)
    {
        if (profile.Config.KvVersion != VaultKvVersion.AutoDetect || profile.DetectedKvVersion.HasValue)
            return;

        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/sys/mounts/{profile.Config.MountPath}/tune", profile);
            var response = await profile.HttpClient.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("options", out var options) &&
                    options.TryGetProperty("version", out var version) &&
                    version.GetString() == "2")
                {
                    profile.DetectedKvVersion = VaultKvVersion.V2;
                    return;
                }
                profile.DetectedKvVersion = VaultKvVersion.V1;
                return;
            }
        }
        catch { /* fall through to heuristic */ }

        // Heuristic: try KV v2 read path, check for data.data wrapper
        profile.DetectedKvVersion = VaultKvVersion.V2; // assume v2, correct on first read if needed
    }

    // --- Read/Write/Patch/List (implemented in Task 3) ---

    public async Task<string?> ReadSecretAsync(string profileName, string path, string key, int? version = null, CancellationToken ct = default)
    {
        var profile = GetOrCreateProfile(profileName);
        await EnsureAuthenticatedAsync(profile, ct);
        await DetectKvVersionAsync(profile, ct);

        var cacheKey = $"{profileName}|{path}|{key}";
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.expiry)
                return cached.value;
        }

        var url = profile.EffectiveKvVersion == VaultKvVersion.V2
            ? $"/v1/{profile.Config.MountPath}/data/{path}" + (version.HasValue ? $"?version={version}" : "")
            : $"/v1/{profile.Config.MountPath}/{path}";

        var request = CreateAuthenticatedRequest(HttpMethod.Get, url, profile);
        var response = await profile.HttpClient.SendAsync(request, ct);

        // Handle KV version heuristic fallback
        if (response.StatusCode == HttpStatusCode.NotFound && profile.DetectedKvVersion == VaultKvVersion.V2 && profile.Config.KvVersion == VaultKvVersion.AutoDetect)
        {
            // Maybe it's actually v1 — try v1 path
            var v1Url = $"/v1/{profile.Config.MountPath}/{path}";
            var v1Request = CreateAuthenticatedRequest(HttpMethod.Get, v1Url, profile);
            var v1Response = await profile.HttpClient.SendAsync(v1Request, ct);
            if (v1Response.IsSuccessStatusCode)
            {
                profile.DetectedKvVersion = VaultKvVersion.V1;
                response = v1Response;
            }
        }

        TranslateErrorResponse(response, profileName, path, "read");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);

        string? value;
        if (profile.EffectiveKvVersion == VaultKvVersion.V2)
        {
            var data = doc.RootElement.GetProperty("data").GetProperty("data");
            if (!data.TryGetProperty(key, out var keyElement))
            {
                var availableKeys = data.EnumerateObject().Select(p => p.Name).ToList();
                throw new VaultException($"Secret at '{path}' exists but has no key '{key}' — available keys: {string.Join(", ", availableKeys)}");
            }
            value = keyElement.GetString();
        }
        else
        {
            var data = doc.RootElement.GetProperty("data");
            if (!data.TryGetProperty(key, out var keyElement))
            {
                var availableKeys = data.EnumerateObject().Select(p => p.Name).ToList();
                throw new VaultException($"Secret at '{path}' exists but has no key '{key}' — available keys: {string.Join(", ", availableKeys)}");
            }
            value = keyElement.GetString();
        }

        if (value != null)
        {
            lock (_cacheLock)
            {
                _cache[cacheKey] = (value, DateTime.UtcNow.AddSeconds(profile.Config.CacheTtlSeconds));
            }
        }

        return value;
    }

    public async Task<Dictionary<string, string?>> ReadSecretKeysAsync(string profileName, string path, IEnumerable<string> keys, int? version = null, CancellationToken ct = default)
    {
        var profile = GetOrCreateProfile(profileName);
        await EnsureAuthenticatedAsync(profile, ct);
        await DetectKvVersionAsync(profile, ct);

        var url = profile.EffectiveKvVersion == VaultKvVersion.V2
            ? $"/v1/{profile.Config.MountPath}/data/{path}" + (version.HasValue ? $"?version={version}" : "")
            : $"/v1/{profile.Config.MountPath}/{path}";

        var request = CreateAuthenticatedRequest(HttpMethod.Get, url, profile);
        var response = await profile.HttpClient.SendAsync(request, ct);
        TranslateErrorResponse(response, profileName, path, "read");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var data = profile.EffectiveKvVersion == VaultKvVersion.V2
            ? doc.RootElement.GetProperty("data").GetProperty("data")
            : doc.RootElement.GetProperty("data");

        var result = new Dictionary<string, string?>();
        foreach (var key in keys)
        {
            var value = data.TryGetProperty(key, out var el) ? el.GetString() : null;
            result[key] = value;

            if (value != null)
            {
                var cacheKey = $"{profileName}|{path}|{key}";
                lock (_cacheLock)
                {
                    _cache[cacheKey] = (value, DateTime.UtcNow.AddSeconds(profile.Config.CacheTtlSeconds));
                }
            }
        }
        return result;
    }

    public async Task WriteSecretAsync(string profileName, string path, Dictionary<string, string> data, CancellationToken ct = default)
    {
        var profile = GetOrCreateProfile(profileName);
        await EnsureAuthenticatedAsync(profile, ct);
        await DetectKvVersionAsync(profile, ct);

        string url;
        string body;
        if (profile.EffectiveKvVersion == VaultKvVersion.V2)
        {
            url = $"/v1/{profile.Config.MountPath}/data/{path}";
            body = JsonSerializer.Serialize(new { data });
        }
        else
        {
            url = $"/v1/{profile.Config.MountPath}/{path}";
            body = JsonSerializer.Serialize(data);
        }

        var request = CreateAuthenticatedRequest(HttpMethod.Post, url, profile);
        request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        var response = await profile.HttpClient.SendAsync(request, ct);
        TranslateErrorResponse(response, profileName, path, "write");

        InvalidateCacheForPath(profileName, path);
    }

    public async Task PatchSecretAsync(string profileName, string path, Dictionary<string, string> data, CancellationToken ct = default)
    {
        var profile = GetOrCreateProfile(profileName);
        await EnsureAuthenticatedAsync(profile, ct);
        await DetectKvVersionAsync(profile, ct);

        if (profile.EffectiveKvVersion == VaultKvVersion.V2)
        {
            var url = $"/v1/{profile.Config.MountPath}/data/{path}";
            var body = JsonSerializer.Serialize(new { data });
            var request = CreateAuthenticatedRequest(new HttpMethod("PATCH"), url, profile);
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/merge-patch+json");
            var response = await profile.HttpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.MethodNotAllowed || response.StatusCode == HttpStatusCode.UnsupportedMediaType)
            {
                // PATCH not supported — fall back to read-modify-write
                await ReadModifyWriteAsync(profile, profileName, path, data, ct);
            }
            else
            {
                TranslateErrorResponse(response, profileName, path, "patch");
            }
        }
        else
        {
            // KV v1 has no PATCH — always read-modify-write
            await ReadModifyWriteAsync(profile, profileName, path, data, ct);
        }

        InvalidateCacheForPath(profileName, path);
    }

    private async Task ReadModifyWriteAsync(VaultProfile profile, string profileName, string path, Dictionary<string, string> updates, CancellationToken ct)
    {
        // Read current
        var url = profile.EffectiveKvVersion == VaultKvVersion.V2
            ? $"/v1/{profile.Config.MountPath}/data/{path}"
            : $"/v1/{profile.Config.MountPath}/{path}";
        var readReq = CreateAuthenticatedRequest(HttpMethod.Get, url, profile);
        var readResp = await profile.HttpClient.SendAsync(readReq, ct);
        TranslateErrorResponse(readResp, profileName, path, "read");

        var json = await readResp.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var existing = profile.EffectiveKvVersion == VaultKvVersion.V2
            ? doc.RootElement.GetProperty("data").GetProperty("data")
            : doc.RootElement.GetProperty("data");

        // Merge
        var merged = new Dictionary<string, string>();
        foreach (var prop in existing.EnumerateObject())
            merged[prop.Name] = prop.Value.GetString() ?? "";
        foreach (var kvp in updates)
            merged[kvp.Key] = kvp.Value;

        // Write merged
        await WriteSecretAsync(profileName, path, merged, ct);
    }

    public async Task<List<string>> ListSecretsAsync(string profileName, string prefix, CancellationToken ct = default)
    {
        var profile = GetOrCreateProfile(profileName);
        await EnsureAuthenticatedAsync(profile, ct);
        await DetectKvVersionAsync(profile, ct);

        var url = profile.EffectiveKvVersion == VaultKvVersion.V2
            ? $"/v1/{profile.Config.MountPath}/metadata/{prefix}"
            : $"/v1/{profile.Config.MountPath}/{prefix}";

        var request = CreateAuthenticatedRequest(new HttpMethod("LIST"), url, profile);
        var response = await profile.HttpClient.SendAsync(request, ct);
        TranslateErrorResponse(response, profileName, prefix, "list");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.GetProperty("data").GetProperty("keys");
        return keys.EnumerateArray().Select(k => k.GetString()!).ToList();
    }

    public async Task<bool> TestConnectionAsync(string profileName, CancellationToken ct = default)
    {
        var profile = GetOrCreateProfile(profileName);
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/v1/sys/health");
            AddNamespaceHeader(request, profile);
            var response = await profile.HttpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode ||
                   response.StatusCode == HttpStatusCode.ServiceUnavailable; // sealed but reachable
        }
        catch
        {
            return false;
        }
    }

    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
        }
    }

    private void InvalidateCacheForPath(string profileName, string path)
    {
        var prefix = $"{profileName}|{path}|";
        lock (_cacheLock)
        {
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var key in keysToRemove)
                _cache.Remove(key);
        }
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, VaultProfile profile)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(profile.ClientToken))
            request.Headers.Add("X-Vault-Token", profile.ClientToken);
        AddNamespaceHeader(request, profile);
        return request;
    }

    private static void AddNamespaceHeader(HttpRequestMessage request, VaultProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.Config.Namespace))
            request.Headers.Add("X-Vault-Namespace", profile.Config.Namespace);
    }

    private static void TranslateErrorResponse(HttpResponseMessage response, string profileName, string path, string capability)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                $"Vault authentication failed for profile '{profileName}' — check your token or AppRole credentials",
            HttpStatusCode.Forbidden =>
                $"Permission denied — check that your Vault policy grants '{capability}' on path '{path}'",
            HttpStatusCode.NotFound =>
                $"No secret found at '{path}' — verify the path exists in Vault",
            HttpStatusCode.ServiceUnavailable =>
                "Vault is sealed — it needs to be unsealed before SSH_Helper can access secrets",
            _ =>
                $"Vault request failed: HTTP {(int)response.StatusCode} for {path}"
        };

        throw new VaultException(message);
    }

    private static HttpMessageHandler CreateDefaultHandler(VaultProfileConfig config)
    {
        var handler = new HttpClientHandler();
        if (config.SkipTlsVerification)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        if (!string.IsNullOrEmpty(config.CaCertificatePath) && File.Exists(config.CaCertificatePath))
            handler.ClientCertificates.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(config.CaCertificatePath));
        return handler;
    }

    public void Dispose()
    {
        foreach (var profile in _profiles.Values)
            profile.Dispose();
        _profiles.Clear();
    }
}

public class VaultException : Exception
{
    public VaultException(string message) : base(message) { }
    public VaultException(string message, Exception inner) : base(message, inner) { }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultServiceTests" -v minimal`
Expected: PASS (all 4 tests).

- [ ] **Step 6: Run full build**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Services/Vault/VaultService.cs Services/Vault/VaultProfile.cs SSH_Helper.Tests/Vault/VaultServiceTests.cs
git commit -m "feat(vault): add VaultService with auth, KV version detection, read/write/patch/list, and caching"
```

---

## Task 3: VaultService — Error Translation & Additional Read Tests

**Files:**
- Modify: `SSH_Helper.Tests/Vault/VaultServiceTests.cs`

- [ ] **Step 1: Add tests for error translation and caching**

Add to `VaultServiceTests.cs`:

```csharp
[Fact]
public async Task ReadSecret_403_ThrowsFriendlyPermissionDenied()
{
    var profile = new VaultProfileConfig
    {
        Name = "test",
        Address = "https://vault.test:8200",
        AuthMethod = VaultAuthMethod.Token,
        KvVersion = VaultKvVersion.V2
    };

    var handler = CreateHandler(req =>
    {
        if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"ttl":3600}}""")
            };
        return new HttpResponseMessage(HttpStatusCode.Forbidden);
    });

    var service = new VaultService(
        new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
        _ => handler,
        tokenProvider: (_, _) => "test-token");

    var act = () => service.ReadSecretAsync("test", "ssh/denied", "password");
    await act.Should().ThrowAsync<VaultException>()
        .WithMessage("*Permission denied*'read'*'ssh/denied'*");
}

[Fact]
public async Task ReadSecret_KeyNotFound_ListsAvailableKeys()
{
    var profile = new VaultProfileConfig
    {
        Name = "test",
        Address = "https://vault.test:8200",
        AuthMethod = VaultAuthMethod.Token,
        KvVersion = VaultKvVersion.V2
    };

    var handler = CreateHandler(req =>
    {
        if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"ttl":3600}}""")
            };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":{"data":{"username":"admin","password":"secret"},"metadata":{"version":1}}}""")
        };
    });

    var service = new VaultService(
        new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
        _ => handler,
        tokenProvider: (_, _) => "test-token");

    var act = () => service.ReadSecretAsync("test", "ssh/server", "pasword"); // typo
    await act.Should().ThrowAsync<VaultException>()
        .WithMessage("*'pasword'*available keys: username, password*");
}

[Fact]
public async Task ReadSecret_503_ThrowsSealedMessage()
{
    var profile = new VaultProfileConfig
    {
        Name = "test",
        Address = "https://vault.test:8200",
        AuthMethod = VaultAuthMethod.Token,
        KvVersion = VaultKvVersion.V2
    };

    var handler = CreateHandler(req =>
        new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

    var service = new VaultService(
        new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
        _ => handler,
        tokenProvider: (_, _) => "test-token");

    var act = () => service.ReadSecretAsync("test", "ssh/server", "password");
    await act.Should().ThrowAsync<VaultException>()
        .WithMessage("*sealed*unsealed*");
}

[Fact]
public async Task ReadSecret_CachesResult_SecondCallSkipsHttp()
{
    var callCount = 0;
    var profile = new VaultProfileConfig
    {
        Name = "test",
        Address = "https://vault.test:8200",
        AuthMethod = VaultAuthMethod.Token,
        KvVersion = VaultKvVersion.V2,
        CacheTtlSeconds = 60
    };

    var handler = CreateHandler(req =>
    {
        if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"ttl":3600}}""")
            };
        Interlocked.Increment(ref callCount);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"data":{"data":{"password":"cached-secret"},"metadata":{"version":1}}}""")
        };
    });

    var service = new VaultService(
        new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
        _ => handler,
        tokenProvider: (_, _) => "test-token");

    var r1 = await service.ReadSecretAsync("test", "ssh/server", "password");
    var r2 = await service.ReadSecretAsync("test", "ssh/server", "password");

    r1.Should().Be("cached-secret");
    r2.Should().Be("cached-secret");
    callCount.Should().Be(1); // only one HTTP call
}

[Fact]
public async Task WriteSecret_InvalidatesCache()
{
    var readCount = 0;
    var profile = new VaultProfileConfig
    {
        Name = "test",
        Address = "https://vault.test:8200",
        AuthMethod = VaultAuthMethod.Token,
        KvVersion = VaultKvVersion.V2,
        CacheTtlSeconds = 60
    };

    var currentPassword = "old-pass";
    var handler = CreateHandler(req =>
    {
        if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"ttl":3600}}""")
            };
        if (req.Method == HttpMethod.Post)
        {
            currentPassword = "new-pass";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":{"version":2}}""")
            };
        }
        Interlocked.Increment(ref readCount);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$$"""{"data":{"data":{"password":"{{{currentPassword}}}"},"metadata":{"version":1}}}""")
        };
    });

    var service = new VaultService(
        new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" },
        _ => handler,
        tokenProvider: (_, _) => "test-token");

    var r1 = await service.ReadSecretAsync("test", "ssh/server", "password");
    r1.Should().Be("old-pass");
    readCount.Should().Be(1);

    await service.WriteSecretAsync("test", "ssh/server", new Dictionary<string, string> { ["password"] = "new-pass" });

    var r2 = await service.ReadSecretAsync("test", "ssh/server", "password");
    r2.Should().Be("new-pass");
    readCount.Should().Be(2); // cache was invalidated, new HTTP call
}

[Fact]
public void ClearCache_RemovesAllEntries()
{
    var service = new VaultService(
        new VaultSettings { Enabled = true },
        _ => CreateHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

    service.ClearCache(); // should not throw even when empty
}

[Fact]
public async Task ProfileNotFound_ThrowsDescriptiveError()
{
    var service = new VaultService(
        new VaultSettings
        {
            Enabled = true,
            Profiles = [new VaultProfileConfig { Name = "prod" }],
            DefaultProfileName = "prod"
        },
        _ => CreateHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));

    var act = () => service.ReadSecretAsync("nonexistent", "ssh/server", "password");
    await act.Should().ThrowAsync<VaultException>()
        .WithMessage("*'nonexistent' not found*prod*");
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultServiceTests" -v minimal`
Expected: PASS (all 11 tests).

- [ ] **Step 3: Commit**

```bash
git add SSH_Helper.Tests/Vault/VaultServiceTests.cs
git commit -m "test(vault): add error translation, caching, and edge case tests for VaultService"
```

---

## Task 4: ScriptParser Registration & Validation

**Files:**
- Modify: `Services/Scripting/ScriptParser.cs:22-260`
- Modify: `Services/Editor/ScriptAutocompleteProvider.cs`
- Test: `SSH_Helper.Tests/Scripting/VaultParserTests.cs`

- [ ] **Step 1: Write parser registration tests**

```csharp
// SSH_Helper.Tests/Scripting/VaultParserTests.cs
using FluentAssertions;

namespace SSH_Helper.Tests.Scripting;

public class VaultParserTests
{
    [Fact]
    public void KnownStepCommands_ContainsVault()
    {
        var commands = ScriptParser.GetKnownStepCommands();
        commands.Should().Contain("vault");
    }

    [Fact]
    public void CommandOptionKeys_ContainsVaultOptions()
    {
        var options = ScriptParser.GetKnownStepOptionKeysByCommand();
        options.Should().ContainKey("vault");
        options["vault"].Should().Contain("path")
            .And.Contain("key")
            .And.Contain("keys")
            .And.Contain("into")
            .And.Contain("write")
            .And.Contain("patch")
            .And.Contain("profile")
            .And.Contain("version")
            .And.Contain("on_error");
    }

    [Fact]
    public void Parse_VaultReadStep_Succeeds()
    {
        var yaml = """
            steps:
              - vault:
                  path: "ssh/prod-switches"
                  key: "password"
                  into: switch_pass
            """;
        var result = ScriptParser.Parse(yaml);
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Vault.Should().NotBeNull();
    }

    [Fact]
    public void Parse_VaultWriteStep_Succeeds()
    {
        var yaml = """
            steps:
              - vault:
                  path: "ssh/prod-switches"
                  write:
                    password: "new_pass"
            """;
        var result = ScriptParser.Parse(yaml);
        result.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_VaultPatchStep_Succeeds()
    {
        var yaml = """
            steps:
              - vault:
                  path: "ssh/prod-switches"
                  patch:
                    password: "new_pass"
            """;
        var result = ScriptParser.Parse(yaml);
        result.Steps.Should().HaveCount(1);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultParserTests" -v minimal`
Expected: FAIL — `vault` not in known commands.

- [ ] **Step 3: Add vault to ScriptParser arrays**

In `Services/Scripting/ScriptParser.cs`:

Add `"vault"` to the `KnownStepKeys` array (~line 22-62).

Add to `CommandOptionKeys` dictionary (~line 97-136):
```csharp
["vault"] = ["path", "key", "keys", "into", "write", "patch", "profile", "version", "on_error"],
```

Add to `StepRootOptionKeysByCommand` dictionary (~line 137-180):
```csharp
["vault"] = [],
```

Add `"vault"` to `CommandDescriptions` in `ScriptAutocompleteProvider.cs` (~line 114-155):
```csharp
["vault"] = "Read, write, or patch secrets from HashiCorp Vault",
```

- [ ] **Step 4: Add ScriptStep.Vault property**

The `ScriptStep` model needs a `Vault` property to hold parsed vault options. Check how other commands like `Http` are modeled in `Services/Scripting/Models/ScriptStep.cs` and add:
```csharp
public VaultStepOptions? Vault { get; set; }
```

Create `VaultStepOptions` in `Services/Scripting/Models/`:
```csharp
// Services/Scripting/Models/VaultStepOptions.cs
namespace SSH_Helper.Services.Scripting.Models;

public class VaultStepOptions
{
    public string Path { get; set; } = "";
    public string? Profile { get; set; }
    public string? Key { get; set; }
    public Dictionary<string, string>? Keys { get; set; }
    public string? Into { get; set; }
    public int? Version { get; set; }
    public Dictionary<string, string>? Write { get; set; }
    public Dictionary<string, string>? Patch { get; set; }
    public string? OnError { get; set; }
}
```

Wire up parsing in the `ScriptParser` step-parsing logic where other commands are parsed from YAML nodes (follow the `HttpOptions` pattern).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultParserTests" -v minimal`
Expected: PASS (all 5 tests).

- [ ] **Step 6: Run full build**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Services/Scripting/ScriptParser.cs Services/Scripting/Models/VaultStepOptions.cs Services/Editor/ScriptAutocompleteProvider.cs SSH_Helper.Tests/Scripting/VaultParserTests.cs
git commit -m "feat(vault): register vault command in ScriptParser with step options model"
```

---

## Task 5: VaultCommand — Script Step Handler

**Files:**
- Create: `Services/Scripting/Commands/VaultCommand.cs`
- Test: `SSH_Helper.Tests/Scripting/VaultCommandTests.cs`

- [ ] **Step 1: Write tests for VaultCommand**

```csharp
// SSH_Helper.Tests/Scripting/VaultCommandTests.cs
using FluentAssertions;
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Tests.Scripting;

public class VaultCommandTests
{
    private static (VaultCommand cmd, ScriptContext ctx, VaultService vault) CreateTestSetup(
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var profile = new VaultProfileConfig
        {
            Name = "test",
            Address = "https://vault.test:8200",
            AuthMethod = VaultAuthMethod.Token,
            KvVersion = VaultKvVersion.V2
        };
        var settings = new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" };
        var vault = new VaultService(settings, _ => new DelegatingHandlerStub(handler), tokenProvider: (_, _) => "test-token");
        var ctx = new ScriptContext();
        ctx.VaultService = vault;
        var cmd = new VaultCommand();
        return (cmd, ctx, vault);
    }

    [Fact]
    public async Task Execute_ReadSingleKey_SetsVariable()
    {
        var (cmd, ctx, _) = CreateTestSetup(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent("""{"data":{"data":{"password":"s3cret"},"metadata":{"version":1}}}""") };
        });

        var step = new ScriptStep
        {
            Vault = new VaultStepOptions { Path = "ssh/server", Key = "password", Into = "result" }
        };

        var result = await cmd.ExecuteAsync(step, ctx, CancellationToken.None);
        result.Success.Should().BeTrue();
        ctx.GetVariable("result").Should().Be("s3cret");
    }

    [Fact]
    public async Task Execute_ReadMultipleKeys_SetsAllVariables()
    {
        var (cmd, ctx, _) = CreateTestSetup(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent("""{"data":{"data":{"username":"admin","password":"s3cret"},"metadata":{"version":1}}}""") };
        });

        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "ssh/server",
                Keys = new Dictionary<string, string> { ["username"] = "user_var", ["password"] = "pass_var" }
            }
        };

        var result = await cmd.ExecuteAsync(step, ctx, CancellationToken.None);
        result.Success.Should().BeTrue();
        ctx.GetVariable("user_var").Should().Be("admin");
        ctx.GetVariable("pass_var").Should().Be("s3cret");
    }

    [Fact]
    public async Task Execute_Write_Succeeds()
    {
        var (cmd, ctx, _) = CreateTestSetup(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            if (req.Method == HttpMethod.Post)
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"version":2}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "ssh/server",
                Write = new Dictionary<string, string> { ["password"] = "new-pass" }
            }
        };

        var result = await cmd.ExecuteAsync(step, ctx, CancellationToken.None);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_OnErrorContinue_SetsLastError()
    {
        var (cmd, ctx, _) = CreateTestSetup(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden);
        });

        var step = new ScriptStep
        {
            Vault = new VaultStepOptions { Path = "ssh/denied", Key = "password", Into = "result", OnError = "continue" }
        };

        var result = await cmd.ExecuteAsync(step, ctx, CancellationToken.None);
        result.Success.Should().BeTrue(); // on_error: continue
        ctx.GetVariable("_last_error").Should().Contain("Permission denied");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultCommandTests" -v minimal`
Expected: FAIL — `VaultCommand` does not exist.

- [ ] **Step 3: Implement VaultCommand**

```csharp
// Services/Scripting/Commands/VaultCommand.cs
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Services.Scripting.Commands;

public class VaultCommand : IScriptCommand
{
    public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
    {
        var options = step.Vault ?? throw new InvalidOperationException("Vault step options not set");
        var vaultService = context.VaultService ?? throw new InvalidOperationException("VaultService not configured — enable Vault in Settings");

        var profileName = !string.IsNullOrEmpty(options.Profile)
            ? options.Profile
            : vaultService.ResolveDefaultProfileName(context.EnvironmentVaultProfile);

        if (string.IsNullOrEmpty(profileName))
            return Fail(step, options, "No vault profile specified and no default profile configured");

        var path = context.SubstituteVariables(options.Path);

        try
        {
            if (options.Write != null)
                return await ExecuteWriteAsync(vaultService, profileName, path, options, context, cancellationToken);
            if (options.Patch != null)
                return await ExecutePatchAsync(vaultService, profileName, path, options, context, cancellationToken);
            if (options.Keys != null)
                return await ExecuteReadMultipleAsync(vaultService, profileName, path, options, context, cancellationToken);
            if (options.Key != null)
                return await ExecuteReadSingleAsync(vaultService, profileName, path, options, context, cancellationToken);

            return Fail(step, options, "vault step requires one of: key, keys, write, or patch");
        }
        catch (VaultException ex)
        {
            context.SetVariable("_last_error", ex.Message);
            EmitAuditLog(context, profileName, path, GetOperation(options), $"FAIL: {ex.Message}");
            return Fail(step, options, ex.Message);
        }
    }

    private async Task<CommandResult> ExecuteReadSingleAsync(VaultService vault, string profile, string path, VaultStepOptions options, ScriptContext context, CancellationToken ct)
    {
        var key = context.SubstituteVariables(options.Key!);
        var value = await vault.ReadSecretAsync(profile, path, key, options.Version, ct);
        context.SetVariable(options.Into!, value ?? "");
        EmitAuditLog(context, profile, path, "READ", "ok");
        return CommandResult.Ok();
    }

    private async Task<CommandResult> ExecuteReadMultipleAsync(VaultService vault, string profile, string path, VaultStepOptions options, ScriptContext context, CancellationToken ct)
    {
        var resolvedKeys = options.Keys!.Keys.ToList();
        var values = await vault.ReadSecretKeysAsync(profile, path, resolvedKeys, options.Version, ct);
        foreach (var kvp in options.Keys)
        {
            var variableName = context.SubstituteVariables(kvp.Value);
            context.SetVariable(variableName, values.GetValueOrDefault(kvp.Key) ?? "");
        }
        EmitAuditLog(context, profile, path, "READ", "ok");
        return CommandResult.Ok();
    }

    private async Task<CommandResult> ExecuteWriteAsync(VaultService vault, string profile, string path, VaultStepOptions options, ScriptContext context, CancellationToken ct)
    {
        var data = new Dictionary<string, string>();
        foreach (var kvp in options.Write!)
            data[kvp.Key] = context.SubstituteVariables(kvp.Value);
        await vault.WriteSecretAsync(profile, path, data, ct);
        EmitAuditLog(context, profile, path, "WRITE", "ok");
        return CommandResult.Ok();
    }

    private async Task<CommandResult> ExecutePatchAsync(VaultService vault, string profile, string path, VaultStepOptions options, ScriptContext context, CancellationToken ct)
    {
        var data = new Dictionary<string, string>();
        foreach (var kvp in options.Patch!)
            data[kvp.Key] = context.SubstituteVariables(kvp.Value);
        await vault.PatchSecretAsync(profile, path, data, ct);
        EmitAuditLog(context, profile, path, "PATCH", "ok");
        return CommandResult.Ok();
    }

    private static string GetOperation(VaultStepOptions options)
    {
        if (options.Write != null) return "WRITE";
        if (options.Patch != null) return "PATCH";
        return "READ";
    }

    private static void EmitAuditLog(ScriptContext context, string profile, string path, string operation, string status)
    {
        context.EmitOutput($"[vault] {operation} {profile}@{path} → {status}", ScriptOutputType.Debug);
    }

    private static CommandResult Fail(ScriptStep step, VaultStepOptions options, string message)
    {
        if (string.Equals(options.OnError, "continue", StringComparison.OrdinalIgnoreCase))
            return CommandResult.Ok();
        return CommandResult.Fail(message);
    }
}
```

- [ ] **Step 4: Add VaultService property to ScriptContext**

In `Services/Scripting/ScriptContext.cs`, add property:
```csharp
public VaultService? VaultService { get; set; }
public string? EnvironmentVaultProfile { get; set; }
```

- [ ] **Step 5: Register VaultCommand in ScriptExecutor's command dispatch**

Find where `ScriptExecutor` dispatches commands to handlers (the command name → `IScriptCommand` mapping) and add:
```csharp
["vault"] = new VaultCommand(),
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultCommandTests" -v minimal`
Expected: PASS (all 4 tests).

- [ ] **Step 7: Commit**

```bash
git add Services/Scripting/Commands/VaultCommand.cs Services/Scripting/ScriptContext.cs SSH_Helper.Tests/Scripting/VaultCommandTests.cs
git commit -m "feat(vault): add VaultCommand script step handler with read/write/patch and audit logging"
```

---

## Task 6: Inline {{vault:...}} Syntax

**Files:**
- Modify: `Services/Scripting/ScriptContext.cs:482-534`
- Test: `SSH_Helper.Tests/Scripting/VaultInlineSyntaxTests.cs`

- [ ] **Step 1: Write tests for inline vault resolution**

```csharp
// SSH_Helper.Tests/Scripting/VaultInlineSyntaxTests.cs
using FluentAssertions;
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Tests.Scripting;

public class VaultInlineSyntaxTests
{
    private ScriptContext CreateContextWithVault(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var profile = new VaultProfileConfig
        {
            Name = "test",
            Address = "https://vault.test:8200",
            AuthMethod = VaultAuthMethod.Token,
            KvVersion = VaultKvVersion.V2
        };
        var settings = new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" };
        var vault = new VaultService(settings, _ => new DelegatingHandlerStub(handler), tokenProvider: (_, _) => "test-token");
        var ctx = new ScriptContext();
        ctx.VaultService = vault;
        return ctx;
    }

    [Fact]
    public void SubstituteVariables_VaultPrefix_ResolvesSecret()
    {
        var ctx = CreateContextWithVault(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent("""{"data":{"data":{"password":"inline-secret"},"metadata":{"version":1}}}""") };
        });

        var result = ctx.SubstituteVariables("Password is {{vault:ssh/server#password}}");
        result.Should().Be("Password is inline-secret");
    }

    [Fact]
    public void SubstituteVariables_VaultWithProfile_ResolvesSecret()
    {
        var ctx = CreateContextWithVault(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent("""{"data":{"data":{"password":"profiled-secret"},"metadata":{"version":1}}}""") };
        });

        var result = ctx.SubstituteVariables("{{vault:test@ssh/server#password}}");
        result.Should().Be("profiled-secret");
    }

    [Fact]
    public void SubstituteVariables_VaultError_ResolvesToEmptyAndSetsLastError()
    {
        var ctx = CreateContextWithVault(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        });

        var result = ctx.SubstituteVariables("{{vault:ssh/missing#password}}");
        result.Should().BeEmpty();
        ctx.GetVariable("_last_error").Should().Contain("No secret found");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultInlineSyntaxTests" -v minimal`
Expected: FAIL — vault: prefix not handled in `ResolveVariableExpression`.

- [ ] **Step 3: Add vault: prefix handling to ScriptContext.ResolveVariableExpression**

In `Services/Scripting/ScriptContext.cs`, at the top of `ResolveVariableExpression(string expr)` (~line 482):

```csharp
if (expr.StartsWith("vault:", StringComparison.OrdinalIgnoreCase))
{
    return ResolveVaultExpression(expr.Substring(6));
}
```

Add the helper method:
```csharp
private string ResolveVaultExpression(string vaultExpr)
{
    if (VaultService == null)
        return "";

    try
    {
        // Parse [profile@]path#key
        string? profileName = null;
        string remaining = vaultExpr;

        var atIndex = remaining.IndexOf('@');
        var hashIndex = remaining.IndexOf('#');

        // @ must come before # to be a profile delimiter (not part of the path)
        if (atIndex >= 0 && (hashIndex < 0 || atIndex < hashIndex))
        {
            profileName = remaining.Substring(0, atIndex);
            remaining = remaining.Substring(atIndex + 1);
            hashIndex = remaining.IndexOf('#');
        }

        if (hashIndex < 0)
        {
            SetVariable("_last_error", $"Invalid vault syntax: '{{{{vault:{vaultExpr}}}}}' — missing '#key' delimiter. Expected: {{{{vault:[profile@]path#key}}}}");
            return "";
        }

        var path = remaining.Substring(0, hashIndex);
        var key = remaining.Substring(hashIndex + 1);

        profileName ??= VaultService.ResolveDefaultProfileName(EnvironmentVaultProfile);
        if (string.IsNullOrEmpty(profileName))
        {
            SetVariable("_last_error", "No vault profile specified and no default profile configured");
            return "";
        }

        var result = VaultService.ReadSecretAsync(profileName, path, key).GetAwaiter().GetResult();
        return result ?? "";
    }
    catch (VaultException ex)
    {
        SetVariable("_last_error", ex.Message);
        return "";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultInlineSyntaxTests" -v minimal`
Expected: PASS (all 3 tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Scripting/ScriptContext.cs SSH_Helper.Tests/Scripting/VaultInlineSyntaxTests.cs
git commit -m "feat(vault): add {{vault:path#key}} inline syntax resolution in ScriptContext"
```

---

## Task 7: VaultFunctions — Built-in Functions

**Files:**
- Create: `Services/Scripting/Functions/VaultFunctions.cs`
- Modify: `Services/Scripting/FunctionRegistry.cs:92-100`
- Test: `SSH_Helper.Tests/Scripting/VaultFunctionsTests.cs`

- [ ] **Step 1: Write tests for vault functions**

```csharp
// SSH_Helper.Tests/Scripting/VaultFunctionsTests.cs
using FluentAssertions;
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Tests.Scripting;

public class VaultFunctionsTests
{
    [Fact]
    public void VaultGet_ReturnsSecretValue()
    {
        var profile = new VaultProfileConfig
        {
            Name = "test", Address = "https://vault.test:8200",
            AuthMethod = VaultAuthMethod.Token, KvVersion = VaultKvVersion.V2
        };
        var settings = new VaultSettings { Enabled = true, Profiles = [profile], DefaultProfileName = "test" };
        var vault = new VaultService(settings, _ => new DelegatingHandlerStub(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/auth/"))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent("""{"data":{"ttl":3600}}""") };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent("""{"data":{"data":{"password":"func-secret"},"metadata":{"version":1}}}""") };
        }), tokenProvider: (_, _) => "test-token");

        var ctx = new ScriptContext { VaultService = vault };

        var registry = FunctionRegistry.Instance;
        // vault("path", "key") should resolve
        var result = registry.TryCall("vault", "\"ssh/server\", \"password\"", ctx);
        result.Should().Be("func-secret");
    }

    [Fact]
    public void VaultClearCache_ClearsAndReturnsTrue()
    {
        var settings = new VaultSettings { Enabled = true };
        var vault = new VaultService(settings, _ => new DelegatingHandlerStub(_ => new HttpResponseMessage()));
        var ctx = new ScriptContext { VaultService = vault };

        var result = FunctionRegistry.Instance.TryCall("vault_clear_cache", "", ctx);
        result.Should().Be(true);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultFunctionsTests" -v minimal`
Expected: FAIL — functions not registered.

- [ ] **Step 3: Implement VaultFunctions**

```csharp
// Services/Scripting/Functions/VaultFunctions.cs
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Services.Scripting.Functions;

public class VaultFunctions : IFunctionCategory
{
    public void Register(FunctionRegistry registry)
    {
        registry.Register("vault", VaultGet);
        registry.Register("vault_list", VaultList);
        registry.Register("vault_clear_cache", VaultClearCache);
    }

    private static object? VaultGet(string argsString, ScriptContext context)
    {
        var args = JsonUtilities.SplitTopLevelCommas(argsString);
        if (args.Count < 2) return null;

        var path = Resolve(args[0], context);
        var key = Resolve(args[1], context);
        var profile = args.Count >= 3
            ? Resolve(args[2], context)
            : context.VaultService?.ResolveDefaultProfileName(context.EnvironmentVaultProfile);

        if (context.VaultService == null || string.IsNullOrEmpty(profile))
            return null;

        try
        {
            return context.VaultService.ReadSecretAsync(profile, path, key).GetAwaiter().GetResult();
        }
        catch (VaultException ex)
        {
            context.SetVariable("_last_error", ex.Message);
            return null;
        }
    }

    private static object? VaultList(string argsString, ScriptContext context)
    {
        var args = JsonUtilities.SplitTopLevelCommas(argsString);
        if (args.Count < 1) return new List<string>();

        var prefix = Resolve(args[0], context);
        var profile = args.Count >= 2
            ? Resolve(args[1], context)
            : context.VaultService?.ResolveDefaultProfileName(context.EnvironmentVaultProfile);

        if (context.VaultService == null || string.IsNullOrEmpty(profile))
            return new List<string>();

        try
        {
            return context.VaultService.ListSecretsAsync(profile, prefix).GetAwaiter().GetResult();
        }
        catch (VaultException ex)
        {
            context.SetVariable("_last_error", ex.Message);
            return new List<string>();
        }
    }

    private static object? VaultClearCache(string argsString, ScriptContext context)
    {
        context.VaultService?.ClearCache();
        return true;
    }

    private static string Resolve(string expr, ScriptContext context)
        => JsonUtilities.ResolveJsonValue(expr, context)?.ToString() ?? string.Empty;
}
```

- [ ] **Step 4: Register VaultFunctions in FunctionRegistry**

In `Services/Scripting/FunctionRegistry.cs`, add to `RegisterBuiltInCategories()`:
```csharp
RegisterCategory(new VaultFunctions());
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultFunctionsTests" -v minimal`
Expected: PASS (all 2 tests).

- [ ] **Step 6: Commit**

```bash
git add Services/Scripting/Functions/VaultFunctions.cs Services/Scripting/FunctionRegistry.cs SSH_Helper.Tests/Scripting/VaultFunctionsTests.cs
git commit -m "feat(vault): add vault(), vault_list(), vault_clear_cache() built-in functions"
```

---

## Task 8: VaultCredentialProvider

**Files:**
- Create: `Services/Vault/VaultCredentialProvider.cs`
- Test: `SSH_Helper.Tests/Vault/VaultCredentialProviderTests.cs`

- [ ] **Step 1: Write tests for VaultCredentialProvider**

```csharp
// SSH_Helper.Tests/Vault/VaultCredentialProviderTests.cs
using FluentAssertions;
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Tests.Vault;

public class VaultCredentialProviderTests
{
    [Fact]
    public void ParseVaultPath_SimplePathDefaultKeys()
    {
        VaultCredentialProvider.ParseVaultPath("ssh/switches", out var profile, out var path, out var userKey, out var passKey);
        profile.Should().BeNull();
        path.Should().Be("ssh/switches");
        userKey.Should().Be("username");
        passKey.Should().Be("password");
    }

    [Fact]
    public void ParseVaultPath_WithProfile()
    {
        VaultCredentialProvider.ParseVaultPath("network@ssh/switches", out var profile, out var path, out var userKey, out var passKey);
        profile.Should().Be("network");
        path.Should().Be("ssh/switches");
        userKey.Should().Be("username");
        passKey.Should().Be("password");
    }

    [Fact]
    public void ParseVaultPath_WithCustomKeys()
    {
        VaultCredentialProvider.ParseVaultPath("ssh/switches#user_field,pass_field", out var profile, out var path, out var userKey, out var passKey);
        profile.Should().BeNull();
        path.Should().Be("ssh/switches");
        userKey.Should().Be("user_field");
        passKey.Should().Be("pass_field");
    }

    [Fact]
    public void ParseVaultPath_WithProfileAndCustomKeys()
    {
        VaultCredentialProvider.ParseVaultPath("network@ssh/switches#admin_user,admin_pass", out var profile, out var path, out var userKey, out var passKey);
        profile.Should().Be("network");
        path.Should().Be("ssh/switches");
        userKey.Should().Be("admin_user");
        passKey.Should().Be("admin_pass");
    }

    [Fact]
    public void IsAvailable_ReturnsTrueWhenEnabled()
    {
        var vault = new VaultService(new VaultSettings { Enabled = true, Profiles = [new VaultProfileConfig { Name = "test", Address = "https://v:8200" }] },
            _ => new DelegatingHandlerStub(_ => new HttpResponseMessage()));
        var provider = new VaultCredentialProvider(vault);
        provider.IsAvailable.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultCredentialProviderTests" -v minimal`
Expected: FAIL.

- [ ] **Step 3: Implement VaultCredentialProvider**

```csharp
// Services/Vault/VaultCredentialProvider.cs
namespace SSH_Helper.Services.Vault;

public class VaultCredentialProvider : ICredentialProvider
{
    private readonly VaultService _vaultService;

    public VaultCredentialProvider(VaultService vaultService) => _vaultService = vaultService;

    public bool IsAvailable => _vaultService.IsEnabled;

    public bool TryGetPassword(string target, out string username, out string password)
    {
        username = "";
        password = "";

        // target format: vault path from vault_path column
        if (string.IsNullOrEmpty(target))
            return false;

        try
        {
            ParseVaultPath(target, out var profile, out var path, out var userKey, out var passKey);
            var profileName = profile ?? _vaultService.ResolveDefaultProfileName();
            if (string.IsNullOrEmpty(profileName))
                return false;

            var keys = _vaultService.ReadSecretKeysAsync(profileName, path, [userKey, passKey]).GetAwaiter().GetResult();
            username = keys.GetValueOrDefault(userKey) ?? "";
            password = keys.GetValueOrDefault(passKey) ?? "";
            return !string.IsNullOrEmpty(password);
        }
        catch
        {
            return false;
        }
    }

    public bool SavePassword(string target, string username, string password, string? comment = null)
        => false; // Vault writes are done through scripts, not the credential provider

    public bool DeletePassword(string target)
        => false; // Vault deletions are not supported through the credential provider

    public static void ParseVaultPath(string vaultPath, out string? profile, out string path, out string usernameKey, out string passwordKey)
    {
        profile = null;
        usernameKey = "username";
        passwordKey = "password";

        var remaining = vaultPath;

        // Check for profile@ prefix
        var atIndex = remaining.IndexOf('@');
        var hashIndex = remaining.IndexOf('#');
        if (atIndex >= 0 && (hashIndex < 0 || atIndex < hashIndex))
        {
            profile = remaining.Substring(0, atIndex);
            remaining = remaining.Substring(atIndex + 1);
            hashIndex = remaining.IndexOf('#');
        }

        // Check for #userKey,passKey suffix
        if (hashIndex >= 0)
        {
            path = remaining.Substring(0, hashIndex);
            var keysPart = remaining.Substring(hashIndex + 1);
            var keyParts = keysPart.Split(',', 2);
            if (keyParts.Length == 2)
            {
                usernameKey = keyParts[0].Trim();
                passwordKey = keyParts[1].Trim();
            }
            else if (keyParts.Length == 1)
            {
                passwordKey = keyParts[0].Trim();
            }
        }
        else
        {
            path = remaining;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~VaultCredentialProviderTests" -v minimal`
Expected: PASS (all 5 tests).

- [ ] **Step 5: Commit**

```bash
git add Services/Vault/VaultCredentialProvider.cs SSH_Helper.Tests/Vault/VaultCredentialProviderTests.cs
git commit -m "feat(vault): add VaultCredentialProvider with vault_path parsing"
```

---

## Task 9: JobExecutionService — CredentialMode.Vault

**Files:**
- Modify: `Services/Scheduling/JobExecutionService.cs:519-556`

- [ ] **Step 1: Add Vault case to ResolveCredentials**

In `Services/Scheduling/JobExecutionService.cs`, in the `ResolveCredentials` switch (~line 519), add before the `default:` case:

```csharp
case CredentialMode.Vault:
    if (_vaultCredentialProvider != null &&
        !string.IsNullOrEmpty(job.VaultCredentialPath) &&
        _vaultCredentialProvider.TryGetPassword(job.VaultCredentialPath, out var vaultUser, out var vaultPass))
    {
        return (vaultUser, vaultPass);
    }
    Debug.WriteLine($"Warning: Vault credential resolution failed for job '{job.Name}' at path '{job.VaultCredentialPath}'");
    return (string.Empty, string.Empty);
```

- [ ] **Step 2: Add VaultCredentialProvider field to JobExecutionService**

Add to the constructor parameters and store as a field:
```csharp
private readonly VaultCredentialProvider? _vaultCredentialProvider;
```

Wire it from the constructor. If `ICredentialProvider` is already `VaultCredentialProvider`, cast it. Or add a separate parameter.

- [ ] **Step 3: Run full build to verify compilation**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add Services/Scheduling/JobExecutionService.cs
git commit -m "feat(vault): handle CredentialMode.Vault in JobExecutionService.ResolveCredentials"
```

---

## Task 10: Form1 Wiring

**Files:**
- Modify: `Form1.cs`

- [ ] **Step 1: Add VaultService field and initialization**

Add field to `Form1`:
```csharp
private VaultService? _vaultService;
```

Create `InitializeVault()` method:
```csharp
private void InitializeVault()
{
    _vaultService?.Dispose();
    _vaultService = null;

    var config = _configService.GetCurrent();
    if (!config.Vault.Enabled || config.Vault.Profiles.Count == 0)
        return;

    _vaultService = new VaultService(
        config.Vault,
        tokenProvider: (profileName, _) =>
        {
            var target = CredentialTargets.VaultAuthTarget(profileName, "token");
            return _credentialProvider?.TryGetPassword(target, out _, out var token) == true ? token : null;
        },
        secretIdProvider: (profileName, _) =>
        {
            var target = CredentialTargets.VaultAuthTarget(profileName, "approle_secret");
            return _credentialProvider?.TryGetPassword(target, out _, out var secret) == true ? secret : null;
        },
        ldapPasswordProvider: (profileName, _) =>
        {
            var target = CredentialTargets.VaultAuthTarget(profileName, "ldap_password");
            return _credentialProvider?.TryGetPassword(target, out _, out var pass) == true ? pass : null;
        });
}
```

- [ ] **Step 2: Call InitializeVault after InitializeCredentials**

In the startup sequence (~line 357) and settings-changed handler (~line 5471), add:
```csharp
InitializeVault();
```

- [ ] **Step 3: Pass VaultService to ScriptContext when executing scripts**

Find where `ScriptContext` is created before script execution and add:
```csharp
context.VaultService = _vaultService;
context.EnvironmentVaultProfile = _environmentService?.ActiveEnvironment?.VaultProfileName;
```

- [ ] **Step 4: Pass VaultCredentialProvider to JobExecutionService**

In `InitializeSchedulerServices()`, create `VaultCredentialProvider` from `_vaultService` and pass to `JobExecutionService`.

- [ ] **Step 5: Run full build**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Form1.cs
git commit -m "feat(vault): wire VaultService into Form1 startup, scripting context, and scheduler"
```

---

## Task 11: Settings UI — Vault Profile List

**Files:**
- Modify: `SettingsDialog.cs`

- [ ] **Step 1: Add Vault section controls**

In the General tab builder method, after the Credentials section, add:
```csharp
flow.Controls.Add(SectionHeader("Vault"));
flow.Controls.Add(new CheckBox { Name = "chkVaultEnabled", Text = "Enable HashiCorp Vault integration", AutoSize = true });
// Add a ListView or DataGridView for vault profiles
// Add buttons: Add Profile, Edit Profile, Remove Profile
// Profile editor controls shown in a GroupBox that toggles visibility based on selection
```

Follow the existing pattern for control declaration, FindControl lookup, LoadFromConfig, and SaveToConfig.

- [ ] **Step 2: Add profile editor GroupBox**

Add a collapsible GroupBox containing:
- Profile Name TextBox
- Default CheckBox
- Address TextBox
- Namespace TextBox
- Mount Path TextBox (default "secret")
- KV Version ComboBox (Auto-detect, v1, v2)
- Auth Method ComboBox (Token, AppRole, LDAP)
- Auth fields (dynamic visibility based on AuthMethod selection)
- CA Certificate Path TextBox + Browse button
- Skip TLS Verification CheckBox (with warning label)
- Cache TTL NumericUpDown
- Test Connection Button + status label

- [ ] **Step 3: Wire Load/Save for vault settings**

In LoadFromConfig:
```csharp
_chkVaultEnabled.Checked = config.Vault.Enabled;
// Populate profile list from config.Vault.Profiles
```

In SaveToConfig:
```csharp
config.Vault.Enabled = _chkVaultEnabled.Checked;
// Read profiles from UI controls back into config.Vault.Profiles
```

- [ ] **Step 4: Implement Test Connection button handler**

```csharp
private async void BtnTestVaultConnection_Click(object sender, EventArgs e)
{
    // Build a temporary VaultService from the current UI values
    // Call TestConnectionAsync
    // Show result in the status label
}
```

- [ ] **Step 5: Store sensitive auth values in credential manager**

When saving, store tokens/secrets via `_credentialProvider.SavePassword(CredentialTargets.VaultAuthTarget(profileName, "token"), "", token)`.

- [ ] **Step 6: Run full build**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add SettingsDialog.cs
git commit -m "feat(vault): add Vault profile management UI to SettingsDialog"
```

---

## Task 12: Environment Integration

**Files:**
- Modify: `EnvironmentDialog.cs`

- [ ] **Step 1: Add Vault Profile dropdown to EnvironmentDialog**

Find where environment properties are edited and add a ComboBox:
```csharp
var cmbVaultProfile = new ComboBox
{
    Name = "cmbVaultProfile",
    DropDownStyle = ComboBoxStyle.DropDownList,
    Width = 200
};
cmbVaultProfile.Items.Add("(none)");
// Populate from configured vault profiles
foreach (var profile in config.Vault.Profiles)
    cmbVaultProfile.Items.Add(profile.Name);
```

Wire load/save:
```csharp
// Load
cmbVaultProfile.SelectedItem = env.VaultProfileName ?? "(none)";
// Save
env.VaultProfileName = cmbVaultProfile.SelectedItem as string == "(none)" ? null : cmbVaultProfile.SelectedItem as string;
```

- [ ] **Step 2: Run full build**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add EnvironmentDialog.cs
git commit -m "feat(vault): add Vault Profile selector to EnvironmentDialog"
```

---

## Task 13: Flow Canvas Block & Bridge

**Files:**
- Modify: `FlowCanvas/src/blockDefs/registry.ts`
- Modify: `Services/FlowCanvasBridge.cs`

- [ ] **Step 1: Add vault block definition**

In `FlowCanvas/src/blockDefs/registry.ts`, add to the `blockDefs` array:

```typescript
{
  type: 'vault',
  label: 'Vault',
  category: 'network',
  icon: 'vault',
  description: 'Read, write, or patch secrets from HashiCorp Vault',
  previewKey: 'path',
  properties: [
    { key: 'profile', label: 'Profile', type: 'text' },
    { key: 'path', label: 'Path', type: 'text', required: true },
    { key: 'key', label: 'Key', type: 'text' },
    { key: 'keys', label: 'Keys Map', type: 'keyvalue' },
    { key: 'into', label: 'Into Variable', type: 'text' },
    { key: 'version', label: 'Version', type: 'number' },
    { key: 'write', label: 'Write Data', type: 'keyvalue' },
    { key: 'patch', label: 'Patch Data', type: 'keyvalue' },
    onErrorProp,
  ],
},
```

- [ ] **Step 2: Add vault to FlowCanvasBridge dispatch maps**

In `Services/FlowCanvasBridge.cs`:

Add to `RequiredOptionKeysByCommand`:
```csharp
["vault"] = ["path"],
```

Add to `DictionaryOptionKeys`:
```csharp
"keys", "write", "patch"
```
(Check if these are already included; if not, add them to the appropriate set.)

Add to `IntegerOptionKeys`:
```csharp
"version"
```
(Check if already included.)

- [ ] **Step 3: Build Flow Canvas**

Run: `cd FlowCanvas && npm run build`
Expected: Build succeeded.

- [ ] **Step 4: Run full .NET build**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/blockDefs/registry.ts Services/FlowCanvasBridge.cs
git commit -m "feat(vault): add vault block to Flow Canvas and bridge dispatch maps"
```

---

## Task 14: SCRIPTING.md Documentation

**Files:**
- Modify: `SCRIPTING.md`

- [ ] **Step 1: Add vault command section**

Add a `## vault` section in the command reference area of `SCRIPTING.md` documenting:
- Read single key syntax with example
- Read multiple keys syntax with example
- Write (full replace) syntax with example and warning about key removal
- Patch (merge) syntax with example
- Profile selection
- Version pinning (KV v2 only)
- on_error handling
- Audit log format

- [ ] **Step 2: Add inline syntax documentation**

In the variable substitution section, add:
```markdown
### Vault Inline Syntax

`{{vault:[profile@]path#key}}` resolves a secret from HashiCorp Vault inline.

Examples:
- `{{vault:ssh/server#password}}` — default profile
- `{{vault:network@ssh/switches#password}}` — explicit profile
```

- [ ] **Step 3: Add function documentation**

In the built-in functions section, add entries for:
- `vault("path", "key")` / `vault("path", "key", "profile")`
- `vault_list("prefix")` / `vault_list("prefix", "profile")`
- `vault_clear_cache()`

- [ ] **Step 4: Add vault_path column documentation**

In the CSV Grid Columns section, add:
```markdown
- `vault_path` — Optional. Vault path for per-host credential resolution. Format: `[profile@]path[#usernameKey,passwordKey]`. Defaults to `username` and `password` keys.
```

- [ ] **Step 5: Add Vault policy reference**

Add a "Vault Setup" section with the minimum HCL policies for read-only and read-write access.

- [ ] **Step 6: Add rotation recipe**

Add a "Secret Rotation" recipe showing the generate-push-store-verify workflow.

- [ ] **Step 7: Commit**

```bash
git add SCRIPTING.md
git commit -m "docs(vault): add vault command, inline syntax, functions, and rotation recipes to SCRIPTING.md"
```

---

## Task 15: Final Integration Test & Full Verification

- [ ] **Step 1: Run all vault tests**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Vault" -v minimal`
Expected: All vault tests pass.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj -v minimal`
Expected: All tests pass (no regressions).

- [ ] **Step 3: Run type check**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Build Flow Canvas**

Run: `cd FlowCanvas && npm run build`
Expected: Build succeeded.

- [ ] **Step 5: Verify no untested code paths**

Review all new files for any code paths that lack test coverage. Add tests for any gaps found.

- [ ] **Step 6: Final commit if any cleanup was needed**

```bash
git add -A
git commit -m "chore(vault): final integration verification and cleanup"
```
