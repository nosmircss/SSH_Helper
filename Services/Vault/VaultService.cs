using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using SSH_Helper.Models;

namespace SSH_Helper.Services.Vault
{
    /// <summary>
    /// Core service for HashiCorp Vault integration: authentication, KV read/write/patch/list, caching,
    /// and friendly error translation.
    /// </summary>
    public sealed class VaultService : IDisposable
    {
        private readonly VaultSettings _settings;
        private readonly Func<VaultProfileConfig, HttpMessageHandler> _handlerFactory;
        private readonly Func<string, string, string?>? _tokenProvider;
        private readonly Func<string, string, string?>? _secretIdProvider;
        private readonly Func<string, string, string?>? _ldapPasswordProvider;

        private readonly Dictionary<string, VaultProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _profilesLock = new();
        private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
        private readonly object _cacheLock = new();
        private bool _disposed;

        public VaultService(
            VaultSettings settings,
            Func<VaultProfileConfig, HttpMessageHandler>? handlerFactory = null,
            Func<string, string, string?>? tokenProvider = null,
            Func<string, string, string?>? secretIdProvider = null,
            Func<string, string, string?>? ldapPasswordProvider = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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

            if (!string.IsNullOrEmpty(_settings.DefaultProfileName))
                return _settings.DefaultProfileName;

            return _settings.Profiles.Count > 0 ? _settings.Profiles[0].Name : null;
        }

        public async Task<string?> ReadSecretAsync(
            string profileName, string path, string key,
            int? version = null, CancellationToken ct = default)
        {
            var cacheKey = BuildCacheKey(profileName, path, key, version);
            if (TryGetCachedValue(cacheKey, profileName, out var cached))
                return cached;

            var profile = await GetAuthenticatedProfileAsync(profileName, ct);
            var data = await ReadSecretDataAsync(profile, path, version, ct);

            if (!data.TryGetValue(key, out var value))
            {
                var availableKeys = string.Join(", ", data.Keys.OrderBy(k => k));
                throw new VaultException(
                    $"Secret at '{path}' exists but has no key '{key}' — available keys: {availableKeys}");
            }

            SetCacheValue(cacheKey, profileName, value);
            return value;
        }

        public async Task<Dictionary<string, string?>> ReadSecretKeysAsync(
            string profileName, string path, IEnumerable<string> keys,
            int? version = null, CancellationToken ct = default)
        {
            var keyList = keys.ToList();
            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            var uncachedKeys = new List<string>();

            foreach (var key in keyList)
            {
                var cacheKey = BuildCacheKey(profileName, path, key, version);
                if (TryGetCachedValue(cacheKey, profileName, out var cached))
                    result[key] = cached;
                else
                    uncachedKeys.Add(key);
            }

            if (uncachedKeys.Count == 0)
                return result;

            var profile = await GetAuthenticatedProfileAsync(profileName, ct);
            var data = await ReadSecretDataAsync(profile, path, version, ct);

            foreach (var key in uncachedKeys)
            {
                if (!data.TryGetValue(key, out var value))
                {
                    var availableKeys = string.Join(", ", data.Keys.OrderBy(k => k));
                    throw new VaultException(
                        $"Secret at '{path}' exists but has no key '{key}' — available keys: {availableKeys}");
                }

                var cacheKey = BuildCacheKey(profileName, path, key, version);
                SetCacheValue(cacheKey, profileName, value);
                result[key] = value;
            }

            return result;
        }

        public async Task WriteSecretAsync(
            string profileName, string path, Dictionary<string, string> data,
            CancellationToken ct = default)
        {
            var profile = await GetAuthenticatedProfileAsync(profileName, ct);
            var mount = profile.Config.MountPath.Trim('/');

            string url;
            string jsonBody;

            if (profile.EffectiveKvVersion == VaultKvVersion.V2)
            {
                url = $"v1/{mount}/data/{path.TrimStart('/')}";
                jsonBody = JsonSerializer.Serialize(new { data });
            }
            else
            {
                url = $"v1/{mount}/{path.TrimStart('/')}";
                jsonBody = JsonSerializer.Serialize(data);
            }

            var request = CreateRequest(HttpMethod.Post, url, profile);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await SendWithErrorTranslationAsync(profile, request, path, "write", ct);
            response.Dispose();

            InvalidateCacheForPath(profileName, path);
        }

