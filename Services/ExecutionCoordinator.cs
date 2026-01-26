using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Prepares execution inputs and coordinates preset execution through the SSH service.
    /// </summary>
    public sealed class ExecutionCoordinator
    {
        private readonly SshExecutionService _sshService;
        private readonly ConfigurationService _configService;

        public ExecutionCoordinator(SshExecutionService sshService, ConfigurationService configService)
        {
            _sshService = sshService ?? throw new ArgumentNullException(nameof(sshService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        /// <summary>
        /// Indicates whether an execution is currently running.
        /// </summary>
        public bool IsRunning => _sshService.IsRunning;

        /// <summary>
        /// Creates a preset and timeout options for execution based on current configuration.
        /// </summary>
        public ExecutionPreparation PrepareExecution(string commands, int commandTimeoutSeconds)
        {
            int connectionTimeoutSeconds = _configService.GetCurrent().ConnectionTimeout;
            var timeouts = SshTimeoutOptions.Create(commandTimeoutSeconds, connectionTimeoutSeconds);
            var preset = new PresetInfo { Commands = commands };
            return new ExecutionPreparation(preset, timeouts, commandTimeoutSeconds, connectionTimeoutSeconds);
        }

        /// <summary>
        /// Executes a prepared preset on multiple hosts.
        /// </summary>
        public Task<List<ExecutionResult>> ExecutePresetAsync(
            IEnumerable<HostConnection> hosts,
            ExecutionPreparation preparation,
            string defaultUsername,
            string defaultPassword,
            bool showHeader = true)
        {
            return _sshService.ExecutePresetAsync(hosts, preparation.Preset, defaultUsername, defaultPassword, preparation.Timeouts, showHeader);
        }
    }

    /// <summary>
    /// Prepared execution inputs used by ExecutionCoordinator.
    /// </summary>
    public sealed record ExecutionPreparation(
        PresetInfo Preset,
        SshTimeoutOptions Timeouts,
        int CommandTimeoutSeconds,
        int ConnectionTimeoutSeconds);
}
