// SSH_Helper.Tests/Services/FlowCanvasBridgeLayoutPersistenceTests.cs
using System.Reflection;
using FluentAssertions;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class FlowCanvasBridgeLayoutPersistenceTests
{
    private static double Const(string name) =>
        (double)typeof(FlowCanvasBridge).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void ChildNodeMaxWidth_matches_typescript()
    {
        Const("ChildNodeMaxWidth").Should().Be(300);
        Const("MinColumnWidth").Should().Be(330);
    }
}
