using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Handles loading and saving application configuration.
    /// </summary>
    public class ConfigurationService
    {
        private readonly string _configFilePath;
        private AppConfiguration? _cachedConfig;

        public ConfigurationService(string? configFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
            {
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = Path.Combine(folder, "SSH_Helper");

                if (!Directory.Exists(appFolder))
                {
                    Directory.CreateDirectory(appFolder);
                }

                _configFilePath = Path.Combine(appFolder, "config.json");
                return;
            }

            var directory = Path.GetDirectoryName(configFilePath);
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("Config file path must include a directory.", nameof(configFilePath));

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _configFilePath = configFilePath;
        }

        public string ConfigFilePath => _configFilePath;

        /// <summary>
        /// If non-null, contains a message describing a config load error (e.g., corrupt file).
        /// The UI should check this after Load() and display a warning.
        /// </summary>
        public string? ConfigLoadError { get; private set; }

        /// <summary>
        /// Loads the configuration, creating a default one if it doesn't exist.
        /// </summary>
        public AppConfiguration Load()
        {
            if (!File.Exists(_configFilePath))
            {
                var defaultConfig = CreateDefaultConfiguration();
                Save(defaultConfig);
                _cachedConfig = defaultConfig;
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(_configFilePath);
                var config = ParseConfiguration(json);
                _cachedConfig = config;
                return config;
            }
            catch (Exception ex)
            {
                // Preserve the corrupt file so the user can recover data manually
                try
                {
                    var backupPath = _configFilePath + ".corrupt";
                    File.Copy(_configFilePath, backupPath, overwrite: true);
                }
                catch
                {
                    // If backup fails, continue with default config
                }

                System.Diagnostics.Debug.WriteLine($"Configuration parse error: {ex.Message}. A backup was saved to {_configFilePath}.corrupt");
                ConfigLoadError = $"Configuration file was corrupted and could not be loaded. A backup was saved to config.json.corrupt. Default settings have been applied.";

                var defaultConfig = CreateDefaultConfiguration();
                _cachedConfig = defaultConfig;
                return defaultConfig;
            }
        }

        /// <summary>
        /// Saves the configuration to disk.
        /// </summary>
        public void Save(AppConfiguration config)
        {
            try
            {
                NormalizeEnvironmentData(config);
                NormalizeCommandEditorSettings(config);

                // Keep a backup of the previous config in case the save is interrupted
                if (File.Exists(_configFilePath))
                {
                    try { File.Copy(_configFilePath, _configFilePath + ".bak", overwrite: true); }
                    catch { /* best-effort backup */ }
                }

                string json = JsonConvert.SerializeObject(config, Formatting.Indented);
                File.WriteAllText(_configFilePath, json);
                _cachedConfig = config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Updates specific fields in the configuration.
        /// </summary>
        public void Update(Action<AppConfiguration> updateAction)
        {
            var config = _cachedConfig ?? Load();
            updateAction(config);
            Save(config);
        }

        /// <summary>
        /// Gets the cached configuration or loads it.
        /// </summary>
        public AppConfiguration GetCurrent()
        {
            return _cachedConfig ?? Load();
        }

        /// <summary>
        /// Returns a deep-cloned snapshot of persisted environments and active environment name.
        /// </summary>
        public (Dictionary<string, EnvironmentConfig> Environments, string? ActiveEnvironment) LoadEnvironmentState()
        {
            var config = GetCurrent();
            NormalizeEnvironmentData(config);
            return (CloneEnvironmentMap(config.Environments), config.ActiveEnvironment);
        }

        /// <summary>
        /// Persists the complete environment state in one update operation.
        /// </summary>
        public void SaveEnvironmentState(Dictionary<string, EnvironmentConfig> environments, string? activeEnvironment)
        {
            environments ??= new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase);

            Update(config =>
            {
                config.Environments = CloneEnvironmentMap(environments);
                config.ActiveEnvironment = string.IsNullOrWhiteSpace(activeEnvironment) ? null : activeEnvironment;
                NormalizeEnvironmentData(config);
            });
        }

        /// <summary>
        /// Creates the synthetic legacy "Default" environment from SavedState.
        /// </summary>
        public EnvironmentConfig BuildLegacyDefaultEnvironment()
        {
            var config = GetCurrent();
            return EnvironmentConfig.FromApplicationState(EnvironmentConfig.DefaultName, config.SavedState);
        }

        private AppConfiguration ParseConfiguration(string json)
        {
            // First, deserialize all fields using standard deserialization
            var config = JsonConvert.DeserializeObject<AppConfiguration>(json) ?? new AppConfiguration();

            // Now handle legacy preset format (where value was just a string instead of PresetInfo object)
            var rootObj = JObject.Parse(json);
            var presetsToken = rootObj["Presets"] as JObject;
            if (presetsToken != null)
            {
                config.Presets.Clear();
                foreach (var prop in presetsToken.Properties())
                {
                    if (prop.Value.Type == JTokenType.String)
                    {
                        // Legacy format: value is just a command string
                        config.Presets[prop.Name] = new PresetInfo { Commands = prop.Value.ToString() };
                    }
                    else
                    {
                        var info = prop.Value.ToObject<PresetInfo>() ?? new PresetInfo();
                        info.Commands ??= "";
                        config.Presets[prop.Name] = info;
                    }
                }
            }

            NormalizeEnvironmentData(config);
            NormalizeCommandEditorSettings(config);
            return config;
        }

        private static void NormalizeEnvironmentData(AppConfiguration config)
        {
            config.Environments ??= new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase);

            // Guarantee case-insensitive lookup for environment keys.
            if (config.Environments.Comparer != StringComparer.OrdinalIgnoreCase)
            {
                config.Environments = new Dictionary<string, EnvironmentConfig>(config.Environments, StringComparer.OrdinalIgnoreCase);
            }

            var normalized = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in config.Environments)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                var environment = kvp.Value ?? new EnvironmentConfig();
                environment.Normalize(kvp.Key);
                normalized[kvp.Key] = environment;
            }

            config.Environments = normalized;

            if (!string.IsNullOrWhiteSpace(config.ActiveEnvironment) &&
                !string.Equals(config.ActiveEnvironment, EnvironmentConfig.DefaultName, StringComparison.OrdinalIgnoreCase) &&
                !config.Environments.ContainsKey(config.ActiveEnvironment))
            {
                config.ActiveEnvironment = null;
            }
        }

        private static void NormalizeCommandEditorSettings(AppConfiguration config)
        {
            config.CommandEditor ??= new CommandEditorSettings();
            config.CommandEditor.Normalize();
        }

        private static Dictionary<string, EnvironmentConfig> CloneEnvironmentMap(Dictionary<string, EnvironmentConfig> source)
        {
            var result = new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in source)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                var environment = kvp.Value?.Clone() ?? new EnvironmentConfig { Name = kvp.Key };
                environment.Normalize(kvp.Key);
                result[kvp.Key] = environment;
            }

            return result;
        }

        private static AppConfiguration CreateDefaultConfiguration()
        {
            return new AppConfiguration
            {
                Username = "",
                Timeout = 10,
                UseConnectionPooling = false,
                Presets = new Dictionary<string, PresetInfo>
                {
                    { "Custom", new PresetInfo { Commands = "get system status" } },
                    { "Get external-address-resource list", new PresetInfo { Commands = "dia sys external-address-resource list" } }
                }
            };
        }
    }
}
