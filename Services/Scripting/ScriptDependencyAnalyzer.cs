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

    public class SshRequirementResult
    {
        public bool RequiresSshSession { get; set; }
        public bool UsesSftp { get; set; }
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

        private static readonly HashSet<string> BuiltInVariables = new(StringComparer.OrdinalIgnoreCase)
        {
            "_output", "_timestamp", "_iteration", "_last_error"
        };

        /// <summary>
        /// Analyzes a PresetInfo for column dependencies.
        /// </summary>
        public ColumnDependencyResult AnalyzePreset(PresetInfo preset)
        {
            if (preset.IsScript)
            {
                var parser = new ScriptParser();
                var script = parser.Parse(preset.Commands);
                return AnalyzeScript(script);
            }
            else
            {
                return AnalyzeSimpleCommands(preset.Commands);
            }
        }

        /// <summary>
        /// Analyzes a parsed Script for column dependencies.
        /// </summary>
        public ColumnDependencyResult AnalyzeScript(Script script)
        {
            var definedVars = new HashSet<string>(BuiltInVariables, StringComparer.OrdinalIgnoreCase);
            var referencedVars = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Script vars section defines variables
            foreach (var key in script.Vars.Keys)
                definedVars.Add(key);

            AnalyzeSteps(script.Steps, definedVars, referencedVars);

            // External reads = referenced vars that aren't script-defined
            referencedVars.ExceptWith(definedVars);

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

        public SshRequirementResult AnalyzeSshRequirements(Script script)
        {
            var result = new SshRequirementResult();
            AnalyzeSshRequirementsInSteps(script.Steps, result);
            return result;
        }

        private void AnalyzeSshRequirementsInSteps(List<ScriptStep>? steps, SshRequirementResult result)
        {
            if (steps == null) return;

            foreach (var step in steps)
            {
                var stepType = step.GetStepType();
                if (stepType == StepType.Send)
                {
                    result.RequiresSshSession = true;
                }
                else if (stepType == StepType.Sftp)
                {
                    result.UsesSftp = true;
                    if (step.Sftp == null || string.IsNullOrWhiteSpace(step.Sftp.Host))
                        result.SftpUsesDefaultHost = true;
                    if (step.Sftp == null || string.IsNullOrWhiteSpace(step.Sftp.Username) || string.IsNullOrWhiteSpace(step.Sftp.Password))
                        result.SftpUsesDefaultCredentials = true;
                }

                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Then, result);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Else, result);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Do, result);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Try, result);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Catch, result);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                AnalyzeSshRequirementsInSteps(step.Finally, result);
                if (HasCompleteSshRequirementSignal(result))
                    return;

                if (step.Elif == null)
                    continue;

                foreach (var branch in step.Elif)
                {
                    AnalyzeSshRequirementsInSteps(branch.Then, result);
                    if (HasCompleteSshRequirementSignal(result))
                        return;
                }
            }
        }

        private static bool HasCompleteSshRequirementSignal(SshRequirementResult result)
        {
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

                    case StepType.Parse:
                        if (step.Parse != null)
                        {
                            AddBareVarReference(step.Parse.From, referencedVars);
                            if (!string.IsNullOrEmpty(step.Parse.Into))
                                definedVars.Add(step.Parse.Into);
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
                ExtractVarReferences(collection, referencedVars);
                // Also treat bare collection name as a reference if it's not a variable expression
                if (!collection.Contains("${") && !collection.Contains("{{"))
                    AddBareVarReference(collection, referencedVars);
            }
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
    }
}
