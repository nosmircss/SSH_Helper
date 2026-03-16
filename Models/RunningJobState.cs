namespace SSH_Helper.Models;

/// <summary>
/// Tracks the in-progress state of a running job for crash recovery.
/// Persisted to jobs.json while the job is executing; cleared on completion.
/// </summary>
public class RunningJobState
{
    /// <summary>
    /// When the job execution started (UTC).
    /// </summary>
    public DateTime StartedUtc { get; set; }
}
