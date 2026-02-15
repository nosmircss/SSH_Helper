using System;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Validates a condition and fails or warns depending on severity.
    /// </summary>
    public class AssertCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Assert == null || string.IsNullOrWhiteSpace(step.Assert.Condition))
                return Task.FromResult(CommandResult.Fail("Assert command has no condition"));

            var condition = context.SubstituteVariables(step.Assert.Condition);
            var evaluator = new ExpressionEvaluator(context);
            bool result;

            try
            {
                result = evaluator.Evaluate(condition);
            }
            catch (Exception ex)
            {
                var evalError = $"Assert condition evaluation failed: {ex.Message}";
                context.EmitOutput(evalError, ScriptOutputType.Error);
                return Task.FromResult(CommandResult.Fail(evalError));
            }

            if (result)
            {
                context.EmitOutput($"Assert passed: {step.Assert.Condition}", ScriptOutputType.Debug);
                return Task.FromResult(CommandResult.Ok());
            }

            // Assertion failed - build message
            var message = !string.IsNullOrWhiteSpace(step.Assert.Message)
                ? context.SubstituteVariables(step.Assert.Message)
                : $"Assertion failed: {step.Assert.Condition}";

            var isWarning = string.Equals(step.Assert.Severity, "warning", StringComparison.OrdinalIgnoreCase);

            if (isWarning)
            {
                context.EmitOutput($"[WARNING] {message}", ScriptOutputType.Warning);
                return Task.FromResult(CommandResult.Ok());
            }

            context.EmitOutput($"[ASSERT FAILED] {message}", ScriptOutputType.Error);
            return Task.FromResult(CommandResult.Fail(message));
        }
    }
}
