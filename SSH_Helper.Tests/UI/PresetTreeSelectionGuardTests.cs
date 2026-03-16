using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class PresetTreeSelectionGuardTests
{
    [WinFormsFact]
    public void CanSelectWithoutEnsuringVisible_WhenNodeIsRootLevel_ReturnsTrueEvenIfTreeIsNotShown()
    {
        using var tree = new TreeView();
        var rootPreset = Preset("Gamma");
        tree.Nodes.Add(rootPreset);

        var canSelect = PresetTreeSelectionGuard.CanSelectWithoutEnsuringVisible(rootPreset);

        rootPreset.IsVisible.Should().BeFalse();
        canSelect.Should().BeTrue();
    }

    [WinFormsFact]
    public void CanSelectWithoutEnsuringVisible_WhenAncestorIsCollapsed_ReturnsFalse()
    {
        using var tree = new TreeView();
        var folder = Folder("Servers");
        var childPreset = Preset("Gamma");
        folder.Nodes.Add(childPreset);
        tree.Nodes.Add(folder);

        var canSelect = PresetTreeSelectionGuard.CanSelectWithoutEnsuringVisible(childPreset);

        canSelect.Should().BeFalse();
    }

    [WinFormsFact]
    public void CanSelectWithoutEnsuringVisible_WhenAncestorsAreExpanded_ReturnsTrue()
    {
        using var tree = new TreeView();
        var folder = Folder("Servers");
        var childPreset = Preset("Gamma");
        folder.Nodes.Add(childPreset);
        tree.Nodes.Add(folder);
        folder.Expand();

        var canSelect = PresetTreeSelectionGuard.CanSelectWithoutEnsuringVisible(childPreset);

        canSelect.Should().BeTrue();
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
