using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ExistsCommandTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _testFilePath;
    private readonly ExistsCommand _command = new();

    public ExistsCommandTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ExistsCommandTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        _testFilePath = Path.Combine(_testDirectory, "hosts.txt");
        File.WriteAllText(_testFilePath, "127.0.0.1");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingFile_SetsBooleanAndMetadata()
    {
        var step = new ScriptStep
        {
            Exists = new ExistsOptions
            {
                Path = _testFilePath,
                Into = "file_exists",
                Type = "file"
            }
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("file_exists").Should().Be(true);

        var meta = context.GetVariable("file_exists_meta").Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        meta["exists"].Should().Be(true);
        meta["is_file"].Should().Be(true);
        meta["is_directory"].Should().Be(false);
        meta["type"].Should().Be("file");
    }

    [Fact]
    public async Task ExecuteAsync_ExistingDirectoryWithFileType_ReturnsFalse()
    {
        var step = new ScriptStep
        {
            Exists = new ExistsOptions
            {
                Path = _testDirectory,
                Into = "matches_type",
                Type = "file"
            }
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("matches_type").Should().Be(false);

        var meta = context.GetVariable("matches_type_meta").Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        meta["exists"].Should().Be(false);
        meta["is_directory"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPath_ReturnsFalseWithoutFailure()
    {
        var step = new ScriptStep
        {
            Exists = new ExistsOptions
            {
                Path = Path.Combine(_testDirectory, "missing.txt"),
                Into = "missing_exists"
            }
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("missing_exists").Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_PathFromVariableAndEnvironment_ExpandsAndChecks()
    {
        var step = new ScriptStep
        {
            Exists = new ExistsOptions
            {
                Path = @"${qa_dir}\hosts.txt",
                Into = "expanded_exists"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("qa_dir", $@"%TEMP%\{Path.GetFileName(_testDirectory)}");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("expanded_exists").Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_TypeFromVariable_HonorsResolvedType()
    {
        var step = new ScriptStep
        {
            Exists = new ExistsOptions
            {
                Path = _testDirectory,
                Into = "type_from_var_exists",
                Type = "${expected_type}"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("expected_type", "file");

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("type_from_var_exists").Should().Be(false);

        var meta = context.GetVariable("type_from_var_exists_meta").Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        meta["type"].Should().Be("file");
        meta["is_directory"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPathWithOnErrorContinue_SuppressesFailureAndCapturesError()
    {
        var step = new ScriptStep
        {
            OnError = "continue",
            Exists = new ExistsOptions
            {
                Path = "   ",
                Into = "invalid_exists"
            }
        };

        var context = new ScriptContext();

        var result = await _command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariable("invalid_exists").Should().Be(false);

        var meta = context.GetVariable("invalid_exists_meta").Should().BeAssignableTo<Dictionary<string, object?>>().Subject;
        meta.Should().ContainKey("error");
        meta["error"]!.ToString().Should().Contain("requires 'path'");
    }
}
