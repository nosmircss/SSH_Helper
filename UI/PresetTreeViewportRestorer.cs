using System.Windows.Forms;

namespace SSH_Helper.UI;

internal static class PresetTreeViewportRestorer
{
    internal static PresetNodeTag? Capture(TreeNode? node)
    {
        if (node?.Tag is not PresetNodeTag tag)
        {
            return null;
        }

        return new PresetNodeTag
        {
            IsFolder = tag.IsFolder,
            Name = tag.Name
        };
    }

    internal static void TryRestoreTopNode(
        TreeView treeView,
        TreeNodeCollection nodes,
        PresetNodeTag? preferredTopNodeTag,
        PresetNodeTag? fallbackTopNodeTag)
    {
        var preferredTopNode = FindNodeByTag(nodes, preferredTopNodeTag);
        var fallbackTopNode = ResolveNode(nodes, preferredTopNodeTag, fallbackTopNodeTag) ?? preferredTopNode;

        if (preferredTopNode != null)
        {
            TryRestoreTopNode(treeView, preferredTopNode, fallbackTopNode ?? preferredTopNode);
            return;
        }

        if (fallbackTopNode != null)
        {
            TryRestoreTopNode(treeView, fallbackTopNode, fallbackTopNode);
        }
    }

    internal static TreeNode? ResolveNode(
        TreeNodeCollection nodes,
        PresetNodeTag? preferredTopNodeTag,
        PresetNodeTag? fallbackTopNodeTag)
    {
        return FindNodeByTag(nodes, preferredTopNodeTag)
            ?? FindNodeByTag(nodes, fallbackTopNodeTag);
    }

    private static TreeNode? FindNodeByTag(TreeNodeCollection nodes, PresetNodeTag? targetTag)
    {
        if (targetTag == null)
        {
            return null;
        }

        foreach (TreeNode node in nodes)
        {
            if (node.Tag is PresetNodeTag tag &&
                tag.IsFolder == targetTag.IsFolder &&
                string.Equals(tag.Name, targetTag.Name, StringComparison.Ordinal))
            {
                return node;
            }

            var found = FindNodeByTag(node.Nodes, targetTag);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void TryRestoreTopNode(TreeView treeView, TreeNode preferredTopNode, TreeNode fallbackNode)
    {
        if (TrySetTopNode(treeView, preferredTopNode))
        {
            return;
        }

        var ancestor = preferredTopNode.Parent;
        while (ancestor != null)
        {
            if (TrySetTopNode(treeView, ancestor))
            {
                return;
            }

            ancestor = ancestor.Parent;
        }

        TrySetTopNode(treeView, fallbackNode);
    }

    private static bool TrySetTopNode(TreeView treeView, TreeNode? node)
    {
        if (node == null || node.TreeView != treeView)
        {
            return false;
        }

        try
        {
            treeView.TopNode = node;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
