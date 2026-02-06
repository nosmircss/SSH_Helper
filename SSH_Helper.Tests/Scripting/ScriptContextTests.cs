using System.Collections.Generic;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptContextTests
{
    [Fact]
    public void SubstituteVariables_DynamicArrayIndexUsingNestedInterpolation_ResolvesItem()
    {
        var context = new ScriptContext();
        context.SetVariable("fruits", new List<string> { "apple", "banana", "cherry" });
        context.SetVariable("idx", 1);

        var result = context.SubstituteVariables("Value=${fruits[${idx}]}");

        result.Should().Be("Value=banana");
    }

    [Fact]
    public void SubstituteVariables_DynamicArrayIndexUsingIndexVariable_ResolvesItem()
    {
        var context = new ScriptContext();
        context.SetVariable("fruits", new List<string> { "apple", "banana", "cherry" });
        context.SetVariable("idx", 2);

        var result = context.SubstituteVariables("Value=${fruits[idx]}");

        result.Should().Be("Value=cherry");
    }

    [Fact]
    public void SubstituteVariables_DoubleBraceExpression_ResolvesItem()
    {
        var context = new ScriptContext();
        context.SetVariable("Host_IP", "10.0.0.5");

        var result = context.SubstituteVariables("Host={{Host_IP}}");

        result.Should().Be("Host=10.0.0.5");
    }
}
