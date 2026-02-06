using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ReadFileCommandTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly ReadFileCommand _command = new();

    public ReadFileCommandTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ReadFileCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _testFilePath = Path.Combine(_testDirectory, "audit.jsonl");
        File.WriteAllLines(_testFilePath, new[] { "{\"ok\":true}" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_PathFromVariableWithWindowsEnvVar_ReadsFile()
    {
        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                Path = @"${qa_dir}\audit.jsonl",
                Into = "audit_entries"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("qa_dir", $@"%TEMP%\{Path.GetFileName(_testDirectory)}");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var entries = context.GetVariable("audit_entries");
        entries.Should().BeAssignableTo<List<string>>();
        ((List<string>)entries!).Should().ContainSingle().Which.Should().Be("{\"ok\":true}");
    }
}
