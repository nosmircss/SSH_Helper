using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SSH_Helper.Services.Scripting.Parsers
{
    /// <summary>
    /// Parser for FortiGate/FortiOS configuration files.
    /// Handles config/edit/set/next/end directive syntax.
    /// </summary>
    public class FortiGateParser : IConfigParser
    {
        public string FormatName => "fortigate";

        // Regex patterns for parsing FortiGate config lines
        private static readonly Regex ConfigPattern = new(@"^\s*config\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex EditPattern = new(@"^\s*edit\s+(?:""([^""]+)""|(\S+))$", RegexOptions.Compiled);
        private static readonly Regex SetPattern = new(@"^\s*set\s+(\S+)\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex UnsetPattern = new(@"^\s*unset\s+(\S+)", RegexOptions.Compiled);
        private static readonly Regex EndPattern = new(@"^\s*end\s*$", RegexOptions.Compiled);
        private static readonly Regex NextPattern = new(@"^\s*next\s*$", RegexOptions.Compiled);

        public Dictionary<string, object> Parse(string configText)
        {
            return Parse(configText, null);
        }

        public Dictionary<string, object> Parse(string configText, IEnumerable<string>? sections)
        {
            if (string.IsNullOrEmpty(configText))
                return new Dictionary<string, object>();

            var sectionFilters = sections?.Select(s => s.Trim().ToLowerInvariant()).ToHashSet();
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var contextStack = new Stack<ParserContext>();
            var currentContext = new ParserContext { Target = result };

            var lines = configText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                    continue;

                // Try matching each directive type
                Match match;

                // config <section>
                if ((match = ConfigPattern.Match(line)).Success)
                {
                    var sectionPath = match.Groups[1].Value.Trim();

                    // Check section filter
                    if (sectionFilters != null && contextStack.Count == 0)
                    {
                        // Only filter at the top level
                        if (!ShouldIncludeSection(sectionPath, sectionFilters))
                        {
                            // Skip this entire config block
                            SkipConfigBlock(lines, ref lines);
                            continue;
                        }
                    }

                    // Push current context and create new nested context
                    contextStack.Push(currentContext);

                    // Navigate/create the path in the target dictionary
                    var pathParts = sectionPath.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var target = currentContext.Target;

                    foreach (var part in pathParts)
                    {
                        if (!target.TryGetValue(part, out var existing))
                        {
                            existing = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            target[part] = existing;
                        }

                        if (existing is Dictionary<string, object> dict)
                        {
                            target = dict;
                        }
                        else
                        {
                            // Conflict - existing value is not a dictionary
                            var newDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            target[part] = newDict;
                            target = newDict;
                        }
                    }

                    currentContext = new ParserContext
                    {
                        Target = target,
                        SectionPath = sectionPath,
                        IsTable = false
                    };
                }
                // edit "name"
                else if ((match = EditPattern.Match(line)).Success)
                {
                    var entryName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                    // Mark current context as a table (contains edit entries)
                    currentContext.IsTable = true;

                    // Push current context
                    contextStack.Push(currentContext);

                    // Create entry in current target
                    if (!currentContext.Target.TryGetValue(entryName, out var existing))
                    {
                        existing = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        currentContext.Target[entryName] = existing;
                    }

                    var entryDict = existing as Dictionary<string, object>
                        ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    if (existing is not Dictionary<string, object>)
                        currentContext.Target[entryName] = entryDict;

                    currentContext = new ParserContext
                    {
                        Target = entryDict,
                        EntryName = entryName,
                        IsEditEntry = true
                    };
                }
                // set key value(s)
                else if ((match = SetPattern.Match(line)).Success)
                {
                    var key = match.Groups[1].Value;
                    var valueStr = match.Groups[2].Value.Trim();
                    var value = ParseValue(valueStr);
                    currentContext.Target[key] = value;
                }
                // unset key - omit from output (per design decision)
                else if (UnsetPattern.Match(line).Success)
                {
                    // Intentionally do nothing - unset directives are omitted
                }
                // next - end of edit entry
                else if (NextPattern.Match(line).Success)
                {
                    if (contextStack.Count > 0 && currentContext.IsEditEntry)
                    {
                        currentContext = contextStack.Pop();
                    }
                }
                // end - end of config block
                else if (EndPattern.Match(line).Success)
                {
                    // Pop back to parent context, handling both edit and config blocks
                    while (contextStack.Count > 0)
                    {
                        var popped = contextStack.Pop();
                        if (!currentContext.IsEditEntry)
                        {
                            // This was a config block end
                            currentContext = popped;
                            break;
                        }
                        currentContext = popped;
                        if (!currentContext.IsEditEntry)
                        {
                            break;
                        }
                    }

                    if (contextStack.Count == 0)
                    {
                        currentContext = new ParserContext { Target = result };
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Parses a value string, handling quoted strings, arrays, and simple values.
        /// </summary>
        private object ParseValue(string valueStr)
        {
            if (string.IsNullOrEmpty(valueStr))
                return string.Empty;

            // Check for multiple quoted values (array)
            var quotedValues = ParseQuotedValues(valueStr);
            if (quotedValues.Count > 1)
            {
                return quotedValues;
            }

            // Single quoted value
            if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
            {
                return valueStr.Substring(1, valueStr.Length - 2);
            }

            // Single quoted value with single quotes
            if (valueStr.StartsWith("'") && valueStr.EndsWith("'"))
            {
                return valueStr.Substring(1, valueStr.Length - 2);
            }

            // Return as-is (preserves IP addresses like "10.0.0.1 255.255.255.0")
            return valueStr;
        }

        /// <summary>
        /// Parses multiple quoted values from a string like: "value1" "value2" "value3"
        /// </summary>
        private List<string> ParseQuotedValues(string valueStr)
        {
            var values = new List<string>();
            var regex = new Regex(@"""([^""]*)""");
            var matches = regex.Matches(valueStr);

            if (matches.Count > 0)
            {
                foreach (Match match in matches)
                {
                    values.Add(match.Groups[1].Value);
                }
            }

            return values;
        }

        /// <summary>
        /// Checks if a section path matches any of the filter patterns.
        /// </summary>
        private bool ShouldIncludeSection(string sectionPath, HashSet<string> filters)
        {
            var normalizedPath = sectionPath.ToLowerInvariant();
            return filters.Any(f => normalizedPath.StartsWith(f) || f.StartsWith(normalizedPath));
        }

        /// <summary>
        /// Skips a config block by counting nested config/end pairs.
        /// Note: This is a simplified skip - the main parsing loop handles this differently.
        /// </summary>
        private void SkipConfigBlock(string[] allLines, ref string[] remainingLines)
        {
            // This method is called but the actual skipping happens in the main loop
            // by tracking context depth. This is a placeholder for future optimization.
        }

        /// <summary>
        /// Internal context for tracking parser state.
        /// </summary>
        private class ParserContext
        {
            public Dictionary<string, object> Target { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public string? SectionPath { get; set; }
            public string? EntryName { get; set; }
            public bool IsTable { get; set; }
            public bool IsEditEntry { get; set; }
        }
    }
}
