using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Terminal;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class InteractiveCommandTests
{
    [Fact]
    public async Task ExecuteAsync_ServiceSuccess_ReturnsOk()
    {
        var command = new InteractiveCommand(new StubInteractiveTerminalService(
            (_, _, _) => Task.FromResult(InteractiveTerminalRunResult.Ok())));

        var step = new ScriptStep
        {
            Interactive = new InteractiveOptions()
        };

        var result = await command.ExecuteAsync(step, new ScriptContext(), CancellationToken.None);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SharedUnavailable_ReturnsFailure()
    {
        var command = new InteractiveCommand(new StubInteractiveTerminalService(
            (_, _, _) => Task.FromResult(InteractiveTerminalRunResult.SharedUnavailableResult(
                "InteractiveSharedUnavailable: no active shared SSH shell session is available for session=shared."))));

        var step = new ScriptStep
        {
            Interactive = new InteractiveOptions()
        };

        var result = await command.ExecuteAsync(step, new ScriptContext(), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("InteractiveSharedUnavailable");
    }

    [Fact]
    public async Task ExecuteAsync_SharedUnavailable_WithOnErrorContinue_IsSuppressed()
    {
        var command = new InteractiveCommand(new StubInteractiveTerminalService(
            (_, _, _) => Task.FromResult(InteractiveTerminalRunResult.SharedUnavailableResult(
                "InteractiveSharedUnavailable: no active shared SSH shell session is available for session=shared."))));

        var step = new ScriptStep
        {
            Interactive = new InteractiveOptions(),
            OnError = "continue"
        };

        var result = await command.ExecuteAsync(step, new ScriptContext(), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var command = new InteractiveCommand(new StubInteractiveTerminalService(
            (_, _, token) => Task.FromCanceled<InteractiveTerminalRunResult>(token)));

        var step = new ScriptStep
        {
            Interactive = new InteractiveOptions()
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await command.ExecuteAsync(step, new ScriptContext(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed class StubInteractiveTerminalService : IInteractiveTerminalService
    {
        private readonly Func<ScriptContext, InteractiveOptions, CancellationToken, Task<InteractiveTerminalRunResult>> _handler;

        public StubInteractiveTerminalService(Func<ScriptContext, InteractiveOptions, CancellationToken, Task<InteractiveTerminalRunResult>> handler)
        {
            _handler = handler;
        }

        public Task<InteractiveTerminalRunResult> RunAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            return _handler(context, options, cancellationToken);
        }
    }
}
