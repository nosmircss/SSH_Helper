using System.Drawing;
using System.Windows.Forms;
using SSH_Helper.Forms;

namespace SSH_Helper.UI
{
    internal sealed class InteractiveTerminalViewportControl : Control
    {
        private const TextFormatFlags DrawTextFlags =
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine |
            TextFormatFlags.Left |
            TextFormatFlags.Top;

        private const TextFormatFlags CellMeasureFlags =
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine;

        private static readonly Point InvalidCell = new(-1, -1);
        private const int CellWidthSampleLength = 64;
        private const int SelectionBackColorArgb = unchecked((int)0xFF2A5CAA);
        private const int SelectionForeColorArgb = unchecked((int)0xFFFFFFFF);

        private readonly System.Windows.Forms.Timer _cursorBlinkTimer;
        private TerminalScreenSnapshot? _snapshot;
        private bool _isCursorVisible = true;
        private Size _cellSize;
        private bool _cellSizeValid;
        private Point _selectionAnchor = InvalidCell;
        private Point _selectionCaret = InvalidCell;
        private bool _isSelecting;
        private bool _selectionDragged;

        public InteractiveTerminalViewportControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);
            DoubleBuffered = true;
            TabStop = true;
            Cursor = Cursors.Arrow;

            _cursorBlinkTimer = new System.Windows.Forms.Timer { Interval = 530 };
            _cursorBlinkTimer.Tick += (_, _) =>
            {
                if (!Focused)
                    return;

                _isCursorVisible = !_isCursorVisible;
                InvalidateCursorCell();
            };

            GotFocus += (_, _) =>
            {
                _isCursorVisible = true;
                _cursorBlinkTimer.Start();
                InvalidateCursorCell();
            };

            LostFocus += (_, _) =>
            {
                _cursorBlinkTimer.Stop();
                _isCursorVisible = true;
                InvalidateCursorCell();
            };

