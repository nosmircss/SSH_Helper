using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// Options for the vault command - reads, writes, or patches secrets from HashiCorp Vault.
    /// </summary>
    public class VaultStepOptions
    {
        /// <summary>
        /// The Vault secret path (e.g. "secret/data/ssh/server").
        /// </summary>
        public string Path { get; set; } = "";

        /// <summary>
        /// Optional named Vault connection profile to use.
        /// </summary>
        public string? Profile { get; set; }

        /// <summary>
        /// Single key to read from the secret data map. Result is stored in <see cref="Into"/>.
        /// </summary>
        public string? Key { get; set; }

        /// <summary>
        /// Multiple keys to read. Each entry maps a secret field name to a target variable name.
        /// </summary>
        public Dictionary<string, string>? Keys { get; set; }

        /// <summary>
        /// Variable name to store the result in (used with <see cref="Key"/>).
        /// </summary>
        public string? Into { get; set; }

        /// <summary>
        /// Specific secret version to read (KV v2 only). Defaults to latest.
        /// </summary>
        public int? Version { get; set; }

        /// <summary>
        /// Key/value pairs to write to the Vault path (creates or overwrites the secret).
        /// </summary>
        public Dictionary<string, string>? Write { get; set; }

        /// <summary>
        /// Key/value pairs to patch into the Vault path (merges with existing data, KV v2 only).
        /// </summary>
        public Dictionary<string, string>? Patch { get; set; }

        /// <summary>
        /// Error handling strategy: "stop" (default) or "continue".
        /// </summary>
        public string? OnError { get; set; }
    }
}
