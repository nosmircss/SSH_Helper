using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Iterates over a collection or the entries of an object/map.
    /// Forms: "foreach: item in collection" or "foreach: key, value in map".
    /// Optional: "when: condition" to filter items per iteration.
    /// Iteration variables and metadata are block-scoped: prior values are restored on exit.
    /// </summary>
    public class ForeachCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;
        private static readonly Regex DictPattern = new(@"^(\w+)\s*,\s*(\w+)\s+in\s+(.+)$", RegexOptions.IgnoreCase);
        private static readonly Regex ForeachPattern = new(@"^(\w+)\s+in\s+(.+)$", RegexOptions.IgnoreCase);

        public ForeachCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        /// <summary>
        /// True when the iterator expression matches a supported foreach grammar:
        /// "item in collection" or "key, value in map". Shared with parse-time validation
        /// so malformed iterators are rejected before execution rather than at runtime.
        /// </summary>
        public static bool IsValidIteratorSyntax(string? iterator)
        {
            if (string.IsNullOrWhiteSpace(iterator))
                return false;

            var expr = iterator.Trim();
            return DictPattern.IsMatch(expr) || ForeachPattern.IsMatch(expr);
        }

        private const int MaxLabelLength = 48;

        private static string? TruncateLabel(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value!.Length <= MaxLabelLength) return value;
            var cut = MaxLabelLength - 1;
            if (char.IsHighSurrogate(value[cut - 1])) cut--;
            return value.Substring(0, cut) + "…";
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Foreach))
                return CommandResult.Fail("Foreach command has no iterator expression");

            if (step.Do == null || step.Do.Count == 0)
                return CommandResult.Fail("Foreach requires 'do' block");

            var expr = step.Foreach.Trim();

            // Dictionary form: "key, value in map"
            var dictMatch = DictPattern.Match(expr);
            if (dictMatch.Success)
            {
                var keyName = dictMatch.Groups[1].Value;
                var valueName = dictMatch.Groups[2].Value;
                var entries = ResolveDictEntries(dictMatch.Groups[3].Value.Trim(), context);

                return await IterateAsync(step, context, cancellationToken,
                    count: entries.Count,
                    metadataPrefix: valueName,
                    iterationNames: new[] { keyName, valueName },
                    setIteration: i =>
                    {
                        context.SetVariable(keyName, entries[i].Key);
                        context.SetVariable(valueName, entries[i].Value);
                    },
                    labelFor: i => TruncateLabel(entries[i].Key));
            }

            // Single form: "item in collection"
            var match = ForeachPattern.Match(expr);
            if (!match.Success)
                return CommandResult.Fail($"Invalid foreach syntax: '{step.Foreach}'. Expected 'item in collection' or 'key, value in map'");

            var itemVarName = match.Groups[1].Value;
            var items = ResolveCollection(match.Groups[2].Value.Trim(), context);

            return await IterateAsync(step, context, cancellationToken,
                count: items.Count,
                metadataPrefix: itemVarName,
                iterationNames: new[] { itemVarName },
                setIteration: i => context.SetVariable(itemVarName, items[i]),
                labelFor: i => TruncateLabel(items[i]));
        }

        private async Task<CommandResult> IterateAsync(
            ScriptStep step,
            ScriptContext context,
            CancellationToken cancellationToken,
            int count,
            string metadataPrefix,
            IReadOnlyList<string> iterationNames,
            Action<int> setIteration,
            Func<int, string?>? labelFor = null)
        {
            context.EmitOutput($"Foreach: iterating {count} item(s)", ScriptOutputType.Debug);

            var metadataNames = new[]
            {
                $"{metadataPrefix}_index",
                $"{metadataPrefix}_number",
                $"{metadataPrefix}_first",
                $"{metadataPrefix}_last",
                $"{metadataPrefix}_count"
            };

            // Block scope: remember prior values of every variable the loop writes, restore on exit.
            var scopedNames = iterationNames.Concat(metadataNames).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var saved = new Dictionary<string, (bool existed, object? value)>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in scopedNames)
                saved[name] = (context.HasVariable(name), context.GetVariable(name));

            var evaluator = new ExpressionEvaluator(context);
            int executed = 0;

            // Iteration frame: tags every nested step event with (loop path, index, item label)
            // so the canvas can attribute events to iterations. Index -1 until the first
            // iteration starts; no events fire in that window.
            context.PushIterationFrame(step.StepPath ?? string.Empty, -1);

            try
            {
                for (int index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    setIteration(index);
                    context.SetVariable($"{metadataPrefix}_index", index);
                    context.SetVariable($"{metadataPrefix}_number", index + 1);
                    context.SetVariable($"{metadataPrefix}_first", index == 0);
                    context.SetVariable($"{metadataPrefix}_last", index == count - 1);
                    context.SetVariable($"{metadataPrefix}_count", count);
                    context.SetCurrentIterationFrame(index, labelFor?.Invoke(index));

                    if (!string.IsNullOrEmpty(step.When))
                    {
                        var whenCondition = context.SubstituteVariables(step.When);
                        if (!evaluator.Evaluate(whenCondition))
                            continue; // Skip this item (body not executed)
                    }

                    var result = await _executor.ExecuteStepsAsync(step.Do, context, cancellationToken, context.LoopDepth + 1);
                    executed++;

                    if (result.ShouldExit || result.ShouldReturn)
                    {
                        result.IterationCount = executed;
                        return result;
                    }

                    if (result.ShouldBreak)
                        break;

                    if (result.ShouldContinue)
                        continue;

                    if (!result.Success)
                    {
                        result.IterationCount = executed;
                        return result;
                    }
                }

                return new CommandResult { Success = true, IterationCount = executed };
            }
            finally
            {
                context.PopIterationFrame();
                foreach (var name in scopedNames)
                {
                    var (existed, value) = saved[name];
                    if (existed)
                        context.SetVariable(name, value);
                    else
                        context.RemoveVariable(name);
                }
            }
        }

        private List<string> ResolveCollection(string expr, ScriptContext context)
        {
            return ValueResolver.ResolveCollectionExpression(expr, context);
        }

        private static List<KeyValuePair<string, string>> ResolveDictEntries(string expr, ScriptContext context)
        {
            var obj = JsonUtilities.GetJsonObject(expr, context);
            var entries = new List<KeyValuePair<string, string>>(obj.Count);
            foreach (var kvp in obj)
                entries.Add(new KeyValuePair<string, string>(kvp.Key, JsonUtilities.JsonNodeToStringValue(kvp.Value)));
            return entries;
        }
    }
}
