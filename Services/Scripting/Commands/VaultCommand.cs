using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
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

        private static Dictionary<string, object?> ResolveDataDictionary(
            Dictionary<string, string> source, ScriptContext context)
        {
            var resolved = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kvp in source)
            {
                var key = context.SubstituteVariables(kvp.Key);
                var substitutedValue = context.SubstituteVariables(kvp.Value);
                resolved[key] = TryParseStructuredJson(substitutedValue);
            }
            return resolved;
        }

        private static object? TryParseStructuredJson(string substitutedValue)
        {
            var trimmed = substitutedValue.Trim();

            if (TryParseJsonObjectOrArray(trimmed, out var parsed))
                return parsed;

            // Recovery path for previously stringified payloads like:
            // [{\"customer_id\":\"MC000012\",\"r7_api_key\":\"...\"}]
            var unescaped = trimmed
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);

            if (!string.Equals(unescaped, trimmed, StringComparison.Ordinal) &&
                TryParseJsonObjectOrArray(unescaped, out parsed))
            {
                return parsed;
            }

            return substitutedValue;
        }

        private static bool TryParseJsonObjectOrArray(string text, out JsonNode? node)
        {
            node = null;
            if (!LooksLikeJsonObjectOrArray(text))
                return false;

            try
            {
                var parsed = JsonNode.Parse(text);
                if (parsed is JsonObject || parsed is JsonArray)
                {
                    node = parsed;
                    return true;
                }
            }
            catch
            {
                // Preserve previous behavior when input is not valid JSON.
            }

            return false;
        }

        private static bool LooksLikeJsonObjectOrArray(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            return (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal)) ||
                   (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal));
        }

        private static CommandResult ApplyOnError(ScriptStep step, string message)
            => CommandResult.ApplyOnError(step, message);
    }
}
