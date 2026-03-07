using Newtonsoft.Json;
using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Provides CRUD operations and JSON file persistence for job definitions.
    /// </summary>
    public sealed class JobStorageService
    {
        private const int MaxNameLength = 100;
        private readonly string _jobsFilePath;
        private readonly ICredentialProvider _credentialProvider;
        private readonly Dictionary<string, JobDefinition> _jobs = new();
        private bool _loaded;

        /// <summary>
        /// Creates a new JobStorageService instance.
        /// </summary>
        /// <param name="credentialProvider">Credential provider for cleaning up stored credentials on delete.</param>
        /// <param name="jobsFilePath">
        /// Optional path to the jobs.json file. If null, uses %LocalAppData%\SSH_Helper\jobs.json.
        /// </param>
        public JobStorageService(ICredentialProvider credentialProvider, string? jobsFilePath = null)
        {
            _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));

            if (string.IsNullOrWhiteSpace(jobsFilePath))
            {
                var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolder = Path.Combine(folder, "SSH_Helper");
                if (!Directory.Exists(appFolder))
                    Directory.CreateDirectory(appFolder);

                _jobsFilePath = Path.Combine(appFolder, "jobs.json");
            }
            else
            {
                var directory = Path.GetDirectoryName(jobsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                _jobsFilePath = jobsFilePath;
            }
        }

        /// <summary>
        /// Path to the jobs.json file on disk.
        /// </summary>
        public string JobsFilePath => _jobsFilePath;

        /// <summary>
        /// If non-null, describes a load error (e.g., corrupt file).
        /// </summary>
        public string? LoadError { get; private set; }

        /// <summary>
        /// The current in-memory job definitions, keyed by job ID.
        /// </summary>
        public IReadOnlyDictionary<string, JobDefinition> Jobs => _jobs;

        /// <summary>
        /// Fires when jobs are saved or deleted.
        /// </summary>
        public event EventHandler? JobsChanged;

        /// <summary>
        /// Loads job definitions from the jobs.json file.
        /// </summary>
        public void Load()
        {
            LoadError = null;

            if (!File.Exists(_jobsFilePath))
            {
                _loaded = true;
                return;
            }

            try
            {
                var json = File.ReadAllText(_jobsFilePath);
                var wrapper = JsonConvert.DeserializeObject<JobsFileWrapper>(json);

                _jobs.Clear();
                if (wrapper?.Jobs != null)
                {
                    foreach (var job in wrapper.Jobs)
                    {
                        if (!string.IsNullOrEmpty(job.Id))
                            _jobs[job.Id] = job;
                    }
                }

                _loaded = true;
            }
            catch (Exception ex)
            {
                // Preserve corrupt file for manual recovery
                try
                {
                    File.Copy(_jobsFilePath, _jobsFilePath + ".corrupt", overwrite: true);
                }
                catch
                {
                    // best-effort backup
                }

                System.Diagnostics.Debug.WriteLine($"Jobs file parse error: {ex.Message}. Backup saved to {_jobsFilePath}.corrupt");
                LoadError = "Jobs file was corrupted and could not be loaded. A backup was saved to jobs.json.corrupt. Starting with empty job list.";

                _jobs.Clear();
                _loaded = true;
            }
        }

        /// <summary>
        /// Clears in-memory state and reloads from disk.
        /// </summary>
        public void Reload()
        {
            _jobs.Clear();
            _loaded = false;
            Load();
        }

        /// <summary>
        /// Saves (creates or updates) a job definition.
        /// </summary>
        public void Save(JobDefinition job)
        {
            ArgumentNullException.ThrowIfNull(job);

            // Validate and normalize name
            job.Name = (job.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(job.Name))
                throw new ArgumentException("Job name cannot be empty.", nameof(job));
            if (job.Name.Length > MaxNameLength)
                throw new ArgumentException($"Job name cannot exceed {MaxNameLength} characters.", nameof(job));

            // Enforce unique name (case-insensitive), excluding the job being saved
            foreach (var existing in _jobs.Values)
            {
                if (string.Equals(existing.Name, job.Name, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(existing.Id, job.Id, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"A job with the name '{job.Name}' already exists.", nameof(job));
                }
            }

            job.ModifiedUtc = DateTime.UtcNow;
            _jobs[job.Id] = job;

            PersistToDisk();
            JobsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Deletes a job definition by ID.
        /// </summary>
        /// <param name="jobId">The job ID to delete.</param>
        /// <param name="cleanupCredentials">If true and the job uses Stored credentials, deletes the stored password.</param>
        /// <returns>True if the job was found and deleted; false otherwise.</returns>
        public bool Delete(string jobId, bool cleanupCredentials = true)
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            _jobs.Remove(jobId);

            if (cleanupCredentials && job.CredentialMode == CredentialMode.Stored)
            {
                try
                {
                    _credentialProvider.DeletePassword(CredentialTargets.JobPasswordTarget(jobId));
                }
                catch
                {
                    // best-effort credential cleanup
                }
            }

            PersistToDisk();
            JobsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Gets a job definition by ID, or null if not found.
        /// </summary>
        public JobDefinition? Get(string jobId)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }

        /// <summary>
        /// Returns all jobs targeting the specified preset name (case-insensitive).
        /// </summary>
        public IReadOnlyList<JobDefinition> GetJobsReferencingPreset(string presetName)
        {
            return _jobs.Values
                .Where(j => j.TargetType == JobTargetType.Preset
                    && string.Equals(j.TargetName, presetName, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Returns all jobs targeting the specified folder path (case-insensitive).
        /// </summary>
        public IReadOnlyList<JobDefinition> GetJobsReferencingFolder(string folderPath)
        {
            return _jobs.Values
                .Where(j => j.TargetType == JobTargetType.Folder
                    && string.Equals(j.TargetName, folderPath, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Persists the current in-memory jobs to disk with .bak backup.
        /// </summary>
        private void PersistToDisk()
        {
            // Best-effort backup of existing file
            if (File.Exists(_jobsFilePath))
            {
                try { File.Copy(_jobsFilePath, _jobsFilePath + ".bak", overwrite: true); }
                catch { /* best-effort backup */ }
            }

            var wrapper = new JobsFileWrapper
            {
                Version = 1,
                Jobs = _jobs.Values.ToList()
            };

            var json = JsonConvert.SerializeObject(wrapper, Formatting.Indented);
            File.WriteAllText(_jobsFilePath, json);
        }

        /// <summary>
        /// Imports hosts from a CSV file into the specified job.
        /// </summary>
        /// <param name="jobId">The job to update.</param>
        /// <param name="filePath">Path to the CSV file.</param>
        /// <exception cref="KeyNotFoundException">Job not found.</exception>
        /// <exception cref="ArgumentException">CSV missing required Host_IP column.</exception>
        public void ImportHostsFromCsv(string jobId, string filePath)
        {
            var job = Get(jobId)
                ?? throw new KeyNotFoundException($"Job with ID '{jobId}' not found.");

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
                throw new ArgumentException("CSV file is empty and has no header row.", nameof(filePath));

            var columns = ParseCsvLine(lines[0]);
            if (!columns.Any(c => string.Equals(c, "Host_IP", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("CSV file must contain a 'Host_IP' column.", nameof(filePath));

            var hosts = new List<Dictionary<string, string>>();
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = ParseCsvLine(line);
                var row = new Dictionary<string, string>();
                for (int j = 0; j < columns.Count && j < values.Count; j++)
                {
                    row[columns[j]] = values[j];
                }
                hosts.Add(row);
            }

            job.Hosts = hosts;
            job.HostColumns = columns;
            Save(job);
        }

        /// <summary>
        /// Converts raw row data into the (hosts, columns) format used by JobDefinition.
        /// Pure data transformation with no WinForms dependency.
        /// </summary>
        public static (List<Dictionary<string, string>> Hosts, List<string> Columns) ExtractHostDataFromRows(
            IReadOnlyList<Dictionary<string, string>> rows,
            IReadOnlyList<string> columnNames)
        {
            var columns = new List<string>(columnNames);
            var hosts = new List<Dictionary<string, string>>();

            foreach (var row in rows)
            {
                hosts.Add(new Dictionary<string, string>(row));
            }

            return (hosts, columns);
        }

        /// <summary>
        /// Parses a single CSV line, handling quoted fields containing commas.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // Check for escaped quote ("")
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++; // skip next quote
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        /// <summary>
        /// Wrapper for the jobs.json file format.
        /// </summary>
        private sealed class JobsFileWrapper
        {
            public int Version { get; set; } = 1;
            public List<JobDefinition> Jobs { get; set; } = new();
        }
    }
}
