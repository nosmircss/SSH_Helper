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

        await svc.WriteSecretAsync("test", "app/db", new Dictionary<string, string> { ["pass"] = "new" });

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

        await svc.PatchSecretAsync("test", "app/config", new Dictionary<string, string> { ["key"] = "patched" });

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

        await svc.PatchSecretAsync("test", "app/config", new Dictionary<string, string> { ["new_key"] = "new_val" });

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
}
