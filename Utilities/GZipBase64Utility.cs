using System.IO.Compression;
using System.Text;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Shared GZip + Base64 compression/decompression utility.
    /// </summary>
    public static class GZipBase64Utility
    {
        /// <summary>
        /// Compresses a UTF-8 string using GZip and returns a Base64-encoded result.
        /// </summary>
        public static string CompressAndEncode(string text)
        {
            byte[] raw = Encoding.UTF8.GetBytes(text);
            using var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(raw, 0, raw.Length);
            }
            return Convert.ToBase64String(ms.ToArray());
        }

        /// <summary>
        /// Decompresses a Base64-encoded GZip string back to UTF-8 text.
        /// Optionally strips a prefix before decoding (e.g., "gz64:").
        /// </summary>
        public static string Decompress(string encoded, string? prefixToStrip = null)
        {
            if (prefixToStrip != null && encoded.StartsWith(prefixToStrip, StringComparison.Ordinal))
            {
                encoded = encoded[prefixToStrip.Length..];
            }

            byte[] compressed = Convert.FromBase64String(encoded);
            using var input = new MemoryStream(compressed);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}
