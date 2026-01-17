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

        public ConfigurationService()
        {
            string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appFolder = Path.Combine(folder, "SSH_Helper");

            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }

            _configFilePath = Path.Combine(appFolder, "config.json");
        }

        public string ConfigFilePath => _configFilePath;

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
            catch (Exception)
            {
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

        private AppConfiguration ParseConfiguration(string json)
        {
            var config = new AppConfiguration();
            var rootObj = JObject.Parse(json);

            // Parse presets (handle both legacy string format and new object format)
            var presetsToken = rootObj["Presets"] as JObject;
            if (presetsToken != null)
            {
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

            // Parse other fields
            if (rootObj["Username"]?.Type == JTokenType.String)
            {
                config.Username = rootObj["Username"]!.ToString();
            }

            if (rootObj["Delay"]?.Type == JTokenType.Integer)
            {
                config.Delay = rootObj["Delay"]!.ToObject<int>();
            }

            if (rootObj["Timeout"]?.Type == JTokenType.Integer)
            {
                config.Timeout = rootObj["Timeout"]!.ToObject<int>();
            }

            return config;
        }

        private static AppConfiguration CreateDefaultConfiguration()
        {
            return new AppConfiguration
            {
                Username = "",
                Delay = 500,
                Timeout = 10,
                Presets = new Dictionary<string, PresetInfo>
                {
                    { "Custom", new PresetInfo { Commands = "get system status" } },
                    { "Get external-address-resource list", new PresetInfo { Commands = "dia sys external-address-resource list" } }
                }
            };
        }
    }
}
