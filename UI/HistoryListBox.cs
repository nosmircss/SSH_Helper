using System.Windows.Forms;

namespace SSH_Helper.UI
{
    internal sealed class HistoryListBox : ListBox
    {
        private int _lastMeasuredClientWidth = -1;

        internal void RefreshVariableItemHeights()
        {
            if (IsDisposed || DrawMode != DrawMode.OwnerDrawVariable)
                return;

            _lastMeasuredClientWidth = ClientSize.Width;

            BeginUpdate();
            try
            {
                RefreshItems();
            }
            finally
            {
                EndUpdate();
            }

            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            if (DrawMode == DrawMode.OwnerDrawVariable && ClientSize.Width != _lastMeasuredClientWidth)
            {
                RefreshVariableItemHeights();
            }
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);

            if (DrawMode == DrawMode.OwnerDrawVariable)
            {
                RefreshVariableItemHeights();
            }
        }
    }
}
