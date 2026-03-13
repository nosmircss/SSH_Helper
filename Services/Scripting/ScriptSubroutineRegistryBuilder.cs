using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Builds the resolved subroutine registry for a script and validates call-site wiring.
    /// </summary>
    public sealed class ScriptSubroutineRegistryBuilder
    {
        public ScriptSubroutineRegistry Build(
            Script script,
            List<string> errors,
            bool enforceCanonicalSyntax)
        {
            if (script == null) throw new ArgumentNullException(nameof(script));
            if (errors == null) throw new ArgumentNullException(nameof(errors));

            var localDefinitions = CreateDefinitions(script, importAlias: null);
            var importsByAlias = script.Library
                ? new Dictionary<string, ScriptImportedLibrary>(StringComparer.OrdinalIgnoreCase)
                : LoadImports(script, errors, enforceCanonicalSyntax);

            var registry = new ScriptSubroutineRegistry(script, localDefinitions, importsByAlias);

            ValidateCallSites(script.Steps, registry, currentSubroutine: null, errors);
            foreach (var definition in localDefinitions.Values)
            {
                ValidateCallSites(definition.Subroutine.Steps, registry, definition, errors);
            }

            ValidateCallCycles(localDefinitions, registry, errors);
            return registry;
        }

        private static Dictionary<string, ScriptSubroutineDefinition> CreateDefinitions(
            Script script,
            string? importAlias)
        {
            var definitions = new Dictionary<string, ScriptSubroutineDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in script.Subroutines)
            {
                definitions[pair.Key] = new ScriptSubroutineDefinition(script, pair.Value, importAlias);
            }

            return definitions;
        }

        private Dictionary<string, ScriptImportedLibrary> LoadImports(
            Script script,
            List<string> errors,
            bool enforceCanonicalSyntax)
        {
            var importsByAlias = new Dictionary<string, ScriptImportedLibrary>(StringComparer.OrdinalIgnoreCase);

            foreach (var import in script.Imports)
            {
                var alias = import.Alias?.Trim() ?? string.Empty;
                var path = import.Path?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(alias))
                {
                    errors.Add($"Line {import.LineNumber}: import alias is required");
                    continue;
                }

                if (importsByAlias.ContainsKey(alias))
                {
                    errors.Add($"Line {import.LineNumber}: Duplicate import alias '{alias}'");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    errors.Add($"Line {import.LineNumber}: import path is required");
                    continue;
                }

                if (!Path.IsPathRooted(path))
                {
                    errors.Add($"Line {import.LineNumber}: import path must be absolute");
                    continue;
                }

                if (!ScriptFileAccessValidator.ValidateReadPath(path, out var readError))
                {
                    errors.Add($"Line {import.LineNumber}: {readError}");
                    continue;
                }

                if (!File.Exists(path))
                {
                    errors.Add($"Line {import.LineNumber}: import file does not exist: '{path}'");
                    continue;
                }

                Script importedScript;
                var parser = new ScriptParser();
                string importedText;
                try
                {
                    importedText = File.ReadAllText(path);
                    importedScript = parser.Parse(importedText);
                }
                catch (Exception ex)
                {
                    errors.Add($"Line {import.LineNumber}: failed to parse import '{alias}' from '{path}': {ex.Message}");
                    continue;
                }

                var importErrors = parser.Validate(
                    importedScript,
                    importedText,
                    enforceCanonicalSyntax: enforceCanonicalSyntax,
                    allowLibraryDefinitions: true);

                if (!importedScript.Library)
                {
                    importErrors.Add("Imported file must declare 'library: true'");
                }

                if (importErrors.Count > 0)
                {
                    foreach (var importError in importErrors)
                    {
                        errors.Add($"Line {import.LineNumber}: import '{alias}' ({path}) -> {importError}");
                    }

                    continue;
                }

                var definitions = CreateDefinitions(importedScript, alias);
                importsByAlias[alias] = new ScriptImportedLibrary(alias, path, importedScript, definitions);
            }

            return importsByAlias;
        }

        private void ValidateCallSites(
            List<ScriptStep>? steps,
            ScriptSubroutineRegistry registry,
            ScriptSubroutineDefinition? currentSubroutine,
            List<string> errors)
        {
            if (steps == null)
                return;

            foreach (var step in steps)
            {
                if (step.GetStepType() == StepType.Call)
                {
                    ValidateCallStep(step, registry, currentSubroutine, errors);
                }

                ValidateCallSites(step.Then, registry, currentSubroutine, errors);
                ValidateCallSites(step.Else, registry, currentSubroutine, errors);
                ValidateCallSites(step.Do, registry, currentSubroutine, errors);
                ValidateCallSites(step.Try, registry, currentSubroutine, errors);
                ValidateCallSites(step.Catch, registry, currentSubroutine, errors);
                ValidateCallSites(step.Finally, registry, currentSubroutine, errors);

                if (step.Elif != null)
                {
                    foreach (var branch in step.Elif)
                    {
                        ValidateCallSites(branch.Then, registry, currentSubroutine, errors);
                    }
                }

                if (step.Cases != null)
                {
                    foreach (var switchCase in step.Cases)
                    {
                        ValidateCallSites(switchCase.Do, registry, currentSubroutine, errors);
                    }
                }

                if (step.Parallel?.Steps != null)
                {
                    ValidateCallSites(step.Parallel.Steps, registry, currentSubroutine, errors);
                }
            }
        }

        private void ValidateCallStep(
            ScriptStep step,
            ScriptSubroutineRegistry registry,
            ScriptSubroutineDefinition? currentSubroutine,
            List<string> errors)
        {
            if (step.Call == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(step.Call.Subroutine))
            {
                errors.Add($"Line {step.LineNumber}: call.subroutine is required");
                return;
            }

            if (!registry.TryResolve(step.Call.Subroutine, currentSubroutine, out var target) || target == null)
            {
                errors.Add($"Line {step.LineNumber}: Unknown subroutine '{step.Call.Subroutine}'");
                return;
            }

            var paramsSet = new HashSet<string>(target.Subroutine.Params, StringComparer.OrdinalIgnoreCase);
            var outputsSet = new HashSet<string>(target.Subroutine.Outputs, StringComparer.OrdinalIgnoreCase);

            foreach (var param in target.Subroutine.Params)
            {
                if (!step.Call.Args.ContainsKey(param))
                {
                    errors.Add($"Line {step.LineNumber}: call to '{target.QualifiedName}' is missing required arg '{param}'");
                }
            }

            foreach (var argName in step.Call.Args.Keys)
            {
                if (!paramsSet.Contains(argName))
                {
                    errors.Add($"Line {step.LineNumber}: call to '{target.QualifiedName}' passes unknown arg '{argName}'");
                }
            }

            foreach (var outputName in step.Call.Out.Keys)
            {
                if (!outputsSet.Contains(outputName))
                {
                    errors.Add($"Line {step.LineNumber}: call to '{target.QualifiedName}' binds unknown output '{outputName}'");
                }
            }

            foreach (var binding in step.Call.Out)
            {
                if (!IsSimpleVariableName(binding.Value))
                {
                    errors.Add($"Line {step.LineNumber}: call.out target '{binding.Value}' must be a bare variable name");
                }
            }
        }

        private void ValidateCallCycles(
            IReadOnlyDictionary<string, ScriptSubroutineDefinition> definitions,
            ScriptSubroutineRegistry registry,
            List<string> errors)
        {
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = new Stack<string>();

            foreach (var definition in definitions.Values)
            {
                DetectCycles(definition, definitions, registry, visiting, visited, path, errors);
            }
        }

        private void DetectCycles(
            ScriptSubroutineDefinition definition,
            IReadOnlyDictionary<string, ScriptSubroutineDefinition> definitions,
            ScriptSubroutineRegistry registry,
            HashSet<string> visiting,
            HashSet<string> visited,
            Stack<string> path,
            List<string> errors)
        {
            if (visited.Contains(definition.Name))
            {
                return;
            }

            if (visiting.Contains(definition.Name))
            {
                var cycle = path.Reverse().Concat(new[] { definition.Name });
                errors.Add($"Line {definition.Subroutine.LineNumber}: recursive call cycle detected: {string.Join(" -> ", cycle)}");
                return;
            }

            visiting.Add(definition.Name);
            path.Push(definition.Name);

            foreach (var calledName in GetLocalCallTargets(definition.Subroutine.Steps, registry, definition))
            {
                if (definitions.TryGetValue(calledName, out var calledDefinition))
                {
                    DetectCycles(calledDefinition, definitions, registry, visiting, visited, path, errors);
                }
            }

            path.Pop();
            visiting.Remove(definition.Name);
            visited.Add(definition.Name);
        }

        private static HashSet<string> GetLocalCallTargets(
            List<ScriptStep>? steps,
            ScriptSubroutineRegistry registry,
            ScriptSubroutineDefinition currentSubroutine)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectLocalCallTargets(steps, registry, currentSubroutine, targets);
            return targets;
        }

        private static void CollectLocalCallTargets(
            List<ScriptStep>? steps,
            ScriptSubroutineRegistry registry,
            ScriptSubroutineDefinition currentSubroutine,
            HashSet<string> targets)
        {
            if (steps == null)
            {
                return;
            }

            foreach (var step in steps)
            {
                if (step.GetStepType() == StepType.Call &&
                    step.Call != null &&
                    registry.TryResolve(step.Call.Subroutine, currentSubroutine, out var target) &&
                    target != null &&
                    string.IsNullOrWhiteSpace(target.ImportAlias))
                {
                    targets.Add(target.Name);
                }

                CollectLocalCallTargets(step.Then, registry, currentSubroutine, targets);
                CollectLocalCallTargets(step.Else, registry, currentSubroutine, targets);
                CollectLocalCallTargets(step.Do, registry, currentSubroutine, targets);
                CollectLocalCallTargets(step.Try, registry, currentSubroutine, targets);
                CollectLocalCallTargets(step.Catch, registry, currentSubroutine, targets);
                CollectLocalCallTargets(step.Finally, registry, currentSubroutine, targets);

                if (step.Elif != null)
                {
                    foreach (var branch in step.Elif)
                    {
                        CollectLocalCallTargets(branch.Then, registry, currentSubroutine, targets);
                    }
                }

                if (step.Cases != null)
                {
                    foreach (var switchCase in step.Cases)
                    {
                        CollectLocalCallTargets(switchCase.Do, registry, currentSubroutine, targets);
                    }
                }

                if (step.Parallel?.Steps != null)
                {
                    CollectLocalCallTargets(step.Parallel.Steps, registry, currentSubroutine, targets);
                }
            }
        }

        private static bool IsSimpleVariableName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!(char.IsLetter(value[0]) || value[0] == '_'))
                return false;

            for (int i = 1; i < value.Length; i++)
            {
                var c = value[i];
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    return false;
            }

            return true;
        }
    }
}
