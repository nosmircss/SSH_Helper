using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptDependencyAnalyzerTests
{
    [Fact]
    public void AnalyzePresets_WithSimpleReferences_ReportsAllMissingCandidates()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = "echo {{foo}}\r\necho ${bar}"
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().BeEquivalentTo("foo", "bar");
    }

    [Fact]
    public void AnalyzePresets_WithExternallyResolvedVariables_ExcludesEnvironmentVariables()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = "echo {{foo}}\r\necho ${bar}\r\necho {{baz}}"
        };
        var externallyResolved = new[] { "FOO", "  baz  ", "", "   " };

        var result = analyzer.AnalyzePresets(new[] { preset }, externallyResolved);

        result.ReferencedColumns.Should().BeEquivalentTo("bar");
    }

    [Fact]
    public void AnalyzePresets_UpdateEnvironmentDefinesVariableForLaterSteps()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - updateenvironment:
                      variable: api_token
                      value: "${token_from_api}"
                  - print: "${api_token}"
                """
        };

        var result = analyzer.AnalyzePresets(new[] { preset });

        result.ReferencedColumns.Should().Contain("token_from_api");
        result.ReferencedColumns.Should().NotContain("api_token");
    }
}
