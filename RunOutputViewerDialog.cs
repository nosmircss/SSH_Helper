using SSH_Helper.Models;
using SSH_Helper.UI;

namespace SSH_Helper
{
    /// <summary>
    /// Displays per-host SSH output from a historical job run.
    /// Provides host selection dropdown, in-output search, and clipboard copy.
    /// </summary>
    internal sealed class RunOutputViewerDialog : Form
    {
        private readonly JobRunPayload _payload;
        private readonly bool _darkMode;

        private readonly ComboBox _cboHost;
        private readonly RichTextBox _rtbOutput;
        private readonly Button _btnFind;
        private readonly Button _btnCopyAll;
        private readonly Button _btnClose;
        private readonly Panel _searchPanel;
        private readonly TextBox _txtSearch;
        private readonly Label _lblSearchStatus;
        private int _lastSearchIndex;

        public RunOutputViewerDialog(
            JobRunPayload payload,
            bool darkMode,
            string? fontFamily = null,
            float fontSize = 9f)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            _darkMode = darkMode;

            Text = $"Run Output - {payload.JobName} ({payload.StartedUtc.ToLocalTime():g})";
            Size = new Size(900, 600);
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;

            // --- Top panel with host selector and action buttons ---
            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 35,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(6, 4, 6, 0)
            };

            var lblHost = new Label
            {
                Text = "Host:",
                AutoSize = true,
                Margin = new Padding(0, 5, 4, 0)
            };

