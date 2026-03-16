using System;
using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// Resolved set of local and imported subroutine definitions available to a script execution.
    /// </summary>
    public sealed class ScriptSubroutineRegistry
    {
        private readonly Dictionary<string, ScriptSubroutineDefinition> _localDefinitions;
        private readonly Dictionary<string, ScriptImportedLibrary> _importsByAlias;

        public ScriptSubroutineRegistry(
            Script rootScript,
            Dictionary<string, ScriptSubroutineDefinition>? localDefinitions = null,
            Dictionary<string, ScriptImportedLibrary>? importsByAlias = null)
        {
            RootScript = rootScript ?? throw new ArgumentNullException(nameof(rootScript));
            _localDefinitions = localDefinitions ?? new Dictionary<string, ScriptSubroutineDefinition>(StringComparer.OrdinalIgnoreCase);
            _importsByAlias = importsByAlias ?? new Dictionary<string, ScriptImportedLibrary>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Executable root script that owns this registry.
        /// </summary>
        public Script RootScript { get; }

        /// <summary>
        /// Local subroutines declared directly in the root script.
        /// </summary>
        public IReadOnlyDictionary<string, ScriptSubroutineDefinition> LocalDefinitions => _localDefinitions;

        /// <summary>
        /// Imported libraries keyed by alias.
        /// </summary>
        public IReadOnlyDictionary<string, ScriptImportedLibrary> ImportsByAlias => _importsByAlias;

        /// <summary>
        /// Resolves a subroutine reference relative to the current execution scope.
        /// Bare names resolve within the root script or the current imported library.
        /// Alias-qualified names resolve only through imported libraries.
        /// </summary>
        public bool TryResolve(
            string reference,
            ScriptSubroutineDefinition? currentSubroutine,
            out ScriptSubroutineDefinition? definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(reference))
                return false;

            var trimmed = reference.Trim();
            var dotIndex = trimmed.IndexOf('.');
            if (dotIndex > 0 && dotIndex < trimmed.Length - 1)
            {
                var alias = trimmed[..dotIndex];
                var name = trimmed[(dotIndex + 1)..];
                if (_importsByAlias.TryGetValue(alias, out var library) &&
                    library.DefinitionsByName.TryGetValue(name, out var importedDefinition))
                {
                    definition = importedDefinition;
                    return true;
                }

                return false;
            }

            if (!string.IsNullOrWhiteSpace(currentSubroutine?.ImportAlias) &&
                _importsByAlias.TryGetValue(currentSubroutine.ImportAlias, out var currentLibrary) &&
                currentLibrary.DefinitionsByName.TryGetValue(trimmed, out var libraryDefinition))
            {
                definition = libraryDefinition;
                return true;
            }

            return _localDefinitions.TryGetValue(trimmed, out definition);
        }
    }

    /// <summary>
    /// Imported library metadata and its resolved subroutines.
    /// </summary>
    public sealed class ScriptImportedLibrary
    {
        public ScriptImportedLibrary(
            string alias,
            string path,
            Script script,
            Dictionary<string, ScriptSubroutineDefinition>? definitionsByName = null)
        {
            Alias = alias ?? throw new ArgumentNullException(nameof(alias));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Script = script ?? throw new ArgumentNullException(nameof(script));
            DefinitionsByName = definitionsByName ?? new Dictionary<string, ScriptSubroutineDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        public string Alias { get; }

        public string Path { get; }

        public Script Script { get; }

        public IReadOnlyDictionary<string, ScriptSubroutineDefinition> DefinitionsByName { get; }
    }

    /// <summary>
    /// Concrete subroutine definition resolved into a runtime registry.
    /// </summary>
    public sealed class ScriptSubroutineDefinition
    {
        public ScriptSubroutineDefinition(
            Script script,
            ScriptSubroutine subroutine,
            string? importAlias)
        {
            Script = script ?? throw new ArgumentNullException(nameof(script));
            Subroutine = subroutine ?? throw new ArgumentNullException(nameof(subroutine));
            ImportAlias = importAlias;
        }

        public Script Script { get; }

        public ScriptSubroutine Subroutine { get; }

        public string? ImportAlias { get; }

        public string Name => Subroutine.Name;

        public string QualifiedName => string.IsNullOrWhiteSpace(ImportAlias)
            ? Name
            : $"{ImportAlias}.{Name}";
    }
}
