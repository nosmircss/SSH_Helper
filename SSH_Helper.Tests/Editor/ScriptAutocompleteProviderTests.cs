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
    public void GetCompletion_BareDash_DoesNotTriggerStepCommandSuggestions()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  -";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.None);
        completion.Items.Should().BeEmpty();
    }

    [Fact]
    public void GetCompletion_DashFollowedBySpace_TriggersStepCommandSuggestions()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Select(item => item.Label).Should().Contain("send");
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
    public void GetCompletion_LogBlockOptionKey_SuggestsOnlyLogOptions()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - log:\n      message: done\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().BeEquivalentTo(["level", "message"]);
    }

    [Fact]
    public void GetCompletion_StepLevelOptionKey_ExcludesCommandScopedKeys()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - send:\n      command: show version\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain("capture");
        completion.Items.Select(item => item.Label).Should().NotContain("action");
        completion.Items.Select(item => item.Label).Should().NotContain("max_iterations");
    }

    [Fact]
    public void GetCompletion_WhileStepLevelOptionKey_IncludesMaxIterations()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - while:\n      condition: retries < 3\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["do", "max_iterations"]);
        completion.Items.Select(item => item.Label).Should().NotContain("on_error");
    }

    [Fact]
    public void GetCompletion_PrintStepOptionKey_SuggestsMessageOnly()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - print:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().BeEquivalentTo(["message"]);
    }

    [Fact]
    public void GetCompletion_InputStepOptionKey_IncludesTitle()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - input:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["title", "prompt", "into", "default", "password", "validate", "validation_error"]);
    }

    [Fact]
    public void GetCompletion_ChooseStepOptionKey_IncludesTitle()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - choose:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["title", "prompt", "into", "options", "default"]);
    }

    [Fact]
    public void GetCompletion_MultiselectStepOptionKey_IncludesTitle()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - multiselect:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["title", "prompt", "into", "options", "min", "max"]);
    }

    [Fact]
    public void GetCompletion_ConfirmStepOptionKey_IncludesTitle()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - confirm:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["title", "prompt", "into", "default"]);
    }

    [Fact]
    public void GetCompletion_InteractiveStepOptionKey_SuggestsInteractiveOptions()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - interactive:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["session", "title", "command", "capture", "max_seconds", "max_lines", "width", "height", "mirror_output", "show_window", "on_error"]);
        completion.Items.Select(item => item.Label).Should().NotContain("emulation");
    }

    [Fact]
    public void GetCompletion_InteractiveSessionValue_SuggestsSessionEnumValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - interactive:\n      session: s";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["separate", "shared"]);
    }

    [Fact]
    public void GetCompletion_InteractiveEmulationValue_HasNoSuggestions()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - interactive:\n      emulation: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.None);
        completion.Items.Should().BeEmpty();
    }

    [Fact]
    public void GetCompletion_InteractiveMirrorOutputValue_SuggestsBooleanValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - interactive:\n      mirror_output: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["true", "false"]);
    }

    [Fact]
    public void GetCompletion_InteractiveShowWindowValue_SuggestsBooleanValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - interactive:\n      show_window: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["true", "false"]);
    }

    [Fact]
    public void InterpolationTriggers_DollarAndBraces_AreSymmetric()
    {
        var provider = new ScriptAutocompleteProvider(() => new[] { "hostname", "ip" });
        var prelude = """
                       vars:
                         api_token: abc123
                       steps:
                         - set:
                             expression: dynamic_var = 1
                         - send:
                             command: show version
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
                     - set:
                         expression: generated = 1
                     - send:
                         command: show version
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
