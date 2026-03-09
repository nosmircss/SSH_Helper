using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Provides export/import serialization for job definitions.
    /// Supports .sshjobs JSON files and GZip+Base64 clipboard strings.
    /// </summary>
    public sealed class JobExportService
    {
        /// <summary>
        /// Represents a single job entry prepared for import with conflict metadata.
        /// </summary>
        public class ImportJobEntry
        {
            /// <summary>The job definition (with new ID assigned).</summary>
            public JobDefinition Job { get; set; } = new();

            /// <summary>Whether this job's name conflicts with an existing job.</summary>
            public bool HasConflict { get; set; }

            /// <summary>The resolved name (original or with deterministic import suffix).</summary>
            public string ResolvedName { get; set; } = string.Empty;

            /// <summary>Whether the target preset/folder was not found (caller sets this).</summary>
            public bool MissingTarget { get; set; }
        }

        /// <summary>
        /// Exports jobs to a .sshjobs JSON file.
        /// Credentials are stripped and running state cleared.
        /// </summary>
        public void ExportToFile(IReadOnlyList<JobDefinition> jobs, string filePath)
        {
            var cleanedJobs = jobs.Select(CloneForExport).ToList();
            var doc = new JobExportDocument
            {
                Version = 1,
                ExportedUtc = DateTime.UtcNow,
                Jobs = cleanedJobs
            };

            var json = JsonConvert.SerializeObject(doc, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Exports jobs to a GZip+Base64 encoded string for clipboard sharing.
        /// Credentials are stripped and running state cleared.
        /// </summary>
        public string ExportToString(IReadOnlyList<JobDefinition> jobs)
        {
            var cleanedJobs = jobs.Select(CloneForExport).ToList();
            var doc = new JobExportDocument
            {
                Version = 1,
                ExportedUtc = DateTime.UtcNow,
                Jobs = cleanedJobs
            };

            var json = JsonConvert.SerializeObject(doc);
            return CompressAndEncode(json);
        }

        /// <summary>
        /// Imports jobs from a .sshjobs JSON file.
        /// Returns empty list on invalid or corrupt input.
        /// </summary>
        public List<JobDefinition> ImportFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return new List<JobDefinition>();

                var json = File.ReadAllText(filePath);
                var doc = JsonConvert.DeserializeObject<JobExportDocument>(json);
                return doc?.Jobs ?? new List<JobDefinition>();
            }
            catch
            {
                return new List<JobDefinition>();
            }
        }

        /// <summary>
        /// Imports jobs from a GZip+Base64 encoded string.
        /// Returns empty list on invalid or corrupt input.
        /// </summary>
        public List<JobDefinition> ImportFromString(string encoded)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(encoded))
                    return new List<JobDefinition>();

                var json = DecompressEncoded(encoded);
                var doc = JsonConvert.DeserializeObject<JobExportDocument>(json);
                return doc?.Jobs ?? new List<JobDefinition>();
            }
            catch
            {
                return new List<JobDefinition>();
            }
        }

        /// <summary>
        /// Prepares imported jobs with conflict detection and new GUID assignment.
        /// Generates new IDs for all imported jobs to avoid ID collision.
        /// Resolves conflicting names using deterministic imported suffixes.
        /// </summary>
        public List<ImportJobEntry> PrepareImport(IReadOnlyList<JobDefinition> importJobs,
            IReadOnlyCollection<string> existingNames)
        {
            var nameSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
            var entries = new List<ImportJobEntry>();

            foreach (var job in importJobs)
            {
                var cloned = DeepClone(job);
                cloned.Id = Guid.NewGuid().ToString("N");

                string resolvedName = ReserveImportName(cloned.Name, nameSet, out var hasConflict);

                entries.Add(new ImportJobEntry
                {
                    Job = cloned,
                    HasConflict = hasConflict,
                    ResolvedName = resolvedName
                });
            }

            return entries;
        }

        /// <summary>
        /// Deep clones a job and strips credentials, running state, and any legacy drift flag for export.
        /// </summary>
        private static JobDefinition CloneForExport(JobDefinition source)
        {
            var cloned = DeepClone(source);
            cloned.CredentialMode = CredentialMode.InheritFromApp;
            cloned.RunningState = null;
            cloned.HasDriftWarning = false;
            return cloned;
        }

        /// <summary>
        /// Deep clones a JobDefinition via JSON serialization round-trip.
        /// </summary>
        private static JobDefinition DeepClone(JobDefinition source)
        {
            var json = JsonConvert.SerializeObject(source);
            return JsonConvert.DeserializeObject<JobDefinition>(json)!;
        }

        private static string ReserveImportName(string originalName, HashSet<string> reservedNames, out bool hasConflict)
        {
            if (reservedNames.Add(originalName))
            {
                hasConflict = false;
                return originalName;
            }

            hasConflict = true;

            for (var suffixIndex = 1; ; suffixIndex++)
            {
                var candidate = suffixIndex == 1
                    ? $"{originalName} (imported)"
                    : $"{originalName} (imported {suffixIndex})";

                if (reservedNames.Add(candidate))
                    return candidate;
            }
        }

        private static string CompressAndEncode(string text)
        {
            byte[] raw = Encoding.UTF8.GetBytes(text);
            using var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(raw, 0, raw.Length);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        private static string DecompressEncoded(string encoded)
        {
            byte[] compressed = Convert.FromBase64String(encoded);
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}
