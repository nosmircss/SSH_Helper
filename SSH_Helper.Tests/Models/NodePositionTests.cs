using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class NodePositionTests
{
    [Fact]
    public void Clone_copies_stepPath_and_blockType()
    {
        var layout = new CanvasLayoutData();
        layout.Positions["node-0"] = new NodePosition { X = 1, Y = 2, StepPath = "steps/0", BlockType = "print" };

        var clone = layout.Clone();

        var p = clone.Positions["node-0"];
        p.X.Should().Be(1);
        p.StepPath.Should().Be("steps/0");
        p.BlockType.Should().Be("print");
    }
}
