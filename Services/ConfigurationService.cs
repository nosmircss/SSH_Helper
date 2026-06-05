using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Utilities;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Handles loading and saving application configuration.
    /// </summary>
    public class ConfigurationService
    {
        private const string SavedStateCompressionPrefix = "gz64:";
        private readonly string _configFilePath;
        private AppConfiguration? _cachedConfig;

        public ConfigurationService(string? configFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
            {
                _configFilePath = Path.Combine(AppDataPaths.GetAppFolder(), "config.json");
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
                var hadCompressedSavedState = !string.IsNullOrWhiteSpace(config.SavedStateCompressed);
                InflateSavedStateFromCompressedPayload(config);
                var shouldPersistCompressedState = !hadCompressedSavedState && config.SavedState != null;
                _cachedConfig = config;

                // Migrate the legacy global "Auto-layout on edits" toggle into the new default layout mode.
                // autoReflow ON  -> AutoFlow default; OFF -> Manual default. Runs once: after this the
                // new field is set and FlowCanvasAutoReflow is ignored.
                if (config.WindowState != null
                    && config.WindowState.FlowCanvasDefaultLayoutMode == null
                    && config.WindowState.FlowCanvasAutoReflow.HasValue)
                {
                    config.WindowState.FlowCanvasDefaultLayoutMode =
                        config.WindowState.FlowCanvasAutoReflow.Value ? LayoutMode.AutoFlow : LayoutMode.Manual;
                    config.WindowState.FlowCanvasAutoReflow = null;
                }

                if (shouldPersistCompressedState)
                {
                    try
                    {
                        Save(config);
                    }
                    catch
                    {
                        // Best effort: keep the in-memory state even if write-back fails.
                    }
                }

                return config;
            }
            catch (Exception ex)
            {
                // Preserve the corrupt file so the user can recover data manually
                JsonFileWriter.TryBackupCorrupt(_configFilePath);

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

                var savedState = config.SavedState;
                var compressedSavedState = CompressSavedState(savedState);

                config.SavedStateCompressed = compressedSavedState;
                if (!string.IsNullOrEmpty(compressedSavedState))
                {
                    // Persist compressed state only to keep config size down.
                    config.SavedState = null;
                }

                string json;
                try
                {
                    json = JsonConvert.SerializeObject(config, Formatting.Indented);
                }
                finally
                {
                    // Keep runtime config hydrated and avoid holding duplicate compressed payload in memory.
                    config.SavedState = savedState;
                    config.SavedStateCompressed = null;
                }

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
        /// Returns a deep-cloned snapshot of persisted environments plus the active/base environment names.
        /// </summary>
        public (Dictionary<string, EnvironmentConfig> Environments, string? ActiveEnvironment, string? BaseEnvironment) LoadEnvironmentState()
        {
            var config = GetCurrent();
            NormalizeEnvironmentData(config);
            return (CloneEnvironmentMap(config.Environments), config.ActiveEnvironment, config.BaseEnvironment);
        }

        /// <summary>
        /// Persists the complete environment state in one update operation.
        /// </summary>
        public void SaveEnvironmentState(
            Dictionary<string, EnvironmentConfig> environments,
            string? activeEnvironment,
            string? baseEnvironment)
        {
            environments ??= new Dictionary<string, EnvironmentConfig>(StringComparer.OrdinalIgnoreCase);

            Update(config =>
            {
                config.Environments = CloneEnvironmentMap(environments);
                config.ActiveEnvironment = string.IsNullOrWhiteSpace(activeEnvironment) ? null : activeEnvironment;
                config.BaseEnvironment = string.IsNullOrWhiteSpace(baseEnvironment) ? null : baseEnvironment;
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
            AppConfiguration config;

            // Legacy support: older config versions stored preset values as plain strings.
            // Detect this cheaply first so large modern configs do not pay for an extra full DOM parse.
            var hasLegacyPresetFormat = ContainsLegacyPresetFormat(json);
            if (hasLegacyPresetFormat)
            {
                config = ParseConfigurationWithLegacyPresets(json);
            }
            else
            {
                config = JsonConvert.DeserializeObject<AppConfiguration>(json) ?? new AppConfiguration();
            }

            NormalizeEnvironmentData(config);
            NormalizeCommandEditorSettings(config);
            return config;
        }

        private static bool ContainsLegacyPresetFormat(string json)
        {
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                using var sr = new StringReader(json);
                using var reader = new JsonTextReader(sr);

                while (reader.Read())
                {
                    if (reader.TokenType != JsonToken.PropertyName ||
                        !string.Equals(reader.Value?.ToString(), "Presets", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                        return false;

                    var presetsDepth = reader.Depth;
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndObject && reader.Depth == presetsDepth)
                            return false;

                        if (reader.TokenType != JsonToken.PropertyName)
                            continue;

                        if (!reader.Read())
                            return false;

                        if (reader.TokenType == JsonToken.String)
                            return true;

                        if (reader.TokenType == JsonToken.StartObject || reader.TokenType == JsonToken.StartArray)
                            reader.Skip();
                    }

                    return false;
                }
            }
            catch
            {
                // If detection fails, fall back to default parser behavior.
            }

            return false;
        }

        private static AppConfiguration ParseConfigurationWithLegacyPresets(string json)
        {
            var rootObj = JObject.Parse(json);
            var presetsToken = rootObj["Presets"] as JObject;

            // Avoid conversion failures for legacy string preset values.
            if (presetsToken != null)
            {
                rootObj["Presets"] = new JObject();
            }

            var config = rootObj.ToObject<AppConfiguration>() ?? new AppConfiguration();
            ApplyLegacyPresetFormat(presetsToken, config);
            return config;
        }

        private static void ApplyLegacyPresetFormat(JObject? presetsToken, AppConfiguration config)
        {
            if (presetsToken == null)
                return;

            config.Presets.Clear();
            foreach (var prop in presetsToken.Properties())
            {
                if (prop.Value.Type == JTokenType.String)
                {
                    config.Presets[prop.Name] = new PresetInfo { Commands = prop.Value.ToString() };
                    continue;
                }

                var info = prop.Value.ToObject<PresetInfo>() ?? new PresetInfo();
                info.Commands ??= "";
                config.Presets[prop.Name] = info;
            }
        }

        private static void InflateSavedStateFromCompressedPayload(AppConfiguration config)
        {
            if (config == null)
                return;

            var compressedPayload = config.SavedStateCompressed;
            if (string.IsNullOrWhiteSpace(compressedPayload))
                return;

            try
            {
                var savedStateJson = DecompressPayload(compressedPayload);
                var inflated = JsonConvert.DeserializeObject<ApplicationState>(savedStateJson);
                if (inflated != null)
                {
                    config.SavedState = inflated;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SavedState decompression failed: {ex.Message}");
            }
            finally
            {
                // Do not keep compressed + inflated copies in memory at the same time.
                config.SavedStateCompressed = null;
            }
        }

        private static string? CompressSavedState(ApplicationState? savedState)
        {
            if (savedState == null)
                return null;

            var json = JsonConvert.SerializeObject(savedState, Formatting.None);
            if (string.IsNullOrEmpty(json))
                return null;

            return SavedStateCompressionPrefix + GZipBase64Utility.CompressAndEncode(json);
        }

        private static string DecompressPayload(string compressedPayload)
            => GZipBase64Utility.Decompress(compressedPayload, prefixToStrip: SavedStateCompressionPrefix);

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

            if (!string.IsNullOrWhiteSpace(config.BaseEnvironment) &&
                !string.Equals(config.BaseEnvironment, EnvironmentConfig.DefaultName, StringComparison.OrdinalIgnoreCase) &&
                !config.Environments.ContainsKey(config.BaseEnvironment))
            {
                config.BaseEnvironment = null;
            }

            if (string.IsNullOrWhiteSpace(config.BaseEnvironment))
            {
                config.BaseEnvironment = !string.IsNullOrWhiteSpace(config.ActiveEnvironment)
                    ? config.ActiveEnvironment
                    : EnvironmentConfig.DefaultName;
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
