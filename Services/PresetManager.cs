using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Manages command presets including CRUD operations and import/export.
    /// </summary>
    public class PresetManager
    {
        private readonly Dictionary<string, PresetInfo> _presets = new();
        private readonly ConfigurationService _configService;

        public event EventHandler? PresetsChanged;

        public PresetManager(ConfigurationService configService)
        {
            _configService = configService;
        }

        public IReadOnlyDictionary<string, PresetInfo> Presets => _presets;

        /// <summary>
        /// Loads presets from configuration.
        /// </summary>
        public void Load()
        {
            _presets.Clear();
            var config = _configService.Load();

            foreach (var kvp in config.Presets)
            {
                _presets[kvp.Key] = kvp.Value;
            }

            OnPresetsChanged();
        }

        /// <summary>
        /// Gets a preset by name.
        /// </summary>
        public PresetInfo? Get(string name)
        {
            return _presets.TryGetValue(name, out var preset) ? preset : null;
        }

        /// <summary>
        /// Saves or updates a preset.
        /// </summary>
        public void Save(string name, PresetInfo preset)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Preset name cannot be empty", nameof(name));

            _presets[name] = preset;
            PersistToConfig();
            OnPresetsChanged();
        }

        /// <summary>
        /// Renames a preset.
        /// </summary>
        public bool Rename(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return false;

            if (!_presets.TryGetValue(oldName, out var preset))
                return false;

            if (_presets.ContainsKey(newName))
                return false;

            _presets.Remove(oldName);
            _presets[newName] = preset;
            PersistToConfig();
            OnPresetsChanged();
            return true;
        }

        /// <summary>
        /// Deletes a preset.
        /// </summary>
        public bool Delete(string name)
        {
            if (!_presets.Remove(name))
                return false;

            PersistToConfig();
            OnPresetsChanged();
            return true;
        }

        /// <summary>
        /// Duplicates a preset with a new name.
        /// </summary>
        public string Duplicate(string sourceName, string? suggestedName = null)
        {
            if (!_presets.TryGetValue(sourceName, out var source))
                throw new ArgumentException($"Preset '{sourceName}' not found", nameof(sourceName));

            string newName = suggestedName ?? $"{sourceName}_Copy";
            newName = GetUniqueName(newName);

            _presets[newName] = source.Clone();
            PersistToConfig();
            OnPresetsChanged();
            return newName;
        }

        /// <summary>
        /// Gets a unique preset name by appending _1, _2, etc. if needed.
        /// </summary>
        public string GetUniqueName(string baseName)
        {
            string candidate = baseName;
            int i = 1;
            while (_presets.ContainsKey(candidate))
            {
                candidate = $"{baseName}_{i++}";
            }
            return candidate;
        }

        /// <summary>
        /// Exports a preset to a compressed, base64-encoded string.
        /// Format: <name>_<gzip+base64(JSON)>
        /// </summary>
        public string Export(string name)
        {
            if (!_presets.TryGetValue(name, out var preset))
                throw new ArgumentException($"Preset '{name}' not found", nameof(name));

            var payload = new
            {
                v = 1,
                commands = preset.Commands ?? "",
                delay = preset.Delay,
                timeout = preset.Timeout
            };

            string json = JsonConvert.SerializeObject(payload);
            string encoded = CompressAndEncode(json);
            return $"{name}_{encoded}";
        }

        /// <summary>
        /// Imports a preset from an encoded string.
        /// </summary>
        /// <param name="encodedString">The encoded preset string</param>
        /// <param name="defaultDelay">Default delay if not specified in preset</param>
        /// <param name="defaultTimeout">Default timeout if not specified in preset</param>
        /// <returns>The name of the imported preset</returns>
        public string Import(string encodedString, int? defaultDelay = null, int? defaultTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(encodedString))
                throw new ArgumentException("Import string cannot be empty", nameof(encodedString));

            int lastUnderscore = encodedString.LastIndexOf('_');
            if (lastUnderscore <= 0 || lastUnderscore >= encodedString.Length - 1)
                throw new FormatException("Invalid format. Expected <name>_<encoded>");

            string importedName = encodedString.Substring(0, lastUnderscore);
            string encoded = encodedString.Substring(lastUnderscore + 1);

            var preset = ParseImportedPayload(encoded, defaultDelay, defaultTimeout);
            string finalName = GetUniqueName(importedName);

            _presets[finalName] = preset;
            PersistToConfig();
            OnPresetsChanged();

            return finalName;
        }

        /// <summary>
        /// Exports all presets to a JSON file.
        /// </summary>
        public void ExportAllToFile(string filePath)
        {
            var exportData = new Dictionary<string, object>();
            exportData["version"] = 1;
            exportData["exportDate"] = DateTime.Now.ToString("O");
            exportData["presets"] = _presets;

            string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Imports all presets from a JSON file.
        /// If a preset exists, appends "_imported" to the name.
        /// </summary>
        /// <returns>The number of presets imported</returns>
        public int ImportAllFromFile(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var importData = JObject.Parse(json);

            var presetsToken = importData["presets"];
            if (presetsToken == null)
                throw new FormatException("Invalid preset file format: missing 'presets' key");

            var importedPresets = presetsToken.ToObject<Dictionary<string, PresetInfo>>();
            if (importedPresets == null)
                throw new FormatException("Invalid preset file format: could not parse presets");

            int count = 0;
            foreach (var kvp in importedPresets)
            {
                string name = kvp.Key;

                // If preset exists, append "_imported" and make unique
                if (_presets.ContainsKey(name))
                {
                    name = GetUniqueName(name + "_imported");
                }

                _presets[name] = kvp.Value;
                count++;
            }

            if (count > 0)
            {
                PersistToConfig();
                OnPresetsChanged();
            }

            return count;
        }

        /// <summary>
        /// Applies default delay/timeout to presets that don't have them set.
        /// </summary>
        public void ApplyDefaults(int defaultDelay, int defaultTimeout)
        {
            bool changed = false;

            foreach (var preset in _presets.Values)
            {
                if (!preset.Delay.HasValue)
                {
                    preset.Delay = defaultDelay;
                    changed = true;
                }
                if (!preset.Timeout.HasValue)
                {
                    preset.Timeout = defaultTimeout;
                    changed = true;
                }
            }

            if (changed)
            {
                PersistToConfig();
            }
        }

        private PresetInfo ParseImportedPayload(string encoded, int? defaultDelay, int? defaultTimeout)
        {
            string decompressed = DecompressEncoded(encoded);

            if (decompressed.TrimStart().StartsWith("{"))
            {
                try
                {
                    var obj = JObject.Parse(decompressed);
                    string commands = obj["commands"]?.ToString() ?? obj["Commands"]?.ToString() ?? "";
                    int? delay = obj["delay"]?.Type == JTokenType.Null ? null : obj["delay"]?.Value<int?>();
                    int? timeout = obj["timeout"]?.Type == JTokenType.Null ? null : obj["timeout"]?.Value<int?>();

                    return new PresetInfo
                    {
                        Commands = commands,
                        Delay = delay,
                        Timeout = timeout
                    };
                }
                catch
                {
                    // Fall back to treating decompressed text as raw commands
                }
            }

            return new PresetInfo
            {
                Commands = decompressed,
                Delay = defaultDelay,
                Timeout = defaultTimeout
            };
        }

        private void PersistToConfig()
        {
            var config = _configService.Load();
            config.Presets = new Dictionary<string, PresetInfo>(_presets);
            _configService.Save(config);
        }

        private static string CompressAndEncode(string text)
        {
            byte[] raw = Encoding.UTF8.GetBytes(text);
            using var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(raw, 0, raw.Length);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        private static string DecompressEncoded(string encoded)
        {
            byte[] compressed = Convert.FromBase64String(encoded);
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }

        protected virtual void OnPresetsChanged()
        {
            PresetsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
