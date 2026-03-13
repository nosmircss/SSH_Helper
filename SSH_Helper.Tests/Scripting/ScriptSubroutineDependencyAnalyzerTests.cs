using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptSubroutineDependencyAnalyzerTests
{
    [Fact]
    public void AnalyzePreset_CallLiteralStringArg_DoesNotReportWordsAsMissingColumns()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                subroutines:
                  print_section:
                    params: [title]
                    steps:
                      - print: "${title}"
                steps:
                  - call:
                      subroutine: print_section
                      args:
                        title: "=== IPv4 Unique Internet Service Matches ==="
                """
        };

        var result = analyzer.AnalyzePreset(preset);

        result.ReferencedColumns.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzePreset_LocalSubroutineParamsAreNotReportedAsMissingColumns()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                subroutines:
                  normalize:
                    params: [ip, token]
                    outputs: [message]
                    steps:
                      - set:
                          expression: message = "${ip}:${token}"
                steps:
                  - call:
                      subroutine: normalize
                      args:
                        ip: source_ip
                        token: api_token
                      out:
                        message: summary
                  - print: "${summary}"
                """
        };

        var result = analyzer.AnalyzePreset(preset);

        result.ReferencedColumns.Should().BeEquivalentTo(["source_ip", "api_token"]);
        result.ReferencedColumns.Should().NotContain("ip");
        result.ReferencedColumns.Should().NotContain("token");
        result.ReferencedColumns.Should().NotContain("summary");
    }

    [Fact]
    public void AnalyzePreset_ImportedSubroutineOnlyReportsCallerSideArgs()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                steps:
                  - call:
                      subroutine: common.lookup
                      args:
                        ip: source_ip
                        region: region_code
                      out:
                        message: summary
                  - print: "${summary}"
                """
        };

        var result = analyzer.AnalyzePreset(preset);

        result.ReferencedColumns.Should().BeEquivalentTo(["source_ip", "region_code"]);
    }

    [Fact]
    public void AnalyzePreset_CallStructuredExpressionArg_ReportsNestedVariableDependencies()
    {
        var analyzer = new ScriptDependencyAnalyzer();
        var preset = new PresetInfo
        {
            Commands = """
                ---
                subroutines:
                  normalize:
                    params: [items]
                    steps:
                      - foreach:
                          iterator: item in items
                          do:
                            - print: "${item}"
                steps:
                  - call:
                      subroutine: normalize
                      args:
                        items: compact(split(source_services, ','))
                """
        };

        var result = analyzer.AnalyzePreset(preset);

        result.ReferencedColumns.Should().BeEquivalentTo(["source_services"]);
    }
}
