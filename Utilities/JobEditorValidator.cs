using SSH_Helper.Models;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Pure static validation logic for the job editor dialog.
    /// All methods return null on success or an error message string on failure.
    /// </summary>
    internal static class JobEditorValidator
    {
        private const int MaxNameLength = 100;

        /// <summary>
        /// Validates the job name field.
        /// </summary>
        public static string? ValidateName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Job name is required";

            if (name.Trim().Length > MaxNameLength)
                return $"Job name must be {MaxNameLength} characters or less";

            return null;
        }

        /// <summary>
        /// Validates that a target preset or folder has been selected.
        /// </summary>
        public static string? ValidateTarget(string? targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
                return "Please select a target preset/folder";

            return null;
        }

        /// <summary>
        /// Validates the cron expression when schedule type is Recurring.
        /// Delegates to InputValidator for actual cron parsing.
        /// </summary>
        public static string? ValidateCron(ScheduleType scheduleType, string? cronExpression)
        {
            if (scheduleType != ScheduleType.Recurring)
                return null;

            return InputValidator.ValidateCronExpression(cronExpression);
        }

        /// <summary>
        /// Validates one-time schedule date is in the future.
        /// </summary>
        public static string? ValidateOneTimeDate(ScheduleType scheduleType, DateTime? dateTimeUtc)
        {
            if (scheduleType != ScheduleType.OneTime)
                return null;

            if (!dateTimeUtc.HasValue)
                return "One-time schedule date is required";

            if (!InputValidator.IsFutureDate(dateTimeUtc.Value))
                return "One-time schedule must be in the future";

            return null;
        }

        /// <summary>
        /// Validates that at least one host with a valid IP is present.
        /// </summary>
        public static string? ValidateHosts(IReadOnlyList<Dictionary<string, string>>? hosts)
        {
            if (hosts == null || hosts.Count == 0)
                return "At least one host with a valid IP is required";

            var hasValidHost = false;
            foreach (var host in hosts)
            {
                if (host.TryGetValue("Host_IP", out var ip) && !string.IsNullOrWhiteSpace(ip))
                {
                    hasValidHost = true;
                    break;
                }
            }

            if (!hasValidHost)
                return "At least one host with a valid IP is required";

            return null;
        }

        /// <summary>
        /// Validates stored credentials when credential mode is Stored.
        /// </summary>
        public static string? ValidateStoredCredentials(CredentialMode mode, string? username)
        {
            if (mode != CredentialMode.Stored)
                return null;

            if (string.IsNullOrWhiteSpace(username))
                return "Username is required for stored credentials";

            return null;
        }

        /// <summary>
        /// Validates per-host credentials when credential mode is PerHostColumn.
        /// </summary>
        public static string? ValidatePerHostCredentials(
            CredentialMode mode,
            IReadOnlyList<Dictionary<string, string>>? hosts,
            IReadOnlyList<string>? hostColumns)
        {
            if (mode != CredentialMode.PerHostColumn)
                return null;

            var hasUsernameColumn = hostColumns?.Any(column =>
                string.Equals(column, "username", StringComparison.OrdinalIgnoreCase)) == true;
            var hasPasswordColumn = hostColumns?.Any(column =>
                string.Equals(column, "password", StringComparison.OrdinalIgnoreCase)) == true;

            if (!hasUsernameColumn || !hasPasswordColumn)
                return "Per-host credentials require 'username' and 'password' columns in the Hosts tab.";

            if (hosts == null)
                return null;

            for (var index = 0; index < hosts.Count; index++)
            {
                var row = hosts[index];
                if (!row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                    continue;

                if (!TryGetRowValue(row, "username", out var username)
                    || !TryGetRowValue(row, "password", out var password)
                    || string.IsNullOrWhiteSpace(username)
                    || string.IsNullOrWhiteSpace(password))
                {
                    return $"Host row {index + 1} is missing username or password required for per-host credentials.";
                }
            }

            return null;
        }

        /// <summary>
        /// Runs all validators in sequence, returning the first error or null if all valid.
        /// </summary>
        public static string? ValidateAll(
            string? name,
            string? targetName,
            ScheduleType scheduleType,
            string? cronExpression,
            DateTime? oneTimeUtc,
            IReadOnlyList<Dictionary<string, string>>? hosts,
            IReadOnlyList<string>? hostColumns,
            CredentialMode credentialMode,
            string? storedUsername)
        {
            return ValidateName(name)
                ?? ValidateTarget(targetName)
                ?? ValidateCron(scheduleType, cronExpression)
                ?? ValidateOneTimeDate(scheduleType, oneTimeUtc)
                ?? ValidateHosts(hosts)
                ?? ValidatePerHostCredentials(credentialMode, hosts, hostColumns)
                ?? ValidateStoredCredentials(credentialMode, storedUsername);
        }

        private static bool TryGetRowValue(
            IReadOnlyDictionary<string, string> row,
            string key,
            out string value)
        {
            foreach (var kvp in row)
            {
                if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kvp.Value;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }
    }
}
