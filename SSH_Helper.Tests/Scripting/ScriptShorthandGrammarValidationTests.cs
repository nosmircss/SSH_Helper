using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

/// <summary>
/// Parse-time grammar validation for the set/foreach/exit shorthand forms
/// (Proposal C, sub-feature 3). Malformed forms must fail validation before
/// execution instead of deferring failure to runtime against real hosts.
/// </summary>
public class ScriptShorthandGrammarValidationTests
{
    private readonly ScriptParser _parser = new();

    // ---- foreach iterator grammar (task 3.1) ----

    [Fact]
    public void Validate_ForeachShorthand_MissingInKeyword_IsBlockingError()
    {
        const string yaml = """
            ---
            steps:
              - foreach: items
                do:
                  - print: hi
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Invalid foreach syntax") && e.Contains("item in collection"));
    }

    [Fact]
    public void Validate_ForeachMappingIterator_Malformed_IsBlockingError()
    {
        const string yaml = """
            ---
            steps:
              - foreach:
                  iterator: items
                  do:
                    - print: hi
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Invalid foreach syntax"));
    }

    [Fact]
    public void Validate_ForeachShorthand_SingleForm_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - foreach: item in items
                do:
                  - print: "{{item}}"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Invalid foreach syntax"));
    }

    [Fact]
    public void Validate_ForeachShorthand_DictForm_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - foreach: key, value in mymap
                do:
                  - print: "{{key}}"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Invalid foreach syntax"));
    }

    [Fact]
    public void Validate_Foreach_InterpolatedCollection_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - foreach: "ip in {{ips}}"
                do:
                  - print: "{{ip}}"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Invalid foreach syntax"));
    }

    [Fact]
    public void Validate_Foreach_FunctionCallCollection_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - foreach: tag in json.items(response, "data.tags")
                do:
                  - print: "{{tag}}"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Invalid foreach syntax"));
    }

    // ---- set assignment grammar (task 3.2) ----

    [Fact]
    public void Validate_SetShorthand_MissingName_IsBlockingError()
    {
        const string yaml = """
            ---
            steps:
              - set: "= 5"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("variable name"));
    }

    [Fact]
    public void Validate_SetShorthand_EmptyValue_IsAccepted_AsInitializeToEmpty()
    {
        // "x = " is a deliberate initialize-to-empty assignment that the runtime supports
        // (e.g. resetting a capture variable before a send). It must not be rejected.
        const string yaml = """
            ---
            steps:
              - set: "counter ="
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Set requires"));
    }

    [Fact]
    public void Validate_SetShorthand_WellFormed_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - set: counter = 5
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SetShorthand_NestedAssignment_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - set: obj.field = value
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Set requires"));
    }

    [Fact]
    public void Validate_SetShorthand_EqualityOperatorInValue_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - set: result = a == b
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Set requires"));
    }

    [Fact]
    public void Validate_SetShorthand_EmptyStringValue_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - set: 'msg = ""'
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().NotContain(e => e.Contains("Set requires"));
    }

    // ---- nested-block coverage: validation must reach steps inside every block-bearing command ----

    [Fact]
    public void Validate_MalformedForeach_InsideRepeatBlock_IsBlockingError()
    {
        const string yaml = """
            ---
            steps:
              - repeat:
                  until: "true"
                  do:
                    - foreach: items
                      do:
                        - print: hi
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Invalid foreach syntax"));
    }

    [Fact]
    public void Validate_MalformedSet_InsideRepeatBlock_IsBlockingError()
    {
        const string yaml = """
            ---
            steps:
              - repeat:
                  until: "true"
                  do:
                    - set: "= 5"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("variable name"));
    }

    // ---- exit shorthand (task 3.3): free-text by design; well-formed forms accepted ----

    [Fact]
    public void Validate_ExitShorthand_StatusOnly_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - exit: success
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ExitShorthand_StatusWithMessage_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - exit: failure "deploy aborted"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ExitShorthand_PlainMessage_IsAccepted()
    {
        const string yaml = """
            ---
            steps:
              - exit: all checks passed
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().BeEmpty();
    }
}
