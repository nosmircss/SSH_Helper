using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ExtractCommandTests
{
    private readonly ExtractCommand _command = new();

    [Fact]
    public async Task ExecuteAsync_MultipleCaptureGroups_AllowsRootMountpoint()
    {
        var step = new ScriptStep
        {
            Extract = new ExtractOptions
            {
                From = "disk_line",
                Pattern = "(\\d+%)\\s+(/\\S*)",
                Into = new List<string> { "disk_pct", "disk_mount" }
            }
        };

        var context = new ScriptContext();
        context.SetVariable("disk_line", "/dev/mapper/mint--vg-root  467G  133G  311G  30% /");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("disk_pct").Should().Be("30%");
        context.GetVariableString("disk_mount").Should().Be("/");
    }

    [Fact]
    public async Task ExecuteAsync_PlusQuantifier_DoesNotMatchBareSlashMountpoint()
    {
        var step = new ScriptStep
        {
            Extract = new ExtractOptions
            {
                From = "disk_line",
                Pattern = "(\\d+%)\\s+(/\\S+)",
                Into = new List<string> { "disk_pct", "disk_mount" }
            }
        };

        var context = new ScriptContext();
        context.SetVariable("disk_line", "/dev/mapper/mint--vg-root  467G  133G  311G  30% /");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariableString("disk_pct").Should().BeEmpty();
        context.GetVariableString("disk_mount").Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_CatastrophicPattern_IsBoundedByTimeout()
    {
        var step = new ScriptStep
        {
            Extract = new ExtractOptions
            {
                From = "payload",
                Pattern = "(a+)+$",
                Into = "result"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("payload", new string('a', 6000) + "!");
        var stopwatch = Stopwatch.StartNew();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(7));
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_EmptySource_InitializesIntoVariableSoIsEmptyChecksWork()
    {
        var step = new ScriptStep
        {
            Extract = new ExtractOptions
            {
                From = "command_output",
                Pattern = "Version: (.+)",
                Into = "version"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("command_output", string.Empty);

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);
        var evaluator = new ExpressionEvaluator(context);

        result.Success.Should().BeTrue();
        context.HasVariable("version").Should().BeTrue();
        context.GetVariableString("version").Should().BeEmpty();
        evaluator.Evaluate("version is empty").Should().BeTrue();
    }
}
