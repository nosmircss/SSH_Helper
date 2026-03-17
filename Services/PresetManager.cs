using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Utilities;

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
        private JobStorageService? _jobStorageService;

        public event EventHandler? PresetsChanged;
        public event EventHandler? FoldersChanged;

        public PresetManager(ConfigurationService configService)
        {
            _configService = configService;
        }

        public IReadOnlyDictionary<string, PresetInfo> Presets => _presets;
        public IReadOnlyDictionary<string, FolderInfo> Folders => _folders;

        /// <summary>
        /// Returns the current application configuration snapshot backing preset operations.
        /// </summary>
        internal AppConfiguration GetCurrentConfiguration()
        {
            return _configService.GetCurrent();
        }

        /// <summary>
        /// Sets or clears the optional JobStorageService used for job reference integrity.
        /// Called after both services are constructed (not via constructor to avoid circular dependencies).
        /// </summary>
        public void SetJobStorageService(JobStorageService? service)
        {
            _jobStorageService = service;
        }

        /// <summary>
        /// Returns all jobs that reference the specified preset name.
        /// Returns empty list if JobStorageService is not set.
        /// </summary>
        public IReadOnlyList<Models.JobDefinition> GetJobsReferencingPreset(string presetName)
        {
            return _jobStorageService?.GetJobsReferencingPreset(presetName)
                ?? Array.Empty<Models.JobDefinition>();
        }

        /// <summary>
        /// Returns all jobs that reference the specified folder path.
        /// Returns empty list if JobStorageService is not set.
        /// </summary>
        public IReadOnlyList<Models.JobDefinition> GetJobsReferencingFolder(string folderPath)
        {
            return _jobStorageService?.GetJobsReferencingFolder(folderPath)
                ?? Array.Empty<Models.JobDefinition>();
        }

        /// <summary>
        /// Loads presets and folders from configuration.
        /// </summary>
        public void Load()
        {
            Load(_configService.Load());
        }

        /// <summary>
        /// Loads presets and folders from the supplied configuration snapshot.
        /// </summary>
        public void Load(AppConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _presets.Clear();
            _folders.Clear();
            var validEnvironmentNames = new HashSet<string>(config.Environments.Keys, StringComparer.OrdinalIgnoreCase)
            {
                EnvironmentConfig.DefaultName
            };
            bool needsPersist = false;

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
                var folderInfo = kvp.Value ?? new FolderInfo();
                var normalizedBaseEnvironment = NormalizeFolderBaseEnvironment(folderInfo.BaseEnvironment);
                if (!string.Equals(folderInfo.BaseEnvironment, normalizedBaseEnvironment, StringComparison.Ordinal))
                {
                    folderInfo.BaseEnvironment = normalizedBaseEnvironment;
                    needsPersist = true;
                }

                if (!string.IsNullOrWhiteSpace(folderInfo.BaseEnvironment) &&
                    !validEnvironmentNames.Contains(folderInfo.BaseEnvironment))
                {
                    folderInfo.BaseEnvironment = null;
                    needsPersist = true;
                }

                _folders[kvp.Key] = folderInfo;
            }

            // Ensure all folders referenced by presets have entries in _folders
            // This handles legacy configs or manual edits where PresetFolders might be missing entries
            // Also ensures parent folders exist for nested paths
            foreach (var preset in _presets.Values)
            {
                if (!string.IsNullOrEmpty(preset.Folder))
                {
                    // Ensure the folder and all its ancestors exist
                    foreach (var path in FolderPathUtility.GetAllPathsInHierarchy(preset.Folder))
                    {
                        if (!_folders.ContainsKey(path))
                        {
                            _folders[path] = new FolderInfo { IsExpanded = true };
                            needsPersist = true;
                        }
                    }
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

            // Update job references to the renamed preset
            if (_jobStorageService != null)
            {
                var referencingJobs = _jobStorageService.GetJobsReferencingPreset(oldName);
                foreach (var job in referencingJobs)
                {
                    job.TargetName = newName;
                    _jobStorageService.Save(job);
                }
            }

            OnPresetsChanged();
            return true;
        }

        /// <summary>
        /// Deletes a preset.
        /// </summary>
        public bool Delete(string name)
        {
            if (!_presets.TryGetValue(name, out var preset))
                return false;

            _presets.Remove(name);
            PersistToConfig();

            // Auto-disable jobs that reference the deleted preset
            if (_jobStorageService != null)
            {
                var referencingJobs = _jobStorageService.GetJobsReferencingPreset(name);
                foreach (var job in referencingJobs)
                {
                    job.IsEnabled = false;
                    job.DisabledReason = $"Preset '{name}' was deleted";
                    _jobStorageService.Save(job);
                }
            }

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
        /// <param name="filePath">Path to the JSON file containing presets to import</param>
        /// <param name="targetFolder">Optional folder to place all imported presets into.
        /// If null, presets keep their original folder structure.
        /// If empty string, all presets go to root level.
        /// If a folder name, all presets go into that folder.</param>
        /// <returns>The number of presets imported</returns>
        public int ImportAllFromFile(string filePath, string? targetFolder = null)
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
                        // When targetFolder is specified, prepend it to the folder path
                        string folderPath = !string.IsNullOrEmpty(targetFolder)
                            ? FolderPathUtility.CombinePath(targetFolder, kvp.Key)
                            : kvp.Key;

                        // Ensure parent folders exist for the (potentially combined) path
                        foreach (var path in FolderPathUtility.GetAllPathsInHierarchy(folderPath))
                        {
                            if (!_folders.ContainsKey(path))
                            {
                                // Use imported folder info for the actual folder, default for parents
                                if (path == folderPath)
                                    _folders[path] = kvp.Value;
                                else
                                    _folders[path] = new FolderInfo { IsExpanded = true };
                            }
                        }
                    }
                }
            }

            // Ensure target folder and its parents exist if specified
            if (!string.IsNullOrEmpty(targetFolder))
            {
                foreach (var path in FolderPathUtility.GetAllPathsInHierarchy(targetFolder))
                {
                    if (!_folders.ContainsKey(path))
                    {
                        _folders[path] = new FolderInfo { IsExpanded = true };
                    }
                }
            }

            int count = 0;
            var affectedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in importedPresets)
            {
                string name = kvp.Key;

                // If preset exists, append "_imported" and make unique
                if (_presets.ContainsKey(name))
                {
                    name = GetUniqueName(name + "_imported");
                }

                // Override folder if targetFolder is specified (including empty string for root)
                if (targetFolder != null)
                {
                    if (string.IsNullOrEmpty(targetFolder))
                    {
                        // Empty string means import to root - clear the folder
                        kvp.Value.Folder = null;
                    }
                    else if (string.IsNullOrEmpty(kvp.Value.Folder))
                    {
                        // Preset had no folder, put it directly in target folder
                        kvp.Value.Folder = targetFolder;
                    }
                    else
                    {
                        // Preset had a folder, combine target with original to preserve structure
                        kvp.Value.Folder = FolderPathUtility.CombinePath(targetFolder, kvp.Value.Folder);
                    }
                }

                // Ensure folder and its parents exist if preset has one
                if (!string.IsNullOrEmpty(kvp.Value.Folder))
                {
                    affectedFolders.Add(kvp.Value.Folder);
                    foreach (var path in FolderPathUtility.GetAllPathsInHierarchy(kvp.Value.Folder))
                    {
                        if (!_folders.ContainsKey(path))
                        {
                            _folders[path] = new FolderInfo { IsExpanded = true };
                        }
                    }
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
        /// Clears timeout overrides from all presets, causing them to inherit the global default.
        /// </summary>
        /// <returns>The number of presets that were modified.</returns>
        public int ClearAllTimeouts()
        {
            int count = 0;
            foreach (var preset in _presets.Values)
            {
                if (preset.Timeout.HasValue)
                {
                    preset.Timeout = null;
                    count++;
                }
            }

            if (count > 0)
                PersistToConfig();

            return count;
        }

        #region Folder Operations

        /// <summary>
        /// Creates a new folder. For nested paths (e.g., "A/B/C"), automatically creates
        /// all parent folders that don't exist.
        /// </summary>
        /// <param name="path">The folder path (can be nested with "/" separator)</param>
        /// <returns>True if created (or any parents created), false if folder already exists</returns>
        public bool CreateFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Folder name cannot be empty", nameof(path));

            bool anyCreated = false;

            // Create all folders in the path hierarchy
            foreach (var folderPath in FolderPathUtility.GetAllPathsInHierarchy(path))
            {
                if (!_folders.ContainsKey(folderPath))
                {
                    _folders[folderPath] = new FolderInfo { IsExpanded = true };
                    anyCreated = true;
                }
            }

            if (anyCreated)
            {
                PersistToConfig();
                OnFoldersChanged();
            }

            return anyCreated;
        }

        /// <summary>
        /// Renames a folder and updates all presets in that folder and descendant folders.
        /// Also renames all descendant folders.
        /// </summary>
        public bool RenameFolder(string oldPath, string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath))
                return false;

            if (!_folders.TryGetValue(oldPath, out var folderInfo))
                return false;

            if (_folders.ContainsKey(newPath))
                return false;

            // Ensure parent folders exist for the new path
            var parentPath = FolderPathUtility.GetParentPath(newPath);
            if (parentPath != null && !_folders.ContainsKey(parentPath))
            {
                // Create parent folders
                foreach (var path in FolderPathUtility.GetAllPathsInHierarchy(parentPath))
                {
                    if (!_folders.ContainsKey(path))
                    {
                        _folders[path] = new FolderInfo { IsExpanded = true };
                    }
                }
            }

            // Find all descendant folders
            var descendantFolders = _folders.Keys
                .Where(k => FolderPathUtility.IsDescendantOf(k, oldPath))
                .ToList();

            // Update all presets in this folder or descendants
            foreach (var preset in _presets.Values)
            {
                if (string.Equals(preset.Folder, oldPath, StringComparison.Ordinal))
                {
                    preset.Folder = newPath;
                }
                else if (!string.IsNullOrEmpty(preset.Folder) && FolderPathUtility.IsDescendantOf(preset.Folder, oldPath))
                {
                    preset.Folder = FolderPathUtility.RenamePath(preset.Folder, oldPath, newPath);
                }
            }

            // Move folder metadata for this folder
            _folders.Remove(oldPath);
            _folders[newPath] = folderInfo;

            // Update all descendant folder paths
            foreach (var descendant in descendantFolders)
            {
                var descendantInfo = _folders[descendant];
                _folders.Remove(descendant);
                var newDescendantPath = FolderPathUtility.RenamePath(descendant, oldPath, newPath);
                _folders[newDescendantPath] = descendantInfo;
            }

            PersistToConfig();
            OnFoldersChanged();
            OnPresetsChanged();
            return true;
        }

        /// <summary>
        /// Deletes a folder and optionally all descendant folders.
        /// </summary>
        /// <param name="path">Folder path</param>
        /// <param name="deletePresets">If true, deletes presets. If false, moves them to parent folder (or root).</param>
        /// <returns>True if folder was deleted</returns>
        public bool DeleteFolder(string path, bool deletePresets = false)
        {
            if (!_folders.Remove(path))
                return false;

            // Find and remove all descendant folders
            var descendantFolders = _folders.Keys
                .Where(k => FolderPathUtility.IsDescendantOf(k, path))
                .ToList();

            foreach (var descendant in descendantFolders)
            {
                _folders.Remove(descendant);
            }

            // Determine parent folder for moving presets (null = root)
            var parentPath = FolderPathUtility.GetParentPath(path);

            // Handle presets in the deleted folder and all descendants
            var affectedPresets = _presets.Where(p =>
                string.Equals(p.Value.Folder, path, StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(p.Value.Folder) && FolderPathUtility.IsDescendantOf(p.Value.Folder, path))
            ).ToList();

            if (deletePresets)
            {
                foreach (var kvp in affectedPresets)
                {
                    _presets.Remove(kvp.Key);
                }
            }
            else
            {
                // Move presets to parent folder (or root if no parent)
                foreach (var kvp in affectedPresets)
                {
                    kvp.Value.Folder = parentPath;
                }
            }

            PersistToConfig();

            // Auto-disable jobs that reference the deleted folder
            if (_jobStorageService != null)
            {
                var referencingJobs = _jobStorageService.GetJobsReferencingFolder(path);
                foreach (var job in referencingJobs)
                {
                    job.IsEnabled = false;
                    job.DisabledReason = $"Folder '{path}' was deleted";
                    _jobStorageService.Save(job);
                }
            }

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
        /// Gets all folder paths.
        /// </summary>
        public IEnumerable<string> GetFolders()
        {
            return _folders.Keys;
        }

        /// <summary>
        /// Gets all root-level folders (folders with no parent).
        /// </summary>
        public IEnumerable<string> GetRootFolders()
        {
            return _folders.Keys.Where(k => !k.Contains(FolderPathUtility.Separator));
        }

        /// <summary>
        /// Gets immediate child folders of a parent path.
        /// </summary>
        /// <param name="parentPath">Parent folder path, or null for root-level folders.</param>
        public IEnumerable<string> GetSubfolders(string? parentPath)
        {
            if (string.IsNullOrEmpty(parentPath))
            {
                return GetRootFolders();
            }

            return _folders.Keys.Where(k => FolderPathUtility.IsImmediateChildOf(k, parentPath));
        }

        /// <summary>
        /// Gets all descendant folders of a parent path (recursive).
        /// </summary>
        public IEnumerable<string> GetAllDescendantFolders(string parentPath)
        {
            if (string.IsNullOrEmpty(parentPath))
                return Enumerable.Empty<string>();

            return _folders.Keys.Where(k => FolderPathUtility.IsDescendantOf(k, parentPath));
        }

        /// <summary>
        /// Counts all presets in a folder and its descendants.
        /// </summary>
        public int CountPresetsInFolderAndDescendants(string path)
        {
            return _presets.Count(p =>
                string.Equals(p.Value.Folder, path, StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(p.Value.Folder) && FolderPathUtility.IsDescendantOf(p.Value.Folder, path)));
        }

        /// <summary>
        /// Counts all descendant folders of a path.
        /// </summary>
        public int CountDescendantFolders(string path)
        {
            return GetAllDescendantFolders(path).Count();
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
        /// Sets or clears a folder-specific base environment override.
        /// </summary>
        public bool SetFolderBaseEnvironment(string name, string? environmentName)
        {
            if (!_folders.TryGetValue(name, out var folderInfo))
                return false;

            var normalizedEnvironmentName = NormalizeFolderBaseEnvironment(environmentName);
            if (string.Equals(folderInfo.BaseEnvironment, normalizedEnvironmentName, StringComparison.OrdinalIgnoreCase))
                return true;

            folderInfo.BaseEnvironment = normalizedEnvironmentName;
            PersistToConfig();
            OnFoldersChanged();
            return true;
        }

        /// <summary>
        /// Updates folder base-environment references after an environment rename.
        /// </summary>
        public int RenameFolderBaseEnvironment(string oldName, string newName)
        {
            var sourceName = NormalizeFolderBaseEnvironment(oldName);
            var targetName = NormalizeFolderBaseEnvironment(newName);
            if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(targetName))
                return 0;

            int updatedCount = 0;
            foreach (var folderInfo in _folders.Values)
            {
                if (!string.Equals(folderInfo.BaseEnvironment, sourceName, StringComparison.OrdinalIgnoreCase))
                    continue;

                folderInfo.BaseEnvironment = targetName;
                updatedCount++;
            }

            if (updatedCount > 0)
            {
                PersistToConfig();
                OnFoldersChanged();
            }

            return updatedCount;
        }

        /// <summary>
        /// Clears folder base-environment references that point to a deleted environment.
        /// </summary>
        public int ClearFolderBaseEnvironment(string environmentName)
        {
            var normalizedEnvironmentName = NormalizeFolderBaseEnvironment(environmentName);
            if (string.IsNullOrWhiteSpace(normalizedEnvironmentName))
                return 0;

            int clearedCount = 0;
            foreach (var folderInfo in _folders.Values)
            {
                if (!string.Equals(folderInfo.BaseEnvironment, normalizedEnvironmentName, StringComparison.OrdinalIgnoreCase))
                    continue;

                folderInfo.BaseEnvironment = null;
                clearedCount++;
            }

            if (clearedCount > 0)
            {
                PersistToConfig();
                OnFoldersChanged();
            }

            return clearedCount;
        }

        /// <summary>
        /// Gets a unique folder path by appending _1, _2, etc. to the last segment if needed.
        /// </summary>
        public string GetUniqueFolderName(string basePath)
        {
            if (!_folders.ContainsKey(basePath))
                return basePath;

            var parentPath = FolderPathUtility.GetParentPath(basePath);
            var folderName = FolderPathUtility.GetFolderName(basePath);

            int i = 1;
            string candidate;
            do
            {
                var newName = $"{folderName}_{i++}";
                candidate = FolderPathUtility.CombinePath(parentPath, newName);
            } while (_folders.ContainsKey(candidate));

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

                    // Ensure folder and its parents exist if specified
                    if (!string.IsNullOrEmpty(folder))
                    {
                        foreach (var path in FolderPathUtility.GetAllPathsInHierarchy(folder))
                        {
                            if (!_folders.ContainsKey(path))
                            {
                                _folders[path] = new FolderInfo { IsExpanded = true };
                            }
                        }
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
            var config = _configService.GetCurrent();

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

        private static string? NormalizeFolderBaseEnvironment(string? environmentName)
        {
            return string.IsNullOrWhiteSpace(environmentName)
                ? null
                : environmentName.Trim();
        }

        private static string CompressAndEncode(string text)
            => GZipBase64Utility.CompressAndEncode(text);

        private static string DecompressEncoded(string encoded)
            => GZipBase64Utility.Decompress(encoded);

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
