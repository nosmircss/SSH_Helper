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
    public void IsYamlScript_ScriptSections_ReturnsTrue(string input)
    {
        var result = ScriptParser.IsYamlScript(input);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("name: Test Script")]
    [InlineData("description: Uplink to core")]
    [InlineData("version: 1")]
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
      prompt: ""Are you sure?""
      into: confirmed
      default: {defaultStr}";

        var script = _parser.Parse(yaml);

        var step = script.Steps[0];
        step.GetStepType().Should().Be(StepType.Confirm);
        step.Confirm.Should().NotBeNull();
        step.Confirm!.Prompt.Should().Be("Are you sure?");
        step.Confirm.Into.Should().Be("confirmed");
        step.Confirm.Default.Should().Be(expected);
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

    #endregion
}
