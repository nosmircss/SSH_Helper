using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class PresetBaseEnvironmentResolverTests
{
    [Fact]
    public void Resolve_WhenFolderHasExplicitBase_ReturnsFolderBase()
    {
        var folders = new Dictionary<string, FolderInfo>(StringComparer.Ordinal)
        {
            ["Network/Prod"] = new() { BaseEnvironment = "prod" }
        };

        var resolution = PresetBaseEnvironmentResolver.Resolve("Default", "Network/Prod", folders);

        resolution.EnvironmentName.Should().Be("prod");
        resolution.SourceKind.Should().Be(PresetBaseEnvironmentSourceKind.FolderBase);
        resolution.SourceFolderPath.Should().Be("Network/Prod");
    }

    [Fact]
    public void Resolve_WhenChildFolderHasNoBase_UsesNearestAncestorBase()
    {
        var folders = new Dictionary<string, FolderInfo>(StringComparer.Ordinal)
        {
            ["Network"] = new() { BaseEnvironment = "lab" },
            ["Network/Switches"] = new()
        };

        var resolution = PresetBaseEnvironmentResolver.Resolve("Default", "Network/Switches", folders);

        resolution.EnvironmentName.Should().Be("lab");
        resolution.SourceKind.Should().Be(PresetBaseEnvironmentSourceKind.FolderBase);
        resolution.SourceFolderPath.Should().Be("Network");
    }

    [Fact]
    public void Resolve_WhenNestedFoldersHaveOverrides_UsesNearestFolderBase()
    {
        var folders = new Dictionary<string, FolderInfo>(StringComparer.Ordinal)
        {
            ["Network"] = new() { BaseEnvironment = "lab" },
            ["Network/Switches"] = new() { BaseEnvironment = "prod" }
        };

        var resolution = PresetBaseEnvironmentResolver.Resolve("Default", "Network/Switches/Access", folders);

        resolution.EnvironmentName.Should().Be("prod");
        resolution.SourceKind.Should().Be(PresetBaseEnvironmentSourceKind.FolderBase);
        resolution.SourceFolderPath.Should().Be("Network/Switches");
    }

    [Fact]
    public void Resolve_WhenNoFolderOverrideExists_ReturnsGlobalBase()
    {
        var folders = new Dictionary<string, FolderInfo>(StringComparer.Ordinal);

        var resolution = PresetBaseEnvironmentResolver.Resolve("Default", "Network/Prod", folders);

        resolution.EnvironmentName.Should().Be("Default");
        resolution.SourceKind.Should().Be(PresetBaseEnvironmentSourceKind.GlobalBase);
        resolution.SourceFolderPath.Should().BeNull();
    }
}
