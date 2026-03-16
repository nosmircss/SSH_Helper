using System.Windows.Forms;

namespace SSH_Helper.UI;

internal static class PresetTreeDeleteMutation
{
    internal static void RemoveNodeAndSelectReplacement(TreeView treeView, TreeNode nodeToDelete, TreeNode? replacementNode)
    {
        var topNodeBefore = PresetTreeViewportRestorer.Capture(treeView.TopNode);

        treeView.BeginUpdate();
        try
        {
            RemoveNode(nodeToDelete);

            if (replacementNode != null && replacementNode.TreeView == treeView)
            {
                treeView.SelectedNode = replacementNode;
            }

            PresetTreeViewportRestorer.TryRestoreTopNode(
                treeView,
                treeView.Nodes,
                topNodeBefore,
                PresetTreeViewportRestorer.Capture(replacementNode));
        }
        finally
        {
            treeView.EndUpdate();
        }
    }

    private static void RemoveNode(TreeNode node)
    {
        if (node.Parent != null)
        {
            node.Parent.Nodes.Remove(node);
            return;
        }

        node.TreeView?.Nodes.Remove(node);
    }
}
