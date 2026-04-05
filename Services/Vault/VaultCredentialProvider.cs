using System.Diagnostics;
using SSH_Helper.Services.Vault;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Adapts VaultService to ICredentialProvider for SSH_Helper's credential resolution pipeline.
    /// Treats the provider target as a vault_path string: [profile@]path[#usernameKey,passwordKey].
    /// </summary>
    public sealed class VaultCredentialProvider : ICredentialProvider
    {
        private readonly VaultService _vaultService;

        public VaultCredentialProvider(VaultService vaultService)
        {
            _vaultService = vaultService ?? throw new ArgumentNullException(nameof(vaultService));
        }

        public bool IsAvailable => _vaultService.IsEnabled;

        public bool TryGetPassword(string target, out string username, out string password)
        {
            username = string.Empty;
            password = string.Empty;

            if (!IsAvailable || string.IsNullOrWhiteSpace(target))
                return false;

            try
            {
                ParseVaultPath(target, out var profile, out var path, out var usernameKey, out var passwordKey);

                var resolvedProfile = profile ?? _vaultService.ResolveDefaultProfileName();
                if (string.IsNullOrEmpty(resolvedProfile))
                    return false;

                var keys = new[] { usernameKey, passwordKey };
                var result = _vaultService.ReadSecretKeysAsync(resolvedProfile, path, keys)
                    .GetAwaiter().GetResult();

                result.TryGetValue(usernameKey, out var u);
                result.TryGetValue(passwordKey, out var p);

                username = u ?? string.Empty;
                password = p ?? string.Empty;

                return !string.IsNullOrEmpty(password);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VaultCredentialProvider: failed to resolve credentials from '{target}': {ex.Message}");
                return false;
            }
        }

        public bool SavePassword(string target, string username, string password, string? comment = null) => false;

        public bool DeletePassword(string target) => false;

        /// <summary>
        /// Parses a vault_path string of the form [profile@]path[#usernameKey,passwordKey].
        /// </summary>
        /// <param name="vaultPath">The raw vault path string.</param>
        /// <param name="profile">The profile name, or null if not specified.</param>
        /// <param name="path">The secret path within the KV store.</param>
        /// <param name="usernameKey">The key name for the username field. Default: "username".</param>
        /// <param name="passwordKey">The key name for the password field. Default: "password".</param>
        public static void ParseVaultPath(
            string vaultPath,
            out string? profile,
            out string path,
            out string usernameKey,
            out string passwordKey)
        {
            profile = null;
            usernameKey = "username";
            passwordKey = "password";

            // Strip custom keys suffix: #user_field,pass_field
            var hashIdx = vaultPath.IndexOf('#');
            string pathPart;
            if (hashIdx >= 0)
            {
                var keysPart = vaultPath[(hashIdx + 1)..];
                pathPart = vaultPath[..hashIdx];

                var comma = keysPart.IndexOf(',');
                if (comma >= 0)
                {
                    usernameKey = keysPart[..comma];
                    passwordKey = keysPart[(comma + 1)..];
                }
                else
                {
                    usernameKey = keysPart;
                }
            }
            else
            {
                pathPart = vaultPath;
            }

            // Split profile from path: profile@path (@ before the # we already removed)
            var atIdx = pathPart.IndexOf('@');
            if (atIdx >= 0)
            {
                profile = pathPart[..atIdx];
                path = pathPart[(atIdx + 1)..];
            }
            else
            {
                path = pathPart;
            }
        }
    }
}
