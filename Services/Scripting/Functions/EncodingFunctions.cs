using System;
using System.Security.Cryptography;
using System.Text;

namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// Encoding and hashing functions for the scripting language.
    /// </summary>
    public class EncodingFunctions : IFunctionCategory
    {
        public void Register(FunctionRegistry registry)
        {
            registry.Register("base64_encode", Base64Encode);
            registry.Register("base64_decode", Base64Decode);
            registry.Register("url_encode", UrlEncode);
            registry.Register("url_decode", UrlDecode);
            registry.Register("hash", Hash);
            registry.Register("hex_encode", HexEncode);
            registry.Register("hex_decode", HexDecode);
        }

        private static object? Base64Encode(string argsString, ScriptContext context)
        {
            var value = Resolve(argsString, context);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static object? Base64Decode(string argsString, ScriptContext context)
        {
            var value = Resolve(argsString, context);
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static object? UrlEncode(string argsString, ScriptContext context)
        {
            var value = Resolve(argsString, context);
            return Uri.EscapeDataString(value);
        }

        private static object? UrlDecode(string argsString, ScriptContext context)
        {
            var value = Resolve(argsString, context);
            return Uri.UnescapeDataString(value);
        }

        private static object? Hash(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count == 0) return null;

            var value = Resolve(args[0], context);
            var algorithm = args.Count >= 2
                ? JsonUtilities.ResolveJsonValue(args[1], context)?.ToString()?.ToUpperInvariant() ?? "SHA256"
                : "SHA256";

            byte[] hashBytes;
            using (var hasher = CreateHashAlgorithm(algorithm))
            {
                if (hasher == null) return null;
                hashBytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(value));
            }

            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private static object? HexEncode(string argsString, ScriptContext context)
        {
            var value = Resolve(argsString, context);
            var bytes = Encoding.UTF8.GetBytes(value);
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static object? HexDecode(string argsString, ScriptContext context)
        {
            var value = Resolve(argsString, context);
            try
            {
                var bytes = new byte[value.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        // --- Helpers ---

        private static string Resolve(string argsString, ScriptContext context)
        {
            return JsonUtilities.ResolveJsonValue(argsString.Trim(), context)?.ToString() ?? string.Empty;
        }

        private static HashAlgorithm? CreateHashAlgorithm(string name)
        {
            return name switch
            {
                "MD5" => MD5.Create(),
                "SHA1" => SHA1.Create(),
                "SHA256" => SHA256.Create(),
                "SHA384" => SHA384.Create(),
                "SHA512" => SHA512.Create(),
                _ => SHA256.Create()
            };
        }
    }
}
