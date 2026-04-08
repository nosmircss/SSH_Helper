namespace SSH_Helper.Services.Vault
{
    internal sealed record VaultOidcCallbackBinding(
        string ListenerHost,
        string AuthorityHost,
        int Port,
        string Path,
        string RedirectUri,
        string ListenerPrefix);

    internal static class VaultOidcCallbackSettings
    {
        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 8250;
        private const string DefaultPath = "/oidc/callback";

        public static bool TryCreate(
            string? callbackHost,
            int callbackPort,
            string? callbackPath,
            string? profileName,
            out VaultOidcCallbackBinding? binding,
            out string? error)
        {
            try
            {
                binding = Create(callbackHost, callbackPort, callbackPath, profileName);
                error = null;
                return true;
            }
            catch (VaultException ex)
            {
                binding = null;
                error = ex.Message;
                return false;
            }
        }

        public static VaultOidcCallbackBinding Create(
            string? callbackHost,
            int callbackPort,
            string? callbackPath,
            string? profileName = null)
        {
            var listenerHost = NormalizeLoopbackHost(callbackHost, profileName);
            var port = callbackPort <= 0 ? DefaultPort : callbackPort;
            if (port < 1 || port > 65535)
                throw new VaultException(BuildProfileMessage(profileName, "has an invalid OIDC callback port."));

            var path = NormalizeCallbackPath(callbackPath);
            var authorityHost = listenerHost.Contains(':', StringComparison.Ordinal)
                ? $"[{listenerHost}]"
                : listenerHost;

            return new VaultOidcCallbackBinding(
                listenerHost,
                authorityHost,
                port,
                path,
                $"http://{authorityHost}:{port}{path}",
                $"http://{authorityHost}:{port}/");
        }

        public static string NormalizeCallbackPath(string? callbackPath)
        {
            var normalized = string.IsNullOrWhiteSpace(callbackPath)
                ? DefaultPath
                : callbackPath.Trim();

            if (!normalized.StartsWith('/'))
                normalized = "/" + normalized;

            return normalized;
        }

        private static string NormalizeLoopbackHost(string? callbackHost, string? profileName)
        {
            var normalized = string.IsNullOrWhiteSpace(callbackHost)
                ? DefaultHost
                : callbackHost.Trim();

            return normalized.ToLowerInvariant() switch
            {
                "127.0.0.1" => "127.0.0.1",
                "localhost" => "localhost",
                "::1" => "::1",
                "[::1]" => "::1",
                _ => throw new VaultException(
                    BuildProfileMessage(
                        profileName,
                        $"has an invalid OIDC callback host '{normalized}'. Only loopback hosts are allowed: 127.0.0.1, localhost, ::1."))
            };
        }

        private static string BuildProfileMessage(string? profileName, string detail)
        {
            return string.IsNullOrWhiteSpace(profileName)
                ? $"Vault OIDC login failed: {detail}"
                : $"Vault profile '{profileName}' {detail}";
        }
    }
}
