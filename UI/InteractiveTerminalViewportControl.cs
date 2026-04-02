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
        // Selection points are stored in buffer coordinates (column + absolute buffer row).
        private Point _selectionAnchor = InvalidCell;
        private Point _selectionCaret = InvalidCell;
        private int _selectionColumns = -1;
        private bool _isSelecting;
        private bool _selectionDragged;
        private readonly Dictionary<int, SolidBrush> _brushCache = new();

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

        public bool HasSelection => TryGetSelectionRange(out _);

        public bool HasSnapshot => _snapshot != null;

        public Func<TerminalBufferSelection, string?>? SelectionTextProvider { get; set; }

        public void SetSnapshot(TerminalScreenSnapshot snapshot)
        {
            var previousSnapshot = _snapshot;
            _snapshot = snapshot;
            NormalizeSelectionBounds(snapshot);
            InvalidateSnapshotDelta(previousSnapshot, snapshot);
        }

        private void InvalidateSnapshotDelta(TerminalScreenSnapshot? previousSnapshot, TerminalScreenSnapshot currentSnapshot)
        {
            if (previousSnapshot == null ||
                previousSnapshot.Columns != currentSnapshot.Columns ||
                previousSnapshot.Rows != currentSnapshot.Rows)
            {
                Invalidate();
                return;
            }

            var rows = currentSnapshot.Rows;
            var previousRowHashes = previousSnapshot.RowHashes;
            var currentRowHashes = currentSnapshot.RowHashes;
            if (previousRowHashes.Length != rows || currentRowHashes.Length != rows)
            {
                Invalidate();
                return;
            }

            var rowScanCount = rows;
            var minDirtyRow = int.MaxValue;
            var maxDirtyRow = -1;

            for (var row = 0; row < rowScanCount; row++)
            {
                if (previousRowHashes[row] == currentRowHashes[row])
                    continue;

                minDirtyRow = Math.Min(minDirtyRow, row);
                maxDirtyRow = Math.Max(maxDirtyRow, row);
            }

            if (previousSnapshot.CursorRow >= 0 && previousSnapshot.CursorRow < rows)
            {
                minDirtyRow = Math.Min(minDirtyRow, previousSnapshot.CursorRow);
                maxDirtyRow = Math.Max(maxDirtyRow, previousSnapshot.CursorRow);
            }

            if (currentSnapshot.CursorRow >= 0 && currentSnapshot.CursorRow < rows)
            {
                minDirtyRow = Math.Min(minDirtyRow, currentSnapshot.CursorRow);
                maxDirtyRow = Math.Max(maxDirtyRow, currentSnapshot.CursorRow);
            }

            if (maxDirtyRow < minDirtyRow)
                return;

            var cellHeight = Math.Max(1, EnsureCellSize().Height);
            var y = minDirtyRow * cellHeight;
            var height = Math.Max(1, (maxDirtyRow - minDirtyRow + 1) * cellHeight);
            Invalidate(new Rectangle(0, y, Math.Max(1, ClientSize.Width), height));
        }

        public void ClearSelection()
        {
            if (!HasSelectionState())
                return;

            ResetSelectionState();
            Invalidate();
        }

        public void SelectAllVisible()
        {
            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return;

            var topBufferRow = GetViewportTopBufferRow(snapshot);
            _selectionAnchor = new Point(0, topBufferRow);
            _selectionCaret = new Point(snapshot.Columns - 1, topBufferRow + snapshot.Rows - 1);
            _selectionColumns = snapshot.Columns;
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
            if (snapshot == null || !TryGetSelectionRange(snapshot, out var range))
                return string.Empty;

            if (SelectionTextProvider != null)
            {
                try
                {
                    var providedText = SelectionTextProvider.Invoke(range);
                    if (providedText != null)
                        return providedText;
                }
                catch
                {
                    // Fall back to viewport-only extraction if provider fails.
                }
            }

            return GetSelectedTextFromViewportSnapshot(snapshot, range);
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

            var bufferCell = GetBufferCellFromPoint(e.Location, snapshot);
            if (bufferCell == InvalidCell)
                return;

            _selectionAnchor = bufferCell;
            _selectionCaret = bufferCell;
            _selectionColumns = snapshot.Columns;
            _selectionDragged = false;
            _isSelecting = true;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left)
                return;

            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return;

            var viewportCell = GetViewportCellFromPoint(e.Location, snapshot);
            if (!TrySelectWord(viewportCell, snapshot))
            {
                Capture = false;
                ClearSelection();
                return;
            }

            _isSelecting = false;
            Capture = false;
            Invalidate();
            CopySelectionToClipboard();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isSelecting)
                return;

            var snapshot = _snapshot;
            if (snapshot == null)
                return;

            var bufferCell = GetBufferCellFromPoint(e.Location, snapshot);
            if (bufferCell == InvalidCell || bufferCell == _selectionCaret)
                return;

            _selectionCaret = bufferCell;
            _selectionColumns = snapshot.Columns;
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
                foreach (var brush in _brushCache.Values)
                {
                    brush.Dispose();
                }

                _brushCache.Clear();
            }

            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            FillBackground(e.Graphics, BackColor.ToArgb(), e.ClipRectangle);

            var snapshot = _snapshot;
            if (snapshot == null || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return;

            var hasSelection = TryGetSelectionBounds(snapshot, out var selectedStartIndex, out var selectedEndIndex);
            var cellSize = EnsureCellSize();
            var cellWidth = Math.Max(1, cellSize.Width);
            var cellHeight = Math.Max(1, cellSize.Height);
            var columns = snapshot.Columns;
            var rows = snapshot.Rows;
            var viewportTopBufferRow = GetViewportTopBufferRow(snapshot);
            var characters = snapshot.Characters;
            var foreColors = snapshot.ForeColors;
            var backColors = snapshot.BackColors;
            var clip = e.ClipRectangle;
            var startRow = Math.Clamp(clip.Top / cellHeight, 0, rows - 1);
            var endRow = Math.Clamp(Math.Max(0, clip.Bottom - 1) / cellHeight, 0, rows - 1);
            var startColumn = Math.Clamp(clip.Left / cellWidth, 0, columns - 1);
            var endColumn = Math.Clamp(Math.Max(0, clip.Right - 1) / cellWidth, 0, columns - 1);

            for (var row = startRow; row <= endRow; row++)
            {
                var rowOffset = row * columns;
                var bufferRow = viewportTopBufferRow + row;
                var y = row * cellHeight;
                var column = startColumn;

                while (column <= endColumn)
                {
                    var runStart = column;
                    var runBufferIndex = ((long)bufferRow * columns) + column;
                    var runScreenIndex = rowOffset + column;
                    var runSelected = hasSelection &&
                                      runBufferIndex >= selectedStartIndex &&
                                      runBufferIndex <= selectedEndIndex;
                    var runFore = runSelected ? SelectionForeColorArgb : foreColors[runScreenIndex];
                    var runBack = runSelected ? SelectionBackColorArgb : backColors[runScreenIndex];
                    column++;

                    while (column <= endColumn)
                    {
                        runBufferIndex = ((long)bufferRow * columns) + column;
                        runScreenIndex = rowOffset + column;
                        var currentSelected = hasSelection &&
                                              runBufferIndex >= selectedStartIndex &&
                                              runBufferIndex <= selectedEndIndex;
                        var currentFore = currentSelected ? SelectionForeColorArgb : foreColors[runScreenIndex];
                        var currentBack = currentSelected ? SelectionBackColorArgb : backColors[runScreenIndex];
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
                    FillBackground(e.Graphics, runBack, rect);

                    if (!AllSpaces(characters, rowOffset + runStart, runLength))
                    {
                        DrawRunText(
                            e.Graphics,
                            characters.AsSpan(rowOffset + runStart, runLength),
                            rect,
                            runFore,
                            runBack);
                    }
                }
            }

            DrawCursor(e.Graphics, snapshot, cellWidth, cellHeight);
        }

        private void FillBackground(Graphics graphics, int backColorArgb, Rectangle rect)
        {
            if (!_brushCache.TryGetValue(backColorArgb, out var brush))
            {
                brush = new SolidBrush(Color.FromArgb(backColorArgb));
                _brushCache[backColorArgb] = brush;
            }

            graphics.FillRectangle(brush, rect);
        }

        private void DrawCursor(
            Graphics graphics,
            TerminalScreenSnapshot snapshot,
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

            FillBackground(graphics, snapshot.CursorBackColor, rect);

            var character = snapshot.Characters[cellIndex];
            if (character != ' ')
            {
                Span<char> cursorText = stackalloc char[1];
                cursorText[0] = character;
                DrawRunText(
                    graphics,
                    cursorText,
                    rect,
                    snapshot.CursorForeColor,
                    snapshot.CursorBackColor);
            }
        }

        private void DrawRunText(
            Graphics graphics,
            ReadOnlySpan<char> text,
            Rectangle rect,
            int foreColorArgb,
            int backColorArgb)
        {
            var foreColor = Color.FromArgb(foreColorArgb);
            var backColor = Color.FromArgb(backColorArgb);
#if NET8_0_OR_GREATER
            TextRenderer.DrawText(
                graphics,
                text,
                Font,
                rect,
                foreColor,
                backColor,
                DrawTextFlags);
#else
            var textValue = text.ToString();
            TextRenderer.DrawText(
                graphics,
                textValue,
                Font,
                rect,
                foreColor,
                backColor,
                DrawTextFlags);
#endif
        }

        private void NormalizeSelectionBounds(TerminalScreenSnapshot snapshot)
        {
            if (!HasSelectionState())
                return;

            if (snapshot.Columns <= 0 || snapshot.Rows <= 0)
            {
                ResetSelectionState();
                return;
            }

            if (_selectionColumns > 0 && _selectionColumns != snapshot.Columns)
            {
                // Buffer index math changes with terminal width; drop stale selection.
                ResetSelectionState();
                return;
            }

            _selectionColumns = snapshot.Columns;
            _selectionAnchor = ClampBufferCellToSnapshot(_selectionAnchor, snapshot);
            _selectionCaret = ClampBufferCellToSnapshot(_selectionCaret, snapshot);
        }

        private bool TryGetSelectionRange(out TerminalBufferSelection range)
        {
            var snapshot = _snapshot;
            if (snapshot == null)
            {
                range = default;
                return false;
            }

            return TryGetSelectionRange(snapshot, out range);
        }

        private bool TryGetSelectionRange(TerminalScreenSnapshot snapshot, out TerminalBufferSelection range)
        {
            range = default;
            if (snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return false;

            if (_selectionAnchor == InvalidCell || _selectionCaret == InvalidCell)
                return false;

            if (_selectionColumns > 0 && _selectionColumns != snapshot.Columns)
                return false;

            var anchor = ClampBufferCellToSnapshot(_selectionAnchor, snapshot);
            var caret = ClampBufferCellToSnapshot(_selectionCaret, snapshot);
            if (anchor == InvalidCell || caret == InvalidCell)
                return false;

            var anchorIndex = ToBufferLinearIndex(anchor, snapshot.Columns);
            var caretIndex = ToBufferLinearIndex(caret, snapshot.Columns);
            if (anchorIndex == caretIndex && !_selectionDragged)
                return false;

            var startCell = anchorIndex <= caretIndex ? anchor : caret;
            var endCell = anchorIndex <= caretIndex ? caret : anchor;
            range = new TerminalBufferSelection(
                startCell.X,
                startCell.Y,
                endCell.X,
                endCell.Y,
                snapshot.Columns);
            return true;
        }

        private bool TryGetSelectionBounds(
            TerminalScreenSnapshot snapshot,
            out long startIndex,
            out long endIndex)
        {
            startIndex = -1;
            endIndex = -1;
            if (!TryGetSelectionRange(snapshot, out var range))
                return false;

            startIndex = ((long)range.StartBufferRow * range.Columns) + range.StartColumn;
            endIndex = ((long)range.EndBufferRow * range.Columns) + range.EndColumn;
            return true;
        }

        private static long ToBufferLinearIndex(Point bufferCell, int columns)
        {
            return ((long)bufferCell.Y * columns) + bufferCell.X;
        }

        private Point GetViewportCellFromPoint(Point location, TerminalScreenSnapshot snapshot)
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

        private Point GetBufferCellFromPoint(Point location, TerminalScreenSnapshot snapshot)
        {
            var viewportCell = GetViewportCellFromPoint(location, snapshot);
            if (viewportCell == InvalidCell)
                return InvalidCell;

            var topBufferRow = GetViewportTopBufferRow(snapshot);
            return new Point(viewportCell.X, topBufferRow + viewportCell.Y);
        }

        private static Point ClampViewportCellToSnapshot(Point cell, TerminalScreenSnapshot snapshot)
        {
            if (cell == InvalidCell || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return InvalidCell;

            var maxColumn = Math.Max(0, snapshot.Columns - 1);
            var maxRow = Math.Max(0, snapshot.Rows - 1);
            return new Point(Math.Clamp(cell.X, 0, maxColumn), Math.Clamp(cell.Y, 0, maxRow));
        }

        private static Point ClampBufferCellToSnapshot(Point cell, TerminalScreenSnapshot snapshot)
        {
            if (cell == InvalidCell || snapshot.Columns <= 0 || snapshot.Rows <= 0)
                return InvalidCell;

            var maxColumn = Math.Max(0, snapshot.Columns - 1);
            var totalRows = Math.Max(1, snapshot.HistoryLength + snapshot.Rows);
            var maxBufferRow = Math.Max(0, totalRows - 1);
            return new Point(Math.Clamp(cell.X, 0, maxColumn), Math.Clamp(cell.Y, 0, maxBufferRow));
        }

        private static int GetViewportTopBufferRow(TerminalScreenSnapshot snapshot)
        {
            var totalRows = Math.Max(1, snapshot.HistoryLength + snapshot.Rows);
            var effectiveOffset = Math.Clamp(snapshot.EffectiveScrollOffset, 0, snapshot.HistoryLength);
            var topBufferRow = snapshot.HistoryLength - effectiveOffset;
            return Math.Clamp(topBufferRow, 0, Math.Max(0, totalRows - 1));
        }

        private bool TrySelectWord(Point viewportCell, TerminalScreenSnapshot snapshot)
        {
            if (viewportCell == InvalidCell)
                return false;

            var clampedViewportCell = ClampViewportCellToSnapshot(viewportCell, snapshot);
            if (clampedViewportCell == InvalidCell)
                return false;

            var columns = snapshot.Columns;
            var rowOffset = clampedViewportCell.Y * columns;
            var characters = snapshot.Characters;
            var startColumn = clampedViewportCell.X;
            var endColumn = clampedViewportCell.X;

            if (rowOffset + startColumn < 0 || rowOffset + startColumn >= characters.Length)
                return false;

            if (!IsWordCharacter(characters[rowOffset + startColumn]))
                return false;

            while (startColumn > 0 && IsWordCharacter(characters[rowOffset + startColumn - 1]))
            {
                startColumn--;
            }

            while (endColumn < columns - 1 && IsWordCharacter(characters[rowOffset + endColumn + 1]))
            {
                endColumn++;
            }

            var bufferRow = GetViewportTopBufferRow(snapshot) + clampedViewportCell.Y;
            _selectionAnchor = new Point(startColumn, bufferRow);
            _selectionCaret = new Point(endColumn, bufferRow);
            _selectionColumns = snapshot.Columns;
            _selectionDragged = true;
            return true;
        }

        private static bool IsWordCharacter(char value)
        {
            return value != '\0' && !char.IsWhiteSpace(value);
        }

        private static string GetSelectedTextFromViewportSnapshot(
            TerminalScreenSnapshot snapshot,
            TerminalBufferSelection range)
        {
            var columns = snapshot.Columns;
            if (columns <= 0)
                return string.Empty;

            var viewportTopBufferRow = GetViewportTopBufferRow(snapshot);
            var viewportBottomBufferRow = viewportTopBufferRow + Math.Max(0, snapshot.Rows - 1);
            var startBufferRow = Math.Max(range.StartBufferRow, viewportTopBufferRow);
            var endBufferRow = Math.Min(range.EndBufferRow, viewportBottomBufferRow);
            if (endBufferRow < startBufferRow)
                return string.Empty;

            var characters = snapshot.Characters;
            var lines = new List<string>(endBufferRow - startBufferRow + 1);
            for (var bufferRow = startBufferRow; bufferRow <= endBufferRow; bufferRow++)
            {
                var viewportRow = bufferRow - viewportTopBufferRow;
                if (viewportRow < 0 || viewportRow >= snapshot.Rows)
                    continue;

                var lineStartColumn = bufferRow == range.StartBufferRow ? range.StartColumn : 0;
                var lineEndColumn = bufferRow == range.EndBufferRow ? range.EndColumn : columns - 1;
                lineStartColumn = Math.Clamp(lineStartColumn, 0, columns - 1);
                lineEndColumn = Math.Clamp(lineEndColumn, 0, columns - 1);
                if (lineEndColumn < lineStartColumn)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                var length = lineEndColumn - lineStartColumn + 1;
                var lineStartIndex = (viewportRow * columns) + lineStartColumn;
                var line = new string(characters, lineStartIndex, length).TrimEnd(' ');
                lines.Add(line);
            }

            return string.Join(Environment.NewLine, lines);
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

        private bool HasSelectionState()
        {
            return _selectionAnchor != InvalidCell ||
                   _selectionCaret != InvalidCell ||
                   _selectionDragged;
        }

        private void ResetSelectionState()
        {
            _selectionAnchor = InvalidCell;
            _selectionCaret = InvalidCell;
            _selectionColumns = -1;
            _selectionDragged = false;
            _isSelecting = false;
        }
    }
}
