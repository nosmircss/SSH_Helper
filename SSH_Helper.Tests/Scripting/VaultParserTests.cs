using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class VaultParserTests
{
    private readonly ScriptParser _parser = new();

    [Fact]
    public void KnownStepCommands_ContainsVault()
    {
        ScriptParser.GetKnownStepCommands().Should().Contain("vault");
    }

    [Fact]
    public void CommandOptionKeys_ContainsVaultOptions()
    {
        var options = ScriptParser.GetKnownStepOptionKeysByCommand();
        options.Should().ContainKey("vault");
        options["vault"].Should().Contain("path")
            .And.Contain("key").And.Contain("keys").And.Contain("into")
            .And.Contain("write").And.Contain("patch")
            .And.Contain("profile").And.Contain("version").And.Contain("on_error");
    }

    [Fact]
    public void Parse_VaultReadSingleKey_Succeeds()
    {
        var yaml = "steps:\n  - vault:\n      path: \"ssh/server\"\n      key: \"password\"\n      into: result";
        var result = _parser.Parse(yaml);
        result.Steps.Should().HaveCount(1);
        result.Steps[0].Vault.Should().NotBeNull();
        result.Steps[0].Vault!.Path.Should().Be("ssh/server");
        result.Steps[0].Vault!.Key.Should().Be("password");
        result.Steps[0].Vault!.Into.Should().Be("result");
    }

    [Fact]
    public void Parse_VaultReadMultipleKeys_Succeeds()
    {
        var yaml = "steps:\n  - vault:\n      path: \"ssh/server\"\n      keys:\n        username: user_var\n        password: pass_var";
        var result = _parser.Parse(yaml);
        result.Steps[0].Vault.Should().NotBeNull();
        result.Steps[0].Vault!.Keys.Should().ContainKey("username").And.ContainKey("password");
    }

    [Fact]
    public void Parse_VaultWrite_Succeeds()
    {
        var yaml = "steps:\n  - vault:\n      path: \"ssh/server\"\n      write:\n        password: new_pass";
        var result = _parser.Parse(yaml);
        result.Steps[0].Vault.Should().NotBeNull();
        result.Steps[0].Vault!.Write.Should().ContainKey("password");
    }

    [Fact]
    public void Parse_VaultPatch_Succeeds()
    {
        var yaml = "steps:\n  - vault:\n      path: \"ssh/server\"\n      patch:\n        password: new_pass";
        var result = _parser.Parse(yaml);
        result.Steps[0].Vault!.Patch.Should().ContainKey("password");
    }

    [Fact]
    public void Parse_VaultWithProfile_Succeeds()
    {
        var yaml = "steps:\n  - vault:\n      profile: network\n      path: \"ssh/switches\"\n      key: password\n      into: result";
        var result = _parser.Parse(yaml);
        result.Steps[0].Vault!.Profile.Should().Be("network");
    }

    [Fact]
    public void Parse_VaultWithVersion_Succeeds()
    {
        var yaml = "steps:\n  - vault:\n      path: \"ssh/server\"\n      version: 3\n      key: password\n      into: result";
        var result = _parser.Parse(yaml);
        result.Steps[0].Vault!.Version.Should().Be(3);
    }
}
