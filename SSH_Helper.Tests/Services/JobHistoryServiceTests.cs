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
        int hostsSucceeded = 2,
        int hostsFailed = 0,
        List<JobHostOutput>? hostOutputs = null)
    {
        var now = DateTime.UtcNow;
        return new JobRunResult
        {
            JobId = jobId,
            JobName = jobName,
            StartedUtc = now.AddSeconds(-5),
            CompletedUtc = now,
            Success = success,
            HostsSucceeded = hostsSucceeded,
            HostsFailed = hostsFailed,
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
}
