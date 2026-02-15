using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Rebex.TerminalEmulation;
using SSH_Helper.UI;

namespace SSH_Helper.Forms
{
    internal sealed class TerminalKeyEventArgs : EventArgs
    {
        public FunctionKey? FunctionKey { get; init; }
        public ConsoleKey? ConsoleKey { get; init; }
        public ConsoleModifiers Modifiers { get; init; }
    }

    internal sealed class TerminalSizeChangedEventArgs : EventArgs
    {
        public int Columns { get; init; }
        public int Rows { get; init; }
    }

    internal sealed class TerminalScreenSnapshot
    {
        public required int Columns { get; init; }
        public required int Rows { get; init; }
        public required int HistoryLength { get; init; }
        public required int ScrollbackOffset { get; init; }
        public required char[] Characters { get; init; }
        public required int[] ForeColors { get; init; }
        public required int[] BackColors { get; init; }
        public int CursorColumn { get; init; } = -1;
        public int CursorRow { get; init; } = -1;
        public int CursorForeColor { get; init; }
        public int CursorBackColor { get; init; }
        public required int Hash { get; init; }
    }

    internal sealed class InteractiveTerminalForm : Form
    {
        private static readonly object _placementLock = new();
        private static Point? _lastKnownLocation;

        private const int WmSysCommand = 0x0112;
        private const uint MfString = 0x0000;
        private const uint MfSeparator = 0x0800;
        private const int SysMenuCommandCopyAllToClipboard = 0x1F00;
        private const int SysMenuCommandClearScrollback = 0x1F10;
        private const int SysMenuCommandResetTerminal = 0x1F20;

        private readonly InteractiveTerminalViewportControl _terminalView;
        private readonly VScrollBar _historyScrollBar;
        private readonly bool _isDarkMode;
        private readonly string _baseTitle;
        private int _lastColumns;
        private int _lastRows;
        private int _lastRenderHash = int.MinValue;
        private int _historyLength;
        private volatile int _scrollbackOffset;
        private bool _syncingScrollBar;
        private bool _acceptHostInput = true;
        private bool _allowTerminalActions = true;
        private string[]? _detachedHistoryLines;

        public bool IsFollowingTail => _scrollbackOffset == 0;
        public int ScrollbackOffset => _scrollbackOffset;

        public Func<string?>? CopyAllTextProvider { get; set; }
        public Action? ClearScrollbackAction { get; set; }
        public Action? ResetTerminalAction { get; set; }

        public event EventHandler<string>? TextInput;
        public event EventHandler<TerminalKeyEventArgs>? KeyInput;
        public event EventHandler<TerminalSizeChangedEventArgs>? TerminalSizeChanged;

        public InteractiveTerminalForm(string title)
        {
            Text = title;
            _baseTitle = title;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(760, 420);
            Size = new Size(980, 620);
            KeyPreview = true;
            Padding = Padding.Empty;
            BackColor = Color.Black;
            ApplyRememberedLocation();

            _terminalView = new InteractiveTerminalViewportControl
            {
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Font = new Font("Courier New", 10f, FontStyle.Regular, GraphicsUnit.Point),
                BackColor = Color.Black,
                ForeColor = Color.FromArgb(187, 187, 187)
            };
            _historyScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Width = SystemInformation.VerticalScrollBarWidth,
                SmallChange = 1,
                LargeChange = 1,
                Minimum = 0,
                Maximum = 0,
                Value = 0,
                Enabled = false
            };

            var mainForm = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            _isDarkMode = mainForm != null && mainForm.BackColor.GetBrightness() < 0.2f;

            Controls.Add(_terminalView);
            Controls.Add(_historyScrollBar);

            KeyDown += InteractiveTerminalForm_KeyDown;
            KeyPress += InteractiveTerminalForm_KeyPress;
            _terminalView.Resize += (_, _) => EmitTerminalSizeChanged();
            _terminalView.MouseWheel += TerminalView_MouseWheel;
            _terminalView.MouseEnter += (_, _) => _terminalView.Focus();
            _terminalView.MouseDown += TerminalView_MouseDown;
            _historyScrollBar.ValueChanged += HistoryScrollBar_ValueChanged;

            if (_isDarkMode)
            {
                DialogTheme.ApplyTo(this, true);
                DialogTheme.SetDarkTitleBar(this, true);
                _terminalView.BackColor = Color.Black;
                _terminalView.ForeColor = Color.FromArgb(187, 187, 187);
            }
            else
            {
                _terminalView.BackColor = Color.Black;
                _terminalView.ForeColor = Color.FromArgb(187, 187, 187);
            }
        }

        public void AppendOutput(string text)
        {
            // Full-screen emulation renders terminal output through SetScreen snapshots.
            // This path is intentionally kept as a no-op for exceptional legacy calls.
        }

