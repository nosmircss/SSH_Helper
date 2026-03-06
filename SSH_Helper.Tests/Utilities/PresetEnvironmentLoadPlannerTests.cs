using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class PresetEnvironmentLoadPlannerTests
{
    [Fact]
    public void Plan_WhenDeclaredEnvironmentDiffersFromActive_SwitchesActiveEnvironment()
    {
        var action = PresetEnvironmentLoadPlanner.Plan("Default", "Default", "prod");

        action.Kind.Should().Be(PresetEnvironmentLoadActionKind.SwitchActiveEnvironment);
        action.TargetEnvironment.Should().Be("prod");
    }

    [Fact]
    public void Plan_WhenDeclaredEnvironmentMatchesActive_ReturnsNoAction()
    {
        var action = PresetEnvironmentLoadPlanner.Plan("prod", "Default", "prod");

        action.Kind.Should().Be(PresetEnvironmentLoadActionKind.None);
        action.TargetEnvironment.Should().BeNull();
    }

    [Fact]
    public void Plan_WhenNoDeclaredEnvironmentAndActiveDiffersFromBase_RestoresBaseEnvironment()
    {
        var action = PresetEnvironmentLoadPlanner.Plan("prod", "Default", null);

        action.Kind.Should().Be(PresetEnvironmentLoadActionKind.RestoreBaseEnvironment);
        action.TargetEnvironment.Should().Be("Default");
    }

    [Fact]
    public void Plan_WhenNoDeclaredEnvironmentAndActiveMatchesBase_ReturnsNoAction()
    {
        var action = PresetEnvironmentLoadPlanner.Plan("Default", "Default", null);

        action.Kind.Should().Be(PresetEnvironmentLoadActionKind.None);
        action.TargetEnvironment.Should().BeNull();
    }
}
