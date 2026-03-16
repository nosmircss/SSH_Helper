using System.Drawing;
using System.Windows.Forms;

namespace SSH_Helper.UI
{
    internal static class HistoryListLayout
    {
        internal const int HorizontalPadding = 4;
        internal const int VerticalPadding = 4;
        internal const int MaxVisibleLines = 3;

        private const TextFormatFlags TextMeasurementFlags =
            TextFormatFlags.WordBreak |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.TextBoxControl;

        internal static TextFormatFlags TextDrawFlags =>
            TextMeasurementFlags | TextFormatFlags.PreserveGraphicsTranslateTransform;

        internal static int GetMinimumItemHeight(Font? font)
        {
            return GetLineHeight(font) + (VerticalPadding * 2);
        }

        internal static int GetMaximumItemHeight(Font? font)
        {
            return (GetLineHeight(font) * MaxVisibleLines) + (VerticalPadding * 2);
        }

        internal static int CalculateItemHeight(string? text, Font? font, int clientWidth)
        {
            var resolvedFont = font ?? SystemFonts.MessageBoxFont!;
            var minimumHeight = GetMinimumItemHeight(resolvedFont);

            if (string.IsNullOrWhiteSpace(text))
                return minimumHeight;

            var availableTextWidth = GetAvailableTextWidth(clientWidth);
            var measuredHeight = MeasureWrappedTextHeight(text, resolvedFont, availableTextWidth);
            var clampedTextHeight = Math.Clamp(
                measuredHeight,
                GetLineHeight(resolvedFont),
                GetLineHeight(resolvedFont) * MaxVisibleLines);

            return Math.Max(minimumHeight, clampedTextHeight + (VerticalPadding * 2));
        }

        internal static Rectangle GetTextBounds(Rectangle itemBounds)
        {
            return new Rectangle(
                itemBounds.Left + HorizontalPadding,
                itemBounds.Top + VerticalPadding,
                Math.Max(1, itemBounds.Width - (HorizontalPadding * 2)),
                Math.Max(1, itemBounds.Height - (VerticalPadding * 2)));
        }

        internal static int GetAvailableTextWidth(int clientWidth)
        {
            return Math.Max(1, clientWidth - (HorizontalPadding * 2));
        }

        internal static int GetLineHeight(Font? font)
        {
            var resolvedFont = font ?? SystemFonts.MessageBoxFont!;

            try
            {
                return Math.Max(
                    resolvedFont.Height,
                    TextRenderer.MeasureText("Ag", resolvedFont, Size.Empty, TextFormatFlags.NoPadding).Height);
            }
            catch (ArgumentException)
            {
                return (int)Math.Ceiling(resolvedFont.Size * 1.6f);
            }
        }

        private static int MeasureWrappedTextHeight(string text, Font font, int availableTextWidth)
        {
            try
            {
                return TextRenderer.MeasureText(
                    text,
                    font,
                    new Size(availableTextWidth, int.MaxValue),
                    TextMeasurementFlags).Height;
            }
            catch (ArgumentException)
            {
                return GetLineHeight(font);
            }
        }
    }
}
