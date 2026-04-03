using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class Form1FlowCanvasTestStepScopingTests
{
    [Fact]
    public void TryBuildYamlThroughTopLevelStep_TruncatesToRequestedStep()
    {
        var yaml = """
            ---
            name: scoped-script
            vars:
              token: abc
            steps:
              - print: first
              - if: "${flag}"
                then:
                  - print: nested
              - print: last
            """;

        var method = GetStaticMethod("TryBuildYamlThroughTopLevelStep");
        var args = new object?[] { yaml, 1, null, null };

        var success = (bool)method.Invoke(null, args)!;
        var truncatedYaml = args[2] as string;
        var error = args[3] as string;

        success.Should().BeTrue();
        error.Should().BeEmpty();
        truncatedYaml.Should().NotBeNull();
        truncatedYaml.Should().Contain("name: scoped-script");
        truncatedYaml.Should().Contain("vars:");
        truncatedYaml.Should().NotContain("- print: last");

        var parser = new ScriptParser();
        var script = parser.Parse(truncatedYaml!);
        script.Steps.Should().HaveCount(2);
        script.Steps[0].Print.Should().Be("first");
        script.Steps[1].If.Should().Be("${flag}");
        script.Vars.Should().ContainKey("token");
    }

    [Fact]
    public void TryBuildYamlThroughTopLevelStep_NonYaml_ReturnsError()
    {
        const string nonYaml = "show version\nshow interface status";
        var method = GetStaticMethod("TryBuildYamlThroughTopLevelStep");
        var args = new object?[] { nonYaml, 0, null, null };

        var success = (bool)method.Invoke(null, args)!;
        var error = args[3] as string;

        success.Should().BeFalse();
        error.Should().Be("Test-step requires a YAML script.");
    }

    [Fact]
    public void BuildTestStepAllowedRoots_NestedTarget_IncludesPrerequisiteChain()
    {
        var method = GetStaticMethod("BuildTestStepAllowedRoots");
        var roots = (HashSet<string>)method.Invoke(null, new object?[] { "steps/2/then/3/else/1", 2 })!;

        roots.Should().Contain("steps/0");
        roots.Should().Contain("steps/1");
        roots.Should().Contain("steps/2");
        roots.Should().Contain("steps/2/then/0");
        roots.Should().Contain("steps/2/then/1");
        roots.Should().Contain("steps/2/then/2");
        roots.Should().Contain("steps/2/then/3");
        roots.Should().Contain("steps/2/then/3/else/0");
        roots.Should().Contain("steps/2/then/3/else/1");
        roots.Should().NotContain("steps/2/then/4");
    }

    [Fact]
    public void IsStepPathAllowed_RespectsRootsAndDescendants()
    {
        var method = GetStaticMethod("IsStepPathAllowed");
        var roots = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "steps/0",
            "steps/2/then/1"
        };

        var topLevelAllowed = (bool)method.Invoke(null, new object?[] { "steps/0/do/0", roots })!;
        var nestedAllowed = (bool)method.Invoke(null, new object?[] { "steps/2/then/1/else/0", roots })!;
        var siblingBlocked = (bool)method.Invoke(null, new object?[] { "steps/2/then/2", roots })!;
        var unrelatedBlocked = (bool)method.Invoke(null, new object?[] { "steps/3", roots })!;

        topLevelAllowed.Should().BeTrue();
        nestedAllowed.Should().BeTrue();
        siblingBlocked.Should().BeFalse();
        unrelatedBlocked.Should().BeFalse();
    }

    private static MethodInfo GetStaticMethod(string name)
    {
        var method = typeof(global::SSH_Helper.Form1).GetMethod(
            name,
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull($"Form1 should expose private static helper '{name}' for test-step scoping behavior.");
        return method!;
    }
}
