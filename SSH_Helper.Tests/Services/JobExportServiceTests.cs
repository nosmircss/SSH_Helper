using FluentAssertions;
using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services
{
    public class JobExportServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly JobExportService _service;

        public JobExportServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"JobExportTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _service = new JobExportService();
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }

        private static JobDefinition CreateTestJob(string name = "Test Job",
            CredentialMode credMode = CredentialMode.InheritFromApp)
        {
            return new JobDefinition
            {
                Name = name,
                TargetType = JobTargetType.Preset,
                TargetName = "MyPreset",
                TargetContentHash = "abc123",
                CronExpression = "0 * * * *",
                ScheduleType = ScheduleType.Recurring,
                CredentialMode = credMode,
                Hosts = new List<Dictionary<string, string>>
                {
                    new() { { "Host_IP", "10.0.0.1" } }
                },
                HostColumns = new List<string> { "Host_IP" }
            };
        }

        #region ExportToFile / ImportFromFile Round-Trip

        [Fact]
        public void ExportToFile_CreatesValidJsonWithVersionAndJobs()
        {
            var jobs = new List<JobDefinition> { CreateTestJob() };
            var filePath = Path.Combine(_tempDir, "test.sshjobs");

            _service.ExportToFile(jobs, filePath);

            File.Exists(filePath).Should().BeTrue();
            var json = File.ReadAllText(filePath);
            var doc = JsonConvert.DeserializeObject<JobExportDocument>(json);
            doc.Should().NotBeNull();
            doc!.Version.Should().Be(1);
            doc.ExportedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            doc.Jobs.Should().HaveCount(1);
            doc.Jobs[0].Name.Should().Be("Test Job");
        }

        [Fact]
        public void ExportToFile_ImportFromFile_RoundTripsCorrectly()
        {
            var jobs = new List<JobDefinition> { CreateTestJob("Job1"), CreateTestJob("Job2") };
            var filePath = Path.Combine(_tempDir, "roundtrip.sshjobs");

            _service.ExportToFile(jobs, filePath);
            var imported = _service.ImportFromFile(filePath);

            imported.Should().HaveCount(2);
            imported[0].Name.Should().Be("Job1");
            imported[1].Name.Should().Be("Job2");
        }

        #endregion

        #region ExportToString / ImportFromString Round-Trip

        [Fact]
        public void ExportToString_ProducesNonEmptyBase64String()
        {
            var jobs = new List<JobDefinition> { CreateTestJob() };

            var encoded = _service.ExportToString(jobs);

            encoded.Should().NotBeNullOrWhiteSpace();
            // Verify it's valid Base64
            var act = () => Convert.FromBase64String(encoded);
            act.Should().NotThrow();
        }

        [Fact]
        public void ExportToString_ImportFromString_RoundTripsCorrectly()
        {
            var jobs = new List<JobDefinition> { CreateTestJob("StringJob") };

            var encoded = _service.ExportToString(jobs);
            var imported = _service.ImportFromString(encoded);

            imported.Should().HaveCount(1);
            imported[0].Name.Should().Be("StringJob");
            imported[0].TargetName.Should().Be("MyPreset");
            imported[0].CronExpression.Should().Be("0 * * * *");
        }

        #endregion

        #region Credential Stripping

        [Fact]
        public void Export_ResetsCredentialModeToInheritFromApp()
        {
            var job = CreateTestJob(credMode: CredentialMode.Stored);
            var jobs = new List<JobDefinition> { job };

            var encoded = _service.ExportToString(jobs);
            var imported = _service.ImportFromString(encoded);

            imported[0].CredentialMode.Should().Be(CredentialMode.InheritFromApp);
        }

        [Fact]
        public void Export_ResetsCredentialMode_PerHostColumn()
        {
            var job = CreateTestJob(credMode: CredentialMode.PerHostColumn);
            var jobs = new List<JobDefinition> { job };
            var filePath = Path.Combine(_tempDir, "cred.sshjobs");

            _service.ExportToFile(jobs, filePath);
            var imported = _service.ImportFromFile(filePath);

            imported[0].CredentialMode.Should().Be(CredentialMode.InheritFromApp);
        }

        #endregion

        #region Running State Stripping

        [Fact]
        public void Export_NullsRunningState()
        {
            var job = CreateTestJob();
            job.RunningState = new RunningJobState { StartedUtc = DateTime.UtcNow };
            var jobs = new List<JobDefinition> { job };

            var encoded = _service.ExportToString(jobs);
            var imported = _service.ImportFromString(encoded);

            imported[0].RunningState.Should().BeNull();
        }

        #endregion

        #region Drift Warning Stripping

        [Fact]
        public void Export_ClearsHasDriftWarning()
        {
            var job = CreateTestJob();
            job.HasDriftWarning = true;
            var jobs = new List<JobDefinition> { job };

            var encoded = _service.ExportToString(jobs);
            var imported = _service.ImportFromString(encoded);

            imported[0].HasDriftWarning.Should().BeFalse();
        }

        #endregion

        #region Field Preservation

        [Fact]
        public void Export_PreservesAllOtherFields()
        {
            var job = CreateTestJob();
            job.ScheduleType = ScheduleType.Recurring;
            job.CronExpression = "0 */2 * * *";
            job.StopOnError = true;
            job.FolderExecutionMode = FolderExecutionMode.Parallel;
            job.MaxHistoryRuns = 50;
            job.HistoryRetentionDays = 90;
            job.IsEnabled = false;
            job.DisabledReason = "test reason";
            var jobs = new List<JobDefinition> { job };

            var encoded = _service.ExportToString(jobs);
            var imported = _service.ImportFromString(encoded);

            var result = imported[0];
            result.ScheduleType.Should().Be(ScheduleType.Recurring);
            result.CronExpression.Should().Be("0 */2 * * *");
            result.StopOnError.Should().BeTrue();
            result.FolderExecutionMode.Should().Be(FolderExecutionMode.Parallel);
            result.MaxHistoryRuns.Should().Be(50);
            result.HistoryRetentionDays.Should().Be(90);
            result.IsEnabled.Should().BeFalse();
            result.DisabledReason.Should().Be("test reason");
            result.TargetName.Should().Be("MyPreset");
            result.TargetContentHash.Should().Be("abc123");
        }

        #endregion

        #region Import Conflict Detection

        [Fact]
        public void PrepareImport_DetectsNameConflictsAndSuffixes()
        {
            var importJobs = new List<JobDefinition>
            {
                CreateTestJob("ExistingJob"),
                CreateTestJob("NewJob")
            };
            var existingNames = new HashSet<string> { "ExistingJob" };

            var entries = _service.PrepareImport(importJobs, existingNames);

            entries.Should().HaveCount(2);

            var conflicting = entries.First(e => e.HasConflict);
            conflicting.ResolvedName.Should().Be("ExistingJob (imported)");

            var nonConflicting = entries.First(e => !e.HasConflict);
            nonConflicting.ResolvedName.Should().Be("NewJob");
        }

        [Fact]
        public void PrepareImport_UsesNumberedImportedSuffixWhenFirstSuffixAlreadyExists()
        {
            var importJobs = new List<JobDefinition>
            {
                CreateTestJob("ExistingJob")
            };
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ExistingJob",
                "ExistingJob (imported)"
            };

            var entries = _service.PrepareImport(importJobs, existingNames);

            entries.Should().ContainSingle();
            entries[0].HasConflict.Should().BeTrue();
            entries[0].ResolvedName.Should().Be("ExistingJob (imported 2)");
        }

        [Fact]
        public void PrepareImport_ReservesNamesAcrossImportedBatch()
        {
            var importJobs = new List<JobDefinition>
            {
                CreateTestJob("SharedJob"),
                CreateTestJob("SharedJob"),
                CreateTestJob("SharedJob")
            };

            var entries = _service.PrepareImport(importJobs, Array.Empty<string>());

            entries.Select(entry => entry.ResolvedName).Should().Equal(
                "SharedJob",
                "SharedJob (imported)",
                "SharedJob (imported 2)");
        }

        [Fact]
        public void PrepareImport_GeneratesNewGuids()
        {
            var job = CreateTestJob("SomeJob");
            var originalId = job.Id;
            var importJobs = new List<JobDefinition> { job };

            var entries = _service.PrepareImport(importJobs, new HashSet<string>());

            entries[0].Job.Id.Should().NotBe(originalId);
            entries[0].Job.Id.Should().HaveLength(32); // GUID "N" format
        }

        #endregion

        #region Corrupt Input Handling

        [Fact]
        public void ImportFromFile_InvalidJson_ReturnsEmptyList()
        {
            var filePath = Path.Combine(_tempDir, "corrupt.sshjobs");
            File.WriteAllText(filePath, "this is not valid json {{{");

            var result = _service.ImportFromFile(filePath);

            result.Should().BeEmpty();
        }

        [Fact]
        public void ImportFromFile_MissingFile_ReturnsEmptyList()
        {
            var result = _service.ImportFromFile(Path.Combine(_tempDir, "nonexistent.sshjobs"));

            result.Should().BeEmpty();
        }

        [Fact]
        public void ImportFromString_InvalidBase64_ReturnsEmptyList()
        {
            var result = _service.ImportFromString("not-valid-base64!!!");

            result.Should().BeEmpty();
        }

        [Fact]
        public void ImportFromString_EmptyString_ReturnsEmptyList()
        {
            var result = _service.ImportFromString("");

            result.Should().BeEmpty();
        }

        [Fact]
        public void ImportFromString_NullString_ReturnsEmptyList()
        {
            var result = _service.ImportFromString(null!);

            result.Should().BeEmpty();
        }

        #endregion

        #region Export Does Not Modify Source

        [Fact]
        public void ExportToString_DoesNotModifySourceJobs()
        {
            var job = CreateTestJob(credMode: CredentialMode.Stored);
            job.RunningState = new RunningJobState { StartedUtc = DateTime.UtcNow };
            job.HasDriftWarning = true;
            var originalId = job.Id;
            var jobs = new List<JobDefinition> { job };

            _service.ExportToString(jobs);

            // Source job should be untouched
            job.CredentialMode.Should().Be(CredentialMode.Stored);
            job.RunningState.Should().NotBeNull();
            job.HasDriftWarning.Should().BeTrue();
            job.Id.Should().Be(originalId);
        }

        #endregion
    }
}
