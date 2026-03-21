using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Functions;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class FunctionRegistryTests
{
    [Fact]
    public void Register_And_TryEvaluate_DispatchesToHandler()
    {
        var registry = new FunctionRegistry();
        registry.Register("greet", (args, ctx) => $"Hello, {args}!");

        var context = new ScriptContext();
        registry.TryEvaluate("greet", "world", context, out var value).Should().BeTrue();
        value.Should().Be("Hello, world!");
    }

    [Fact]
    public void TryEvaluate_UnregisteredName_ReturnsFalse()
    {
        var registry = new FunctionRegistry();
        var context = new ScriptContext();

        registry.TryEvaluate("unknown_func", "", context, out var value).Should().BeFalse();
        value.Should().BeNull();
    }

    [Fact]
    public void Register_OverwritesExisting()
    {
        var registry = new FunctionRegistry();
        registry.Register("test", (args, ctx) => "v1");
        registry.Register("test", (args, ctx) => "v2");

        var context = new ScriptContext();
        registry.TryEvaluate("test", "", context, out var value).Should().BeTrue();
        value.Should().Be("v2");
    }

    [Fact]
    public void IsRegistered_ReturnsCorrectly()
    {
        var registry = new FunctionRegistry();
        registry.IsRegistered("test").Should().BeFalse();

        registry.Register("test", (args, ctx) => null);
        registry.IsRegistered("test").Should().BeTrue();
    }

    [Fact]
    public void TryEvaluate_IsCaseInsensitive()
    {
        var registry = new FunctionRegistry();
        registry.Register("MyFunc", (args, ctx) => "ok");

        var context = new ScriptContext();
        registry.TryEvaluate("myfunc", "", context, out var value).Should().BeTrue();
        value.Should().Be("ok");
        registry.TryEvaluate("MYFUNC", "", context, out _).Should().BeTrue();
    }

    [Fact]
    public void Count_ReflectsRegistrations()
    {
        var registry = new FunctionRegistry();
        registry.Count.Should().Be(0);

        registry.Register("a", (args, ctx) => null);
        registry.Register("b", (args, ctx) => null);
        registry.Count.Should().Be(2);
    }

    [Fact]
    public void RegisterCategory_RegistersAllFunctions()
    {
        var registry = new FunctionRegistry();
        registry.RegisterCategory(new TestCategory());

        registry.IsRegistered("cat_a").Should().BeTrue();
        registry.IsRegistered("cat_b").Should().BeTrue();
        registry.Count.Should().Be(2);
    }

    [Fact]
    public void TryEvaluate_NullOrEmptyName_ReturnsFalse()
    {
        var registry = new FunctionRegistry();
        var context = new ScriptContext();

        registry.TryEvaluate("", "", context, out _).Should().BeFalse();
        registry.TryEvaluate(null!, "", context, out _).Should().BeFalse();
        registry.TryEvaluate("  ", "", context, out _).Should().BeFalse();
    }

    [Fact]
    public void Singleton_Instance_IsNotNull()
    {
        FunctionRegistry.Instance.Should().NotBeNull();
    }

    private class TestCategory : IFunctionCategory
    {
        public void Register(FunctionRegistry registry)
        {
            registry.Register("cat_a", (args, ctx) => "A");
            registry.Register("cat_b", (args, ctx) => "B");
        }
    }
}
