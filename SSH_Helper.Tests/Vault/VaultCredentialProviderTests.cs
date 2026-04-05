using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using SSH_Helper.Services.Vault;
using Xunit;

namespace SSH_Helper.Tests.Vault;

public class VaultCredentialProviderTests
{
    // --- ParseVaultPath ---

    [Fact]
    public void ParseVaultPath_SimplePath_NullProfileAndDefaultKeys()
    {
        VaultCredentialProvider.ParseVaultPath(
            "ssh/switches",
            out var profile, out var path, out var userKey, out var passKey);

        profile.Should().BeNull();
        path.Should().Be("ssh/switches");
        userKey.Should().Be("username");
        passKey.Should().Be("password");
    }

    [Fact]
    public void ParseVaultPath_ProfileAtPath_ParsesProfileAndPath()
    {
        VaultCredentialProvider.ParseVaultPath(
            "network@ssh/switches",
            out var profile, out var path, out var userKey, out var passKey);

        profile.Should().Be("network");
        path.Should().Be("ssh/switches");
        userKey.Should().Be("username");
        passKey.Should().Be("password");
    }

    [Fact]
    public void ParseVaultPath_CustomKeys_NullProfileAndCustomKeys()
    {
        VaultCredentialProvider.ParseVaultPath(
            "ssh/switches#user_field,pass_field",
            out var profile, out var path, out var userKey, out var passKey);

        profile.Should().BeNull();
        path.Should().Be("ssh/switches");
        userKey.Should().Be("user_field");
        passKey.Should().Be("pass_field");
    }

    [Fact]
    public void ParseVaultPath_ProfileAndCustomKeys_AllParsed()
    {
        VaultCredentialProvider.ParseVaultPath(
            "network@ssh/switches#admin_user,admin_pass",
            out var profile, out var path, out var userKey, out var passKey);

        profile.Should().Be("network");
        path.Should().Be("ssh/switches");
        userKey.Should().Be("admin_user");
        passKey.Should().Be("admin_pass");
    }

    [Fact]
    public void ParseVaultPath_DeepPath_PreservesSlashes()
    {
        VaultCredentialProvider.ParseVaultPath(
            "prod@infra/network/core/switches",
            out var profile, out var path, out _, out _);

        profile.Should().Be("prod");
        path.Should().Be("infra/network/core/switches");
    }

    // --- IsAvailable ---

    [Fact]
    public void IsAvailable_WhenVaultEnabledWithProfiles_ReturnsTrue()
    {
        var settings = new VaultSettings
        {
            Enabled = true,
            Profiles = [new VaultProfileConfig { Name = "test", Address = "https://vault.test:8200" }]
        };
        var vault = new VaultService(settings);
        var provider = new VaultCredentialProvider(vault);

        provider.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_WhenVaultDisabled_ReturnsFalse()
    {
        var settings = new VaultSettings
        {
            Enabled = false,
            Profiles = [new VaultProfileConfig { Name = "test", Address = "https://vault.test:8200" }]
        };
        var vault = new VaultService(settings);
        var provider = new VaultCredentialProvider(vault);

        provider.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void IsAvailable_WhenNoProfiles_ReturnsFalse()
    {
        var settings = new VaultSettings { Enabled = true, Profiles = [] };
        var vault = new VaultService(settings);
        var provider = new VaultCredentialProvider(vault);

        provider.IsAvailable.Should().BeFalse();
    }

    // --- TryGetPassword ---

    [Fact]
    public void TryGetPassword_WhenVaultDisabled_ReturnsFalse()
    {
        var settings = new VaultSettings { Enabled = false };
        var vault = new VaultService(settings);
        var provider = new VaultCredentialProvider(vault);

        var result = provider.TryGetPassword("ssh/switches", out var user, out var pass);

        result.Should().BeFalse();
        user.Should().BeEmpty();
        pass.Should().BeEmpty();
    }

    [Fact]
    public void TryGetPassword_EmptyTarget_ReturnsFalse()
    {
        var settings = CreateEnabledSettings();
        var vault = new VaultService(settings);
        var provider = new VaultCredentialProvider(vault);

        var result = provider.TryGetPassword("", out var user, out var pass);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryGetPassword_VaultReturnsCredentials_ReturnsTrueAndPopulatesOutParams()
    {
        var settings = CreateEnabledSettings("default");
        var vault = new VaultService(
            settings,
            handlerFactory: _ => new DelegatingHandlerStub(req =>
            {
                if (req.RequestUri!.PathAndQuery.Contains("lookup-self"))
                    return TokenLookupResponse();

                if (req.RequestUri.PathAndQuery.Contains("sys/mounts"))
                    return new HttpResponseMessage(HttpStatusCode.Forbidden);

                // KV read: return username and password fields
                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new
                    {
                        data = new { username = "admin", password = "s3cr3t" }
                    }
                });
            }),
            tokenProvider: (_, _) => "test-token");

        var provider = new VaultCredentialProvider(vault);

        var result = provider.TryGetPassword("ssh/switches", out var user, out var pass);

        result.Should().BeTrue();
        user.Should().Be("admin");
        pass.Should().Be("s3cr3t");
    }

    [Fact]
    public void TryGetPassword_DefaultProfileOverride_IsUsedWhenPathHasNoExplicitProfile()
    {
        var settings = new VaultSettings
        {
            Enabled = true,
            DefaultProfileName = "app-default",
            Profiles =
            [
                new VaultProfileConfig
                {
                    Name = "app-default",
                    Address = "https://vault.test:8200",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                },
                new VaultProfileConfig
                {
                    Name = "job-default",
                    Address = "https://vault.test:8200",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                }
            ]
        };

        var vault = new VaultService(
            settings,
            handlerFactory: profileConfig => new DelegatingHandlerStub(req =>
            {
                if (req.RequestUri!.PathAndQuery.Contains("lookup-self", StringComparison.Ordinal))
                    return TokenLookupResponse();

                var payload = profileConfig.Name == "job-default"
                    ? new { username = "job-user", password = "job-pass" }
                    : new { username = "app-user", password = "app-pass" };

                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new
                    {
                        data = payload
                    }
                });
            }),
            tokenProvider: (_, _) => "test-token");

