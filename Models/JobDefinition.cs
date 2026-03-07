using Newtonsoft.Json;

namespace SSH_Helper.Models
{
    /// <summary>
    /// Determines how the job resolves its SSH credentials.
    /// </summary>
    public enum CredentialMode
    {
        /// <summary>
        /// Use the application-level default credentials.
        /// </summary>
        InheritFromApp = 0,

        /// <summary>
        /// Use credentials stored specifically for this job in Windows Credential Manager.
        /// </summary>
        Stored = 1,

        /// <summary>
        /// Each host row carries its own credentials via a designated column.
        /// </summary>
        PerHostColumn = 2
    }

    /// <summary>
    /// Determines the type of schedule attached to a job.
    /// </summary>
    public enum ScheduleType
    {
        /// <summary>
        /// No schedule configured; job runs on demand only.
        /// </summary>
        None = 0,

        /// <summary>
        /// Uses a cron expression for recurring execution.
        /// </summary>
        Recurring = 1,

        /// <summary>
        /// Executes once at a specific date and time.
        /// </summary>
        OneTime = 2
    }

    /// <summary>
    /// Determines how presets within a folder job are executed.
    /// </summary>
    public enum FolderExecutionMode
    {
        /// <summary>
        /// Presets are executed one after another in order.
        /// </summary>
        Sequential = 0,

        /// <summary>
        /// Presets are executed concurrently.
        /// </summary>
        Parallel = 1
    }

    /// <summary>
    /// Tracks the lifecycle state of a job execution.
    /// </summary>
    public enum JobExecutionState
    {
        Queued,
        Started,
        Completed,
        Failed,
        Cancelled,
        Skipped
    }

    /// <summary>
    /// Determines what kind of preset target the job executes.
    /// </summary>
    public enum JobTargetType
    {
        /// <summary>
        /// A single named preset.
        /// </summary>
        Preset = 0,

        /// <summary>
        /// All presets in a named folder (executed sequentially).
        /// </summary>
        Folder = 1
    }

    /// <summary>
    /// Represents a scheduled or on-demand job that executes SSH presets against a set of hosts.
    /// </summary>
    public class JobDefinition
    {
        /// <summary>
        /// Stable GUID-based identifier (32-char lowercase hex, no dashes).
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// User-facing display name for the job.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether the job is eligible for execution.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Whether the job targets a single preset or an entire folder.
        /// </summary>
        public JobTargetType TargetType { get; set; } = JobTargetType.Preset;

        /// <summary>
        /// The preset name or folder path this job targets.
        /// </summary>
        public string TargetName { get; set; } = string.Empty;

        /// <summary>
        /// SHA256 hash of the target preset content at the time the job was saved, for drift detection.
        /// </summary>
        public string TargetContentHash { get; set; } = string.Empty;

        /// <summary>
        /// For folder targets: maps each preset name to its content hash at save time.
        /// Null for single-preset targets.
        /// </summary>
        public Dictionary<string, string>? FolderPresetHashes { get; set; }

        /// <summary>
        /// The list of hosts this job executes against.
        /// Each dictionary represents one host row with column-name keys matching the main grid format.
        /// </summary>
        public List<Dictionary<string, string>> Hosts { get; set; } = new();

        /// <summary>
        /// Ordered list of column names for the host grid (preserves column order).
        /// </summary>
        public List<string> HostColumns { get; set; } = new();

        /// <summary>
        /// How the job resolves SSH credentials.
        /// </summary>
        public CredentialMode CredentialMode { get; set; } = CredentialMode.InheritFromApp;

        /// <summary>
        /// Optional cron expression for recurring schedule (placeholder for Phase 3).
        /// </summary>
        public string? CronExpression { get; set; }

        /// <summary>
        /// Optional one-time scheduled execution time (placeholder for Phase 3).
        /// </summary>
        public DateTime? OneTimeScheduleUtc { get; set; }

        /// <summary>
        /// The type of schedule attached to this job (None, Recurring, or OneTime).
        /// </summary>
        public ScheduleType ScheduleType { get; set; } = ScheduleType.None;

        /// <summary>
        /// Indicates that the target preset content has changed since the job was saved.
        /// </summary>
        public bool HasDriftWarning { get; set; }

        /// <summary>
        /// If the job was auto-disabled, the reason why (e.g., "Target preset deleted").
        /// Null when the job is manually enabled/disabled.
        /// </summary>
        public string? DisabledReason { get; set; }

        /// <summary>
        /// When the job was first created (UTC).
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the job was last modified (UTC).
        /// </summary>
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Tracks in-progress execution state for crash recovery.
        /// Null when the job is not running. Persisted to jobs.json while executing.
        /// </summary>
        public RunningJobState? RunningState { get; set; }

        /// <summary>
        /// For folder targets: determines whether presets are executed sequentially or in parallel.
        /// Default: Sequential (per user decision: continue through all presets in order).
        /// </summary>
        public FolderExecutionMode FolderExecutionMode { get; set; } = FolderExecutionMode.Sequential;

        /// <summary>
        /// When true, folder execution stops at the first preset that fails.
        /// Default: false (per user decision: continue through all presets).
        /// </summary>
        public bool StopOnError { get; set; }
    }
}
