using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SSH_Helper
{
    internal sealed class FindDialog : Form
    {
        // VS Code dark theme colors
        private static readonly Color BackgroundColor = Color.FromArgb(37, 37, 38);
        private static readonly Color InputBackgroundColor = Color.FromArgb(60, 60, 60);
        private static readonly Color InputBorderColor = Color.FromArgb(69, 69, 69);
        private static readonly Color InputFocusBorderColor = Color.FromArgb(0, 127, 212);
        private static readonly Color TextColor = Color.FromArgb(204, 204, 204);
        private static readonly Color MatchCountColor = Color.FromArgb(158, 158, 158);
        private static readonly Color NoMatchColor = Color.FromArgb(206, 145, 120);
        private static readonly Color ButtonHoverColor = Color.FromArgb(90, 93, 94);
        private static readonly Color ToggleActiveBackColor = Color.FromArgb(14, 99, 156);
        private static readonly Color BorderColor = Color.FromArgb(69, 69, 69);

        private readonly Form1 _owner;
        private readonly Panel _container;
        private readonly TextBox _txtFind;
        private readonly Label _lblMatchCount;
        private readonly IconButton _btnPrev;
        private readonly IconButton _btnNext;
        private readonly ToggleButton _btnMatchCase;
        private readonly ToggleButton _btnWholeWord;
        private readonly ToggleButton _btnRegex;
        private readonly IconButton _btnClose;
        private readonly ToolTip _toolTip;

        private Control? _anchorControl;

        internal string SearchText => _txtFind.Text;
        internal bool MatchCase => _btnMatchCase.IsToggled;

        public FindDialog(Form1 owner, string initialText, bool matchCase)
        {
            _owner = owner;

            // Borderless form
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = BackgroundColor;
            AutoScaleMode = AutoScaleMode.Dpi;
            Size = new Size(420, 34);
            Owner = owner;
            KeyPreview = true;

            _toolTip = new ToolTip { InitialDelay = 400 };

            // Main container panel
            _container = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BackgroundColor,
                Padding = new Padding(4, 0, 4, 0)
            };

            // Input container with border
            var inputPanel = new InputPanel
            {
                Left = 8,
                Top = 4,
                Width = 220,
                Height = 26,
                BackColor = InputBackgroundColor
            };

            // Search textbox
            _txtFind = new TextBox
            {
                Left = 6,
                Top = 4,
                Width = 140,
                Height = 18,
                Text = initialText,
                BackColor = InputBackgroundColor,
                ForeColor = TextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };

            // Match count label (inside input area, right-aligned)
            _lblMatchCount = new Label
            {
                Left = 150,
                Top = 5,
                Width = 66,
                Height = 16,
                ForeColor = MatchCountColor,
                BackColor = InputBackgroundColor,
                Font = new Font("Segoe UI", 8f),
                TextAlign = ContentAlignment.MiddleRight,
                Text = ""
            };

            inputPanel.Controls.Add(_txtFind);
            inputPanel.Controls.Add(_lblMatchCount);

            // Focus handling for input panel border
            _txtFind.GotFocus += (_, _) => inputPanel.IsFocused = true;
            _txtFind.LostFocus += (_, _) => inputPanel.IsFocused = false;

            // Toggle buttons (Aa, ab, .*)
            _btnMatchCase = new ToggleButton("Aa", "Match Case (Alt+C)")
            {
                Left = 234,
                Top = 5,
                IsToggled = matchCase
            };

            _btnWholeWord = new ToggleButton("ab", "Match Whole Word (Alt+W)")
            {
                Left = 262,
                Top = 5,
                IsToggled = false,
                Enabled = false,
                Visible = false // Hide for now - not implemented
            };

            _btnRegex = new ToggleButton(".*", "Use Regular Expression (Alt+R)")
            {
                Left = 290,
                Top = 5,
                IsToggled = false,
                Enabled = false,
                Visible = false // Hide for now - not implemented
            };

            // Navigation buttons
            _btnPrev = new IconButton(IconType.ArrowUp, "Previous Match (Shift+F3)")
            {
                Left = 262,
                Top = 5
            };

            _btnNext = new IconButton(IconType.ArrowDown, "Next Match (F3)")
            {
                Left = 290,
                Top = 5
            };

            // Selection/expand button (visual only, like VS Code)
            var _btnSelection = new IconButton(IconType.Selection, "Find in Selection")
            {
                Left = 318,
                Top = 5,
                Enabled = false,
                Visible = false // Hide - not implemented
            };

            // Close button
            _btnClose = new IconButton(IconType.Close, "Close (Escape)")
            {
                Left = 318,
                Top = 5
            };

            // Set tooltips
            _toolTip.SetToolTip(_btnMatchCase, "Match Case (Alt+C)");
            _toolTip.SetToolTip(_btnPrev, "Previous Match (Shift+F3)");
            _toolTip.SetToolTip(_btnNext, "Next Match (F3)");
            _toolTip.SetToolTip(_btnClose, "Close (Escape)");

            // Event handlers
            _btnNext.Click += (_, _) => FindNext();
            _btnPrev.Click += (_, _) => FindPrevious();
            _btnClose.Click += (_, _) => Hide();
            _btnMatchCase.ToggledChanged += (_, _) => UpdateMatchCount();

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
                else if (e.Alt && e.KeyCode == Keys.C)
                {
                    _btnMatchCase.IsToggled = !_btnMatchCase.IsToggled;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            KeyDown += OnKeyDown;

            // Add controls
            _container.Controls.Add(inputPanel);
            _container.Controls.Add(_btnMatchCase);
            _container.Controls.Add(_btnPrev);
            _container.Controls.Add(_btnNext);
            _container.Controls.Add(_btnClose);

            Controls.Add(_container);
        }

        public void AnchorTo(Control control, int rightMargin = 20)
        {
            _anchorControl = control;
            UpdatePosition();

            // Re-position when parent form moves or resizes
            if (_owner != null)
            {
                _owner.LocationChanged -= Owner_PositionChanged;
                _owner.SizeChanged -= Owner_PositionChanged;
                _owner.LocationChanged += Owner_PositionChanged;
                _owner.SizeChanged += Owner_PositionChanged;
            }
        }

        private void Owner_PositionChanged(object? sender, EventArgs e)
        {
            if (Visible)
                UpdatePosition();
        }

        private void UpdatePosition()
        {
            if (_anchorControl == null) return;

            var screenPoint = _anchorControl.PointToScreen(Point.Empty);
            Left = screenPoint.X + _anchorControl.Width - Width - 20;
            Top = screenPoint.Y + 5;
        }

        private void FindNext(bool highlightFirst = false)
        {
            if (string.IsNullOrEmpty(_txtFind.Text))
            {
                ClearStatus();
                return;
            }
            _owner.FindFromDialog(_txtFind.Text, _btnMatchCase.IsToggled, forward: true, highlightFirst);
        }

        private void FindPrevious()
        {
            if (string.IsNullOrEmpty(_txtFind.Text))
            {
                ClearStatus();
                return;
            }
            _owner.FindFromDialog(_txtFind.Text, _btnMatchCase.IsToggled, forward: false, highlightFirst: false);
        }

        private void UpdateMatchCount()
        {
            if (!string.IsNullOrEmpty(_txtFind.Text))
                _owner.UpdateFindStatus(_txtFind.Text, _btnMatchCase.IsToggled);
            else
                ClearStatus();
        }

        private void ClearStatus()
        {
            _lblMatchCount.Text = "";
            _lblMatchCount.ForeColor = MatchCountColor;
            _btnPrev.Enabled = true;
            _btnNext.Enabled = true;
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
            _lblMatchCount.ForeColor = isError ? NoMatchColor : MatchCountColor;
            _lblMatchCount.Text = message;
        }

        internal void SetMatchInfo(int currentMatch, int totalMatches)
        {
            if (totalMatches == 0)
            {
                _lblMatchCount.ForeColor = NoMatchColor;
                _lblMatchCount.Text = "No results";
                _btnPrev.Enabled = false;
                _btnNext.Enabled = false;
            }
            else
            {
                _lblMatchCount.ForeColor = MatchCountColor;
                _lblMatchCount.Text = $"{currentMatch} of {totalMatches}";
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            // Draw bottom and left border (shadow effect like VS Code)
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
                if (_owner != null)
                {
                    _owner.LocationChanged -= Owner_PositionChanged;
                    _owner.SizeChanged -= Owner_PositionChanged;
                }
            }
            base.Dispose(disposing);
        }

        // Input panel with custom border
        private sealed class InputPanel : Panel
        {
            public bool IsFocused
            {
                get => _isFocused;
                set { _isFocused = value; Invalidate(); }
            }
            private bool _isFocused;

            public InputPanel()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(InputBackgroundColor);

                var borderColor = _isFocused ? InputFocusBorderColor : InputBorderColor;
                using var pen = new Pen(borderColor, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private enum IconType { ArrowUp, ArrowDown, Close, Selection }

        // Icon button matching VS Code style
        private sealed class IconButton : Button
        {
            private readonly IconType _iconType;
            private readonly string _tooltipText;

            public IconButton(IconType iconType, string tooltipText)
            {
                _iconType = iconType;
                _tooltipText = tooltipText;

                Size = new Size(22, 22);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                FlatAppearance.MouseOverBackColor = ButtonHoverColor;
                FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 70, 70);
                BackColor = Color.Transparent;
                Cursor = Cursors.Hand;
                TabStop = false;

                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw hover background
                if (ClientRectangle.Contains(PointToClient(MousePosition)) && Enabled)
                {
                    using var brush = new SolidBrush(ButtonHoverColor);
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }

                var color = Enabled ? TextColor : Color.FromArgb(100, 100, 100);
                using var pen = new Pen(color, 1.2f);

                int cx = Width / 2;
                int cy = Height / 2;

                switch (_iconType)
                {
                    case IconType.ArrowUp:
                        // Up arrow
                        e.Graphics.DrawLine(pen, cx - 4, cy + 2, cx, cy - 2);
                        e.Graphics.DrawLine(pen, cx, cy - 2, cx + 4, cy + 2);
                        break;

                    case IconType.ArrowDown:
                        // Down arrow
                        e.Graphics.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
                        e.Graphics.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
                        break;

                    case IconType.Close:
                        // X icon
                        e.Graphics.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
                        e.Graphics.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
                        break;

                    case IconType.Selection:
                        // Selection icon (three horizontal lines)
                        e.Graphics.DrawLine(pen, cx - 4, cy - 3, cx + 4, cy - 3);
                        e.Graphics.DrawLine(pen, cx - 4, cy, cx + 4, cy);
                        e.Graphics.DrawLine(pen, cx - 4, cy + 3, cx + 4, cy + 3);
                        break;
                }
            }
        }

        // Toggle button (Aa, ab, .*)
        private sealed class ToggleButton : Button
        {
            private bool _isToggled;

            public bool IsToggled
            {
                get => _isToggled;
                set
                {
                    if (_isToggled != value)
                    {
                        _isToggled = value;
                        Invalidate();
                        ToggledChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            public event EventHandler? ToggledChanged;

            public ToggleButton(string text, string tooltipText)
            {
                Text = text;
                Size = new Size(24, 22);
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                BackColor = Color.Transparent;
                ForeColor = TextColor;
                Font = new Font("Consolas", 9f, FontStyle.Bold);
                Cursor = Cursors.Hand;
                TabStop = false;

                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnClick(EventArgs e)
            {
                IsToggled = !IsToggled;
                base.OnClick(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                // Always clear with parent background first to avoid artifacts
                e.Graphics.Clear(BackgroundColor);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Background
                var isHovered = ClientRectangle.Contains(PointToClient(MousePosition));
                Color bgColor;

                if (_isToggled)
                    bgColor = ToggleActiveBackColor;
                else if (isHovered)
                    bgColor = ButtonHoverColor;
                else
                    bgColor = BackgroundColor;

                using (var brush = new SolidBrush(bgColor))
                {
                    e.Graphics.FillRectangle(brush, ClientRectangle);
                }

                // Border when toggled
                if (_isToggled)
                {
                    using var pen = new Pen(InputFocusBorderColor, 1);
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }

                // Text
                var textColor = _isToggled ? Color.White : TextColor;
                using (var brush = new SolidBrush(textColor))
                {
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    e.Graphics.DrawString(Text, Font, brush, ClientRectangle, sf);
                }
            }
        }
    }
}