        public async Task PatchSecretAsync(
            string profileName, string path, Dictionary<string, string> data,
            CancellationToken ct = default)
        {
            var profile = await GetAuthenticatedProfileAsync(profileName, ct);

            if (profile.EffectiveKvVersion == VaultKvVersion.V2)
            {
                if (await TryPatchV2Async(profile, path, data, ct))
                    return;
            }

            // Fallback: read-modify-write for v1 or when v2 PATCH is unsupported
            await ReadModifyWriteAsync(profile, profileName, path, data, ct);
        }

        public async Task<List<string>> ListSecretsAsync(
            string profileName, string prefix,
            CancellationToken ct = default)
        {
            var profile = await GetAuthenticatedProfileAsync(profileName, ct);
            var mount = profile.Config.MountPath.Trim('/');

            string url;
            if (profile.EffectiveKvVersion == VaultKvVersion.V2)
                url = $"v1/{mount}/metadata/{prefix.TrimStart('/')}";
            else
                url = $"v1/{mount}/{prefix.TrimStart('/')}";

            var request = CreateRequest(new HttpMethod("LIST"), url, profile);

            var response = await SendWithErrorTranslationAsync(profile, request, prefix, "list", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();

            using var doc = JsonDocument.Parse(body);
            var keys = doc.RootElement
                .GetProperty("data")
                .GetProperty("keys")
                .EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .ToList();

            return keys;
        }

        /// <summary>
        /// Tests connectivity and authentication against a vault profile.
        /// Throws VaultException with details on failure instead of returning false.
        /// </summary>
        public async Task TestConnectionAsync(string profileName, CancellationToken ct = default)
        {
            // Step 1: Authenticate (will throw VaultException with details on failure)
            var profile = await GetAuthenticatedProfileAsync(profileName, ct);

            // Step 2: Health check
            var request = CreateRequest(HttpMethod.Get, "v1/sys/health", profile);
            var response = await profile.HttpClient.SendAsync(request, ct);

            var code = (int)response.StatusCode;
            response.Dispose();

            if (code is not (200 or 429 or 472 or 473))
            {
                var status = code switch
                {
                    501 => "Vault is not initialized",
                    503 => "Vault is sealed",
                    _ => $"Vault returned HTTP {code}"
                };
                throw new VaultException($"Authentication succeeded but health check failed: {status}");
            }
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            List<VaultProfile> profilesToDispose;
            lock (_profilesLock)
            {
                profilesToDispose = _profiles.Values.ToList();
                _profiles.Clear();
            }

            foreach (var profile in profilesToDispose)
                profile.Dispose();

            ClearCache();
        }

        // --- Authentication ---

        private async Task<VaultProfile> GetAuthenticatedProfileAsync(string profileName, CancellationToken ct)
        {
            var profile = GetOrCreateProfile(profileName);

            if (!profile.IsTokenExpired)
                return profile;

            switch (profile.Config.AuthMethod)
            {
                case VaultAuthMethod.Token:
                    await AuthenticateWithTokenAsync(profile, ct);
                    break;
                case VaultAuthMethod.AppRole:
                    await AuthenticateWithAppRoleAsync(profile, ct);
                    break;
                case VaultAuthMethod.Ldap:
                    await AuthenticateWithLdapAsync(profile, ct);
                    break;
                default:
                    throw new VaultException($"Unsupported auth method: {profile.Config.AuthMethod}");
            }

            await DetectKvVersionIfNeeded(profile, ct);

            return profile;
        }

        private async Task AuthenticateWithTokenAsync(VaultProfile profile, CancellationToken ct)
        {
            var token = _tokenProvider?.Invoke(profile.Config.Name, "token");
            if (string.IsNullOrEmpty(token))
                throw new VaultException(
                    $"Vault authentication failed for profile '{profile.Config.Name}' — no token found in credential manager");

            profile.ClientToken = token;

            // Validate token and get TTL
            var request = CreateRequest(HttpMethod.Post, "v1/auth/token/lookup-self", profile);
            var response = await SendWithErrorTranslationAsync(profile, request, "auth/token/lookup-self", "read", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");

            if (data.TryGetProperty("ttl", out var ttlElement))
            {
                var ttl = ttlElement.GetInt64();
                if (ttl > 0)
                    profile.TokenExpiry = DateTime.UtcNow.AddSeconds(ttl * 0.75);
                else
                    profile.TokenExpiry = DateTime.MaxValue; // Non-expiring token
            }
            else
            {
                profile.TokenExpiry = DateTime.MaxValue;
            }
        }

        private async Task AuthenticateWithAppRoleAsync(VaultProfile profile, CancellationToken ct)
        {
            var secretId = _secretIdProvider?.Invoke(profile.Config.Name, "approle");
            if (string.IsNullOrEmpty(secretId))
                throw new VaultException(
                    $"Vault authentication failed for profile '{profile.Config.Name}' — no AppRole secret ID found in credential manager");

            var payload = JsonSerializer.Serialize(new
            {
                role_id = profile.Config.AppRoleRoleId,
                secret_id = secretId
            });

            var request = new HttpRequestMessage(HttpMethod.Post, "v1/auth/approle/login")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            ApplyNamespaceHeader(request, profile);

            var response = await SendWithErrorTranslationAsync(profile, request, "auth/approle/login", "write", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();

            using var doc = JsonDocument.Parse(body);
            var auth = doc.RootElement.GetProperty("auth");
            profile.ClientToken = auth.GetProperty("client_token").GetString();

            if (auth.TryGetProperty("lease_duration", out var leaseDuration))
            {
                var ttl = leaseDuration.GetInt64();
                profile.TokenExpiry = ttl > 0
                    ? DateTime.UtcNow.AddSeconds(ttl * 0.75)
                    : DateTime.MaxValue;
            }
            else
            {
                profile.TokenExpiry = DateTime.MaxValue;
            }
        }

        private async Task AuthenticateWithLdapAsync(VaultProfile profile, CancellationToken ct)
        {
            var password = _ldapPasswordProvider?.Invoke(profile.Config.Name, "ldap");
            if (string.IsNullOrEmpty(password))
                throw new VaultException(
                    $"Vault authentication failed for profile '{profile.Config.Name}' — no LDAP password found in credential manager");

            var username = profile.Config.LdapUsername;
            var payload = JsonSerializer.Serialize(new { password });

            var request = new HttpRequestMessage(HttpMethod.Post, $"v1/auth/ldap/login/{Uri.EscapeDataString(username)}")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            ApplyNamespaceHeader(request, profile);

            var response = await SendWithErrorTranslationAsync(profile, request, $"auth/ldap/login/{username}", "write", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();

            using var doc = JsonDocument.Parse(body);
            var auth = doc.RootElement.GetProperty("auth");
            profile.ClientToken = auth.GetProperty("client_token").GetString();

            if (auth.TryGetProperty("lease_duration", out var leaseDuration))
            {
                var ttl = leaseDuration.GetInt64();
                profile.TokenExpiry = ttl > 0
                    ? DateTime.UtcNow.AddSeconds(ttl * 0.75)
                    : DateTime.MaxValue;
            }
            else
            {
                profile.TokenExpiry = DateTime.MaxValue;
            }
        }

        // --- KV Version Detection ---

        private async Task DetectKvVersionIfNeeded(VaultProfile profile, CancellationToken ct)
        {
            if (profile.Config.KvVersion != VaultKvVersion.AutoDetect)
                return;

            if (profile.DetectedKvVersion != null)
                return;

            var mount = profile.Config.MountPath.Trim('/');

            // Try sys/mounts tune first
            try
            {
                var request = CreateRequest(HttpMethod.Get, $"v1/sys/mounts/{mount}/tune", profile);
                var response = await profile.HttpClient.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    response.Dispose();

                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("options", out var options) &&
                        options.TryGetProperty("version", out var versionProp))
                    {
                        var versionStr = versionProp.GetString();
                        profile.DetectedKvVersion = versionStr == "1" ? VaultKvVersion.V1 : VaultKvVersion.V2;
                        return;
                    }
                }

                response.Dispose();

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    await DetectKvVersionByHeuristicAsync(profile, mount, ct);
                    return;
                }
            }
            catch
            {
                // Fall through to heuristic
            }

            await DetectKvVersionByHeuristicAsync(profile, mount, ct);
        }

        private async Task DetectKvVersionByHeuristicAsync(VaultProfile profile, string mount, CancellationToken ct)
        {
            // Try v2 read path first: a non-404 response means the data/ prefix is valid → v2
            try
            {
                var v2Request = CreateRequest(HttpMethod.Get, $"v1/{mount}/data/detect-kv-version-probe", profile);
                var v2Response = await profile.HttpClient.SendAsync(v2Request, ct);
                var v2Code = (int)v2Response.StatusCode;
                v2Response.Dispose();

                if (v2Code != 404)
                {
                    profile.DetectedKvVersion = VaultKvVersion.V2;
                    return;
                }
            }
            catch
            {
                // Fall through to v1 probe
            }

            // v2 path returned 404 — try the v1 path (no data/ prefix)
            try
            {
                var v1Request = CreateRequest(HttpMethod.Get, $"v1/{mount}/detect-kv-version-probe", profile);
                var v1Response = await profile.HttpClient.SendAsync(v1Request, ct);
                var v1Code = (int)v1Response.StatusCode;
                v1Response.Dispose();

                if (v1Code != 404)
                {
                    profile.DetectedKvVersion = VaultKvVersion.V1;
                    return;
                }
            }
            catch
            {
                // ignored
            }

            // Both probes returned 404 — default to v2 as the more common modern setup
            profile.DetectedKvVersion = VaultKvVersion.V2;
        }

        // --- KV Operations ---

        private async Task<Dictionary<string, string?>> ReadSecretDataAsync(
            VaultProfile profile, string path, int? version, CancellationToken ct)
        {
            var mount = profile.Config.MountPath.Trim('/');
            string url;

            if (profile.EffectiveKvVersion == VaultKvVersion.V2)
            {
                url = $"v1/{mount}/data/{path.TrimStart('/')}";
                if (version.HasValue)
                    url += $"?version={version.Value}";
            }
            else
            {
                url = $"v1/{mount}/{path.TrimStart('/')}";
            }

            var request = CreateRequest(HttpMethod.Get, url, profile);
            var response = await SendWithErrorTranslationAsync(profile, request, path, "read", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();

            using var doc = JsonDocument.Parse(body);
            JsonElement dataElement;

            if (profile.EffectiveKvVersion == VaultKvVersion.V2)
                dataElement = doc.RootElement.GetProperty("data").GetProperty("data");
            else
                dataElement = doc.RootElement.GetProperty("data");

            var result = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var prop in dataElement.EnumerateObject())
            {
                result[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : prop.Value.ToString();
            }

            return result;
        }

        private async Task<bool> TryPatchV2Async(
            VaultProfile profile, string path, Dictionary<string, string> data,
            CancellationToken ct)
        {
            var mount = profile.Config.MountPath.Trim('/');
            var url = $"v1/{mount}/data/{path.TrimStart('/')}";

            var jsonBody = JsonSerializer.Serialize(new { data });

            var request = CreateRequest(new HttpMethod("PATCH"), url, profile);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/merge-patch+json");

            try
            {
                var response = await profile.HttpClient.SendAsync(request, ct);

                if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    response.Dispose();
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var patchErrorBody = "";
                    try { patchErrorBody = await response.Content.ReadAsStringAsync(ct); } catch { }
                    response.Dispose();
                    TranslateErrorResponse(response.StatusCode, profile.Config.Name, path, "update", ExtractVaultErrors(patchErrorBody));
                }

                response.Dispose();
                InvalidateCacheForPath(profile.Config.Name, path);
                return true;
            }
            catch (VaultException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private async Task ReadModifyWriteAsync(
            VaultProfile profile, string profileName, string path,
            Dictionary<string, string> data, CancellationToken ct)
        {
            Dictionary<string, string?> existing;
            try
            {
                existing = await ReadSecretDataAsync(profile, path, null, ct);
            }
            catch (VaultException ex) when (ex.Message.Contains("No secret found"))
            {
                existing = new Dictionary<string, string?>();
            }

            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in existing)
                merged[kvp.Key] = kvp.Value ?? "";

            foreach (var kvp in data)
                merged[kvp.Key] = kvp.Value;

            var mount = profile.Config.MountPath.Trim('/');
            string url;
            string jsonBody;

            if (profile.EffectiveKvVersion == VaultKvVersion.V2)
            {
                url = $"v1/{mount}/data/{path.TrimStart('/')}";
                jsonBody = JsonSerializer.Serialize(new { data = merged });
            }
            else
            {
                url = $"v1/{mount}/{path.TrimStart('/')}";
                jsonBody = JsonSerializer.Serialize(merged);
            }

            var request = CreateRequest(HttpMethod.Post, url, profile);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await SendWithErrorTranslationAsync(profile, request, path, "write", ct);
            response.Dispose();

            InvalidateCacheForPath(profileName, path);
        }

        // --- HTTP Helpers ---

        private HttpRequestMessage CreateRequest(HttpMethod method, string url, VaultProfile profile)
        {
            var request = new HttpRequestMessage(method, url);

            if (!string.IsNullOrEmpty(profile.ClientToken))
                request.Headers.Add("X-Vault-Token", profile.ClientToken);

            ApplyNamespaceHeader(request, profile);

            return request;
        }

        private static void ApplyNamespaceHeader(HttpRequestMessage request, VaultProfile profile)
        {
            if (!string.IsNullOrEmpty(profile.Config.Namespace))
                request.Headers.Add("X-Vault-Namespace", profile.Config.Namespace);
        }

        private async Task<HttpResponseMessage> SendWithErrorTranslationAsync(
            VaultProfile profile, HttpRequestMessage request, string path, string capability,
            CancellationToken ct)
        {
            HttpResponseMessage response;
            try
            {
                response = await profile.HttpClient.SendAsync(request, ct);
            }
            catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
            {
                throw new VaultException(
                    $"Cannot connect to Vault at '{profile.Config.Address}' — connection refused. Is the Vault server running?",
                    ex);
            }
            catch (HttpRequestException ex)
            {
                throw new VaultException(
                    $"Cannot connect to Vault at '{profile.Config.Address}' — {ex.Message}",
                    ex);
            }

            if (response.IsSuccessStatusCode)
                return response;

            var statusCode = response.StatusCode;
            var errorBody = "";
            try
            {
                errorBody = await response.Content.ReadAsStringAsync(ct);
            }
            catch { /* ignore read failures */ }
            response.Dispose();

            // Extract Vault's error messages from the response body
            var vaultErrors = ExtractVaultErrors(errorBody);

            TranslateErrorResponse(statusCode, profile.Config.Name, path, capability, vaultErrors);

            // TranslateErrorResponse always throws, but compiler needs this
            throw new VaultException($"Unexpected Vault error: HTTP {(int)statusCode}");
        }

        private static string ExtractVaultErrors(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return "";
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                {
                    var messages = errors.EnumerateArray()
                        .Select(e => e.GetString())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                    if (messages.Count > 0)
                        return string.Join("; ", messages);
                }
            }
            catch { /* not JSON or no errors field */ }
            return "";
        }

        private static void TranslateErrorResponse(HttpStatusCode statusCode, string profileName, string path, string capability, string vaultErrors)
        {
            var detail = string.IsNullOrEmpty(vaultErrors) ? "" : $" ({vaultErrors})";

            switch ((int)statusCode)
            {
                case 400:
                    throw new VaultException(
                        $"Vault rejected the request for '{path}'{detail}");
                case 401:
                    throw new VaultException(
                        $"Vault authentication failed for profile '{profileName}'{detail} — check your token or AppRole credentials");
                case 403:
                    throw new VaultException(
                        $"Permission denied on '{path}'{detail} — check that your Vault policy grants '{capability}'");
                case 404:
                    throw new VaultException(
                        $"No secret found at '{path}'{detail} — verify the path exists in Vault");
                case 503:
                    throw new VaultException(
                        $"Vault is sealed — it needs to be unsealed before SSH_Helper can access secrets");
                default:
                    throw new VaultException(
                        $"Vault returned HTTP {(int)statusCode} for path '{path}'{detail}");
            }
        }

        // --- Profile Management ---

        private VaultProfile GetOrCreateProfile(string profileName)
        {
            lock (_profilesLock)
            {
                if (_profiles.TryGetValue(profileName, out var existing))
                    return existing;
            }

            var config = _settings.Profiles.FirstOrDefault(
                p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

            if (config == null)
            {
                var configured = string.Join(", ", _settings.Profiles.Select(p => p.Name));
                throw new VaultException(
                    $"Vault profile '{profileName}' not found. Configured profiles: {configured}");
            }

            var handler = _handlerFactory(config);
            var createdProfile = new VaultProfile(config, handler);

            lock (_profilesLock)
            {
                if (_profiles.TryGetValue(profileName, out var racedExisting))
                {
                    createdProfile.Dispose();
                    return racedExisting;
                }

                _profiles[config.Name] = createdProfile;
                return createdProfile;
            }
        }

        // --- Caching ---

        private static string BuildCacheKey(string profileName, string path, string key, int? version)
            => $"{profileName}|{path}|v={(version?.ToString() ?? "latest")}|{key}";

        private bool TryGetCachedValue(string cacheKey, string profileName, out string? value)
        {
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var entry))
                {
                    if (DateTime.UtcNow < entry.Expiry)
                    {
                        value = entry.Value;
                        return true;
                    }

                    _cache.Remove(cacheKey);
                }
            }