            _cboHost = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 220,
                Margin = new Padding(0, 2, 8, 0)
            };
            _cboHost.SelectedIndexChanged += CboHost_SelectedIndexChanged;

            _btnFind = new Button
            {
                Text = "Find",
                Size = new Size(60, 26),
                Margin = new Padding(0, 1, 4, 0)
            };
            _btnFind.Click += BtnFind_Click;

            _btnCopyAll = new Button
            {
                Text = "Copy All",
                Size = new Size(75, 26),
                Margin = new Padding(0, 1, 0, 0)
            };
            _btnCopyAll.Click += BtnCopyAll_Click;

            topPanel.Controls.Add(lblHost);
            topPanel.Controls.Add(_cboHost);
            topPanel.Controls.Add(_btnFind);
            topPanel.Controls.Add(_btnCopyAll);

            // --- Search bar (hidden by default, toggled by Find button or Ctrl+F) ---
            _searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30,
                Visible = false,
                Padding = new Padding(6, 2, 6, 2)
            };

            _txtSearch = new TextBox
            {
                Width = 250,
                Location = new Point(6, 3),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _txtSearch.KeyDown += TxtSearch_KeyDown;

            _lblSearchStatus = new Label
            {
                AutoSize = true,
                Location = new Point(264, 6),
                Text = ""
            };

            var btnSearchClose = new Button
            {
                Text = "X",
                Size = new Size(24, 22),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(6, 3),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSearchClose.Click += (_, _) =>
            {
                _searchPanel.Visible = false;
                _rtbOutput?.Focus();
            };

            _searchPanel.Controls.Add(_txtSearch);
            _searchPanel.Controls.Add(_lblSearchStatus);
            _searchPanel.Controls.Add(btnSearchClose);

            _searchPanel.Resize += (_, _) =>
            {
                btnSearchClose.Left = _searchPanel.ClientSize.Width - btnSearchClose.Width - 6;
            };

            // --- Main output area ---
            _rtbOutput = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                WordWrap = false,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Both
            };

            // --- Bottom panel with Close button ---
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 45,
                Padding = new Padding(6, 6, 6, 6)
            };

            _btnClose = new Button
            {
                Text = "Close",
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            _btnClose.Location = new Point(
                bottomPanel.ClientSize.Width - _btnClose.Width - 12,
                8);

            bottomPanel.Resize += (_, _) =>
            {
                _btnClose.Left = bottomPanel.ClientSize.Width - _btnClose.Width - 12;
            };

            bottomPanel.Controls.Add(_btnClose);

            // --- Add controls in correct order for Dock layout ---
            // Bottom and Top panels first, then Fill panel last
            Controls.Add(_rtbOutput);
            Controls.Add(_searchPanel);
            Controls.Add(topPanel);
            Controls.Add(bottomPanel);

            AcceptButton = _btnClose;
            CancelButton = _btnClose;

            // --- Populate host dropdown ---
            PopulateHostSelector();

            // --- Apply theming ---
            DialogTheme.ApplyTo(this, darkMode);
            DialogTheme.StyleButton(_btnFind, darkMode);
            DialogTheme.StyleButton(_btnCopyAll, darkMode);
            DialogTheme.StyleButton(_btnClose, darkMode, isPrimary: true);
            DialogTheme.StyleButton(btnSearchClose, darkMode);
            DialogTheme.SetDarkTitleBar(this, darkMode);

            if (!string.IsNullOrEmpty(fontFamily))
            {
                DialogTheme.SetDialogFont(this, new Font(fontFamily, fontSize));
            }

            // Keyboard shortcut: Ctrl+F opens search
            KeyDown += (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.F)
                {
                    ToggleSearchBar();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            if (darkMode)
            {
                Load += (_, _) => DialogTheme.ApplyNativeTheme(this, true);
            }
        }

        private void PopulateHostSelector()
        {
            _cboHost.Items.Clear();

            if (_payload.HostOutputs == null || _payload.HostOutputs.Count == 0)
            {
                _rtbOutput.Text = "No output available";
                _cboHost.Enabled = false;
                return;
            }

            foreach (var hostOutput in _payload.HostOutputs)
            {
                var statusIndicator = hostOutput.Success ? "OK" : "FAIL";
                _cboHost.Items.Add(new HostOutputItem(hostOutput, $"{hostOutput.HostAddress} ({statusIndicator})"));
            }

            if (_cboHost.Items.Count > 0)
            {
                _cboHost.SelectedIndex = 0;
            }
        }

        private void CboHost_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_cboHost.SelectedItem is HostOutputItem item)
            {
                _rtbOutput.Text = string.IsNullOrEmpty(item.HostOutput.Output)
                    ? "(no output captured for this host)"
                    : item.HostOutput.Output;
                _rtbOutput.SelectionStart = 0;
                _rtbOutput.SelectionLength = 0;
                _lastSearchIndex = 0;
                _lblSearchStatus.Text = "";
            }
        }

        private void BtnFind_Click(object? sender, EventArgs e)
        {
            ToggleSearchBar();
        }

        private void ToggleSearchBar()
        {
            _searchPanel.Visible = !_searchPanel.Visible;
            if (_searchPanel.Visible)
            {
                _txtSearch.Focus();
                _txtSearch.SelectAll();
            }
        }

        private void BtnCopyAll_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_rtbOutput.Text))
            {
                return;
            }

            try
            {
                Clipboard.SetText(_rtbOutput.Text);
                DialogTheme.Show(this,
                    "Output copied to clipboard.",
                    "Copied",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                DialogTheme.Show(this,
                    $"Failed to copy output: {ex.Message}",
                    "Copy Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void TxtSearch_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SearchInOutput(e.Shift);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                _searchPanel.Visible = false;
                _rtbOutput.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void SearchInOutput(bool reverse = false)
        {
            var searchText = _txtSearch.Text;
            if (string.IsNullOrEmpty(searchText) || string.IsNullOrEmpty(_rtbOutput.Text))
            {
                _lblSearchStatus.Text = "";
                return;
            }

            var options = RichTextBoxFinds.None;
            if (reverse)
            {
                options |= RichTextBoxFinds.Reverse;
            }

            int startIndex;
            int endIndex;

            if (reverse)
            {
                startIndex = 0;
                endIndex = _lastSearchIndex > 0 ? _lastSearchIndex : _rtbOutput.TextLength;
            }
            else
            {
                startIndex = _lastSearchIndex;
                endIndex = _rtbOutput.TextLength;
            }

            if (startIndex < 0) startIndex = 0;
            if (startIndex > _rtbOutput.TextLength) startIndex = 0;

            var index = _rtbOutput.Find(searchText, startIndex, endIndex, options);

            if (index < 0 && !reverse)
            {
                // Wrap around from beginning
                index = _rtbOutput.Find(searchText, 0, _rtbOutput.TextLength, RichTextBoxFinds.None);
                if (index >= 0)
                {
                    _lblSearchStatus.Text = "Wrapped";
                }
            }
            else if (index < 0 && reverse)
            {
                // Wrap around from end
                index = _rtbOutput.Find(searchText, 0, _rtbOutput.TextLength, RichTextBoxFinds.Reverse);
                if (index >= 0)
                {
                    _lblSearchStatus.Text = "Wrapped";
                }
            }

            if (index >= 0)
            {
                _rtbOutput.Select(index, searchText.Length);
                _rtbOutput.ScrollToCaret();
                _lastSearchIndex = reverse ? index : index + searchText.Length;
                if (_lblSearchStatus.Text != "Wrapped")
                {
                    _lblSearchStatus.Text = "";
                }
            }
            else
            {
                _lblSearchStatus.Text = "Not found";
                _lastSearchIndex = 0;
            }
        }

        /// <summary>
        /// Wraps a JobHostOutput for ComboBox display with success/fail indicator.
        /// </summary>
        private sealed class HostOutputItem
        {
            public JobHostOutput HostOutput { get; }
            private readonly string _displayText;

            public HostOutputItem(JobHostOutput hostOutput, string displayText)
            {
                HostOutput = hostOutput;
                _displayText = displayText;
            }

            public override string ToString() => _displayText;
        }
    }
}
