using System.Windows.Forms;

namespace SSH_Helper.UI;

internal static class PresetTreeSelectionGuard
{
    internal static bool CanSelectWithoutEnsuringVisible(TreeNode? node)
    {
        if (node == null)
        {
            return false;
        }

        var current = node.Parent;
        while (current != null)
        {
            if (!current.IsExpanded)
            {
                return false;
            }

            current = current.Parent;
        }

        return true;
    }
}
