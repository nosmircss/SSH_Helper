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
    }
}
