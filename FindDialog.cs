using System;
using System.Drawing;
using System.Windows.Forms;

namespace SSH_Helper
{
    internal class FindDialog : Form
    {
        private readonly Form1 _owner;
        private TextBox txtFind;
        private CheckBox chkMatchCase;
        private CheckBox chkWrap;
        private Button btnNext;
        private Button btnPrev;
        private Button btnClose;
        private Label lblStatus;

        internal string SearchText => txtFind.Text;
        internal bool MatchCase => chkMatchCase.Checked;
        internal bool Wrap => chkWrap.Checked;

        public FindDialog(Form1 owner, string initialText, bool matchCase, bool wrap)
        {
            _owner = owner;
            InitializeComponent();

            txtFind.Text = initialText;
            chkMatchCase.Checked = matchCase;
            chkWrap.Checked = wrap;

            AcceptButton = btnNext; // Enter = Find Next
        }

        private void InitializeComponent()
        {
            Text = "Find";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 420;
            Height = 170;
            TopMost = true; // Always on top

            txtFind = new TextBox { Left = 12, Top = 12, Width = 390, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            chkMatchCase = new CheckBox { Left = 12, Top = 45, Width = 120, Text = "Match case" };
            chkWrap = new CheckBox { Left = 140, Top = 45, Width = 120, Text = "Wrap search", Checked = true };

            btnNext = new Button { Text = "Find Next (F3)", Left = 12, Top = 75, Width = 95 };
            btnPrev = new Button { Text = "Find Previous (Shift-F3)", Left = 113, Top = 75, Width = 110 };
            btnClose = new Button { Text = "Close", Left = 229, Top = 75, Width = 75 };

            lblStatus = new Label { Left = 12, Top = 110, Width = 390, Height = 22, ForeColor = Color.DimGray, AutoEllipsis = true };

            btnNext.Click += (s, e) => _owner.FindNextFromDialog(txtFind.Text, chkMatchCase.Checked, chkWrap.Checked);
            btnPrev.Click += (s, e) => _owner.FindPreviousFromDialog(txtFind.Text, chkMatchCase.Checked, chkWrap.Checked);
            btnClose.Click += (s, e) => Hide();
            txtFind.TextChanged += (s, e) => { lblStatus.Text = ""; };

            // Enter / Shift+Enter support directly in the textbox
            txtFind.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    if (e.Shift)
                        _owner.FindPreviousFromDialog(txtFind.Text, chkMatchCase.Checked, chkWrap.Checked);
                    else
                        _owner.FindNextFromDialog(txtFind.Text, chkMatchCase.Checked, chkWrap.Checked);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            Controls.Add(txtFind);
            Controls.Add(chkMatchCase);
            Controls.Add(chkWrap);
            Controls.Add(btnNext);
            Controls.Add(btnPrev);
            Controls.Add(btnClose);
            Controls.Add(lblStatus);

            KeyPreview = true;
            KeyDown += FindDialog_KeyDown;
        }

        private void FindDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F3)
            {
                if (e.Shift)
                    _owner.FindPreviousFromDialog(txtFind.Text, chkMatchCase.Checked, chkWrap.Checked);
                else
                    _owner.FindNextFromDialog(txtFind.Text, chkMatchCase.Checked, chkWrap.Checked);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        internal void SetStatus(string message, bool error = false)
        {
            lblStatus.ForeColor = error ? Color.Firebrick : Color.DimGray;
            lblStatus.Text = message;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            txtFind.Focus();
            txtFind.SelectAll();
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
    }
}