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
        private readonly string _baseDirectory;
        private Func<JobRunResult, JobHistoryRetentionOptions>? _retentionOptionsResolver;

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
                ?? Path.Combine(AppDataPaths.GetAppFolder(), "job-history");
        }

        #region Event Subscription

        /// <summary>
        /// Subscribes to a JobExecutionService's JobCompleted event so that
        /// every completed job run is automatically persisted.
        /// </summary>
        public void SubscribeTo(
            JobExecutionService executionService,
            Func<JobRunResult, JobHistoryRetentionOptions>? retentionOptionsResolver = null)
        {
            _retentionOptionsResolver = retentionOptionsResolver;
            executionService.JobCompleted += OnJobCompleted;
        }

        private void OnJobCompleted(object? sender, JobRunResult result)
        {
            SaveRun(result, _retentionOptionsResolver?.Invoke(result));
        }

        #endregion

        #region Save

        /// <summary>
        /// Persists a job run to history. Generates a unique run ID, writes the
        /// payload file, updates the index, and enforces retention limits.
        /// </summary>
        public void SaveRun(
            JobRunResult result,
            int maxRuns = JobHistoryRetentionOptions.DefaultMaxRuns,
            int retentionDays = JobHistoryRetentionOptions.DefaultRetentionDays,
            int maxOutputChars = JobHistoryRetentionOptions.DefaultMaxOutputChars)
        {
            SaveRun(result, new JobHistoryRetentionOptions
            {
                MaxRuns = maxRuns,
                RetentionDays = retentionDays,
                MaxOutputChars = maxOutputChars
            });
        }

        /// <summary>
        /// Persists a job run to history using the provided retention policy.
        /// </summary>
        public void SaveRun(JobRunResult result, JobHistoryRetentionOptions? options)
        {
            ArgumentNullException.ThrowIfNull(result);

            SaveRunCore(
                jobId: result.JobId,
                jobName: result.JobName,
                startedUtc: result.StartedUtc,
                completedUtc: result.CompletedUtc,
                success: result.Success,
                wasCancelled: result.WasCancelled,
                hostsSucceeded: result.HostsSucceeded,
                hostsFailed: result.HostsFailed,
                errorMessage: result.ErrorMessage,
                hostOutputs: result.HostOutputs,
                wasSkipped: false,
                skippedRunCount: 0,
                skippedWindowStartUtc: null,
                skippedWindowEndUtc: null,
                options: NormalizeOptions(options));
        }

        /// <summary>
        /// Persists a skipped run detected during scheduler startup.
        /// </summary>
        public void SaveSkippedRun(
            SkippedRunEntry skippedRun,
            JobHistoryRetentionOptions? options = null,
            string? errorMessage = null)
        {
            ArgumentNullException.ThrowIfNull(skippedRun);

            var localScheduledTime = skippedRun.ScheduledTimeUtc.ToLocalTime().ToString("g");
            SaveRunCore(
                jobId: skippedRun.JobId,
                jobName: skippedRun.JobName,
                startedUtc: skippedRun.ScheduledTimeUtc,
                completedUtc: skippedRun.ScheduledTimeUtc,
                success: false,
                wasCancelled: false,
                hostsSucceeded: 0,
                hostsFailed: 0,
                errorMessage: errorMessage ?? $"Missed scheduled run at {localScheduledTime} while the application was closed.",
                hostOutputs: null,
                wasSkipped: true,
                skippedRunCount: 0,
                skippedWindowStartUtc: null,
                skippedWindowEndUtc: null,
                options: NormalizeOptions(options));
        }

        /// <summary>
        /// Persists a summarized skipped run detected during scheduler startup.
        /// </summary>
        public void SaveSkippedRunSummary(
            SkippedRunSummaryEntry skippedSummary,
            JobHistoryRetentionOptions? options = null,
            string? errorMessage = null)
        {
            ArgumentNullException.ThrowIfNull(skippedSummary);

            SaveRunCore(
                jobId: skippedSummary.JobId,
                jobName: skippedSummary.JobName,
                startedUtc: skippedSummary.LastScheduledTimeUtc,
                completedUtc: skippedSummary.LastScheduledTimeUtc,
                success: false,
                wasCancelled: false,
                hostsSucceeded: 0,
                hostsFailed: 0,
                errorMessage: errorMessage ?? BuildSkippedSummaryMessage(skippedSummary),
                hostOutputs: null,
                wasSkipped: true,
                skippedRunCount: skippedSummary.MissedRunCount,
                skippedWindowStartUtc: skippedSummary.FirstScheduledTimeUtc,
                skippedWindowEndUtc: skippedSummary.LastScheduledTimeUtc,
                options: NormalizeOptions(options));
        }

        private void SaveRunCore(
            string jobId,
            string jobName,
            DateTime startedUtc,
            DateTime completedUtc,
            bool success,
            bool wasCancelled,
            int hostsSucceeded,
            int hostsFailed,
            string? errorMessage,
            List<JobHostOutput>? hostOutputs,
            bool wasSkipped,
            int skippedRunCount,
            DateTime? skippedWindowStartUtc,
            DateTime? skippedWindowEndUtc,
            JobHistoryRetentionOptions options)
        {
            var payload = new JobRunPayload
            {
                JobId = jobId,
                JobName = jobName,
                StartedUtc = startedUtc,
                CompletedUtc = completedUtc,
                Success = success,
                WasCancelled = wasCancelled,
                HostsSucceeded = hostsSucceeded,
                HostsFailed = hostsFailed,
                ErrorMessage = errorMessage,
                ConsecutiveFailureCount = !success && !wasSkipped && !wasCancelled ? 1 : 0,
                WasSkipped = wasSkipped,
                SkippedRunCount = skippedRunCount,
                SkippedWindowStartUtc = skippedWindowStartUtc,
                SkippedWindowEndUtc = skippedWindowEndUtc,
                HostOutputs = BuildTruncatedOutputs(hostOutputs, options.MaxOutputChars)
            };

            var record = new JobRunRecord
            {
                JobId = jobId,
                JobName = jobName,
                StartedUtc = startedUtc,
                CompletedUtc = completedUtc,
                Success = success,
                WasCancelled = wasCancelled,
                HostsSucceeded = hostsSucceeded,
                HostsFailed = hostsFailed,
                ErrorMessage = errorMessage,
                ConsecutiveFailureCount = payload.ConsecutiveFailureCount,
                WasSkipped = wasSkipped,
                SkippedRunCount = skippedRunCount,
                SkippedWindowStartUtc = skippedWindowStartUtc,
                SkippedWindowEndUtc = skippedWindowEndUtc
            };

            // Ensure job subdirectory exists
            var jobDir = GetJobDirectory(jobId);
            Directory.CreateDirectory(jobDir);

            var indexDoc = LoadJobIndex(jobId);
            if (TryCollapseLatestFailure(jobId, indexDoc, record, payload))
            {
                SaveIndex(jobId, indexDoc);
                EnforceRetention(jobId, options.MaxRuns, options.RetentionDays);
                return;
            }

            var runId = HistoryIdGenerator.NewId();
            var runFileName = $"{runId}.json";
            payload.Id = runId;
            record.Id = runId;
            record.RunFileName = runFileName;

            // Write payload atomically (no backup needed for individual run files)
            var payloadPath = GetRunFilePath(jobId, runFileName);
            JsonFileWriter.WriteJsonAtomic(payloadPath, Serialize(payload), createBackup: false);

            // Load existing index, prepend new record (newest first), save atomically
            indexDoc.Entries.Insert(0, record);
            SaveIndex(jobId, indexDoc);

            // Enforce retention limits
            EnforceRetention(jobId, options.MaxRuns, options.RetentionDays);
        }

        private bool TryCollapseLatestFailure(
            string jobId,
            JobRunIndexDocument indexDoc,
            JobRunRecord candidateRecord,
            JobRunPayload candidatePayload)
        {
            if (candidateRecord.Success || candidateRecord.WasSkipped || candidateRecord.WasCancelled || indexDoc.Entries.Count == 0)
            {
                return false;
            }

            var latestRecord = indexDoc.Entries[0];
            if (!CanCollapseFailure(latestRecord, candidateRecord))
            {
                return false;
            }

            var latestPayload = LoadRunPayload(jobId, latestRecord.RunFileName);
            if (latestPayload == null || !CanCollapseFailure(latestPayload, candidatePayload))
            {
                return false;
            }

            var nextCount = Math.Max(latestRecord.ConsecutiveFailureCount, 1) + 1;

            latestRecord.JobName = candidateRecord.JobName;
            latestRecord.StartedUtc = candidateRecord.StartedUtc;
            latestRecord.CompletedUtc = candidateRecord.CompletedUtc;
            latestRecord.HostsSucceeded = candidateRecord.HostsSucceeded;
            latestRecord.HostsFailed = candidateRecord.HostsFailed;
            latestRecord.ErrorMessage = candidateRecord.ErrorMessage;
            latestRecord.ConsecutiveFailureCount = nextCount;

            latestPayload.JobName = candidatePayload.JobName;
            latestPayload.StartedUtc = candidatePayload.StartedUtc;
            latestPayload.CompletedUtc = candidatePayload.CompletedUtc;
            latestPayload.HostsSucceeded = candidatePayload.HostsSucceeded;
            latestPayload.HostsFailed = candidatePayload.HostsFailed;
            latestPayload.ErrorMessage = candidatePayload.ErrorMessage;
            latestPayload.ConsecutiveFailureCount = nextCount;
            latestPayload.HostOutputs = candidatePayload.HostOutputs;

            var payloadPath = GetRunFilePath(jobId, latestRecord.RunFileName);
            JsonFileWriter.WriteJsonAtomic(payloadPath, Serialize(latestPayload), createBackup: false);
            return true;
        }

        private static bool CanCollapseFailure(JobRunRecord existingRecord, JobRunRecord candidateRecord)
        {
            return !existingRecord.Success
                && !existingRecord.WasSkipped
                && !existingRecord.WasCancelled
                && existingRecord.HostsSucceeded == candidateRecord.HostsSucceeded
                && existingRecord.HostsFailed == candidateRecord.HostsFailed
                && string.Equals(
                    NormalizeComparableText(existingRecord.ErrorMessage),
                    NormalizeComparableText(candidateRecord.ErrorMessage),
                    StringComparison.Ordinal);
        }

        private static bool CanCollapseFailure(JobRunPayload existingPayload, JobRunPayload candidatePayload)
        {
            return !existingPayload.Success
                && !existingPayload.WasSkipped
                && !existingPayload.WasCancelled
                && existingPayload.HostsSucceeded == candidatePayload.HostsSucceeded
                && existingPayload.HostsFailed == candidatePayload.HostsFailed
                && string.Equals(
                    NormalizeComparableText(existingPayload.ErrorMessage),
                    NormalizeComparableText(candidatePayload.ErrorMessage),
                    StringComparison.Ordinal)
                && string.Equals(
                    BuildHostFailureSignature(existingPayload.HostOutputs),
                    BuildHostFailureSignature(candidatePayload.HostOutputs),
                    StringComparison.Ordinal);
        }

        private static string BuildHostFailureSignature(IEnumerable<JobHostOutput>? hostOutputs)
        {
            if (hostOutputs == null)
            {
                return string.Empty;
            }

            return string.Join(
                "\n",
                hostOutputs
                    .OrderBy(output => output.HostAddress, StringComparer.OrdinalIgnoreCase)
                    .Select(output =>
                        $"{NormalizeComparableText(output.HostAddress)}|{output.Success}|{NormalizeComparableText(output.ErrorMessage)}"));
        }

        private static string NormalizeComparableText(string? text)
            => (text ?? string.Empty).Trim();

        private static JobHistoryRetentionOptions NormalizeOptions(JobHistoryRetentionOptions? options)
        {
            var effective = options ?? new JobHistoryRetentionOptions();
            return new JobHistoryRetentionOptions
            {
                MaxRuns = effective.MaxRuns > 0 ? effective.MaxRuns : JobHistoryRetentionOptions.DefaultMaxRuns,
                RetentionDays = effective.RetentionDays > 0 ? effective.RetentionDays : JobHistoryRetentionOptions.DefaultRetentionDays,
                MaxOutputChars = effective.MaxOutputChars > 0 ? effective.MaxOutputChars : JobHistoryRetentionOptions.DefaultMaxOutputChars
            };
        }

        private static string BuildSkippedSummaryMessage(SkippedRunSummaryEntry skippedSummary)
        {
            if (skippedSummary.MissedRunCount <= 1)
            {
                var localScheduledTime = skippedSummary.LastScheduledTimeUtc.ToLocalTime().ToString("g");
                return $"Missed 1 scheduled run at {localScheduledTime} while the application was closed.";
            }

            var firstLocalTime = skippedSummary.FirstScheduledTimeUtc.ToLocalTime().ToString("g");
            var lastLocalTime = skippedSummary.LastScheduledTimeUtc.ToLocalTime().ToString("g");
            return $"Missed {skippedSummary.MissedRunCount} scheduled runs while the application was closed. Range: {firstLocalTime} to {lastLocalTime}.";
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
                    WasCancelled = ho.WasCancelled,
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

        private void SaveIndex(string jobId, JobRunIndexDocument indexDoc)
        {
            var indexPath = GetIndexPath(jobId);
            JsonFileWriter.WriteJsonAtomic(indexPath, Serialize(indexDoc), createBackup: true);
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
                // Best-effort
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

        #region Query API

        /// <summary>
        /// Returns run records for a job, optionally filtered by status, date range,
        /// and limited to a maximum number of results.
        /// Entries are returned newest-first.
        /// </summary>
        public IReadOnlyList<JobRunRecord> GetRunsForJob(string jobId, JobRunFilter? filter = null)
        {
            var indexDoc = LoadJobIndex(jobId);
            IEnumerable<JobRunRecord> entries = indexDoc.Entries;

            if (filter != null)
            {
                if (filter.Success.HasValue)
                    entries = entries.Where(r => r.Success == filter.Success.Value);

                if (filter.FromUtc.HasValue)
                    entries = entries.Where(r => r.CompletedUtc >= filter.FromUtc.Value);

                if (filter.ToUtc.HasValue)
                    entries = entries.Where(r => r.CompletedUtc <= filter.ToUtc.Value);
            }

            var maxResults = filter?.MaxResults ?? 50;
            return entries.Take(maxResults).ToList().AsReadOnly();
        }

        /// <summary>
        /// Loads the full run payload (including per-host output) for a specific run.
        /// Returns null if the file does not exist or is corrupt.
        /// </summary>
        public JobRunPayload? LoadRunPayload(string jobId, string runFileName)
        {
            var path = GetRunFilePath(jobId, runFileName);

            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<JobRunPayload>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Searches a single run's per-host output for the given text.
        /// Matching is case-insensitive against both output content and host address.
        /// </summary>
        public IReadOnlyList<JobHostOutput> SearchRunOutput(string jobId, string runFileName, string searchText)
        {
            var payload = LoadRunPayload(jobId, runFileName);
            if (payload == null)
                return Array.Empty<JobHostOutput>();

            return payload.HostOutputs
                .Where(ho =>
                    ho.Output.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    ho.HostAddress.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsReadOnly();
        }

        #endregion

        #region Deletion

        /// <summary>
        /// Deletes all history for a job by removing its entire subdirectory.
        /// Best-effort: exceptions are swallowed.
        /// </summary>
        public void DeleteAllHistory(string jobId)
        {
            try
            {
                var jobDir = GetJobDirectory(jobId);
                if (Directory.Exists(jobDir))
                    Directory.Delete(jobDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        /// <summary>
        /// Deletes a single run record and its payload file from the job's history.
        /// </summary>
        public void DeleteRun(string jobId, string runId)
        {
            var indexDoc = LoadJobIndex(jobId);
            var entry = indexDoc.Entries.FirstOrDefault(e => e.Id == runId);

            if (entry == null)
                return;

            indexDoc.Entries.Remove(entry);
            DeletePayloadFile(jobId, entry.RunFileName);

            var indexPath = GetIndexPath(jobId);
            JsonFileWriter.WriteJsonAtomic(indexPath, Serialize(indexDoc), createBackup: true);
        }

        /// <summary>
        /// Returns the IDs of all jobs that have history stored.
        /// </summary>
        public IReadOnlyList<string> GetJobIds()
        {
            if (!Directory.Exists(_baseDirectory))
                return Array.Empty<string>();

            return Directory.GetDirectories(_baseDirectory)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList()
                .AsReadOnly()!;
        }

        /// <summary>
        /// Clears all history for a job. Alias for <see cref="DeleteAllHistory"/>.
        /// </summary>
        public void ClearHistory(string jobId) => DeleteAllHistory(jobId);

        #endregion

        #region Path Helpers

        private string GetJobDirectory(string jobId)
            => Path.Combine(_baseDirectory, jobId);

        private string GetIndexPath(string jobId)
            => Path.Combine(GetJobDirectory(jobId), "index.json");

        private string GetRunFilePath(string jobId, string runFileName)
            => Path.Combine(GetJobDirectory(jobId), runFileName);

        private static string Serialize(object obj)
            => JsonFileWriter.Serialize(obj);

        #endregion
    }
}
