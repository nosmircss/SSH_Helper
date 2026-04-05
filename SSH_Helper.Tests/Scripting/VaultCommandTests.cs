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
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Vault;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class VaultCommandTests
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

    private static VaultService CreateVaultService(
        DelegatingHandler handler, string profileName = "test")
    {
        return new VaultService(
            CreateSettings(profileName),
            handlerFactory: _ => handler,
            tokenProvider: (_, _) => "s.test-token");
    }

    [Fact]
    public async Task ReadSingleKey_SetsVariable()
    {
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            if (path.Contains("secret/data/ssh/server"))
                return KvV2ReadResponse(new Dictionary<string, string> { ["password"] = "s3cret" });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = CreateVaultService(handler);
        var command = new VaultCommand();
        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "ssh/server",
                Key = "password",
                Into = "result"
            }
        };

        var context = new ScriptContext { VaultService = vault };
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("result").Should().Be("s3cret");
    }

    [Fact]
    public async Task ReadMultipleKeys_SetsAllVariables()
    {
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            if (path.Contains("secret/data/ssh/server"))
                return KvV2ReadResponse(new Dictionary<string, string>
                {
                    ["username"] = "admin",
                    ["password"] = "s3cret"
                });
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = CreateVaultService(handler);
        var command = new VaultCommand();
        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "ssh/server",
                Keys = new Dictionary<string, string>
                {
                    ["username"] = "user_var",
                    ["password"] = "pass_var"
                }
            }
        };

        var context = new ScriptContext { VaultService = vault };
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("user_var").Should().Be("admin");
        context.GetVariableString("pass_var").Should().Be("s3cret");
    }

    [Fact]
    public async Task Write_Succeeds()
    {
        string? capturedBody = null;
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            if (path.Contains("secret/data/ssh/server") && req.Method == HttpMethod.Post)
            {
                capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse(HttpStatusCode.OK, new { data = new { version = 1 } });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = CreateVaultService(handler);
        var command = new VaultCommand();
        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "ssh/server",
                Write = new Dictionary<string, string>
                {
                    ["password"] = "new-pass"
                }
            }
        };

        var context = new ScriptContext { VaultService = vault };
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("new-pass");
    }

    [Fact]
    public async Task Patch_Succeeds()
    {
        string? capturedBody = null;
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            // V2 PATCH attempt
            if (path.Contains("secret/data/ssh/server") && req.Method == HttpMethod.Patch)
            {
                capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse(HttpStatusCode.OK, new { data = new { version = 2 } });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = CreateVaultService(handler);
        var command = new VaultCommand();
        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "ssh/server",
                Patch = new Dictionary<string, string>
                {
                    ["password"] = "patched-pass"
                }
            }
        };

        var context = new ScriptContext { VaultService = vault };
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("patched-pass");
    }

    [Fact]
    public async Task OnErrorContinue_SetsLastError_ReturnsSuccess()
    {
        var handler = new DelegatingHandlerStub(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.Contains("auth/token/lookup-self"))
                return TokenLookupResponse();
            // Return 404 for the secret read to trigger VaultException
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var vault = CreateVaultService(handler);
        var command = new VaultCommand();
        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "missing/secret",
                Key = "nope",
                Into = "result",
                OnError = "continue"
            }
        };

        var context = new ScriptContext { VaultService = vault };
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariableString("_last_error").Should().NotBeEmpty();
    }

    [Fact]
    public async Task MissingPath_Fails()
    {
        var handler = new DelegatingHandlerStub(_ =>
            new HttpResponseMessage(HttpStatusCode.OK));

        using var vault = CreateVaultService(handler);
        var command = new VaultCommand();
        var step = new ScriptStep
        {
            Vault = new VaultStepOptions
            {
                Path = "",
                Key = "something",
                Into = "result"
            }
        };

        var context = new ScriptContext { VaultService = vault };
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("path");
    }
}

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
