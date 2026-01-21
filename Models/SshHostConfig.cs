namespace SSH_Helper.Models
{
    /// <summary>
    /// Resolved SSH configuration for a specific host.
    /// Contains merged options from all matching Host blocks in the SSH config file.
    /// </summary>
    public class SshHostConfig
    {
        /// <summary>
        /// The actual hostname or IP to connect to (from HostName directive).
        /// If not specified, uses the original host pattern.
        /// </summary>
        public string? HostName { get; set; }

        /// <summary>
        /// SSH port number (from Port directive).
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Username for authentication (from User directive).
        /// </summary>
        public string? User { get; set; }

        /// <summary>
        /// Path to the private key file (from IdentityFile directive).
        /// Path is expanded (~ replaced with user home directory).
        /// </summary>
        public string? IdentityFile { get; set; }

        /// <summary>
        /// Preferred host key algorithms in order of preference (from HostKeyAlgorithms directive).
        /// Values are OpenSSH algorithm names (e.g., "ssh-ed25519", "ssh-rsa").
        /// </summary>
        public string[]? HostKeyAlgorithms { get; set; }

        /// <summary>
        /// Preferred encryption ciphers in order of preference (from Ciphers directive).
        /// Values are OpenSSH cipher names (e.g., "aes256-ctr", "chacha20-poly1305@openssh.com").
        /// </summary>
        public string[]? Ciphers { get; set; }
    }
}
