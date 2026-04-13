using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// Represents a parsed YAML script document.
    /// </summary>
    public class Script
    {
        /// <summary>
        /// Top-level keys explicitly declared in the YAML document.
        /// Used for validation that depends on presence, not only effective value.
        /// </summary>
        public HashSet<string> DeclaredTopLevelKeys { get; } = new(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Parser-originated validation issues for top-level script structure.
        /// </summary>
        public List<string> ParseErrors { get; } = new();

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
        /// Optional environment name to activate when the preset is loaded.
        /// </summary>
        public string? Environment { get; set; }

        /// <summary>
        /// Enable debug output for this script (shows Extract results, Set values, etc.).
        /// Useful for troubleshooting and building scripts.
        /// </summary>
        public bool Debug { get; set; }

        /// <summary>
        /// Suppress the script execution banner/header in output.
        /// </summary>
        public bool NoBanner { get; set; }

        /// <summary>
        /// Emit compact single-line errors instead of banner-style blocks.
        /// </summary>
        public bool CompactErrors { get; set; }

        /// <summary>
        /// Suppress the pre-execution warning when referenced grid columns are missing.
        /// Missing references still resolve to empty values at runtime.
        /// </summary>
        public bool SuppressMissingColumnWarning { get; set; }

        /// <summary>
        /// Marks this file as a definition-only library rather than an executable script.
        /// </summary>
        public bool Library { get; set; }

        /// <summary>
        /// Variables declared in the script with their default values.
        /// </summary>
        public Dictionary<string, object?> Vars { get; set; } = new();

        /// <summary>
        /// Imported file-backed libraries available to this script.
        /// </summary>
        public List<ScriptImport> Imports { get; set; } = new();

        /// <summary>
        /// Named reusable subroutines declared in this file.
        /// </summary>
        public Dictionary<string, ScriptSubroutine> Subroutines { get; set; } =
            new(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional host-scoped bootstrap steps that run before main steps.
        /// Used for local pre-connection setup such as fetching ephemeral SSH credentials.
        /// </summary>
        public List<ScriptStep> Preconnect { get; set; } = new();

        /// <summary>
        /// The execution steps of the script.
        /// </summary>
        public List<ScriptStep> Steps { get; set; } = new();

        /// <summary>
        /// Resolved subroutine registry built during validation and reused at runtime.
        /// </summary>
        public ScriptSubroutineRegistry? SubroutineRegistry { get; set; }
    }

    /// <summary>
    /// File-backed library import.
    /// </summary>
    public class ScriptImport
    {
        /// <summary>
        /// Line number in the original YAML for error reporting.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Absolute file path to the imported library.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Alias used to qualify imported subroutine references.
        /// </summary>
        public string Alias { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reusable named block of script steps with explicit params and outputs.
    /// </summary>
    public class ScriptSubroutine
    {
        /// <summary>
        /// Line number in the original YAML for error reporting.
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Subroutine name as declared in the parent mapping.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Input parameter names available inside the subroutine scope.
        /// </summary>
        public List<string> Params { get; set; } = new();

        /// <summary>
        /// Output variable names that may be copied back to the caller.
        /// </summary>
        public List<string> Outputs { get; set; } = new();

        /// <summary>
        /// Steps executed when the subroutine is called.
        /// </summary>
        public List<ScriptStep> Steps { get; set; } = new();

        /// <summary>
        /// Parser-originated validation issues for this subroutine definition.
        /// </summary>
        public List<string> ParseErrors { get; } = new();
    }
}
