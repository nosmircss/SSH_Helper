using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Attaches a label to the current host's history entry.
    /// Supports both simple string form and options object with a replace flag.
    /// </summary>
    public class SetHistoryLabelCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            var operation = BuildOperation(step.SetHistoryLabel, context);
            context.HistoryLabelTouched = true;
            context.AddHistoryLabelOperation(operation);

            var historyLabel = context.HistoryLabel;
            var replacesAddress = context.HistoryLabelReplacesAddress;
            operation.ApplyTo(ref historyLabel, ref replacesAddress);
            context.HistoryLabel = historyLabel;
            context.HistoryLabelReplacesAddress = replacesAddress;

            return Task.FromResult(CommandResult.Ok());
        }

        private static HistoryLabelOperation BuildOperation(object? rawStepValue, ScriptContext context)
        {
            string? rawValue = null;
            string? rawMode = null;
            string? rawSeparator = null;
            bool? replaceAddress = null;

            switch (rawStepValue)
            {
                case string scalar:
                    rawValue = scalar;
                    break;

                case SetHistoryLabelOptions typed:
                    rawValue = typed.Value;
                    rawMode = typed.Mode;
                    rawSeparator = typed.Separator;
                    replaceAddress = typed.Replace;
                    break;

                case IDictionary<object, object> dict:
                    if (dict.TryGetValue("value", out var value))
                        rawValue = value?.ToString();
                    if (dict.TryGetValue("mode", out var mode))
                        rawMode = mode?.ToString();
                    if (dict.TryGetValue("separator", out var separator))
                        rawSeparator = separator?.ToString();
                    if (dict.TryGetValue("replace", out var replace) &&
                        bool.TryParse(replace?.ToString(), out var parsedReplace))
                    {
                        replaceAddress = parsedReplace;
                    }
                    break;
            }

            return new HistoryLabelOperation
            {
                Mode = HistoryLabelOperation.NormalizeMode(context.SubstituteVariables(rawMode ?? string.Empty)),
                Value = context.SubstituteVariables(rawValue ?? string.Empty),
                Separator = context.SubstituteVariables(rawSeparator ?? string.Empty),
                ReplaceAddress = replaceAddress
            };
        }
    }
}
