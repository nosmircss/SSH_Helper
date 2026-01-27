namespace SSH_Helper.Models
{
    /// <summary>
    /// Represents connection details for a single SSH host.
    /// </summary>
    public class HostConnection
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
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

            // Port: Only use config if grid has default (22)
            if (Port == 22 && config.Port.HasValue)
                Port = config.Port.Value;

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
