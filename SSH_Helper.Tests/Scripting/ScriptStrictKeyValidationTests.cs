using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptStrictKeyValidationTests
{
    private readonly ScriptParser _parser = new();

    [Fact]
    public void Validate_UnknownStepOptionKey_IsBlockingError()
    {
        const string yaml = """
            ---
            steps:
              - send:
                  command: show version
                  tieout: 5
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("tieout"));
    }

    [Fact]
    public void Validate_UnknownStepOptionKey_SuggestsClosestKnownKey()
    {
        const string yaml = """
            ---
            steps:
              - send:
                  command: show version
                  tieout: 5
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Did you mean 'timeout'?"));
    }

    [Fact]
    public void Validate_DistantUnknownKey_HasNoSuggestion()
    {
        const string yaml = """
            ---
            steps:
              - send:
                  command: show version
                  zzzzzzzz: 5
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("zzzzzzzz"));
        errors.Should().NotContain(e => e.Contains("Did you mean"));
    }

    // ---- misspelled command (task 1.3) ----

    [Fact]
    public void Validate_MisspelledCommand_IsBlockingError()
    {
        const string yaml = """
            ---
            steps:
              - snd: echo hello
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("snd"));
    }

    [Fact]
    public void Validate_MisspelledCommand_SuggestsClosestCommand()
    {
        const string yaml = """
            ---
            steps:
              - snd: echo hello
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Did you mean 'send'?"));
    }

    [Fact]
    public void Validate_MisspelledCommand_DoesNotAlsoReportGenericNoCommand()
    {
        const string yaml = """
            ---
            steps:
              - snd: echo hello
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        // The misspelled-command diagnostic carries the suggestion; the generic
        // "no recognized command" fallback would be redundant noise here.
        errors.Should().NotContain(e => e.Contains("no recognized command"));
    }

    [Fact]
    public void Validate_DistantUnknownCommand_HasNoSuggestion()
    {
        const string yaml = """
            ---
            steps:
              - zzzzzzzz: echo hello
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("zzzzzzzz"));
        errors.Should().NotContain(e => e.Contains("Did you mean"));
    }

    [Fact]
    public void Validate_StepWithoutAnyCommand_StillReportsNoRecognizedCommand()
    {
        // A step whose only key is a recognized block key (no command) has no
        // misspelled token to suggest, so the generic fallback must remain.
        const string yaml = """
            ---
            steps:
              - then:
                  - print: hi
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("no recognized command"));
    }

    [Fact]
    public void Validate_FlowSiblingSteps_MisspelledCommandDoesNotSuppressSiblingNoCommandError()
    {
        // Flow-style inline maps share a physical line; suppression must be scoped per-step,
        // so the misspelled command must not hide a sibling command-less step's error.
        const string yaml = """
            ---
            steps: [{when: "true"}, {snd: x}]
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Did you mean 'send'?"));
        errors.Should().Contain(e => e.Contains("no recognized command"));
    }

    [Fact]
    public void Validate_MisspelledCommandOnLaterLine_StillCollapsesToSingleDiagnostic()
    {
        // The misspelled command is not on the step's mapping-start line; suppression must
        // still apply to this step (scoped per-step, not by line number).
        const string yaml = """
            ---
            steps:
              - when: "true"
                snd: echo hi
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Did you mean 'send'?"));
        errors.Should().NotContain(e => e.Contains("no recognized command"));
    }

    [Fact]
    public void Validate_UnknownCommandNamedLikeDeprecated_IsStillBlocking()
    {
        // The unknown-key message embeds the user-supplied key name; a key literally named
        // "deprecated" must not be downgraded to a non-blocking warning.
        const string yaml = """
            ---
            steps:
              - deprecated: echo hello
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("deprecated"));
    }

    [Fact]
    public void Validate_UnknownOptionKeyNamedLikeDeprecated_IsStillBlocking()
    {
        const string yaml = """
            ---
            steps:
              - send:
                  command: show version
                  deprecated: true
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("deprecated"));
    }

    [Fact]
    public void Validate_FlowSiblingSteps_CommandLessBlockStep_StillReportsNoCommand()
    {
        // A misspelled-command step and a command-less block step sharing a physical line:
        // suppression must be per-step, so the block step's error is not swallowed.
        const string yaml = """
            ---
            steps: [{snd: x}, {then: [{print: hi}]}]
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Did you mean 'send'?"));
        errors.Should().Contain(e => e.Contains("no recognized command"));
    }
}
