using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// Network-address helper functions (IP / CIDR / URL) for the scripting language.
    /// All functions are pure and perform no I/O.
    /// </summary>
    public class NetworkFunctions : IFunctionCategory
    {
        public void Register(FunctionRegistry registry)
        {
            registry.Register("is_valid_ip", IsValidIp);
            registry.Register("ip_version", IpVersion);
            registry.Register("ip_in_cidr", IpInCidr);
            registry.Register("url_host", UrlHost);
            registry.Register("url_port", UrlPort);
        }

        private static object? IsValidIp(string argsString, ScriptContext context)
        {
            return IPAddress.TryParse(Resolve(argsString, context), out _);
        }

        private static object? IpVersion(string argsString, ScriptContext context)
        {
            if (!IPAddress.TryParse(Resolve(argsString, context), out var addr))
                return string.Empty;

            return addr.AddressFamily == AddressFamily.InterNetworkV6 ? 6 : 4;
        }

        private static object? IpInCidr(string argsString, ScriptContext context)
        {
            var args = JsonUtilities.SplitTopLevelCommas(argsString);
            if (args.Count < 2) return false;

            if (!IPAddress.TryParse(Resolve(args[0], context), out var address))
                return false;

            var cidr = Resolve(args[1], context);
            var slash = cidr.IndexOf('/');
            if (slash <= 0) return false;

            if (!IPAddress.TryParse(cidr.Substring(0, slash), out var baseAddress))
                return false;
            if (!int.TryParse(cidr.Substring(slash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var prefix))
                return false;

            var addrBytes = address.GetAddressBytes();
            var baseBytes = baseAddress.GetAddressBytes();
            if (addrBytes.Length != baseBytes.Length)
                return false; // mismatched address families
            if (prefix < 0 || prefix > addrBytes.Length * 8)
                return false;

            int fullBytes = prefix / 8;
            int remainingBits = prefix % 8;

            for (int i = 0; i < fullBytes; i++)
            {
                if (addrBytes[i] != baseBytes[i])
                    return false;
            }

            if (remainingBits > 0)
            {
                int mask = (0xFF << (8 - remainingBits)) & 0xFF;
                if ((addrBytes[fullBytes] & mask) != (baseBytes[fullBytes] & mask))
                    return false;
            }

            return true;
        }

        private static object? UrlHost(string argsString, ScriptContext context)
        {
            return Uri.TryCreate(Resolve(argsString, context), UriKind.Absolute, out var uri)
                ? uri.Host
                : string.Empty;
        }

        private static object? UrlPort(string argsString, ScriptContext context)
        {
            if (!Uri.TryCreate(Resolve(argsString, context), UriKind.Absolute, out var uri))
                return string.Empty;

            return uri.Port >= 0 ? uri.Port : (object)string.Empty;
        }

        private static string Resolve(string expr, ScriptContext context)
        {
            return JsonUtilities.ResolveJsonValue(expr.Trim(), context)?.ToString() ?? string.Empty;
        }
    }
}
