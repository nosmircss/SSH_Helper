using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

/// <summary>
/// Tests for the ScriptParser class.
/// </summary>
public class ScriptParserTests
{
    private readonly ScriptParser _parser;

    public ScriptParserTests()
    {
        _parser = new ScriptParser();
    }

    #region IsYamlScript Detection Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsYamlScript_NullOrEmpty_ReturnsFalse(string? input)
    {
        var result = ScriptParser.IsYamlScript(input!);
        result.Should().BeFalse();
    }

    [Fact]
    public void IsYamlScript_PlainCommands_ReturnsFalse()
    {
        var input = @"show version
show interfaces
show ip route";

        var result = ScriptParser.IsYamlScript(input);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsYamlScript_YamlDocumentMarker_ReturnsTrue()
    {
        var input = @"---
name: Test Script
steps:
  - send: test";

        var result = ScriptParser.IsYamlScript(input);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("vars:\n  test: value")]
    [InlineData("steps:\n  - send: test")]
    [InlineData("preconnect:\n  - set: bootstrap = ready")]
    public void IsYamlScript_ScriptSections_ReturnsTrue(string input)
    {
        var result = ScriptParser.IsYamlScript(input);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("name: Test Script")]
    [InlineData("description: Uplink to core")]
    [InlineData("version: 1")]
    [InlineData("environment: prod")]
    [InlineData("compact_errors: true")]
    [InlineData("suppress_missing_column_warning: true")]
    public void IsYamlScript_MetadataOnlyKeywords_ReturnsFalse(string input)
    {
        var result = ScriptParser.IsYamlScript(input);
        result.Should().BeFalse();
    }

    [Fact]
    public void IsYamlScript_PlainCommandsWithNameAndDescription_ReturnsFalse()
    {
        var input = @"name: Ethernet1/1
description: Uplink to core
show interface status";

        var result = ScriptParser.IsYamlScript(input);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("- send: test command")]
    [InlineData("- print: Hello")]
    [InlineData("- wait: 1000")]
    [InlineData("- set: var = value")]
    [InlineData("- exit: success")]
    [InlineData("- extract:\n    from: output")]
    [InlineData("- if: condition\n  then:")]
    [InlineData("- foreach: item in items\n  do:")]
    [InlineData("- while: condition\n  do:")]
    [InlineData("- break: true")]
    [InlineData("- continue: true")]
    [InlineData("- try:\n  - print: test")]
    [InlineData("- updatecolumn:\n    column: test")]
    [InlineData("- updateenvironment:\n    variable: token")]
    [InlineData("- playsound:\n    path: C:\\temp\\alert.mp3")]
    [InlineData("- interactive:\n    session: separate")]
    public void IsYamlScript_StepSyntax_ReturnsTrue(string input)
    {
        var result = ScriptParser.IsYamlScript(input);
        result.Should().BeTrue();
    }

    [Fact]
    public void IsYamlScript_CommentsOnly_ReturnsFalse()
    {
        var input = @"# This is a comment
# Another comment
show version";

        var result = ScriptParser.IsYamlScript(input);

        result.Should().BeFalse();
    }

    #endregion

    #region Parse Basic Script Tests

    [Fact]
    public void Parse_MinimalScript_ReturnsScript()
    {
        var yaml = @"---
steps:
  - send: test command";

        var script = _parser.Parse(yaml);

        script.Should().NotBeNull();
        script.Steps.Should().HaveCount(1);
        script.Steps[0].Send.Should().Be("test command");
    }

    [Fact]
    public void Parse_ScriptWithName_ParsesName()
    {
        var yaml = @"---
name: My Test Script
steps:
  - send: test";

        var script = _parser.Parse(yaml);

        script.Name.Should().Be("My Test Script");
    }

    [Fact]
    public void Parse_ScriptWithDescription_ParsesDescription()
    {
        var yaml = @"---
description: This script does something
steps:
  - send: test";

        var script = _parser.Parse(yaml);

        script.Description.Should().Be("This script does something");
    }

    [Fact]
    public void Parse_ScriptWithVersion_ParsesVersion()
    {
        var yaml = @"---
version: 2
steps:
  - send: test";

        var script = _parser.Parse(yaml);

        script.Version.Should().Be(2);
    }

    [Fact]
    public void Parse_ScriptWithEnvironment_ParsesEnvironmentWithoutWarnings()
    {
        var yaml = @"---
environment: prod
steps:
  - print: test";

        var script = _parser.Parse(yaml);

        script.Environment.Should().Be("prod");
        _parser.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ScriptWithDebugTrue_ParsesDebug()
    {
        var yaml = @"---
debug: true
steps:
  - send: test";

        var script = _parser.Parse(yaml);

        script.Debug.Should().BeTrue();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("yes")]
    [InlineData("1")]
    public void Parse_DebugVariations_AllParsedAsTrue(string debugValue)
    {
        var yaml = $@"---
debug: {debugValue}
steps:
  - send: test";

        var script = _parser.Parse(yaml);

        script.Debug.Should().BeTrue();
    }

    [Fact]
    public void Parse_ScriptWithSuppressMissingColumnWarning_ParsesFlag()
    {
        var yaml = @"---
suppress_missing_column_warning: true
steps:
  - print: test";

        var script = _parser.Parse(yaml);

        script.SuppressMissingColumnWarning.Should().BeTrue();
    }

    [Fact]
    public void Parse_ScriptWithCompactErrors_ParsesFlag()
    {
        var yaml = @"---
compact_errors: true
steps:
  - print: test";

        var script = _parser.Parse(yaml);

        script.CompactErrors.Should().BeTrue();
    }

    [Fact]
    public void Parse_ScriptWithPreconnect_ParsesPreconnectSteps()
    {
        var yaml = """
            ---
            preconnect:
              - set: bootstrap = "ready"
            steps:
              - print: "${bootstrap}"
            """;

        var script = _parser.Parse(yaml);

        script.Preconnect.Should().HaveCount(1);
        script.Preconnect[0].Set.Should().Be("bootstrap = \"ready\"");
        script.Steps.Should().HaveCount(1);
    }

    #endregion

    #region Parse Variables Tests

    [Fact]
    public void Parse_ScriptWithVars_ParsesVariables()
    {
        var yaml = @"---
vars:
  username: admin
  timeout: 30
steps:
  - send: test";

        var script = _parser.Parse(yaml);

        script.Vars.Should().ContainKey("username");
        script.Vars["username"].Should().Be("admin");
        script.Vars.Should().ContainKey("timeout");
        script.Vars["timeout"].Should().Be("30");
    }

    [Fact]
    public void Parse_VarsWithList_ParsesAsList()
    {
        var yaml = @"---
vars:
  commands:
    - show version
    - show interfaces
steps:
  - send: test";

        var script = _parser.Parse(yaml);

        script.Vars["commands"].Should().BeAssignableTo<List<string>>();
        var commands = script.Vars["commands"] as List<string>;
        commands.Should().HaveCount(2);
        commands.Should().Contain("show version");
    }

    #endregion

    #region Parse Step Types Tests

    [Fact]
    public void Parse_SendStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - send: show version";

        var script = _parser.Parse(yaml);

        script.Steps[0].Send.Should().Be("show version");
    }

    [Fact]
    public void Parse_PrintStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - print: Hello World";

        var script = _parser.Parse(yaml);

        script.Steps[0].Print.Should().Be("Hello World");
    }

