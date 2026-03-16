using SSH_Helper.Models;
using SSH_Helper.Services;

namespace SSH_Helper.Utilities
{
    internal sealed class PresetSaveImpact
    {
        private static readonly IReadOnlyList<JobDefinition> EmptyJobs = Array.Empty<JobDefinition>();

        public static PresetSaveImpact None { get; } = new(string.Empty, null, EmptyJobs);

        public PresetSaveImpact(string presetName, string? folderPath, IReadOnlyList<JobDefinition> affectedJobs)
        {
            PresetName = presetName ?? string.Empty;
            FolderPath = folderPath;
            AffectedJobs = affectedJobs ?? EmptyJobs;
        }

        public string PresetName { get; }

        public string? FolderPath { get; }

        public IReadOnlyList<JobDefinition> AffectedJobs { get; }

        public bool HasAffectedJobs => AffectedJobs.Count > 0;
    }

    internal static class PresetSaveImpactResolver
    {
        public static PresetSaveImpact Resolve(
            PresetManager presetManager,
            string presetName,
            string? folderPath)
        {
            ArgumentNullException.ThrowIfNull(presetManager);

            var folderJobs = string.IsNullOrWhiteSpace(folderPath)
                ? Enumerable.Empty<JobDefinition>()
                : presetManager.GetJobsReferencingFolder(folderPath);

            return Resolve(
                presetName,
                folderPath,
                presetManager.GetJobsReferencingPreset(presetName),
                folderJobs);
        }

        internal static PresetSaveImpact Resolve(
            string presetName,
            string? folderPath,
            IEnumerable<JobDefinition> presetJobs,
            IEnumerable<JobDefinition> folderJobs)
        {
            ArgumentNullException.ThrowIfNull(presetJobs);
            ArgumentNullException.ThrowIfNull(folderJobs);

            if (string.IsNullOrWhiteSpace(presetName))
            {
                return PresetSaveImpact.None;
            }

            var affectedJobs = presetJobs
                .Concat(folderJobs)
                .GroupBy(static job => job.Id, StringComparer.Ordinal)
                .Select(static group => group.First())
                .OrderBy(static job => job.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static job => job.Id, StringComparer.Ordinal)
                .ToList()
                .AsReadOnly();

            return affectedJobs.Count == 0
                ? PresetSaveImpact.None
                : new PresetSaveImpact(presetName, folderPath, affectedJobs);
        }
    }
}
