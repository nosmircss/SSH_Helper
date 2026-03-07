namespace SSH_Helper.Models;

/// <summary>
/// Lightweight execution result raised as event data after job completion.
/// Used by Phase 4 history handoff to persist run outcomes.
/// </summary>
public class JobRunResult
{
    /// <summary>
    /// The ID of the job that was executed.
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the job at execution time.
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// When execution started (UTC).
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// When execution completed (UTC).
    /// </summary>
    public DateTime CompletedUtc { get; set; }

    /// <summary>
    /// Whether the overall job execution succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of hosts that completed successfully.
    /// </summary>
    public int HostsSucceeded { get; set; }

    /// <summary>
    /// Number of hosts that failed during execution.
    /// </summary>
    public int HostsFailed { get; set; }

    /// <summary>
    /// Optional error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Per-host execution outputs for history recording.
    /// Populated by ExecuteJobCoreAsync for the JobCompleted event.
    /// May be null for error-path completions where no hosts were reached.
    /// </summary>
    public List<JobHostOutput>? HostOutputs { get; set; }
}
