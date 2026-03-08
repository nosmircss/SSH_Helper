using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Resolves the effective scheduler history policy for a specific job.
    /// </summary>
    internal static class SchedulerHistoryPolicyResolver
    {
        internal static JobHistoryRetentionOptions Resolve(AppConfiguration config, JobDefinition? job)
        {
            ArgumentNullException.ThrowIfNull(config);

            var defaults = new JobHistoryRetentionOptions();
            return new JobHistoryRetentionOptions
            {
                MaxRuns = NormalizePositive(job?.MaxHistoryRuns, config.DefaultMaxHistoryRuns, defaults.MaxRuns),
                RetentionDays = NormalizePositive(job?.HistoryRetentionDays, config.DefaultHistoryRetentionDays, defaults.RetentionDays),
                MaxOutputChars = NormalizePositive(null, config.MaxJobOutputCharsPerHost, defaults.MaxOutputChars)
            };
        }

        private static int NormalizePositive(int? overrideValue, int configuredValue, int fallbackValue)
        {
            if (overrideValue.HasValue && overrideValue.Value > 0)
            {
                return overrideValue.Value;
            }

            if (configuredValue > 0)
            {
                return configuredValue;
            }

            return fallbackValue;
        }
    }
}
