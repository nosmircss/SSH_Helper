namespace SSH_Helper.Utilities
{
    internal sealed class ModelessDialogManager<TForm>
        where TForm : Form
    {
        internal static Action<Form>? RestoreOwnerActivationOverrideForTests { get; set; }

        private TForm? _current;

        public TForm ShowOrActivate(Func<TForm> factory, IWin32Window owner)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(owner);

            var ownerForm = owner as Form;

            if (_current != null && !_current.IsDisposed)
            {
                if (_current.WindowState == FormWindowState.Minimized)
                {
                    _current.WindowState = FormWindowState.Normal;
                }

                if (!_current.Visible)
                {
                    _current.Show(owner);
                }

                _current.BringToFront();
                _current.Activate();
                return _current;
            }

            _current = factory();
            PrepareInitialShow(_current, ownerForm);
            var dialog = _current;
            _current.FormClosed += (_, _) =>
            {
                if (ReferenceEquals(_current, dialog))
                {
                    _current = null;
                }

                RestoreOwnerActivation(ownerForm);
            };
            _current.Show(owner);
            _current.BringToFront();
            _current.Activate();
            return _current;
        }

        public TForm? Current => _current != null && !_current.IsDisposed ? _current : null;

        private static void PrepareInitialShow(Form dialog, Form? ownerForm)
        {
            if (ownerForm == null || dialog.IsDisposed || dialog.StartPosition != FormStartPosition.CenterParent)
            {
                return;
            }

            dialog.StartPosition = FormStartPosition.Manual;
            PositionCenteredOnOwner(dialog, ownerForm);

            dialog.Load += (_, _) => PositionCenteredOnOwner(dialog, ownerForm);
        }

        private static void PositionCenteredOnOwner(Form dialog, Form ownerForm)
        {
            if (dialog.IsDisposed || ownerForm.IsDisposed || !ownerForm.Visible)
            {
                return;
            }

            var anchorBounds = ownerForm.WindowState == FormWindowState.Minimized
                ? Screen.FromControl(ownerForm).WorkingArea
                : ownerForm.Bounds;
            var workingArea = Screen.FromRectangle(anchorBounds).WorkingArea;

            var x = anchorBounds.Left + ((anchorBounds.Width - dialog.Width) / 2);
            var y = anchorBounds.Top + ((anchorBounds.Height - dialog.Height) / 2);

            x = Math.Max(workingArea.Left, Math.Min(x, workingArea.Right - dialog.Width));
            y = Math.Max(workingArea.Top, Math.Min(y, workingArea.Bottom - dialog.Height));

            dialog.Location = new Point(x, y);
        }

        private static void RestoreOwnerActivation(Form? ownerForm)
        {
            if (ownerForm == null || ownerForm.IsDisposed || !ownerForm.Visible)
            {
                return;
            }

            void ReactivateOwner()
            {
                if (ownerForm.IsDisposed || !ownerForm.Visible || ownerForm.WindowState == FormWindowState.Minimized)
                {
                    return;
                }

                var activationOverride = RestoreOwnerActivationOverrideForTests;
                if (activationOverride != null)
                {
                    activationOverride(ownerForm);
                    return;
                }

                ownerForm.BringToFront();
                ownerForm.Activate();
            }

            try
            {
                if (ownerForm.InvokeRequired)
                {
                    ownerForm.BeginInvoke((Action)ReactivateOwner);
                }
                else
                {
                    ReactivateOwner();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
