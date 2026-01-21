using SSH_Helper.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Manages SSH config file loading, caching, and host resolution.
    /// </summary>
    public class SshConfigService
    {
        private readonly SshConfigParser _parser = new();
        private SshConfigFile? _cachedConfig;
        private DateTime _lastCacheCheck;
        private static readonly TimeSpan CacheCheckInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Gets the default SSH config file path: %USERPROFILE%\.ssh\config
        /// </summary>
        public string DefaultConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "config");

        /// <summary>
        /// Checks if the default SSH config file exists.
        /// </summary>
        public bool ConfigFileExists() => File.Exists(DefaultConfigPath);

        /// <summary>
        /// Loads and parses the SSH config file, using cache if still valid.
        /// </summary>
        public SshConfigFile? LoadConfig(string? path = null)
        {
            var configPath = path ?? DefaultConfigPath;

            if (!File.Exists(configPath))
            {
                _cachedConfig = null;
                return null;
            }

            // Check if we need to reload (file modified since last load)
            var now = DateTime.UtcNow;
            if (_cachedConfig != null &&
                _cachedConfig.FilePath == configPath &&
                now - _lastCacheCheck < CacheCheckInterval)
            {
                return _cachedConfig;
            }

            _lastCacheCheck = now;

            // Check if file has been modified
            try
            {
                var lastModified = File.GetLastWriteTimeUtc(configPath);
                if (_cachedConfig != null &&
                    _cachedConfig.FilePath == configPath &&
                    _cachedConfig.LastModified == lastModified)
                {
                    return _cachedConfig;
                }

                // Reload config
                _cachedConfig = _parser.Parse(configPath);
                return _cachedConfig;
            }
            catch
            {
                return _cachedConfig;
            }
        }

        /// <summary>
        /// Gets the resolved SSH configuration for a specific host.
        /// Returns null if no matching configuration is found.
        /// </summary>
        public SshHostConfig? GetHostConfig(string hostname, string? configPath = null)
        {
            var config = LoadConfig(configPath);
            if (config == null)
                return null;

            return _parser.GetConfigForHost(config, hostname);
        }

        /// <summary>
        /// Clears the cached configuration.
        /// Call this when the user toggles the SSH config setting.
        /// </summary>
        public void ClearCache()
        {
            _cachedConfig = null;
            _lastCacheCheck = DateTime.MinValue;
        }
    }
}
