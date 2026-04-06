using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SSH_Helper.Models;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Result of analyzing a script or command text for column dependencies.
    /// </summary>
    public class ColumnDependencyResult
    {
        /// <summary>
        /// Variable references that are not defined within the script itself.
        /// These are potential grid column dependencies.
        /// </summary>
        public HashSet<string> ReferencedColumns { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class PresetColumnDependencyResult : ColumnDependencyResult
    {
        /// <summary>
        /// Indicates that the preset's script explicitly suppresses the missing-column warning dialog.
        /// </summary>
        public bool SuppressMissingColumnWarning { get; set; }
    }

    public class SshRequirementResult
    {
        public bool RequiresSshSession { get; set; }
        public bool UsesSftp { get; set; }
        public bool UsesInteractive { get; set; }
        public bool UsesBrowserCallbackCapture { get; set; }
        public bool SftpUsesDefaultHost { get; set; }
        public bool SftpUsesDefaultCredentials { get; set; }
    }

    /// <summary>
    /// Statically analyzes scripts and command text to identify grid column
    /// dependencies before execution. This allows warning the user about
    /// missing columns that would silently resolve to empty strings.
    /// </summary>
    public class ScriptDependencyAnalyzer
    {
        private static readonly Regex VarRefPattern = new(@"\$\{([^}]+)\}|\{\{([^}]+)\}\}", RegexOptions.Compiled);
        private static readonly Regex BareVariableNamePattern = new(@"^[A-Za-z_]\w*$", RegexOptions.Compiled);
        private static readonly Regex BareIdentifierPattern = new(@"\b[A-Za-z_]\w*\b", RegexOptions.Compiled);
        private static readonly Regex FunctionCallExpressionPattern = new(
            @"^[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*\(",
            RegexOptions.Compiled);
        private static readonly Regex MemberOrIndexerExpressionPattern = new(
            @"^[A-Za-z_]\w*(?:\s*(?:\.\s*[A-Za-z_]\w*|\[[^\]]+\]))+$",
            RegexOptions.Compiled);

        private static readonly HashSet<string> BuiltInVariables = new(StringComparer.OrdinalIgnoreCase)
        {
            "_output", "_timestamp", "_iteration", "_last_error", "_writefile"
        };

        private static readonly HashSet<string> ExpressionKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "and", "or", "not", "is", "empty", "defined", "matches", "contains",
            "startswith", "endswith", "in", "true", "false", "null", "pretty"
        };

        /// <summary>
        /// Analyzes a PresetInfo for column dependencies.
        /// </summary>
        public ColumnDependencyResult AnalyzePreset(PresetInfo preset)
        {
            var result = AnalyzePresetDetails(preset);
            return new ColumnDependencyResult
            {
                ReferencedColumns = result.ReferencedColumns
            };
        }

        /// <summary>
        /// Analyzes a PresetInfo for column dependencies and exposes per-preset warning suppression metadata.
        /// </summary>
        public PresetColumnDependencyResult AnalyzePresetDetails(PresetInfo preset)
        {
            if (preset.IsScript)
            {
                var parser = new ScriptParser();
                var script = parser.Parse(preset.Commands);
                var result = AnalyzeScript(script);
                return new PresetColumnDependencyResult
                {
                    ReferencedColumns = result.ReferencedColumns,
                    SuppressMissingColumnWarning = script.SuppressMissingColumnWarning
                };
            }

            return new PresetColumnDependencyResult
            {
                ReferencedColumns = AnalyzeSimpleCommands(preset.Commands).ReferencedColumns
            };
        }

        /// <summary>
        /// Analyzes a parsed Script for column dependencies.
        /// </summary>
        public ColumnDependencyResult AnalyzeScript(Script script)
        {
            var globalDefinedVars = new HashSet<string>(BuiltInVariables, StringComparer.OrdinalIgnoreCase);
            var referencedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rootReferencedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Script vars section defines variables
            foreach (var key in script.Vars.Keys)
                globalDefinedVars.Add(key);

            AnalyzeSteps(script.Steps, globalDefinedVars, rootReferencedVars);

            rootReferencedVars.ExceptWith(globalDefinedVars);
            referencedVars.UnionWith(rootReferencedVars);

            var reachableLocalSubroutines = CollectReachableLocalSubroutines(script);
            foreach (var subroutineName in reachableLocalSubroutines)
            {
                if (!script.Subroutines.TryGetValue(subroutineName, out var subroutine))
                    continue;

                var localDefinedVars = new HashSet<string>(globalDefinedVars, StringComparer.OrdinalIgnoreCase);
                var localReferencedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var param in subroutine.Params)
                    localDefinedVars.Add(param);
                foreach (var output in subroutine.Outputs)
                    localDefinedVars.Add(output);

                AnalyzeSteps(subroutine.Steps, localDefinedVars, localReferencedVars);
                localReferencedVars.ExceptWith(localDefinedVars);
                referencedVars.UnionWith(localReferencedVars);
            }

            // External reads = referenced vars that aren't script-defined
            referencedVars.RemoveWhere(IsRuntimeUnderscoreVariable);

            return new ColumnDependencyResult { ReferencedColumns = referencedVars };
        }

        /// <summary>
        /// Analyzes simple (non-YAML) command text for ${variable} references.
        /// All references are potential column dependencies since simple commands
        /// have no variable definition mechanism.
        /// </summary>
        public ColumnDependencyResult AnalyzeSimpleCommands(string commandText)
        {
            var referencedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ExtractVarReferences(commandText, referencedVars);

            // Remove built-ins
            referencedVars.ExceptWith(BuiltInVariables);
            referencedVars.RemoveWhere(IsRuntimeUnderscoreVariable);

            return new ColumnDependencyResult { ReferencedColumns = referencedVars };
        }

        /// <summary>
        /// Analyzes multiple presets and merges results.
        /// Variables defined in earlier presets are available to later ones.
        /// </summary>
        public ColumnDependencyResult AnalyzePresets(IEnumerable<PresetInfo> presets)
        {
            var combined = new ColumnDependencyResult();
            foreach (var preset in presets)
            {
                var result = AnalyzePreset(preset);
                combined.ReferencedColumns.UnionWith(result.ReferencedColumns);
            }
            return combined;
        }

        /// <summary>
        /// Analyzes multiple presets and removes references that are resolved
        /// by external variable sources (for example active environment variables).
        /// </summary>
        public ColumnDependencyResult AnalyzePresets(
            IEnumerable<PresetInfo> presets,
            IEnumerable<string>? externallyResolvedVariables)
        {
            var combined = AnalyzePresets(presets);
            if (externallyResolvedVariables == null)
                return combined;

            var resolvedSet = new HashSet<string>(
                externallyResolvedVariables
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim()),
                StringComparer.OrdinalIgnoreCase);

            combined.ReferencedColumns.ExceptWith(resolvedSet);
            return combined;
        }

        /// <summary>
        /// Analyzes a single preset and removes references that are resolved by external variable sources.
        /// </summary>
        public PresetColumnDependencyResult AnalyzePresetDetails(
            PresetInfo preset,
            IEnumerable<string>? externallyResolvedVariables)
        {
            var result = AnalyzePresetDetails(preset);
            if (externallyResolvedVariables == null)
                return result;

            var resolvedSet = new HashSet<string>(
                externallyResolvedVariables
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Select(v => v.Trim()),
                StringComparer.OrdinalIgnoreCase);

            result.ReferencedColumns.ExceptWith(resolvedSet);
            return result;
        }

        public SshRequirementResult AnalyzeSshRequirements(Script script)
        {
            var result = new SshRequirementResult();
            AnalyzeSshRequirementsInSteps(
                script.Steps,
                result,
                script.SubroutineRegistry,
                currentSubroutine: null,
                visitedSubroutines: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            return result;
        }

        private void AnalyzeSshRequirementsInSteps(
            List<ScriptStep>? steps,
            SshRequirementResult result,
            ScriptSubroutineRegistry? registry,
            ScriptSubroutineDefinition? currentSubroutine,
            HashSet<string> visitedSubroutines)
        {
            if (steps == null) return;

            foreach (var step in steps)
            {
                var stepType = step.GetStepType();
                if (stepType == StepType.Send)
                {
                    result.RequiresSshSession = true;
                }
                else if (stepType == StepType.Interactive)
                {
                    result.RequiresSshSession = true;
                    result.UsesInteractive = true;
                }
                else if (stepType == StepType.BrowserCallbackCapture)
                {
                    result.UsesBrowserCallbackCapture = true;
                }
                else if (stepType == StepType.Sftp)
                {
                    result.UsesSftp = true;
                    if (step.Sftp == null || string.IsNullOrWhiteSpace(step.Sftp.Host))
                        result.SftpUsesDefaultHost = true;
                    if (step.Sftp == null || string.IsNullOrWhiteSpace(step.Sftp.Username) || string.IsNullOrWhiteSpace(step.Sftp.Password))
                        result.SftpUsesDefaultCredentials = true;
                }
                else if (stepType == StepType.Call &&
                         step.Call != null &&
                         registry != null &&
                         registry.TryResolve(step.Call.Subroutine, currentSubroutine, out var definition) &&
                         definition != null &&
                         visitedSubroutines.Add(definition.QualifiedName))
                {
                    AnalyzeSshRequirementsInSteps(
                        definition.Subroutine.Steps,
                        result,
                        registry,
                        definition,
                        visitedSubroutines);
                }

                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Then, result, registry, currentSubroutine, visitedSubroutines);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Else, result, registry, currentSubroutine, visitedSubroutines);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Do, result, registry, currentSubroutine, visitedSubroutines);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Try, result, registry, currentSubroutine, visitedSubroutines);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Catch, result, registry, currentSubroutine, visitedSubroutines);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Finally, result, registry, currentSubroutine, visitedSubroutines);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                if (step.Elif != null)
                {
                    foreach (var branch in step.Elif)
                    {
                        AnalyzeSshRequirementsInSteps(branch.Then, result, registry, currentSubroutine, visitedSubroutines);
                        if (HasCompleteSshRequirementSignal(result))
                            return;
                    }
                }

                // Recurse into switch cases
                if (step.Cases != null)
                {
                    foreach (var switchCase in step.Cases)
                    {
                        AnalyzeSshRequirementsInSteps(switchCase.Do, result, registry, currentSubroutine, visitedSubroutines);
                        if (HasCompleteSshRequirementSignal(result))
                            return;
                    }
                }

                // Recurse into parallel steps
                if (step.Parallel?.Steps != null)
                {
                    AnalyzeSshRequirementsInSteps(step.Parallel.Steps, result, registry, currentSubroutine, visitedSubroutines);
                    if (HasCompleteSshRequirementSignal(result))
                        return;
                }
            }
        }

        private static bool HasCompleteSshRequirementSignal(SshRequirementResult result)
        {
            if (result.UsesInteractive || result.UsesBrowserCallbackCapture)
                return true;

            return result.RequiresSshSession
                && result.UsesSftp
                && result.SftpUsesDefaultHost
                && result.SftpUsesDefaultCredentials;
        }

        private void AnalyzeSteps(List<ScriptStep>? steps, HashSet<string> definedVars, HashSet<string> referencedVars)
        {
            if (steps == null) return;

            foreach (var step in steps)
            {
                var stepType = step.GetStepType();

                switch (stepType)
                {
                    case StepType.Send:
                        ExtractVarReferences(step.Send, referencedVars);
                        if (step.Respond != null)
                        {
                            foreach (var pair in step.Respond)
                            {
                                ExtractVarReferences(pair.Expect, referencedVars);
                                ExtractVarReferences(pair.Reply, referencedVars);
                            }
                        }
                        if (!string.IsNullOrEmpty(step.Capture))
                            definedVars.Add(step.Capture);
                        break;

                    case StepType.Print:
                        ExtractVarReferences(step.Print, referencedVars);
                        break;

                    case StepType.Set:
                        AnalyzeSetCommand(step.Set, definedVars, referencedVars);
                        break;

                    case StepType.Exit:
                        ExtractVarReferences(step.Exit, referencedVars);
                        break;

                    case StepType.Extract:
                        if (step.Extract != null)
                        {
                            // extract.from is a bare variable reference
                            AddBareVarReference(step.Extract.From, referencedVars);
                            ExtractVarReferences(step.Extract.Pattern, referencedVars);

                            // extract.into defines variable(s)
                            if (step.Extract.Into is string intoStr)
                                definedVars.Add(intoStr);
                            else if (step.Extract.Into is List<string> intoList)
                                foreach (var v in intoList) definedVars.Add(v);
                        }
                        break;

                    case StepType.If:
                        ExtractVarReferences(step.If, referencedVars);
                        if (step.Elif != null)
                        {
                            foreach (var branch in step.Elif)
                            {
                                ExtractVarReferences(branch.If, referencedVars);
                                AnalyzeSteps(branch.Then, definedVars, referencedVars);
                            }
                        }
                        // Analyze both branches - definitions from either are added conservatively
                        AnalyzeSteps(step.Then, definedVars, referencedVars);
                        AnalyzeSteps(step.Else, definedVars, referencedVars);
                        break;

                    case StepType.Foreach:
                        AnalyzeForeachCommand(step.Foreach, definedVars, referencedVars);
                        ExtractVarReferences(step.When, referencedVars);
                        AnalyzeSteps(step.Do, definedVars, referencedVars);
                        break;

                    case StepType.While:
                        ExtractVarReferences(step.While, referencedVars);
                        AnalyzeSteps(step.Do, definedVars, referencedVars);
                        break;

                    case StepType.Try:
                        AnalyzeSteps(step.Try, definedVars, referencedVars);
                        AnalyzeSteps(step.Catch, definedVars, referencedVars);
                        AnalyzeSteps(step.Finally, definedVars, referencedVars);
                        break;

                    case StepType.Break:
                    case StepType.Continue:
                    case StepType.Return:
                        break;

                    case StepType.Readfile:
                        if (step.Readfile != null)
                        {
                            ExtractVarReferences(step.Readfile.Path, referencedVars);
                            if (!string.IsNullOrEmpty(step.Readfile.Into))
                                definedVars.Add(step.Readfile.Into);
                        }
                        break;

                    case StepType.Writefile:
                        if (step.Writefile != null)
                        {
                            ExtractVarReferences(step.Writefile.Path, referencedVars);
                            ExtractVarReferences(step.Writefile.Content, referencedVars);
                        }
                        break;

                    case StepType.Exists:
                        if (step.Exists != null)
                        {
                            ExtractVarReferences(step.Exists.Path, referencedVars);
                            ExtractVarReferences(step.Exists.Type, referencedVars);
                            if (!string.IsNullOrWhiteSpace(step.Exists.Into))
                            {
                                var into = step.Exists.Into.Trim();
                                definedVars.Add(into);
                                definedVars.Add(into + "_meta");
                            }
                        }
                        break;

                    case StepType.Input:
                        if (step.Input != null)
                        {
                            ExtractVarReferences(step.Input.Prompt, referencedVars);
                            ExtractVarReferences(step.Input.Default, referencedVars);
                            if (!string.IsNullOrEmpty(step.Input.Into))
                                definedVars.Add(step.Input.Into);
                        }
                        break;

                    case StepType.Choose:
                        if (step.Choose != null)
                        {
                            ExtractVarReferences(step.Choose.Prompt, referencedVars);
                            ExtractVarReferences(step.Choose.Default, referencedVars);
                            AnalyzeChoiceOptionsSourceReference(step.Choose.OptionsFrom, referencedVars);
                            foreach (var opt in step.Choose.Options)
                            {
                                ExtractVarReferences(opt.Label, referencedVars);
                                ExtractVarReferences(opt.Value, referencedVars);
                            }
                            if (!string.IsNullOrEmpty(step.Choose.Into))
                                definedVars.Add(step.Choose.Into);
                        }
                        break;

                    case StepType.Multiselect:
                        if (step.Multiselect != null)
                        {
                            ExtractVarReferences(step.Multiselect.Prompt, referencedVars);
                            AnalyzeChoiceOptionsSourceReference(step.Multiselect.OptionsFrom, referencedVars);
                            foreach (var opt in step.Multiselect.Options)
                            {
                                ExtractVarReferences(opt.Label, referencedVars);
                                ExtractVarReferences(opt.Value, referencedVars);
                            }
                            if (!string.IsNullOrEmpty(step.Multiselect.Into))
                            {
                                definedVars.Add(step.Multiselect.Into);
                                definedVars.Add($"{step.Multiselect.Into}_count");
                            }
                        }
                        break;

                    case StepType.Confirm:
                        if (step.Confirm != null)
                        {
                            ExtractVarReferences(step.Confirm.Prompt, referencedVars);
                            if (!string.IsNullOrEmpty(step.Confirm.Into))
                                definedVars.Add(step.Confirm.Into);
                        }
                        break;

                    case StepType.Interactive:
                        if (step.Interactive != null)
                        {
                            ExtractVarReferences(step.Interactive.Title, referencedVars);
                            ExtractVarReferences(step.Interactive.Command, referencedVars);
                            ExtractVarReferences(step.Interactive.Capture, referencedVars);

                            var captureVariable = step.Interactive.Capture?.Trim();
                            if (!string.IsNullOrWhiteSpace(captureVariable) && BareVariableNamePattern.IsMatch(captureVariable))
                                definedVars.Add(captureVariable);
                        }
                        break;

                    case StepType.UpdateColumn:
                        if (step.UpdateColumn != null)
                        {
                            ExtractVarReferences(step.UpdateColumn.Value, referencedVars);
                        }
                        break;

                    case StepType.UpdateEnvironment:
                        if (step.UpdateEnvironment != null)
                        {
                            ExtractVarReferences(step.UpdateEnvironment.Value, referencedVars);
                            if (!string.IsNullOrWhiteSpace(step.UpdateEnvironment.Variable))
                                definedVars.Add(step.UpdateEnvironment.Variable.Trim());
                        }
                        break;

                    case StepType.Log:
                        if (step.Log is string logStr)
                        {
                            ExtractVarReferences(logStr, referencedVars);
                        }
                        else if (step.Log is LogOptions logOpts)
                        {
                            ExtractVarReferences(logOpts.Message, referencedVars);
                        }
                        break;

                    case StepType.Http:
                        if (step.Http != null)
                        {
                            ExtractVarReferences(step.Http.Url, referencedVars);
                            ExtractVarReferences(step.Http.Method, referencedVars);
                            ExtractVarReferences(step.Http.Body, referencedVars);
                            ExtractVarReferences(step.Http.Auth, referencedVars);
                            ExtractVarReferences(step.Http.Username, referencedVars);
                            ExtractVarReferences(step.Http.Password, referencedVars);
                            ExtractVarReferences(step.Http.Token, referencedVars);
                            ExtractVarReferences(step.Http.ContentType, referencedVars);
                            if (step.Http.Headers != null)
                            {
                                foreach (var headerValue in step.Http.Headers.Values)
                                    ExtractVarReferences(headerValue, referencedVars);
                            }

                            if (!string.IsNullOrWhiteSpace(step.Http.Into))
                            {
                                definedVars.Add(step.Http.Into);
                                definedVars.Add(step.Http.Into + "_status");
                                definedVars.Add(step.Http.Into + "_headers");
                            }
                        }
                        break;
                    case StepType.BrowserCallbackCapture:
                        if (step.BrowserCallbackCapture != null)
                        {
                            ExtractVarReferences(step.BrowserCallbackCapture.StartUrl, referencedVars);
                            ExtractVarReferences(step.BrowserCallbackCapture.CallbackPath, referencedVars);
                            ExtractVarReferences(step.BrowserCallbackCapture.CaptureMode, referencedVars);
                            ExtractVarReferences(step.BrowserCallbackCapture.Into, referencedVars);
                            if (step.BrowserCallbackCapture.RequiredFields != null)
                            {
                                foreach (var requiredField in step.BrowserCallbackCapture.RequiredFields)
                                {
                                    ExtractVarReferences(requiredField, referencedVars);
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(step.BrowserCallbackCapture.Into))
                            {
                                var into = step.BrowserCallbackCapture.Into.Trim();
                                definedVars.Add(into);
                                definedVars.Add(into + "_count");
                                definedVars.Add(into + "_keys");
                            }
                        }
                        break;

                    case StepType.Ping:
                        if (step.Ping != null)
                        {
                            ExtractVarReferences(step.Ping.Host, referencedVars);
                            if (!string.IsNullOrWhiteSpace(step.Ping.Into))
                            {
                                definedVars.Add(step.Ping.Into);
                                definedVars.Add(step.Ping.Into + "_avg");
                                definedVars.Add(step.Ping.Into + "_loss");
                            }
                        }
                        break;

                    case StepType.Dns:
                        if (step.Dns != null)
                        {
                            ExtractVarReferences(step.Dns.Host, referencedVars);
                            ExtractVarReferences(step.Dns.Type, referencedVars);
                            if (!string.IsNullOrWhiteSpace(step.Dns.Into))
                            {
                                definedVars.Add(step.Dns.Into);
                                definedVars.Add(step.Dns.Into + "_count");
                            }
                        }
                        break;

                    case StepType.Portcheck:
                        if (step.Portcheck != null)
                        {
                            ExtractVarReferences(step.Portcheck.Host, referencedVars);
                            if (!string.IsNullOrWhiteSpace(step.Portcheck.Into))
                            {
                                definedVars.Add(step.Portcheck.Into);
                                definedVars.Add(step.Portcheck.Into + "_latency");
                            }
                        }
                        break;

                    case StepType.Sftp:
                        if (step.Sftp != null)
                        {
                            ExtractVarReferences(step.Sftp.Action, referencedVars);
                            ExtractVarReferences(step.Sftp.LocalPath, referencedVars);
                            ExtractVarReferences(step.Sftp.RemotePath, referencedVars);
                            ExtractVarReferences(step.Sftp.Host, referencedVars);
                            ExtractVarReferences(step.Sftp.Username, referencedVars);
                            ExtractVarReferences(step.Sftp.Password, referencedVars);
                            if (!string.IsNullOrWhiteSpace(step.Sftp.Into))
                            {
                                definedVars.Add(step.Sftp.Into);
                                definedVars.Add(step.Sftp.Into + "_bytes");
                            }
                        }
                        break;

                    case StepType.Webhook:
                        if (step.Webhook != null)
                        {
                            ExtractVarReferences(step.Webhook.Url, referencedVars);
                            ExtractVarReferences(step.Webhook.Body, referencedVars);
                            if (step.Webhook.Headers != null)
                            {
                                foreach (var headerValue in step.Webhook.Headers.Values)
                                    ExtractVarReferences(headerValue, referencedVars);
                            }
                            if (!string.IsNullOrEmpty(step.Webhook.Into))
                            {
                                definedVars.Add(step.Webhook.Into);
                                definedVars.Add(step.Webhook.Into + "_status");
                            }
                        }
                        break;

                    case StepType.LocalCmd:
                        if (step.LocalCmd != null)
                        {
                            ExtractVarReferences(step.LocalCmd.Command, referencedVars);
                            ExtractVarReferences(step.LocalCmd.WorkingDir, referencedVars);
                            if (step.LocalCmd.Env != null)
                            {
                                foreach (var envValue in step.LocalCmd.Env.Values)
                                    ExtractVarReferences(envValue, referencedVars);
                            }
                            if (!string.IsNullOrEmpty(step.LocalCmd.Into))
                            {
                                var into = step.LocalCmd.Into;
                                if (string.Equals(step.LocalCmd.RunMode, "background", StringComparison.OrdinalIgnoreCase))
                                {
                                    definedVars.Add(into + "_pid");
                                    definedVars.Add(into + "_started");
                                    definedVars.Add(into + "_start_error");
                                }
                                else if (step.LocalCmd.Interactive)
                                {
                                    definedVars.Add(into + "_exit_code");
                                }
                                else
                                {
                                    definedVars.Add(into + "_stdout");
                                    definedVars.Add(into + "_stderr");
                                    definedVars.Add(into + "_exit_code");
                                }
                            }
                        }
                        break;

                    case StepType.Vault:
                        if (step.Vault != null)
                        {
                            ExtractVarReferences(step.Vault.Path, referencedVars);
                            if (!string.IsNullOrEmpty(step.Vault.Into))
                                definedVars.Add(step.Vault.Into);
                            if (step.Vault.Keys != null)
                                foreach (var kvp in step.Vault.Keys)
                                    definedVars.Add(kvp.Value);
                            if (step.Vault.Write != null)
                                foreach (var kvp in step.Vault.Write)
                                    ExtractVarReferences(kvp.Value, referencedVars);
                            if (step.Vault.Patch != null)
                                foreach (var kvp in step.Vault.Patch)
                                    ExtractVarReferences(kvp.Value, referencedVars);
                        }
                        break;

                    case StepType.Parse:
                        if (step.Parse != null)
                        {
                            AddBareVarReference(step.Parse.From, referencedVars);
                            if (!string.IsNullOrEmpty(step.Parse.Into))
                                definedVars.Add(step.Parse.Into);
                        }
                        break;

                    case StepType.Assert:
                        if (step.Assert != null)
                        {
                            ExtractVarReferences(step.Assert.Condition, referencedVars);
                            ExtractVarReferences(step.Assert.Message, referencedVars);
                        }
                        break;

                    case StepType.Switch:
                        ExtractVarReferences(step.Switch, referencedVars);
                        if (step.Cases != null)
                        {
                            foreach (var switchCase in step.Cases)
                            {
                                ExtractVarReferences(switchCase.Value, referencedVars);
                                if (switchCase.Do != null)
                                    AnalyzeSteps(switchCase.Do, definedVars, referencedVars);
                            }
                        }
                        if (step.Else != null)
                            AnalyzeSteps(step.Else, definedVars, referencedVars);
                        break;

                    case StepType.Parallel:
                        if (step.Parallel?.Steps != null)
                            AnalyzeSteps(step.Parallel.Steps, definedVars, referencedVars);
                        break;

                    case StepType.Call:
                        if (step.Call != null)
                        {
                            foreach (var argExpression in step.Call.Args.Values)
                            {
                                AnalyzeExpressionReferences(argExpression, referencedVars);
                            }

                            foreach (var outputBinding in step.Call.Out.Values)
                            {
                                if (BareVariableNamePattern.IsMatch(outputBinding))
                                {
                                    definedVars.Add(outputBinding);
                                }
                            }
                        }
                        break;

                    case StepType.Table:
                        if (step.Table != null)
                        {
                            ExtractVarReferences(step.Table.Data, referencedVars);
                            if (!string.IsNullOrEmpty(step.Table.Into))
                                definedVars.Add(step.Table.Into);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Analyzes a set command: "varname = expression"
        /// Left side is a definition, right side may contain references.
        /// </summary>
        private void AnalyzeSetCommand(string? setExpr, HashSet<string> definedVars, HashSet<string> referencedVars)
        {
            if (string.IsNullOrEmpty(setExpr)) return;

            var eqIndex = setExpr.IndexOf('=');
            if (eqIndex <= 0) return;

            // Left side: variable being defined
            var varName = setExpr.Substring(0, eqIndex).Trim();
            // Handle nested paths like "obj.key.subkey" - use root name
            var dotIndex = varName.IndexOf('.');
            if (dotIndex > 0)
                varName = varName.Substring(0, dotIndex);
            definedVars.Add(varName);

            // Right side: may contain ${var} references
            var rightSide = setExpr.Substring(eqIndex + 1);
            ExtractVarReferences(rightSide, referencedVars);
        }

        /// <summary>
        /// Analyzes a foreach header: "item in collection"
        /// Defines the iterator variable and references the collection.
        /// </summary>
        private void AnalyzeForeachCommand(string? foreachExpr, HashSet<string> definedVars, HashSet<string> referencedVars)
        {
            if (string.IsNullOrEmpty(foreachExpr)) return;

            var parts = foreachExpr.Split(new[] { " in " }, 2, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var iteratorVar = parts[0].Trim();
                var collection = parts[1].Trim();

                // Iterator variable is defined
                definedVars.Add(iteratorVar);
                definedVars.Add(iteratorVar + "_index");

                // Collection may be a variable reference or contain ${var}/{{var}}
                AnalyzeExpressionReferences(collection, referencedVars);
            }
        }

        private static void AnalyzeExpressionReferences(string? expression, HashSet<string> references)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;

            ExtractVarReferences(expression, references);

            var trimmed = expression.Trim();
            if (BareVariableNamePattern.IsMatch(trimmed))
            {
                AddBareVarReference(trimmed, references);
                return;
            }

            if (LooksLikeStructuredExpression(trimmed) &&
                !trimmed.Contains("${", StringComparison.Ordinal) &&
                !trimmed.Contains("{{", StringComparison.Ordinal))
            {
                ExtractBareExpressionReferences(trimmed, references);
            }
        }

        private static bool LooksLikeStructuredExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            if (BareVariableNamePattern.IsMatch(expression))
                return true;

            if (FunctionCallExpressionPattern.IsMatch(expression))
                return true;

            if (MemberOrIndexerExpressionPattern.IsMatch(expression))
                return true;

            if (expression.EndsWith(".length", StringComparison.OrdinalIgnoreCase))
            {
                var baseExpression = expression.Substring(0, expression.Length - ".length".Length);
                return BareVariableNamePattern.IsMatch(baseExpression)
                    || MemberOrIndexerExpressionPattern.IsMatch(baseExpression);
            }

            return false;
        }

        private static HashSet<string> CollectReachableLocalSubroutines(Script script)
        {
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            TraverseReachableLocalSubroutines(script, script.Steps, reachable);
            return reachable;
        }

        private static void TraverseReachableLocalSubroutines(
            Script script,
            List<ScriptStep>? steps,
            HashSet<string> reachable)
        {
            if (steps == null)
                return;

            foreach (var step in steps)
            {
                if (step.GetStepType() == StepType.Call &&
                    step.Call != null &&
                    IsBareLocalSubroutineReference(step.Call.Subroutine, script.Subroutines, out var localName) &&
                    reachable.Add(localName))
                {
                    TraverseReachableLocalSubroutines(script, script.Subroutines[localName].Steps, reachable);
                }

                TraverseReachableLocalSubroutines(script, step.Then, reachable);
                TraverseReachableLocalSubroutines(script, step.Else, reachable);
                TraverseReachableLocalSubroutines(script, step.Do, reachable);
                TraverseReachableLocalSubroutines(script, step.Try, reachable);
                TraverseReachableLocalSubroutines(script, step.Catch, reachable);
                TraverseReachableLocalSubroutines(script, step.Finally, reachable);

                if (step.Elif != null)
                {
                    foreach (var branch in step.Elif)
                    {
                        TraverseReachableLocalSubroutines(script, branch.Then, reachable);
                    }
                }

                if (step.Cases != null)
                {
                    foreach (var switchCase in step.Cases)
                    {
                        TraverseReachableLocalSubroutines(script, switchCase.Do, reachable);
                    }
                }

                if (step.Parallel?.Steps != null)
                {
                    TraverseReachableLocalSubroutines(script, step.Parallel.Steps, reachable);
                }
            }
        }

        private static bool IsBareLocalSubroutineReference(
            string? reference,
            IReadOnlyDictionary<string, ScriptSubroutine> subroutines,
            out string localName)
        {
            localName = string.Empty;
            if (string.IsNullOrWhiteSpace(reference))
                return false;

            var trimmed = reference.Trim();
            if (trimmed.Contains('.', StringComparison.Ordinal))
                return false;

            if (!subroutines.ContainsKey(trimmed))
                return false;

            localName = trimmed;
            return true;
        }

        /// <summary>
        /// Extracts all ${variable} and {{variable}} references from a string and adds
        /// the base variable names to the set. Handles array indexing and .length suffix.
        /// </summary>
        private static void ExtractVarReferences(string? text, HashSet<string> references)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (Match match in VarRefPattern.Matches(text))
            {
                var expr = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                AddResolvedVarName(expr, references);
            }
        }

        /// <summary>
        /// Resolves a variable expression to its base name(s) and adds to the set.
        /// Handles: simple names, array[index], var.length
        /// </summary>
        private static void AddResolvedVarName(string expr, HashSet<string> references)
        {
            // If expression contains a function call, extract variable references
            // from its arguments rather than treating the whole expression as a name.
            if (FunctionCallExpressionPattern.IsMatch(expr.TrimStart()))
            {
                ExtractBareExpressionReferences(expr, references);
                return;
            }

            // Strip .length suffix
            if (expr.EndsWith(".length", StringComparison.OrdinalIgnoreCase))
            {
                expr = expr.Substring(0, expr.Length - ".length".Length);
            }

            // Handle array indexing: varname[index]
            var bracketIdx = expr.IndexOf('[');
            if (bracketIdx > 0)
            {
                var varName = expr.Substring(0, bracketIdx);
                references.Add(varName);

                // The index itself could be a variable name
                var closeBracket = expr.IndexOf(']', bracketIdx);
                if (closeBracket > bracketIdx + 1)
                {
                    var indexExpr = expr.Substring(bracketIdx + 1, closeBracket - bracketIdx - 1);
                    if (!int.TryParse(indexExpr, out _))
                    {
                        // Index is a variable reference
                        references.Add(indexExpr);
                    }
                }
                return;
            }

            // Handle dot-path property access: var.prop
            var dotIdx = expr.IndexOf('.');
            if (dotIdx > 0)
            {
                references.Add(expr.Substring(0, dotIdx));
                return;
            }

            // Simple variable name
            references.Add(expr);
        }

        /// <summary>
        /// Adds a bare variable name as a reference (for extract.from, parse.from, etc.).
        /// Ignores empty/null values.
        /// </summary>
        private static void AddBareVarReference(string? varName, HashSet<string> references)
        {
            if (string.IsNullOrWhiteSpace(varName)) return;
            references.Add(varName.Trim());
        }

        private static void ExtractBareExpressionReferences(string? expression, HashSet<string> references)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;

            var sanitized = MaskQuotedContent(expression);
            foreach (Match match in BareIdentifierPattern.Matches(sanitized))
            {
                var token = match.Value;
                if (ExpressionKeywords.Contains(token))
                    continue;

                var previousIndex = GetPreviousNonWhitespaceIndex(sanitized, match.Index - 1);
                if (previousIndex >= 0 && sanitized[previousIndex] == '.')
                    continue;

                if (LooksLikeQualifiedFunctionCall(sanitized, match.Index, match.Length))
                    continue;

                AddBareVarReference(token, references);
            }
        }

        private static string MaskQuotedContent(string text)
        {
            var chars = text.ToCharArray();
            var inString = false;
            var stringChar = '\0';

            for (int i = 0; i < chars.Length; i++)
            {
                var current = chars[i];
                if (!inString)
                {
                    if (current == '"' || current == '\'')
                    {
                        inString = true;
                        stringChar = current;
                        chars[i] = ' ';
                    }

                    continue;
                }

                if (current == '\\' && i + 1 < chars.Length)
                {
                    chars[i] = ' ';
                    chars[++i] = ' ';
                    continue;
                }

                chars[i] = ' ';
                if (current == stringChar)
                {
                    inString = false;
                    stringChar = '\0';
                }
            }

            return new string(chars);
        }

        private static bool LooksLikeQualifiedFunctionCall(string text, int tokenStart, int tokenLength)
        {
            var cursor = GetNextNonWhitespaceIndex(text, tokenStart + tokenLength);
            if (cursor < 0)
                return false;

            if (text[cursor] == '(')
                return true;

            if (text[cursor] != '.')
                return false;

            while (cursor >= 0 && cursor < text.Length && text[cursor] == '.')
            {
                cursor = GetNextNonWhitespaceIndex(text, cursor + 1);
                if (cursor < 0 || !IsIdentifierStart(text[cursor]))
                    return false;

                cursor++;
                while (cursor < text.Length && IsIdentifierPart(text[cursor]))
                    cursor++;

                cursor = GetNextNonWhitespaceIndex(text, cursor);
                if (cursor < 0)
                    return false;
            }

            return cursor >= 0 && cursor < text.Length && text[cursor] == '(';
        }

        private static int GetPreviousNonWhitespaceIndex(string text, int start)
        {
            for (int i = start; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        private static int GetNextNonWhitespaceIndex(string text, int start)
        {
            for (int i = start; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

        private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';

        private static void AnalyzeChoiceOptionsSourceReference(string? source, HashSet<string> references)
        {
            if (string.IsNullOrWhiteSpace(source))
                return;

            ExtractVarReferences(source, references);

            var trimmed = source.Trim();
            if (trimmed.Length == 0)
                return;

            if (BareVariableNamePattern.IsMatch(trimmed))
            {
                AddBareVarReference(trimmed, references);
            }
        }

        private static bool IsRuntimeUnderscoreVariable(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                return false;

            var trimmed = variableName.Trim();
            return trimmed.StartsWith("_", StringComparison.Ordinal)
                && BareVariableNamePattern.IsMatch(trimmed);
        }
    }
}
