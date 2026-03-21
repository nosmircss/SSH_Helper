using System.Windows.Forms;

namespace SSH_Helper
{
    internal sealed class BufferedSplitContainer : SplitContainer
    {
        private const int WmEraseBkgnd = 0x0014;

        public BufferedSplitContainer()
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

            if (Panel1.BackColor.A < byte.MaxValue || Panel2.BackColor.A < byte.MaxValue)
            {
                return false;
            }

            return true;
        }
    }
}
