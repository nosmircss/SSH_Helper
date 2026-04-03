using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Persists an environment variable update and refreshes the runtime variable value.
    /// </summary>
    public class UpdateEnvironmentCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.UpdateEnvironment == null)
                return Task.FromResult(CommandResult.Fail("UpdateEnvironment command has no options"));

            var options = step.UpdateEnvironment;

            if (string.IsNullOrWhiteSpace(options.Variable))
                return Task.FromResult(CommandResult.Fail("UpdateEnvironment requires 'variable' name"));

            if (options.Value == null)
                return Task.FromResult(CommandResult.Fail("UpdateEnvironment requires 'value'"));

            var variable = options.Variable.Trim();
            var resolvedValue = context.SubstituteVariables(options.Value);

            // Keep the current script context in sync with the persisted environment value.
            context.SetVariable(variable, resolvedValue);
            context.RequestEnvironmentUpdate(variable, resolvedValue);
            context.EmitOutput($"UpdateEnvironment: {variable} = '{ScriptingHelpers.FormatForDisplay(resolvedValue)}'", ScriptOutputType.Debug);

            return Task.FromResult(CommandResult.Ok());
        }
    }
}
