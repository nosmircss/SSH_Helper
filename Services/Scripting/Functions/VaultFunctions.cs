using System;
using System.Collections.Generic;
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Services.Scripting.Functions
{
    public class VaultFunctions : IFunctionCategory
    {
        public void Register(FunctionRegistry registry)
        {
            registry.Register("vault", VaultGet);
            registry.Register("vault_list", VaultList);
            registry.Register("vault_clear_cache", VaultClearCache);
        }

        private static object? VaultGet(string argsString, ScriptContext context)
        {
            if (context.VaultService == null)
                return null;

            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2)
                return null;

            var path = Resolve(args[0], context);
            var key = Resolve(args[1], context);
            var profile = args.Count >= 3
                ? Resolve(args[2], context)
                : context.VaultService.ResolveDefaultProfileName(context.EnvironmentVaultProfile);

            if (string.IsNullOrEmpty(profile))
                return null;

            try
            {
                return context.VaultService.ReadSecretAsync(profile, path, key).GetAwaiter().GetResult();
            }
            catch (VaultException)
            {
                return null;
            }
        }

        private static object? VaultList(string argsString, ScriptContext context)
        {
            if (context.VaultService == null)
                return null;

            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 1)
                return null;

            var prefix = Resolve(args[0], context);
            var profile = args.Count >= 2
                ? Resolve(args[1], context)
                : context.VaultService.ResolveDefaultProfileName(context.EnvironmentVaultProfile);

            if (string.IsNullOrEmpty(profile))
                return null;

            try
            {
                return context.VaultService.ListSecretsAsync(profile, prefix).GetAwaiter().GetResult();
            }
            catch (VaultException)
            {
                return null;
            }
        }

        private static object? VaultClearCache(string argsString, ScriptContext context)
        {
            if (context.VaultService == null)
                return false;

            context.VaultService.ClearCache();
            return true;
        }

        private static string Resolve(string expr, ScriptContext context)
        {
            var resolved = JsonUtilities.ResolveJsonValue(expr, context);
            return resolved?.ToString() ?? string.Empty;
        }
    }
}
