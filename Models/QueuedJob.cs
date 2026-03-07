namespace SSH_Helper.Models;

/// <summary>
/// Represents a job waiting in the FIFO execution queue.
/// In-memory only -- not persisted to JSON.
/// </summary>
public class QueuedJob
{
    /// <summary>
    /// The ID of the job definition to execute.
    /// </summary>
    public string JobId { get; set; }

    /// <summary>
    /// When this job was added to the queue (UTC).
    /// </summary>
    public DateTime QueuedUtc { get; set; }

    public QueuedJob(string jobId, DateTime queuedUtc)
    {
        JobId = jobId;
        QueuedUtc = queuedUtc;
    }
}
