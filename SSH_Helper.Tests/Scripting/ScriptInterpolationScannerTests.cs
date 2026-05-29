using System.Collections.Generic;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

/// <summary>
/// Unified balanced-brace interpolation scanner (Proposal C, sub-feature 4):
/// {{ }} (canonical) and ${ } (alias) are scanned with identical balanced-brace rules,
/// so the same expression resolves identically in either form, including nesting.
/// </summary>
public class ScriptInterpolationScannerTests
{
    private static ScriptContext MakeContext()
    {
        var context = new ScriptContext();
        context.SetVariable("fruits", new List<string> { "apple", "banana", "cherry" });
        context.SetVariable("idx", 1);
        context.SetVariable("name", "world");
        return context;
    }

    [Fact]
    public void DoubleBrace_NestedArrayIndex_ResolvesViaBalancedScan()
    {
        var context = MakeContext();

        context.SubstituteVariables("{{fruits[{{idx}}]}}")
            .Should().Be("banana");
    }

    [Fact]
    public void DoubleBrace_And_Dollar_SameNestedExpression_ResolveEquivalently()
    {
        var context = MakeContext();

        var dollar = context.SubstituteVariables("Value=${fruits[${idx}]}");
        var brace = context.SubstituteVariables("Value={{fruits[{{idx}}]}}");

        brace.Should().Be(dollar);
        brace.Should().Be("Value=banana");
    }

    [Fact]
    public void SimpleForms_DoubleBrace_And_Dollar_AreEquivalent()
    {
        var context = MakeContext();

        context.SubstituteVariables("{{name}}")
            .Should().Be(context.SubstituteVariables("${name}"));
    }

    [Fact]
    public void AdjacentDoubleBraceExpressions_BothResolved()
    {
        var context = MakeContext();

        context.SubstituteVariables("{{name}}-{{name}}")
            .Should().Be("world-world");
    }

    [Fact]
    public void DollarFormInsideDoubleBrace_ResolvesInnerFirst()
    {
        var context = MakeContext();

        context.SubstituteVariables("{{fruits[${idx}]}}")
            .Should().Be("banana");
    }

    [Fact]
    public void UnclosedDoubleBrace_LeftLiteral()
    {
        var context = MakeContext();

        context.SubstituteVariables("prefix {{name")
            .Should().Be("prefix {{name");
    }

    [Fact]
    public void NoEscapeSyntax_BackslashPassedThroughIdenticallyForBothForms()
    {
        var context = MakeContext();

        // There is no escape mechanism; backslashes are literal in both forms.
        context.SubstituteVariables(@"\{{name}}").Should().Be(@"\world");
        context.SubstituteVariables(@"\${name}").Should().Be(@"\world");
    }
}
