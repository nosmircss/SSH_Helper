using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Invokes a local or imported subroutine in an isolated child scope.
    /// </summary>
    public sealed class CallCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;

        public CallCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Call == null)
                return CommandResult.Fail("Call command has no configuration");

            if (context.SubroutineRegistry == null)
                return CommandResult.Fail("No subroutine registry is available for this script");

            if (!context.SubroutineRegistry.TryResolve(step.Call.Subroutine, context.CurrentSubroutine, out var definition) ||
                definition == null)
            {
                return CommandResult.Fail($"Unknown subroutine '{step.Call.Subroutine}'");
            }

            var resolvedArgs = new Dictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var arg in step.Call.Args)
            {
                resolvedArgs[arg.Key] = ValueResolver.ResolveExpressionValue(arg.Value, context);
            }

            var result = await _executor.ExecuteSubroutineAsync(
                definition,
                context,
                resolvedArgs,
                step.Call.Out,
                cancellationToken);

            if (!result.Success &&
                string.Equals(step.OnError, "continue", System.StringComparison.OrdinalIgnoreCase))
            {
                return CommandResult.Suppressed(result.Message ?? $"Call to '{definition.QualifiedName}' failed");
            }

            return result;
        }
    }
}
