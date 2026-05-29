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
}
