using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Functions;
using SSH_Helper.Services.Vault;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class VaultFunctionsTests
{
    private static VaultSettings CreateSettings(string profileName = "test")
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
                    Address = "https://vault.test:8200",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2,
                    CacheTtlSeconds = 300
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

    private static HttpResponseMessage TokenLookupResponse()
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            data = new { ttl = 3600, display_name = "token", policies = new[] { "default" } }
        });
    }

    private static HttpResponseMessage KvV2ReadResponse(Dictionary<string, string> data)
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            data = new { data, metadata = new { version = 1 } }
        });
    }

    private static HttpResponseMessage ListResponse(List<string> keys)
    {
        return JsonResponse(HttpStatusCode.OK, new
        {
            data = new { keys }
        });
    }

    [Fact]
    public void Vault_ReturnsSecretValue()
    {
        var handler = new FnDelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            if (path.Contains("secret/data/ssh/server"))
                return KvV2ReadResponse(new Dictionary<string, string> { ["password"] = "s3cret" });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = new VaultService(
            CreateSettings(),
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.test-token");

        var registry = new FunctionRegistry();
        var vaultFns = new VaultFunctions();
        vaultFns.Register(registry);

        var context = new ScriptContext { VaultService = vault };

        registry.TryEvaluate("vault", "\"ssh/server\", \"password\"", context, out var result)
            .Should().BeTrue();
        result.Should().Be("s3cret");
    }

    [Fact]
    public void VaultClearCache_ReturnsTrue()
    {
        var handler = new FnDelegatingHandlerStub(_ =>
            new HttpResponseMessage(HttpStatusCode.OK));

        using var vault = new VaultService(
            CreateSettings(),
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.test-token");

        var registry = new FunctionRegistry();
        var vaultFns = new VaultFunctions();
        vaultFns.Register(registry);

        var context = new ScriptContext { VaultService = vault };

        registry.TryEvaluate("vault_clear_cache", "", context, out var result)
            .Should().BeTrue();
        result.Should().Be(true);
    }

    [Fact]
    public void VaultList_ReturnsListOfPaths()
    {
        var handler = new FnDelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            if (path.Contains("secret/metadata/ssh"))
                return ListResponse(new List<string> { "server1", "server2" });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = new VaultService(
            CreateSettings(),
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.test-token");

        var registry = new FunctionRegistry();
        var vaultFns = new VaultFunctions();
        vaultFns.Register(registry);

        var context = new ScriptContext { VaultService = vault };

        registry.TryEvaluate("vault_list", "\"ssh\"", context, out var result)
            .Should().BeTrue();
        result.Should().BeOfType<List<string>>()
            .Which.Should().Contain("server1").And.Contain("server2");
    }

    [Fact]
    public void VaultFunctions_RegisteredInSingleton()
    {
        var registry = FunctionRegistry.Instance;
        registry.IsRegistered("vault").Should().BeTrue();
        registry.IsRegistered("vault_list").Should().BeTrue();
        registry.IsRegistered("vault_clear_cache").Should().BeTrue();
    }
}

internal sealed class FnDelegatingHandlerStub : DelegatingHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FnDelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
