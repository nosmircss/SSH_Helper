using System.Text.RegularExpressions;
using SSH_Helper.Models;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Parses OpenSSH config files (~/.ssh/config).
    /// Supports: Host, HostName, Port, User, IdentityFile, HostKeyAlgorithms, Ciphers
    /// </summary>
    public class SshConfigParser
    {
        private static readonly HashSet<string> SupportedOptions = new(StringComparer.OrdinalIgnoreCase)
        {
            "hostname", "port", "user", "identityfile", "hostkeyalgorithms", "ciphers"
        };

        /// <summary>
        /// Parses an SSH config file from the specified path.
        /// </summary>
        public SshConfigFile Parse(string filePath)
        {
            var result = new SshConfigFile
            {
                FilePath = filePath
            };

            if (!File.Exists(filePath))
                return result;

            result.LastModified = File.GetLastWriteTimeUtc(filePath);

            try
            {
                using var stream = File.OpenRead(filePath);
                return Parse(stream, filePath, result.LastModified);
            }
            catch
            {
                // Return empty config on error
                return result;
            }
        }

        /// <summary>
        /// Parses an SSH config from a stream (useful for testing).
        /// </summary>
        public SshConfigFile Parse(Stream stream, string filePath = "", DateTime? lastModified = null)
        {
            var result = new SshConfigFile
            {
                FilePath = filePath,
                LastModified = lastModified ?? DateTime.UtcNow
            };

            using var reader = new StreamReader(stream);
            SshConfigEntry? currentEntry = null;

            while (reader.ReadLine() is { } line)
            {
                // Skip empty lines and comments
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                // Parse key-value pair
                var (key, value) = ParseLine(trimmed);
                if (string.IsNullOrEmpty(key))
                    continue;

                if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                {
                    // Start a new Host block
                    currentEntry = new SshConfigEntry
                    {
                        HostPatterns = ParseHostPatterns(value)
                    };
                    result.Entries.Add(currentEntry);
                }
                else if (currentEntry != null && SupportedOptions.Contains(key))
                {
                    // Add option to current block (first occurrence wins)
                    if (!currentEntry.Options.ContainsKey(key))
                    {
                        currentEntry.Options[key] = value;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the resolved configuration for a specific hostname.
        /// Handles wildcard matching and merges all matching Host blocks.
        /// First match wins for each option (OpenSSH semantics).
        /// </summary>
        public SshHostConfig? GetConfigForHost(SshConfigFile configFile, string hostname)
        {
            if (configFile.Entries.Count == 0 || string.IsNullOrEmpty(hostname))
                return null;

            var result = new SshHostConfig();
            var hasMatch = false;

            foreach (var entry in configFile.Entries)
            {
                if (!MatchesHost(entry.HostPatterns, hostname))
                    continue;

                hasMatch = true;

                // Apply options (first match wins)
                if (result.HostName == null && entry.Options.TryGetValue("hostname", out var hostName))
                    result.HostName = hostName;

                if (result.Port == null && entry.Options.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var port))
                    result.Port = port;

                if (result.User == null && entry.Options.TryGetValue("user", out var user))
                    result.User = user;

                if (result.IdentityFile == null && entry.Options.TryGetValue("identityfile", out var identityFile))
                    result.IdentityFile = ExpandPath(identityFile);

                if (result.HostKeyAlgorithms == null && entry.Options.TryGetValue("hostkeyalgorithms", out var hostKeyAlgs))
                    result.HostKeyAlgorithms = ParseCommaList(hostKeyAlgs);

                if (result.Ciphers == null && entry.Options.TryGetValue("ciphers", out var ciphers))
                    result.Ciphers = ParseCommaList(ciphers);
            }

            return hasMatch ? result : null;
        }

        /// <summary>
        /// Parses a line into key-value pair.
        /// Handles both "Key Value" and "Key=Value" formats.
        /// </summary>
        private static (string key, string value) ParseLine(string line)
        {
            // Handle Key=Value format
            var eqIndex = line.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = line[..eqIndex].Trim();
                var value = line[(eqIndex + 1)..].Trim();
                return (key, StripQuotes(value));
            }

            // Handle "Key Value" format (first whitespace separates key from value)
            var spaceIndex = line.IndexOfAny(new[] { ' ', '\t' });
            if (spaceIndex > 0)
            {
                var key = line[..spaceIndex].Trim();
                var value = line[(spaceIndex + 1)..].Trim();
                return (key, StripQuotes(value));
            }

            return (string.Empty, string.Empty);
        }

        /// <summary>
        /// Parses host patterns from a Host line (can have multiple patterns).
        /// </summary>
        private static List<string> ParseHostPatterns(string value)
        {
            return value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();
        }

        /// <summary>
        /// Checks if a hostname matches any of the host patterns.
        /// Supports * (match any) and ? (match single char) wildcards.
        /// </summary>
        private static bool MatchesHost(List<string> patterns, string hostname)
        {
            foreach (var pattern in patterns)
            {
                if (MatchesPattern(pattern, hostname))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Matches a hostname against a single pattern with wildcard support.
        /// </summary>
        private static bool MatchesPattern(string pattern, string hostname)
        {
            // Simple case: exact match
            if (pattern.Equals(hostname, StringComparison.OrdinalIgnoreCase))
                return true;

            // Handle wildcards: convert to regex
            // * matches any sequence, ? matches single char
            var regexPattern = "^" +
                Regex.Escape(pattern)
                    .Replace("\\*", ".*")
                    .Replace("\\?", ".") +
                "$";

            try
            {
                return Regex.IsMatch(hostname, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Expands ~ to the user's home directory.
        /// </summary>
        private static string ExpandPath(string path)
        {
            if (path.StartsWith("~/") || path.StartsWith("~\\"))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, path[2..]);
            }

            if (path == "~")
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            return path;
        }

        /// <summary>
        /// Parses a comma-separated list of values.
        /// </summary>
        private static string[] ParseCommaList(string value)
        {
            return value.Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToArray();
        }

        /// <summary>
        /// Strips surrounding quotes from a value.
        /// </summary>
        private static string StripQuotes(string value)
        {
            if (value.Length >= 2)
            {
                if ((value.StartsWith('"') && value.EndsWith('"')) ||
                    (value.StartsWith('\'') && value.EndsWith('\'')))
                {
                    return value[1..^1];
                }
            }
            return value;
        }
    }
}
