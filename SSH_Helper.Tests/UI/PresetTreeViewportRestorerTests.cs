using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class PresetTreeViewportRestorerTests
{
    [WinFormsFact]
    public void TryRestoreTopNode_WhenPreferredNodeStillExists_RestoresPreferredNode()
    {
        using var form = new Form { Width = 240, Height = 120 };
        using var tree = new TreeView { Dock = DockStyle.Fill, ItemHeight = 18 };
        form.Controls.Add(tree);
        AddRootPresets(tree, Enumerable.Range(1, 20).Select(i => $"Preset {i:00}"));

        form.Show();
        Application.DoEvents();

        tree.TopNode = tree.Nodes[10];
        var preferred = PresetTreeViewportRestorer.Capture(tree.TopNode);

        tree.BeginUpdate();
        try
        {
            tree.Nodes.Clear();
            AddRootPresets(tree, Enumerable.Range(1, 20).Select(i => $"Preset {i:00}"));
            PresetTreeViewportRestorer.TryRestoreTopNode(tree, tree.Nodes, preferred, fallbackTopNodeTag: null);
        }
        finally
        {
            tree.EndUpdate();
        }

        ((PresetNodeTag)tree.TopNode!.Tag!).Name.Should().Be("Preset 11");
    }

    [WinFormsFact]
    public void ResolveNode_WhenPreferredNodeWasDeleted_UsesFallbackNode()
    {
        using var tree = new TreeView();
        AddRootPresets(tree, Enumerable.Range(1, 20).Select(i => $"Preset {i:00}"));
        var preferred = new PresetNodeTag { IsFolder = false, Name = "Preset 11" };
        var fallback = new PresetNodeTag { IsFolder = false, Name = "Preset 10" };
        tree.Nodes.Clear();
        AddRootPresets(tree, Enumerable.Range(1, 20)
            .Select(i => $"Preset {i:00}")
            .Where(name => name != "Preset 11"));

        var resolvedNode = PresetTreeViewportRestorer.ResolveNode(tree.Nodes, preferred, fallback);

        ((PresetNodeTag)resolvedNode!.Tag!).Name.Should().Be("Preset 10");
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
