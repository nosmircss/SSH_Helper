using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class PresetSaveImpactResolverTests
{
    [Fact]
    public void Resolve_BlankPresetName_ReturnsNone()
    {
        var impact = PresetSaveImpactResolver.Resolve(
            presetName: string.Empty,
            folderPath: null,
            presetJobs: Array.Empty<JobDefinition>(),
            folderJobs: Array.Empty<JobDefinition>());

        impact.Should().BeSameAs(PresetSaveImpact.None);
    }

    [Fact]
    public void Resolve_CombinesPresetAndFolderJobs_AndSortsByName()
    {
        var impact = PresetSaveImpactResolver.Resolve(
            presetName: "Nightly",
            folderPath: "Schedulers",
            presetJobs: new[]
            {
                CreateJob("job-b", "Zulu Direct", JobTargetType.Preset, "Nightly"),
                CreateJob("job-c", "bravo Direct", JobTargetType.Preset, "Nightly")
            },
            folderJobs: new[]
            {
                CreateJob("job-a", "Alpha Folder", JobTargetType.Folder, "Schedulers")
            });

        impact.HasAffectedJobs.Should().BeTrue();
        impact.AffectedJobs.Select(static job => job.Name).Should().Equal(
            "Alpha Folder",
            "bravo Direct",
            "Zulu Direct");
    }

    [Fact]
    public void Resolve_DeduplicatesDuplicateJobIds()
    {
        var duplicateId = "job-dup";
        var impact = PresetSaveImpactResolver.Resolve(
            presetName: "Nightly",
            folderPath: "Schedulers",
            presetJobs: new[]
            {
                CreateJob(duplicateId, "Duplicate Direct", JobTargetType.Preset, "Nightly")
            },
            folderJobs: new[]
            {
                CreateJob(duplicateId, "Duplicate Folder", JobTargetType.Folder, "Schedulers"),
                CreateJob("job-other", "Other Folder", JobTargetType.Folder, "Schedulers")
            });

        impact.AffectedJobs.Should().HaveCount(2);
        impact.AffectedJobs.Select(static job => job.Id).Should().Equal(duplicateId, "job-other");
        impact.AffectedJobs[0].Name.Should().Be("Duplicate Direct");
    }

    [Fact]
    public void Resolve_NoAffectedJobs_ReturnsNone()
    {
        var impact = PresetSaveImpactResolver.Resolve(
            presetName: "Nightly",
            folderPath: "Schedulers",
            presetJobs: Array.Empty<JobDefinition>(),
            folderJobs: Array.Empty<JobDefinition>());

        impact.Should().BeSameAs(PresetSaveImpact.None);
    }

    private static JobDefinition CreateJob(string id, string name, JobTargetType targetType, string targetName)
    {
        return new JobDefinition
        {
            Id = id,
            Name = name,
            TargetType = targetType,
            TargetName = targetName
        };
    }
}
