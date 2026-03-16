using System.IO;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptSubroutineParserValidationTests
{
    private readonly ScriptParser _parser = new();

    [Fact]
    public void Parse_WithImportsSubroutinesCallAndReturn_ParsesNewLanguageElements()
    {
        var yaml = """
            ---
            imports:
              - path: "C:\\Scripts\\common.yaml"
                as: common
            subroutines:
              lookup:
                params: [ip]
                outputs: [result]
                steps:
                  - return: true
            steps:
              - call:
                  subroutine: common.lookup
                  args:
                    ip: host_ip
                  out:
                    result: lookup_result
            """;

        var script = _parser.Parse(yaml);

        script.Imports.Should().ContainSingle();
        script.Imports[0].Alias.Should().Be("common");
        script.Subroutines.Should().ContainKey("lookup");
        script.Subroutines["lookup"].Params.Should().BeEquivalentTo(["ip"]);
        script.Subroutines["lookup"].Outputs.Should().BeEquivalentTo(["result"]);
        script.Subroutines["lookup"].Steps.Should().ContainSingle();
        script.Subroutines["lookup"].Steps[0].GetStepType().Should().Be(SSH_Helper.Services.Scripting.Models.StepType.Return);
        script.Steps.Should().ContainSingle();
        var call = script.Steps[0].Call;
        call.Should().NotBeNull();
        call!.Subroutine.Should().Be("common.lookup");
        call.Args.Should().ContainKey("ip");
        call.Out.Should().ContainKey("result");
    }

    [Fact]
    public void Validate_LibraryDefinition_WithAllowFlag_Succeeds()
    {
        var yaml = """
            ---
            library: true
            subroutines:
              format_message:
                params: [name]
                outputs: [message]
                steps:
                  - set:
                      expression: message = "hello ${name}"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true, allowLibraryDefinitions: true);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_LibraryDefinition_WithoutAllowFlag_FailsDirectExecution()
    {
        var yaml = """
            ---
            library: true
            subroutines:
              noop:
                steps:
                  - return: true
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("cannot be executed directly"));
    }

    [Fact]
    public void Validate_RelativeImportPath_IsRejected()
    {
        var yaml = """
            ---
            imports:
              - path: ".\\common.yaml"
                as: common
            steps:
              - print: "test"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("import path must be absolute"));
    }

    [Fact]
    public void Validate_CallArgAndOutputBindings_AreValidatedAgainstSignature()
    {
        var yaml = """
            ---
            subroutines:
              lookup:
                params: [ip, token]
                outputs: [result]
                steps:
                  - return: true
            steps:
              - call:
                  subroutine: lookup
                  args:
                    ip: host_ip
                    extra: other_value
                  out:
                    result: good_target
                    not_declared: bad_target
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("missing required arg 'token'"));
        errors.Should().Contain(error => error.Contains("unknown arg 'extra'"));
        errors.Should().Contain(error => error.Contains("unknown output 'not_declared'"));
    }

    [Fact]
    public void Validate_ReturnOutsideSubroutine_Fails()
    {
        var yaml = """
            ---
            steps:
              - return: true
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("return can only be used inside subroutines"));
    }

    [Fact]
    public void Validate_LocalSubroutineCycle_IsRejected()
    {
        var yaml = """
            ---
            subroutines:
              first:
                steps:
                  - call:
                      subroutine: second
              second:
                steps:
                  - call:
                      subroutine: first
            steps:
              - call:
                  subroutine: first
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("recursive call cycle detected"));
    }

    [Fact]
    public void Validate_ImportedLibrary_IsLoadedAndResolved()
    {
        var libraryPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.yaml");
        try
        {
            File.WriteAllText(libraryPath, """
                ---
                library: true
                subroutines:
                  format_message:
                    params: [name]
                    outputs: [message]
                    steps:
                      - set:
                          expression: message = "hello ${name}"
                """);

            var yaml = $"""
                ---
                imports:
                  - path: "{libraryPath.Replace("\\", "\\\\")}"
                    as: common
                steps:
                  - call:
                      subroutine: common.format_message
                      args:
                        name: user_name
                      out:
                        message: message
                """;

            var script = _parser.Parse(yaml);
            var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

            errors.Should().BeEmpty();
            script.SubroutineRegistry.Should().NotBeNull();
            script.SubroutineRegistry!.ImportsByAlias.Should().ContainKey("common");
        }
        finally
        {
            if (File.Exists(libraryPath))
            {
                File.Delete(libraryPath);
            }
        }
    }
}
