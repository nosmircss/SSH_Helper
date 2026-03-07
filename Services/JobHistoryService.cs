using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Persists, prunes, queries, and searches job execution history.
    /// Each job gets a subdirectory under the base history folder containing
    /// an index.json and individual {runId}.json payload files.
    /// </summary>
    public sealed class JobHistoryService
    {
        private const int DefaultMaxRuns = 50;
        private const int DefaultRetentionDays = 30;
        private const int DefaultMaxOutputChars = 1_048_576;

        private readonly string _baseDirectory;

        /// <summary>
        /// Creates a new JobHistoryService.
        /// </summary>
        /// <param name="basePath">
        /// Optional base directory for history storage.
        /// Defaults to %LocalAppData%\SSH_Helper\job-history.
        /// </param>
        public JobHistoryService(string? basePath = null)
        {
            _baseDirectory = basePath
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SSH_Helper",
                    "job-history");
        }

        #region Event Subscription

        /// <summary>
        /// Subscribes to a JobExecutionService's JobCompleted event so that
        /// every completed job run is automatically persisted.
        /// </summary>
        public void SubscribeTo(JobExecutionService executionService)
        {
            executionService.JobCompleted += OnJobCompleted;
        }

        private void OnJobCompleted(object? sender, JobRunResult result)
        {
            SaveRun(result, DefaultMaxRuns, DefaultRetentionDays, DefaultMaxOutputChars);
        }

        #endregion

        #region Save

        /// <summary>
        /// Persists a job run to history. Generates a unique run ID, writes the
        /// payload file, updates the index, and enforces retention limits.
        /// </summary>
        public void SaveRun(
            JobRunResult result,
            int maxRuns = DefaultMaxRuns,
            int retentionDays = DefaultRetentionDays,
            int maxOutputChars = DefaultMaxOutputChars)
        {
            var runId = HistoryIdGenerator.NewId();
            var runFileName = $"{runId}.json";

            // Build payload with truncated output
            var payload = new JobRunPayload
            {
                Id = runId,
                JobId = result.JobId,
                JobName = result.JobName,
                StartedUtc = result.StartedUtc,
                CompletedUtc = result.CompletedUtc,
                Success = result.Success,
                HostsSucceeded = result.HostsSucceeded,
                HostsFailed = result.HostsFailed,
                ErrorMessage = result.ErrorMessage,
                HostOutputs = BuildTruncatedOutputs(result.HostOutputs, maxOutputChars)
            };

            // Build lightweight index entry
            var record = new JobRunRecord
            {
                Id = runId,
                JobId = result.JobId,
                JobName = result.JobName,
                StartedUtc = result.StartedUtc,
                CompletedUtc = result.CompletedUtc,
                Success = result.Success,
                HostsSucceeded = result.HostsSucceeded,
                HostsFailed = result.HostsFailed,
                ErrorMessage = result.ErrorMessage,
                RunFileName = runFileName
            };

            // Ensure job subdirectory exists
            var jobDir = GetJobDirectory(result.JobId);
            Directory.CreateDirectory(jobDir);

            // Write payload atomically (no backup needed for individual run files)
            var payloadPath = GetRunFilePath(result.JobId, runFileName);
            JsonFileWriter.WriteJsonAtomic(payloadPath, Serialize(payload), createBackup: false);

            // Load existing index, prepend new record (newest first), save atomically
            var indexDoc = LoadJobIndex(result.JobId);
            indexDoc.Entries.Insert(0, record);
            var indexPath = GetIndexPath(result.JobId);
            JsonFileWriter.WriteJsonAtomic(indexPath, Serialize(indexDoc), createBackup: true);

            // Enforce retention limits
            EnforceRetention(result.JobId, maxRuns, retentionDays);
        }

        #endregion

        #region Truncation

        private static List<JobHostOutput> BuildTruncatedOutputs(
            List<JobHostOutput>? hostOutputs,
            int maxOutputChars)
        {
            if (hostOutputs == null || hostOutputs.Count == 0)
                return new List<JobHostOutput>();

            var result = new List<JobHostOutput>(hostOutputs.Count);
            foreach (var ho in hostOutputs)
            {
                result.Add(new JobHostOutput
                {
                    HostAddress = ho.HostAddress,
                    Output = TruncateOutput(ho.Output, maxOutputChars),
                    Success = ho.Success,
                    ErrorMessage = ho.ErrorMessage
                });
            }
            return result;
        }

        private static string TruncateOutput(string output, int maxChars)
        {
            if (string.IsNullOrEmpty(output) || output.Length <= maxChars)
                return output ?? string.Empty;

            var marker = $"\n[... output truncated: {output.Length - maxChars:N0} characters removed ...]\n";
            var keepChars = maxChars - marker.Length;

            if (keepChars <= 0)
                return marker;

            return output.Substring(0, keepChars) + marker;
        }

        #endregion

        #region Index I/O

        private JobRunIndexDocument LoadJobIndex(string jobId)
        {
            var indexPath = GetIndexPath(jobId);

            if (!File.Exists(indexPath))
                return new JobRunIndexDocument();

            try
            {
                var json = File.ReadAllText(indexPath);
                var doc = JsonConvert.DeserializeObject<JobRunIndexDocument>(json);
                return doc ?? new JobRunIndexDocument();
            }
            catch
            {
                TryBackupCorruptFile(indexPath);
                return new JobRunIndexDocument();
            }
        }

        private static void TryBackupCorruptFile(string path)
        {
            try
            {
                var backupPath = $"{path}.corrupt_{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(path, backupPath);
            }
            catch
            {
                // Best-effort: swallow exceptions during corrupt file backup
            }
        }

        #endregion

        #region Retention

        private void EnforceRetention(string jobId, int maxRuns, int retentionDays)
        {
            var indexDoc = LoadJobIndex(jobId);
            var removed = false;

            // Phase 1: Age-based pruning
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var agedOut = indexDoc.Entries.Where(e => e.CompletedUtc < cutoff).ToList();
            foreach (var entry in agedOut)
            {
                indexDoc.Entries.Remove(entry);
                DeletePayloadFile(jobId, entry.RunFileName);
                removed = true;
            }

            // Phase 2: Count-based pruning (remove oldest, which are at the end)
            while (indexDoc.Entries.Count > maxRuns)
            {
                var last = indexDoc.Entries[indexDoc.Entries.Count - 1];
                indexDoc.Entries.RemoveAt(indexDoc.Entries.Count - 1);
                DeletePayloadFile(jobId, last.RunFileName);
                removed = true;
            }

            if (removed)
            {
                var indexPath = GetIndexPath(jobId);
                JsonFileWriter.WriteJsonAtomic(indexPath, Serialize(indexDoc), createBackup: true);
            }
        }

        private void DeletePayloadFile(string jobId, string runFileName)
        {
            try
            {
                var path = GetRunFilePath(jobId, runFileName);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort deletion
            }
        }

        #endregion

        #region Path Helpers

        private string GetJobDirectory(string jobId)
            => Path.Combine(_baseDirectory, jobId);

        private string GetIndexPath(string jobId)
            => Path.Combine(GetJobDirectory(jobId), "index.json");

        private string GetRunFilePath(string jobId, string runFileName)
            => Path.Combine(GetJobDirectory(jobId), runFileName);

        private static string Serialize(object obj)
            => JsonConvert.SerializeObject(obj, Formatting.Indented);

        #endregion
    }
}
