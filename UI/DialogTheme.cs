using System.Runtime.InteropServices;

namespace SSH_Helper.UI
{
    /// <summary>
    /// Shared theme colors and helpers for all dialogs.
    /// </summary>
    internal static class DialogTheme
    {
        // Light theme colors
        public static readonly Color LightBackground = Color.FromArgb(248, 249, 250);
        public static readonly Color LightPanel = Color.White;
        public static readonly Color LightText = Color.FromArgb(33, 37, 41);
        public static readonly Color LightSecondaryText = Color.FromArgb(108, 117, 125);
        public static readonly Color LightBorder = Color.FromArgb(222, 226, 230);
        public static readonly Color LightInput = Color.White;
        public static readonly Color LightAccent = Color.FromArgb(0, 120, 212);

        // Dark theme colors (VS Code inspired)
        public static readonly Color DarkBackground = Color.FromArgb(30, 30, 30);
        public static readonly Color DarkSurface1 = Color.FromArgb(37, 37, 38);
        public static readonly Color DarkSurface2 = Color.FromArgb(45, 45, 46);
        public static readonly Color DarkText = Color.FromArgb(204, 204, 204);
        public static readonly Color DarkSecondaryText = Color.FromArgb(128, 128, 128);
        public static readonly Color DarkBorder = Color.FromArgb(48, 48, 48);
        public static readonly Color DarkInput = Color.FromArgb(60, 60, 60);
        public static readonly Color DarkAccent = Color.FromArgb(0, 120, 212);

        /// <summary>
        /// Recursively applies theme colors to a control and all its children.
        /// </summary>
        public static void ApplyTo(Control root, bool darkMode)
        {
            ApplyToControl(root, darkMode);

            foreach (Control child in root.Controls)
            {
                ApplyTo(child, darkMode);
            }
        }

        private static void ApplyToControl(Control control, bool darkMode)
        {
            var bg = darkMode ? DarkBackground : LightBackground;
            var surface = darkMode ? DarkSurface1 : LightPanel;
            var text = darkMode ? DarkText : LightText;
            var secondaryText = darkMode ? DarkSecondaryText : LightSecondaryText;
            var input = darkMode ? DarkInput : LightInput;
            var border = darkMode ? DarkBorder : LightBorder;

            switch (control)
            {
                case Form form:
                    form.BackColor = bg;
                    form.ForeColor = text;
                    break;

                case TabControl tab:
                    tab.BackColor = bg;
                    tab.ForeColor = text;
                    break;

                case TabPage tabPage:
                    tabPage.BackColor = bg;
                    tabPage.ForeColor = text;
                    break;

                case Button btn:
                    // Buttons get styled separately via StyleButton
                    break;

                case TextBox txt:
                    txt.BackColor = input;
                    txt.ForeColor = text;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case RichTextBox rtb:
                    rtb.BackColor = input;
                    rtb.ForeColor = text;
                    rtb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case CheckBox chk:
                    chk.BackColor = Color.Transparent;
                    chk.ForeColor = text;
                    break;

                case RadioButton rb:
                    rb.BackColor = Color.Transparent;
                    rb.ForeColor = text;
                    break;

                case NumericUpDown nud:
                    nud.BackColor = input;
                    nud.ForeColor = text;
                    break;

                case ComboBox cbo:
                    cbo.BackColor = input;
                    cbo.ForeColor = text;
                    if (darkMode)
                    {
                        cbo.FlatStyle = FlatStyle.Flat;
                    }
                    break;

                case CheckedListBox clb:
                    clb.BackColor = input;
                    clb.ForeColor = text;
                    break;

                case ListBox lb:
                    lb.BackColor = input;
                    lb.ForeColor = text;
                    break;

                case TrackBar trk:
                    trk.BackColor = bg;
                    break;

                case ProgressBar:
                    // ProgressBar styling is limited in WinForms
                    break;

                case LinkLabel lnk:
                    lnk.BackColor = Color.Transparent;
                    lnk.LinkColor = darkMode ? Color.FromArgb(75, 156, 211) : Color.FromArgb(0, 102, 204);
                    lnk.ActiveLinkColor = darkMode ? Color.FromArgb(100, 180, 230) : Color.FromArgb(0, 80, 160);
                    lnk.VisitedLinkColor = darkMode ? Color.FromArgb(75, 156, 211) : Color.FromArgb(0, 102, 204);
                    break;

                case Panel pnl:
                    pnl.BackColor = bg;
                    pnl.ForeColor = text;
                    break;

                case Label lbl:
                    lbl.BackColor = Color.Transparent;
                    // Preserve intentional secondary text colors
                    if (lbl.ForeColor == Color.Gray ||
                        lbl.ForeColor == Color.FromArgb(108, 117, 125) ||
                        lbl.ForeColor == Color.FromArgb(70, 70, 70))
                    {
                        lbl.ForeColor = secondaryText;
                    }
                    else
                    {
                        lbl.ForeColor = text;
                    }
                    break;
            }
        }

