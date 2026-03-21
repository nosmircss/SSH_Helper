using System.Windows.Forms;

namespace SSH_Helper
{
    internal sealed class BufferedPanel : Panel
    {
        private const int WmEraseBkgnd = 0x0014;

        public BufferedPanel()
        {
            SetStyle(
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.ResizeRedraw,
                true);
            DoubleBuffered = true;
            UpdateStyles();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmEraseBkgnd && CanSkipBackgroundErase())
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);
        }

        private bool CanSkipBackgroundErase()
        {
            if (BackgroundImage != null || BackColor.A < byte.MaxValue)
            {
                return false;
            }

            foreach (Control child in Controls)
            {
                if (!child.Visible)
                {
                    continue;
                }

                if (child.BackColor.A < byte.MaxValue)
                {
                    return false;
                }

            }

            return true;
        }
    }
}
