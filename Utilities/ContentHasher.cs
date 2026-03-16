using System.Security.Cryptography;
using System.Text;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Produces deterministic SHA256 content hashes for scheduler target snapshots.
    /// </summary>
    public static class ContentHasher
    {
        /// <summary>
        /// Computes an uppercase hexadecimal SHA256 hash of the given content.
        /// Returns empty string for null or empty input.
        /// </summary>
        public static string ComputeHash(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
