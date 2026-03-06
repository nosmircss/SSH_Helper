using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class FolderBaseEnvironmentSummaryFormatterTests
{
    [Fact]
    public void FormatSummaryLine_WhenFolderHasExplicitBase_ShowsExplicitFolderBase()
    {
        var resolution = new PresetBaseEnvironmentResolution(
            "prod",
            PresetBaseEnvironmentSourceKind.FolderBase,
            "Network/Parent");

        var line = FolderBaseEnvironmentSummaryFormatter.FormatSummaryLine("lab", resolution);

        line.Should().Be("  Folder Base Environment: lab");
    }

    [Fact]
    public void FormatSummaryLine_WhenFolderInheritsFromAncestor_IncludesSourceFolder()
    {
        var resolution = new PresetBaseEnvironmentResolution(
            "prod",
            PresetBaseEnvironmentSourceKind.FolderBase,
            "Network/Parent");

        var line = FolderBaseEnvironmentSummaryFormatter.FormatSummaryLine(null, resolution);

        line.Should().Be("  Inherited Folder Base: prod (from Network/Parent)");
    }

    [Fact]
    public void FormatSummaryLine_WhenFolderFallsBackToGlobal_LabelsGlobalBase()
    {
        var resolution = new PresetBaseEnvironmentResolution(
            "Default",
            PresetBaseEnvironmentSourceKind.GlobalBase,
            null);

        var line = FolderBaseEnvironmentSummaryFormatter.FormatSummaryLine(null, resolution);

        line.Should().Be("  Base Environment: Default (global)");
    }

    [Fact]
    public void FormatInheritChoiceLabel_WhenFolderInheritsFromAncestor_IncludesSourceFolder()
    {
        var resolution = new PresetBaseEnvironmentResolution(
            "prod",
            PresetBaseEnvironmentSourceKind.FolderBase,
            "Network/Parent");

        var label = FolderBaseEnvironmentSummaryFormatter.FormatInheritChoiceLabel(resolution);

        label.Should().Be("(Inherit: prod from Network/Parent)");
    }

    [Fact]
    public void FormatInheritChoiceLabel_WhenFolderFallsBackToGlobal_LabelsGlobalSource()
    {
        var resolution = new PresetBaseEnvironmentResolution(
            "Default",
            PresetBaseEnvironmentSourceKind.GlobalBase,
            null);

        var label = FolderBaseEnvironmentSummaryFormatter.FormatInheritChoiceLabel(resolution);

        label.Should().Be("(Inherit Global: Default)");
    }
}