        public void SetScreen(TerminalScreenSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            if (_lastRenderHash == snapshot.Hash)
                return;

            _lastRenderHash = snapshot.Hash;
            _historyLength = Math.Max(0, snapshot.HistoryLength);
            _scrollbackOffset = Math.Clamp(snapshot.ScrollbackOffset, 0, _historyLength);
            _terminalView.SetSnapshot(snapshot);
            SyncHistoryScrollBar();
        }

        public void FocusTerminal()
        {
            _terminalView.Focus();
        }

        public void EnableDetachedReadOnlyMode(string? statusSuffix = null, string? detachedHistoryText = null)
        {
            _acceptHostInput = false;
            _allowTerminalActions = false;

            var suffix = string.IsNullOrWhiteSpace(statusSuffix)
                ? "Detached (read-only)"
                : statusSuffix.Trim();

            if (!Text.Contains(suffix, StringComparison.OrdinalIgnoreCase))
            {
                Text = $"{_baseTitle} - {suffix}";
            }

            if (!string.IsNullOrEmpty(detachedHistoryText))
            {
                _detachedHistoryLines = NormalizeDetachedHistoryLines(detachedHistoryText);
                _scrollbackOffset = 0;
                RenderDetachedHistorySnapshot();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FocusTerminal();
            EmitTerminalSizeChanged();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            InitializeSystemMenuCommands();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RememberLocation();
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmSysCommand)
            {
                var command = (int)(m.WParam.ToInt64() & 0xFFF0);
                switch (command)
                {
                    case SysMenuCommandCopyAllToClipboard:
                        CopyAllToClipboard();
                        m.Result = IntPtr.Zero;
                        return;
                    case SysMenuCommandClearScrollback:
                        ClearScrollback();
                        m.Result = IntPtr.Zero;
                        return;
                    case SysMenuCommandResetTerminal:
                        ResetTerminal();
                        m.Result = IntPtr.Zero;
                        return;
                }
            }

            base.WndProc(ref m);
        }

        private void InteractiveTerminalForm_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (!_acceptHostInput)
                return;

            if (char.IsControl(e.KeyChar))
                return;

