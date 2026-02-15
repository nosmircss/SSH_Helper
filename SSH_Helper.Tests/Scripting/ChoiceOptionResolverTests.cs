using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ChoiceOptionResolverTests
{
    [Fact]
    public void Resolve_InlineOptions_SubstitutesLabelAndValue()
    {
        var context = new ScriptContext(new Dictionary<string, string>
        {
            ["iface"] = "port1"
        });

        var resolved = ChoiceOptionResolver.Resolve(
            [new ChoiceOption { Label = "Interface ${iface}", Value = "${iface}" }],
            null,
            context,
            out var error);

        error.Should().BeNull();
        resolved.Should().ContainSingle();
        resolved[0].Label.Should().Be("Interface port1");
        resolved[0].Value.Should().Be("port1");
    }

    [Fact]
    public void Resolve_SourceVariableName_UsesListVariable()
    {
        var context = new ScriptContext();
        context.SetVariable("interface_list", new List<string> { "port1", "port2" });

        var resolved = ChoiceOptionResolver.Resolve(
            [],
            "interface_list",
            context,
            out var error);

        error.Should().BeNull();
        resolved.Select(o => o.Value).Should().Equal("port1", "port2");
    }

    [Fact]
    public void Resolve_SourceVariableToken_UsesListVariable()
    {
        var context = new ScriptContext();
        context.SetVariable("interface_list", new List<string> { "wan1", "wan2" });

        var resolved = ChoiceOptionResolver.Resolve(
            [],
            "${interface_list}",
            context,
            out var error);

        error.Should().BeNull();
        resolved.Select(o => o.Value).Should().Equal("wan1", "wan2");
    }

    [Fact]
    public void Resolve_SourceMissingVariable_ReturnsError()
    {
        var context = new ScriptContext();

        var resolved = ChoiceOptionResolver.Resolve(
            [],
            "missing_list",
            context,
            out var error);

        resolved.Should().BeEmpty();
        error.Should().Contain("did not resolve");
    }
}
