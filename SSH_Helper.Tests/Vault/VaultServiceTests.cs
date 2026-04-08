using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services.Vault;
using Xunit;

namespace SSH_Helper.Tests.Vault;

/// <summary>
/// A DelegatingHandler stub that invokes a user-supplied function for each request.
/// </summary>
internal sealed class DelegatingHandlerStub : DelegatingHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public DelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}

internal sealed class StubVaultOidcLoginFlow : IVaultOidcLoginFlow
{
    private readonly Func<string, string, int, string, int, CancellationToken, Task<VaultOidcLoginResult>> _handler;

    public StubVaultOidcLoginFlow(Func<string, string, int, string, int, CancellationToken, Task<VaultOidcLoginResult>> handler)
    {
        _handler = handler;
    }

    public Task<VaultOidcLoginResult> ExecuteAsync(
        string authUrl,
        string callbackHost,
        int callbackPort,
        string callbackPath,
        int timeoutSeconds,
        CancellationToken cancellationToken)
        => _handler(authUrl, callbackHost, callbackPort, callbackPath, timeoutSeconds, cancellationToken);
}

public class VaultServiceTests
{
    private static VaultSettings CreateSettings(
        string profileName = "test",
        string address = "https://vault.test:8200",
        VaultAuthMethod authMethod = VaultAuthMethod.Token,
        VaultKvVersion kvVersion = VaultKvVersion.V2,
        int cacheTtl = 300)
    {
        return new VaultSettings
        {
            Enabled = true,
            DefaultProfileName = profileName,
            Profiles =
            [
                new VaultProfileConfig
                {
                    Name = profileName,
                    Address = address,
                    AuthMethod = authMethod,
                    KvVersion = kvVersion,
                    CacheTtlSeconds = cacheTtl
                }
            ]
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
    }

    private static HttpResponseMessage TokenLookupResponse(int ttl = 3600)
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            data = new { ttl, display_name = "token", policies = new[] { "default" } }
        });
    }

    private static HttpResponseMessage AppRoleLoginResponse(string token = "s.approle-token", int leaseDuration = 3600)
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            auth = new { client_token = token, lease_duration = leaseDuration, policies = new[] { "default" } }
        });
    }

    private static HttpResponseMessage UserpassLoginResponse(string token = "s.userpass-token", int leaseDuration = 3600)
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            auth = new { client_token = token, lease_duration = leaseDuration, policies = new[] { "default" } }
        });
    }

    private static HttpResponseMessage KvV2ReadResponse(Dictionary<string, string> data)
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            data = new { data, metadata = new { version = 1 } }
        });
    }

    private static HttpResponseMessage KvV1ReadResponse(Dictionary<string, string> data)
    {
        return JsonResponse(HttpStatusCode.OK, new { data });
    }

    private static HttpResponseMessage MountTuneResponse(string version = "2")
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            options = new { version }
        });
    }

    // -- Test 1: Token auth -- valid token caches and allows reads --

    [Fact]
    public async Task TokenAuth_ValidToken_CachesAndAllowsReads()
    {
        var callCount = 0;
        var settings = CreateSettings();
        var handler = new DelegatingHandlerStub(req =>
        {
            callCount++;
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/myapp/config"))
                return KvV2ReadResponse(new Dictionary<string, string> { ["db_pass"] = "s3cret" });

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.my-token");

        var val1 = await svc.ReadSecretAsync("test", "myapp/config", "db_pass");
        val1.Should().Be("s3cret");

        // Second read should hit cache, not make another HTTP call
        var beforeCount = callCount;
        var val2 = await svc.ReadSecretAsync("test", "myapp/config", "db_pass");
        val2.Should().Be("s3cret");
        callCount.Should().Be(beforeCount, "cached reads should not make HTTP calls");
    }

    // -- Test 2: AppRole auth -- gets client_token from login response --

    [Fact]
    public async Task AppRoleAuth_GetsClientToken()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.AppRole);
        settings.Profiles[0].AppRoleRoleId = "my-role-id";

        string? capturedToken = null;
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/approle/login"))
                return AppRoleLoginResponse("s.approle-result");

            if (path.Contains("secret/data/app/creds"))
            {
                capturedToken = req.Headers.GetValues("X-Vault-Token").FirstOrDefault();
                return KvV2ReadResponse(new Dictionary<string, string> { ["key"] = "val" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            secretIdProvider: (_, _) => "my-secret-id");

        var val = await svc.ReadSecretAsync("test", "app/creds", "key");
        val.Should().Be("val");
        capturedToken.Should().Be("s.approle-result");
    }

    // -- Test 2b: Userpass auth -- gets client_token from login response --

    [Fact]
    public async Task UserpassAuth_GetsClientToken()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Userpass);
        settings.Profiles[0].UserpassUsername = "alice";

        string? capturedToken = null;
        string? capturedBody = null;
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/userpass/login/alice"))
            {
                capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return UserpassLoginResponse("s.userpass-result");
            }

            if (path.Contains("secret/data/app/creds"))
            {
                capturedToken = req.Headers.GetValues("X-Vault-Token").FirstOrDefault();
                return KvV2ReadResponse(new Dictionary<string, string> { ["key"] = "val" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            userpassPasswordProvider: (_, _) => "my-userpass-password");

        var val = await svc.ReadSecretAsync("test", "app/creds", "key");
        val.Should().Be("val");
        capturedToken.Should().Be("s.userpass-result");
        capturedBody.Should().Contain("my-userpass-password");
    }

    // -- Test 2c: Userpass auth -- missing password fails with clear message --

    [Fact]
    public async Task UserpassAuth_MissingPassword_ThrowsFriendlyError()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Userpass);
        settings.Profiles[0].UserpassUsername = "alice";

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var act = () => svc.ReadSecretAsync("test", "app/creds", "key");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*userpass password*credential manager*");
    }

    [Fact]
    public async Task UserpassAuth_MissingUsername_ThrowsFriendlyError()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Userpass);

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => new DelegatingHandlerStub(_ => new HttpResponseMessage(HttpStatusCode.NotFound)),
            userpassPasswordProvider: (_, _) => "set");

        var act = () => svc.ReadSecretAsync("test", "app/creds", "key");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*userpass username configured*");
    }

    // -- Test 3: KV v2 auto-detection via mount tune --

    [Fact]
    public async Task KvAutoDetect_UsesMountTune()
    {
        var settings = CreateSettings(kvVersion: VaultKvVersion.AutoDetect);
        var tuneCalled = false;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("sys/mounts/secret/tune"))
            {
                tuneCalled = true;
                return MountTuneResponse("2");
            }

            if (path.Contains("secret/data/app/db"))
                return KvV2ReadResponse(new Dictionary<string, string> { ["pass"] = "abc" });

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var val = await svc.ReadSecretAsync("test", "app/db", "pass");
        val.Should().Be("abc");
        tuneCalled.Should().BeTrue("should query mount tune for auto-detection");
    }

    // -- Test 4: KV v1 fallback when mount tune is forbidden (403) --

    [Fact]
    public async Task KvAutoDetect_FallsBackToV1OnForbiddenTune()
    {
        var settings = CreateSettings(kvVersion: VaultKvVersion.AutoDetect);
        string? capturedReadPath = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("sys/mounts/secret/tune"))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);

            // v2 heuristic probe returns 404 — no v2 data/ prefix recognized
            if (path.Contains("secret/data/detect-kv-version-probe"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            // v1 heuristic probe succeeds — the mount responds without data/ prefix
            if (path.EndsWith("secret/detect-kv-version-probe"))
                return KvV1ReadResponse(new Dictionary<string, string> { ["probe"] = "ok" });

            // After v1 detection, reads should go through v1 path (no data/ prefix)
            if (path.Contains("secret/app/db") && !path.Contains("secret/data/"))
            {
                capturedReadPath = path;
                return KvV1ReadResponse(new Dictionary<string, string> { ["pass"] = "v1-detected" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var val = await svc.ReadSecretAsync("test", "app/db", "pass");
        val.Should().Be("v1-detected");
        capturedReadPath.Should().Contain("secret/app/db", "should use v1 path without data/ prefix");
        capturedReadPath.Should().NotContain("secret/data/", "v1 path must not include the v2 data/ segment");
    }

    // -- Test 5: Read single key (v2) -- returns correct value --

    [Fact]
    public async Task ReadSecret_V2_ReturnsCorrectValue()
    {
        var settings = CreateSettings();

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/myapp/config"))
                return KvV2ReadResponse(new Dictionary<string, string>
                {
                    ["username"] = "admin",
                    ["password"] = "hunter2"
                });

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var val = await svc.ReadSecretAsync("test", "myapp/config", "password");
        val.Should().Be("hunter2");
    }

    // -- Test 6: Read with key not found -- throws with available keys list --

    [Fact]
    public async Task ReadSecret_KeyNotFound_ThrowsWithAvailableKeys()
    {
        var settings = CreateSettings();

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/myapp/config"))
                return KvV2ReadResponse(new Dictionary<string, string>
                {
                    ["username"] = "admin",
                    ["password"] = "hunter2"
                });

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var act = () => svc.ReadSecretAsync("test", "myapp/config", "nonexistent");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*has no key 'nonexistent'*available keys*password*username*");
    }

    // -- Test 7: 403 Forbidden -- throws friendly permission denied message --

    [Fact]
    public async Task ReadSecret_403_ThrowsFriendlyMessage()
    {
        var settings = CreateSettings();

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/forbidden/path"))
                return new HttpResponseMessage(HttpStatusCode.Forbidden);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var act = () => svc.ReadSecretAsync("test", "forbidden/path", "key");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*Permission denied*'forbidden/path'*'read'*");
    }

    // -- Test 8: 503 Sealed -- throws friendly sealed message --

    [Fact]
    public async Task ReadSecret_503_ThrowsSealedMessage()
    {
        var settings = CreateSettings();

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/sealed/path"))
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var act = () => svc.ReadSecretAsync("test", "sealed/path", "key");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*Vault is sealed*unsealed*");
    }

    // -- Test 9: Caching -- second read for same path/key skips HTTP call --

    [Fact]
    public async Task Caching_SecondReadSkipsHttp()
    {
        var settings = CreateSettings();
        var readCount = 0;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/cached/path"))
            {
                readCount++;
                return KvV2ReadResponse(new Dictionary<string, string> { ["key"] = "cached-val" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        await svc.ReadSecretAsync("test", "cached/path", "key");
        await svc.ReadSecretAsync("test", "cached/path", "key");

        readCount.Should().Be(1, "second read should use cache");
    }

    // -- Test 9b: Caching must include version in the cache key --

    [Fact]
    public async Task Caching_DifferentVersions_DoNotShareCacheEntries()
    {
        var settings = CreateSettings();
        var readCount = 0;

        var handler = new DelegatingHandlerStub(req =>
        {
            var pathAndQuery = req.RequestUri!.PathAndQuery;

            if (pathAndQuery.Contains("auth/token/lookup-self", StringComparison.Ordinal))
                return TokenLookupResponse();

            if (pathAndQuery.Contains("secret/data/versioned/path", StringComparison.Ordinal))
            {
                readCount++;
                var version = req.RequestUri!.Query;
                var value = version.Contains("version=2", StringComparison.Ordinal)
                    ? "value-v2"
                    : "value-v1";

                return KvV2ReadResponse(new Dictionary<string, string> { ["key"] = value });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var v1 = await svc.ReadSecretAsync("test", "versioned/path", "key", version: 1);
        var v2 = await svc.ReadSecretAsync("test", "versioned/path", "key", version: 2);

        v1.Should().Be("value-v1");
        v2.Should().Be("value-v2");
        readCount.Should().Be(2, "versioned reads must not reuse cache entries across different versions");
    }

    // -- Test 10: Write invalidates cache -- read after write makes new HTTP call --

    [Fact]
    public async Task WriteInvalidatesCache_ReadAfterWriteMakesNewCall()
    {
        var settings = CreateSettings();
        var readCount = 0;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (req.Method == HttpMethod.Get && path.Contains("secret/data/app/db"))
            {
                readCount++;
                return KvV2ReadResponse(new Dictionary<string, string> { ["pass"] = $"v{readCount}" });
            }

            if (req.Method == HttpMethod.Post && path.Contains("secret/data/app/db"))
                return JsonResponse(HttpStatusCode.OK, new { data = new { version = 2 } });

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        await svc.ReadSecretAsync("test", "app/db", "pass");
        readCount.Should().Be(1);

        await svc.WriteSecretAsync("test", "app/db", new Dictionary<string, object?> { ["pass"] = "new" });

        await svc.ReadSecretAsync("test", "app/db", "pass");
        readCount.Should().Be(2, "cache should be invalidated after write");
    }

    // -- Test 11: ClearCache -- forces new HTTP call after cache is cleared --

    [Fact]
    public async Task ClearCache_ForcesNewHttpCallOnNextRead()
    {
        var settings = CreateSettings();
        var secretReadCount = 0;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/app/creds"))
            {
                secretReadCount++;
                return KvV2ReadResponse(new Dictionary<string, string> { ["api_key"] = "k3y" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        // First read — populates cache
        var val1 = await svc.ReadSecretAsync("test", "app/creds", "api_key");
        val1.Should().Be("k3y");
        secretReadCount.Should().Be(1);

        // Second read — served from cache, no new HTTP call
        await svc.ReadSecretAsync("test", "app/creds", "api_key");
        secretReadCount.Should().Be(1, "cached read should not make an HTTP call");

        // Clear the cache
        svc.ClearCache();

        // Third read — cache is gone, must make a new HTTP call
        var val3 = await svc.ReadSecretAsync("test", "app/creds", "api_key");
        val3.Should().Be("k3y");
        secretReadCount.Should().Be(2, "ClearCache should force a new HTTP call on the next read");
    }

    // -- Test 12: Profile not found -- throws descriptive error listing configured profiles --

    [Fact]
    public async Task ProfileNotFound_ThrowsWithConfiguredProfiles()
    {
        var settings = new VaultSettings
        {
            Enabled = true,
            Profiles =
            [
                new VaultProfileConfig { Name = "prod", Address = "https://vault.prod:8200" },
                new VaultProfileConfig { Name = "staging", Address = "https://vault.staging:8200" }
            ]
        };

        using var svc = new VaultService(settings);

        var act = () => svc.ReadSecretAsync("nonexistent", "path", "key");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*'nonexistent' not found*prod*staging*");
    }

    // -- Test 13: Patch v2 -- sends PATCH with merge-patch content type --

    [Fact]
    public async Task PatchV2_SendsPatchWithMergePatchContentType()
    {
        var settings = CreateSettings();
        HttpMethod? capturedMethod = null;
        string? capturedContentType = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/app/config") && req.Method.Method == "PATCH")
            {
                capturedMethod = req.Method;
                capturedContentType = req.Content?.Headers.ContentType?.MediaType;
                return JsonResponse(HttpStatusCode.OK, new { data = new { version = 3 } });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        await svc.PatchSecretAsync("test", "app/config", new Dictionary<string, object?> { ["key"] = "patched" });

        capturedMethod!.Method.Should().Be("PATCH");
        capturedContentType.Should().Be("application/merge-patch+json");
    }

    // -- Test 14: Patch fallback to read-modify-write when PATCH returns 405 --

    [Fact]
    public async Task PatchFallback_405_UsesReadModifyWrite()
    {
        var settings = CreateSettings();
        var postCalled = false;
        string? postedBody = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/data/app/config") && req.Method.Method == "PATCH")
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);

            if (path.Contains("secret/data/app/config") && req.Method == HttpMethod.Get)
            {
                return KvV2ReadResponse(new Dictionary<string, string>
                {
                    ["existing_key"] = "existing_val"
                });
            }

            if (path.Contains("secret/data/app/config") && req.Method == HttpMethod.Post)
            {
                postCalled = true;
                postedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse(HttpStatusCode.OK, new { data = new { version = 2 } });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        await svc.PatchSecretAsync("test", "app/config", new Dictionary<string, object?> { ["new_key"] = "new_val" });

        postCalled.Should().BeTrue("should fall back to POST after 405");

        // The posted body should contain both existing and new keys
        postedBody.Should().Contain("existing_key");
        postedBody.Should().Contain("new_key");
    }

    // -- Test 15: List secrets -- returns list of path strings --

    [Fact]
    public async Task ListSecrets_ReturnsPathStrings()
    {
        var settings = CreateSettings();

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();

            if (path.Contains("secret/metadata/apps/") && req.Method.Method == "LIST")
            {
                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new
                    {
                        keys = new[] { "app1/", "app2/", "shared" }
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.token");

        var results = await svc.ListSecretsAsync("test", "apps/");

        results.Should().HaveCount(3);
        results.Should().Contain("app1/");
        results.Should().Contain("app2/");
        results.Should().Contain("shared");
    }

    [Fact]
    public async Task ConcurrentProfileAccess_DoesNotThrowDictionaryRaceExceptions()
    {
        async Task RunAttemptAsync()
        {
            var settings = CreateSettings();
            var handler = new DelegatingHandlerStub(req =>
            {
                var path = req.RequestUri!.AbsolutePath;

                if (path.Contains("auth/token/lookup-self", StringComparison.Ordinal))
                    return TokenLookupResponse();

                if (path.Contains("secret/data/concurrent/path", StringComparison.Ordinal))
                    return KvV2ReadResponse(new Dictionary<string, string> { ["key"] = "ok" });

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

            using var svc = new VaultService(
                settings,
                handlerFactory: _ => handler,
                tokenProvider: (_, _) => "s.token");

            using var gate = new ManualResetEventSlim(false);
            var tasks = Enumerable.Range(0, 64)
                .Select(_ => Task.Run(async () =>
                {
                    gate.Wait();
                    await svc.ReadSecretAsync("test", "concurrent/path", "key");
                }))
                .ToArray();

            gate.Set();
            await Task.WhenAll(tasks);
        }

        Func<Task> act = async () =>
        {
            for (var attempt = 0; attempt < 8; attempt++)
                await RunAttemptAsync();
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OidcAuth_Success_PersistsTokenAndReadsSecret()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Oidc);
        settings.Profiles[0].OidcRole = "desktop-role";
        settings.Profiles[0].OidcAuthMountPath = "oidc";
        settings.Profiles[0].OidcCallbackHost = "127.0.0.1";
        settings.Profiles[0].OidcCallbackPort = 8250;
        settings.Profiles[0].OidcCallbackPath = "/oidc/callback";
        settings.Profiles[0].OidcTimeoutSeconds = 120;

        string? persistedProfile = null;
        string? persistedToken = null;
        string? callbackBody = null;
        string? generatedState = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/oidc/oidc/auth_url", StringComparison.Ordinal))
            {
                var authUrlBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(authUrlBody))
                {
                    using var doc = JsonDocument.Parse(authUrlBody);
                    generatedState = doc.RootElement.GetProperty("state").GetString();
                }

                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new { auth_url = "https://idp.example.com/authorize" }
                });
            }

            if (path.Contains("auth/oidc/oidc/callback", StringComparison.Ordinal))
            {
                callbackBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse(HttpStatusCode.OK, new
                {
                    auth = new { client_token = "s.oidc-token", lease_duration = 3600 }
                });
            }

            if (path.Contains("secret/data/oidc/path", StringComparison.Ordinal))
                return KvV2ReadResponse(new Dictionary<string, string> { ["password"] = "p@ss" });

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var oidcFlow = new StubVaultOidcLoginFlow((_, _, _, _, _, _) =>
            Task.FromResult(new VaultOidcLoginResult
            {
                State = generatedState ?? string.Empty,
                Code = "auth-code"
            }));

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: null,
            secretIdProvider: null,
            ldapPasswordProvider: null,
            userpassPasswordProvider: null,
            tokenSaver: (name, token) =>
            {
                persistedProfile = name;
                persistedToken = token;
            },
            oidcLoginFlow: oidcFlow);

        var value = await svc.ReadSecretAsync("test", "oidc/path", "password");

        value.Should().Be("p@ss");
        persistedProfile.Should().Be("test");
        persistedToken.Should().Be("s.oidc-token");
        callbackBody.Should().Contain("auth-code");
    }

    [Fact]
    public async Task OidcAuth_StateMismatch_ThrowsFriendlyError()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Oidc);
        settings.Profiles[0].OidcRole = "desktop-role";

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/oidc/oidc/auth_url", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new { auth_url = "https://idp.example.com/authorize?state=expected" }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var oidcFlow = new StubVaultOidcLoginFlow((_, _, _, _, _, _) =>
            Task.FromResult(new VaultOidcLoginResult
            {
                State = "unexpected",
                Code = "auth-code"
            }));

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: null,
            secretIdProvider: null,
            ldapPasswordProvider: null,
            userpassPasswordProvider: null,
            tokenSaver: null,
            oidcLoginFlow: oidcFlow);

        var act = () => svc.ReadSecretAsync("test", "oidc/path", "password");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*callback state mismatch*");
    }

    [Fact]
    public async Task OidcAuth_InvalidCallbackHost_ThrowsFriendlyErrorBeforeHttpCalls()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Oidc);
        settings.Profiles[0].OidcRole = "desktop-role";
        settings.Profiles[0].OidcCallbackHost = "vault.example.com";

        var httpCallCount = 0;
        var handler = new DelegatingHandlerStub(_ =>
        {
            httpCallCount++;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var oidcFlow = new StubVaultOidcLoginFlow((_, _, _, _, _, _) =>
            throw new InvalidOperationException("OIDC browser flow should not start for invalid callback hosts."));

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: null,
            secretIdProvider: null,
            ldapPasswordProvider: null,
            userpassPasswordProvider: null,
            tokenSaver: null,
            oidcLoginFlow: oidcFlow);

        var act = () => svc.ReadSecretAsync("test", "oidc/path", "password");
        await act.Should().ThrowAsync<VaultException>()
            .WithMessage("*loopback*");
        httpCallCount.Should().Be(0);
    }

    [Fact]
    public async Task OidcAuth_ValidPersistedToken_SkipsBrowserLogin()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Oidc);
        settings.Profiles[0].OidcRole = "desktop-role";

        var lookupSelfCount = 0;
        var browserAuthStarted = false;
        string? capturedSecretToken = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self", StringComparison.Ordinal))
            {
                lookupSelfCount++;
                return TokenLookupResponse(7200);
            }

            if (path.Contains("auth/oidc/oidc/auth_url", StringComparison.Ordinal) ||
                path.Contains("auth/oidc/oidc/callback", StringComparison.Ordinal))
            {
                browserAuthStarted = true;
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (path.Contains("secret/data/oidc/path", StringComparison.Ordinal))
            {
                capturedSecretToken = req.Headers.GetValues("X-Vault-Token").FirstOrDefault();
                return KvV2ReadResponse(new Dictionary<string, string> { ["password"] = "cached-secret" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var oidcFlow = new StubVaultOidcLoginFlow((_, _, _, _, _, _) =>
            throw new InvalidOperationException("OIDC browser flow should not start when persisted token is valid."));

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.persisted-token",
            secretIdProvider: null,
            ldapPasswordProvider: null,
            userpassPasswordProvider: null,
            tokenSaver: null,
            oidcLoginFlow: oidcFlow);

        var value = await svc.ReadSecretAsync("test", "oidc/path", "password");

        value.Should().Be("cached-secret");
        lookupSelfCount.Should().Be(1);
        browserAuthStarted.Should().BeFalse();
        capturedSecretToken.Should().Be("s.persisted-token");
    }

    [Fact]
    public async Task OidcAuth_UnauthorizedPersistedToken_FallsBackToBrowserLogin()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Oidc);
        settings.Profiles[0].OidcRole = "desktop-role";
        settings.Profiles[0].OidcAuthMountPath = "oidc";

        var lookupSelfCount = 0;
        var authUrlCount = 0;
        string? generatedState = null;
        string? capturedSecretToken = null;
        string? persistedToken = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self", StringComparison.Ordinal))
            {
                lookupSelfCount++;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            if (path.Contains("auth/oidc/oidc/auth_url", StringComparison.Ordinal))
            {
                authUrlCount++;
                var authUrlBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(authUrlBody!);
                generatedState = doc.RootElement.GetProperty("state").GetString();
                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new { auth_url = "https://idp.example.com/authorize" }
                });
            }

            if (path.Contains("auth/oidc/oidc/callback", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, new
                {
                    auth = new { client_token = "s.oidc-fresh", lease_duration = 3600 }
                });
            }

            if (path.Contains("secret/data/oidc/path", StringComparison.Ordinal))
            {
                capturedSecretToken = req.Headers.GetValues("X-Vault-Token").FirstOrDefault();
                return KvV2ReadResponse(new Dictionary<string, string> { ["password"] = "fresh-secret" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var oidcFlow = new StubVaultOidcLoginFlow((_, _, _, _, _, _) =>
            Task.FromResult(new VaultOidcLoginResult
            {
                State = generatedState ?? string.Empty,
                Code = "auth-code"
            }));

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.persisted-expired",
            secretIdProvider: null,
            ldapPasswordProvider: null,
            userpassPasswordProvider: null,
            tokenSaver: (_, token) => persistedToken = token,
            oidcLoginFlow: oidcFlow);

        var value = await svc.ReadSecretAsync("test", "oidc/path", "password");

        value.Should().Be("fresh-secret");
        lookupSelfCount.Should().Be(1);
        authUrlCount.Should().Be(1);
        capturedSecretToken.Should().Be("s.oidc-fresh");
        persistedToken.Should().Be("s.oidc-fresh");
    }

    [Fact]
    public async Task OidcAuth_PersistedTokenValidationTransportError_DoesNotFallBackToBrowserLogin()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Oidc);
        settings.Profiles[0].OidcRole = "desktop-role";

        var browserAuthStarted = false;
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/token/lookup-self", StringComparison.Ordinal))
                throw new HttpRequestException("network down");

            if (path.Contains("auth/oidc/oidc/auth_url", StringComparison.Ordinal) ||
                path.Contains("auth/oidc/oidc/callback", StringComparison.Ordinal))
            {
                browserAuthStarted = true;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var oidcFlow = new StubVaultOidcLoginFlow((_, _, _, _, _, _) =>
        {
            browserAuthStarted = true;
            throw new InvalidOperationException("OIDC browser flow should not start when persisted token validation fails with transport error.");
        });

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.persisted-token",
            secretIdProvider: null,
            ldapPasswordProvider: null,
            userpassPasswordProvider: null,
            tokenSaver: null,
            oidcLoginFlow: oidcFlow);

        var act = () => svc.ReadSecretAsync("test", "oidc/path", "password");
        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*network down*");
        browserAuthStarted.Should().BeFalse();
    }

    [Fact]
    public async Task OidcAuth_Ipv6LoopbackHost_NormalizesRedirectUri()
    {
        var settings = CreateSettings(authMethod: VaultAuthMethod.Oidc);
        settings.Profiles[0].OidcRole = "desktop-role";
        settings.Profiles[0].OidcCallbackHost = "::1";

        string? generatedState = null;
        string? redirectUri = null;

        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (path.Contains("auth/oidc/oidc/auth_url", StringComparison.Ordinal))
            {
                var authUrlBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(authUrlBody!);
                generatedState = doc.RootElement.GetProperty("state").GetString();
                redirectUri = doc.RootElement.GetProperty("redirect_uri").GetString();
                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new { auth_url = "https://idp.example.com/authorize" }
                });
            }

            if (path.Contains("auth/oidc/oidc/callback", StringComparison.Ordinal))
            {
                return JsonResponse(HttpStatusCode.OK, new
                {
                    auth = new { client_token = "s.oidc-token", lease_duration = 3600 }
                });
            }

            if (path.Contains("secret/data/oidc/path", StringComparison.Ordinal))
                return KvV2ReadResponse(new Dictionary<string, string> { ["password"] = "ipv6-secret" });

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var oidcFlow = new StubVaultOidcLoginFlow((_, _, _, _, _, _) =>
            Task.FromResult(new VaultOidcLoginResult
            {
                State = generatedState ?? string.Empty,
                Code = "auth-code"
            }));

        using var svc = new VaultService(
            settings,
            handlerFactory: _ => handler,
            tokenProvider: null,
            secretIdProvider: null,
            ldapPasswordProvider: null,
            userpassPasswordProvider: null,
            tokenSaver: null,
            oidcLoginFlow: oidcFlow);

        var value = await svc.ReadSecretAsync("test", "oidc/path", "password");

        value.Should().Be("ipv6-secret");
        redirectUri.Should().Be("http://[::1]:8250/oidc/callback");
    }
}