        /// <summary>
        /// Styles a button as primary (accent) or normal.
        /// </summary>
        public static void StyleButton(Button btn, bool darkMode, bool isPrimary = false)
        {
            if (isPrimary)
            {
                btn.BackColor = darkMode ? DarkAccent : LightAccent;
                btn.ForeColor = Color.White;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
            }
            else
            {
                btn.BackColor = darkMode ? DarkSurface2 : SystemColors.Control;
                btn.ForeColor = darkMode ? DarkText : LightText;
                btn.FlatStyle = darkMode ? FlatStyle.Flat : FlatStyle.Standard;
                if (darkMode)
                {
                    btn.FlatAppearance.BorderColor = DarkBorder;
                    btn.FlatAppearance.BorderSize = 1;
                }
            }
        }

        /// <summary>
        /// Sets the Windows dark mode title bar attribute on a form.
        /// </summary>
        public static void SetDarkTitleBar(Form form, bool darkMode)
        {
            try
            {
                var value = darkMode ? 1 : 0;
                NativeMethods.DwmSetWindowAttribute(
                    form.Handle,
                    NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref value,
                    sizeof(int));
            }
            catch
            {
                // Ignore on older Windows versions
            }
        }

        /// <summary>
        /// Applies dark/light scrollbar and native theme to a control and its children.
        /// Forces handle creation for controls on non-visible tab pages so the theme applies everywhere.
        /// </summary>
        public static void ApplyNativeTheme(Control root, bool darkMode)
        {
            // Force handle creation so native theme can be applied even
            // to controls on non-visible tab pages.
            if (!root.IsHandleCreated)
                _ = root.Handle;

            ApplyNativeThemeToControl(root, darkMode);

            foreach (Control child in root.Controls)
            {
                ApplyNativeTheme(child, darkMode);
            }
        }

        private static void ApplyNativeThemeToControl(Control control, bool darkMode)
        {
            if (!control.IsHandleCreated) return;

            var theme = darkMode ? "DarkMode_Explorer" : "Explorer";

            switch (control)
            {
                case CheckedListBox:
                case ListBox:
                case TreeView:
                case ListView:
                case TextBox txt when txt.Multiline:
                case RichTextBox:
                    ApplyScrollbarThemeToHandle(control.Handle, darkMode);
                    break;

                case TabControl:
                    // Apply dark mode to the tab control itself
                    NativeMethods.AllowDarkModeForWindow(control.Handle, darkMode);
                    NativeMethods.SetWindowTheme(control.Handle, theme, null);
                    NativeMethods.SendMessage(control.Handle, NativeMethods.WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
                    break;

                case CheckBox:
                case RadioButton:
                case ComboBox:
                case NumericUpDown:
                case TrackBar:
                    // Apply dark theme to native sub-windows (check square, dropdown arrow, spin buttons, slider track)
                    ApplyScrollbarThemeToHandle(control.Handle, darkMode);
                    break;

                case Panel pnl when pnl.AutoScroll:
                    ApplyScrollbarThemeToHandle(control.Handle, darkMode);
                    break;
            }
        }

        /// <summary>
        /// Applies dark or light owner-draw styling to a TabControl so tab headers
        /// match the dark theme (same approach as Form1's presetsTabControl).
        /// </summary>
        public static void StyleTabControl(TabControl tabControl, bool darkMode)
        {
            if (darkMode)
            {
                tabControl.Appearance = TabAppearance.Normal;
                tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabControl.DrawItem -= DarkTabControl_DrawItem;
                tabControl.DrawItem += DarkTabControl_DrawItem;
                tabControl.Paint -= DarkTabControl_Paint;
                tabControl.Paint += DarkTabControl_Paint;

                if (tabControl is BorderlessTabControl borderlessTab)
                {
                    borderlessTab.HideBorder = true;
                    borderlessTab.BorderBackgroundColor = DarkBackground;
                }

                if (tabControl.Parent is Panel parentPanel)
                {
                    parentPanel.BackColor = DarkBackground;
                }

                foreach (TabPage page in tabControl.TabPages)
                {
                    page.BackColor = DarkBackground;
                    page.ForeColor = DarkText;
                }

                tabControl.Invalidate();
                tabControl.Parent?.Invalidate();
            }
            else
            {
                tabControl.Appearance = TabAppearance.Normal;
                tabControl.DrawMode = TabDrawMode.Normal;
                tabControl.DrawItem -= DarkTabControl_DrawItem;
                tabControl.Paint -= DarkTabControl_Paint;

                if (tabControl is BorderlessTabControl borderlessTab)
                {
                    borderlessTab.HideBorder = false;
                }

                foreach (TabPage page in tabControl.TabPages)
                {
                    page.BackColor = SystemColors.Control;
                    page.ForeColor = SystemColors.ControlText;
                }

                tabControl.Invalidate();
            }
        }

        private static void DarkTabControl_Paint(object? sender, PaintEventArgs e)
        {
            if (sender is not TabControl tabControl) return;

            using var bgBrush = new SolidBrush(DarkBackground);
            using var headerBrush = new SolidBrush(DarkSurface1);

            var tabHeight = tabControl.ItemSize.Height + 4;

            // Fill the entire content area below the tabs
            var contentRect = new Rectangle(0, tabHeight - 2, tabControl.Width, tabControl.Height - tabHeight + 2);
            e.Graphics.FillRectangle(bgBrush, contentRect);

            // Paint thick borders to cover default 3D effects
            e.Graphics.FillRectangle(bgBrush, 0, tabHeight - 2, 4, tabControl.Height - tabHeight + 4);
            e.Graphics.FillRectangle(bgBrush, tabControl.Width - 4, tabHeight - 2, 4, tabControl.Height - tabHeight + 4);
            e.Graphics.FillRectangle(bgBrush, 0, tabControl.Height - 4, tabControl.Width, 4);

            // Fill area to the right of the last tab
            if (tabControl.TabCount > 0)
            {
                var lastTabRect = tabControl.GetTabRect(tabControl.TabCount - 1);
                var fillRect = new Rectangle(lastTabRect.Right, 0, tabControl.Width - lastTabRect.Right, tabHeight - 2);
                e.Graphics.FillRectangle(headerBrush, fillRect);

                // Fill above the tabs to cover top border
                e.Graphics.FillRectangle(headerBrush, 0, 0, tabControl.Width, 2);
            }

            // Subtle separator line between tabs and content
            using var borderPen = new Pen(DarkBorder);
            e.Graphics.DrawLine(borderPen, 0, tabHeight - 2, tabControl.Width, tabHeight - 2);
        }

        private static void DarkTabControl_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabControl) return;