    [Fact]
    public void Parse_WaitStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - wait: 2000";

        var script = _parser.Parse(yaml);

        script.Steps[0].Wait.Should().Be(2000);
    }

    [Fact]
    public void Parse_SetStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - set: myvar = value";

        var script = _parser.Parse(yaml);

        script.Steps[0].Set.Should().Be("myvar = value");
    }

    [Fact]
    public void Parse_SetHistoryLabelScalarStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - sethistorylabel: Core Router";

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.SetHistoryLabel);
        script.Steps[0].SetHistoryLabel.Should().Be("Core Router");
    }

    [Fact]
    public void Parse_SetHistoryLabelMappingStep_ParsesValueAndReplace()
    {
        var yaml = @"---
steps:
  - sethistorylabel:
      value: Distribution Switch
      replace: true";

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.SetHistoryLabel);
        script.Steps[0].SetHistoryLabel.Should().BeEquivalentTo(new SetHistoryLabelOptions
        {
            Value = "Distribution Switch",
            Replace = true
        });
    }

    [Fact]
    public void Parse_SetHistoryLabelMappingStep_ParsesModeAndSeparator()
    {
        var yaml = @"---
steps:
  - sethistorylabel:
      value: Router
      mode: append
      separator: ' / '";

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.SetHistoryLabel);
        script.Steps[0].SetHistoryLabel.Should().BeEquivalentTo(new SetHistoryLabelOptions
        {
            Value = "Router",
            Mode = "append",
            Separator = " / ",
            Replace = null
        });
    }

    [Fact]
    public void Parse_ExitStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - exit: success";

        var script = _parser.Parse(yaml);

        script.Steps[0].Exit.Should().Be("success");
    }

    [Fact]
    public void Parse_StepWithTimeout_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - send: show version
    timeout: 60";

        var script = _parser.Parse(yaml);

        script.Steps[0].Timeout.Should().Be(60);
    }

    [Fact]
    public void Parse_StepWithExpect_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - send: show version
    expect: ""Version""";

        var script = _parser.Parse(yaml);

        script.Steps[0].Expect.Should().Be("Version");
    }

    [Fact]
    public void Parse_StepWithOnError_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - send: show version
    on_error: continue";

        var script = _parser.Parse(yaml);

        script.Steps[0].OnError.Should().Be("continue");
    }

    [Fact]
    public void Parse_StepWithCapture_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - send: show version
    capture: output";

        var script = _parser.Parse(yaml);

        script.Steps[0].Capture.Should().Be("output");
    }

    [Fact]
    public void Parse_StepWithSuppress_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - send: show version
    suppress: true";

        var script = _parser.Parse(yaml);

        script.Steps[0].Suppress.Should().BeTrue();
    }

    #endregion

    #region Parse Control Flow Tests

    [Fact]
    public void Parse_IfThenElse_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - if: ""{{var}} == 'value'""
    then:
      - send: matched
    else:
      - send: not matched";

        var script = _parser.Parse(yaml);

        script.Steps[0].If.Should().Be("{{var}} == 'value'");
        script.Steps[0].Then.Should().HaveCount(1);
        script.Steps[0].Then![0].Send.Should().Be("matched");
        script.Steps[0].Else.Should().HaveCount(1);
        script.Steps[0].Else![0].Send.Should().Be("not matched");
    }

    [Fact]
    public void Parse_Foreach_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - foreach: item in items
    do:
      - send: ""{{item}}""";

        var script = _parser.Parse(yaml);

        script.Steps[0].Foreach.Should().Be("item in items");
        script.Steps[0].Do.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_While_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - while: ""{{counter}} < 5""
    do:
      - send: iteration
      - set: counter = {{counter}} + 1";

        var script = _parser.Parse(yaml);

        script.Steps[0].While.Should().Be("{{counter}} < 5");
        script.Steps[0].Do.Should().HaveCount(2);
    }

    #endregion

    #region Parse Extract Tests

    [Fact]
    public void Parse_ExtractStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - extract:
      from: output
      pattern: ""Version: (.+)""
      into: version";

        var script = _parser.Parse(yaml);

        script.Steps[0].Extract.Should().NotBeNull();
        script.Steps[0].Extract!.From.Should().Be("output");
        script.Steps[0].Extract!.Pattern.Should().Be("Version: (.+)");
        script.Steps[0].Extract!.Into.Should().Be("version");
    }

    [Fact]
    public void Parse_ExtractWithMatch_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - extract:
      from: output
      pattern: ""(\\d+)""
      into: number
      match: first";

        var script = _parser.Parse(yaml);

        script.Steps[0].Extract!.Match.Should().Be("first");
    }

    #endregion

    #region Parse Error Handling Tests

    [Fact]
    public void Parse_InvalidYaml_ThrowsScriptParseException()
    {
        var yaml = @"---
invalid: yaml: syntax: here
  bad indentation";

        var action = () => _parser.Parse(yaml);

        action.Should().Throw<ScriptParseException>();
    }

    [Fact]
    public void Parse_EmptyScript_ReturnsEmptySteps()
    {
        var yaml = @"---
name: Empty Script";

        var script = _parser.Parse(yaml);

        script.Steps.Should().BeEmpty();
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_EmptySteps_ReturnsError()
    {
        var yaml = @"---
name: Empty Script";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script);

        errors.Should().Contain(e => e.Contains("no steps"));
    }

    [Fact]
    public void Validate_ValidScript_ReturnsNoErrors()
    {
        var yaml = @"---
steps:
  - send: show version
  - wait: 1000
  - print: Done";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ScalarStepItem_ReturnsError()
    {
        var yaml = @"---
steps:
  - print: ""ok""
  - print ""missing colon""";
        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(2);
        script.Steps[1].GetStepType().Should().Be(StepType.Unknown);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Step has no recognized command"));
        errors.Should().Contain(e => e.Contains("print \"missing colon\""));
    }

    [Fact]
    public void Validate_TableScalarForm_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - table: "${items}"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error =>
            error.Contains("table must be a mapping with required key 'data'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_IfWithoutThen_ReturnsError()
    {
        var yaml = @"---
steps:
  - if: condition";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("then"));
    }

    [Fact]
    public void Validate_ForeachWithoutDo_ReturnsError()
    {
        var yaml = @"---
steps:
  - foreach: item in items";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("do"));
    }

    [Fact]
    public void Validate_WhileWithoutDo_ReturnsError()
    {
        var yaml = @"---
steps:
  - while: condition";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("do"));
    }

    [Fact]
    public void Validate_WhileWithInvalidMaxIterations_ReturnsError()
    {
        var yaml = @"---
steps:
  - while: condition
    max_iterations: 0
    do:
      - print: test";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("max_iterations"));
    }

    [Fact]
    public void Validate_SetWithoutEquals_ReturnsError()
    {
        var yaml = @"---
steps:
  - set: variableWithoutValue";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("variable = value"));
    }

    [Fact]
    public void Validate_ExtractMissingFrom_ReturnsError()
    {
        var yaml = @"---
steps:
  - extract:
      pattern: ""(.+)""
      into: result";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("from"));
    }

    [Fact]
    public void Validate_ExtractMissingPattern_ReturnsError()
    {
        var yaml = @"---
steps:
  - extract:
      from: output
      into: result";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("pattern"));
    }

    [Fact]
    public void Validate_ExtractMissingInto_ReturnsError()
    {
        var yaml = @"---
steps:
  - extract:
      from: output
      pattern: ""(.+)""";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("into"));
    }

    #endregion

    #region Parse UpdateColumn Tests

    [Fact]
    public void Parse_UpdateColumnStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - updatecolumn:
      column: version
      value: ""1.0.0""";

        var script = _parser.Parse(yaml);

        script.Steps[0].UpdateColumn.Should().NotBeNull();
        script.Steps[0].UpdateColumn!.Column.Should().Be("version");
        script.Steps[0].UpdateColumn!.Value.Should().Be("1.0.0");
    }

    [Fact]
    public void Parse_UpdateColumnWithVariable_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - updatecolumn:
      column: hostname
      value: ""${extracted_hostname}""";

        var script = _parser.Parse(yaml);

        script.Steps[0].UpdateColumn.Should().NotBeNull();
        script.Steps[0].UpdateColumn!.Column.Should().Be("hostname");
        script.Steps[0].UpdateColumn!.Value.Should().Be("${extracted_hostname}");
    }

    [Fact]
    public void Parse_MultipleUpdateColumns_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - updatecolumn:
      column: version
      value: ""${version}""
  - updatecolumn:
      column: hostname
      value: ""${hostname}""
  - updatecolumn:
      column: last_checked
      value: ""${_timestamp}""";

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(3);
        script.Steps[0].UpdateColumn!.Column.Should().Be("version");
        script.Steps[1].UpdateColumn!.Column.Should().Be("hostname");
        script.Steps[2].UpdateColumn!.Column.Should().Be("last_checked");
    }

    [Fact]
    public void Validate_UpdateColumnMissingColumn_ReturnsError()
    {
        var yaml = @"---
steps:
  - updatecolumn:
      value: ""test""";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("column"));
    }

    [Fact]
    public void Validate_UpdateColumnMissingValue_ReturnsError()
    {
        var yaml = @"---
steps:
  - updatecolumn:
      column: test";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("value"));
    }

    [Fact]
    public void Validate_UpdateColumnValid_NoErrors()
    {
        var yaml = @"---
steps:
  - updatecolumn:
      column: status
      value: active";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Parse UpdateEnvironment Tests

    [Fact]
    public void Parse_UpdateEnvironmentStep_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - updateenvironment:
      variable: api_token
      value: ""abc123""";

        var script = _parser.Parse(yaml);

        script.Steps[0].UpdateEnvironment.Should().NotBeNull();
        script.Steps[0].UpdateEnvironment!.Variable.Should().Be("api_token");
        script.Steps[0].UpdateEnvironment!.Value.Should().Be("abc123");
    }

    [Fact]
    public void Parse_UpdateEnvironmentWithVariable_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - updateenvironment:
      variable: api_token
      value: ""${new_token}""";

        var script = _parser.Parse(yaml);

        script.Steps[0].UpdateEnvironment.Should().NotBeNull();
        script.Steps[0].UpdateEnvironment!.Variable.Should().Be("api_token");
        script.Steps[0].UpdateEnvironment!.Value.Should().Be("${new_token}");
    }

    [Fact]
    public void Validate_UpdateEnvironmentMissingVariable_ReturnsError()
    {
        var yaml = @"---
steps:
  - updateenvironment:
      value: ""test""";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("variable"));
    }

    [Fact]
    public void Validate_UpdateEnvironmentMissingValue_ReturnsError()
    {
        var yaml = @"---
steps:
  - updateenvironment:
      variable: token";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("value"));
    }

    [Fact]
    public void Validate_UpdateEnvironmentValid_NoErrors()
    {
        var yaml = @"---
steps:
  - updateenvironment:
      variable: token
      value: refreshed";
        var script = _parser.Parse(yaml);

        var errors = _parser.Validate(script, yaml);

        errors.Should().BeEmpty();
    }

    #endregion

    #region Extended Control Flow Tests

    [Fact]
    public void Parse_IfWithElif_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - if: a == 1
    then:
      - print: one
    elif:
      - if: a == 2
        then:
          - print: two
      - if: a == 3
        then:
          - print: three
    else:
      - print: other";

        var script = _parser.Parse(yaml);

        script.Steps[0].If.Should().Be("a == 1");
        script.Steps[0].Elif.Should().NotBeNull();
        script.Steps[0].Elif!.Should().HaveCount(2);
        script.Steps[0].Elif![0].If.Should().Be("a == 2");
        script.Steps[0].Elif![0].Then.Should().ContainSingle();
    }

    [Fact]
    public void Parse_WhileWithMaxIterations_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - while: i < 10
    max_iterations: 25
    do:
      - set: i = i + 1";

        var script = _parser.Parse(yaml);

        script.Steps[0].While.Should().Be("i < 10");
        script.Steps[0].MaxIterations.Should().Be(25);
    }

    [Fact]
    public void Parse_BreakAndContinue_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - break: true
  - continue: true";

        var script = _parser.Parse(yaml);

        script.Steps[0].GetStepType().Should().Be(StepType.Break);
        script.Steps[1].GetStepType().Should().Be(StepType.Continue);
    }

    [Fact]
    public void Parse_TryCatchFinally_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - try:
      - print: inside
    catch:
      - print: caught
    finally:
      - print: done";

        var script = _parser.Parse(yaml);

        script.Steps[0].GetStepType().Should().Be(StepType.Try);
        script.Steps[0].Try.Should().ContainSingle();
        script.Steps[0].Catch.Should().ContainSingle();
        script.Steps[0].Finally.Should().ContainSingle();
    }

    [Fact]
    public void Validate_BreakOutsideLoop_ReturnsError()
    {
        var yaml = @"---
steps:
  - break: true";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("break can only be used inside foreach/while"));
    }

    [Fact]
    public void Validate_ContinueOutsideLoop_ReturnsError()
    {
        var yaml = @"---
steps:
  - continue: true";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("continue can only be used inside foreach/while"));
    }

    [Fact]
    public void Validate_BreakInsideLoop_NoError()
    {
        var yaml = @"---
steps:
  - while: i < 10
    do:
      - break: true";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_UnknownKey_AddsWarningWithLineNumber()
    {
        var yaml = @"---
steps:
  - send: show version
    typoo: yes";

        _parser.Parse(yaml);

        _parser.Warnings.Should().ContainSingle();
        _parser.Warnings[0].Should().Contain("Line");
        _parser.Warnings[0].Should().Contain("Unknown step key 'typoo'");
    }

    [Fact]
    public void Parse_UnknownOptionKey_AddsWarningWithLineNumber()
    {
        var yaml = @"---
steps:
  - readfile:
      path: C:\\temp\\x.txt
      into: data
      typoo: yes";

        _parser.Parse(yaml);

        _parser.Warnings.Should().ContainSingle();
        _parser.Warnings[0].Should().Contain("Unknown readfile key 'typoo'");
    }

    [Fact]
    public void Parse_ReadfileSelectFile_ParsesAndValidatesWithoutPath()
    {
        var yaml = @"---
steps:
  - readfile:
      select_file: true
      into: chosen_lines";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);
        var readfile = script.Steps[0].Readfile;

        script.Steps.Should().ContainSingle();
        readfile.Should().NotBeNull();
        readfile!.SelectFile.Should().BeTrue();
        readfile.Path.Should().BeEmpty();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ReadfileSelectFile_WithCustomMessageAndFileExt_ParsesAndValidates()
    {
        var yaml = @"---
steps:
  - readfile:
      select_file: true
      message: Pick the host list to import.
      fileext: txt,json
      into: chosen_lines";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);
        var readfile = script.Steps[0].Readfile;

        readfile.Should().NotBeNull();
        readfile!.SelectFile.Should().BeTrue();
        readfile.Message.Should().Be("Pick the host list to import.");
        readfile.FileExt.Should().Be("txt,json");
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReadfileWithoutPathAndWithoutSelectFile_ReturnsError()
    {
        var yaml = @"---
steps:
  - readfile:
      into: chosen_lines";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().ContainSingle(error => error.Contains("Readfile requires 'path'"));
    }

    [Fact]
    public void Validate_ReadfileSelectFileWithoutInto_ReturnsError()
    {
        var yaml = @"---
steps:
  - readfile:
      select_file: true";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().ContainSingle(error => error.Contains("Readfile requires 'into'"));
    }

    [Fact]
    public void Validate_PlaySoundWithoutPath_ReturnsError()
    {
        var yaml = @"---
steps:
  - playsound:
      wait: true";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(error => error.Contains("Playsound requires 'path'"));
    }

    [Fact]
    public void Validate_PlaySoundInvalidVolume_ReturnsError()
    {
        var yaml = @"---
steps:
  - playsound:
      path: C:\\temp\\alert.mp3
      volume: 101";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(error => error.Contains("Playsound 'volume' must be between 0 and 100"));
    }

    [Fact]
    public void Validate_PlaySoundFractionalMaxSeconds_IsAccepted()
    {
        var yaml = """
            ---
            steps:
              - playsound:
                  path: C:\\temp\\alert.mp3
                  wait: true
                  max_seconds: 0.25
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        script.Steps.Should().ContainSingle();
        var playSound = script.Steps[0].PlaySound;
        playSound.Should().NotBeNull();
        playSound!.MaxSeconds.HasValue.Should().BeTrue();
        playSound.MaxSeconds!.Value.Should().BeApproximately(0.25, 0.0001);
        errors.Should().NotContain(error => error.Contains("playsound.max_seconds"));
    }

    [Fact]
    public void Validate_SetHistoryLabelInvalidMode_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - sethistorylabel:
                  value: Router
                  mode: sideways
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(error => error.Contains("sethistorylabel 'mode' must be one of replace, append, prepend, clear"));
    }

    #endregion

    #region Complex Script Tests

    [Fact]
    public void Parse_CompleteScript_ParsesAllElements()
    {
        var yaml = @"---
name: Complete Test Script
description: A comprehensive test script
version: 1
debug: true
vars:
  device_type: router
  max_retries: 3
steps:
  - print: Starting configuration
  - send: enable
    timeout: 30
  - wait: 1000
  - send: configure terminal
    capture: config_output
  - if: ""{{device_type}} == 'router'""
    then:
      - send: router ospf 1
      - send: network 10.0.0.0 0.255.255.255 area 0
    else:
      - send: vlan 100
  - foreach: interface in interfaces
    do:
      - send: ""interface {{interface}}""
      - send: no shutdown
  - extract:
      from: config_output
      pattern: ""hostname (.+)""
      into: hostname
  - print: ""Hostname is {{hostname}}""
  - exit: success";

        var script = _parser.Parse(yaml);

        script.Name.Should().Be("Complete Test Script");
        script.Description.Should().Be("A comprehensive test script");
        script.Version.Should().Be(1);
        script.Debug.Should().BeTrue();
        script.Vars.Should().HaveCount(2);
        script.Steps.Should().HaveCount(9);
    }

    #endregion

    #region Choose/Multiselect/Confirm Parser Tests

    [Theory]
    [InlineData("- choose:\n    prompt: test\n    into: x\n    options:\n      - a")]
    [InlineData("- multiselect:\n    prompt: test\n    into: x\n    options:\n      - a")]
    [InlineData("- confirm:\n    prompt: test\n    into: x")]
    public void IsYamlScript_InteractivePromptSteps_ReturnsTrue(string input)
    {
        ScriptParser.IsYamlScript(input).Should().BeTrue();
    }

    [Fact]
    public void Parse_ChooseStep_SimpleOptions_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - choose:
      title: ""Device Role Selection""
      prompt: ""Select device:""
      into: device
      options:
        - router
        - switch
        - firewall
      default: router";

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        var step = script.Steps[0];
        step.GetStepType().Should().Be(StepType.Choose);
        step.Choose.Should().NotBeNull();
        step.Choose!.Title.Should().Be("Device Role Selection");
        step.Choose!.Prompt.Should().Be("Select device:");
        step.Choose.Into.Should().Be("device");
        step.Choose.Options.Should().HaveCount(3);
        step.Choose.Options[0].Label.Should().Be("router");
        step.Choose.Options[0].Value.Should().Be("router");
        step.Choose.Options[1].Label.Should().Be("switch");
        step.Choose.Options[2].Label.Should().Be("firewall");
        step.Choose.Default.Should().Be("router");
    }

    [Fact]
    public void Parse_ChooseStep_LabelValueOptions_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - choose:
      prompt: ""Select protocol:""
      into: port
      options:
        - label: ""SSH (22)""
          value: ""22""
        - label: ""HTTPS (443)""
          value: ""443""
      default: ""22""";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.Choose.Should().NotBeNull();
        step.Choose!.Options.Should().HaveCount(2);
        step.Choose.Options[0].Label.Should().Be("SSH (22)");
        step.Choose.Options[0].Value.Should().Be("22");
        step.Choose.Options[1].Label.Should().Be("HTTPS (443)");
        step.Choose.Options[1].Value.Should().Be("443");
        step.Choose.Default.Should().Be("22");
    }

    [Fact]
    public void Parse_MultiselectStep_WithMinMax_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - multiselect:
      title: ""Interface Selection""
      prompt: ""Select interfaces:""
      into: ifaces
      options:
        - Gig0/0
        - Gig0/1
        - Loopback0
      min: 1
      max: 2";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.GetStepType().Should().Be(StepType.Multiselect);
        step.Multiselect.Should().NotBeNull();
        step.Multiselect!.Title.Should().Be("Interface Selection");
        step.Multiselect!.Prompt.Should().Be("Select interfaces:");
        step.Multiselect.Into.Should().Be("ifaces");
        step.Multiselect.Options.Should().HaveCount(3);
        step.Multiselect.Min.Should().Be(1);
        step.Multiselect.Max.Should().Be(2);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    public void Parse_ConfirmStep_DefaultValues_ParsesCorrectly(string defaultStr, bool expected)
    {
        var yaml = $@"---
steps:
  - confirm:
      title: ""Confirm Action""
      prompt: ""Are you sure?""
      into: confirmed
      default: {defaultStr}";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.GetStepType().Should().Be(StepType.Confirm);
        step.Confirm.Should().NotBeNull();
        step.Confirm!.Title.Should().Be("Confirm Action");
        step.Confirm!.Prompt.Should().Be("Are you sure?");
        step.Confirm.Into.Should().Be("confirmed");
        step.Confirm.Default.Should().Be(expected);
    }

    [Fact]
    public void Parse_InputStep_WithTitle_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - input:
      title: ""Credential Prompt""
      prompt: ""Enter username:""
      into: username";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.GetStepType().Should().Be(StepType.Input);
        step.Input.Should().NotBeNull();
        step.Input!.Title.Should().Be("Credential Prompt");
        step.Input.Prompt.Should().Be("Enter username:");
        step.Input.Into.Should().Be("username");
    }

    [Fact]
    public void Parse_InputStep_FontSize_ParsesAsFloat()
    {
        var yaml = @"---
steps:
  - input:
      prompt: ""Enter value:""
      into: v
      font_size: 14";

        var script = _parser.Parse(yaml);

        script.Steps[0].Input!.FontSize.Should().Be(14f);
    }

    [Fact]
    public void Parse_InputStep_FontSizeOmitted_IsNull()
    {
        var yaml = @"---
steps:
  - input:
      prompt: ""Enter value:""
      into: v";

        var script = _parser.Parse(yaml);

        script.Steps[0].Input!.FontSize.Should().BeNull();
    }

    [Fact]
    public void Parse_ChooseStep_FontSize_ParsesAsFloat()
    {
        var yaml = @"---
steps:
  - choose:
      prompt: ""Pick:""
      into: pick
      options: [a, b]
      font_size: 16.5";

        var script = _parser.Parse(yaml);

        script.Steps[0].Choose!.FontSize.Should().Be(16.5f);
    }

    [Fact]
    public void Parse_MultiselectStep_FontSize_ParsesAsFloat()
    {
        var yaml = @"---
steps:
  - multiselect:
      prompt: ""Pick many:""
      into: picks
      options: [a, b, c]
      font_size: 12";

        var script = _parser.Parse(yaml);

        script.Steps[0].Multiselect!.FontSize.Should().Be(12f);
    }

    [Fact]
    public void Parse_ConfirmStep_FontSize_ParsesAsFloat()
    {
        var yaml = @"---
steps:
  - confirm:
      prompt: ""Are you sure?""
      into: answer
      font_size: 18";

        var script = _parser.Parse(yaml);

        script.Steps[0].Confirm!.FontSize.Should().Be(18f);
    }

    [Fact]
    public void Parse_InputOnErrorInsideMap_ParsesOnError()
    {
        var yaml = """
            ---
            steps:
              - input:
                  prompt: "Enter value:"
                  into: answer
                  on_error: continue
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.Input);
        script.Steps[0].OnError.Should().Be("continue");
    }

    [Fact]
    public void Parse_ChooseStep_MixedOptions_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - choose:
      prompt: ""Pick:""
      into: pick
      options:
        - simple
        - label: ""Labeled""
          value: ""lbl""";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.Choose!.Options.Should().HaveCount(2);
        step.Choose.Options[0].Label.Should().Be("simple");
        step.Choose.Options[0].Value.Should().Be("simple");
        step.Choose.Options[1].Label.Should().Be("Labeled");
        step.Choose.Options[1].Value.Should().Be("lbl");
    }

    [Fact]
    public void Parse_ChooseStep_OptionsFromVariable_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - choose:
      prompt: ""Pick interface:""
      into: selected_interface
      options: interface_list";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.Choose.Should().NotBeNull();
        step.Choose!.Options.Should().BeEmpty();
        step.Choose.OptionsFrom.Should().Be("interface_list");
    }

    [Fact]
    public void Parse_MultiselectStep_OptionsFromVariable_ParsesCorrectly()
    {
        var yaml = @"---
steps:
  - multiselect:
      prompt: ""Pick interfaces:""
      into: selected_interfaces
      options: ${interface_list}";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.Multiselect.Should().NotBeNull();
        step.Multiselect!.Options.Should().BeEmpty();
        step.Multiselect.OptionsFrom.Should().Be("${interface_list}");
    }

    [Fact]
    public void Validate_ChooseWithoutInto_ReturnsError()
    {
        var yaml = @"---
steps:
  - choose:
      options:
        - a";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("Choose requires 'into'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ChooseWithoutOptions_ReturnsError()
    {
        var yaml = @"---
steps:
  - choose:
      into: selected";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("Choose requires 'options'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultiselectWithoutInto_ReturnsError()
    {
        var yaml = @"---
steps:
  - multiselect:
      options:
        - a";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("Multiselect requires 'into'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MultiselectWithoutOptions_ReturnsError()
    {
        var yaml = @"---
steps:
  - multiselect:
      into: selected";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("Multiselect requires 'options'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_ConfirmWithoutInto_ReturnsError()
    {
        var yaml = @"---
steps:
  - confirm:
      prompt: Are you sure?";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("Confirm requires 'into'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WebhookWithoutUrl_ReturnsError()
    {
        var yaml = @"---
steps:
  - webhook:
      method: POST";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("Webhook requires 'url'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_LogMapWithoutMessage_ReturnsError()
    {
        var yaml = @"---
steps:
  - log:
      level: info";

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("Log requires 'message'", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Interactive Terminal Parser Tests

    [Fact]
    public void Parse_InteractiveStep_DefaultsApplied()
    {
        var yaml = """
            ---
            steps:
              - interactive: {}
            """;

        var script = _parser.Parse(yaml);
        var step = script.Steps[0];

        step.GetStepType().Should().Be(StepType.Interactive);
        step.Interactive.Should().NotBeNull();
        step.Interactive!.Session.Should().Be(InteractiveSessionMode.Separate);
        step.Interactive.ShowWindow.Should().BeTrue();
    }

    [Fact]
    public void Parse_InteractiveCaptureModeOptions_ParsesCorrectly()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  title: "Packet Capture"
                  command: diagnose sniffer packet any 'host 10.0.0.1' 4 10 a
                  capture: sniffer_output
                  max_seconds: 120
                  max_lines: 250
                  width: 940
                  height: 600
                  mirror_output: true
                  show_window: false
            """;

        var script = _parser.Parse(yaml);
        var step = script.Steps[0];

        step.Interactive.Should().NotBeNull();
        step.Interactive!.Session.Should().Be(InteractiveSessionMode.Separate);
        step.Interactive.Title.Should().Be("Packet Capture");
        step.Interactive.Command.Should().Be("diagnose sniffer packet any 'host 10.0.0.1' 4 10 a");
        step.Interactive.Capture.Should().Be("sniffer_output");
        step.Interactive.MaxSeconds.Should().Be(120);
        step.Interactive.MaxLines.Should().Be(250);
        step.Interactive.Width.Should().Be(940);
        step.Interactive.Height.Should().Be(600);
        step.Interactive.MirrorOutput.Should().BeTrue();
        step.Interactive.ShowWindow.Should().BeFalse();
    }

    [Fact]
    public void Validate_InteractiveScalarForm_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive: true
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive must be a mapping"));
        errors.Count(error => error.Contains("interactive must be a mapping", StringComparison.OrdinalIgnoreCase))
            .Should().Be(1);
    }

    [Fact]
    public void Validate_InteractiveInvalidSession_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: pooled
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.session must be 'separate' or 'shared'"));
    }

    [Fact]
    public void Validate_InteractiveCommandWithSharedSession_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: shared
                  command: tcpdump -i any
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.session must be 'separate' when interactive.command is set"));
    }

    [Fact]
    public void Validate_InteractiveMaxSecondsNotPositive_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  command: tcpdump -i any
                  max_seconds: 0
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.max_seconds must be greater than 0"));
    }

    [Fact]
    public void Validate_InteractiveMaxLinesNotPositive_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  command: tcpdump -i any
                  max_lines: 0
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.max_lines must be greater than 0"));
    }

    [Fact]
    public void Validate_InteractiveWidthNotPositive_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  width: 0
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.width must be greater than 0"));
    }

    [Fact]
    public void Validate_InteractiveHeightNotPositive_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  height: 0
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.height must be greater than 0"));
    }

    [Fact]
    public void Parse_InteractiveLegacyColumnsRows_ParsesAndWarns()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  columns: 100
                  rows: 30
            """;

        var script = _parser.Parse(yaml);

        script.Steps[0].Interactive.Should().NotBeNull();
        script.Steps[0].Interactive!.Columns.Should().Be(100);
        script.Steps[0].Interactive!.Rows.Should().Be(30);
        _parser.Warnings.Should().Contain(warning => warning.Contains("interactive.columns is deprecated; use interactive.width/interactive.height (pixels)", StringComparison.OrdinalIgnoreCase));
        _parser.Warnings.Should().Contain(warning => warning.Contains("interactive.rows is deprecated; use interactive.width/interactive.height (pixels)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InteractiveShowWindowFalseWithoutCommand_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  show_window: false
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.show_window=false requires interactive.command"));
    }

    [Fact]
    public void Validate_InteractiveShowWindowFalseWithoutLimits_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  command: tcpdump -i any
                  show_window: false
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.show_window=false requires interactive.max_seconds or interactive.max_lines"));
    }

    [Fact]
    public void Parse_InteractiveEmulationKey_AddsDeprecationWarning()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  emulation: basic
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().NotContain(error => error.Contains("interactive.emulation", StringComparison.OrdinalIgnoreCase));
        _parser.Warnings.Should().Contain(warning => warning.Contains("interactive.emulation is deprecated and ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_InteractiveUnknownKey_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  unknown_flag: true
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("interactive.unknown_flag is not supported"));
    }

    [Fact]
    public void Parse_InteractiveOnErrorInsideMap_ParsesOnError()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  on_error: continue
            """;

        var script = _parser.Parse(yaml);
        script.Steps[0].OnError.Should().Be("continue");
    }

    #endregion

    #region Send Retry/Respond Parser Tests

    [Fact]
    public void Parse_SendMapWithRetryOptions_ParsesCorrectly()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: show version
                  retry: 2
                  retry_delay: 5
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].Send.Should().Be("show version");
        script.Steps[0].Retry.Should().Be(2);
        script.Steps[0].RetryDelay.Should().Be(5);
    }

    [Fact]
    public void Parse_SendMapWithFailOnNonZero_ParsesCorrectly()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: definitely_not_a_command
                  fail_on_nonzero: true
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].Send.Should().Be("definitely_not_a_command");
        script.Steps[0].FailOnNonZero.Should().BeTrue();
    }

    [Fact]
    public void Parse_SendMapWithRespondPairs_ParsesCorrectly()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: adduser qa
                  respond:
                    - expect: "Password:"
                      reply: "secret"
                    - expect: "Confirm:"
                      reply: "secret"
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].Respond.Should().NotBeNull();
        script.Steps[0].Respond.Should().HaveCount(2);
        script.Steps[0].Respond![0].Expect.Should().Be("Password:");
        script.Steps[0].Respond![0].Reply.Should().Be("secret");
        script.Steps[0].Respond![1].Expect.Should().Be("Confirm:");
    }

    [Fact]
    public void Validate_SendRespondPairMissingReply_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: adduser qa
                  respond:
                    - expect: "Password:"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("send.respond entry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_SendFailOnNonZeroWithExpect_ReturnsError()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: hostname
                  expect: "ready"
                  fail_on_nonzero: true
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("send.fail_on_nonzero is not supported with send.expect"));
    }

    [Fact]
    public void Validate_SendFailOnNonZeroWithRespond_IsValid()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: adduser qa
                  fail_on_nonzero: true
                  respond:
                    - expect: "Password:"
                      reply: "secret"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().NotContain(error => error.Contains("fail_on_nonzero"));
    }

    #endregion

    #region Exists Parser Tests

    [Fact]
    public void Parse_ExistsStep_ParsesCorrectly()
    {
      var yaml = """
        ---
        steps:
          - exists:
              path: "%UserProfile%\\Documents\\hosts.txt"
              into: hosts_file
              type: file
              on_error: continue
        """;

      var script = _parser.Parse(yaml);

      script.Steps.Should().HaveCount(1);
      script.Steps[0].GetStepType().Should().Be(StepType.Exists);
      script.Steps[0].Exists.Should().NotBeNull();
      script.Steps[0].Exists!.Path.Should().Be("%UserProfile%\\Documents\\hosts.txt");
      script.Steps[0].Exists.Into.Should().Be("hosts_file");
      script.Steps[0].Exists.Type.Should().Be("file");
      script.Steps[0].OnError.Should().Be("continue");
    }

    [Fact]
    public void Validate_ExistsWithoutPath_ReturnsError()
    {
      var yaml = """
        ---
        steps:
          - exists:
              into: file_present
        """;

      var script = _parser.Parse(yaml);
      var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

      errors.Should().Contain(error => error.Contains("Exists requires 'path'"));
    }

    [Fact]
    public void Validate_ExistsWithInvalidType_ReturnsError()
    {
      var yaml = """
        ---
        steps:
          - exists:
              path: "C:\\temp"
              into: has_path
              type: symlink
        """;

      var script = _parser.Parse(yaml);
      var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

      errors.Should().Contain(error => error.Contains("Exists 'type' must be one of any, file, directory"));
    }

    [Fact]
    public void Parse_ExistsUnknownKey_AddsWarning()
    {
      var yaml = """
        ---
        steps:
          - exists:
              path: "C:\\temp"
              into: has_path
              typoo: true
        """;

      _ = _parser.Parse(yaml);

      _parser.Warnings.Should().ContainSingle();
      _parser.Warnings[0].Should().Contain("Unknown exists key 'typoo'");
    }

    #endregion

    #region PreprocessYaml Tests

    [Fact]
    public void PreprocessYaml_QuotesTernaryInSetStep()
    {
        var yaml = """
            steps:
              - set: t1 = 10 > 5 ? "yes" : "no"
            """;

        var result = ScriptParser.PreprocessYaml(yaml);

        result.Should().Contain("set: 't1 = 10 > 5 ? \"yes\" : \"no\"'");
    }

    [Fact]
    public void PreprocessYaml_LeavesAlreadyQuotedValuesAlone()
    {
        var yaml = """
            steps:
              - set: 't1 = 10 > 5 ? "yes" : "no"'
            """;

        var result = ScriptParser.PreprocessYaml(yaml);

        // Should be unchanged
        result.Should().Be(yaml);
    }

    [Fact]
    public void PreprocessYaml_LeavesValuesWithoutColonAlone()
    {
        var yaml = """
            steps:
              - set: score = 75
            """;

        var result = ScriptParser.PreprocessYaml(yaml);

        result.Should().Be(yaml);
    }

    [Fact]
    public void PreprocessYaml_HandlesNestedTernary()
    {
        var yaml = """
            steps:
              - set: grade = score >= 90 ? "A" : score >= 80 ? "B" : "F"
            """;

        var result = ScriptParser.PreprocessYaml(yaml);

        result.Should().Contain("set: 'grade = score >= 90 ? \"A\" : score >= 80 ? \"B\" : \"F\"'");
    }

    [Fact]
    public void PreprocessYaml_DoesNotQuoteColonInsideQuotedString()
    {
        // The colon is inside quotes, so there's no unquoted colon
        var yaml = """
            steps:
              - print: "time is 10:30"
            """;

        var result = ScriptParser.PreprocessYaml(yaml);

        result.Should().Be(yaml);
    }

    [Fact]
    public void PreprocessYaml_QuotesEmbeddedQuotedStringWithTrailingText()
    {
        // YAML would parse "alice" as the value and choke on " in allowed_names"
        var yaml = """
            steps:
              - if: "alice" in allowed_names
            """;

        var result = ScriptParser.PreprocessYaml(yaml);

        result.Should().Contain("if: '\"alice\" in allowed_names'");
    }

    [Fact]
    public void PreprocessYaml_LeavesFullyQuotedValueAlone()
    {
        // Value is a single complete quoted string — no ambiguity
        var yaml = """
            steps:
              - print: "this is fine"
            """;

        var result = ScriptParser.PreprocessYaml(yaml);

        result.Should().Be(yaml);
    }

    [Fact]
    public void Parse_TernaryExpressionWithoutManualQuoting()
    {
        var yaml = """
            steps:
              - set: t1 = 10 > 5 ? "yes" : "no"
              - set: score = 75
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(2);
        script.Steps[0].Set.Should().Be("t1 = 10 > 5 ? \"yes\" : \"no\"");
        script.Steps[1].Set.Should().Be("score = 75");
    }

    [Fact]
    public void Parse_IfWithQuotedMembershipExpression()
    {
        var yaml = """
            steps:
              - if: "alice" in allowed_names
                then:
                  - print: "found"
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(1);
        script.Steps[0].If.Should().Be("\"alice\" in allowed_names");
    }

    [Fact]
    public void Validate_PreconnectAsScalar_ReturnsError()
    {
        var yaml = """
            ---
            preconnect: true
            steps:
              - print: ok
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(e => e.Contains("preconnect must be a sequence", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_PreconnectSendStep_ReturnsError()
    {
        var yaml = """
            ---
            preconnect:
              - send: echo should_fail
            steps:
              - print: ok
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(e => e.Contains("not allowed in preconnect", StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
