using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Updates a column in the host table with a value extracted from script variables.
    /// </summary>
    public class UpdateColumnCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.UpdateColumn == null)
                return Task.FromResult(CommandResult.Fail("UpdateColumn command has no options"));

            var options = step.UpdateColumn;

            if (string.IsNullOrEmpty(options.Column))
                return Task.FromResult(CommandResult.Fail("UpdateColumn requires 'column' name"));

            if (options.Value == null)
                return Task.FromResult(CommandResult.Fail("UpdateColumn requires 'value'"));

            // Substitute variables in the value
            var resolvedValue = context.SubstituteVariables(options.Value);

            // Request the column update
            context.RequestColumnUpdate(options.Column, resolvedValue);

            context.EmitOutput($"UpdateColumn: {options.Column} = '{TruncateForDisplay(resolvedValue)}'", ScriptOutputType.Debug);

            return Task.FromResult(CommandResult.Ok());
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
