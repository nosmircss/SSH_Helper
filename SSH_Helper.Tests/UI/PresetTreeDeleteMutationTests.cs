using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class PresetTreeDeleteMutationTests
{
    [WinFormsFact]
    public void RemoveNodeAndSelectReplacement_DeletesSelectedNodeWithoutResettingViewport()
    {
        using var form = new Form { Width = 240, Height = 120 };
        using var tree = new TreeView { Dock = DockStyle.Fill, ItemHeight = 18 };
        form.Controls.Add(tree);
        AddRootPresets(tree, Enumerable.Range(1, 30).Select(i => $"Preset {i:00}"));

        form.Show();
        Application.DoEvents();

        tree.TopNode = tree.Nodes[10];
        tree.SelectedNode = tree.Nodes[20];
        var nodeToDelete = tree.Nodes[20];
        var replacementNode = tree.Nodes[19];

        PresetTreeDeleteMutation.RemoveNodeAndSelectReplacement(tree, nodeToDelete, replacementNode);

        tree.TopNode!.Index.Should().BeGreaterThan(0);
        ((PresetNodeTag)tree.SelectedNode!.Tag!).Name.Should().Be("Preset 20");
        tree.SelectedNode.IsVisible.Should().BeTrue();
        tree.Nodes.Cast<TreeNode>()
            .Select(node => ((PresetNodeTag)node.Tag!).Name)
            .Should().NotContain("Preset 21");
    }

    private static void AddRootPresets(TreeView tree, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            tree.Nodes.Add(new TreeNode(name)
            {
                Tag = new PresetNodeTag
                {
                    IsFolder = false,
                    Name = name
                }
            });
        }
    }
}
