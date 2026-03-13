using FluentAssertions;
using SSH_Helper.Services.Editor;
using Xunit;

namespace SSH_Helper.Tests.Editor;

public class ScriptSubroutineEditorTests
{
    [Fact]
    public void GetCompletion_TopLevelPrefix_SuggestsImportsSubroutinesAndLibrary()
    {
        var provider = new ScriptAutocompleteProvider();

        var importsCompletion = provider.GetCompletion("im", 2);
        var subroutinesCompletion = provider.GetCompletion("sub", 3);
        var libraryCompletion = provider.GetCompletion("li", 2);

        importsCompletion.Items.Select(item => item.Label).Should().Contain("imports");
        subroutinesCompletion.Items.Select(item => item.Label).Should().Contain("subroutines");
        libraryCompletion.Items.Select(item => item.Label).Should().Contain("library");
    }

    [Fact]
    public void GetCompletion_CallStepOptionKey_SuggestsSubroutineArgsAndOut()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = "steps:\n  - call:\n      ";

        var completion = provider.GetCompletion(text, text.Length);

        completion.Context.Should().Be(CompletionContextKind.StepOptionKey);
        completion.Items.Select(item => item.Label).Should().Contain(["subroutine", "args", "out", "on_error"]);
    }

    [Fact]
    public void GetInterpolationSymbols_SubroutineParamsOutputsAndCallOutBindings_AreIncluded()
    {
        var provider = new ScriptAutocompleteProvider();
        var text = """
            subroutines:
              lookup:
                params: [ip]
                outputs:
                  - message
                steps:
                  - set:
                      expression: local_value = "${ip}"
            steps:
              - call:
                  subroutine: lookup
                  out:
                    message: greeting
            """;

        var symbols = provider.GetInterpolationSymbols(text);

        symbols.Should().Contain(["ip", "message", "local_value", "greeting"]);
    }

    [Fact]
    public void BuildLineHighlights_RecognizesCallReturnAndLibraryTokens()
    {
        var highlighter = new YamlSshSyntaxHighlighter();
        var callLine = "  - call:";
        var returnLine = "      - return: true";
        var libraryLine = "library: true";

        var callHighlights = highlighter.BuildLineHighlights(callLine, 0, darkMode: false);
        var returnHighlights = highlighter.BuildLineHighlights(returnLine, 0, darkMode: false);
        var libraryHighlights = highlighter.BuildLineHighlights(libraryLine, 0, darkMode: false);

        callHighlights.Should().Contain(span => span.Length == "call".Length);
        returnHighlights.Should().Contain(span => span.Length == "return".Length);
        libraryHighlights.Should().Contain(span => span.Start == 0 && span.Length == "library".Length);
    }
}
