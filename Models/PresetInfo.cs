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
        public string Commands { get; set; } = string.Empty;
        public int? Timeout { get; set; }
        public bool IsFavorite { get; set; }

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
                IsFavorite = IsFavorite
            };
        }
    }
}
