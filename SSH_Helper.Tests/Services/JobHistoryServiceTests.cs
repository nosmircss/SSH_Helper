using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public sealed class JobHistoryServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _historyPath;
    private readonly JobHistoryService _service;

    public JobHistoryServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"JobHistoryTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _historyPath = Path.Combine(_testDirectory, "job-history");
        _service = new JobHistoryService(_historyPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    #region Test Helpers

    private static JobRunResult CreateTestResult(
        string jobId = "testjob1",
        string jobName = "Test Job",
        bool success = true,
        bool wasCancelled = false,
        int hostsSucceeded = 2,
        int hostsFailed = 0,
        List<JobHostOutput>? hostOutputs = null,
        string? errorMessage = null)
    {
        var now = DateTime.UtcNow;
        return new JobRunResult
        {
            JobId = jobId,
            JobName = jobName,
            StartedUtc = now.AddSeconds(-5),
            CompletedUtc = now,
            Success = success,
            WasCancelled = wasCancelled,
            HostsSucceeded = hostsSucceeded,
            HostsFailed = hostsFailed,
            ErrorMessage = errorMessage,
            HostOutputs = hostOutputs ?? new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "Router config backup complete",
                    Success = true
                },
                new()
                {
                    HostAddress = "10.0.0.2",
                    Output = "Switch interface status check",
                    Success = true
                }
            }
        };
    }

    #endregion

    #region HIST-01: Run Record Persistence

    [Fact]
    public void SaveRun_PersistsRunRecord()
    {
        var result = CreateTestResult();

        _service.SaveRun(result);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(1);
        runs[0].JobId.Should().Be("testjob1");
        runs[0].JobName.Should().Be("Test Job");
        runs[0].Success.Should().BeTrue();
        runs[0].HostsSucceeded.Should().Be(2);
        runs[0].HostsFailed.Should().Be(0);
        runs[0].StartedUtc.Should().BeCloseTo(result.StartedUtc, TimeSpan.FromSeconds(1));
        runs[0].CompletedUtc.Should().BeCloseTo(result.CompletedUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SaveRun_PersistsPayloadFile()
    {
        var result = CreateTestResult();

        _service.SaveRun(result);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(1);

        var payloadPath = Path.Combine(_historyPath, "testjob1", runs[0].RunFileName);
        File.Exists(payloadPath).Should().BeTrue("payload file should exist on disk");
    }

    [Fact]
    public void SaveRun_MultipleRuns_OrderedNewestFirst()
    {
        var result1 = CreateTestResult(jobName: "Run 1");
        result1.CompletedUtc = DateTime.UtcNow.AddMinutes(-2);
        result1.StartedUtc = result1.CompletedUtc.AddSeconds(-5);

        var result2 = CreateTestResult(jobName: "Run 2");
        result2.CompletedUtc = DateTime.UtcNow.AddMinutes(-1);
        result2.StartedUtc = result2.CompletedUtc.AddSeconds(-5);

        var result3 = CreateTestResult(jobName: "Run 3");
        result3.CompletedUtc = DateTime.UtcNow;
        result3.StartedUtc = result3.CompletedUtc.AddSeconds(-5);

        _service.SaveRun(result1);
        _service.SaveRun(result2);
        _service.SaveRun(result3);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(3);
        runs[0].JobName.Should().Be("Run 3");
        runs[1].JobName.Should().Be("Run 2");
        runs[2].JobName.Should().Be("Run 1");
    }

    [Fact]
    public void SaveRun_NullHostOutputs_HandlesGracefully()
    {
        var result = CreateTestResult(hostOutputs: null!);
        result.HostOutputs = null;

        _service.SaveRun(result);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(1);

        var payload = _service.LoadRunPayload("testjob1", runs[0].RunFileName);
        payload.Should().NotBeNull();
        payload!.HostOutputs.Should().BeEmpty();
    }

    [Fact]
    public void SaveRun_CreatesJobSubdirectory()
    {
        var jobDir = Path.Combine(_historyPath, "newjob123");
        Directory.Exists(jobDir).Should().BeFalse("job directory should not exist before first save");

        var result = CreateTestResult(jobId: "newjob123");

        _service.SaveRun(result);

        Directory.Exists(jobDir).Should().BeTrue("job directory should be created by SaveRun");
    }

    [Fact]
    public void SaveRun_ConsecutiveMatchingFailures_CollapsesIntoSingleRecordWithIncrementedCount()
    {
        var firstFailureUtc = new DateTime(2026, 3, 8, 14, 10, 0, DateTimeKind.Utc);
        var secondFailureUtc = firstFailureUtc.AddMinutes(5);

        var firstFailure = CreateTestResult(
            success: false,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "first failure output",
                    Success = false,
                    ErrorMessage = "Authentication failed"
                }
            },
            errorMessage: "Authentication failed");
        firstFailure.StartedUtc = firstFailureUtc.AddSeconds(-30);
        firstFailure.CompletedUtc = firstFailureUtc;

        var secondFailure = CreateTestResult(
            success: false,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "second failure output",
                    Success = false,
                    ErrorMessage = "Authentication failed"
                }
            },
            errorMessage: "Authentication failed");
        secondFailure.StartedUtc = secondFailureUtc.AddSeconds(-20);
        secondFailure.CompletedUtc = secondFailureUtc;

        _service.SaveRun(firstFailure);
        _service.SaveRun(secondFailure);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(1);
        runs[0].Success.Should().BeFalse();
        runs[0].ConsecutiveFailureCount.Should().Be(2);
        runs[0].StartedUtc.Should().Be(secondFailure.StartedUtc);
        runs[0].CompletedUtc.Should().Be(secondFailure.CompletedUtc);
        runs[0].ErrorMessage.Should().Be("Authentication failed");

        var payload = _service.LoadRunPayload("testjob1", runs[0].RunFileName);
        payload.Should().NotBeNull();
        payload!.ConsecutiveFailureCount.Should().Be(2);
        payload.HostOutputs.Should().ContainSingle();
        payload.HostOutputs[0].Output.Should().Be("second failure output");

        var jsonFiles = Directory.GetFiles(Path.Combine(_historyPath, "testjob1"), "*.json");
        jsonFiles.Should().HaveCount(2, "index.json plus one collapsed payload file should remain");
    }

    [Fact]
    public void SaveRun_DifferentFailure_DoesNotCollapse()
    {
        var authFailure = CreateTestResult(
            success: false,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "auth output",
                    Success = false,
                    ErrorMessage = "Authentication failed"
                }
            },
            errorMessage: "Authentication failed");

        var connectionFailure = CreateTestResult(
            success: false,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "connection output",
                    Success = false,
                    ErrorMessage = "Connection failed"
                }
            },
            errorMessage: "Connection failed");

        _service.SaveRun(authFailure);
        _service.SaveRun(connectionFailure);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(2);
        runs[0].ErrorMessage.Should().Be("Connection failed");
        runs[0].ConsecutiveFailureCount.Should().Be(1);
        runs[1].ErrorMessage.Should().Be("Authentication failed");
        runs[1].ConsecutiveFailureCount.Should().Be(1);
    }

    [Fact]
    public void SaveRun_CancelledRuns_DoNotCollapseIntoFailureStreaks()
    {
        var firstCancelled = CreateTestResult(
            success: false,
            wasCancelled: true,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "cancelled output 1",
                    Success = false,
                    WasCancelled = true,
                    ErrorMessage = "Operation cancelled"
                }
            },
            errorMessage: "Cancelled by user.");
        firstCancelled.CompletedUtc = new DateTime(2026, 3, 12, 16, 0, 0, DateTimeKind.Utc);
        firstCancelled.StartedUtc = firstCancelled.CompletedUtc.AddSeconds(-10);

        var secondCancelled = CreateTestResult(
            success: false,
            wasCancelled: true,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "cancelled output 2",
                    Success = false,
                    WasCancelled = true,
                    ErrorMessage = "Operation cancelled"
                }
            },
            errorMessage: "Cancelled by user.");
        secondCancelled.CompletedUtc = firstCancelled.CompletedUtc.AddMinutes(5);
        secondCancelled.StartedUtc = secondCancelled.CompletedUtc.AddSeconds(-8);

        _service.SaveRun(firstCancelled);
        _service.SaveRun(secondCancelled);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(2);
        runs.Should().OnlyContain(run => run.WasCancelled);
        runs[0].ConsecutiveFailureCount.Should().Be(0);
        runs[1].ConsecutiveFailureCount.Should().Be(0);

        var payload = _service.LoadRunPayload("testjob1", runs[0].RunFileName);
        payload.Should().NotBeNull();
        payload!.WasCancelled.Should().BeTrue();
        payload.HostOutputs.Should().ContainSingle();
        payload.HostOutputs[0].WasCancelled.Should().BeTrue();
    }

    [Fact]
    public void SaveRun_SuccessBetweenMatchingFailures_DoesNotCollapseAcrossReset()
    {
        var failure = CreateTestResult(
            success: false,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "auth output",
                    Success = false,
                    ErrorMessage = "Authentication failed"
                }
            },
            errorMessage: "Authentication failed");

        var success = CreateTestResult(
            success: true,
            hostsSucceeded: 1,
            hostsFailed: 0,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "success output",
                    Success = true
                }
            });

        var repeatedFailure = CreateTestResult(
            success: false,
            hostsSucceeded: 0,
            hostsFailed: 1,
            hostOutputs: new List<JobHostOutput>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Output = "auth output again",
                    Success = false,
                    ErrorMessage = "Authentication failed"
                }
            },
            errorMessage: "Authentication failed");

        _service.SaveRun(failure);
        _service.SaveRun(success);
        _service.SaveRun(repeatedFailure);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(3);
        runs[0].Success.Should().BeFalse();
        runs[0].ConsecutiveFailureCount.Should().Be(1);
        runs[1].Success.Should().BeTrue();
        runs[2].Success.Should().BeFalse();
        runs[2].ConsecutiveFailureCount.Should().Be(1);
    }

    [Fact]
    public void SaveSkippedRun_PersistsSkippedHistoryEntry()
    {
        var scheduledUtc = new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        var skipped = new SkippedRunEntry
        {
            JobId = "missed-job",
            JobName = "Missed Job",
            ScheduledTimeUtc = scheduledUtc
        };

        _service.SaveSkippedRun(skipped, errorMessage: "Missed while closed");

        var runs = _service.GetRunsForJob("missed-job");
        runs.Should().HaveCount(1);
        runs[0].WasSkipped.Should().BeTrue();
        runs[0].StartedUtc.Should().Be(scheduledUtc);
        runs[0].CompletedUtc.Should().Be(scheduledUtc);
        runs[0].ErrorMessage.Should().Be("Missed while closed");
        runs[0].SkippedRunCount.Should().Be(0);
        runs[0].SkippedWindowStartUtc.Should().BeNull();
        runs[0].SkippedWindowEndUtc.Should().BeNull();

        var payload = _service.LoadRunPayload("missed-job", runs[0].RunFileName);
        payload.Should().NotBeNull();
        payload!.WasSkipped.Should().BeTrue();
        payload.SkippedRunCount.Should().Be(0);
        payload.SkippedWindowStartUtc.Should().BeNull();
        payload.SkippedWindowEndUtc.Should().BeNull();
        payload.HostOutputs.Should().BeEmpty();
    }

    [Fact]
    public void SaveSkippedRunSummary_PersistsAggregatedSkippedHistoryEntry()
    {
        var firstScheduledUtc = new DateTime(2026, 3, 8, 12, 0, 0, DateTimeKind.Utc);
        var lastScheduledUtc = new DateTime(2026, 3, 8, 12, 10, 0, DateTimeKind.Utc);
        var summary = new SkippedRunSummaryEntry
        {
            JobId = "summary-job",
            JobName = "Summary Job",
            MissedRunCount = 3,
            FirstScheduledTimeUtc = firstScheduledUtc,
            LastScheduledTimeUtc = lastScheduledUtc
        };

        _service.SaveSkippedRunSummary(summary);

        var runs = _service.GetRunsForJob("summary-job");
        runs.Should().HaveCount(1);
        runs[0].WasSkipped.Should().BeTrue();
        runs[0].StartedUtc.Should().Be(lastScheduledUtc);
        runs[0].CompletedUtc.Should().Be(lastScheduledUtc);
        runs[0].SkippedRunCount.Should().Be(3);
        runs[0].SkippedWindowStartUtc.Should().Be(firstScheduledUtc);
        runs[0].SkippedWindowEndUtc.Should().Be(lastScheduledUtc);
        runs[0].ErrorMessage.Should().Be(
            $"Missed 3 scheduled runs while the application was closed. Range: {firstScheduledUtc.ToLocalTime():g} to {lastScheduledUtc.ToLocalTime():g}.");

        var payload = _service.LoadRunPayload("summary-job", runs[0].RunFileName);
        payload.Should().NotBeNull();
        payload!.WasSkipped.Should().BeTrue();
        payload.SkippedRunCount.Should().Be(3);
        payload.SkippedWindowStartUtc.Should().Be(firstScheduledUtc);
        payload.SkippedWindowEndUtc.Should().Be(lastScheduledUtc);
        payload.HostOutputs.Should().BeEmpty();
    }

    #endregion

    #region HIST-02: Output Capture and Truncation

    [Fact]
    public void LoadRunPayload_ReturnsFullPerHostOutput()
    {
        var outputs = new List<JobHostOutput>
        {
            new() { HostAddress = "192.168.1.1", Output = "Output from host A", Success = true },
            new() { HostAddress = "192.168.1.2", Output = "Output from host B", Success = false, ErrorMessage = "Connection timed out" }
        };
        var result = CreateTestResult(hostsSucceeded: 1, hostsFailed: 1, hostOutputs: outputs);

        _service.SaveRun(result);

        var runs = _service.GetRunsForJob("testjob1");
        var payload = _service.LoadRunPayload("testjob1", runs[0].RunFileName);

        payload.Should().NotBeNull();
        payload!.HostOutputs.Should().HaveCount(2);
        payload.HostOutputs[0].HostAddress.Should().Be("192.168.1.1");
        payload.HostOutputs[0].Output.Should().Be("Output from host A");
        payload.HostOutputs[0].Success.Should().BeTrue();
        payload.HostOutputs[1].HostAddress.Should().Be("192.168.1.2");
        payload.HostOutputs[1].Output.Should().Be("Output from host B");
        payload.HostOutputs[1].Success.Should().BeFalse();
        payload.HostOutputs[1].ErrorMessage.Should().Be("Connection timed out");
    }

    [Fact]
    public void TruncateOutput_LargeOutput_TruncatedWithMarker()
    {
        var largeOutput = new string('X', 200);
        var outputs = new List<JobHostOutput>
        {
            new() { HostAddress = "10.0.0.1", Output = largeOutput, Success = true }
        };
        var result = CreateTestResult(hostsSucceeded: 1, hostsFailed: 0, hostOutputs: outputs);

        _service.SaveRun(result, maxOutputChars: 100);

        var runs = _service.GetRunsForJob("testjob1");
        var payload = _service.LoadRunPayload("testjob1", runs[0].RunFileName);

        payload.Should().NotBeNull();
        payload!.HostOutputs[0].Output.Length.Should().BeLessOrEqualTo(100);
        payload.HostOutputs[0].Output.Should().Contain("[... output truncated:");
    }

    [Fact]
    public void TruncateOutput_SmallOutput_Unchanged()
    {
        var smallOutput = "Short output that fits within limits";
        var outputs = new List<JobHostOutput>
        {
            new() { HostAddress = "10.0.0.1", Output = smallOutput, Success = true }
        };
        var result = CreateTestResult(hostsSucceeded: 1, hostsFailed: 0, hostOutputs: outputs);

        _service.SaveRun(result, maxOutputChars: 100);

        var runs = _service.GetRunsForJob("testjob1");
        var payload = _service.LoadRunPayload("testjob1", runs[0].RunFileName);

        payload.Should().NotBeNull();
        payload!.HostOutputs[0].Output.Should().Be(smallOutput);
    }

    [Fact]
    public void LoadRunPayload_NonexistentRun_ReturnsNull()
    {
        var payload = _service.LoadRunPayload("testjob1", "nonexistent.json");

        payload.Should().BeNull();
    }

    #endregion

    #region HIST-03: Retention / Pruning

    [Fact]
    public void EnforceRetention_CountBased_RemovesOldest()
    {
        // Save 5 runs with maxRuns=3; oldest 2 should be pruned
        for (int i = 0; i < 5; i++)
        {
            var result = CreateTestResult(jobName: $"Run {i}");
            result.CompletedUtc = DateTime.UtcNow.AddMinutes(i);
            result.StartedUtc = result.CompletedUtc.AddSeconds(-5);
            _service.SaveRun(result, maxRuns: 3);
        }

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(3);
        // Newest first: Run 4, Run 3, Run 2
        runs[0].JobName.Should().Be("Run 4");
        runs[1].JobName.Should().Be("Run 3");
        runs[2].JobName.Should().Be("Run 2");
    }

    [Fact]
    public void EnforceRetention_AgeBased_RemovesExpired()
    {
        // Save 3 runs with CompletedUtc set to 40 days ago (old),
        // then save a new run with retentionDays=30 to prune them
        for (int i = 0; i < 3; i++)
        {
            var result = CreateTestResult(jobName: $"Old Run {i}");
            result.CompletedUtc = DateTime.UtcNow.AddDays(-40 + i); // 40, 39, 38 days ago
            result.StartedUtc = result.CompletedUtc.AddSeconds(-5);
            _service.SaveRun(result, retentionDays: 365);
        }

        _service.GetRunsForJob("testjob1").Should().HaveCount(3);

        // Now save a fresh run with retentionDays=30 -- the 3 old entries (40, 39, 38 days ago) are pruned
        var newResult = CreateTestResult(jobName: "Fresh Run");
        newResult.CompletedUtc = DateTime.UtcNow;
        newResult.StartedUtc = newResult.CompletedUtc.AddSeconds(-5);
        _service.SaveRun(newResult, retentionDays: 30);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(1);
        runs[0].JobName.Should().Be("Fresh Run");
    }

    [Fact]
    public void EnforceRetention_DualPruning_AppliesBothLimits()
    {
        // Save 5 runs with CompletedUtc 40 days ago (aged) plus some recent
        // Then trigger dual pruning with both count and age limits
        for (int i = 0; i < 3; i++)
        {
            var result = CreateTestResult(jobName: $"Old {i}");
            result.CompletedUtc = DateTime.UtcNow.AddDays(-40 + i); // 40, 39, 38 days ago
            result.StartedUtc = result.CompletedUtc.AddSeconds(-5);
            _service.SaveRun(result, maxRuns: 50, retentionDays: 365);
        }
        for (int i = 0; i < 2; i++)
        {
            var result = CreateTestResult(jobName: $"Recent {i}");
            result.CompletedUtc = DateTime.UtcNow.AddMinutes(-i);
            result.StartedUtc = result.CompletedUtc.AddSeconds(-5);
            _service.SaveRun(result, maxRuns: 50, retentionDays: 365);
        }

        _service.GetRunsForJob("testjob1").Should().HaveCount(5);

        // Save with maxRuns=2 and retentionDays=30:
        // - Age pruning removes 3 old entries (38-40 days old > 30 day limit)
        // - Count pruning then keeps only 2 of the remaining 3 (2 recent + 1 new)
        var newResult = CreateTestResult(jobName: "Final Run");
        newResult.CompletedUtc = DateTime.UtcNow;
        newResult.StartedUtc = newResult.CompletedUtc.AddSeconds(-5);
        _service.SaveRun(newResult, maxRuns: 2, retentionDays: 30);

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(2, "age pruning removes 3 old, then count pruning trims to 2");
        runs[0].JobName.Should().Be("Final Run");
    }

    [Fact]
    public void EnforceRetention_DeletesPayloadFiles()
    {
        // Save 5 runs, then the oldest 2 should be pruned and their payload files deleted
        var runFileNames = new List<string>();

        for (int i = 0; i < 5; i++)
        {
            var result = CreateTestResult(jobName: $"Run {i}");
            result.CompletedUtc = DateTime.UtcNow.AddMinutes(i);
            result.StartedUtc = result.CompletedUtc.AddSeconds(-5);
            _service.SaveRun(result, maxRuns: 3);
        }

        var jobDir = Path.Combine(_historyPath, "testjob1");
        // Should have index.json + 3 run payload files (2 pruned)
        var jsonFiles = Directory.GetFiles(jobDir, "*.json");
        // index.json + 3 payload files = 4 total
        jsonFiles.Should().HaveCount(4, "index.json + 3 payload files (2 pruned)");
    }

    #endregion

    #region HIST-04: Query, Filter, Search, and Deletion

    [Fact]
    public void GetRunsForJob_FilterBySuccess_ReturnsOnlyMatching()
    {
        _service.SaveRun(CreateTestResult(success: true, jobName: "Success 1"));
        _service.SaveRun(CreateTestResult(success: false, jobName: "Failure 1", hostsFailed: 1, hostsSucceeded: 1));
        _service.SaveRun(CreateTestResult(success: true, jobName: "Success 2"));

        var successOnly = _service.GetRunsForJob("testjob1", new JobRunFilter { Success = true });
        successOnly.Should().HaveCount(2);
        successOnly.Should().OnlyContain(r => r.Success);
    }

    [Fact]
    public void GetRunsForJob_FilterByFailure_ReturnsOnlyFailed()
    {
        _service.SaveRun(CreateTestResult(success: true, jobName: "Success 1"));
        _service.SaveRun(CreateTestResult(success: false, jobName: "Failure 1", hostsFailed: 2, hostsSucceeded: 0));
        _service.SaveRun(CreateTestResult(success: true, jobName: "Success 2"));

        var failedOnly = _service.GetRunsForJob("testjob1", new JobRunFilter { Success = false });
        failedOnly.Should().HaveCount(1);
        failedOnly[0].JobName.Should().Be("Failure 1");
    }

    [Fact]
    public void GetRunsForJob_FilterByDateRange_ReturnsWithinRange()
    {
        var now = DateTime.UtcNow;

        var oldResult = CreateTestResult(jobName: "Old");
        oldResult.CompletedUtc = now.AddDays(-10);
        oldResult.StartedUtc = oldResult.CompletedUtc.AddSeconds(-5);

        var midResult = CreateTestResult(jobName: "Mid");
        midResult.CompletedUtc = now.AddDays(-3);
        midResult.StartedUtc = midResult.CompletedUtc.AddSeconds(-5);

        var recentResult = CreateTestResult(jobName: "Recent");
        recentResult.CompletedUtc = now;
        recentResult.StartedUtc = recentResult.CompletedUtc.AddSeconds(-5);

        _service.SaveRun(oldResult);
        _service.SaveRun(midResult);
        _service.SaveRun(recentResult);

        var filter = new JobRunFilter
        {
            FromUtc = now.AddDays(-5),
            ToUtc = now.AddDays(-1)
        };

        var filtered = _service.GetRunsForJob("testjob1", filter);
        filtered.Should().HaveCount(1);
        filtered[0].JobName.Should().Be("Mid");
    }

    [Fact]
    public void GetRunsForJob_MaxResults_LimitsOutput()
    {
        for (int i = 0; i < 5; i++)
        {
            _service.SaveRun(CreateTestResult(jobName: $"Run {i}"));
        }

        var filter = new JobRunFilter { MaxResults = 2 };
        var runs = _service.GetRunsForJob("testjob1", filter);
        runs.Should().HaveCount(2);
    }

    [Fact]
    public void GetRunsForJob_NoFilter_ReturnsAll()
    {
        for (int i = 0; i < 5; i++)
        {
            _service.SaveRun(CreateTestResult(jobName: $"Run {i}"));
        }

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(5);
    }

    [Fact]
    public void SearchRunOutput_FindsMatchingText()
    {
        var outputs = new List<JobHostOutput>
        {
            new() { HostAddress = "10.0.0.1", Output = "Router config backup complete", Success = true },
            new() { HostAddress = "10.0.0.2", Output = "Switch interface status check", Success = true }
        };
        var result = CreateTestResult(hostOutputs: outputs);
        _service.SaveRun(result);

        var runs = _service.GetRunsForJob("testjob1");
        var matches = _service.SearchRunOutput("testjob1", runs[0].RunFileName, "backup");

        matches.Should().HaveCount(1);
        matches[0].HostAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public void SearchRunOutput_CaseInsensitive()
    {
        var outputs = new List<JobHostOutput>
        {
            new() { HostAddress = "10.0.0.1", Output = "Router config backup complete", Success = true },
            new() { HostAddress = "10.0.0.2", Output = "Switch interface status check", Success = true }
        };
        var result = CreateTestResult(hostOutputs: outputs);
        _service.SaveRun(result);

        var runs = _service.GetRunsForJob("testjob1");
        var matches = _service.SearchRunOutput("testjob1", runs[0].RunFileName, "STATUS");

        matches.Should().HaveCount(1);
        matches[0].HostAddress.Should().Be("10.0.0.2");
    }

    [Fact]
    public void SearchRunOutput_NoMatch_ReturnsEmpty()
    {
        var result = CreateTestResult();
        _service.SaveRun(result);

        var runs = _service.GetRunsForJob("testjob1");
        var matches = _service.SearchRunOutput("testjob1", runs[0].RunFileName, "nonexistent-text-xyz");

        matches.Should().BeEmpty();
    }

    [Fact]
    public void DeleteAllHistory_RemovesJobDirectory()
    {
        _service.SaveRun(CreateTestResult());
        _service.SaveRun(CreateTestResult(jobName: "Run 2"));

        _service.GetRunsForJob("testjob1").Should().HaveCount(2);

        _service.DeleteAllHistory("testjob1");

        _service.GetRunsForJob("testjob1").Should().BeEmpty();
        var jobDir = Path.Combine(_historyPath, "testjob1");
        Directory.Exists(jobDir).Should().BeFalse("job directory should be removed");
    }

    [Fact]
    public void DeleteAllHistory_NonexistentJob_DoesNotThrow()
    {
        var act = () => _service.DeleteAllHistory("nonexistent-job-xyz");

        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteRun_RemovesSpecificEntry()
    {
        _service.SaveRun(CreateTestResult(jobName: "Keep 1"));
        _service.SaveRun(CreateTestResult(jobName: "Delete Me"));
        _service.SaveRun(CreateTestResult(jobName: "Keep 2"));

        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().HaveCount(3);

        var toDelete = runs.First(r => r.JobName == "Delete Me");
        _service.DeleteRun("testjob1", toDelete.Id);

        var remaining = _service.GetRunsForJob("testjob1");
        remaining.Should().HaveCount(2);
        remaining.Should().NotContain(r => r.JobName == "Delete Me");
        remaining.Should().Contain(r => r.JobName == "Keep 1");
        remaining.Should().Contain(r => r.JobName == "Keep 2");
    }

    [Fact]
    public void GetJobIds_ReturnsAllJobsWithHistory()
    {
        _service.SaveRun(CreateTestResult(jobId: "job-alpha"));
        _service.SaveRun(CreateTestResult(jobId: "job-beta"));
        _service.SaveRun(CreateTestResult(jobId: "job-gamma"));

        var ids = _service.GetJobIds();
        ids.Should().HaveCount(3);
        ids.Should().Contain("job-alpha");
        ids.Should().Contain("job-beta");
        ids.Should().Contain("job-gamma");
    }

    [Fact]
    public void CorruptIndex_RecoveredGracefully()
    {
        // Save a run to create valid index
        _service.SaveRun(CreateTestResult());
        _service.GetRunsForJob("testjob1").Should().HaveCount(1);

        // Corrupt the index file
        var indexPath = Path.Combine(_historyPath, "testjob1", "index.json");
        File.WriteAllText(indexPath, "{{{{not json");

        // GetRunsForJob should return empty (corrupt recovery)
        var runs = _service.GetRunsForJob("testjob1");
        runs.Should().BeEmpty();

        // A backup file with .corrupt_ prefix should exist
        var jobDir = Path.Combine(_historyPath, "testjob1");
        var corruptBackups = Directory.GetFiles(jobDir, "index.json.corrupt_*");
        corruptBackups.Should().NotBeEmpty("corrupt index should be backed up");
    }

    #endregion
}
