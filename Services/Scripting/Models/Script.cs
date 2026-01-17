using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// Represents a parsed YAML script document.
    /// </summary>
    public class Script
    {
        /// <summary>
        /// Optional name of the script.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Optional description of what the script does.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Script version for compatibility tracking.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Enable debug output for this script (shows Extract results, Set values, etc.).
        /// Useful for troubleshooting and building scripts.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Variables declared in the script with their default values.
        /// </summary>
        public Dictionary<string, object?> Vars { get; set; } = new();

        /// <summary>
        /// The execution steps of the script.
        /// </summary>
        public List<ScriptStep> Steps { get; set; } = new();
    }
}
