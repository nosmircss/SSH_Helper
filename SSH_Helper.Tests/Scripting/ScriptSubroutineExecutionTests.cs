using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptSubroutineExecutionTests
{
    [Fact]
    public async Task ExecuteAsync_CallCopiesDeclaredOutputsBackWithoutLeakingLocals()
    {
        var yaml = """
            ---
            vars:
              user_name: nos
            subroutines:
              build_message:
                params: [name]
                outputs: [message]
                steps:
                  - set:
                      expression: message = "hello ${name}"
                  - set:
                      expression: local_only = "hidden"
            steps:
              - call:
                  subroutine: build_message
                  args:
                    name: user_name
                  out:
                    message: greeting
            """;

        var context = await ExecuteYamlAsync(yaml);

        context.GetVariableString("greeting").Should().Be("hello nos");
        context.HasVariable("local_only").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnCopiesOutputsBackAndStopsSubroutineEarly()
    {
        var yaml = """
            ---
            subroutines:
              build_message:
                params: [name]
                outputs: [message]
                steps:
                  - set:
                      expression: message = "hello ${name}"
                  - return: true
                  - set:
                      expression: message = "should not run"
            steps:
              - set:
                  expression: user_name = "nos"
              - call:
                  subroutine: build_message
                  args:
                    name: user_name
                  out:
                    message: greeting
            """;

        var context = await ExecuteYamlAsync(yaml);

        context.GetVariableString("greeting").Should().Be("hello nos");
    }

    [Fact]
    public async Task ExecuteAsync_SubroutineFailureWithOnErrorContinue_DoesNotCopyOutputsBack()
    {
        var yaml = """
            ---
            subroutines:
              fail_lookup:
                outputs: [message]
                steps:
                  - readfile:
                      path: "C:\\definitely-missing\\file.txt"
                      into: file_contents
                  - set:
                      expression: message = "changed"
            steps:
              - set:
                  expression: greeting = "keep"
              - call:
                  subroutine: fail_lookup
                  on_error: continue
                  out:
                    message: greeting
              - set:
                  expression: after = "done"
            """;

        var context = await ExecuteYamlAsync(yaml);

        context.GetVariableString("greeting").Should().Be("keep");
        context.GetVariableString("after").Should().Be("done");
    }

    [Fact]
    public async Task ExecuteAsync_SubroutineReceivesClonedListArgs()
    {
        var yaml = """
            ---
            subroutines:
              mutate_list:
                params: [items]
                steps:
                  - set:
                      expression: items = push(items, "beta")
            steps:
              - set:
                  expression: source = list("alpha")
              - call:
                  subroutine: mutate_list
                  args:
                    items: source
            """;

        var context = await ExecuteYamlAsync(yaml);

        context.GetVariable("source").Should().BeOfType<List<string>>();
        ((List<string>)context.GetVariable("source")!).Should().BeEquivalentTo(["alpha"]);
    }

    [Fact]
    public async Task ExecuteAsync_ImportedLibraryCallSupportsNestedSubroutineCalls()
    {
        var libraryPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.yaml");
        try
        {
            File.WriteAllText(libraryPath, """
                ---
                library: true
                subroutines:
                  format_name:
                    params: [name]
                    outputs: [formatted]
                    steps:
                      - set:
                          expression: formatted = upper(name)
                  entry:
                    params: [name]
                    outputs: [message]
                    steps:
                      - call:
                          subroutine: format_name
                          args:
                            name: name
                          out:
                            formatted: formatted
                      - set:
                          expression: message = "hello ${formatted}"
                """);

            var yaml = $"""
                ---
                imports:
                  - path: "{libraryPath.Replace("\\", "\\\\")}"
                    as: common
                steps:
                  - set:
                      expression: user_name = "nos"
                  - call:
                      subroutine: common.entry
                      args:
                        name: user_name
                      out:
                        message: greeting
                """;

            var context = await ExecuteYamlAsync(yaml);

            context.GetVariableString("greeting").Should().Be("hello NOS");
        }
        finally
        {
            if (File.Exists(libraryPath))
            {
                File.Delete(libraryPath);
            }
        }
    }

    private static async Task<ScriptContext> ExecuteYamlAsync(string yaml)
    {
        var parser = new ScriptParser();
        var script = parser.Parse(yaml);
        var errors = parser.Validate(script, yaml, enforceCanonicalSyntax: true);
        errors.Should().BeEmpty();

        var executor = new ScriptExecutor();
        var context = new ScriptContext();
        var result = await executor.ExecuteAsync(script, context);

        result.Status.Should().Be(ScriptExitStatus.Success);
        return context;
    }
}
