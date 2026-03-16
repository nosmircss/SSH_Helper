using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;
using System.IO;

namespace SSH_Helper.Tests.Scripting;

public class CanonicalCommandMapSyntaxTests
{
    private readonly ScriptParser _parser = new();
    private const string RepoSamplesPlaceholderRoot = @"C:\\Path\\To\\SSH_Helper\\ScriptSamples";
    private const string QaCatalogLibraryPlaceholder = "__QA_CATALOG_LIBRARY_PATH__";

    [Fact]
    public void Parse_CanonicalMapSyntax_ParsesConvertedCommands()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: hostname
                  capture: host_result
              - print:
                  message: "Host=${host_result}"
              - wait:
                  seconds: 2
              - set:
                  expression: "counter = 1"
              - if:
                  condition: "counter == 1"
                  then:
                    - print:
                        message: ok
              - foreach:
                  iterator: "item in items"
                  do:
                    - print:
                        message: "${item}"
              - while:
                  condition: "counter < 3"
                  max_iterations: 3
                  do:
                    - set:
                        expression: "counter = counter + 1"
              - try:
                  do:
                    - print:
                        message: "inside"
                  catch:
                    - print:
                        message: "caught"
                  finally:
                    - print:
                        message: "done"
              - exit:
                  status: success
                  message: "Complete"
            """;

        var script = _parser.Parse(yaml);
        script.Steps.Should().HaveCount(9);

        script.Steps[0].GetStepType().Should().Be(StepType.Send);
        script.Steps[0].Send.Should().Be("hostname");
        script.Steps[0].Capture.Should().Be("host_result");

        script.Steps[1].GetStepType().Should().Be(StepType.Print);
        script.Steps[1].Print.Should().Be("Host=${host_result}");

        script.Steps[2].GetStepType().Should().Be(StepType.Wait);
        script.Steps[2].Wait.Should().Be(2);

        script.Steps[3].GetStepType().Should().Be(StepType.Set);
        script.Steps[3].Set.Should().Be("counter = 1");

        script.Steps[4].GetStepType().Should().Be(StepType.If);
        script.Steps[4].If.Should().Be("counter == 1");
        script.Steps[4].Then.Should().NotBeNullOrEmpty();

        script.Steps[5].GetStepType().Should().Be(StepType.Foreach);
        script.Steps[5].Foreach.Should().Be("item in items");
        script.Steps[5].Do.Should().NotBeNullOrEmpty();

        script.Steps[6].GetStepType().Should().Be(StepType.While);
        script.Steps[6].While.Should().Be("counter < 3");
        script.Steps[6].MaxIterations.Should().Be(3);
        script.Steps[6].Do.Should().NotBeNullOrEmpty();

        script.Steps[7].GetStepType().Should().Be(StepType.Try);
        script.Steps[7].Try.Should().NotBeNullOrEmpty();
        script.Steps[7].Catch.Should().NotBeNullOrEmpty();
        script.Steps[7].Finally.Should().NotBeNullOrEmpty();

        script.Steps[8].GetStepType().Should().Be(StepType.Exit);
        script.Steps[8].Exit.Should().Be("success Complete");

        _parser.Validate(script, yaml, enforceCanonicalSyntax: true).Should().BeEmpty();
    }

    [Fact]
    public void Validate_EnforceCanonicalSyntax_AcceptsShorthandSend()
    {
        var yaml = """
            ---
            steps:
              - send: echo hi
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().BeEmpty();
        script.Steps[0].Send.Should().Be("echo hi");
    }

    [Fact]
    public void Validate_EnforceCanonicalSyntax_AcceptsShorthandControlFlowHeaders()
    {
        var yaml = """
            ---
            steps:
              - if: i < 3
                then:
                  - print: tick
              - foreach: item in items
                do:
                  - print: ${item}
              - while: i < 3
                do:
                  - print:
                      message: tick
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().BeEmpty();
        script.Steps[0].If.Should().Be("i < 3");
        script.Steps[1].Foreach.Should().Be("item in items");
        script.Steps[2].While.Should().Be("i < 3");
    }

    [Fact]
    public void Validate_EnforceCanonicalSyntax_RequiresSendCommand()
    {
        var yaml = """
            ---
            steps:
              - send:
                  capture: output
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("send.command is required"));
    }

    [Fact]
    public void Validate_EnforceCanonicalSyntax_RejectsRootOnErrorForCommandMap()
    {
        var yaml = """
            ---
            steps:
              - send:
                  command: echo hi
                on_error: continue
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().Contain(error => error.Contains("step-level on_error is not supported for 'send'"));
    }

    [Fact]
    public void Validate_EnforceCanonicalSyntax_AcceptsShorthandExit()
    {
        var yaml = """
            ---
            steps:
              - exit: "Done"
              - exit: failure "Failed"
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().BeEmpty();
        script.Steps[0].Exit.Should().Be("success Done");
        script.Steps[1].Exit.Should().Be("failure \"Failed\"");
    }

    [Fact]
    public void Validate_EnforceCanonicalSyntax_AcceptsInteractiveCaptureMap()
    {
        var yaml = """
            ---
            steps:
              - interactive:
                  session: separate
                  command: tcpdump -i any
                  capture: pcap_output
                  max_seconds: 30
                  mirror_output: true
                  on_error: continue
            """;

        var script = _parser.Parse(yaml);
        var errors = _parser.Validate(script, yaml, enforceCanonicalSyntax: true);

        errors.Should().BeEmpty();
        script.Steps[0].GetStepType().Should().Be(StepType.Interactive);
    }

    [Fact]
    public void Validate_ScriptSamples_AreCanonicalAndPassEnforcedValidation()
    {
        var repoRoot = FindRepositoryRoot();
        var samplesRoot = Path.Combine(repoRoot, "ScriptSamples");
        var sampleFiles = Directory.GetFiles(samplesRoot, "*.yaml", SearchOption.AllDirectories);
        sampleFiles.Should().NotBeEmpty();

        foreach (var sampleFile in sampleFiles)
        {
            var text = NormalizeSampleTextForValidation(sampleFile, File.ReadAllText(sampleFile), samplesRoot);
            Script script;
            try
            {
                script = _parser.Parse(text);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse sample script '{sampleFile}': {ex.Message}", ex);
            }

            var errors = _parser.Validate(
                script,
                text,
                enforceCanonicalSyntax: true,
                allowLibraryDefinitions: script.Library);
            errors.Should().BeEmpty($"script sample should be canonical: {sampleFile}");
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? search = new(AppContext.BaseDirectory);
        while (search != null && !Directory.Exists(Path.Combine(search.FullName, "ScriptSamples")))
        {
            search = search.Parent;
        }

        search.Should().NotBeNull("test should run from within the repository tree");
        return search!.FullName;
    }

    private static string NormalizeSampleTextForValidation(string sampleFile, string text, string samplesRoot)
    {
        var normalized = text.Replace(RepoSamplesPlaceholderRoot, samplesRoot.Replace("\\", "\\\\"));

        if (sampleFile.EndsWith(Path.Combine("ScriptSamples", "qa", "catalog_runner.yaml"), System.StringComparison.OrdinalIgnoreCase))
        {
            var qaLibraryPath = Path.Combine(samplesRoot, "qa", "catalog_library.yaml").Replace("\\", "\\\\");
            normalized = normalized.Replace(QaCatalogLibraryPlaceholder, qaLibraryPath);
        }

        return normalized;
    }
}
