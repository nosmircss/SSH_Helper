using System.Collections.Generic;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class DebugStateStepPathTests
{
    [Fact]
    public void ShouldPauseAtStep_UsesStepPathToResolveNodeBreakpoints()
    {
        var state = new DebugState();
        state.SetNodeToStepPathMap(new Dictionary<string, string>
        {
            ["node-top"] = "steps/0",
            ["node-nested"] = "steps/0/then/0"
        });

        state.ToggleNodeBreakpoint("node-nested");

        Assert.False(state.ShouldPauseAtStep("steps/0", lineNumber: 10));
        Assert.True(state.ShouldPauseAtStep("steps/0/then/0", lineNumber: 10));
        Assert.Equal("node-nested", state.GetNodeIdForStepPath("steps/0/then/0"));
    }

    [Fact]
    public void SetNodeToStepIndexMap_Compatibility_StillResolvesPaths()
    {
        var state = new DebugState();
        state.SetNodeToStepIndexMap(new Dictionary<string, int>
        {
            ["node-1"] = 1
        });

        state.ToggleNodeBreakpoint("node-1");

        Assert.True(state.ShouldPauseAtStep("steps/1", lineNumber: 1));
        Assert.Equal("node-1", state.GetNodeIdForStepPath("steps/1"));
    }
}
