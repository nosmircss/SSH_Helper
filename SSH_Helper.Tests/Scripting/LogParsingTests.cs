using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

/// <summary>
/// Tests for parsing log and webhook commands from YAML.
/// </summary>
public class LogParsingTests
{
    private readonly ScriptParser _parser;

    public LogParsingTests()
    {
        _parser = new ScriptParser();
    }

    [Fact]
    public void Parse_LogSimpleString_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"---
steps:
  - log: ""Test message""
";

        // Act
        var script = _parser.Parse(yaml);

        // Assert
        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.Log);
        script.Steps[0].Log.Should().Be("Test message");
    }

    [Fact]
    public void Parse_LogWithOptions_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"---
steps:
  - log:
      message: ""Warning message""
      level: warning
";

        // Act
        var script = _parser.Parse(yaml);

        // Assert
        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.Log);
        script.Steps[0].Log.Should().BeOfType<LogOptions>();
        var logOptions = (LogOptions)script.Steps[0].Log!;
        logOptions.Message.Should().Be("Warning message");
        logOptions.Level.Should().Be("warning");
    }

    [Fact]
    public void Parse_WebhookCommand_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"---
steps:
  - webhook:
      url: ""https://example.com/api""
      method: POST
      body: '{""test"": true}'
      timeout: 60
      into: response
";

        // Act
        var script = _parser.Parse(yaml);

        // Assert
        script.Steps.Should().HaveCount(1);
        script.Steps[0].GetStepType().Should().Be(StepType.Webhook);
        script.Steps[0].Webhook.Should().NotBeNull();
        script.Steps[0].Webhook!.Url.Should().Be("https://example.com/api");
        script.Steps[0].Webhook!.Method.Should().Be("POST");
        script.Steps[0].Webhook!.Body.Should().Be("{\"test\": true}");
        script.Steps[0].Webhook!.Timeout.Should().Be(60);
        script.Steps[0].Webhook!.Into.Should().Be("response");
    }

    [Fact]
    public void Parse_WebhookWithHeaders_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"---
steps:
  - webhook:
      url: ""https://example.com/api""
      headers:
        Authorization: ""Bearer token123""
        Content-Type: ""application/json""
";

        // Act
        var script = _parser.Parse(yaml);

        // Assert
        script.Steps[0].Webhook!.Headers.Should().NotBeNull();
        script.Steps[0].Webhook!.Headers.Should().ContainKey("Authorization");
        script.Steps[0].Webhook!.Headers!["Authorization"].Should().Be("Bearer token123");
    }

    [Fact]
    public void Parse_WritefileWithFormat_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"---
steps:
  - writefile:
      path: ""output.json""
      format: json
      content: ""${data}""
      pretty: true
";

        // Act
        var script = _parser.Parse(yaml);

        // Assert
        script.Steps[0].Writefile!.Format.Should().Be("json");
        script.Steps[0].Writefile!.Pretty.Should().BeTrue();
    }

    [Fact]
    public void Parse_WritefileWithCsvHeaders_ParsesCorrectly()
    {
        // Arrange
        var yaml = @"---
steps:
  - writefile:
      path: ""output.csv""
      format: csv
      content: ""${rows}""
      headers:
        - Host
        - Status
        - Version
";

        // Act
        var script = _parser.Parse(yaml);

        // Assert
        script.Steps[0].Writefile!.Format.Should().Be("csv");
        script.Steps[0].Writefile!.Headers.Should().HaveCount(3);
        script.Steps[0].Writefile!.Headers.Should().Contain("Host");
        script.Steps[0].Writefile!.Headers.Should().Contain("Status");
    }

    [Fact]
    public void IsYamlScript_WithLogCommand_ReturnsTrue()
    {
        // Arrange
        var text = "- log: \"test\"";

        // Act
        var result = ScriptParser.IsYamlScript(text);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsYamlScript_WithWebhookCommand_ReturnsTrue()
    {
        // Arrange
        var text = "- webhook:\n    url: test";

        // Act
        var result = ScriptParser.IsYamlScript(text);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_LogCommand_NoErrors()
    {
        // Arrange
        var yaml = @"---
steps:
  - log: ""Test""
";
        var script = _parser.Parse(yaml);

        // Act
        var errors = _parser.Validate(script);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WebhookCommand_NoErrors()
    {
        // Arrange
        var yaml = @"---
steps:
  - webhook:
      url: ""https://test.com""
";
        var script = _parser.Parse(yaml);

        // Act
        var errors = _parser.Validate(script);

        // Assert
        errors.Should().BeEmpty();
    }
}
