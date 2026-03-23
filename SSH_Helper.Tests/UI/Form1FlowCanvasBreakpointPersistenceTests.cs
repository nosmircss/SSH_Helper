using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class Form1FlowCanvasBreakpointPersistenceTests
{
    [WinFormsFact]
    public void CleanupFlowCanvasExecutionStateAfterRun_PreservesPendingDebugTogglesForRerun()
    {
        using var form = new SSH_Helper.Form1();
        _ = form.Handle;

        var pendingBreakpoints = GetHashSetField(form, "_pendingBreakpoints");
        var pendingDisabledBlocks = GetHashSetField(form, "_pendingDisabledBlocks");

        pendingBreakpoints.Add("node-breakpoint");
        pendingDisabledBlocks.Add("node-disabled");

        SimulateRunStartStateFiltering(form, pendingBreakpoints, pendingDisabledBlocks, new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            ["node-breakpoint"] = "steps/0",
            ["node-disabled"] = "steps/1"
        });

        pendingBreakpoints.Should().Contain("node-breakpoint");
        pendingDisabledBlocks.Should().Contain("node-disabled");

        InvokePrivateMethod(form, "CleanupFlowCanvasExecutionStateAfterRun");

        pendingBreakpoints.Should().Contain("node-breakpoint",
            "breakpoints still visible in Flow Canvas should remain armed for the next run until the user toggles them off");
        pendingDisabledBlocks.Should().Contain("node-disabled",
            "disabled blocks should also persist across reruns until the user changes them");
    }

    private static void SimulateRunStartStateFiltering(
        SSH_Helper.Form1 form,
        HashSet<string> pendingBreakpoints,
        HashSet<string> pendingDisabledBlocks,
        IReadOnlyDictionary<string, string> nodeToStepPathMap)
    {
        var pendingBreakpointsSnapshot = pendingBreakpoints.ToList();
        var pendingDisabledSnapshot = pendingDisabledBlocks.ToList();

        InvokePrivateMethod(form, "PrepareFlowCanvasExecutionStateForRunStart");

        foreach (var nodeId in pendingBreakpointsSnapshot)
        {
            if (nodeToStepPathMap.ContainsKey(nodeId))
                pendingBreakpoints.Add(nodeId);
        }

        foreach (var nodeId in pendingDisabledSnapshot)
        {
            if (nodeToStepPathMap.ContainsKey(nodeId))
                pendingDisabledBlocks.Add(nodeId);
        }
    }

    private static HashSet<string> GetHashSetField(SSH_Helper.Form1 form, string fieldName)
    {
        var field = typeof(SSH_Helper.Form1).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"{fieldName} should exist on Form1");

        var value = field!.GetValue(form) as HashSet<string>;
        value.Should().NotBeNull($"{fieldName} should be initialized");
        return value!;
    }

    private static void InvokePrivateMethod(SSH_Helper.Form1 form, string methodName)
    {
        var method = typeof(SSH_Helper.Form1).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"{methodName} should exist on Form1");
        method!.Invoke(form, null);
    }
}

