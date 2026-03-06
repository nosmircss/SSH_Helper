using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class PresetHeaderIndicatorFormatterTests
{
    [Fact]
    public void Format_WhenNothingIsSelectedAndEditorIsClean_ReturnsDefaultLabel()
    {
        var indicator = PresetHeaderIndicatorFormatter.Format(null, null, isDirty: false);

        indicator.Should().Be("Presets");
    }

    [Fact]
    public void Format_WhenPresetIsSelectedAndEditorIsClean_ReturnsPresetName()
    {
        var indicator = PresetHeaderIndicatorFormatter.Format(null, "Deploy App", isDirty: false);

        indicator.Should().Be("Preset: Deploy App");
    }

    [Fact]
    public void Format_WhenPresetIsDirty_AppendsUnsavedMarker()
    {
        var indicator = PresetHeaderIndicatorFormatter.Format(null, "Deploy App", isDirty: true);

        indicator.Should().Be("Preset: Deploy App (unsaved)");
    }

    [Fact]
    public void Format_WhenFolderIsSelected_ReturnsFolderLabel()
    {
        var indicator = PresetHeaderIndicatorFormatter.Format("Prod/Shared", "Deploy App", isDirty: true);

        indicator.Should().Be("Folder: Prod/Shared");
    }

    [Fact]
    public void Format_WhenEditorIsDirtyWithoutPresetName_ReturnsUnsavedDefaultLabel()
    {
        var indicator = PresetHeaderIndicatorFormatter.Format(null, null, isDirty: true);

        indicator.Should().Be("Presets (unsaved)");
    }

    [Fact]
    public void FormatCommandSectionTitle_WhenEditorIsClean_ReturnsCommands()
    {
        var indicator = PresetHeaderIndicatorFormatter.FormatCommandSectionTitle(isDirty: false);

        indicator.Should().Be("Commands");
    }

    [Fact]
    public void FormatCommandSectionTitle_WhenEditorIsDirty_ReturnsUnsavedCommands()
    {
        var indicator = PresetHeaderIndicatorFormatter.FormatCommandSectionTitle(isDirty: true);

        indicator.Should().Be("Commands (unsaved)");
    }

    [Fact]
    public void FormatSaveButtonLabel_WhenEditorIsClean_ReturnsSave()
    {
        var indicator = PresetHeaderIndicatorFormatter.FormatSaveButtonLabel(isDirty: false);

        indicator.Should().Be("Save");
    }

    [Fact]
    public void FormatSaveButtonLabel_WhenEditorIsDirty_ReturnsSaveStar()
    {
        var indicator = PresetHeaderIndicatorFormatter.FormatSaveButtonLabel(isDirty: true);

        indicator.Should().Be("Save*");
    }
}
