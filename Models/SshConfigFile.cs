namespace SSH_Helper.Models
{
    /// <summary>
    /// Represents a parsed SSH config file.
    /// </summary>
    public class SshConfigFile
    {
        /// <summary>
        /// Path to the SSH config file.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Last modified timestamp of the file (for cache invalidation).
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// List of Host blocks parsed from the config file.
        /// </summary>
        public List<SshConfigEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// Represents a single Host block in an SSH config file.
    /// </summary>
    public class SshConfigEntry
    {
        /// <summary>
        /// Host patterns this entry applies to (supports wildcards * and ?).
        /// A single Host line can have multiple patterns: "Host server1 server2 *.example.com"
        /// </summary>
        public List<string> HostPatterns { get; set; } = new();

        /// <summary>
        /// Key-value pairs of SSH options within this Host block.
        /// Keys are case-insensitive (stored lowercase).
        /// </summary>
        public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
