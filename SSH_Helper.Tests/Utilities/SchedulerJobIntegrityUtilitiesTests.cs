using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class SchedulerJobIntegrityUtilitiesTests
{
    [Fact]
    public void ApplyMissingTargetImportState_PresetJob_DisablesWithExplicitReason()
    {
        var job = new JobDefinition
        {
            TargetType = JobTargetType.Preset,
            TargetName = "MissingPreset",
            IsEnabled = true
        };

        SchedulerJobIntegrityUtilities.ApplyMissingTargetImportState(job);

        job.IsEnabled.Should().BeFalse();
        job.DisabledReason.Should().Be("Missing preset target 'MissingPreset'");
    }

    [Fact]
    public void ApplyMissingTargetImportState_FolderJob_DisablesWithExplicitReason()
    {
        var job = new JobDefinition
        {
            TargetType = JobTargetType.Folder,
            TargetName = "MissingFolder",
            IsEnabled = true
        };

        SchedulerJobIntegrityUtilities.ApplyMissingTargetImportState(job);

        job.IsEnabled.Should().BeFalse();
        job.DisabledReason.Should().Be("Missing folder target 'MissingFolder'");
    }

    [Fact]
    public void FormatStoredCredentialNote_WithStoredPassword_ReturnsKeepSecretMessage()
    {
        SchedulerJobIntegrityUtilities.FormatStoredCredentialNote(true)
            .Should().Be("Credentials are stored in Windows Credential Manager. Leave password blank to keep the current secret.");
    }
}
