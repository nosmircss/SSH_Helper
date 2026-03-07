using FluentAssertions;
using Newtonsoft.Json;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class ExecutionPipelineModelTests
{
    #region FolderExecutionMode Enum

    [Fact]
    public void FolderExecutionMode_Sequential_IsZero()
    {
        ((int)FolderExecutionMode.Sequential).Should().Be(0);
    }

    [Fact]
    public void FolderExecutionMode_Parallel_IsOne()
    {
        ((int)FolderExecutionMode.Parallel).Should().Be(1);
    }

    #endregion

    #region JobExecutionState Enum

    [Fact]
    public void JobExecutionState_HasAllExpectedValues()
    {
        Enum.GetNames(typeof(JobExecutionState)).Should().HaveCount(6);
    }

    [Fact]
    public void JobExecutionState_HasQueued()
    {
        Enum.IsDefined(typeof(JobExecutionState), "Queued").Should().BeTrue();
    }

    [Fact]
    public void JobExecutionState_HasStarted()
    {
        Enum.IsDefined(typeof(JobExecutionState), "Started").Should().BeTrue();
    }

    [Fact]
    public void JobExecutionState_HasCompleted()
    {
        Enum.IsDefined(typeof(JobExecutionState), "Completed").Should().BeTrue();
    }

    [Fact]
    public void JobExecutionState_HasFailed()
    {
        Enum.IsDefined(typeof(JobExecutionState), "Failed").Should().BeTrue();
    }

    [Fact]
    public void JobExecutionState_HasCancelled()
    {
        Enum.IsDefined(typeof(JobExecutionState), "Cancelled").Should().BeTrue();
    }

    [Fact]
    public void JobExecutionState_HasSkipped()
    {
        Enum.IsDefined(typeof(JobExecutionState), "Skipped").Should().BeTrue();
    }

    #endregion

    #region RunningJobState

    [Fact]
    public void RunningJobState_HasStartedUtcProperty()
    {
        var state = new RunningJobState { StartedUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) };

        state.StartedUtc.Should().Be(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RunningJobState_SerializesToJson()
    {
        var state = new RunningJobState { StartedUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) };

        var json = JsonConvert.SerializeObject(state);
        var deserialized = JsonConvert.DeserializeObject<RunningJobState>(json);

        deserialized.Should().NotBeNull();
        deserialized!.StartedUtc.Should().Be(state.StartedUtc);
    }

    #endregion

    #region QueuedJob

    [Fact]
    public void QueuedJob_Constructor_SetsProperties()
    {
        var now = DateTime.UtcNow;
        var job = new QueuedJob("job123", now);

        job.JobId.Should().Be("job123");
        job.QueuedUtc.Should().Be(now);
    }

    [Fact]
    public void QueuedJob_Properties_AreSettable()
    {
        var job = new QueuedJob("initial", DateTime.MinValue);
        job.JobId = "updated";
        job.QueuedUtc = DateTime.MaxValue;

        job.JobId.Should().Be("updated");
        job.QueuedUtc.Should().Be(DateTime.MaxValue);
    }

    #endregion

    #region JobRunResult

    [Fact]
    public void JobRunResult_HasAllExpectedProperties()
    {
        var started = DateTime.UtcNow;
        var completed = started.AddMinutes(5);

        var result = new JobRunResult
        {
            JobId = "abc123",
            JobName = "Test Job",
            StartedUtc = started,
            CompletedUtc = completed,
            Success = true,
            HostsSucceeded = 5,
            HostsFailed = 1,
            ErrorMessage = null
        };

        result.JobId.Should().Be("abc123");
        result.JobName.Should().Be("Test Job");
        result.StartedUtc.Should().Be(started);
        result.CompletedUtc.Should().Be(completed);
        result.Success.Should().BeTrue();
        result.HostsSucceeded.Should().Be(5);
        result.HostsFailed.Should().Be(1);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void JobRunResult_ErrorMessage_CanBeSet()
    {
        var result = new JobRunResult
        {
            JobId = "err",
            JobName = "Failed",
            Success = false,
            ErrorMessage = "Connection refused"
        };

        result.ErrorMessage.Should().Be("Connection refused");
    }

    #endregion

    #region JobDefinition New Properties

    [Fact]
    public void JobDefinition_RunningState_DefaultsToNull()
    {
        var job = new JobDefinition();

        job.RunningState.Should().BeNull();
    }

    [Fact]
    public void JobDefinition_RunningState_CanBeSet()
    {
        var job = new JobDefinition();
        var state = new RunningJobState { StartedUtc = DateTime.UtcNow };
        job.RunningState = state;

        job.RunningState.Should().NotBeNull();
        job.RunningState!.StartedUtc.Should().Be(state.StartedUtc);
    }

    [Fact]
    public void JobDefinition_FolderExecutionMode_DefaultsToSequential()
    {
        var job = new JobDefinition();

        job.FolderExecutionMode.Should().Be(FolderExecutionMode.Sequential);
    }

    [Fact]
    public void JobDefinition_FolderExecutionMode_CanBeSetToParallel()
    {
        var job = new JobDefinition();
        job.FolderExecutionMode = FolderExecutionMode.Parallel;

        job.FolderExecutionMode.Should().Be(FolderExecutionMode.Parallel);
    }

    [Fact]
    public void JobDefinition_StopOnError_DefaultsToFalse()
    {
        var job = new JobDefinition();

        job.StopOnError.Should().BeFalse();
    }

    [Fact]
    public void JobDefinition_StopOnError_CanBeSetToTrue()
    {
        var job = new JobDefinition();
        job.StopOnError = true;

        job.StopOnError.Should().BeTrue();
    }

    [Fact]
    public void JobDefinition_ExistingProperties_StillWork()
    {
        // Verify existing properties are not broken by new additions
        var job = new JobDefinition();

        job.Id.Should().NotBeNullOrEmpty();
        job.Name.Should().BeEmpty();
        job.IsEnabled.Should().BeTrue();
        job.TargetType.Should().Be(JobTargetType.Preset);
        job.CredentialMode.Should().Be(CredentialMode.InheritFromApp);
        job.ScheduleType.Should().Be(ScheduleType.None);
        job.Hosts.Should().BeEmpty();
        job.HostColumns.Should().BeEmpty();
    }

    [Fact]
    public void JobDefinition_WithRunningState_SerializesToJson()
    {
        var job = new JobDefinition
        {
            Name = "Serialize Test",
            RunningState = new RunningJobState { StartedUtc = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc) },
            FolderExecutionMode = FolderExecutionMode.Parallel,
            StopOnError = true
        };

        var json = JsonConvert.SerializeObject(job);
        var deserialized = JsonConvert.DeserializeObject<JobDefinition>(json);

        deserialized.Should().NotBeNull();
        deserialized!.RunningState.Should().NotBeNull();
        deserialized.RunningState!.StartedUtc.Should().Be(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
        deserialized.FolderExecutionMode.Should().Be(FolderExecutionMode.Parallel);
        deserialized.StopOnError.Should().BeTrue();
    }

    #endregion
}
