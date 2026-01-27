namespace SSH_Helper.Services
{
    /// <summary>
    /// Builds credential target names for Windows Credential Manager.
    /// </summary>
    public static class CredentialTargets
    {
        private const string Prefix = "SSH_Helper";

        public static string DefaultPasswordTarget => $"{Prefix}:default";

        public static string HostPasswordTarget(string host, string username)
        {
            var safeHost = (host ?? string.Empty).Trim();
            var safeUser = (username ?? string.Empty).Trim();
            return $"{Prefix}:host:{safeHost}|user:{safeUser}";
        }
    }
}
