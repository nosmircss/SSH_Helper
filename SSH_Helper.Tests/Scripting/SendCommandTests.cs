using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class SendCommandTests
{
    [Fact]
    public async Task ExecuteAsync_FailOnNonZero_ZeroExitStatus_SucceedsAndStripsMarker()
    {
        var session = new FakeSendSession(command =>
            $"{command}\r\nhello from send\r\n{SendCommand.ExitStatusSentinel}:0\r\ntester$");
        var command = new SendCommand(_ => session);
        var step = new ScriptStep
        {
            Send = "printf 'hello'",
            Capture = "result",
            FailOnNonZero = true
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        session.LastCommand.Should().Contain(SendCommand.ExitStatusSentinel);
        context.GetVariableString("result").Should().Be("hello from send");
        context.GetVariableString("_output").Should().Be("hello from send");
        context.FullOutput.Should().NotContain(SendCommand.ExitStatusSentinel);
    }

    [Fact]
    public async Task ExecuteAsync_FailOnNonZero_NonZeroExitStatus_FailsAfterCapturingOutput()
    {
        var session = new FakeSendSession(command =>
            $"{command}\r\nzsh: command not found: definitely_not_a_command\r\n{SendCommand.ExitStatusSentinel}:127\r\ntester$");
        var command = new SendCommand(_ => session);
        var step = new ScriptStep
        {
            Send = "definitely_not_a_command",
            Capture = "result",
            FailOnNonZero = true
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Command exited with status 127");
        context.GetVariableString("result").Should().Contain("command not found");
        context.GetVariableString("_output").Should().Contain("definitely_not_a_command");
        context.FullOutput.Should().Contain("Command exited with status 127");
        context.FullOutput.Should().NotContain(SendCommand.ExitStatusSentinel);
    }

    [Fact]
    public async Task ExecuteAsync_FailOnNonZero_MissingMarker_FailsExplicitly()
    {
        var session = new FakeSendSession(command =>
            $"{command}\r\npartial output only\r\ntester$");
        var command = new SendCommand(_ => session);
        var step = new ScriptStep
        {
            Send = "hostname",
            Capture = "result",
            FailOnNonZero = true
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Command exit status marker was missing");
        context.GetVariableString("result").Should().Be("partial output only");
        context.FullOutput.Should().Contain("Command exit status marker was missing");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutFailOnNonZero_DoesNotFailOnShellErrorText()
    {
        var session = new FakeSendSession(command =>
            $"{command}\r\nzsh: command not found: plain_missing_command\r\ntester$");
        var command = new SendCommand(_ => session);
        var step = new ScriptStep
        {
            Send = "plain_missing_command",
            Capture = "result"
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        session.LastCommand.Should().NotContain(SendCommand.ExitStatusSentinel);
        context.GetVariableString("result").Should().Contain("plain_missing_command");
        context.FullOutput.Should().NotContain("Command exited with status");
    }

    private sealed class FakeSendSession : SendCommand.ISendCommandSession
    {
        private readonly System.Func<string, string> _execute;

        public FakeSendSession(System.Func<string, string> execute)
        {
            _execute = execute;
        }

        public string? CurrentPrompt => "tester$";

        public string LastCommand { get; private set; } = string.Empty;

        public Task<string> ExecuteAsync(
            string command,
            string? expectPattern,
            int? timeoutSeconds,
            CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Task.FromResult(_execute(command));
        }

        public Task<string> ExecuteWithRespondsAsync(
            string command,
            IReadOnlyList<(string expectPattern, string reply)> responds,
            int? timeoutSeconds,
            CancellationToken cancellationToken)
        {
            LastCommand = command;
            return Task.FromResult(_execute(command));
        }
    }
}