            SnapToTailForInput();
            TextInput?.Invoke(this, e.KeyChar.ToString());
            e.Handled = true;
        }

        private void InteractiveTerminalForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (!_acceptHostInput)
                return;

            // Ctrl+letter combinations (for example Ctrl+C).
            if (e.Control && e.KeyCode >= Keys.A && e.KeyCode <= Keys.Z)
            {
                var consoleKey = (ConsoleKey)((int)ConsoleKey.A + ((int)e.KeyCode - (int)Keys.A));
                SnapToTailForInput();
                KeyInput?.Invoke(this, new TerminalKeyEventArgs
                {
                    ConsoleKey = consoleKey,
                    Modifiers = ConsoleModifiers.Control
                });
                e.SuppressKeyPress = true;
                return;
            }

            FunctionKey? functionKey = e.KeyCode switch
            {
                Keys.Enter => Rebex.TerminalEmulation.FunctionKey.Enter,
                Keys.Tab => Rebex.TerminalEmulation.FunctionKey.Tab,
                Keys.Back => Rebex.TerminalEmulation.FunctionKey.Backspace,
                Keys.Escape => Rebex.TerminalEmulation.FunctionKey.Escape,
                Keys.Up => Rebex.TerminalEmulation.FunctionKey.UpArrow,
                Keys.Down => Rebex.TerminalEmulation.FunctionKey.DownArrow,
                Keys.Left => Rebex.TerminalEmulation.FunctionKey.LeftArrow,
                Keys.Right => Rebex.TerminalEmulation.FunctionKey.RightArrow,
                Keys.Home => Rebex.TerminalEmulation.FunctionKey.Home,
                Keys.End => Rebex.TerminalEmulation.FunctionKey.End,
                Keys.PageUp => Rebex.TerminalEmulation.FunctionKey.PageUp,
                Keys.PageDown => Rebex.TerminalEmulation.FunctionKey.PageDown,
                Keys.Insert => Rebex.TerminalEmulation.FunctionKey.Insert,
                Keys.Delete => Rebex.TerminalEmulation.FunctionKey.Delete,
                _ => null
            };

            if (!functionKey.HasValue)
                return;

            SnapToTailForInput();
            var modifiers = ConsoleModifiers.None;
            if (e.Control) modifiers |= ConsoleModifiers.Control;
            if (e.Shift) modifiers |= ConsoleModifiers.Shift;
            if (e.Alt) modifiers |= ConsoleModifiers.Alt;

            KeyInput?.Invoke(this, new TerminalKeyEventArgs
            {
                FunctionKey = functionKey.Value,
                Modifiers = modifiers
            });
            e.SuppressKeyPress = true;
        }

        private void EmitTerminalSizeChanged()
        {
            var cellSize = _terminalView.CellSize;
            var columnWidth = Math.Max(1, cellSize.Width);
            var rowHeight = Math.Max(1, cellSize.Height);
            var columns = Math.Max(20, _terminalView.ClientSize.Width / columnWidth);
            var rows = Math.Max(5, _terminalView.ClientSize.Height / rowHeight);

            if (columns == _lastColumns && rows == _lastRows)
                return;

            _lastColumns = columns;
            _lastRows = rows;

            if (_detachedHistoryLines != null)
            {
                RenderDetachedHistorySnapshot();
                return;
            }

            TerminalSizeChanged?.Invoke(this, new TerminalSizeChangedEventArgs
            {
                Columns = columns,
                Rows = rows
            });
        }

        private void TerminalView_MouseWheel(object? sender, MouseEventArgs e)
        {
            var wheelNotches = e.Delta / SystemInformation.MouseWheelScrollDelta;
            if (wheelNotches == 0)
                return;

            var scrollLines = SystemInformation.MouseWheelScrollLines;
            if (scrollLines <= 0)
                scrollLines = 3;

            AdjustScrollbackOffset(wheelNotches * scrollLines);
        }

        private void TerminalView_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                PasteClipboardToHost();
            }
        }

        private void HistoryScrollBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_syncingScrollBar)
                return;

            var nextOffset = Math.Max(0, _historyLength - _historyScrollBar.Value);
            if (nextOffset == _scrollbackOffset)
                return;

            _scrollbackOffset = nextOffset;
            if (_detachedHistoryLines != null)
            {
                RenderDetachedHistorySnapshot();
                return;
            }

            // Force rerender request because viewport changed while terminal content may not.
            _lastRenderHash = int.MinValue;
        }

        private void AdjustScrollbackOffset(int deltaLines)
        {
            if (deltaLines == 0 || _historyLength <= 0)
                return;

            var nextOffset = Math.Clamp(_scrollbackOffset + deltaLines, 0, _historyLength);
            if (nextOffset == _scrollbackOffset)
                return;

            _scrollbackOffset = nextOffset;
            if (_detachedHistoryLines != null)
            {
                RenderDetachedHistorySnapshot();
                return;
            }

            _lastRenderHash = int.MinValue;
            SyncHistoryScrollBar();
        }

        private void SnapToTailForInput()
        {
            _terminalView.ClearSelection();

            if (_scrollbackOffset == 0)
                return;

            _scrollbackOffset = 0;
            _lastRenderHash = int.MinValue;
            SyncHistoryScrollBar();
        }

        private void FollowTailAndRefresh()
        {
            _terminalView.ClearSelection();
            _scrollbackOffset = 0;
            _historyLength = 0;
            _lastRenderHash = int.MinValue;
            SyncHistoryScrollBar();
            FocusTerminal();
        }

        private void PasteClipboardToHost()
        {
            if (!_acceptHostInput)
            {
                FocusTerminal();
                return;
            }

            var clipboardText = GetClipboardTextSafe();
            if (string.IsNullOrEmpty(clipboardText))
            {
                FocusTerminal();
                return;
            }

            SnapToTailForInput();
            TextInput?.Invoke(this, NormalizePasteText(clipboardText));
            FocusTerminal();
        }

        private static string GetClipboardTextSafe()
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizePasteText(string text)
        {
            return text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');
        }

        private void CopyAllToClipboard()
        {
            var text = CopyAllTextProvider?.Invoke();
            if (string.IsNullOrEmpty(text))
            {
                FocusTerminal();
                return;
            }

            try
            {
                Clipboard.SetText(text);
            }
            catch
            {
                // Ignore clipboard contention errors.
            }

            FocusTerminal();
        }

        private void ClearScrollback()
        {
            if (!_allowTerminalActions)
            {
                FocusTerminal();
                return;
            }

            try
            {
                ClearScrollbackAction?.Invoke();
            }
            finally
            {
                FollowTailAndRefresh();
            }
        }

        private void ResetTerminal()
        {
            if (!_allowTerminalActions)
            {
                FocusTerminal();
                return;
            }

            try
            {
                ResetTerminalAction?.Invoke();
            }
            finally
            {
                FollowTailAndRefresh();
            }
        }

        private void InitializeSystemMenuCommands()
        {
            if (!IsHandleCreated)
                return;

            var hMenu = GetSystemMenu(Handle, false);
            if (hMenu == IntPtr.Zero)
                return;

            _ = AppendMenu(hMenu, MfSeparator, 0, IntPtr.Zero);
            _ = AppendMenu(hMenu, MfString, (nuint)SysMenuCommandCopyAllToClipboard, "Copy All to Clipboard");
            _ = AppendMenu(hMenu, MfString, (nuint)SysMenuCommandClearScrollback, "Clear Scrollback");
            _ = AppendMenu(hMenu, MfString, (nuint)SysMenuCommandResetTerminal, "Reset Terminal");
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, IntPtr lpNewItem);

        private void SyncHistoryScrollBar()
        {
            _syncingScrollBar = true;
            try
            {
                var viewportRows = Math.Max(1, _lastRows);
                _historyScrollBar.Minimum = 0;
                _historyScrollBar.SmallChange = 1;
                _historyScrollBar.LargeChange = viewportRows;
                _historyScrollBar.Maximum = _historyLength + viewportRows - 1;
                _historyScrollBar.Enabled = _historyLength > 0;
                var value = Math.Clamp(_historyLength - _scrollbackOffset, 0, _historyLength);
                _historyScrollBar.Value = value;
            }
            finally
            {
                _syncingScrollBar = false;
            }
        }

        private void RenderDetachedHistorySnapshot()
        {
            if (_detachedHistoryLines == null)
                return;

            var rows = Math.Max(1, _lastRows);
            var columns = Math.Max(1, _lastColumns);
            var totalRows = Math.Max(rows, _detachedHistoryLines.Length);
            var historyLength = Math.Max(0, totalRows - rows);
            var appliedOffset = Math.Clamp(_scrollbackOffset, 0, historyLength);
            var startRow = Math.Max(0, totalRows - rows - appliedOffset);
            var count = rows * columns;
            var characters = new char[count];
            var foreColors = new int[count];
            var backColors = new int[count];
            Array.Fill(characters, ' ');

            var defaultFore = Color.FromArgb(187, 187, 187).ToArgb();
            var defaultBack = Color.Black.ToArgb();
            Array.Fill(foreColors, defaultFore);
            Array.Fill(backColors, defaultBack);

            var displayPadding = Math.Max(0, rows - _detachedHistoryLines.Length);
            var hash = new HashCode();
            hash.Add(columns);
            hash.Add(rows);
            hash.Add(historyLength);
            hash.Add(appliedOffset);

            for (var row = 0; row < rows; row++)
            {
                var sourceRow = startRow + row;
                if (sourceRow < 0 || sourceRow >= totalRows)
                    continue;

                var sourceLineIndex = sourceRow - displayPadding;
                if (sourceLineIndex < 0 || sourceLineIndex >= _detachedHistoryLines.Length)
                    continue;

                var line = _detachedHistoryLines[sourceLineIndex] ?? string.Empty;
                var copyLength = Math.Min(columns, line.Length);
                if (copyLength <= 0)
                    continue;

                line.AsSpan(0, copyLength).CopyTo(characters.AsSpan(row * columns, copyLength));
                hash.Add(line);
            }

            SetScreen(new TerminalScreenSnapshot
            {
                Columns = columns,
                Rows = rows,
                HistoryLength = historyLength,
                ScrollbackOffset = appliedOffset,
                Characters = characters,
                ForeColors = foreColors,
                BackColors = backColors,
                CursorColumn = -1,
                CursorRow = -1,
                CursorForeColor = defaultBack,
                CursorBackColor = defaultFore,
                Hash = hash.ToHashCode()
            });
        }

        private static string[] NormalizeDetachedHistoryLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            var normalized = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n');

            return normalized.Split('\n');
        }

        private void ApplyRememberedLocation()
        {
            Point? rememberedLocation;
            lock (_placementLock)
            {
                rememberedLocation = _lastKnownLocation;
            }

            if (!rememberedLocation.HasValue)
                return;

            StartPosition = FormStartPosition.Manual;
            Location = ClampLocationToVisibleArea(rememberedLocation.Value, Size);
        }

        private void RememberLocation()
        {
            if (IsDisposed)
                return;

            var bounds = WindowState == FormWindowState.Normal
                ? Bounds
                : RestoreBounds;

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var clampedLocation = ClampLocationToVisibleArea(bounds.Location, bounds.Size);
            lock (_placementLock)
            {
                _lastKnownLocation = clampedLocation;
            }
        }

        private static Point ClampLocationToVisibleArea(Point location, Size size)
        {
            var targetBounds = new Rectangle(location, size);
            var workingArea = Screen.FromRectangle(targetBounds).WorkingArea;

            var maxX = Math.Max(workingArea.Left, workingArea.Right - size.Width);
            var maxY = Math.Max(workingArea.Top, workingArea.Bottom - size.Height);

            var x = Math.Max(workingArea.Left, Math.Min(location.X, maxX));
            var y = Math.Max(workingArea.Top, Math.Min(location.Y, maxY));
            return new Point(x, y);
        }
    }
}
