using FluentAssertions;
using Moq;
using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services
{
    public class JobStorageServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _jobsFilePath;
        private readonly Mock<ICredentialProvider> _credProviderMock;

        public JobStorageServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"JobStorageTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _jobsFilePath = Path.Combine(_tempDir, "jobs.json");
            _credProviderMock = new Mock<ICredentialProvider>();
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        private JobStorageService CreateService(string? path = null)
        {
            return new JobStorageService(_credProviderMock.Object, path ?? _jobsFilePath);
        }

        private JobDefinition CreateTestJob(string name = "Test Job")
        {
            return new JobDefinition
            {
                Name = name,
                TargetType = JobTargetType.Preset,
                TargetName = "MyPreset"
            };
        }

        #region Constructor

        [Fact]
        public void Constructor_NullCredentialProvider_ThrowsArgumentNullException()
        {
            var act = () => new JobStorageService(null!, _jobsFilePath);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Constructor_NullFilePath_UsesDefaultLocation()
        {
            var service = new JobStorageService(_credProviderMock.Object);
            service.JobsFilePath.Should().Contain("SSH_Helper")
                .And.EndWith("jobs.json");
        }

        #endregion

        #region Load

        [Fact]
        public void Load_MissingFile_ReturnsEmptyJobs()
        {
            var service = CreateService();
            service.Load();
            service.Jobs.Should().BeEmpty();
            service.LoadError.Should().BeNull();
        }

        [Fact]
        public void Load_ValidFile_PopulatesJobs()
        {
            var job = CreateTestJob();
            var wrapper = new { Version = 1, Jobs = new[] { job } };
            File.WriteAllText(_jobsFilePath, JsonConvert.SerializeObject(wrapper));

            var service = CreateService();
            service.Load();

            service.Jobs.Should().ContainKey(job.Id);
            service.Jobs[job.Id].Name.Should().Be("Test Job");
        }

        [Fact]
        public void Load_CorruptFile_SetsLoadError_RenamesFile()
        {
            File.WriteAllText(_jobsFilePath, "NOT VALID JSON {{{");

            var service = CreateService();
            service.Load();

            service.Jobs.Should().BeEmpty();
            service.LoadError.Should().NotBeNullOrEmpty();
            File.Exists(_jobsFilePath + ".corrupt").Should().BeTrue();
        }

        [Fact]
        public void Reload_ReReadsFromDisk()
        {
            var service = CreateService();
            service.Load();

            // Write a job to disk externally
            var job = CreateTestJob();
            var wrapper = new { Version = 1, Jobs = new[] { job } };
            File.WriteAllText(_jobsFilePath, JsonConvert.SerializeObject(wrapper));

            service.Reload();
            service.Jobs.Should().ContainKey(job.Id);
        }

        #endregion

        #region Save

        [Fact]
        public void Save_NewJob_PersistsToFile()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            File.Exists(_jobsFilePath).Should().BeTrue();
            service.Jobs.Should().ContainKey(job.Id);
        }

        [Fact]
        public void Save_ExistingJob_UpdatesModifiedUtc()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);
            var firstModified = service.Jobs[job.Id].ModifiedUtc;

            // Small delay to ensure time difference
            System.Threading.Thread.Sleep(20);
            job.Name = "Updated Name";
            service.Save(job);

            service.Jobs[job.Id].ModifiedUtc.Should().BeAfter(firstModified);
            service.Jobs[job.Id].Name.Should().Be("Updated Name");
        }

        [Fact]
        public void Save_DuplicateName_DifferentId_ThrowsArgumentException()
        {
            var service = CreateService();
            service.Load();

            var job1 = CreateTestJob("Unique Name");
            service.Save(job1);

            var job2 = CreateTestJob("unique name"); // case-insensitive match
            var act = () => service.Save(job2);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Save_SameJobSameName_DoesNotThrow()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob("My Job");
            service.Save(job);

            // Saving same job with same name should succeed
            job.TargetName = "OtherPreset";
            var act = () => service.Save(job);
            act.Should().NotThrow();
        }

        [Fact]
        public void Save_NullJob_ThrowsArgumentNullException()
        {
            var service = CreateService();
            service.Load();

            var act = () => service.Save(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Save_EmptyName_ThrowsArgumentException()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            job.Name = "";
            var act = () => service.Save(job);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Save_WhitespaceName_ThrowsArgumentException()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            job.Name = "   ";
            var act = () => service.Save(job);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Save_NameExceeding100Chars_ThrowsArgumentException()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            job.Name = new string('A', 101);
            var act = () => service.Save(job);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Save_TrimsNameWhitespace()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            job.Name = "  Trimmed Name  ";
            service.Save(job);

            service.Jobs[job.Id].Name.Should().Be("Trimmed Name");
        }

        [Fact]
        public void Save_CreatesBackupFile_WhenFileExists()
        {
            var service = CreateService();
            service.Load();

            var job1 = CreateTestJob("First");
            service.Save(job1);
            File.Exists(_jobsFilePath).Should().BeTrue();

            var job2 = CreateTestJob("Second");
            service.Save(job2);
            File.Exists(_jobsFilePath + ".bak").Should().BeTrue();
        }

        [Fact]
        public void Save_RaisesJobsChangedEvent()
        {
            var service = CreateService();
            service.Load();

            bool eventFired = false;
            service.JobsChanged += (s, e) => eventFired = true;

            var job = CreateTestJob();
            service.Save(job);

            eventFired.Should().BeTrue();
        }

        [Fact]
        public void SaveAndLoad_CustomPresetJob_RoundTripsCustomPresetContent()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob("Custom Job");
            job.TargetType = JobTargetType.CustomPreset;
            job.TargetName = string.Empty;
            job.CustomPresetCommands = "---\nsteps:\n  - wait: 1\n";

            service.Save(job);

            var reloaded = CreateService();
            reloaded.Load();

            reloaded.Jobs.Should().ContainKey(job.Id);
            reloaded.Jobs[job.Id].TargetType.Should().Be(JobTargetType.CustomPreset);
            reloaded.Jobs[job.Id].TargetName.Should().BeEmpty();
            reloaded.Jobs[job.Id].CustomPresetCommands.Should().Be("---\r\nsteps:\r\n  - wait: 1\r\n");
        }

        [Fact]
        public void SaveAndLoad_TimeoutOverrides_RoundTrip()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob("Timeout Override Job");
            job.CommandTimeoutOverrideSeconds = 45;
            job.ConnectionTimeoutOverrideSeconds = 12;

            service.Save(job);

            var reloaded = CreateService();
            reloaded.Load();

            reloaded.Jobs.Should().ContainKey(job.Id);
            reloaded.Jobs[job.Id].CommandTimeoutOverrideSeconds.Should().Be(45);
            reloaded.Jobs[job.Id].ConnectionTimeoutOverrideSeconds.Should().Be(12);
        }

        #endregion

        #region Delete

        [Fact]
        public void Delete_ExistingJob_ReturnsTrue_RemovesFromJobs()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            var result = service.Delete(job.Id);
            result.Should().BeTrue();
            service.Jobs.Should().NotContainKey(job.Id);
        }

        [Fact]
        public void Delete_NonExistentJob_ReturnsFalse()
        {
            var service = CreateService();
            service.Load();

            var result = service.Delete("nonexistent");
            result.Should().BeFalse();
        }

        [Fact]
        public void Delete_StoredCredentialMode_CleanupTrue_CallsDeletePassword()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            job.CredentialMode = CredentialMode.Stored;
            service.Save(job);

            service.Delete(job.Id, cleanupCredentials: true);

            _credProviderMock.Verify(
                p => p.DeletePassword(CredentialTargets.JobPasswordTarget(job.Id)),
                Times.Once);
        }

        [Fact]
        public void Delete_NonStoredCredentialMode_DoesNotCallDeletePassword()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            job.CredentialMode = CredentialMode.InheritFromApp;
            service.Save(job);

            service.Delete(job.Id);

            _credProviderMock.Verify(
                p => p.DeletePassword(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void Delete_StoredCredentialMode_CleanupFalse_DoesNotCallDeletePassword()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            job.CredentialMode = CredentialMode.Stored;
            service.Save(job);

            service.Delete(job.Id, cleanupCredentials: false);

            _credProviderMock.Verify(
                p => p.DeletePassword(It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public void Delete_RaisesJobsChangedEvent()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            bool eventFired = false;
            service.JobsChanged += (s, e) => eventFired = true;

            service.Delete(job.Id);
            eventFired.Should().BeTrue();
        }

        #endregion

        #region Get

        [Fact]
        public void Get_ExistingJob_ReturnsJob()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            var retrieved = service.Get(job.Id);
            retrieved.Should().NotBeNull();
            retrieved!.Name.Should().Be("Test Job");
        }

        [Fact]
        public void Get_NonExistentJob_ReturnsNull()
        {
            var service = CreateService();
            service.Load();

            service.Get("nonexistent").Should().BeNull();
        }

        #endregion

        #region Reference Queries

        [Fact]
        public void GetJobsReferencingPreset_ReturnsMatchingJobs()
        {
            var service = CreateService();
            service.Load();

            var job1 = new JobDefinition { Name = "Job1", TargetType = JobTargetType.Preset, TargetName = "MyPreset" };
            var job2 = new JobDefinition { Name = "Job2", TargetType = JobTargetType.Preset, TargetName = "OtherPreset" };
            var job3 = new JobDefinition { Name = "Job3", TargetType = JobTargetType.Preset, TargetName = "mypreset" }; // case-insensitive
            var job4 = new JobDefinition { Name = "Job4", TargetType = JobTargetType.Folder, TargetName = "MyPreset" }; // wrong type
            service.Save(job1);
            service.Save(job2);
            service.Save(job3);
            service.Save(job4);

            var results = service.GetJobsReferencingPreset("MyPreset");
            results.Should().HaveCount(2);
            results.Select(j => j.Id).Should().BeEquivalentTo(new[] { job1.Id, job3.Id });
        }

        [Fact]
        public void GetJobsReferencingFolder_ReturnsMatchingJobs()
        {
            var service = CreateService();
            service.Load();

            var job1 = new JobDefinition { Name = "Job1", TargetType = JobTargetType.Folder, TargetName = "MyFolder" };
            var job2 = new JobDefinition { Name = "Job2", TargetType = JobTargetType.Folder, TargetName = "myfolder" }; // case-insensitive
            var job3 = new JobDefinition { Name = "Job3", TargetType = JobTargetType.Preset, TargetName = "MyFolder" }; // wrong type
            service.Save(job1);
            service.Save(job2);
            service.Save(job3);

            var results = service.GetJobsReferencingFolder("MyFolder");
            results.Should().HaveCount(2);
            results.Select(j => j.Id).Should().BeEquivalentTo(new[] { job1.Id, job2.Id });
        }

        #endregion

        #region Persistence Roundtrip

        [Fact]
        public void Save_ThenLoad_InNewService_JobsSurvive()
        {
            var service1 = CreateService();
            service1.Load();

            var job = CreateTestJob();
            job.Hosts.Add(new Dictionary<string, string> { ["Host_IP"] = "10.0.0.1" });
            job.HostColumns.Add("Host_IP");
            service1.Save(job);

            var service2 = CreateService();
            service2.Load();

            service2.Jobs.Should().ContainKey(job.Id);
            service2.Jobs[job.Id].Hosts.Should().HaveCount(1);
            service2.Jobs[job.Id].Hosts[0]["Host_IP"].Should().Be("10.0.0.1");
        }

        #endregion

        #region ImportHostsFromCsv

        [Fact]
        public void ImportHostsFromCsv_ValidFile_PopulatesJobHosts()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            var csvPath = Path.Combine(_tempDir, "hosts.csv");
            File.WriteAllText(csvPath, "Host_IP,port,username\n10.0.0.1,22,admin\n10.0.0.2,2222,root\n");

            service.ImportHostsFromCsv(job.Id, csvPath);

            var updated = service.Get(job.Id)!;
            updated.Hosts.Should().HaveCount(2);
            updated.Hosts[0]["Host_IP"].Should().Be("10.0.0.1");
            updated.Hosts[0]["port"].Should().Be("22");
            updated.Hosts[1]["Host_IP"].Should().Be("10.0.0.2");
            updated.HostColumns.Should().BeEquivalentTo(new[] { "Host_IP", "port", "username" });
        }

        [Fact]
        public void ImportHostsFromCsv_MissingHostIPColumn_ThrowsArgumentException()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            var csvPath = Path.Combine(_tempDir, "bad.csv");
            File.WriteAllText(csvPath, "name,port\nserver1,22\n");

            var act = () => service.ImportHostsFromCsv(job.Id, csvPath);
            act.Should().Throw<ArgumentException>().WithMessage("*Host_IP*");
        }

        [Fact]
        public void ImportHostsFromCsv_NonExistentJob_ThrowsKeyNotFoundException()
        {
            var service = CreateService();
            service.Load();

            var csvPath = Path.Combine(_tempDir, "hosts.csv");
            File.WriteAllText(csvPath, "Host_IP\n10.0.0.1\n");

            var act = () => service.ImportHostsFromCsv("nonexistent", csvPath);
            act.Should().Throw<KeyNotFoundException>();
        }

        [Fact]
        public void ImportHostsFromCsv_QuotedFieldsWithCommas_ParsesCorrectly()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            var csvPath = Path.Combine(_tempDir, "quoted.csv");
            File.WriteAllText(csvPath, "Host_IP,description\n10.0.0.1,\"server, main\"\n10.0.0.2,plain\n");

            service.ImportHostsFromCsv(job.Id, csvPath);

            var updated = service.Get(job.Id)!;
            updated.Hosts[0]["description"].Should().Be("server, main");
            updated.Hosts[1]["description"].Should().Be("plain");
        }

        [Fact]
        public void ImportHostsFromCsv_EmptyFile_SetsEmptyHosts()
        {
            var service = CreateService();
            service.Load();

            var job = CreateTestJob();
            service.Save(job);

            var csvPath = Path.Combine(_tempDir, "empty.csv");
            File.WriteAllText(csvPath, "Host_IP\n");

            service.ImportHostsFromCsv(job.Id, csvPath);

            var updated = service.Get(job.Id)!;
            updated.Hosts.Should().BeEmpty();
            updated.HostColumns.Should().Contain("Host_IP");
        }

        #endregion

        #region ExtractHostDataFromRows

        [Fact]
        public void ExtractHostDataFromRows_ValidInput_ReturnsCopiedData()
        {
            var rows = new List<Dictionary<string, string>>
            {
                new() { ["Host_IP"] = "10.0.0.1", ["port"] = "22" },
                new() { ["Host_IP"] = "10.0.0.2", ["port"] = "2222" }
            };
            var columns = new List<string> { "Host_IP", "port" };

            var (hosts, cols) = JobStorageService.ExtractHostDataFromRows(rows, columns);

            hosts.Should().HaveCount(2);
            hosts[0]["Host_IP"].Should().Be("10.0.0.1");
            cols.Should().BeEquivalentTo(columns);
        }

        [Fact]
        public void ExtractHostDataFromRows_EmptyInput_ReturnsEmptyLists()
        {
            var (hosts, cols) = JobStorageService.ExtractHostDataFromRows(
                new List<Dictionary<string, string>>(),
                new List<string>());

            hosts.Should().BeEmpty();
            cols.Should().BeEmpty();
        }

        [Fact]
        public void ExtractHostDataFromRows_PreservesCustomColumns()
        {
            var rows = new List<Dictionary<string, string>>
            {
                new() { ["Host_IP"] = "10.0.0.1", ["custom_var"] = "value1" }
            };
            var columns = new List<string> { "Host_IP", "custom_var" };

            var (hosts, cols) = JobStorageService.ExtractHostDataFromRows(rows, columns);

            hosts[0].Should().ContainKey("custom_var");
            hosts[0]["custom_var"].Should().Be("value1");
            cols.Should().Contain("custom_var");
        }

        #endregion
    }
}