            FontChanged += (_, _) =>
            {
                _cellSizeValid = false;
                Invalidate();
            };
        }

        public Size CellSize => EnsureCellSize();

        public bool HasSelection => TryGetSelectionBounds(out _, out _);

        public bool HasSnapshot => _snapshot != null;

        public void SetSnapshot(TerminalScreenSnapshot snapshot)
        {
            _snapshot = snapshot;
            NormalizeSelectionBounds(snapshot);
            Invalidate();
        }

        public void ClearSelection()
        {
            if (_selectionAnchor == InvalidCell && _selectionCaret == InvalidCell && !_selectionDragged)
                return;

            _selectionAnchor = InvalidCell;
            _selectionCaret = InvalidCell;
            _selectionDragged = false;
            _isSelecting = false;
            Invalidate();
        }

        public void SelectAllVisible()
        {
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return;

            _selectionAnchor = new Point(0, 0);
            _selectionCaret = new Point(snapshot.Columns - 1, snapshot.Rows - 1);
            _selectionDragged = true;
            _isSelecting = false;
            Invalidate();
        }

        public bool CopySelectionToClipboard()
        {
            var text = GetSelectedText();
            if (string.IsNullOrEmpty(text))
                return false;

            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetSelectedText()
        {
            var snapshot = _snapshot;
            if (snapshot == null || !TryGetSelectionBounds(out var startIndex, out var endIndex))
                return string.Empty;

            var columns = snapshot.Columns;
            if (columns <= 0)
                return string.Empty;

            var characters = snapshot.Characters;
            var startRow = startIndex / columns;
            var startColumn = startIndex % columns;
            var endRow = endIndex / columns;
            var endColumn = endIndex % columns;
            var lines = new List<string>(endRow - startRow + 1);

            for (var row = startRow; row <= endRow; row++)
            {
                var lineStartColumn = row == startRow ? startColumn : 0;
                var lineEndColumn = row == endRow ? endColumn : columns - 1;
                var length = lineEndColumn - lineStartColumn + 1;
                if (length <= 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                var lineStartIndex = row * columns + lineStartColumn;
                var line = new string(characters, lineStartIndex, length).TrimEnd(' ');
                lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            var key = keyData & Keys.KeyCode;
            if (key is Keys.Up or Keys.Down or Keys.Left or Keys.Right or
                Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or
                Keys.Insert or Keys.Delete)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button != MouseButtons.Left)
                return;

            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return;

            var cell = GetCellFromPoint(e.Location, snapshot);
            if (cell == InvalidCell)
                return;

            _selectionAnchor = cell;
            _selectionCaret = cell;
            _selectionDragged = false;
            _isSelecting = true;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isSelecting)
                return;

            var snapshot = _snapshot;
            if (snapshot == null)
                return;

            var cell = GetCellFromPoint(e.Location, snapshot);
            if (cell == InvalidCell || cell == _selectionCaret)
                return;

            _selectionCaret = cell;
            _selectionDragged = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            if (!_isSelecting)
                return;

            _isSelecting = false;
            Capture = false;

            if (!_selectionDragged)
            {
                ClearSelection();
                return;
            }

            Invalidate();
            CopySelectionToClipboard();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_isSelecting)
            {
                // Continue tracking while dragging outside bounds.
                Focus();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cursorBlinkTimer.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);

            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return;

            var hasSelection = TryGetSelectionBounds(out var selectedStartIndex, out var selectedEndIndex);
            var cellSize = EnsureCellSize();
            var cellWidth = Math.Max(1, cellSize.Width);
            var cellHeight = Math.Max(1, cellSize.Height);
            var columns = snapshot.Columns;
            var rows = snapshot.Rows;
            var characters = snapshot.Characters;
            var foreColors = snapshot.ForeColors;
            var backColors = snapshot.BackColors;
            var brushCache = new Dictionary<int, SolidBrush>();

            try
            {
                for (var row = 0; row < rows; row++)
                {
                    var rowOffset = row * columns;
                    var y = row * cellHeight;
                    var column = 0;

                    while (column < columns)
                    {
                        var runStart = column;
                        var runIndex = rowOffset + column;
                        var runSelected = hasSelection && runIndex >= selectedStartIndex && runIndex <= selectedEndIndex;
                        var runFore = runSelected ? SelectionForeColorArgb : foreColors[runIndex];
                        var runBack = runSelected ? SelectionBackColorArgb : backColors[runIndex];
                        column++;

                        while (column < columns)
                        {
                            runIndex = rowOffset + column;
                            var currentSelected = hasSelection && runIndex >= selectedStartIndex && runIndex <= selectedEndIndex;
                            var currentFore = currentSelected ? SelectionForeColorArgb : foreColors[runIndex];
                            var currentBack = currentSelected ? SelectionBackColorArgb : backColors[runIndex];

                            if (currentFore != runFore || currentBack != runBack)
                                break;

                            column++;
                        }

                        var runLength = column - runStart;
                        if (runLength <= 0)
                            continue;

                        var x = runStart * cellWidth;
                        var width = runLength * cellWidth;
                        var rect = new Rectangle(x, y, width, cellHeight);
                        FillBackground(e.Graphics, brushCache, runBack, rect);

                        if (!AllSpaces(characters, rowOffset + runStart, runLength))
                        {
                            var text = new string(characters, rowOffset + runStart, runLength);
                            TextRenderer.DrawText(
                                e.Graphics,
                                text,
                                Font,
                                rect,
                                Color.FromArgb(runFore),
                                Color.FromArgb(runBack),
                                DrawTextFlags);
                        }
                    }
                }

                DrawCursor(e.Graphics, snapshot, brushCache, cellWidth, cellHeight);
            }
            finally
            {
                foreach (var brush in brushCache.Values)
                {
                    brush.Dispose();
                }
            }
        }

        private static void FillBackground(Graphics graphics, Dictionary<int, SolidBrush> brushCache, int backColorArgb, Rectangle rect)
        {
            if (!brushCache.TryGetValue(backColorArgb, out var brush))
            {
                brush = new SolidBrush(Color.FromArgb(backColorArgb));
                brushCache[backColorArgb] = brush;
            }

            graphics.FillRectangle(brush, rect);
        }

        private void DrawCursor(
            Graphics graphics,
            TerminalScreenSnapshot snapshot,
            Dictionary<int, SolidBrush> brushCache,
            int cellWidth,
            int cellHeight)
        {
            if (snapshot.CursorColumn < 0 || snapshot.CursorRow < 0 ||
                snapshot.CursorColumn >= snapshot.Columns || snapshot.CursorRow >= snapshot.Rows)
            {
                return;
            }

            if (Focused && !_isCursorVisible)
                return;

            var cellIndex = snapshot.CursorRow * snapshot.Columns + snapshot.CursorColumn;
            if (cellIndex < 0 || cellIndex >= snapshot.Characters.Length)
                return;

            var rect = new Rectangle(
                snapshot.CursorColumn * cellWidth,
                snapshot.CursorRow * cellHeight,
                cellWidth,
                cellHeight);

            FillBackground(graphics, brushCache, snapshot.CursorBackColor, rect);

            var character = snapshot.Characters[cellIndex];
            if (character != ' ')
            {
                TextRenderer.DrawText(
                    graphics,
                    new string(character, 1),
                    Font,
                    rect,
                    Color.FromArgb(snapshot.CursorForeColor),
                    Color.FromArgb(snapshot.CursorBackColor),
                    DrawTextFlags);
            }
        }

        private void NormalizeSelectionBounds(TerminalScreenSnapshot snapshot)
        {
            if (_selectionAnchor == InvalidCell && _selectionCaret == InvalidCell)
                return;

            if (snapshot.Columns <= 0 || snapshot.Rows <= 0)
            {
                ClearSelection();
                return;
            }

            _selectionAnchor = ClampCellToSnapshot(_selectionAnchor, snapshot);
            _selectionCaret = ClampCellToSnapshot(_selectionCaret, snapshot);
        }

        private bool TryGetSelectionBounds(out int startIndex, out int endIndex)
        {
            startIndex = -1;
            endIndex = -1;

            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return false;

            if (_selectionAnchor == InvalidCell || _selectionCaret == InvalidCell)
                return false;

            var anchor = ClampCellToSnapshot(_selectionAnchor, snapshot);
            var caret = ClampCellToSnapshot(_selectionCaret, snapshot);
            var anchorIndex = anchor.Y * snapshot.Columns + anchor.X;
            var caretIndex = caret.Y * snapshot.Columns + caret.X;

            if (anchorIndex == caretIndex && !_selectionDragged)
                return false;

            if (anchorIndex <= caretIndex)
            {
                startIndex = anchorIndex;
                endIndex = caretIndex;
            }
            else
            {
                startIndex = caretIndex;
                endIndex = anchorIndex;
            }

            return true;
        }

        private Point GetCellFromPoint(Point location, TerminalScreenSnapshot snapshot)
        {
            var cellSize = EnsureCellSize();
            var cellWidth = Math.Max(1, cellSize.Width);
            var cellHeight = Math.Max(1, cellSize.Height);

            var maxColumn = Math.Max(0, snapshot.Columns - 1);
            var maxRow = Math.Max(0, snapshot.Rows - 1);
            var column = Math.Clamp(location.X / cellWidth, 0, maxColumn);
            var row = Math.Clamp(location.Y / cellHeight, 0, maxRow);
            return new Point(column, row);
        }

        private static Point ClampCellToSnapshot(Point cell, TerminalScreenSnapshot snapshot)
        {
            if (cell == InvalidCell || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return InvalidCell;

            var maxColumn = Math.Max(0, snapshot.Columns - 1);
            var maxRow = Math.Max(0, snapshot.Rows - 1);
            return new Point(Math.Clamp(cell.X, 0, maxColumn), Math.Clamp(cell.Y, 0, maxRow));
        }

        private static bool AllSpaces(char[] characters, int start, int length)
        {
            for (var i = 0; i < length; i++)
            {
                if (characters[start + i] != ' ')
                    return false;
            }

            return true;
        }

        private Size EnsureCellSize()
        {
            if (_cellSizeValid)
                return _cellSize;

            var widthSample = new string('W', CellWidthSampleLength);
            var widthSampleSize = TextRenderer.MeasureText(widthSample, Font, Size.Empty, CellMeasureFlags);
            var cellWidth = (int)Math.Round(widthSampleSize.Width / (double)CellWidthSampleLength, MidpointRounding.AwayFromZero);
            if (cellWidth <= 0)
            {
                cellWidth = 1;
            }

            var lineHeight = Font.Height;
            if (IsHandleCreated)
            {
                using var graphics = CreateGraphics();
                lineHeight = (int)Math.Ceiling(Font.GetHeight(graphics));
            }

            if (lineHeight <= 0)
            {
                lineHeight = 1;
            }

            _cellSize = new Size(cellWidth, lineHeight);
            _cellSizeValid = true;
            return _cellSize;
        }

        private void InvalidateCursorCell()
        {
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.CursorColumn < 0 || snapshot.CursorRow < 0)
                return;

            var cellSize = EnsureCellSize();
            var rect = new Rectangle(
                snapshot.CursorColumn * Math.Max(1, cellSize.Width),
                snapshot.CursorRow * Math.Max(1, cellSize.Height),
                Math.Max(1, cellSize.Width),
                Math.Max(1, cellSize.Height));
            Invalidate(rect);
        }
    }
}
