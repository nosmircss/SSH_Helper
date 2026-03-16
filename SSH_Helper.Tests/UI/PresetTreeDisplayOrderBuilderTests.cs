using System.Linq;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class PresetTreeDisplayOrderBuilderTests
{
    [WinFormsFact]
    public void Build_WhenTreeIsNotShown_PreservesRootPresetOrderForDeleteSelection()
    {
        using var tree = new TreeView();
        tree.Nodes.Add(Preset("Alpha"));
        tree.Nodes.Add(Preset("Beta"));
        tree.Nodes.Add(Preset("Gamma"));

        var orderedNodes = PresetTreeDisplayOrderBuilder.Build(tree.Nodes);
        var target = PresetDeletionSelectionResolver.GetAdjacentPresetName(orderedNodes, "Gamma");

        orderedNodes.Where(node => !node.IsFolder).Select(node => node.Name)
            .Should().Equal("Alpha", "Beta", "Gamma");
        target.Should().Be("Beta");
    }

    [WinFormsFact]
    public void Build_WhenFolderIsCollapsed_SkipsCollapsedChildrenButKeepsExpandedBranchOrder()
    {
        using var form = new Form();
        using var tree = new TreeView { Dock = DockStyle.Fill };
        form.Controls.Add(tree);

        var collapsedFolder = Folder("Collapsed");
        collapsedFolder.Nodes.Add(Preset("Hidden"));

        var rootPreset = Preset("Root");

        var expandedFolder = Folder("Expanded");
        expandedFolder.Nodes.Add(Preset("Inside"));

        tree.Nodes.Add(collapsedFolder);
        tree.Nodes.Add(rootPreset);
        tree.Nodes.Add(expandedFolder);

        form.Show();
        expandedFolder.Expand();
        Application.DoEvents();

        var orderedNodes = PresetTreeDisplayOrderBuilder.Build(tree.Nodes);

        orderedNodes.Select(node => $"{(node.IsFolder ? "F" : "P")}:{node.Name}")
            .Should().Equal("F:Collapsed", "P:Root", "F:Expanded", "P:Inside");
    }

    private static TreeNode Folder(string name) => new(name)
    {
        Tag = new PresetNodeTag
        {
            IsFolder = true,
            Name = name
        }
    };

    private static TreeNode Preset(string name) => new(name)
    {
        Tag = new PresetNodeTag
        {
            IsFolder = false,
            Name = name
        }
    };
}
