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
        private readonly Dictionary<string, FolderInfo> _folders = new();
        private readonly ConfigurationService _configService;

        public event EventHandler? PresetsChanged;
        public event EventHandler? FoldersChanged;

        public PresetManager(ConfigurationService configService)
        {
            _configService = configService;
        }

        public IReadOnlyDictionary<string, PresetInfo> Presets => _presets;
        public IReadOnlyDictionary<string, FolderInfo> Folders => _folders;

        /// <summary>
        /// Loads presets and folders from configuration.
        /// </summary>
        public void Load()
        {
            _presets.Clear();
            _folders.Clear();
            var config = _configService.Load();

            foreach (var kvp in config.Presets)
            {
                var preset = kvp.Value;
                // Normalize empty folder to null for consistent comparison
                if (string.IsNullOrEmpty(preset.Folder))
                {
                    preset.Folder = null;
                }
                _presets[kvp.Key] = preset;
            }

            foreach (var kvp in config.PresetFolders)
            {
                _folders[kvp.Key] = kvp.Value;
            }

            // Ensure all folders referenced by presets have entries in _folders
            // This handles legacy configs or manual edits where PresetFolders might be missing entries
            bool needsPersist = false;
            foreach (var preset in _presets.Values)
            {
                if (!string.IsNullOrEmpty(preset.Folder) && !_folders.ContainsKey(preset.Folder))
                {
                    _folders[preset.Folder] = new FolderInfo { IsExpanded = true };
                    needsPersist = true;
                }
            }

            if (needsPersist)
            {
                PersistToConfig();
            }

            OnPresetsChanged();
            OnFoldersChanged();
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
                v = 2,  // Version 2 includes folder
                commands = preset.Commands ?? "",
                timeout = preset.Timeout,
                folder = preset.Folder,
                isFavorite = preset.IsFavorite
            };

            string json = JsonConvert.SerializeObject(payload);
            string encoded = CompressAndEncode(json);
            return $"{name}_{encoded}";
        }

        /// <summary>
        /// Imports a preset from an encoded string.
        /// </summary>
        /// <param name="encodedString">The encoded preset string</param>
        /// <param name="defaultTimeout">Default timeout if not specified in preset</param>
        /// <returns>The name of the imported preset</returns>
        public string Import(string encodedString, int? defaultTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(encodedString))
                throw new ArgumentException("Import string cannot be empty", nameof(encodedString));

            int lastUnderscore = encodedString.LastIndexOf('_');
            if (lastUnderscore <= 0 || lastUnderscore >= encodedString.Length - 1)
                throw new FormatException("Invalid format. Expected <name>_<encoded>");

            string importedName = encodedString.Substring(0, lastUnderscore);
            string encoded = encodedString.Substring(lastUnderscore + 1);

            var preset = ParseImportedPayload(encoded, defaultTimeout);
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
            exportData["version"] = 2;  // Version 2 includes folders
            exportData["exportDate"] = DateTime.Now.ToString("O");
            exportData["presets"] = _presets;
            exportData["folders"] = _folders;

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

            // Import folders if present (version 2+)
            var foldersToken = importData["folders"];
            if (foldersToken != null)
            {
                var importedFolders = foldersToken.ToObject<Dictionary<string, FolderInfo>>();
                if (importedFolders != null)
                {
                    foreach (var kvp in importedFolders)
                    {
                        if (!_folders.ContainsKey(kvp.Key))
                        {
                            _folders[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            int count = 0;
            foreach (var kvp in importedPresets)
            {
                string name = kvp.Key;

                // If preset exists, append "_imported" and make unique
                if (_presets.ContainsKey(name))
                {
                    name = GetUniqueName(name + "_imported");
                }

                // Ensure folder exists if preset has one
                if (!string.IsNullOrEmpty(kvp.Value.Folder) && !_folders.ContainsKey(kvp.Value.Folder))
                {
                    _folders[kvp.Value.Folder] = new FolderInfo { IsExpanded = true };
                }

                _presets[name] = kvp.Value;
                count++;
            }

            if (count > 0)
            {
                PersistToConfig();
                OnPresetsChanged();
                OnFoldersChanged();
            }

            return count;
        }

        /// <summary>
        /// Applies default timeout to presets that don't have it set.
        /// </summary>
        public void ApplyDefaults(int defaultTimeout)
        {
            bool changed = false;

            foreach (var preset in _presets.Values)
            {
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

        #region Folder Operations

        /// <summary>
        /// Creates a new folder.
        /// </summary>
        /// <param name="name">The folder name</param>
        /// <returns>True if created, false if folder already exists</returns>
        public bool CreateFolder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Folder name cannot be empty", nameof(name));

            if (_folders.ContainsKey(name))
                return false;

            _folders[name] = new FolderInfo { IsExpanded = true };
            PersistToConfig();
            OnFoldersChanged();
            return true;
        }

        /// <summary>
        /// Renames a folder and updates all presets in that folder.
        /// </summary>
        public bool RenameFolder(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return false;

            if (!_folders.TryGetValue(oldName, out var folderInfo))
                return false;

            if (_folders.ContainsKey(newName))
                return false;

            // Update all presets in this folder
            foreach (var preset in _presets.Values)
            {
                if (string.Equals(preset.Folder, oldName, StringComparison.Ordinal))
                {
                    preset.Folder = newName;
                }
            }

            // Move folder metadata
            _folders.Remove(oldName);
            _folders[newName] = folderInfo;

            PersistToConfig();
            OnFoldersChanged();
            OnPresetsChanged();
            return true;
        }

        /// <summary>
        /// Deletes a folder.
        /// </summary>
        /// <param name="name">Folder name</param>
        /// <param name="deletePresets">If true, deletes presets in folder. If false, moves them to root.</param>
        /// <returns>True if folder was deleted</returns>
        public bool DeleteFolder(string name, bool deletePresets = false)
        {
            if (!_folders.Remove(name))
                return false;

            // Handle presets in the deleted folder
            var presetsInFolder = _presets.Where(p => string.Equals(p.Value.Folder, name, StringComparison.Ordinal)).ToList();

            if (deletePresets)
            {
                foreach (var kvp in presetsInFolder)
                {
                    _presets.Remove(kvp.Key);
                }
            }
            else
            {
                // Move presets to root
                foreach (var kvp in presetsInFolder)
                {
                    kvp.Value.Folder = null;
                }
            }

            PersistToConfig();
            OnFoldersChanged();
            OnPresetsChanged();
            return true;
        }

        /// <summary>
        /// Moves a preset to a folder (or root if folder is null).
        /// </summary>
        public bool MovePresetToFolder(string presetName, string? folder)
        {
            if (!_presets.TryGetValue(presetName, out var preset))
                return false;

            // Validate folder exists if specified
            if (!string.IsNullOrEmpty(folder) && !_folders.ContainsKey(folder))
                return false;

            preset.Folder = string.IsNullOrEmpty(folder) ? null : folder;
            PersistToConfig();
            OnPresetsChanged();
            return true;
        }

        /// <summary>
        /// Gets all presets in a specific folder.
        /// </summary>
        /// <param name="folder">Folder name, or null/empty for root level presets</param>
        public IEnumerable<string> GetPresetsInFolder(string? folder)
        {
            return _presets
                .Where(p => string.IsNullOrEmpty(folder)
                    ? string.IsNullOrEmpty(p.Value.Folder)
                    : string.Equals(p.Value.Folder, folder, StringComparison.Ordinal))
                .Select(p => p.Key);
        }

        /// <summary>
        /// Gets all folder names.
        /// </summary>
        public IEnumerable<string> GetFolders()
        {
            return _folders.Keys;
        }

        /// <summary>
        /// Sets the expanded state of a folder.
        /// </summary>
        public void SetFolderExpanded(string name, bool expanded)
        {
            if (_folders.TryGetValue(name, out var folderInfo))
            {
                folderInfo.IsExpanded = expanded;
                System.Diagnostics.Debug.WriteLine($"SetFolderExpanded: {name} = {expanded}, object hash = {folderInfo.GetHashCode()}");
                PersistToConfig();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"SetFolderExpanded: Folder '{name}' NOT FOUND in _folders. Available: {string.Join(", ", _folders.Keys)}");
            }
        }

        /// <summary>
        /// Sets the favorite state of a folder.
        /// </summary>
        public void SetFolderFavorite(string name, bool isFavorite)
        {
            if (_folders.TryGetValue(name, out var folderInfo))
            {
                folderInfo.IsFavorite = isFavorite;
                PersistToConfig();
            }
        }

        /// <summary>
        /// Gets a unique folder name by appending _1, _2, etc. if needed.
        /// </summary>
        public string GetUniqueFolderName(string baseName)
        {
            string candidate = baseName;
            int i = 1;
            while (_folders.ContainsKey(candidate))
            {
                candidate = $"{baseName}_{i++}";
            }
            return candidate;
        }

        #endregion

        private PresetInfo ParseImportedPayload(string encoded, int? defaultTimeout)
        {
            string decompressed = DecompressEncoded(encoded);

            if (decompressed.TrimStart().StartsWith("{"))
            {
                try
                {
                    var obj = JObject.Parse(decompressed);
                    string commands = obj["commands"]?.ToString() ?? obj["Commands"]?.ToString() ?? "";
                    int? timeout = obj["timeout"]?.Type == JTokenType.Null ? null : obj["timeout"]?.Value<int?>();
                    string? folder = obj["folder"]?.ToString();
                    bool isFavorite = obj["isFavorite"]?.Value<bool>() ?? false;

                    // Ensure folder exists if specified
                    if (!string.IsNullOrEmpty(folder) && !_folders.ContainsKey(folder))
                    {
                        _folders[folder] = new FolderInfo { IsExpanded = true };
                    }

                    return new PresetInfo
                    {
                        Commands = commands,
                        Timeout = timeout,
                        Folder = string.IsNullOrEmpty(folder) ? null : folder,
                        IsFavorite = isFavorite
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
                Timeout = defaultTimeout
            };
        }

        private void PersistToConfig()
        {
            var config = _configService.Load();

            // Normalize presets - ensure empty folder strings are null
            var normalizedPresets = new Dictionary<string, PresetInfo>();
            foreach (var kvp in _presets)
            {
                var preset = kvp.Value;
                // Normalize empty folder to null for consistent comparison
                if (string.IsNullOrEmpty(preset.Folder))
                {
                    preset.Folder = null;
                }
                normalizedPresets[kvp.Key] = preset;
            }

            config.Presets = normalizedPresets;
            config.PresetFolders = new Dictionary<string, FolderInfo>(_folders);
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

        protected virtual void OnFoldersChanged()
        {
            FoldersChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
