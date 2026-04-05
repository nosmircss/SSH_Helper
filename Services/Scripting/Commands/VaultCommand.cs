using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Services.Scripting.Commands
{
    public class VaultCommand : IScriptCommand
    {
        public async Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            var options = step.Vault;
            if (options == null)
                return CommandResult.Fail("Vault command has no options");

            if (context.VaultService == null)
                return ApplyOnError(step, "Vault is not configured");

            var path = context.SubstituteVariables(options.Path ?? "");
            if (string.IsNullOrWhiteSpace(path))
                return ApplyOnError(step, "Vault requires 'path'");

            var profileName = ResolveProfile(options.Profile, context);
            if (string.IsNullOrEmpty(profileName))
                return ApplyOnError(step, "No Vault profile available");

            try
            {
                if (options.Write != null && options.Write.Count > 0)
                    return await ExecuteWriteAsync(options, path, profileName, context, cancellationToken);

                if (options.Patch != null && options.Patch.Count > 0)
                    return await ExecutePatchAsync(options, path, profileName, context, cancellationToken);

                if (options.Keys != null && options.Keys.Count > 0)
                    return await ExecuteReadMultipleAsync(options, path, profileName, context, cancellationToken);

                if (!string.IsNullOrEmpty(options.Key))
                    return await ExecuteReadSingleAsync(options, path, profileName, context, cancellationToken);

                return ApplyOnError(step, "Vault step requires one of: key+into, keys, write, or patch");
            }
            catch (VaultException ex)
            {
                context.EmitOutput($"[vault] error {profileName}@{path} -> {ex.Message}", ScriptOutputType.Debug);

                if (step.IsOnErrorContinue || string.Equals(options.OnError, "continue", StringComparison.OrdinalIgnoreCase))
                {
                    context.SetVariable("_last_error", ex.Message);
                    return CommandResult.Suppressed(ex.Message);
                }

                return CommandResult.Fail(ex.Message);
            }
        }

        private async Task<CommandResult> ExecuteReadSingleAsync(
            VaultStepOptions options, string path, string profileName,
            ScriptContext context, CancellationToken ct)
        {
            var key = context.SubstituteVariables(options.Key!);
            var into = context.SubstituteVariables(options.Into ?? "");

            if (string.IsNullOrWhiteSpace(into))
                return CommandResult.Fail("Vault read requires 'into' when using 'key'");

            var value = await context.VaultService!.ReadSecretAsync(profileName, path, key, options.Version, ct);
            context.SetVariable(into, value ?? "");

            context.EmitOutput($"[vault] read {profileName}@{path}#{key} -> success", ScriptOutputType.Debug);
            return CommandResult.Ok();
        }

        private async Task<CommandResult> ExecuteReadMultipleAsync(
            VaultStepOptions options, string path, string profileName,
            ScriptContext context, CancellationToken ct)
        {
            var resolvedKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in options.Keys!)
            {
                resolvedKeys[context.SubstituteVariables(kvp.Key)] = context.SubstituteVariables(kvp.Value);
            }

            var secretKeys = new List<string>(resolvedKeys.Keys);
            var result = await context.VaultService!.ReadSecretKeysAsync(profileName, path, secretKeys, options.Version, ct);

            foreach (var kvp in resolvedKeys)
            {
                var secretKey = kvp.Key;
                var targetVariable = kvp.Value;
                if (result.TryGetValue(secretKey, out var value))
                    context.SetVariable(targetVariable, value ?? "");
            }

            context.EmitOutput($"[vault] read {profileName}@{path} keys=[{string.Join(",", secretKeys)}] -> success", ScriptOutputType.Debug);
            return CommandResult.Ok();
        }

        private async Task<CommandResult> ExecuteWriteAsync(
            VaultStepOptions options, string path, string profileName,
            ScriptContext context, CancellationToken ct)
        {
            var data = ResolveDataDictionary(options.Write!, context);
            await context.VaultService!.WriteSecretAsync(profileName, path, data, ct);

            context.EmitOutput($"[vault] write {profileName}@{path} -> success", ScriptOutputType.Debug);
            return CommandResult.Ok();
        }

        private async Task<CommandResult> ExecutePatchAsync(
            VaultStepOptions options, string path, string profileName,
            ScriptContext context, CancellationToken ct)
        {
            var data = ResolveDataDictionary(options.Patch!, context);
            await context.VaultService!.PatchSecretAsync(profileName, path, data, ct);

            context.EmitOutput($"[vault] patch {profileName}@{path} -> success", ScriptOutputType.Debug);
            return CommandResult.Ok();
        }

        private static string? ResolveProfile(string? explicitProfile, ScriptContext context)
        {
            if (!string.IsNullOrEmpty(explicitProfile))
                return context.SubstituteVariables(explicitProfile);

            return context.VaultService!.ResolveDefaultProfileName(context.EnvironmentVaultProfile);
        }

        private static Dictionary<string, string> ResolveDataDictionary(
            Dictionary<string, string> source, ScriptContext context)
        {
            var resolved = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in source)
            {
                resolved[context.SubstituteVariables(kvp.Key)] = context.SubstituteVariables(kvp.Value);
            }
            return resolved;
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
            => CommandResult.ApplyOnError(step, message);
    }
}
