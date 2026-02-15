using FluentAssertions;
using SSH_Helper.Models;
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

    [Fact]
    public async Task ExecuteAsync_ServiceAddsInteractiveAudit_ContextRetainsSession()
    {
        var command = new InteractiveCommand(new StubInteractiveTerminalService(
            (context, _, _) =>
            {
                context.AddInteractiveSession(new InteractiveTerminalSessionDetails
                {
                    HostAddress = "10.0.0.1",
                    SessionMode = "separate",
                    EmulationMode = "full",
                    StartedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                    EndedAtUtc = DateTime.UtcNow,
                    CloseReason = "user_closed",
                    Completed = true,
                    Transcript = "show version"
                });

                return Task.FromResult(InteractiveTerminalRunResult.Ok());
            }));

        var context = new ScriptContext();
        var step = new ScriptStep
        {
            Interactive = new InteractiveOptions()
        };

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var sessions = context.GetInteractiveSessionsSnapshot();
        sessions.Should().ContainSingle();
        sessions[0].CloseReason.Should().Be("user_closed");
        sessions[0].Transcript.Should().Contain("show version");
    }

    [Fact]
    public async Task ExecuteAsync_WithCaptureVariable_StoresTranscriptAndOutput()
    {
        var command = new InteractiveCommand(new StubInteractiveTerminalService(
            (_, _, _) => Task.FromResult(InteractiveTerminalRunResult.Ok("packet line 1\r\npacket line 2", "ctrl_c_continue"))));

        var context = new ScriptContext();
        var step = new ScriptStep
        {
            Interactive = new InteractiveOptions
            {
                Capture = "sniffer_capture"
            }
        };

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("sniffer_capture").Should().Contain("packet line 1");
        context.GetVariableString("_output").Should().Contain("packet line 2");
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesShowWindowOption()
    {
        InteractiveOptions? receivedOptions = null;
        var command = new InteractiveCommand(new StubInteractiveTerminalService(
            (_, options, _) =>
            {
                receivedOptions = options;
                return Task.FromResult(InteractiveTerminalRunResult.Ok());
            }));

        var step = new ScriptStep
        {
            Interactive = new InteractiveOptions
            {
                Command = "tcpdump -i any",
                MaxLines = 150,
                Width = 180,
                Height = 48,
                Columns = 120,
                Rows = 36,
                ShowWindow = false
            }
        };

        var result = await command.ExecuteAsync(step, new ScriptContext(), CancellationToken.None);

        result.Success.Should().BeTrue();
        receivedOptions.Should().NotBeNull();
        receivedOptions!.MaxLines.Should().Be(150);
        receivedOptions!.Width.Should().Be(180);
        receivedOptions!.Height.Should().Be(48);
        receivedOptions!.Columns.Should().Be(120);
        receivedOptions!.Rows.Should().Be(36);
        receivedOptions!.ShowWindow.Should().BeFalse();
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
