using System.Threading;
using System.Collections.Generic;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class SetCommandTests
{
    private readonly SetCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_QuotedInterpolatedString_DoesNotKeepOuterQuotes()
    {
        var step = new ScriptStep
        {
            Set = "result_str = \"${hn} | Kernel ${ver}\""
        };

        var context = new ScriptContext();
        context.SetVariable("hn", "chris-NUC7i7DNHE");
        context.SetVariable("ver", "6.8.0-90-generic");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("result_str").Should().Be("chris-NUC7i7DNHE | Kernel 6.8.0-90-generic");
    }

    [Fact]
    public async Task ExecuteAsync_QuotedLiteral_StoresWithoutQuotes()
    {
        var step = new ScriptStep
        {
            Set = "status = \"QA Complete\""
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("status").Should().Be("QA Complete");
    }

    [Fact]
    public async Task ExecuteAsync_PushWithQuotedString_DoesNotStoreLiteralQuoteCharacters()
    {
        var step = new ScriptStep
        {
            Set = "services = push(services, \"sshd\")"
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var services = context.GetVariable("services").Should().BeOfType<List<string>>().Subject;
        services.Should().ContainSingle().Which.Should().Be("sshd");
    }

    [Fact]
    public async Task ExecuteAsync_PushList_DebugOutputShowsReadableListValues()
    {
        var step = new ScriptStep
        {
            Set = "services = push(services, \"sshd\")"
        };

        var context = new ScriptContext
        {
            DebugMode = true
        };

        string? debugMessage = null;
        context.OutputReceived += (_, args) =>
        {
            if (args.Type == ScriptOutputType.Debug)
                debugMessage = args.Message;
        };

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        debugMessage.Should().Be("Set services = [sshd]");
    }
}
