using SSH_Helper.Utilities;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Builds credential target names for Windows Credential Manager.
    /// </summary>
    public static class CredentialTargets
    {
        private const string StandardPrefix = "SSH_Helper";
        private const string PortablePrefix = "SSH_Helper_Portable";

        public static string DefaultPasswordTarget => BuildDefaultPasswordTarget(AppDataPaths.IsPortableBuild);

        public static string HostPasswordTarget(string host, string username)
        {
            return BuildHostPasswordTarget(AppDataPaths.IsPortableBuild, host, username);
        }

        public static string JobPasswordTarget(string jobId)
        {
            return BuildJobPasswordTarget(AppDataPaths.IsPortableBuild, jobId);
        }

        internal static string BuildDefaultPasswordTarget(bool portableBuild)
        {
            var prefix = GetPrefix(portableBuild);
            return $"{prefix}:default";
        }

        internal static string BuildHostPasswordTarget(bool portableBuild, string host, string username)
        {
            var prefix = GetPrefix(portableBuild);
            var safeHost = (host ?? string.Empty).Trim();
            var safeUser = (username ?? string.Empty).Trim();
            return $"{prefix}:host:{safeHost}|user:{safeUser}";
        }

        internal static string BuildJobPasswordTarget(bool portableBuild, string jobId)
        {
            var prefix = GetPrefix(portableBuild);
            var safeId = (jobId ?? string.Empty).Trim();
            return $"{prefix}:job:{safeId}";
        }

        private static string GetPrefix(bool portableBuild)
        {
            return portableBuild ? PortablePrefix : StandardPrefix;
        }
    }
}
