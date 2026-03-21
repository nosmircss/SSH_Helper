using System.Drawing;
using System.Windows.Forms;

namespace SSH_Helper.UI
{
    internal sealed class PresetTabHeaderStrip : Control
    {
        private const int TabHorizontalPadding = 12;
        private const int MinTabWidth = 54;

        private readonly string[] _tabTexts = { "Presets", "Favorites" };
        private int _selectedIndex;
        private int _hoverIndex = -1;

        public PresetTabHeaderStrip()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

            DoubleBuffered = true;
            TabStop = false;
            Height = 24;
            Cursor = Cursors.Hand;

            HeaderBackgroundColor = Color.FromArgb(37, 37, 38);
            SelectedTabBackgroundColor = Color.FromArgb(30, 30, 30);
            HoverTabBackgroundColor = Color.FromArgb(45, 45, 46);
            SelectedTextColor = Color.White;
            UnselectedTextColor = Color.FromArgb(153, 153, 153);
            SelectedAccentColor = Color.FromArgb(0, 122, 204);
            BorderColor = Color.FromArgb(48, 48, 48);
        }

        public event EventHandler? SelectedIndexChanged;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                var normalized = Math.Clamp(value, 0, _tabTexts.Length - 1);
                if (_selectedIndex == normalized)
                {
                    return;
                }

                _selectedIndex = normalized;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Color HeaderBackgroundColor { get; set; }

        public Color SelectedTabBackgroundColor { get; set; }

        public Color HoverTabBackgroundColor { get; set; }

        public Color SelectedTextColor { get; set; }

        public Color UnselectedTextColor { get; set; }

        public Color SelectedAccentColor { get; set; }

        public Color BorderColor { get; set; }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var hoverIndex = HitTest(e.Location);
            if (_hoverIndex != hoverIndex)
            {
                _hoverIndex = hoverIndex;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            var clickedIndex = HitTest(e.Location);
            if (clickedIndex >= 0)
            {
                SelectedIndex = clickedIndex;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(HeaderBackgroundColor);

            using var borderPen = new Pen(BorderColor);
            using var accentBrush = new SolidBrush(SelectedAccentColor);

            var x = 0;
            for (int i = 0; i < _tabTexts.Length; i++)
            {
                var tabRect = GetTabBounds(i, x);
                x = tabRect.Right;

                var selected = i == _selectedIndex;
                var hovered = i == _hoverIndex;

                var background = selected
                    ? SelectedTabBackgroundColor
                    : hovered
                        ? HoverTabBackgroundColor
                        : HeaderBackgroundColor;

                using (var backgroundBrush = new SolidBrush(background))
                {
                    e.Graphics.FillRectangle(backgroundBrush, tabRect);
                }

                if (selected)
                {
                    e.Graphics.FillRectangle(accentBrush, tabRect.Left, tabRect.Top, tabRect.Width, 2);
                    e.Graphics.DrawRectangle(borderPen, tabRect.Left, tabRect.Top, tabRect.Width - 1, tabRect.Height - 1);
                }

                var textColor = selected ? SelectedTextColor : UnselectedTextColor;
                TextRenderer.DrawText(
                    e.Graphics,
                    _tabTexts[i],
                    Font,
                    tabRect,
                    textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }

            e.Graphics.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
        }

        private Rectangle GetTabBounds(int index, int x)
        {
            var textWidth = TextRenderer.MeasureText(_tabTexts[index], Font, Size.Empty, TextFormatFlags.NoPadding).Width;
            var tabWidth = Math.Max(MinTabWidth, textWidth + (TabHorizontalPadding * 2));
            return new Rectangle(x, 0, tabWidth, Math.Max(1, Height - 1));
        }

        private int HitTest(Point point)
        {
            var x = 0;
            for (int i = 0; i < _tabTexts.Length; i++)
            {
                var tabRect = GetTabBounds(i, x);
                if (tabRect.Contains(point))
                {
                    return i;
                }

                x = tabRect.Right;
            }

            return -1;
        }
    }
}
