namespace SSH_Helper.UI
{
    /// <summary>
    /// Lightweight diagnostics view for live process memory and large in-memory text payloads.
    /// </summary>
    public sealed class MemoryDebuggerDialog : Form
    {
        private readonly Func<(long WorkingSetBytes, long PrivateBytes, long ManagedHeapBytes, string Summary)> _snapshotProvider;
        private readonly Func<string> _trimAction;
        private readonly bool _darkMode;
        private readonly Font _summaryFont;

        private readonly Label _lblWorkingSet;
        private readonly Label _lblPrivateBytes;
        private readonly Label _lblManagedHeap;
        private readonly TextBox _txtSummary;
        private readonly Button _btnRefresh;
        private readonly Button _btnTrim;

        public MemoryDebuggerDialog(
            Func<(long WorkingSetBytes, long PrivateBytes, long ManagedHeapBytes, string Summary)> snapshotProvider,
            Func<string> trimAction,
            bool darkMode)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _trimAction = trimAction ?? throw new ArgumentNullException(nameof(trimAction));
            _darkMode = darkMode;
            _summaryFont = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point);

            Text = "Memory Debugger";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            MinimumSize = new Size(780, 520);
            Size = new Size(940, 640);
            ShowInTaskbar = false;

            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                Padding = new Padding(12, 10, 12, 6)
            };

            _lblWorkingSet = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Text = "Working set: -"
            };

            _lblPrivateBytes = new Label
            {
                AutoSize = true,
                Location = new Point(12, 34),
                Text = "Private bytes: -"
            };

            _lblManagedHeap = new Label
            {
                AutoSize = true,
                Location = new Point(12, 56),
                Text = "Managed heap: -"
            };

            topPanel.Controls.AddRange([_lblWorkingSet, _lblPrivateBytes, _lblManagedHeap]);

            _txtSummary = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = _summaryFont
            };

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                Padding = new Padding(12, 8, 12, 8)
            };

            _btnRefresh = new Button
            {
                Text = "Refresh",
                Size = new Size(100, 30),
                Location = new Point(12, 10)
            };
            _btnRefresh.Click += (_, __) => RefreshSnapshot();

            _btnTrim = new Button
            {
                Text = "Trim Memory",
                Size = new Size(120, 30),
                Location = new Point(122, 10)
            };
            _btnTrim.Click += (_, __) => TrimAndRefresh();

            var btnClose = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            buttonPanel.Controls.AddRange([_btnRefresh, _btnTrim, btnClose]);

            Controls.Add(_txtSummary);
            Controls.Add(buttonPanel);
            Controls.Add(topPanel);

            AcceptButton = btnClose;
            CancelButton = btnClose;

            buttonPanel.SizeChanged += (_, __) =>
            {
                btnClose.Location = new Point(Math.Max(12, buttonPanel.ClientSize.Width - btnClose.Width - 12), 10);
            };

            DialogTheme.ApplyTo(this, _darkMode);
            DialogTheme.StyleButton(_btnRefresh, _darkMode);
            DialogTheme.StyleButton(_btnTrim, _darkMode, isPrimary: true);
            DialogTheme.StyleButton(btnClose, _darkMode);
            DialogTheme.SetDarkTitleBar(this, _darkMode);
            DialogTheme.ApplyNativeTheme(_txtSummary, _darkMode);

            Shown += (_, __) => RefreshSnapshot();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _summaryFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void RefreshSnapshot()
        {
            try
            {
                var snapshot = _snapshotProvider();
                _lblWorkingSet.Text = $"Working set: {FormatBytes(snapshot.WorkingSetBytes)}";
                _lblPrivateBytes.Text = $"Private bytes: {FormatBytes(snapshot.PrivateBytes)}";
                _lblManagedHeap.Text = $"Managed heap: {FormatBytes(snapshot.ManagedHeapBytes)}";
                _txtSummary.Text = snapshot.Summary ?? string.Empty;
                _txtSummary.SelectionStart = 0;
                _txtSummary.SelectionLength = 0;
            }
            catch (Exception ex)
            {
                _txtSummary.Text = $"Failed to capture memory diagnostics:{Environment.NewLine}{ex.Message}";
            }
        }

        private void TrimAndRefresh()
        {
            _btnTrim.Enabled = false;
            try
            {
                var message = _trimAction();
                RefreshSnapshot();
                DialogTheme.ShowMessage(this, message, "Memory Trim", MessageBoxIcon.Information, _darkMode, Font);
            }
            finally
            {
                _btnTrim.Enabled = true;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes:N0} B";

            string[] units = ["KB", "MB", "GB", "TB"];
            var value = bytes;
            var unitIndex = -1;
            double displayValue;

            do
            {
                displayValue = value / 1024d;
                value /= 1024;
                unitIndex++;
            }
            while (displayValue >= 1024d && unitIndex < units.Length - 1);

            return $"{displayValue:N2} {units[unitIndex]}";
        }
    }
}
