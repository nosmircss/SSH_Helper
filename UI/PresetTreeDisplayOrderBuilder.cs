using System.Windows.Forms;

namespace SSH_Helper.UI;

internal static class PresetTreeDisplayOrderBuilder
{
    internal static List<PresetNodeTag> Build(TreeNodeCollection nodes)
    {
        var orderedTags = new List<PresetNodeTag>();
        AppendDisplayedNodes(nodes, orderedTags);
        return orderedTags;
    }

    private static void AppendDisplayedNodes(TreeNodeCollection nodes, List<PresetNodeTag> orderedTags)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is not PresetNodeTag tag)
            {
                continue;
            }

            orderedTags.Add(new PresetNodeTag
            {
                IsFolder = tag.IsFolder,
                Name = tag.Name
            });

            if (node.IsExpanded && node.Nodes.Count > 0)
            {
                AppendDisplayedNodes(node.Nodes, orderedTags);
            }
        }
    }
}
