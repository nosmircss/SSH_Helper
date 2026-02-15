using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Shows script prompt dialogs on the UI thread without making the main form modal-blocked.
    /// </summary>
    internal static class ScriptPromptDialogRunner
    {
        public static Task<TResult> ShowAsync<TDialog, TResult>(
            Func<TDialog> dialogFactory,
            Func<TDialog, TResult> resultSelector,
            CancellationToken cancellationToken)
            where TDialog : Form
        {
            var mainForm = GetMainForm();
            if (mainForm == null || mainForm.IsDisposed)
            {
                using var dialog = dialogFactory();
                using var registration = RegisterCancellation(dialog, cancellationToken);
                dialog.ShowDialog();
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(resultSelector(dialog));
            }

            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            void ShowDialogModeless()
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(cancellationToken);
                    return;
                }

                TDialog? dialog = null;
                CancellationTokenRegistration registration = default;
                MainFormPromptLock? promptLock = null;

                try
                {
                    dialog = dialogFactory();
                    promptLock = MainFormPromptLock.TryAcquire(mainForm);
                    registration = RegisterCancellation(dialog, cancellationToken);
                    PositionDialogCenteredOnMainForm(dialog, mainForm);

                    dialog.FormClosed += (_, _) =>
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                tcs.TrySetCanceled(cancellationToken);
                            }
                            else
                            {
                                tcs.TrySetResult(resultSelector(dialog));
                            }
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                        finally
                        {
                            registration.Dispose();
                            promptLock?.Dispose();
                            dialog.Dispose();
                        }
                    };

                    dialog.Show(mainForm);
                    dialog.BringToFront();
                    dialog.Activate();
                }
                catch (Exception ex)
                {
                    registration.Dispose();
                    promptLock?.Dispose();
                    dialog?.Dispose();
                    tcs.TrySetException(ex);
                }
            }

            try
            {
                if (mainForm.InvokeRequired)
                {
                    mainForm.BeginInvoke((Action)ShowDialogModeless);
                }
                else
                {
                    ShowDialogModeless();
                }
            }
            catch (ObjectDisposedException)
            {
                tcs.TrySetCanceled(cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }

        private static Form? GetMainForm()
        {
            if (Application.OpenForms.Count <= 0)
                return null;

            return Application.OpenForms[0];
        }

        private static CancellationTokenRegistration RegisterCancellation(Form dialog, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
                return default;

            return cancellationToken.Register(() =>
            {
                try
                {
                    if (dialog.IsDisposed)
                        return;

                    void CloseDialog()
                    {
                        if (dialog.IsDisposed)
                            return;

                        dialog.DialogResult = DialogResult.Cancel;
                        dialog.Close();
                    }

                    if (dialog.InvokeRequired)
                    {
                        dialog.BeginInvoke((Action)CloseDialog);
                    }
                    else
                    {
                        CloseDialog();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            });
        }

        private static void PositionDialogCenteredOnMainForm(Form dialog, Form mainForm)
        {
            if (dialog.IsDisposed || mainForm.IsDisposed)
                return;

            var anchorBounds = mainForm.WindowState == FormWindowState.Minimized
                ? Screen.FromControl(mainForm).WorkingArea
                : mainForm.Bounds;

            var workingArea = Screen.FromRectangle(anchorBounds).WorkingArea;

            var x = anchorBounds.Left + ((anchorBounds.Width - dialog.Width) / 2);
            var y = anchorBounds.Top + ((anchorBounds.Height - dialog.Height) / 2);

            x = Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - dialog.Width));
            y = Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - dialog.Height));

            dialog.StartPosition = FormStartPosition.Manual;
            dialog.Location = new Point(x, y);
        }

        private sealed class MainFormPromptLock : IDisposable
        {
            private readonly List<Control> _disabledControls = new();
            private bool _disposed;

            private MainFormPromptLock()
            {
            }

            public static MainFormPromptLock? TryAcquire(Form mainForm)
            {
                if (mainForm.IsDisposed)
                    return null;

                var stopButton = FindControlByName(mainForm, "btnStopAll");
                if (stopButton == null || stopButton.IsDisposed)
                    return null;

                var keepEnabled = new HashSet<Control>();
                for (Control? current = stopButton; current != null; current = current.Parent)
                {
                    keepEnabled.Add(current);
                }

                var lockState = new MainFormPromptLock();
                foreach (Control control in mainForm.Controls)
                {
                    lockState.DisableControlTree(control, keepEnabled);
                }

                return lockState;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;

                for (int i = _disabledControls.Count - 1; i >= 0; i--)
                {
                    var control = _disabledControls[i];
                    if (control.IsDisposed)
                        continue;

                    // If another path already re-enabled this control, keep that state.
                    if (!control.Enabled)
                    {
                        control.Enabled = true;
                    }
                }

                _disabledControls.Clear();
            }

            private void DisableControlTree(Control control, HashSet<Control> keepEnabled)
            {
                if (control.IsDisposed)
                    return;

                if (!keepEnabled.Contains(control) && control.Enabled)
                {
                    control.Enabled = false;
                    _disabledControls.Add(control);
                }

                foreach (Control child in control.Controls)
                {
                    DisableControlTree(child, keepEnabled);
                }
            }
        }

        private static Control? FindControlByName(Control root, string name)
        {
            if (string.Equals(root.Name, name, StringComparison.Ordinal))
                return root;

            foreach (Control child in root.Controls)
            {
                var match = FindControlByName(child, name);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
