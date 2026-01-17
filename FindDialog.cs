using System.Drawing;
using System.Windows.Forms;

namespace SSH_Helper
{
    internal sealed class FindDialog : Form
    {
        private readonly Form1 _owner;
        private readonly TextBox _txtFind;
        private readonly CheckBox _chkMatchCase;
        private readonly Button _btnPrev;
        private readonly Button _btnNext;
        private readonly Button _btnClose;
        private readonly Label _lblStatus;
        private readonly ToolTip _toolTip;

        internal string SearchText => _txtFind.Text;
        internal bool MatchCase => _chkMatchCase.Checked;

        public FindDialog(Form1 owner, string initialText, bool matchCase)
        {
            _owner = owner;

            Text = "Find";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(380, 70);
            TopMost = true;
            KeyPreview = true;

            _toolTip = new ToolTip();

            // Search textbox
            _txtFind = new TextBox
            {
                Left = 8,
                Top = 10,
                Width = 220,
                Text = initialText
            };

            // Navigation buttons (arrow style)
            _btnPrev = new Button
            {
                Text = "\u25B2",  // Up arrow
                Left = 234,
                Top = 8,
                Width = 32,
                Height = 26,
                Font = new Font(Font.FontFamily, 8f)
            };

            _btnNext = new Button
            {
                Text = "\u25BC",  // Down arrow
                Left = 268,
                Top = 8,
                Width = 32,
                Height = 26,
                Font = new Font(Font.FontFamily, 8f)
            };

            _btnClose = new Button
            {
                Text = "\u2715",  // X symbol
                Left = 306,
                Top = 8,
                Width = 32,
                Height = 26,
                Font = new Font(Font.FontFamily, 9f)
            };

            // Match case checkbox
            _chkMatchCase = new CheckBox
            {
                Left = 344,
                Top = 11,
                Width = 32,
                Height = 20,
                Text = "Aa",
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Checked = matchCase,
                Font = new Font("Consolas", 8f)
            };

            // Status label
            _lblStatus = new Label
            {
                Left = 8,
                Top = 42,
                Width = 368,
                Height = 20,
                ForeColor = Color.DimGray,
                AutoEllipsis = true
            };

            // Tooltips
            _toolTip.SetToolTip(_btnPrev, "Previous match (Shift+F3)");
            _toolTip.SetToolTip(_btnNext, "Next match (F3 or Enter)");
            _toolTip.SetToolTip(_btnClose, "Close (Esc)");
            _toolTip.SetToolTip(_chkMatchCase, "Match case");

            // Event handlers
            _btnNext.Click += (_, _) => FindNext();
            _btnPrev.Click += (_, _) => FindPrevious();
            _btnClose.Click += (_, _) => Hide();
            _chkMatchCase.CheckedChanged += (_, _) => UpdateMatchCount();

            _txtFind.TextChanged += (_, _) =>
            {
                if (_txtFind.Text.Length > 0)
                    FindNext(highlightFirst: true);
                else
                    ClearStatus();
            };

            _txtFind.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (e.Shift)
                        FindPrevious();
                    else
                        FindNext();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            KeyDown += OnKeyDown;

            Controls.Add(_txtFind);
            Controls.Add(_btnPrev);
            Controls.Add(_btnNext);
            Controls.Add(_btnClose);
            Controls.Add(_chkMatchCase);
            Controls.Add(_lblStatus);

            AcceptButton = _btnNext;
        }

        private void FindNext(bool highlightFirst = false)
        {
            if (string.IsNullOrEmpty(_txtFind.Text))
            {
                ClearStatus();
                return;
            }
            _owner.FindFromDialog(_txtFind.Text, _chkMatchCase.Checked, forward: true, highlightFirst);
        }

        private void FindPrevious()
        {
            if (string.IsNullOrEmpty(_txtFind.Text))
            {
                ClearStatus();
                return;
            }
            _owner.FindFromDialog(_txtFind.Text, _chkMatchCase.Checked, forward: false, highlightFirst: false);
        }

        private void UpdateMatchCount()
        {
            if (!string.IsNullOrEmpty(_txtFind.Text))
                _owner.UpdateFindStatus(_txtFind.Text, _chkMatchCase.Checked);
            else
                ClearStatus();
        }

        private void ClearStatus()
        {
            _lblStatus.Text = "";
            _lblStatus.ForeColor = Color.DimGray;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F3:
                    if (e.Shift)
                        FindPrevious();
                    else
                        FindNext();
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    Hide();
                    e.Handled = true;
                    break;
            }
        }

        internal void SetStatus(string message, bool isError = false)
        {
            _lblStatus.ForeColor = isError ? Color.Firebrick : Color.DimGray;
            _lblStatus.Text = message;
        }

        internal void SetMatchInfo(int currentMatch, int totalMatches)
        {
            if (totalMatches == 0)
            {
                _lblStatus.ForeColor = Color.Firebrick;
                _lblStatus.Text = "No matches";
                _btnPrev.Enabled = false;
                _btnNext.Enabled = false;
            }
            else
            {
                _lblStatus.ForeColor = Color.DimGray;
                _lblStatus.Text = $"{currentMatch} of {totalMatches}";
                _btnPrev.Enabled = true;
                _btnNext.Enabled = true;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _txtFind.Focus();
            _txtFind.SelectAll();

            if (!string.IsNullOrEmpty(_txtFind.Text))
                UpdateMatchCount();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