            var tabPage = tabControl.TabPages[e.Index];
            var tabRect = tabControl.GetTabRect(e.Index);
            var isSelected = tabControl.SelectedIndex == e.Index;

            // Fill header background to eliminate white artifacts
            using (var headerBrush = new SolidBrush(DarkSurface1))
            {
                e.Graphics.FillRectangle(headerBrush, tabRect.X - 4, 0, tabRect.Width + 8, tabRect.Y + 2);
            }

            // Draw tab background
            var bgColor = isSelected ? DarkBackground : DarkSurface2;
            using (var bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.FillRectangle(bgBrush, tabRect);
            }

            if (isSelected)
            {
                // Accent line at top of selected tab
                using var accentPen = new Pen(DarkAccent, 2);
                e.Graphics.DrawLine(accentPen, tabRect.Left, tabRect.Top + 1, tabRect.Right - 1, tabRect.Top + 1);

                // Blend bottom with content area
                using var contentBrush = new SolidBrush(DarkBackground);
                e.Graphics.FillRectangle(contentBrush, tabRect.Left - 2, tabRect.Bottom - 2, tabRect.Width + 4, 6);
            }
            else
            {
                // Cover edge highlights on unselected tabs
                using var edgeBrush = new SolidBrush(DarkSurface1);
                e.Graphics.FillRectangle(edgeBrush, tabRect.Right - 1, tabRect.Y, 4, tabRect.Height);
                e.Graphics.FillRectangle(edgeBrush, tabRect.Left - 3, tabRect.Y, 4, tabRect.Height);

                // Darker top line for unselected tabs
                using var topPen = new Pen(DarkSurface1, 2);
                e.Graphics.DrawLine(topPen, tabRect.Left, tabRect.Top + 1, tabRect.Right - 1, tabRect.Top + 1);

                // Bottom border
                using var borderBrush = new SolidBrush(DarkBackground);
                e.Graphics.FillRectangle(borderBrush, tabRect.Left - 2, tabRect.Bottom - 1, tabRect.Width + 4, 5);

                using var borderPen = new Pen(DarkBorder);
                e.Graphics.DrawLine(borderPen, tabRect.Left - 2, tabRect.Bottom - 1, tabRect.Right + 2, tabRect.Bottom - 1);
            }

            // Draw tab text
            var textColor = isSelected ? Color.White : DarkSecondaryText;
            using (var textBrush = new SolidBrush(textColor))
            {
                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(tabPage.Text, tabControl.Font, textBrush, tabRect, sf);
            }
        }

        private static void ApplyScrollbarThemeToHandle(IntPtr handle, bool dark)
        {
            NativeMethods.AllowDarkModeForWindow(handle, dark);

            var theme = dark ? "DarkMode_Explorer" : "Explorer";
            NativeMethods.SetWindowTheme(handle, theme, null);

            NativeMethods.EnumChildWindows(handle, (childHwnd, lParam) =>
            {
                NativeMethods.AllowDarkModeForWindow(childHwnd, dark);
                NativeMethods.SetWindowTheme(childHwnd, theme, null);
                return true;
            }, IntPtr.Zero);

            NativeMethods.SendMessage(handle, NativeMethods.WM_THEMECHANGED, IntPtr.Zero, IntPtr.Zero);
            NativeMethods.SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_FRAMECHANGED);
        }
    }
}
