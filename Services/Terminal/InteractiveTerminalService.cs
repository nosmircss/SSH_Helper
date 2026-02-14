using System.IO;
using System.Drawing;
using System.Collections.Concurrent;
using Rebex.Net;
using Rebex.TerminalEmulation;
using SSH_Helper.Forms;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;

// Alias to avoid conflict with SSH_Helper.Services.Scripting namespace.
using RebexScripting = Rebex.TerminalEmulation.Scripting;

namespace SSH_Helper.Services.Terminal
{
    public interface IInteractiveTerminalService
    {
        Task<InteractiveTerminalRunResult> RunAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken);
    }

    public sealed class InteractiveTerminalRunResult
    {
        public bool Success { get; private init; }
        public bool SharedUnavailable { get; private init; }
        public string? ErrorMessage { get; private init; }

        public static InteractiveTerminalRunResult Ok() => new() { Success = true };

        public static InteractiveTerminalRunResult Fail(string message) => new()
        {
            Success = false,
            ErrorMessage = message
        };

        public static InteractiveTerminalRunResult SharedUnavailableResult(string message) => new()
        {
            Success = false,
            SharedUnavailable = true,
            ErrorMessage = message
        };
    }

    public sealed class InteractiveTerminalService : IInteractiveTerminalService
    {
        public async Task<InteractiveTerminalRunResult> RunAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(options);
            cancellationToken.ThrowIfCancellationRequested();

            return options.Session == InteractiveSessionMode.Shared
                ? await RunSharedAsync(context, options, cancellationToken)
                : await RunSeparateAsync(context, options, cancellationToken);
        }

        private async Task<InteractiveTerminalRunResult> RunSharedAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            var sharedSession = context.Session;
            if (sharedSession == null || !sharedSession.IsConnected)
            {
                return InteractiveTerminalRunResult.SharedUnavailableResult(
                    "InteractiveSharedUnavailable: no active shared SSH shell session is available for session=shared.");
            }

            try
            {
                sharedSession.FlushBuffer();
                var terminal = sharedSession.SharedTerminal;

                await RunWindowLoopAsync(
                    title: $"{context.CurrentHost?.ToString() ?? "Current Host"} - Interactive ({options.Session.ToString().ToLowerInvariant()}/{options.Emulation.ToString().ToLowerInvariant()})",
                    scripting: sharedSession.SharedScripting,
                    terminal: terminal,
                    cancellationToken: cancellationToken,
                    onCancellation: () =>
                    {
                        // Required behavior: stop/cancel force-closes active shared interactive session.
                        sharedSession.Dispose();
                    });

                sharedSession.SyncAfterInteractive();
                return InteractiveTerminalRunResult.Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return InteractiveTerminalRunResult.Fail($"Interactive terminal failed: {ex.Message}");
            }
        }

        private async Task<InteractiveTerminalRunResult> RunSeparateAsync(
            ScriptContext context,
            InteractiveOptions options,
            CancellationToken cancellationToken)
        {
            var host = context.CurrentHost;
            if (host == null || string.IsNullOrWhiteSpace(host.IpAddress))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires current host connection details.");
            }

            var username = !string.IsNullOrWhiteSpace(context.ResolvedUsername) ? context.ResolvedUsername : host.Username;
            var password = !string.IsNullOrWhiteSpace(context.ResolvedPassword) ? context.ResolvedPassword : host.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires a resolved username.");
            }

            if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(host.IdentityFile))
            {
                return InteractiveTerminalRunResult.Fail("Interactive command requires a resolved password or an identity file.");
            }

            var timeouts = context.Timeouts ?? SshTimeoutOptions.Default;
            Ssh? client = null;
            VirtualTerminal? virtualTerminal = null;
            RebexScripting? scripting = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                client = new Ssh
                {
                    Timeout = (int)timeouts.ConnectionTimeout.TotalMilliseconds
                };

                ApplyAlgorithmSettings(client, host);
                await Task.Run(() => ConnectAndLogin(client, host, username, password ?? string.Empty), cancellationToken);

                var terminalOptions = SshTerminalOptionsFactory.Create();
                (scripting, virtualTerminal) = SshTerminalOptionsFactory.CreateScriptingWithHistory(
                    client,
                    terminalOptions,
                    SshTerminalOptionsFactory.DefaultColumns,
                    SshTerminalOptionsFactory.DefaultRows,
                    SshTerminalOptionsFactory.DefaultHistoryMaxLength);

                await RunWindowLoopAsync(
                    title: $"{host} - Interactive ({options.Session.ToString().ToLowerInvariant()}/{options.Emulation.ToString().ToLowerInvariant()})",
                    scripting: scripting,
                    terminal: virtualTerminal,
                    cancellationToken: cancellationToken,
                    onCancellation: () => CloseSeparateResources(client, virtualTerminal, scripting));

                return InteractiveTerminalRunResult.Ok();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return InteractiveTerminalRunResult.Fail($"Interactive terminal failed: {ex.Message}");
            }
            finally
            {
                CloseSeparateResources(client, virtualTerminal, scripting);
            }
        }

        private async Task RunWindowLoopAsync(
            string title,
            RebexScripting scripting,
            ITerminal? terminal,
            CancellationToken cancellationToken,
            Action? onCancellation)
        {
            var ioLock = new object();
            var pendingInput = new ConcurrentQueue<Action<RebexScripting>>();
            var formClosedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var uiLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var cancelledByToken = false;
            InteractiveTerminalForm? form = null;

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                cancelledByToken = true;
                onCancellation?.Invoke();

                if (form == null || form.IsDisposed)
                    return;

                try
                {
                    if (form.InvokeRequired)
                    {
                        form.BeginInvoke(new Action(form.Close));
                    }
                    else
                    {
                        form.Close();
                    }
                }
                catch
                {
                    // Form might already be disposing.
                }
            });

            await InvokeOnUiThreadAsync(() =>
            {
                form = new InteractiveTerminalForm(title);

                form.FormClosed += (_, _) =>
                {
                    formClosedTcs.TrySetResult(true);
                    uiLoopCancellation.Cancel();
                };

                form.TextInput += (_, text) =>
                {
                    pendingInput.Enqueue(stream => stream.Send(text));
                };

                form.KeyInput += (_, keyArgs) =>
                {
                    pendingInput.Enqueue(stream =>
                    {
                        if (keyArgs.FunctionKey.HasValue)
                        {
                            stream.Send(keyArgs.FunctionKey.Value, keyArgs.Modifiers);
                        }
                        else if (keyArgs.ConsoleKey.HasValue)
                        {
                            stream.Send(keyArgs.ConsoleKey.Value, keyArgs.Modifiers);
                        }
                    });
                };

                form.TerminalSizeChanged += (_, sizeArgs) =>
                {
                    if (terminal == null)
                        return;

                    try
                    {
                        lock (ioLock)
                        {
                            terminal.SetScreenSize(sizeArgs.Columns, sizeArgs.Rows);
                        }
                    }
                    catch
                    {
                        // Ignore resize failures while terminal is closing.
                    }
                };

                form.Show(Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
                form.FocusTerminal();
            });

            var pumpTask = Task.Run(async () =>
            {
                try
                {
                    await PumpFullAsync(form!, scripting, terminal, ioLock, pendingInput, uiLoopCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is expected.
                }
            }, CancellationToken.None);

            await formClosedTcs.Task;
            uiLoopCancellation.Cancel();
            await pumpTask;

            if (cancelledByToken || cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }

        private static async Task PumpFullAsync(
            InteractiveTerminalForm form,
            RebexScripting scripting,
            ITerminal? terminal,
            object ioLock,
            ConcurrentQueue<Action<RebexScripting>> pendingInput,
            CancellationToken cancellationToken)
        {
            if (terminal == null)
            {
                AppendOutputSafe(form, "\r\n[interactive-error] Full terminal emulation is unavailable.\r\n");
                return;
            }

            const int processTimeoutMs = 2;
            const int activeDelayMs = 1;
            const int idleDelayMs = 4;
            var lastScreenHash = int.MinValue;
            var lastRenderedHistoryLength = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                TerminalScreenSnapshot? snapshot = null;
                var hadPendingInput = !pendingInput.IsEmpty;
                try
                {
                    lock (ioLock)
                    {
                        FlushPendingInput(scripting, pendingInput);
                        scripting.Process(processTimeoutMs);
                        var totalHistoryLength = Math.Max(0, terminal.HistoryLength);
                        var requestedOffset = form.ScrollbackOffset;
                        if (requestedOffset > 0 && totalHistoryLength > lastRenderedHistoryLength)
                        {
                            // Match PuTTY-like behavior: keep a stable scrollback anchor while new data arrives.
                            requestedOffset = Math.Min(
                                totalHistoryLength,
                                requestedOffset + (totalHistoryLength - lastRenderedHistoryLength));
                        }

                        snapshot = BuildScreenSnapshot(terminal, requestedOffset);
                        lastRenderedHistoryLength = snapshot.HistoryLength;
                    }
                }
                catch (Exception ex) when (IsTimeoutException(ex))
                {
                    await Task.Delay(25, cancellationToken);
                    continue;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    AppendOutputSafe(form, $"\r\n[interactive-error] {ex.Message}\r\n");
                    break;
                }

                if (snapshot != null && snapshot.Hash != lastScreenHash)
                {
                    lastScreenHash = snapshot.Hash;
                    SetScreenSafe(form, snapshot);
                    if (!hadPendingInput)
                    {
                        await Task.Delay(activeDelayMs, cancellationToken);
                    }
                    continue;
                }

                if (hadPendingInput)
                    continue;

                await Task.Delay(idleDelayMs, cancellationToken);
            }
        }

        private static void FlushPendingInput(
            RebexScripting scripting,
            ConcurrentQueue<Action<RebexScripting>> pendingInput)
        {
            while (pendingInput.TryDequeue(out var sendAction))
            {
                try
                {
                    sendAction(scripting);
                }
                catch
                {
                    // Ignore send failures while terminal is closing.
                }
            }
        }

        private static void AppendOutputSafe(InteractiveTerminalForm form, string text)
        {
            if (form.IsDisposed || string.IsNullOrEmpty(text))
                return;

            try
            {
                if (form.InvokeRequired)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        if (!form.IsDisposed)
                            form.AppendOutput(text);
                    }));
                }
                else
                {
                    form.AppendOutput(text);
                }
            }
            catch
            {
                // UI closed while appending.
            }
        }

        private static void SetScreenSafe(InteractiveTerminalForm form, TerminalScreenSnapshot snapshot)
        {
            if (form.IsDisposed)
                return;

            try
            {
                if (form.InvokeRequired)
                {
                    form.BeginInvoke(new Action(() =>
                    {
                        if (!form.IsDisposed)
                            form.SetScreen(snapshot);
                    }));
                }
                else
                {
                    form.SetScreen(snapshot);
                }
            }
            catch
            {
                // UI closed while rendering.
            }
        }

        private static TerminalScreenSnapshot BuildScreenSnapshot(
            ITerminal terminal,
            int scrollbackOffset)
        {
            var screen = terminal.Screen;
            var columns = Math.Max(1, screen.Columns);
            var rows = Math.Max(1, screen.Rows);
            var historyLength = Math.Max(0, terminal.HistoryLength);
            var appliedOffset = Math.Clamp(scrollbackOffset, 0, historyLength);
            var palette = terminal.Palette;
            var count = columns * rows;
            var characters = new char[count];
            var foreColors = new int[count];
            var backColors = new int[count];
            var hash = new HashCode();
            var paletteCache = new Dictionary<int, int>();
            var defaultForeColor = ResolvePaletteColorArgb(
                palette,
                TerminalColor.LightGray,
                Color.FromArgb(187, 187, 187).ToArgb(),
                paletteCache);
            var defaultBackColor = ResolvePaletteColorArgb(
                palette,
                TerminalColor.Black,
                Color.Black.ToArgb(),
                paletteCache);

            hash.Add(columns);
            hash.Add(rows);
            hash.Add(historyLength);
            hash.Add(appliedOffset);

            var absoluteIndex = 0;
            for (var row = 0; row < rows; row++)
            {
                var sourceRow = row - appliedOffset;
                for (var column = 0; column < columns; column++)
                {
                    var cell = screen.GetCell(column, sourceRow);
                    var character = cell.Character == '\0' ? ' ' : cell.Character;
                    var foreColorIndex = cell.Bold && cell.ForeColor >= TerminalColor.Black && cell.ForeColor <= TerminalColor.LightGray
                        ? cell.ForeColor + 8
                        : cell.ForeColor;
                    var foreColor = ResolvePaletteColorArgb(palette, foreColorIndex, defaultForeColor, paletteCache);
                    var backColor = ResolvePaletteColorArgb(palette, cell.BackColor, defaultBackColor, paletteCache);

                    // Some streams resolve "default foreground" to palette black (0) on black background,
                    // which makes plain text invisible. Normalize that case back to default foreground.
                    if (backColor == defaultBackColor &&
                        foreColor == defaultBackColor)
                    {
                        foreColor = defaultForeColor;
                    }

                    hash.Add(character);
                    hash.Add(foreColor);
                    hash.Add(backColor);
                    characters[absoluteIndex] = character;
                    foreColors[absoluteIndex] = foreColor;
                    backColors[absoluteIndex] = backColor;

                    absoluteIndex++;
                }
            }

            var cursorColumn = -1;
            var cursorRow = -1;
            var cursorForeColor = defaultBackColor;
            var cursorBackColor = defaultForeColor;

            var viewportCursorRow = screen.CursorTop - appliedOffset;
            if (screen.CursorLeft >= 0 &&
                screen.CursorLeft < columns &&
                viewportCursorRow >= 0 &&
                viewportCursorRow < rows)
            {
                cursorColumn = screen.CursorLeft;
                cursorRow = viewportCursorRow;
                var cursorIndex = cursorRow * columns + cursorColumn;
                cursorForeColor = backColors[cursorIndex];
                cursorBackColor = foreColors[cursorIndex];

                if (cursorForeColor == cursorBackColor)
                {
                    cursorForeColor = defaultBackColor;
                    cursorBackColor = defaultForeColor;
                }
            }

            hash.Add(cursorColumn);
            hash.Add(cursorRow);
            hash.Add(cursorForeColor);
            hash.Add(cursorBackColor);

            return new TerminalScreenSnapshot
            {
                Columns = columns,
                Rows = rows,
                HistoryLength = historyLength,
                ScrollbackOffset = appliedOffset,
                Characters = characters,
                ForeColors = foreColors,
                BackColors = backColors,
                CursorColumn = cursorColumn,
                CursorRow = cursorRow,
                CursorForeColor = cursorForeColor,
                CursorBackColor = cursorBackColor,
                Hash = hash.ToHashCode()
            };
        }

        private static int ResolvePaletteColorArgb(
            TerminalPalette? palette,
            int paletteIndex,
            int fallback,
            Dictionary<int, int> paletteCache)
        {
            if (palette == null || paletteIndex < 0)
                return fallback;

            if (paletteCache.TryGetValue(paletteIndex, out var cachedArgb))
                return cachedArgb;

            try
            {
                var colorArgb = palette.GetColor(paletteIndex).ToArgb();
                paletteCache[paletteIndex] = colorArgb;
                return colorArgb;
            }
            catch
            {
                return fallback;
            }
        }

        private static Task InvokeOnUiThreadAsync(Action action)
        {
            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (mainForm == null || !mainForm.InvokeRequired)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            mainForm.BeginInvoke(new Action(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));
            return tcs.Task;
        }

        private static bool IsTimeoutException(Exception ex)
        {
            return ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("time limit", StringComparison.OrdinalIgnoreCase);
        }

        private static void CloseSeparateResources(Ssh? client, VirtualTerminal? virtualTerminal, RebexScripting? scripting)
        {
            try
            {
                virtualTerminal?.Dispose();
            }
            catch
            {
                // Ignore cleanup exceptions.
            }

            if (virtualTerminal == null)
            {
                try
                {
                    if (scripting is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch
                {
                    // Ignore cleanup exceptions.
                }
            }

            try
            {
                if (client != null && client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch
            {
                // Ignore cleanup exceptions.
            }

            try
            {
                client?.Dispose();
            }
            catch
            {
                // Ignore cleanup exceptions.
            }
        }

        private static void ConnectAndLogin(Ssh client, HostConnection host, string username, string password)
        {
            client.Connect(host.IpAddress, host.Port);

            if (!string.IsNullOrEmpty(host.IdentityFile) && File.Exists(host.IdentityFile))
            {
                var passphrase = host.IdentityFilePassphrase ?? string.Empty;
                client.Login(username, new SshPrivateKey(host.IdentityFile, passphrase));
                return;
            }

            client.Login(username, password);
        }

        private static void ApplyAlgorithmSettings(Ssh client, HostConnection host)
        {
            if (host.HostKeyAlgorithms?.Length > 0)
            {
                client.Settings.SshParameters.SetHostKeyAlgorithms(host.HostKeyAlgorithms);
            }

            if (host.Ciphers?.Length > 0)
            {
                client.Settings.SshParameters.SetEncryptionAlgorithms(host.Ciphers);
            }
        }
    }
}
