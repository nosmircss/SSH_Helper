using FluentAssertions;
using System.Diagnostics;
using System.Collections.Generic;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_ParenthesizedGroupWithOr_ReturnsTrue()
    {
        var context = new ScriptContext();
        context.SetVariable("x", 42);
        context.SetVariable("name", "TestHost");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("(x > 10 and x < 50) or name == 'Other'");

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NestedGroupInRightOperand_RespectsGrouping()
    {
        var context = new ScriptContext();
        context.SetVariable("x", 5);
        context.SetVariable("name", "TestHost");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("x > 10 or (x < 10 and name == 'TestHost')");

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ParenthesizedGroupWithOr_ReturnsFalseWhenAllFalse()
    {
        var context = new ScriptContext();
        context.SetVariable("x", 5);
        context.SetVariable("name", "Nope");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("(x > 10 and x < 50) or name == 'Other'");

        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MatchesCatastrophicPattern_CompletesWithoutThrowing()
    {
        var context = new ScriptContext();
        context.SetVariable("payload", new string('a', 6000) + "!");

        var evaluator = new ExpressionEvaluator(context);
        var stopwatch = Stopwatch.StartNew();

        var result = evaluator.Evaluate("payload matches '(a+)+$'");

        stopwatch.Stop();
        result.Should().BeFalse();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void Evaluate_VariableWithSpaces_ResolvesCorrectly()
    {
        var context = new ScriptContext();
        context.SetVariable("name", "John Doe");

        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("${name} == \"John Doe\"");

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NestedStringFunctionsInCondition_ResolvesCorrectly()
    {
        var context = new ScriptContext();
        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("trim(upper('  admin  ')) == 'ADMIN'");

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NestedJsonFunctionWithPlaceholdersInCondition_ResolvesCorrectly()
    {
        var context = new ScriptContext();
        context.SetVariable("admins", "{\"system\":{\"admin\":{\"admin\":{\"trusthost1\":\" 10.0.0.0 255.255.255.0 \"}}}}");
        context.SetVariable("admin_name", "admin");
        context.SetVariable("i", 1);
        var evaluator = new ExpressionEvaluator(context);

        var result = evaluator.Evaluate("trim(json.get(admins, \"system.admin.${admin_name}.trusthost${i}\", \"\")) == '10.0.0.0 255.255.255.0'");

        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_InOperator_WithList_IsCaseInsensitive()
    {
        var context = new ScriptContext();
        context.SetVariable("svc_key", "amazon-aws");
        context.SetVariable("exclude_service_matches_norm", new List<string> { "cloudflare-cdn", "amazon-aws" });
        var evaluator = new ExpressionEvaluator(context);

        evaluator.Evaluate("svc_key in exclude_service_matches_norm").Should().BeTrue();
        evaluator.Evaluate("svc_key not in exclude_service_matches_norm").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_InOperator_WithJsonArrayString_UsesCollectionMembership()
    {
        var context = new ScriptContext();
        context.SetVariable("svc_key", "cloudflare-web");
        context.SetVariable("exclude_service_matches_norm", "[\"cloudflare-cdn\",\"cloudflare-web\"]");
        var evaluator = new ExpressionEvaluator(context);

        evaluator.Evaluate("svc_key in exclude_service_matches_norm").Should().BeTrue();
    }

    [Fact]
    public void Evaluate_InOperator_WithNewlineDelimitedString_UsesLines()
    {
        var context = new ScriptContext();
        context.SetVariable("needle", "beta");
        context.SetVariable("haystack", "alpha\r\nbeta\r\ngamma");
        var evaluator = new ExpressionEvaluator(context);

        evaluator.Evaluate("needle in haystack").Should().BeTrue();
    }

    [Fact]
    public void Evaluate_IsEmpty_OnJsonCollections_UsesStructuralSemantics()
    {
        var context = new ScriptContext();
        var evaluator = new ExpressionEvaluator(context);

        context.SetVariable("items", "[]");
        evaluator.Evaluate("items is empty").Should().BeTrue();
        evaluator.Evaluate("items").Should().BeFalse();

        context.SetVariable("items", "{}");
        evaluator.Evaluate("items is empty").Should().BeTrue();
        evaluator.Evaluate("items").Should().BeFalse();

        context.SetVariable("items", "{\"value\":1}");
        evaluator.Evaluate("items is not empty").Should().BeTrue();
        evaluator.Evaluate("items").Should().BeTrue();
    }
}
