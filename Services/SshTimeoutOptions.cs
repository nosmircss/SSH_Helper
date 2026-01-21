namespace SSH_Helper.Services
{
    /// <summary>
    /// Configuration options for SSH connection and command timeouts.
    /// </summary>
    public class SshTimeoutOptions
    {
        /// <summary>
        /// Timeout for establishing the initial SSH connection.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Maximum time to wait for a single command to complete.
        /// </summary>
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Maximum time to wait with no data received before considering the stream idle.
        /// </summary>
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Interval between polling the stream for new data.
        /// </summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Time to wait for the initial prompt after connection.
        /// Should be long enough for devices that show banners/EULA (e.g., FortiGate).
        /// </summary>
        public TimeSpan InitialPromptTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Creates default timeout options.
        /// </summary>
        public static SshTimeoutOptions Default => new();

        /// <summary>
        /// Creates timeout options from a single timeout value (backwards compatibility).
        /// </summary>
        public static SshTimeoutOptions FromSeconds(int seconds)
        {
            return new SshTimeoutOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(seconds),
                CommandTimeout = TimeSpan.FromSeconds(seconds),
                IdleTimeout = TimeSpan.FromSeconds(Math.Max(10, seconds / 3))
            };
        }

        /// <summary>
        /// Creates timeout options with separate command and connection timeouts.
        /// </summary>
        /// <param name="commandTimeoutSeconds">Command timeout in seconds</param>
        /// <param name="connectionTimeoutSeconds">Connection timeout in seconds</param>
        public static SshTimeoutOptions Create(int commandTimeoutSeconds, int connectionTimeoutSeconds)
        {
            return new SshTimeoutOptions
            {
                ConnectionTimeout = TimeSpan.FromSeconds(connectionTimeoutSeconds),
                CommandTimeout = TimeSpan.FromSeconds(commandTimeoutSeconds),
                IdleTimeout = TimeSpan.FromSeconds(Math.Max(10, commandTimeoutSeconds / 3))
            };
        }
    }
}
