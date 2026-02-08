using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class NetworkStepParserTests
{
    private readonly ScriptParser _parser = new();

    [Fact]
    public void IsYamlScript_NewNetworkStepKeywords_ReturnTrue()
    {
        ScriptParser.IsYamlScript("- http:\n    url: https://example.com").Should().BeTrue();
        ScriptParser.IsYamlScript("- ping: 127.0.0.1").Should().BeTrue();
        ScriptParser.IsYamlScript("- dns:\n    host: localhost").Should().BeTrue();
        ScriptParser.IsYamlScript("- portcheck:\n    host: localhost").Should().BeTrue();
        ScriptParser.IsYamlScript("- sftp:\n    action: upload").Should().BeTrue();
    }

    [Fact]
    public void Parse_NetworkStepSyntax_ParsesAndNormalizesEnums()
    {
        var yaml = """
            ---
            steps:
              - http:
                  url: "https://example.com/api"
                  method: post
                  auth: BEARER
                  token: "${api_token}"
                  content_type: XML
                  verify_tls: false
                  into: api_result
              - ping: "127.0.0.1"
              - dns:
                  host: "example.com"
                  type: aaaa
                  into: dns_result
              - portcheck:
                  host: "localhost"
                  port: 443
                  timeout: 3
                  into: port_result
              - sftp:
                  action: DOWNLOAD
                  local_path: "C:\\temp\\file.txt"
                  remote_path: "/var/tmp/file.txt"
                  into: transfer
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(5);
        script.Steps[0].GetStepType().Should().Be(StepType.Http);
        script.Steps[0].Http!.Method.Should().Be("POST");
        script.Steps[0].Http.Auth.Should().Be("bearer");
        script.Steps[0].Http.ContentType.Should().Be("xml");
        script.Steps[0].Http.VerifyTls.Should().BeFalse();

        script.Steps[1].GetStepType().Should().Be(StepType.Ping);
        script.Steps[1].Ping!.Host.Should().Be("127.0.0.1");

        script.Steps[2].GetStepType().Should().Be(StepType.Dns);
        script.Steps[2].Dns!.Type.Should().Be("AAAA");

        script.Steps[3].GetStepType().Should().Be(StepType.Portcheck);
        script.Steps[3].Portcheck!.Port.Should().Be(443);

        script.Steps[4].GetStepType().Should().Be(StepType.Sftp);
        script.Steps[4].Sftp!.Action.Should().Be("download");

        var errors = _parser.Validate(script, yaml);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NetworkStepDefaults_AreApplied()
    {
        var yaml = """
            ---
            steps:
              - http:
                  url: "https://example.com/health"
              - ping:
                  host: "127.0.0.1"
              - dns:
                  host: "localhost"
              - portcheck:
                  host: "localhost"
              - sftp:
                  action: upload
                  local_path: "local.txt"
                  remote_path: "/tmp/remote.txt"
            """;

        var script = _parser.Parse(yaml);

        var http = script.Steps[0].Http!;
        http.Method.Should().Be("GET");
        http.Timeout.Should().Be(30);
        http.FollowRedirects.Should().BeTrue();
        http.AllowFailure.Should().BeFalse();
        http.VerifyTls.Should().BeTrue();
        http.Auth.Should().Be("none");

        var ping = script.Steps[1].Ping!;
        ping.Count.Should().Be(4);
        ping.Timeout.Should().Be(3000);

        var dns = script.Steps[2].Dns!;
        dns.Type.Should().Be("A");
        dns.Timeout.Should().Be(10);

        var portcheck = script.Steps[3].Portcheck!;
        portcheck.Port.Should().Be(22);
        portcheck.Timeout.Should().Be(5);

        var sftp = script.Steps[4].Sftp!;
        sftp.Overwrite.Should().BeTrue();
        sftp.Timeout.Should().Be(120);
    }

    [Fact]
    public void Parse_NetworkStepNestedOnErrorAlias_ParsesWithoutWarnings()
    {
        var yaml = """
            ---
            steps:
              - http:
                  url: "https://example.com/health"
                  on_error: continue
              - ping:
                  host: "127.0.0.1"
                  on_error: continue
              - dns:
                  host: "example.com"
                  on_error: continue
              - portcheck:
                  host: "localhost"
                  on_error: continue
              - sftp:
                  action: upload
                  local_path: "local.txt"
                  remote_path: "/tmp/remote.txt"
                  on_error: continue
              - webhook:
                  url: "https://example.com/hook"
                  on_error: continue
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(6);
        script.Steps.Should().OnlyContain(step => step.OnError == "continue");
        _parser.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Parse_NestedOnError_DoesNotOverrideExplicitStepLevelOnError()
    {
        var yaml = """
            ---
            steps:
              - on_error: stop
                http:
                  url: "https://example.com/health"
                  on_error: continue
              - http:
                  url: "https://example.com/health"
                  on_error: stop
                on_error: continue
            """;

        var script = _parser.Parse(yaml);

        script.Steps.Should().HaveCount(2);
        script.Steps[0].OnError.Should().Be("stop");
        script.Steps[1].OnError.Should().Be("continue");
        _parser.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NetworkStepRequiredFields_ReturnsLineErrors()
    {
        var yaml = """
            ---
            steps:
              - http:
                  method: GET
              - ping:
                  count: 2
              - dns:
                  type: A
              - portcheck:
                  port: 443
              - sftp:
                  action: upload
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Http requires 'url'"));
        errors.Should().Contain(e => e.Contains("Ping requires 'host'"));
        errors.Should().Contain(e => e.Contains("Dns requires 'host'"));
        errors.Should().Contain(e => e.Contains("Portcheck requires 'host'"));
        errors.Should().Contain(e => e.Contains("Sftp requires 'local_path'"));
        errors.Should().Contain(e => e.Contains("Sftp requires 'remote_path'"));
        errors.Should().OnlyContain(e => e.Contains("Line "));
    }

    [Fact]
    public void Validate_NetworkStepEnums_AndVerifyTlsType_AreEnforced()
    {
        var yaml = """
            ---
            steps:
              - http:
                  url: "https://example.com"
                  method: trace
                  auth: digest
                  content_type: yaml
                  verify_tls: "not-bool"
              - dns:
                  host: "example.com"
                  type: TXT
              - sftp:
                  action: copy
                  local_path: "a.txt"
                  remote_path: "/tmp/a.txt"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("Http 'method'"));
        errors.Should().Contain(e => e.Contains("Http 'auth'"));
        errors.Should().Contain(e => e.Contains("Http 'content_type'"));
        errors.Should().Contain(e => e.Contains("Http 'verify_tls' must be a boolean"));
        errors.Should().Contain(e => e.Contains("Dns 'type'"));
        errors.Should().Contain(e => e.Contains("Sftp 'action'"));
    }

    [Fact]
    public void Validate_HttpAuthRequirements_AreEnforced()
    {
        var yaml = """
            ---
            steps:
              - http:
                  url: "https://example.com"
                  auth: basic
              - http:
                  url: "https://example.com"
                  auth: bearer
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml);

        errors.Should().Contain(e => e.Contains("auth: basic") && e.Contains("username"));
        errors.Should().Contain(e => e.Contains("auth: basic") && e.Contains("password"));
        errors.Should().Contain(e => e.Contains("auth: bearer") && e.Contains("token"));
    }
}
