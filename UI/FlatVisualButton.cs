using System.Drawing;
using System.Windows.Forms;

namespace SSH_Helper.UI;

public class FlatVisualButton : Button
{
    private bool _isHovered;
    private bool _isPressed;

    public FlatVisualButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        TextAlign = ContentAlignment.MiddleCenter;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled)
        {
            _isHovered = false;
            _isPressed = false;
        }

        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (!Enabled)
        {
            return;
        }

        _isHovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!Enabled)
        {
            return;
        }

        _isHovered = false;
        _isPressed = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        if (!Enabled || mevent.Button != MouseButtons.Left)
        {
            return;
        }

        _isPressed = true;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        if (!Enabled || mevent.Button != MouseButtons.Left)
        {
            return;
        }

        _isPressed = false;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var graphics = pevent.Graphics;
        graphics.Clear(Parent?.BackColor ?? SystemColors.Control);

        var clientRectangle = ClientRectangle;
        if (clientRectangle.Width <= 0 || clientRectangle.Height <= 0)
        {
            return;
        }

        using var backgroundBrush = new SolidBrush(ResolveBackgroundColor());
        graphics.FillRectangle(backgroundBrush, clientRectangle);

        if (FlatAppearance.BorderSize > 0)
        {
            using var borderPen = new Pen(FlatAppearance.BorderColor, FlatAppearance.BorderSize);
            var borderRectangle = Rectangle.Inflate(clientRectangle, -FlatAppearance.BorderSize / 2, -FlatAppearance.BorderSize / 2);
            graphics.DrawRectangle(borderPen, borderRectangle);
        }

        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            clientRectangle,
            ForeColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);
    }

    private Color ResolveBackgroundColor()
    {
        if (!Enabled)
        {
            return BackColor;
        }

        if (_isPressed && !FlatAppearance.MouseDownBackColor.IsEmpty)
        {
            return FlatAppearance.MouseDownBackColor;
        }

        if (_isHovered && !FlatAppearance.MouseOverBackColor.IsEmpty)
        {
            return FlatAppearance.MouseOverBackColor;
        }

        return BackColor;
    }
}