        var provider = new VaultCredentialProvider(vault);

        var result = provider.TryGetPassword("ssh/switches", out var user, out var pass, "job-default");

        result.Should().BeTrue();
        user.Should().Be("job-user");
        pass.Should().Be("job-pass");
    }

    [Fact]
    public void TryGetPassword_ExplicitProfileInPath_WinsOverDefaultProfileOverride()
    {
        var settings = new VaultSettings
        {
            Enabled = true,
            DefaultProfileName = "app-default",
            Profiles =
            [
                new VaultProfileConfig
                {
                    Name = "app-default",
                    Address = "https://vault.test:8200",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                },
                new VaultProfileConfig
                {
                    Name = "job-default",
                    Address = "https://vault.test:8200",
                    AuthMethod = VaultAuthMethod.Token,
                    KvVersion = VaultKvVersion.V2
                }
            ]
        };

        var vault = new VaultService(
            settings,
            handlerFactory: profileConfig => new DelegatingHandlerStub(req =>
            {
                if (req.RequestUri!.PathAndQuery.Contains("lookup-self", StringComparison.Ordinal))
                    return TokenLookupResponse();

                var payload = profileConfig.Name == "app-default"
                    ? new { username = "app-user", password = "app-pass" }
                    : new { username = "job-user", password = "job-pass" };

                return JsonResponse(HttpStatusCode.OK, new
                {
                    data = new
                    {
                        data = payload
                    }
                });
            }),
            tokenProvider: (_, _) => "test-token");

        var provider = new VaultCredentialProvider(vault);

        var result = provider.TryGetPassword("app-default@ssh/switches", out var user, out var pass, "job-default");

        result.Should().BeTrue();
        user.Should().Be("app-user");
        pass.Should().Be("app-pass");
    }

    [Fact]
    public void TryGetPassword_VaultThrows_ReturnsFalse()
    {
        var settings = CreateEnabledSettings("default");
        var vault = new VaultService(
            settings,
            handlerFactory: _ => new DelegatingHandlerStub(_ =>
                new HttpResponseMessage(HttpStatusCode.Unauthorized)),
            tokenProvider: (_, _) => "test-token");

        var provider = new VaultCredentialProvider(vault);

        var result = provider.TryGetPassword("ssh/switches", out var user, out var pass);

        result.Should().BeFalse();
        user.Should().BeEmpty();
        pass.Should().BeEmpty();
    }

    [Fact]
    public void SavePassword_AlwaysReturnsFalse()
    {
        var provider = new VaultCredentialProvider(new VaultService(CreateEnabledSettings()));
        provider.SavePassword("target", "user", "pass").Should().BeFalse();
    }

    [Fact]
    public void DeletePassword_AlwaysReturnsFalse()
    {
        var provider = new VaultCredentialProvider(new VaultService(CreateEnabledSettings()));
        provider.DeletePassword("target").Should().BeFalse();
    }

    // --- Helpers ---

    private static VaultSettings CreateEnabledSettings(string profileName = "default") =>
        new()
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
                    KvVersion = VaultKvVersion.V2
                }
            ]
        };

    private static HttpResponseMessage TokenLookupResponse(int ttl = 3600) =>
        JsonResponse(HttpStatusCode.OK, new
        {
            data = new { ttl, display_name = "token", policies = new[] { "default" } }
        });

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object body) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json")
        };
}
