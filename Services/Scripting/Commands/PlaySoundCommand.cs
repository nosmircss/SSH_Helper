using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Plays a local audio file (WAV/MP3) with optional wait mode.
    /// </summary>
    public class PlaySoundCommand : IScriptCommand
    {
        private const string BackendName = "naudio";
        private readonly Func<PlaybackRequest, CancellationToken, Task<PlaybackResult>> _playAsync;

        public record PlaybackRequest(string Path, bool Wait, int Volume, double? MaxSeconds);
        public record PlaybackResult(bool Started, bool Completed, long? DurationMs);

        public PlaySoundCommand(Func<PlaybackRequest, CancellationToken, Task<PlaybackResult>>? playAsync = null)
        {
            _playAsync = playAsync ?? PlayWithNAudioAsync;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.PlaySound == null)
                return CommandResult.Fail("Playsound command has no options");

            cancellationToken.ThrowIfCancellationRequested();

            var options = step.PlaySound;
            var resolvedPath = Environment.ExpandEnvironmentVariables(context.SubstituteVariables(options.Path ?? string.Empty)).Trim();
            var wait = options.Wait;
            var volume = Math.Clamp(options.Volume, 0, 100);
            var maxSeconds = options.MaxSeconds;
            var into = options.Into?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(resolvedPath))
                return FailWithCapture(step, context, into, resolvedPath, wait, volume, "playsound requires 'path'");

            if (!File.Exists(resolvedPath))
                return FailWithCapture(step, context, into, resolvedPath, wait, volume, $"Audio file not found: {resolvedPath}");

            var extension = Path.GetExtension(resolvedPath);
            if (!string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
            {
                return FailWithCapture(step, context, into, resolvedPath, wait, volume, "playsound supports only .wav and .mp3 files");
            }

            if (maxSeconds.HasValue && maxSeconds.Value <= 0)
                return FailWithCapture(step, context, into, resolvedPath, wait, volume, "playsound.max_seconds must be greater than 0");

            try
            {
                var result = await _playAsync(new PlaybackRequest(resolvedPath, wait, volume, maxSeconds), cancellationToken).ConfigureAwait(false);
                Capture(into, context, resolvedPath, wait, volume, null, result.DurationMs);
                context.EmitOutput(
                    $"Playsound: {(result.Started ? "started" : "not started")} '{resolvedPath}' (wait={wait}, volume={volume})",
                    ScriptOutputType.Debug);

                return CommandResult.Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException ex)
            {
                return FailWithCapture(step, context, into, resolvedPath, wait, volume, ex.Message);
            }
            catch (Exception ex)
            {
                return FailWithCapture(step, context, into, resolvedPath, wait, volume, $"Playsound error: {ex.Message}");
            }
        }

        private static void Capture(
            string into,
            ScriptContext context,
            string path,
            bool wait,
            int volume,
            string? error,
            long? durationMs)
        {
            if (string.IsNullOrWhiteSpace(into))
                return;

            context.SetVariable(into, string.IsNullOrWhiteSpace(error));

            var meta = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["path"] = path,
                ["wait"] = wait,
                ["volume"] = volume,
                ["backend"] = BackendName,
            };

            if (durationMs.HasValue)
                meta["duration_ms"] = durationMs.Value;

            if (!string.IsNullOrWhiteSpace(error))
                meta["error"] = error;

            context.SetVariable(into + "_meta", meta);
        }

        private static CommandResult FailWithCapture(
            ScriptStep step,
            ScriptContext context,
            string into,
            string path,
            bool wait,
            int volume,
            string message)
        {
            Capture(into, context, path, wait, volume, message, null);
            return ApplyPlaySoundOnError(step, message);
        }

        private static CommandResult ApplyPlaySoundOnError(ScriptStep step, string message)
        {
            // playsound defaults to continue when on_error is omitted.
            if (string.IsNullOrWhiteSpace(step.OnError) || step.IsOnErrorContinue)
                return CommandResult.Suppressed(message);

            return CommandResult.Fail(message);
        }

        private static async Task<PlaybackResult> PlayWithNAudioAsync(PlaybackRequest request, CancellationToken cancellationToken)
        {
            if (!request.Wait)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await PlayBlockingInternalAsync(request.Path, request.Volume, request.MaxSeconds, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Fire-and-forget mode intentionally suppresses background playback exceptions.
                    }
                });

                return new PlaybackResult(true, false, null);
            }

            var elapsed = await PlayBlockingInternalAsync(request.Path, request.Volume, request.MaxSeconds, cancellationToken).ConfigureAwait(false);
            return new PlaybackResult(true, true, elapsed);
        }

        private static Task<long> PlayBlockingInternalAsync(string path, int volume, double? maxSeconds, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                using var playbackStopped = new ManualResetEventSlim(false);
                using var reader = new AudioFileReader(path) { Volume = volume / 100f };
                using var output = new WaveOutEvent();

                Exception? playbackException = null;
                output.PlaybackStopped += (_, args) =>
                {
                    playbackException = args.Exception;
                    playbackStopped.Set();
                };

                output.Init(reader);
                var stopwatch = Stopwatch.StartNew();
                output.Play();

                try
                {
                    if (maxSeconds.HasValue)
                    {
                        if (!playbackStopped.Wait(TimeSpan.FromSeconds(maxSeconds.Value), cancellationToken))
                        {
                            output.Stop();
                            var timeoutSeconds = maxSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture);
                            throw new TimeoutException($"playsound timed out after {timeoutSeconds} second(s)");
                        }
                    }
                    else
                    {
                        playbackStopped.Wait(cancellationToken);
                    }
                }
                finally
                {
                    if (output.PlaybackState == PlaybackState.Playing)
                    {
                        output.Stop();
                    }
                }

                if (playbackException != null)
                {
                    throw playbackException;
                }

                stopwatch.Stop();
                return stopwatch.ElapsedMilliseconds;
            }, cancellationToken);
        }
    }
}
