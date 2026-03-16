using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Iterates over a collection or lines in a variable.
    /// Format: "foreach: item in collection" or "foreach: line in output"
    /// Optional: "when: condition" to filter items
    /// </summary>
    public class ForeachCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;
        private static readonly Regex ForeachPattern = new(@"^(\w+)\s+in\s+(.+)$", RegexOptions.IgnoreCase);

        public ForeachCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(step.Foreach))
                return CommandResult.Fail("Foreach command has no iterator expression");

            if (step.Do == null || step.Do.Count == 0)
                return CommandResult.Fail("Foreach requires 'do' block");

            // Parse "item in collection"
            var match = ForeachPattern.Match(step.Foreach.Trim());
            if (!match.Success)
                return CommandResult.Fail($"Invalid foreach syntax: '{step.Foreach}'. Expected 'item in collection'");

            var itemVarName = match.Groups[1].Value;
            var collectionExpr = match.Groups[2].Value.Trim();

            // Resolve the collection
            var items = ResolveCollection(collectionExpr, context);

            context.EmitOutput($"Foreach: iterating {items.Count} item(s)", ScriptOutputType.Debug);

            // Create evaluator for 'when' filter
            var evaluator = new ExpressionEvaluator(context);

            int index = 0;
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Set the iterator variable
                context.SetVariable(itemVarName, item);
                context.SetVariable($"{itemVarName}_index", index);

                // Apply 'when' filter if present
                if (!string.IsNullOrEmpty(step.When))
                {
                    var whenCondition = context.SubstituteVariables(step.When);
                    if (!evaluator.Evaluate(whenCondition))
                    {
                        index++;
                        continue; // Skip this item
                    }
                }

                // Execute the 'do' block
                var result = await _executor.ExecuteStepsAsync(step.Do, context, cancellationToken, context.LoopDepth + 1);

                // Handle control flow
                if (result.ShouldExit)
                    return result;

                if (result.ShouldReturn)
                    return result;

                if (result.ShouldBreak)
                    break;

                if (result.ShouldContinue)
                {
                    index++;
                    continue;
                }

                if (!result.Success)
                    return result;

                index++;
            }

            return CommandResult.Ok();
        }

        private List<string> ResolveCollection(string expr, ScriptContext context)
        {
            return ValueResolver.ResolveCollectionExpression(expr, context);
        }
    }
}
