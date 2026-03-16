using SSH_Helper.Models;

namespace SSH_Helper.Utilities
{
    internal static class SchedulerJobIntegrityUtilities
    {
        public static void ApplyMissingTargetImportState(JobDefinition job)
        {
            ArgumentNullException.ThrowIfNull(job);

            job.IsEnabled = false;
            job.DisabledReason = job.TargetType switch
            {
                JobTargetType.Folder => $"Missing folder target '{job.TargetName}'",
                JobTargetType.CustomPreset => "Missing custom preset content",
                _ => $"Missing preset target '{job.TargetName}'"
            };
        }

        public static string FormatStoredCredentialNote(bool hasStoredPassword)
        {
            return hasStoredPassword
                ? "Credentials are stored in Windows Credential Manager. Leave password blank to keep the current secret."
                : "Credentials are stored in Windows Credential Manager. Enter a password to store it for this job.";
        }
    }
}
