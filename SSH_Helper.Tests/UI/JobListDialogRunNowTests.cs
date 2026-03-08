using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class JobListDialogRunNowTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly string _jobsPath;

    public JobListDialogRunNowTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobListDialogRunNow_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _jobsPath = Path.Combine(_testDirectory, "jobs.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [WinFormsFact]
    public void RunNowButton_UsesProvidedRunNowInvoker()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = new JobDefinition
        {
            Name = "Run Now Job",
            TargetType = JobTargetType.Preset,
            TargetName = "Nightly",
            HostColumns = new List<string> { CsvManager.HostColumnName },
            Hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "10.0.0.9"
                }
            }
        };
        jobStorage.Save(job);

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        string? invokedJobId = null;

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            jobId =>
            {
                invokedJobId = jobId;
                return Task.FromResult(true);
            },
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var grid = GetField<DataGridView>(dialog, "_gridJobs");
        grid.Rows.Count.Should().Be(1);
        grid.Rows[0].Selected = true;
        grid.CurrentCell = grid.Rows[0].Cells[0];

        var toolStrip = GetField<ToolStrip>(dialog, "_toolStrip");
        var runNowButton = toolStrip.Items
            .OfType<ToolStripButton>()
            .Single(item => string.Equals(item.Name, "RunNow", StringComparison.Ordinal));

        runNowButton.PerformClick();
        Application.DoEvents();

        SpinWait.SpinUntil(() => invokedJobId != null, millisecondsTimeout: 1000).Should().BeTrue();
        invokedJobId.Should().Be(job.Id);
    }

    [WinFormsFact]
    public void HistoryGrid_UsesPersistedStartedUtcAndDerivedDuration()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = new JobDefinition
        {
            Name = "History Job",
            TargetType = JobTargetType.Preset,
            TargetName = "Nightly",
            HostColumns = new List<string> { CsvManager.HostColumnName },
            Hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "10.0.0.9"
                }
            }
        };
        jobStorage.Save(job);

        var startedUtc = new DateTime(2026, 3, 8, 13, 15, 0, DateTimeKind.Utc);
        historyService.SaveRun(new JobRunResult
        {
            JobId = job.Id,
            JobName = job.Name,
            StartedUtc = startedUtc,
            CompletedUtc = startedUtc.AddMinutes(2).AddSeconds(5),
            Success = true,
            HostsSucceeded = 1,
            HostsFailed = 0,
            HostOutputs = new List<JobHostOutput>()
        });

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        jobsGrid.Rows.Count.Should().Be(1);
        jobsGrid.Rows[0].Selected = true;
        jobsGrid.CurrentCell = jobsGrid.Rows[0].Cells[0];
        InvokeMethod(dialog, "RefreshHistory");
        Application.DoEvents();

        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");
        historyGrid.Rows.Count.Should().Be(1);
        historyGrid.Rows[0].Cells["Started"].Value.Should().Be(startedUtc.ToLocalTime().ToString("g"));
        historyGrid.Rows[0].Cells["Duration"].Value.Should().Be("02:05");
    }

    [WinFormsFact]
    public void HistoryGrid_SingleSkippedSummary_RendersSkippedWithoutCount()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = CreateTestJob("Single Skipped Summary Job");
        jobStorage.Save(job);

        var missedUtc = new DateTime(2026, 3, 8, 13, 5, 0, DateTimeKind.Utc);
        historyService.SaveSkippedRunSummary(new SkippedRunSummaryEntry
        {
            JobId = job.Id,
            JobName = job.Name,
            MissedRunCount = 1,
            FirstScheduledTimeUtc = missedUtc,
            LastScheduledTimeUtc = missedUtc
        });

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");
        var viewOutputButton = GetField<Button>(dialog, "_btnViewOutput");

        SelectJobRow(jobsGrid, job.Id);
        InvokeMethod(dialog, "OnJobSelectionChanged", jobsGrid, EventArgs.Empty);
        Application.DoEvents();

        historyGrid.Rows.Count.Should().Be(1);
        historyGrid.Rows[0].Cells["Started"].Value.Should().Be(missedUtc.ToLocalTime().ToString("g"));
        historyGrid.Rows[0].Cells["Duration"].Value.Should().Be("00:00");
        historyGrid.Rows[0].Cells["Result"].Value.Should().Be("SKIPPED");
        historyGrid.Rows[0].Cells["Error"].Value.Should().Be(
            $"Missed 1 scheduled run at {missedUtc.ToLocalTime():g} while the application was closed.");
        viewOutputButton.Enabled.Should().BeFalse();
    }

    [WinFormsFact]
    public void HistoryGrid_SkippedSummary_RendersCompactlyAndDisablesViewOutput()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = CreateTestJob("Skipped Summary Job");
        jobStorage.Save(job);

        var firstMissedUtc = new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        var lastMissedUtc = new DateTime(2026, 3, 8, 12, 10, 0, DateTimeKind.Utc);
        historyService.SaveSkippedRunSummary(new SkippedRunSummaryEntry
        {
            JobId = job.Id,
            JobName = job.Name,
            MissedRunCount = 3,
            FirstScheduledTimeUtc = firstMissedUtc,
            LastScheduledTimeUtc = lastMissedUtc
        });

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");
        var viewOutputButton = GetField<Button>(dialog, "_btnViewOutput");

        SelectJobRow(jobsGrid, job.Id);
        InvokeMethod(dialog, "OnJobSelectionChanged", jobsGrid, EventArgs.Empty);
        Application.DoEvents();

        jobsGrid.Rows[0].Cells["LastResult"].Value.Should().Be("SKIPPED (3)");
        historyGrid.Rows.Count.Should().Be(1);
        historyGrid.Rows[0].Cells["Started"].Value.Should().Be(lastMissedUtc.ToLocalTime().ToString("g"));
        historyGrid.Rows[0].Cells["Duration"].Value.Should().Be("00:00");
        historyGrid.Rows[0].Cells["Result"].Value.Should().Be("SKIPPED (3)");
        historyGrid.Rows[0].Cells["Error"].Value.Should().Be(
            $"Missed 3 scheduled runs while the application was closed. Range: {firstMissedUtc.ToLocalTime():g} to {lastMissedUtc.ToLocalTime():g}.");
        viewOutputButton.Enabled.Should().BeFalse();
    }

    [WinFormsFact]
    public void HistoryGrid_LegacySkippedEntry_StillRendersSkipped()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = CreateTestJob("Legacy Skipped Job");
        jobStorage.Save(job);

        historyService.SaveSkippedRun(new SkippedRunEntry
        {
            JobId = job.Id,
            JobName = job.Name,
            ScheduledTimeUtc = new DateTime(2026, 3, 8, 11, 55, 0, DateTimeKind.Utc)
        }, errorMessage: "Missed while closed");

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");

        SelectJobRow(jobsGrid, job.Id);
        InvokeMethod(dialog, "OnJobSelectionChanged", jobsGrid, EventArgs.Empty);
        Application.DoEvents();

        historyGrid.Rows.Count.Should().Be(1);
        historyGrid.Rows[0].Cells["Result"].Value.Should().Be("SKIPPED");
    }

    [WinFormsFact]
    public void HistoryGrid_ConsecutiveIdenticalFailures_RenderCollapsedFailureCount()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = CreateTestJob("Repeated Failure Job");
        jobStorage.Save(job);

        historyService.SaveRun(new JobRunResult
        {
            JobId = job.Id,
            JobName = job.Name,
            StartedUtc = new DateTime(2026, 3, 8, 15, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 3, 8, 15, 0, 30, DateTimeKind.Utc),
            Success = false,
            HostsSucceeded = 0,
            HostsFailed = 1,
            ErrorMessage = "Authentication failed",
            HostOutputs = new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.9",
                    Output = "first auth failure",
                    Success = false,
                    ErrorMessage = "Authentication failed"
                }
            }
        });

        historyService.SaveRun(new JobRunResult
        {
            JobId = job.Id,
            JobName = job.Name,
            StartedUtc = new DateTime(2026, 3, 8, 15, 5, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 3, 8, 15, 5, 20, DateTimeKind.Utc),
            Success = false,
            HostsSucceeded = 0,
            HostsFailed = 1,
            ErrorMessage = "Authentication failed",
            HostOutputs = new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.9",
                    Output = "second auth failure",
                    Success = false,
                    ErrorMessage = "Authentication failed"
                }
            }
        });

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");

        SelectJobRow(jobsGrid, job.Id);
        InvokeMethod(dialog, "OnJobSelectionChanged", jobsGrid, EventArgs.Empty);
        Application.DoEvents();

        jobsGrid.Rows[0].Cells["LastResult"].Value.Should().Be("FAIL x2 (0/1)");
        historyGrid.Rows.Count.Should().Be(1);
        historyGrid.Rows[0].Cells["Result"].Value.Should().Be("FAIL x2 (0/1)");
        historyGrid.Rows[0].Cells["Error"].Value.Should().Be("Authentication failed");
    }

    [WinFormsFact]
    public void HistoryGrid_PopulatesOnInitialLoad_WithoutManualSelection()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = new JobDefinition
        {
            Name = "Initial History Job",
            TargetType = JobTargetType.Preset,
            TargetName = "Nightly",
            HostColumns = new List<string> { CsvManager.HostColumnName },
            Hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "10.0.0.9"
                }
            }
        };
        jobStorage.Save(job);

        historyService.SaveRun(new JobRunResult
        {
            JobId = job.Id,
            JobName = job.Name,
            StartedUtc = new DateTime(2026, 3, 8, 14, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 3, 8, 14, 1, 0, DateTimeKind.Utc),
            Success = true,
            HostsSucceeded = 1,
            HostsFailed = 0,
            HostOutputs = new List<JobHostOutput>()
        });

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");

        jobsGrid.Rows.Count.Should().Be(1);
        jobsGrid.SelectedRows.Count.Should().Be(1);
        jobsGrid.CurrentRow.Should().NotBeNull();
        jobsGrid.CurrentRow!.Tag.Should().Be(job.Id);
        historyGrid.Rows.Count.Should().Be(1);
    }

    [WinFormsFact]
    public void CompletionRefresh_PreservesActiveJobAndVisibleHistory()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var alphaJob = CreateTestJob("Alpha Job");
        var betaJob = CreateTestJob("Beta Job");
        jobStorage.Save(alphaJob);
        jobStorage.Save(betaJob);

        historyService.SaveRun(new JobRunResult
        {
            JobId = betaJob.Id,
            JobName = betaJob.Name,
            StartedUtc = new DateTime(2026, 3, 8, 14, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 3, 8, 14, 1, 0, DateTimeKind.Utc),
            Success = true,
            HostsSucceeded = 1,
            HostsFailed = 0,
            HostOutputs = new List<JobHostOutput>()
        });

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");

        SelectJobRow(jobsGrid, betaJob.Id);
        InvokeMethod(dialog, "OnJobSelectionChanged", jobsGrid, EventArgs.Empty);
        Application.DoEvents();

        historyGrid.Rows.Count.Should().Be(1);
        jobsGrid.CurrentRow.Should().NotBeNull();
        jobsGrid.CurrentRow!.Tag.Should().Be(betaJob.Id);

        var completedResult = new JobRunResult
        {
            JobId = betaJob.Id,
            JobName = betaJob.Name,
            StartedUtc = new DateTime(2026, 3, 8, 15, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 3, 8, 15, 2, 30, DateTimeKind.Utc),
            Success = true,
            HostsSucceeded = 1,
            HostsFailed = 0,
            HostOutputs = new List<JobHostOutput>()
        };
        historyService.SaveRun(completedResult);

        InvokeMethod(dialog, "OnJobCompletedExternal", null, completedResult);
        Application.DoEvents();

        jobsGrid.CurrentRow.Should().NotBeNull();
        jobsGrid.CurrentRow!.Tag.Should().Be(betaJob.Id);
        jobsGrid.SelectedRows.Count.Should().Be(1);
        historyGrid.Rows.Count.Should().Be(2);
    }

    [WinFormsFact]
    public void RefreshJobList_PreservesSelectedNonFirstHistoryRun()
    {
        var configService = new ConfigurationService(_configPath);
        var presetManager = new PresetManager(configService);
        presetManager.Load();
        presetManager.Save("Nightly", new PresetInfo { Commands = "echo nightly" });

        var credentialProvider = new FakeCredentialProvider();
        var schedulingService = new SchedulingService();
        var historyService = new JobHistoryService(Path.Combine(_testDirectory, "history"));
        var exportService = new JobExportService();
        var jobStorage = new JobStorageService(credentialProvider, _jobsPath);
        jobStorage.Load();

        var job = CreateTestJob("History Selection Job");
        jobStorage.Save(job);

        historyService.SaveRun(new JobRunResult
        {
            JobId = job.Id,
            JobName = job.Name,
            StartedUtc = new DateTime(2026, 3, 8, 14, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 3, 8, 14, 1, 0, DateTimeKind.Utc),
            Success = true,
            HostsSucceeded = 1,
            HostsFailed = 0,
            HostOutputs = new List<JobHostOutput>()
        });

        historyService.SaveRun(new JobRunResult
        {
            JobId = job.Id,
            JobName = job.Name,
            StartedUtc = new DateTime(2026, 3, 8, 15, 0, 0, DateTimeKind.Utc),
            CompletedUtc = new DateTime(2026, 3, 8, 15, 2, 0, DateTimeKind.Utc),
            Success = true,
            HostsSucceeded = 1,
            HostsFailed = 0,
            HostOutputs = new List<JobHostOutput>()
        });

        var runs = historyService.GetRunsForJob(job.Id);
        runs.Count.Should().Be(2);
        var selectedRunFileName = runs[1].RunFileName;

        using var executionService = new JobExecutionService(
            jobStorage,
            schedulingService,
            configService,
            presetManager,
            credentialProvider);

        using var dialog = new JobListDialog(
            jobStorage,
            executionService,
            historyService,
            schedulingService,
            presetManager,
            exportService,
            credentialProvider,
            runNowInvoker: null,
            getMainGridRows: null,
            getMainGridColumns: null,
            darkMode: false);

        dialog.Show();
        Application.DoEvents();

        var jobsGrid = GetField<DataGridView>(dialog, "_gridJobs");
        var historyGrid = GetField<DataGridView>(dialog, "_gridHistory");

        SelectJobRow(jobsGrid, job.Id);
        InvokeMethod(dialog, "OnJobSelectionChanged", jobsGrid, EventArgs.Empty);
        Application.DoEvents();

        historyGrid.Rows.Count.Should().Be(2);
        SelectHistoryRow(historyGrid, selectedRunFileName);
        InvokeMethod(dialog, "OnHistorySelectionChanged", historyGrid, EventArgs.Empty);
        Application.DoEvents();

        historyGrid.CurrentRow.Should().NotBeNull();
        historyGrid.CurrentRow!.Tag.Should().Be(selectedRunFileName);

        InvokeMethod(dialog, "RefreshJobList");
        Application.DoEvents();

        historyGrid.SelectedRows.Count.Should().Be(1);
        historyGrid.CurrentRow.Should().NotBeNull();
        historyGrid.CurrentRow!.Tag.Should().Be(selectedRunFileName);
        historyGrid.SelectedRows[0].Tag.Should().Be(selectedRunFileName);
    }

    private static JobDefinition CreateTestJob(string name)
    {
        return new JobDefinition
        {
            Name = name,
            TargetType = JobTargetType.Preset,
            TargetName = "Nightly",
            HostColumns = new List<string> { CsvManager.HostColumnName },
            Hosts = new List<Dictionary<string, string>>
            {
                new()
                {
                    [CsvManager.HostColumnName] = "10.0.0.9"
                }
            }
        };
    }

    private static void SelectJobRow(DataGridView grid, string jobId)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!string.Equals(row.Tag as string, jobId, StringComparison.Ordinal))
                continue;

            grid.ClearSelection();
            row.Selected = true;
            grid.CurrentCell = row.Cells[0];
            return;
        }

        throw new InvalidOperationException($"Could not find job row '{jobId}'.");
    }

    private static void SelectHistoryRow(DataGridView grid, string runFileName)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (!string.Equals(row.Tag as string, runFileName, StringComparison.Ordinal))
                continue;

            grid.ClearSelection();
            row.Selected = true;
            grid.CurrentCell = row.Cells[0];
            return;
        }

        throw new InvalidOperationException($"Could not find history row '{runFileName}'.");
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }

    private static void InvokeMethod(object obj, string methodName, params object?[]? args)
    {
        args ??= Array.Empty<object?>();
        var methods = obj.GetType()
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
            .ToList();

        var method = methods.SingleOrDefault(candidate => candidate.GetParameters().Length == args.Length);
        method.Should().NotBeNull($"method '{methodName}' should exist on {obj.GetType().Name}");
        method!.Invoke(obj, args);
    }

    private sealed class FakeCredentialProvider : ICredentialProvider
    {
        public bool IsAvailable => true;

        public bool TryGetPassword(string target, out string username, out string password)
        {
            username = string.Empty;
            password = string.Empty;
            return false;
        }

        public bool SavePassword(string target, string username, string password, string? comment = null)
        {
            return true;
        }

        public bool DeletePassword(string target)
        {
            return true;
        }
    }
}
