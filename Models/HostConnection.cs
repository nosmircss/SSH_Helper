namespace SSH_Helper.Models
{
    /// <summary>
    /// Represents connection details for a single SSH host.
    /// </summary>
    public class HostConnection
    {
        public string IpAddress { get; set; } = string.Empty;
        private int _port = 22;
        public int Port { get => _port; set { _port = value; HasExplicitPort = true; } }
        public bool HasExplicitPort { get; private set; }
        internal bool HasEndpointPort { get; private set; }
        public ConnectionPortPolicy? PortPolicy { get; set; }
        public bool IsPortResolvedForRun { get; internal set; }
        // Run clones carry identity, never credentials/history under a discovered endpoint.
        internal HostConnection? OriginalHost { get; set; }
        internal HostConnection EventHost => OriginalHost ?? this;

        internal HostConnection CloneForRun()
        {
            var clone = (HostConnection)MemberwiseClone();
            clone.Variables = new Dictionary<string, string>(Variables, StringComparer.OrdinalIgnoreCase);
            clone.HostKeyAlgorithms = HostKeyAlgorithms?.ToArray();
            clone.Ciphers = Ciphers?.ToArray();
            clone.OriginalHost = EventHost;
            return clone;
        }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();

        /// <summary>
        /// Path to identity file (private key) for key-based authentication.
        /// If set, takes precedence over password authentication.
        /// </summary>
        public string? IdentityFile { get; set; }

        /// <summary>
        /// Passphrase for the identity file (if the key is encrypted).
        /// </summary>
        public string? IdentityFilePassphrase { get; set; }

        /// <summary>
        /// Preferred host key algorithms from SSH config (OpenSSH names).
        /// </summary>
        public string[]? HostKeyAlgorithms { get; set; }

        /// <summary>
        /// Preferred encryption ciphers from SSH config (OpenSSH names).
        /// </summary>
        public string[]? Ciphers { get; set; }

        /// <summary>
        /// Parses a host string in the format "ip:port" or "ip".
        /// </summary>
        public static HostConnection Parse(string hostWithPort)
        {
            var result = new HostConnection();

            if (string.IsNullOrWhiteSpace(hostWithPort))
                return result;

            var parts = hostWithPort.Split(':');
            result.IpAddress = parts[0];

            if (parts.Length > 1 && int.TryParse(parts[1], out int port) && port > 0 && port <= 65535)
            {
                result.Port = port;
                result.HasEndpointPort = true;
            }

            return result;
        }

        /// <summary>
        /// Validates the IP address format.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(IpAddress))
                return false;

            return Utilities.InputValidator.IsValidHostOrIp(ToString());
        }

        /// <summary>
        /// Merges SSH config values into this connection.
        /// Grid values take precedence over config values.
        /// </summary>
        public void ApplySshConfig(SshHostConfig? config)
        {
            if (config == null) return;

            // Explicit 22 and pending discovery policies must not be replaced by config.
            if (!HasExplicitPort && PortPolicy == null && config.Port.HasValue)
                _port = config.Port.Value;

            // Username: Only use config if grid is empty
            if (string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(config.User))
                Username = config.User;

            // IdentityFile: Only use config if not already set
            if (string.IsNullOrEmpty(IdentityFile) && !string.IsNullOrEmpty(config.IdentityFile))
                IdentityFile = config.IdentityFile;

            // Algorithms: Only use config if not already set
            HostKeyAlgorithms ??= config.HostKeyAlgorithms;
            Ciphers ??= config.Ciphers;
        }

        public override string ToString() => Port == 22 ? IpAddress : $"{IpAddress}:{Port}";
    }
}
