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
    public void GetCompletion_StepPrefixWithoutDash_WithinSteps_SuggestsStepCommandsAndPrependsListMarker()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  se";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        var sendItem = completion.Items.Single(item => item.Label == "send");
        sendItem.InsertText.Should().Be("- send");
        completion.ReplaceStart.Should().Be("steps:\n  ".Length);
        completion.ReplaceLength.Should().Be(2);
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
    public void GetCompletion_TopLevelEnvironmentPrefix_SuggestsEnvironment()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "en";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.TopLevelKey);
        completion.Items.Select(item => item.Label).Should().Contain("environment");
    }

    [Fact]
    public void GetCompletion_BlankTopLevelLine_BeforeVarsAndSteps_AutoRequest_SuggestsRootKeys()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "name: demo\nversion: 1\n";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.TopLevelKey);
        completion.Items.Select(item => item.Label).Should().Contain("steps");
        completion.Items.Select(item => item.Label).Should().Contain("name");
    }

    [Fact]
    public void GetCompletion_BlankTopLevelLine_AfterVarsAndSteps_AutoRequest_DoesNotSuggestRootKeys()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "name: demo\nvars:\n  token: abc\nsteps:\n  - send: ok\n";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.None);
        completion.Items.Should().BeEmpty();
    }

    [Fact]
    public void GetCompletion_BlankTopLevelLine_AfterVarsAndSteps_ManualRequest_SuggestsStepCommands()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "name: demo\nvars:\n  token: abc\nsteps:\n  - send: ok\n";

        var completion = provider.GetCompletion(text, text.Length, manualRequest: true);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Select(item => item.Label).Should().Contain("send");
        completion.Items.Should().OnlyContain(item => item.InsertText.StartsWith("  - ", StringComparison.Ordinal));
    }

    [Fact]
    public void GetCompletion_BlankLine_AfterIndentlessStepsSequence_ManualRequest_SuggestsStepCommands()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n- send:\n    command: df\n\n- extract:\n    from: ${Host_IP}\n    into: foo\n    pattern: .*\n\n";

        var completion = provider.GetCompletion(text, text.Length, manualRequest: true);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Select(item => item.Label).Should().Contain("send");
        completion.Items.Should().OnlyContain(item => item.InsertText.StartsWith("- ", StringComparison.Ordinal));
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
    public void GetCompletion_DnsTypeValue_SuggestsDnsTypeValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - dns:\n      type: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["A", "AAAA", "PTR"]);
        completion.Items.Select(item => item.Label).Should().NotContain("directory");
    }

    [Fact]
    public void GetCompletion_ExistsTypeValue_SuggestsExistsTypeValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - exists:\n      type: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["any", "file", "directory"]);
        completion.Items.Select(item => item.Label).Should().NotContain("AAAA");
    }

    [Fact]
    public void GetCompletion_InputPasswordValue_SuggestsBooleanValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - input:\n      password: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["true", "false"]);
    }

    [Fact]
    public void GetCompletion_SftpPasswordValue_DoesNotSuggestBooleanValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - sftp:\n      password: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.None);
        completion.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("extract", "required")]
    [InlineData("send", "suppress")]
    [InlineData("readfile", "skip_empty_lines")]
    [InlineData("readfile", "trim_lines")]
    [InlineData("writefile", "pretty")]
    [InlineData("sftp", "overwrite")]
    public void GetCompletion_BooleanLikeOptionValues_SuggestsTrueFalse(string command, string key)
    {
        var provider = new ScriptAutocompleteProvider();
        var text = $"steps:\n  - {command}:\n      {key}: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["true", "false"]);
    }

    [Theory]
    [InlineData("debug")]
    [InlineData("nobanner")]
    [InlineData("compact_errors")]
    [InlineData("suppress_missing_column_warning")]
    [InlineData("library")]
    public void GetCompletion_TopLevelBooleanKeys_SuggestsTrueFalse(string key)
    {
        var provider = new ScriptAutocompleteProvider();
        var text = $"{key}: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["true", "false"]);
    }

    [Fact]
    public void GetCompletion_StepPrefix_IncludesNewCommandKeywords()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - pl";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Select(item => item.Label).Should().Contain("playsound");
    }

    [Fact]
    public void GetCompletion_StepPrefix_LocalCmd_ShowsDetailText()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - lo";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Should().Contain(item =>
            item.Label == "localcmd" &&
            item.Detail == "run local cmd");
    }

    [Fact]
    public void GetCompletion_StepPrefix_SetHistoryLabel_ShowsDetailText()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - seth";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Should().Contain(item =>
            item.Label == "sethistorylabel" &&
            item.Detail == "Label history entry. Scalar 'sethistorylabel: name' or object form { value, replace, mode, separator }");
    }

    [Fact]
    public void GetCompletion_SetHistoryLabelStepOptionKey_SuggestsValueReplaceModeAndSeparator()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - sethistorylabel:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["value", "replace", "mode", "separator"]);
    }

    [Fact]
    public void GetCompletion_SetHistoryLabelModeValue_SuggestsKnownModes()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - sethistorylabel:\n      mode: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["replace", "append", "prepend", "clear"]);
    }

    [Fact]
    public void GetCompletion_StepPrefix_Notify_ShowsDetailText()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - not";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepCommand);
        completion.Items.Should().Contain(item =>
            item.Label == "notify" &&
            item.Detail == "Send a notification via Slack/Teams/Discord webhook, Teams Adaptive Card, Windows toast, or SMTP email");
    }

    [Fact]
    public void GetCompletion_NotifyStepOptionKey_SuggestsNotifyFields()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - notify:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["profile", "channel", "title", "message", "level", "mention", "attachments", "into", "on_error"]);
    }

    [Fact]
    public void GetCompletion_NotifyLevelValue_SuggestsKnownLevels()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - notify:\n      level: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["info", "warn", "error", "success"]);
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
    public void GetCompletion_SendStepOptionKey_IncludesRetryAndRespondOptions()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - send:\n      command: show version\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["retry", "retry_delay", "fail_on_nonzero", "respond"]);
    }

    [Fact]
    public void GetCompletion_SendFailOnNonZeroValue_SuggestsBooleanValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - send:\n      fail_on_nonzero: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["true", "false"]);
    }

    [Fact]
    public void GetCompletion_LocalCmdRunModeValue_SuggestsForegroundAndBackground()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - localcmd:\n      run_mode: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["foreground", "background"]);
    }

    [Fact]
    public void GetCompletion_LocalCmdConfirmValue_SuggestsAllowedConfirmModes()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - localcmd:\n      confirm: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["always", "once", "never"]);
    }

    [Fact]
    public void GetCompletion_LocalCmdShellValue_SuggestsPowershellAndCustomOnly()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - localcmd:\n      shell: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["powershell", "custom"]);
        completion.Items.Select(item => item.Label).Should().NotContain("cmd");
    }

    [Fact]
    public void GetCompletion_SendRespondNestedOptionKey_SuggestsExpectAndReply()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = """
                   steps:
                     - send:
                         command: adduser qa
                         respond:
                           -
                             
                   """;

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["expect", "reply"]);
    }

    [Fact]
    public void GetCompletion_SwitchShorthandSiblingKeys_IncludeCasesAndElse()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - switch: \"${mode}\"\n    ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["cases", "else"]);
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
        completion.Items.Should().Contain(item => item.Label == "into" && item.Detail == "required");
        completion.Items.Should().Contain(item => item.Label == "options" && item.Detail == "required");
    }

    [Fact]
    public void GetCompletion_MultiselectStepOptionKey_IncludesTitle()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - multiselect:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["title", "prompt", "into", "options", "min", "max"]);
        completion.Items.Should().Contain(item => item.Label == "into" && item.Detail == "required");
        completion.Items.Should().Contain(item => item.Label == "options" && item.Detail == "required");
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
    public void GetCompletion_ReadfileStepOptionKey_IncludesPickerCustomizationOptions()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - readfile:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["path", "select_file", "autobrowse", "message", "fileext", "path_into", "path_only", "into", "skip_empty_lines", "trim_lines", "max_lines", "encoding"]);
        completion.Items.Should().Contain(item => item.Label == "path" && item.Detail == "required");
        completion.Items.Should().Contain(item => item.Label == "into" && item.Detail == "required");
    }

    [Theory]
    [MemberData(nameof(GetRequiredOptionTagCases))]
    public void GetCompletion_CommandStepOptionKey_MarksAuditedRequiredOptions(
        string command,
        string[] expectedRequiredKeys)
    {
        var provider = new ScriptAutocompleteProvider();
        var text = $"steps:\n  - {command}:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);

        foreach (var requiredKey in expectedRequiredKeys)
        {
            completion.Items.Should().Contain(item =>
                string.Equals(item.Label, requiredKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.Detail, "required", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void GetCompletion_ReadfileSelectFileValue_SuggestsBooleanValues()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - readfile:\n      select_file: ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.OptionValue);
        completion.Items.Select(item => item.Label).Should().Contain(["true", "false"]);
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

        symbols.Should().Contain("_prompt");
        symbols.Should().Contain("_timestamp");
        symbols.Should().Contain("_output");
        symbols.Should().Contain("_outputwindow");
        symbols.Should().Contain("hostname");
        symbols.Should().Contain("port");
    }

    [Fact]
    public void GetCompletion_InterpolationPrefix_SuggestsPromptBuiltInWithDescription()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - print:\n      message: \"${_pr\"";

        var completion = provider.GetCompletion(text, text.Length - 1);

        completion.Context.Should().Be(CompletionContextKind.Interpolation);
        completion.Items.Should().Contain(item => item.Label == "_prompt" && item.Detail == "Current SSH prompt");
    }

    [Fact]
    public void GetCompletion_InterpolationPrefix_SuggestsOutputWindowBuiltInWithDescription()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - print:\n      message: \"${_outputw\"";

        var completion = provider.GetCompletion(text, text.Length - 1);

        completion.Context.Should().Be(CompletionContextKind.Interpolation);
        completion.Items.Should().Contain(item => item.Label == "_outputwindow" && item.Detail == "Output window transcript");
    }

    public static IEnumerable<object[]> GetRequiredOptionTagCases()
    {
        yield return new object[] { "send", new[] { "command" } };
        yield return new object[] { "if", new[] { "condition", "then" } };
        yield return new object[] { "foreach", new[] { "iterator", "do" } };
        yield return new object[] { "while", new[] { "condition", "do" } };
        yield return new object[] { "exists", new[] { "path", "into" } };
        yield return new object[] { "choose", new[] { "into", "options" } };
        yield return new object[] { "multiselect", new[] { "into", "options" } };
        yield return new object[] { "confirm", new[] { "into" } };
        yield return new object[] { "assert", new[] { "condition" } };
        yield return new object[] { "switch", new[] { "value", "cases" } };
        yield return new object[] { "browser_callback_capture", new[] { "start_url", "callback_path", "into" } };
        yield return new object[] { "localcmd", new[] { "command" } };
    }
}
