using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Extracts data from a variable using regex patterns with capture groups.
    /// </summary>
    public class ExtractCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Extract == null)
                return Task.FromResult(CommandResult.Fail("Extract command has no options"));

            var options = step.Extract;

            if (string.IsNullOrEmpty(options.From))
                return Task.FromResult(CommandResult.Fail("Extract requires 'from' variable"));

            if (string.IsNullOrEmpty(options.Pattern))
                return Task.FromResult(CommandResult.Fail("Extract requires 'pattern'"));

            if (options.Into == null)
                return Task.FromResult(CommandResult.Fail("Extract requires 'into' variable"));

            // Get the source text
            var sourceText = context.GetVariableString(options.From);
            if (string.IsNullOrEmpty(sourceText))
            {
                context.EmitOutput($"Extract: source variable '{options.From}' is empty", ScriptOutputType.Warning);
                return Task.FromResult(CommandResult.Ok());
            }

            // Prepare the pattern (strip delimiters if present)
            var pattern = options.Pattern.Trim();
            if (pattern.StartsWith("/") && pattern.EndsWith("/"))
            {
                pattern = pattern.Substring(1, pattern.Length - 2);
            }
            else if ((pattern.StartsWith("'") && pattern.EndsWith("'")) ||
                     (pattern.StartsWith("\"") && pattern.EndsWith("\"")))
            {
                pattern = pattern.Substring(1, pattern.Length - 2);
            }

            try
            {
                var regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var matches = regex.Matches(sourceText);

                if (matches.Count == 0)
                {
                    context.EmitOutput($"Extract: no matches found for pattern '{pattern}'", ScriptOutputType.Debug);
                    // Set empty value(s)
                    SetEmptyResults(options.Into, context);
                    return Task.FromResult(CommandResult.Ok());
                }

                // Determine which match(es) to capture
                var matchMode = options.Match?.ToLowerInvariant() ?? "first";

                if (matchMode == "all")
                {
                    // Capture all matches as a list
                    ExtractAllMatches(matches, options.Into, context);
                }
                else if (matchMode == "last")
                {
                    // Capture last match
                    ExtractSingleMatch(matches[matches.Count - 1], options.Into, context);
                }
                else if (int.TryParse(matchMode, out var index))
                {
                    // Capture specific match by index
                    if (index >= 0 && index < matches.Count)
                    {
                        ExtractSingleMatch(matches[index], options.Into, context);
                    }
                    else
                    {
                        context.EmitOutput($"Extract: match index {index} out of range (0-{matches.Count - 1})", ScriptOutputType.Warning);
                        SetEmptyResults(options.Into, context);
                    }
                }
                else
                {
                    // Default: first match
                    ExtractSingleMatch(matches[0], options.Into, context);
                }

                return Task.FromResult(CommandResult.Ok());
            }
            catch (RegexParseException ex)
            {
                return Task.FromResult(CommandResult.Fail($"Invalid regex pattern: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Fail($"Extract failed: {ex.Message}"));
            }
        }

        private void ExtractSingleMatch(Match match, object into, ScriptContext context)
        {
            if (into is string varName)
            {
                // Single variable - capture first group (or full match if no groups)
                var value = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                context.SetVariable(varName, value);
                context.EmitOutput($"Extract: {varName} = '{TruncateForDisplay(value)}'", ScriptOutputType.Debug);
            }
            else if (into is System.Collections.IList varList)
            {
                // Multiple variables for multiple capture groups
                for (int i = 0; i < varList.Count; i++)
                {
                    var groupIndex = i + 1; // Group 0 is full match, groups start at 1
                    var value = groupIndex < match.Groups.Count ? match.Groups[groupIndex].Value : "";
                    var name = varList[i]?.ToString() ?? $"group{i}";
                    context.SetVariable(name, value);
                    context.EmitOutput($"Extract: {name} = '{TruncateForDisplay(value)}'", ScriptOutputType.Debug);
                }
            }
        }

        private void ExtractAllMatches(MatchCollection matches, object into, ScriptContext context)
        {
            if (into is string varName)
            {
                // Capture all first groups as a list
                var values = new List<string>();
                foreach (Match match in matches)
                {
                    var value = match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
                    values.Add(value);
                }
                context.SetVariable(varName, values);
                context.EmitOutput($"Extract: {varName} = [{values.Count} items]", ScriptOutputType.Debug);
            }
            else if (into is System.Collections.IList varList && varList.Count > 0)
            {
                // For multiple variables with "all", each variable gets a list of that group's captures
                var groupLists = new Dictionary<string, List<string>>();

                foreach (var varObj in varList)
                {
                    var name = varObj?.ToString();
                    if (!string.IsNullOrEmpty(name))
                        groupLists[name] = new List<string>();
                }

                foreach (Match match in matches)
                {
                    int groupIndex = 1;
                    foreach (var varObj in varList)
                    {
                        var name = varObj?.ToString();
                        if (!string.IsNullOrEmpty(name) && groupIndex < match.Groups.Count)
                        {
                            groupLists[name].Add(match.Groups[groupIndex].Value);
                        }
                        groupIndex++;
                    }
                }

                foreach (var kvp in groupLists)
                {
                    context.SetVariable(kvp.Key, kvp.Value);
                    context.EmitOutput($"Extract: {kvp.Key} = [{kvp.Value.Count} items]", ScriptOutputType.Debug);
                }
            }
        }

        private void SetEmptyResults(object into, ScriptContext context)
        {
            if (into is string varName)
            {
                context.SetVariable(varName, "");
            }
            else if (into is System.Collections.IList varList)
            {
                foreach (var varObj in varList)
                {
                    var name = varObj?.ToString();
                    if (!string.IsNullOrEmpty(name))
                        context.SetVariable(name, "");
                }
            }
        }

        private string TruncateForDisplay(string value, int maxLength = 50)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            value = value.Replace("\r", "").Replace("\n", "\\n");

            if (value.Length <= maxLength)
                return value;

            return value.Substring(0, maxLength) + "...";
        }
    }
}
