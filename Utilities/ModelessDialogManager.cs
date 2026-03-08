namespace SSH_Helper.Utilities
{
    internal sealed class ModelessDialogManager<TForm>
        where TForm : Form
    {
        private TForm? _current;

        public TForm ShowOrActivate(Func<TForm> factory, IWin32Window owner)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(owner);

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
            var dialog = _current;
            _current.FormClosed += (_, _) =>
            {
                if (ReferenceEquals(_current, dialog))
                {
                    _current = null;
                }
            };
            _current.Show(owner);
            return _current;
        }

        public TForm? Current => _current != null && !_current.IsDisposed ? _current : null;
    }
}
