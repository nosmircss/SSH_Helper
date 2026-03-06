using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class PresetEnvironmentStatusFormatterTests
{
    [Fact]
    public void FormatRestoreMessage_WhenUsingGlobalBase_ReportsBaseEnvironment()
    {
        var resolution = new PresetBaseEnvironmentResolution(
            "Default",
            PresetBaseEnvironmentSourceKind.GlobalBase,
            null);

        var message = PresetEnvironmentStatusFormatter.FormatRestoreMessage("QA Interactive Shared_Copy", resolution);

        message.Should().Be("Preset 'QA Interactive Shared_Copy' restored base environment 'Default'.");
    }

    [Fact]
    public void FormatRestoreMessage_WhenUsingFolderBase_ReportsFolderBaseEnvironment()
    {
        var resolution = new PresetBaseEnvironmentResolution(
            "lab",
            PresetBaseEnvironmentSourceKind.FolderBase,
            "Shared/QA");

        var message = PresetEnvironmentStatusFormatter.FormatRestoreMessage("QA Interactive Shared_Copy", resolution);

        message.Should().Be("Preset 'QA Interactive Shared_Copy' restored folder base environment 'lab'.");
    }

    [Fact]
    public void FormatSwitchMessage_ReportsPresetDrivenEnvironmentSwitch()
    {
        var message = PresetEnvironmentStatusFormatter.FormatSwitchMessage("QA Interactive Shared_Copy", "prod");

        message.Should().Be("Preset 'QA Interactive Shared_Copy' switched to environment 'prod'.");
    }

    [Fact]
    public void FormatMissingEnvironmentMessage_ReportsMissingTargetEnvironment()
    {
        var message = PresetEnvironmentStatusFormatter.FormatMissingEnvironmentMessage("QA Interactive Shared_Copy", "prod");

        message.Should().Be("Preset 'QA Interactive Shared_Copy' requested environment 'prod', but it was not found.");
    }
}