            value = null;
            return false;
        }

        private void SetCacheValue(string cacheKey, string profileName, string? value)
        {
            var config = _settings.Profiles.FirstOrDefault(
                p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

            var ttl = config?.CacheTtlSeconds ?? 300;

            lock (_cacheLock)
            {
                _cache[cacheKey] = new CacheEntry(value, DateTime.UtcNow.AddSeconds(ttl));
            }
        }

        private void InvalidateCacheForPath(string profileName, string path)
        {
            var prefix = $"{profileName}|{path}|";

            lock (_cacheLock)
            {
                var keysToRemove = _cache.Keys
                    .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                    .ToList();

                foreach (var key in keysToRemove)
                    _cache.Remove(key);
            }
        }

        // --- Default Handler ---

        private static HttpMessageHandler CreateDefaultHandler(VaultProfileConfig config)
        {
            var handler = new HttpClientHandler();

            if (config.SkipTlsVerification)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }
            else if (!string.IsNullOrEmpty(config.CaCertificatePath) && File.Exists(config.CaCertificatePath))
            {
                var caCert = new X509Certificate2(config.CaCertificatePath);
                handler.ServerCertificateCustomValidationCallback = (_, cert, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None)
                        return true;

                    if (cert == null || chain == null)
                        return false;

                    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                    chain.ChainPolicy.CustomTrustStore.Add(caCert);
                    return chain.Build(cert);
                };
            }

            return handler;
        }

        // --- Cache Entry ---

        private sealed record CacheEntry(string? Value, DateTime Expiry);
    }
}
