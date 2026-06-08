using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptExecutorStepOutputAttributionTests
{
    // Mirrors the FortiGate flow from the bug report: a send inside an IF, followed by a
    // send after the IF. A send block must show its OWN output (empty when the command
    // produced nothing); a container block must show the output carried from the send
    // preceding its START — not the send nested inside it.
    [Fact]
    public async Task StepCompleted_SendShowsOwnOutput_ContainerShowsPrecedingSend()
    {
        var responses = new Dictionary<string, string>
        {
            ["show"] = "running-config-dump",
            ["set status disable"] = "",
            ["end"] = "Port 9001 is different from default port 514.\nDo you want to continue? (y/n)",
            ["y"] = "Port set to 9001",
            ["config log syslogd4 setting"] = "",
        };

        var executor = new ScriptExecutor(null, null, _ => new FakeSendSession(responses));

        var completed = new Dictionary<string, StepExecutionEventArgs>();
        executor.StepCompleted += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.StepPath))
                completed[e.StepPath!] = e;
        };

        var script = new Script
        {
            Steps = new List<ScriptStep>
            {
                new() { Send = "show" },                       // steps/0
                new() { Print = "checkpoint" },                // steps/1 (leaf non-send)
                new() { Send = "set status disable" },         // steps/2
                new() { Send = "end", Expect = @"Do you want to continue\? \(y/n\)" }, // steps/3
                new()
                {
                    If = "port != \"514\"",                    // steps/4 (container)
                    Then = new List<ScriptStep> { new() { Send = "y" } } // steps/4/then/0
                },
                new() { Send = "config log syslogd4 setting" } // steps/5
            }
        };

        var result = await executor.ExecuteAsync(
            script,
            new ScriptContext(new Dictionary<string, string> { ["port"] = "9001" }));

        result.Status.Should().Be(ScriptExitStatus.Success);

        // Send blocks show their own output.
        completed["steps/0"].Output.Should().Be("running-config-dump");
        completed["steps/3"].Output.Should().Contain("(y/n)");
        completed["steps/4/then/0"].Output.Should().Be("Port set to 9001");

        // A send that produced nothing shows empty (not a carried value).
        completed["steps/2"].Output.Should().BeNullOrEmpty();
        completed["steps/5"].Output.Should().BeNullOrEmpty();

        // A leaf non-send carries the output of the send preceding it.
        completed["steps/1"].Output.Should().Be("running-config-dump");

        // The container shows the send preceding its START (end), NOT the nested send (y).
        completed["steps/4"].Output.Should().Contain("(y/n)");
        completed["steps/4"].Output.Should().NotContain("Port set to 9001");
    }

    // The block AFTER a branch ("continue" block) must reference the last send that actually
    // ran in EXECUTION order — i.e. the last send in whichever branch was taken — not whatever
    // following the spine edges backward would find. One scenario per branch shape.

    [Fact]
    public async Task ContinueBlock_AfterThenBranch_CarriesLastSendInThen()
    {
        var completed = await RunAndCollectAsync(
            new() { ["pre"] = "outPre", ["thenB"] = "outThen" },
            new List<ScriptStep>
            {
                new() { Send = "pre" },                                              // steps/0
                new() { If = "1 == 1", Then = new() { new() { Send = "thenB" } } },  // steps/1 (then taken)
                new() { Print = "continue" },                                        // steps/2
            });

        completed["steps/1/then/0"].Output.Should().Be("outThen"); // branch send shows own
        completed["steps/1"].Output.Should().Be("outPre");         // container shows the send before it
        completed["steps/2"].Output.Should().Be("outThen");        // continue carries the then's last send
    }

    [Fact]
    public async Task ContinueBlock_AfterElseBranch_CarriesLastSendInElse()
    {
        var completed = await RunAndCollectAsync(
            new() { ["pre"] = "outPre", ["thenB"] = "outThen", ["elseD"] = "outElse" },
            new List<ScriptStep>
            {
                new() { Send = "pre" },
                new()
                {
                    If = "1 == 0",                                       // false -> else
                    Then = new() { new() { Send = "thenB" } },
                    Else = new() { new() { Send = "elseD" } },
                },
                new() { Print = "continue" },
            });

        completed.Should().NotContainKey("steps/1/then/0");        // then never ran
        completed["steps/1/else/0"].Output.Should().Be("outElse");
        completed["steps/1"].Output.Should().Be("outPre");
        completed["steps/2"].Output.Should().Be("outElse");        // continue carries the else's last send
    }

    [Fact]
    public async Task ContinueBlock_AfterElifBranch_CarriesLastSendInElif()
    {
        var completed = await RunAndCollectAsync(
            new() { ["pre"] = "outPre", ["elifE"] = "outElif" },
            new List<ScriptStep>
            {
                new() { Send = "pre" },
                new()
                {
                    If = "1 == 0",
                    Then = new() { new() { Send = "thenB" } },
                    Elif = new List<ElifBranch>
                    {
                        new() { If = "1 == 1", Then = new() { new() { Send = "elifE" } } },
                    },
                },
                new() { Print = "continue" },
            });

        completed["steps/1/elif/0/then/0"].Output.Should().Be("outElif");
        completed["steps/1"].Output.Should().Be("outPre");
        completed["steps/2"].Output.Should().Be("outElif");        // continue carries the elif's last send
    }

    [Fact]
    public async Task ContinueBlock_AfterSwitchCase_CarriesLastSendInCase()
    {
        var completed = await RunAndCollectAsync(
            new() { ["pre"] = "outPre", ["caseS"] = "outCase" },
            new List<ScriptStep>
            {
                new() { Send = "pre" },
                new()
                {
                    Switch = "go",
                    Cases = new List<SwitchCase>
                    {
                        new() { Value = "go", Do = new() { new() { Send = "caseS" } } },
                    },
                },
                new() { Print = "continue" },
            });

        completed["steps/1/cases/0/do/0"].Output.Should().Be("outCase");
        completed["steps/1"].Output.Should().Be("outPre");
        completed["steps/2"].Output.Should().Be("outCase");        // continue carries the case's last send
    }

    private static async Task<Dictionary<string, StepExecutionEventArgs>> RunAndCollectAsync(
        Dictionary<string, string> responses,
        List<ScriptStep> steps,
        Dictionary<string, string>? vars = null)
    {
        var executor = new ScriptExecutor(null, null, _ => new FakeSendSession(responses));
        var completed = new Dictionary<string, StepExecutionEventArgs>();
        executor.StepCompleted += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.StepPath))
                completed[e.StepPath!] = e;
        };

        var result = await executor.ExecuteAsync(
            new Script { Steps = steps },
            new ScriptContext(vars ?? new Dictionary<string, string>()));

        result.Status.Should().Be(ScriptExitStatus.Success);
        return completed;
    }

    private sealed class FakeSendSession : SendCommand.ISendCommandSession
    {
        private readonly Dictionary<string, string> _responses;

        public FakeSendSession(Dictionary<string, string> responses) => _responses = responses;

        public string? CurrentPrompt => "FG-VM64-KVM #";

        public Task<string> ExecuteAsync(string command, string? expectPattern, int? timeoutSeconds, CancellationToken cancellationToken)
            => Task.FromResult(_responses.TryGetValue(command, out var r) ? r : string.Empty);

        public Task<string> ExecuteWithRespondsAsync(string command, IReadOnlyList<(string expectPattern, string reply)> responds, int? timeoutSeconds, CancellationToken cancellationToken)
            => Task.FromResult(_responses.TryGetValue(command, out var r) ? r : string.Empty);
    }
}
