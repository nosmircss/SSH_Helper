using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class PresetDeletionSelectionResolverTests
{
    [Fact]
    public void GetAdjacentPresetName_WhenPreviousVisibleNodeIsFolder_ReturnsPreviousPreset()
    {
        var visibleNodes = new[]
        {
            Folder("Networking"),
            Preset("Alpha"),
            Folder("Servers"),
            Preset("Beta"),
            Preset("Gamma")
        };

        var target = PresetDeletionSelectionResolver.GetAdjacentPresetName(visibleNodes, "Beta");

        target.Should().Be("Alpha");
    }

    [Fact]
    public void GetAdjacentPresetName_WhenNoPreviousPresetExists_ReturnsNextPreset()
    {
        var visibleNodes = new[]
        {
            Folder("Servers"),
            Preset("Alpha"),
            Preset("Beta")
        };

        var target = PresetDeletionSelectionResolver.GetAdjacentPresetName(visibleNodes, "Alpha");

        target.Should().Be("Beta");
    }

    [Fact]
    public void GetAdjacentPresetName_WhenDeletedPresetIsOnlyVisiblePreset_ReturnsNull()
    {
        var visibleNodes = new[]
        {
            Folder("Servers"),
            Preset("Alpha")
        };

        var target = PresetDeletionSelectionResolver.GetAdjacentPresetName(visibleNodes, "Alpha");

        target.Should().BeNull();
    }

    [Fact]
    public void GetAdjacentPresetName_WhenDeletedPresetIsMissing_ReturnsNull()
    {
        var visibleNodes = new[]
        {
            Preset("Alpha"),
            Preset("Beta")
        };

        var target = PresetDeletionSelectionResolver.GetAdjacentPresetName(visibleNodes, "Gamma");

        target.Should().BeNull();
    }

    private static PresetNodeTag Folder(string name) => new()
    {
        IsFolder = true,
        Name = name
    };

    private static PresetNodeTag Preset(string name) => new()
    {
        IsFolder = false,
        Name = name
    };
}
