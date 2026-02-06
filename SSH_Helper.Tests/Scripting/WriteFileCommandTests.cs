using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class WriteFileCommandTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly WriteFileCommand _command = new();

    public WriteFileCommandTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WriteFileCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_JsonlOverwriteThenAppend_WritesThreeSeparateJsonObjectLines()
    {
        var filePath = Path.Combine(_testDirectory, "events.jsonl");
        var context = new ScriptContext();
        context.SetVariable("record1", "{\"host\":\"srv1\",\"status\":\"up\",\"cpu\":23}");
        context.SetVariable("record2", "{\"host\":\"srv2\",\"status\":\"down\",\"cpu\":0}");
        context.SetVariable("record3", "{\"host\":\"srv3\",\"status\":\"up\",\"cpu\":67}");

        var overwriteStep = BuildJsonlStep(filePath, "${record1}", "overwrite");
        var appendStepTwo = BuildJsonlStep(filePath, "${record2}", "append");
        var appendStepThree = BuildJsonlStep(filePath, "${record3}", "append");

        (await _command.ExecuteAsync(overwriteStep, context, CancellationToken.None)).Success.Should().BeTrue();
        (await _command.ExecuteAsync(appendStepTwo, context, CancellationToken.None)).Success.Should().BeTrue();
        (await _command.ExecuteAsync(appendStepThree, context, CancellationToken.None)).Success.Should().BeTrue();

        var lines = File.ReadAllLines(filePath);
        lines.Should().HaveCount(3);

        using var firstDoc = JsonDocument.Parse(lines[0]);
        using var secondDoc = JsonDocument.Parse(lines[1]);
        using var thirdDoc = JsonDocument.Parse(lines[2]);

        firstDoc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        secondDoc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        thirdDoc.RootElement.ValueKind.Should().Be(JsonValueKind.Object);

        firstDoc.RootElement.GetProperty("host").GetString().Should().Be("srv1");
        secondDoc.RootElement.GetProperty("host").GetString().Should().Be("srv2");
        thirdDoc.RootElement.GetProperty("host").GetString().Should().Be("srv3");
    }

    [Fact]
    public async Task ExecuteAsync_JsonlAppend_SeparatesLineWhenExistingFileLacksTrailingNewline()
    {
        var filePath = Path.Combine(_testDirectory, "events.jsonl");
        File.WriteAllText(filePath, "{\"host\":\"srv1\"}");

        var context = new ScriptContext();
        context.SetVariable("record2", "{\"host\":\"srv2\"}");
        var appendStep = BuildJsonlStep(filePath, "${record2}", "append");

        var result = await _command.ExecuteAsync(appendStep, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var lines = File.ReadAllLines(filePath);
        lines.Should().HaveCount(2);
        JsonDocument.Parse(lines[0]).RootElement.GetProperty("host").GetString().Should().Be("srv1");
        JsonDocument.Parse(lines[1]).RootElement.GetProperty("host").GetString().Should().Be("srv2");
    }

    [Fact]
    public async Task ExecuteAsync_TextOverwriteThenAppend_SeparatesLinesWhenOverwriteHasNoTrailingNewline()
    {
        var filePath = Path.Combine(_testDirectory, "output.txt");
        var context = new ScriptContext();

        var overwriteStep = BuildTextStep(filePath, "First line", "overwrite");
        var appendStepTwo = BuildTextStep(filePath, "Second line", "append");
        var appendStepThree = BuildTextStep(filePath, "Third line", "append");

        (await _command.ExecuteAsync(overwriteStep, context, CancellationToken.None)).Success.Should().BeTrue();
        (await _command.ExecuteAsync(appendStepTwo, context, CancellationToken.None)).Success.Should().BeTrue();
        (await _command.ExecuteAsync(appendStepThree, context, CancellationToken.None)).Success.Should().BeTrue();

        var lines = File.ReadAllLines(filePath);
        lines.Should().Equal("First line", "Second line", "Third line");
    }

    [Fact]
    public async Task ExecuteAsync_CsvFromJsonObjectRows_WithArrayValue_KeepsArrayInSingleCell()
    {
        var filePath = Path.Combine(_testDirectory, "admins.csv");
        var context = new ScriptContext();
        context.SetVariable("report", "[{\"username\":\"admin\",\"accprofile\":\"super_admin\",\"vdom\":[\"root\",\"vd2\"]}]");

        var step = BuildCsvStep(
            filePath,
            "${report}",
            "overwrite",
            "username",
            "accprofile",
            "vdom");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var lines = File.ReadAllLines(filePath);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("username,accprofile,vdom");
        lines[1].Should().Be("admin,super_admin,\"root, vd2\"");
    }

    [Fact]
    public async Task ExecuteAsync_CsvFromJsonObjectRows_WithStringifiedArrayValue_KeepsValueInSingleCell()
    {
        var filePath = Path.Combine(_testDirectory, "admins_stringified.csv");
        var context = new ScriptContext();
        context.SetVariable("report", "[{\"username\":\"admin\",\"accprofile\":\"super_admin\",\"vdom\":\"[\\\"root\\\",\\\"vd2\\\"]\"}]");

        var step = BuildCsvStep(
            filePath,
            "${report}",
            "overwrite",
            "username",
            "accprofile",
            "vdom");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        var lines = File.ReadAllLines(filePath);
        lines.Should().HaveCount(2);
        lines[0].Should().Be("username,accprofile,vdom");
        lines[1].Should().Be("admin,super_admin,\"root, vd2\"");
    }

    private static ScriptStep BuildJsonlStep(string filePath, string content, string mode)
    {
        return new ScriptStep
        {
            Writefile = new WritefileOptions
            {
                Path = filePath,
                Content = content,
                Format = "jsonl",
                Mode = mode
            }
        };
    }

    private static ScriptStep BuildTextStep(string filePath, string content, string mode)
    {
        return new ScriptStep
        {
            Writefile = new WritefileOptions
            {
                Path = filePath,
                Content = content,
                Format = "text",
                Mode = mode
            }
        };
    }

    private static ScriptStep BuildCsvStep(string filePath, string content, string mode, params string[] headers)
    {
        return new ScriptStep
        {
            Writefile = new WritefileOptions
            {
                Path = filePath,
                Content = content,
                Format = "csv",
                Mode = mode,
                Headers = new List<string>(headers)
            }
        };
    }
}
