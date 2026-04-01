using System.Text.Json.Serialization;
using SSH_Helper.Services.Scripting;

namespace SSH_Helper.Models
{
    /// <summary>
    /// The type of preset content.
    /// </summary>
    public enum PresetType
    {
        /// <summary>
        /// Plain text commands, one per line.
        /// </summary>
        Simple,

        /// <summary>
        /// YAML-based script with full scripting capabilities.
        /// </summary>
        YamlScript
    }

    /// <summary>
    /// Represents a saved command preset with optional per-preset timeout override.
    /// </summary>
    public class PresetInfo
    {
        private string _commands = string.Empty;

        public string Commands
        {
            get => _commands;
            set => _commands = NormalizeToWindowsLineEndings(value);
        }
        public int? Timeout { get; set; }
        public bool IsFavorite { get; set; }

        /// <summary>
        /// The folder path this preset belongs to (null or empty = root level).
        /// Supports nested folders using forward-slash separator (e.g., "Network/Cisco/Switches").
        /// Single-level folder names (no slash) represent root-level folders.
        /// </summary>
        public string? Folder { get; set; }

        /// <summary>
        /// Persisted Flow Canvas layout data (positions, comments, disabled blocks).
        /// Null when no layout has been saved for this preset.
        /// </summary>
        public CanvasLayoutData? CanvasLayout { get; set; }

        /// <summary>
        /// Gets the type of this preset (auto-detected from content).
        /// </summary>
        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public PresetType Type => ScriptParser.IsYamlScript(Commands) ? PresetType.YamlScript : PresetType.Simple;

        /// <summary>
        /// Gets whether this preset contains a YAML script.
        /// </summary>
        [JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsScript => Type == PresetType.YamlScript;

        public PresetInfo Clone()
        {
            return new PresetInfo
            {
                Commands = Commands,
                Timeout = Timeout,
                IsFavorite = IsFavorite,
                Folder = Folder,
                CanvasLayout = CanvasLayout
            };
        }

        private static string NormalizeToWindowsLineEndings(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            // Normalize any newline style to LF first, then convert to Windows CRLF.
            return value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace("\n", "\r\n", StringComparison.Ordinal);
        }
    }
}
