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
using SSH_Helper.Services.Vault;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class VaultInlineSyntaxTests
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

    [Fact]
    public void InlineVault_PathAndKey_ResolvesSecret()
    {
        var handler = new InlineDelegatingHandlerStub(req =>
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

        var context = new ScriptContext { VaultService = vault };
        var result = context.SubstituteVariables("{{vault:ssh/server#password}}");

        result.Should().Be("s3cret");
    }

    [Fact]
    public void InlineVault_ExplicitProfile_ResolvesSecret()
    {
        var handler = new InlineDelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            if (path.Contains("secret/data/ssh/server"))
                return KvV2ReadResponse(new Dictionary<string, string> { ["password"] = "profiled" });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = new VaultService(
            CreateSettings(),
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.test-token");

        var context = new ScriptContext { VaultService = vault };
        var result = context.SubstituteVariables("{{vault:test@ssh/server#password}}");

        result.Should().Be("profiled");
    }

    [Fact]
    public void InlineVault_VaultError_ResolvesToEmpty_SetsLastError()
    {
        var handler = new InlineDelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = new VaultService(
            CreateSettings(),
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.test-token");

        var context = new ScriptContext { VaultService = vault };
        var result = context.SubstituteVariables("{{vault:missing/path#key}}");

        result.Should().BeEmpty();
        context.GetVariableString("_last_error").Should().NotBeEmpty();
    }

    [Fact]
    public void InlineVault_MissingHashDelimiter_ResolvesToEmpty_SetsLastError()
    {
        var handler = new InlineDelegatingHandlerStub(_ =>
            new HttpResponseMessage(HttpStatusCode.OK));

        using var vault = new VaultService(
            CreateSettings(),
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.test-token");

        var context = new ScriptContext { VaultService = vault };
        var result = context.SubstituteVariables("{{vault:ssh/server-no-hash}}");

        result.Should().BeEmpty();
        context.GetVariableString("_last_error").Should().Contain("#");
    }
}

internal sealed class InlineDelegatingHandlerStub : DelegatingHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public InlineDelegatingHandlerStub(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}
