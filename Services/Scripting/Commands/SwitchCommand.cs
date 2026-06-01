using System;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Dispatches execution based on a value matching one of several cases.
    /// </summary>
    public class SwitchCommand : IScriptCommand
    {
        private readonly ScriptExecutor _executor;

        public SwitchCommand(ScriptExecutor executor)
        {
            _executor = executor;
        }

        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(step.Switch))
                return CommandResult.Fail("Switch command has no value expression");

            var resolvedValue = context.SubstituteVariables(step.Switch).Trim();
            context.EmitOutput($"Switch on '{step.Switch}' => '{resolvedValue}'", ScriptOutputType.Debug);

            string? branchTaken = null;

            if (step.Cases != null)
            {
                for (int i = 0; i < step.Cases.Count; i++)
                {
                    var switchCase = step.Cases[i];
                    var caseValue = context.SubstituteVariables(switchCase.Value).Trim();

                    bool matches;
                    if (caseValue.StartsWith("matches ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Regex match: "matches pattern"
                        var pattern = caseValue.Substring(8).Trim();
                        try
                        {
                            matches = System.Text.RegularExpressions.Regex.IsMatch(
                                resolvedValue, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        }
                        catch
                        {
                            matches = false;
                        }
                    }
                    else
                    {
                        // Case-insensitive equality
                        matches = string.Equals(resolvedValue, caseValue, StringComparison.OrdinalIgnoreCase);
                    }

                    if (matches)
                    {
                        context.EmitOutput($"Switch matched case '{switchCase.Value}'", ScriptOutputType.Debug);
                        branchTaken = $"cases/{i}/do";
                        if (switchCase.Do != null && switchCase.Do.Count > 0)
                        {
                            var result = await _executor.ExecuteStepsAsync(switchCase.Do, context, cancellationToken, context.LoopDepth);
                            result.BranchTaken = branchTaken;
                            return result;
                        }
                        return new CommandResult { Success = true, BranchTaken = branchTaken };
                    }
                }
            }

            // No match - execute default (stored in Else)
            if (step.Else != null && step.Else.Count > 0)
            {
                context.EmitOutput("Switch using default branch", ScriptOutputType.Debug);
                branchTaken = "default";
                var defaultResult = await _executor.ExecuteStepsAsync(step.Else, context, cancellationToken, context.LoopDepth);
                defaultResult.BranchTaken = branchTaken;
                return defaultResult;
            }

            context.EmitOutput("Switch: no matching case and no default", ScriptOutputType.Debug);
            return new CommandResult { Success = true, BranchTaken = branchTaken };
        }
    }
}
