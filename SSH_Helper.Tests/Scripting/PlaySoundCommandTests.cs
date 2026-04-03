using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class PlaySoundCommandTests : System.IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _mp3Path;
    private readonly string _wavPath;

    public PlaySoundCommandTests()
    {
        _tempDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"PlaySoundCommandTests_{System.Guid.NewGuid()}");
        System.IO.Directory.CreateDirectory(_tempDirectory);
        _mp3Path = System.IO.Path.Combine(_tempDirectory, "alert.mp3");
        _wavPath = System.IO.Path.Combine(_tempDirectory, "alert.wav");
        System.IO.File.WriteAllBytes(_mp3Path, [0x00]);
        System.IO.File.WriteAllBytes(_wavPath, [0x00]);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_tempDirectory))
            System.IO.Directory.Delete(_tempDirectory, true);
    }

    [Fact]
    public async Task ExecuteAsync_ValidStep_ResolvesPathAndCapturesMetadata()
    {
        PlaySoundCommand.PlaybackRequest? observedRequest = null;
        var command = new PlaySoundCommand((request, _) =>
        {
            observedRequest = request;
            return Task.FromResult(new PlaySoundCommand.PlaybackResult(true, true, 42));
        });

        var step = new ScriptStep
        {
            PlaySound = new PlaySoundOptions
            {
                Path = "${audio_dir}\\alert.mp3",
                Wait = false,
                Volume = 75,
                Into = "play_result"
            }
        };

        var context = new ScriptContext();
        context.SetVariable("audio_dir", _tempDirectory);

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        observedRequest.Should().NotBeNull();
        observedRequest!.Path.Should().Be(_mp3Path);
        observedRequest.Wait.Should().BeFalse();
        observedRequest.Volume.Should().Be(75);
        context.GetVariable("play_result").Should().Be(true);

        var meta = context.GetVariable("play_result_meta").Should().BeAssignableTo<System.Collections.Generic.Dictionary<string, object?>>().Subject;
        meta["path"].Should().Be(_mp3Path);
        meta["wait"].Should().Be(false);
        meta["volume"].Should().Be(75);
        meta["backend"].Should().Be("naudio");
        meta["duration_ms"].Should().Be(42L);
    }

    [Fact]
    public async Task ExecuteAsync_MissingPath_WithOnErrorStop_FailsValidation()
    {
        var command = new PlaySoundCommand((request, _) =>
            Task.FromResult(new PlaySoundCommand.PlaybackResult(true, true, 1)));

        var step = new ScriptStep
        {
            OnError = "stop",
            PlaySound = new PlaySoundOptions
            {
                Path = " ",
                Into = "result"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("requires 'path'");
        context.GetVariable("result").Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_FailureWithOnErrorContinue_SuppressesErrorAndCapturesMeta()
    {
        var command = new PlaySoundCommand((request, _) => throw new System.InvalidOperationException("device unavailable"));
        var step = new ScriptStep
        {
            OnError = "continue",
            PlaySound = new PlaySoundOptions
            {
                Path = _mp3Path,
                Into = "result"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariable("result").Should().Be(false);
        var meta = context.GetVariable("result_meta").Should().BeAssignableTo<System.Collections.Generic.Dictionary<string, object?>>().Subject;
        meta.Should().ContainKey("error");
    }

    [Fact]
    public async Task ExecuteAsync_FailureWithoutOnError_DefaultsToContinueAndCapturesMeta()
    {
        var command = new PlaySoundCommand((request, _) => throw new System.InvalidOperationException("device unavailable"));
        var step = new ScriptStep
        {
            PlaySound = new PlaySoundOptions
            {
                Path = _mp3Path,
                Into = "result"
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.SuppressedError.Should().BeTrue();
        context.GetVariable("result").Should().Be(false);
        var meta = context.GetVariable("result_meta").Should().BeAssignableTo<System.Collections.Generic.Dictionary<string, object?>>().Subject;
        meta.Should().ContainKey("error");
    }

    [Fact]
    public async Task ExecuteAsync_ValidStep_AllowsSubSecondMaxSeconds()
    {
        PlaySoundCommand.PlaybackRequest? observedRequest = null;
        var command = new PlaySoundCommand((request, _) =>
        {
            observedRequest = request;
            return Task.FromResult(new PlaySoundCommand.PlaybackResult(true, true, 1));
        });

        var step = new ScriptStep
        {
            PlaySound = new PlaySoundOptions
            {
                Path = _wavPath,
                Wait = true,
                MaxSeconds = 0.25
            }
        };

        var context = new ScriptContext();
        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeTrue();
        observedRequest.Should().NotBeNull();
        observedRequest!.MaxSeconds.Should().Be(0.25);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedExtension_WithOnErrorStop_ReturnsFailure()
    {
        var unsupportedPath = System.IO.Path.Combine(_tempDirectory, "tone.ogg");
        System.IO.File.WriteAllBytes(unsupportedPath, [0x00]);
        var command = new PlaySoundCommand((request, _) => Task.FromResult(new PlaySoundCommand.PlaybackResult(true, true, null)));

        var step = new ScriptStep
        {
            OnError = "stop",
            PlaySound = new PlaySoundOptions
            {
                Path = unsupportedPath
            }
        };

        var context = new ScriptContext();

        var result = await command.ExecuteAsync(step, context, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("supports only .wav and .mp3");
    }
}
