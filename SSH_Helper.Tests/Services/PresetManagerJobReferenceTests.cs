using FluentAssertions;
using Moq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services
{
    /// <summary>
    /// Tests for PresetManager's job reference awareness when interacting with JobStorageService.
    /// Uses temp directories for both ConfigurationService and JobStorageService isolation.
    /// </summary>
    public class PresetManagerJobReferenceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly ConfigurationService _configService;
        private readonly PresetManager _presetManager;
        private readonly Mock<ICredentialProvider> _credProviderMock;
        private readonly JobStorageService _jobStorageService;

        public PresetManagerJobReferenceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"PresetMgrJobRef_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);

            var configPath = Path.Combine(_tempDir, "config.json");
            _configService = new ConfigurationService(configPath);
            _presetManager = new PresetManager(_configService);

            _credProviderMock = new Mock<ICredentialProvider>();
            var jobsPath = Path.Combine(_tempDir, "jobs.json");
            _jobStorageService = new JobStorageService(_credProviderMock.Object, jobsPath);
            _jobStorageService.Load();
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        #region SetJobStorageService

        [Fact]
        public void SetJobStorageService_AcceptsNonNullService()
        {
            // Should not throw
            _presetManager.SetJobStorageService(_jobStorageService);
        }

        [Fact]
        public void SetJobStorageService_AcceptsNull()
        {
            // Should not throw - clears the reference
            _presetManager.SetJobStorageService(null);
        }

        #endregion

        #region GetJobsReferencingPreset

        [Fact]
        public void GetJobsReferencingPreset_WithService_DelegatesToJobStorageService()
        {
            // Arrange
            _presetManager.SetJobStorageService(_jobStorageService);

            var job = new JobDefinition
            {
                Name = "Test Job",
                TargetType = JobTargetType.Preset,
                TargetName = "MyPreset"
            };
            _jobStorageService.Save(job);

            // Act
            var result = _presetManager.GetJobsReferencingPreset("MyPreset");

            // Assert
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Test Job");
        }

        [Fact]
        public void GetJobsReferencingPreset_WithoutService_ReturnsEmptyList()
        {
            // No SetJobStorageService call - should return empty
            var result = _presetManager.GetJobsReferencingPreset("AnyPreset");

            result.Should().BeEmpty();
        }

        [Fact]
        public void GetJobsReferencingPreset_NoMatchingJobs_ReturnsEmptyList()
        {
            _presetManager.SetJobStorageService(_jobStorageService);

            var result = _presetManager.GetJobsReferencingPreset("NonExistentPreset");

            result.Should().BeEmpty();
        }

        #endregion

        #region GetJobsReferencingFolder

        [Fact]
        public void GetJobsReferencingFolder_WithService_DelegatesToJobStorageService()
        {
            _presetManager.SetJobStorageService(_jobStorageService);

            var job = new JobDefinition
            {
                Name = "Folder Job",
                TargetType = JobTargetType.Folder,
                TargetName = "MyFolder"
            };
            _jobStorageService.Save(job);

            var result = _presetManager.GetJobsReferencingFolder("MyFolder");

            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Folder Job");
        }

        [Fact]
        public void GetJobsReferencingFolder_WithoutService_ReturnsEmptyList()
        {
            var result = _presetManager.GetJobsReferencingFolder("AnyFolder");

            result.Should().BeEmpty();
        }

        #endregion

        #region Rename Preset - Job Reference Integrity

        [Fact]
        public void RenamePreset_UpdatesJobTargetName()
        {
            // Arrange
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.Save("OldPreset", new PresetInfo { Commands = "echo test" });

            var job = new JobDefinition
            {
                Name = "Rename Test Job",
                TargetType = JobTargetType.Preset,
                TargetName = "OldPreset"
            };
            _jobStorageService.Save(job);

            // Act
            _presetManager.Rename("OldPreset", "NewPreset");

            // Assert - job TargetName should be updated
            var updatedJob = _jobStorageService.Get(job.Id);
            updatedJob.Should().NotBeNull();
            updatedJob!.TargetName.Should().Be("NewPreset");
        }

        [Fact]
        public void RenamePreset_UpdatesMultipleReferencingJobs()
        {
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.Save("SharedPreset", new PresetInfo { Commands = "echo shared" });

            var job1 = new JobDefinition { Name = "Job 1", TargetType = JobTargetType.Preset, TargetName = "SharedPreset" };
            var job2 = new JobDefinition { Name = "Job 2", TargetType = JobTargetType.Preset, TargetName = "SharedPreset" };
            _jobStorageService.Save(job1);
            _jobStorageService.Save(job2);

            _presetManager.Rename("SharedPreset", "RenamedPreset");

            _jobStorageService.Get(job1.Id)!.TargetName.Should().Be("RenamedPreset");
            _jobStorageService.Get(job2.Id)!.TargetName.Should().Be("RenamedPreset");
        }

        [Fact]
        public void RenamePreset_NoReferencingJobs_WorksUnchanged()
        {
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.Save("LonelyPreset", new PresetInfo { Commands = "echo lonely" });

            // No jobs reference this preset
            var result = _presetManager.Rename("LonelyPreset", "StillLonelyPreset");

            result.Should().BeTrue();
            _presetManager.Presets.Should().ContainKey("StillLonelyPreset");
        }

        [Fact]
        public void RenamePreset_WithoutJobStorageService_WorksNormally()
        {
            // No SetJobStorageService call - backward compatible
            _presetManager.Load();
            _presetManager.Save("PresetA", new PresetInfo { Commands = "echo a" });

            var result = _presetManager.Rename("PresetA", "PresetB");

            result.Should().BeTrue();
            _presetManager.Presets.Should().ContainKey("PresetB");
        }

        #endregion

        #region Delete Preset - Job Reference Integrity

        [Fact]
        public void DeletePreset_DisablesReferencingJobs()
        {
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.Save("Doomed", new PresetInfo { Commands = "echo doomed" });

            var job = new JobDefinition
            {
                Name = "Delete Test Job",
                TargetType = JobTargetType.Preset,
                TargetName = "Doomed",
                IsEnabled = true
            };
            _jobStorageService.Save(job);

            _presetManager.Delete("Doomed");

            var updatedJob = _jobStorageService.Get(job.Id);
            updatedJob.Should().NotBeNull();
            updatedJob!.IsEnabled.Should().BeFalse();
            updatedJob.DisabledReason.Should().Be("Preset 'Doomed' was deleted");
        }

        [Fact]
        public void DeletePreset_NoReferencingJobs_WorksUnchanged()
        {
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.Save("SafePreset", new PresetInfo { Commands = "echo safe" });

            var result = _presetManager.Delete("SafePreset");

            result.Should().BeTrue();
            _presetManager.Presets.Should().NotContainKey("SafePreset");
        }

        [Fact]
        public void DeletePreset_WithoutJobStorageService_WorksNormally()
        {
            _presetManager.Load();
            _presetManager.Save("ToDelete", new PresetInfo { Commands = "echo bye" });

            var result = _presetManager.Delete("ToDelete");

            result.Should().BeTrue();
        }

        #endregion

        #region Delete Folder - Job Reference Integrity

        [Fact]
        public void DeleteFolder_DisablesFolderTypeJobs()
        {
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.CreateFolder("MyFolder");

            var job = new JobDefinition
            {
                Name = "Folder Delete Job",
                TargetType = JobTargetType.Folder,
                TargetName = "MyFolder",
                IsEnabled = true
            };
            _jobStorageService.Save(job);

            _presetManager.DeleteFolder("MyFolder");

            var updatedJob = _jobStorageService.Get(job.Id);
            updatedJob.Should().NotBeNull();
            updatedJob!.IsEnabled.Should().BeFalse();
            updatedJob.DisabledReason.Should().Be("Folder 'MyFolder' was deleted");
        }

        [Fact]
        public void DeleteFolder_NoReferencingJobs_WorksUnchanged()
        {
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.CreateFolder("EmptyFolder");

            var result = _presetManager.DeleteFolder("EmptyFolder");

            result.Should().BeTrue();
        }

        [Fact]
        public void DeleteFolder_WithoutJobStorageService_WorksNormally()
        {
            _presetManager.Load();
            _presetManager.CreateFolder("PlainFolder");

            var result = _presetManager.DeleteFolder("PlainFolder");

            result.Should().BeTrue();
        }

        [Fact]
        public void DeletePreset_PersistsDisabledState()
        {
            _presetManager.SetJobStorageService(_jobStorageService);
            _presetManager.Load();
            _presetManager.Save("Persisted", new PresetInfo { Commands = "echo persisted" });

            var job = new JobDefinition
            {
                Name = "Persist Job",
                TargetType = JobTargetType.Preset,
                TargetName = "Persisted",
                IsEnabled = true
            };
            _jobStorageService.Save(job);

            _presetManager.Delete("Persisted");

            // Reload from disk to verify persistence
            var freshService = new JobStorageService(
                _credProviderMock.Object,
                Path.Combine(_tempDir, "jobs.json"));
            freshService.Load();

            var reloadedJob = freshService.Get(job.Id);
            reloadedJob.Should().NotBeNull();
            reloadedJob!.IsEnabled.Should().BeFalse();
            reloadedJob.DisabledReason.Should().Be("Preset 'Persisted' was deleted");
        }

        #endregion
    }
}
