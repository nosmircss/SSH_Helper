using FluentAssertions;
using SSH_Helper.Services.Editor;
using Xunit;

namespace SSH_Helper.Tests.Editor;

public class ScriptAutocompleteProviderTests
{
    [Fact]
    public void GetCompletion_StepPrefix_UsesParserDrivenStepCommands()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - se";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Select(item => item.Label).Should().Contain("send");
        completion.Items.Select(item => item.Label).Should().NotContain("nonexistent_command");
    }

    [Fact]
    public void GetCompletion_TopLevelPrefix_SuggestsTopLevelKeys()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "na";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.TopLevelKey);
        completion.Items.Select(item => item.Label).Should().Contain("name");
    }

    [Fact]
    public void GetCompletion_OptionValue_SuggestsEnumLikeValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - http:\n      method: P";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["POST", "PUT", "PATCH"]);
    }

    [Fact]
    public void InterpolationTriggers_DollarAndBraces_AreSymmetric()
    {
        var provider = new ScriptAutocompleteProvider(() => new[] { "hostname", "ip" });
        var prelude = """
                      vars:
                        api_token: abc123
                      steps:
                        - set: dynamic_var = 1
                        - send: show version
                          capture: output
                        - http:
                            into: response
                      """;

        var dollarText = prelude + Environment.NewLine + "  - print: ${";
        var braceText = prelude + Environment.NewLine + "  - print: {{";

        var dollar = provider.GetCompletion(dollarText, dollarText.Length);
        var braces = provider.GetCompletion(braceText, braceText.Length);

        var dollarLabels = dollar.Items.Select(item => item.Label).OrderBy(label => label).ToArray();
        var braceLabels = braces.Items.Select(item => item.Label).OrderBy(label => label).ToArray();

        dollar.Context.Should().Be(CompletionContextKind.Interpolation);
        braces.Context.Should().Be(CompletionContextKind.Interpolation);
        dollarLabels.Should().BeEquivalentTo(braceLabels);
    }

    [Fact]
    public void ExtractDynamicSymbols_IncludesVarsSetCaptureAndInto()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = """
                   vars:
                     static_var: abc
                   steps:
                     - set: generated = 1
                     - send: show version
                       capture: command_output
                     - http:
                         into: response
                     - extract:
                         from: command_output
                         pattern: "(.+)"
                         into: [first_match, second_match]
                   """;

        var symbols = provider.ExtractDynamicSymbols(text);

        symbols.Should().Contain("static_var");
        symbols.Should().Contain("generated");
        symbols.Should().Contain("command_output");
        symbols.Should().Contain("response");
        symbols.Should().Contain("response_status");
        symbols.Should().Contain("first_match");
        symbols.Should().Contain("second_match");
    }

    [Fact]
    public void GetInterpolationSymbols_IncludesBuiltInsAndHostColumns()
    {
        var provider = new ScriptAutocompleteProvider(() => new[] { "hostname", "port" });

        var symbols = provider.GetInterpolationSymbols("steps:\n  - print: test");

        symbols.Should().Contain("_timestamp");
        symbols.Should().Contain("_output");
        symbols.Should().Contain("hostname");
        symbols.Should().Contain("port");
    }
}
