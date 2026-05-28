using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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

    [Fact]
    public async Task ExecuteAsync_SelectFileTrue_PromptsAndReadsSelectedFile()
    {
        var allowedFilePath = Path.Combine(_testDirectory, "audit.txt");
        File.WriteAllLines(allowedFilePath, new[] { "{\"ok\":true}" });
        var promptCallCount = 0;
        var command = new ReadFileCommand((request, _) =>
        {
            promptCallCount++;
            request.SuggestedPath.Should().Be("seed.txt");
            request.PromptMessage.Should().Be("Pick the audit file to import.");
            request.AllowedExtensions.Should().BeEquivalentTo([".txt", ".json"]);
            return Task.FromResult<string?>(allowedFilePath);
        });

        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                Path = "seed.txt",
                SelectFile = true,
                Message = "Pick the audit file to import.",
                FileExt = "txt,json",
                Into = "selected_entries"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        promptCallCount.Should().Be(1);
        context.GetVariable("selected_entries").Should().BeEquivalentTo(new List<string> { "{\"ok\":true}" });
        context.GetVariable("selected_entries_path").Should().Be(allowedFilePath);
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileTrue_PathOnly_CapturesResolvedPathWithoutReadingContents()
    {
        var allowedFilePath = Path.Combine(_testDirectory, "picked.txt");
        File.WriteAllLines(allowedFilePath, new[] { "line one", "line two" });
        var promptCallCount = 0;
        var command = new ReadFileCommand((request, _) =>
        {
            promptCallCount++;
            request.AllowedExtensions.Should().BeEquivalentTo([".txt"]);
            return Task.FromResult<string?>(allowedFilePath);
        });

        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                FileExt = "txt",
                PathOnly = true,
                PathInto = "selected_path"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        promptCallCount.Should().Be(1);
        context.GetVariable("selected_path").Should().Be(allowedFilePath);
        context.HasVariable("selected_entries").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileTrue_AutoBrowse_PassesFlagToPrompt()
    {
        var allowedFilePath = Path.Combine(_testDirectory, "picked.txt");
        File.WriteAllLines(allowedFilePath, new[] { "line one" });
        var command = new ReadFileCommand((request, _) =>
        {
            request.AutoBrowse.Should().BeTrue();
            return Task.FromResult<string?>(allowedFilePath);
        });

        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                AutoBrowse = true,
                PathOnly = true,
                PathInto = "selected_path"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("selected_path").Should().Be(allowedFilePath);
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileTrue_PathOnly_ImpliedAutoBrowse_PassesFlagToPrompt()
    {
        var allowedFilePath = Path.Combine(_testDirectory, "picked.txt");
        File.WriteAllLines(allowedFilePath, new[] { "line one" });
        var command = new ReadFileCommand((request, _) =>
        {
            request.AutoBrowse.Should().BeTrue();
            return Task.FromResult<string?>(allowedFilePath);
        });

        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                PathOnly = true,
                PathInto = "selected_path"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("selected_path").Should().Be(allowedFilePath);
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileTrue_PathOnly_AutoBrowseFalse_PassesFalseFlagToPrompt()
    {
        var allowedFilePath = Path.Combine(_testDirectory, "picked.txt");
        File.WriteAllLines(allowedFilePath, new[] { "line one" });
        var command = new ReadFileCommand((request, _) =>
        {
            request.AutoBrowse.Should().BeFalse();
            return Task.FromResult<string?>(allowedFilePath);
        });

        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                AutoBrowse = false,
                PathOnly = true,
                PathInto = "selected_path"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        context.GetVariable("selected_path").Should().Be(allowedFilePath);
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileTrue_PathOnly_UsesNativeFileDialogByDefault()
    {
        var allowedFilePath = Path.Combine(_testDirectory, "picked.txt");
        File.WriteAllLines(allowedFilePath, new[] { "line one" });
        var priorOverride = ReadFileCommand.OpenFileDialogOverrideForTests;
        var openDialogCalls = 0;

        try
        {
            ReadFileCommand.OpenFileDialogOverrideForTests = (request, owner) =>
            {
                openDialogCalls++;
                request.AutoBrowse.Should().BeTrue();
                request.PromptMessage.Should().Be("Choose the file now.");
                request.AllowedExtensions.Should().BeEquivalentTo([".txt"]);
                owner.Should().BeNull();
                return (DialogResult.OK, allowedFilePath);
            };

            var step = new ScriptStep
            {
                Readfile = new ReadfileOptions
                {
                    SelectFile = true,
                    Message = "Choose the file now.",
                    FileExt = "txt",
                    PathOnly = true,
                    PathInto = "selected_path"
                }
            };

            var context = new ScriptContext();

            var result = await new ReadFileCommand().ExecuteAsync(step, context, CancellationToken.None);

            result.Success.Should().BeTrue();
            openDialogCalls.Should().Be(1);
            context.GetVariable("selected_path").Should().Be(allowedFilePath);
        }
        finally
        {
            ReadFileCommand.OpenFileDialogOverrideForTests = priorOverride;
        }
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileWithRestrictedExtensions_RejectsDisallowedSelection()
    {
        var command = new ReadFileCommand((_, _) => Task.FromResult<string?>(_testFilePath));
        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                FileExt = "txt,json",
                Into = "selected_entries"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain(".txt");
        result.Message.Should().Contain(".json");
        context.HasVariable("selected_entries").Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileFalse_DoesNotPrompt()
    {
        var promptCalled = false;
        var command = new ReadFileCommand((_, _) =>
        {
            promptCalled = true;
            return Task.FromResult<string?>(null);
        });

        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                Path = _testFilePath,
                Into = "entries"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        promptCalled.Should().BeFalse();
        context.GetVariable("entries").Should().BeEquivalentTo(new List<string> { "{\"ok\":true}" });
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileCancelled_ReturnsCancelledExitAndSetsEmptyList()
    {
        var command = new ReadFileCommand((_, _) => Task.FromResult<string?>(null));
        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                Into = "entries"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ShouldExit.Should().BeTrue();
        result.ExitStatus.Should().Be(ScriptExitStatus.Cancelled);
        result.Message.Should().Contain("cancelled");
        context.GetVariable("entries").Should().BeEquivalentTo(new List<string>());
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileCancelled_PathOnly_ClearsPathVariableAndReturnsCancelledExit()
    {
        var command = new ReadFileCommand((_, _) => Task.FromResult<string?>(null));
        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                PathOnly = true,
                PathInto = "selected_path"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ShouldExit.Should().BeTrue();
        result.ExitStatus.Should().Be(ScriptExitStatus.Cancelled);
        context.GetVariable("selected_path").Should().Be(string.Empty);
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileCancelled_WithOnErrorContinue_StillReturnsCancelledExit()
    {
        var command = new ReadFileCommand((_, _) => Task.FromResult<string?>(null));
        var step = new ScriptStep
        {
            OnError = "continue",
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                Into = "entries"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ShouldExit.Should().BeTrue();
        result.ExitStatus.Should().Be(ScriptExitStatus.Cancelled);
        result.SuppressedError.Should().BeFalse();
        result.Message.Should().Contain("cancelled");
        context.GetVariable("entries").Should().BeEquivalentTo(new List<string>());
    }

    [Fact]
    public async Task ExecuteAsync_SelectFileWhenDialogsBlocked_ReturnsManualOnlyFailureAndSkipsPrompt()
    {
        var promptCalled = false;
        var command = new ReadFileCommand((_, _) =>
        {
            promptCalled = true;
            return Task.FromResult<string?>(_testFilePath);
        });

        var step = new ScriptStep
        {
            Readfile = new ReadfileOptions
            {
                SelectFile = true,
                Into = "entries"
            }
        };

        var context = new ScriptContext
        {
            AllowFileSelectionDialogs = false
        };

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("manual");
        promptCalled.Should().BeFalse();
        context.GetVariable("entries").Should().BeEquivalentTo(new List<string>());
    }
}
