namespace SSH_Helper.Models
{
    /// <summary>
    /// Represents connection details for a single SSH host.
    /// </summary>
    public class HostConnection
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new();

        /// <summary>
        /// Parses a host string in the format "ip:port" or "ip".
        /// </summary>
        public static HostConnection Parse(string hostWithPort)
        {
            var result = new HostConnection();

            if (string.IsNullOrWhiteSpace(hostWithPort))
                return result;

            var parts = hostWithPort.Split(':');
            result.IpAddress = parts[0];

            if (parts.Length > 1 && int.TryParse(parts[1], out int port) && port > 0 && port <= 65535)
            {
                result.Port = port;
            }

            return result;
        }

        /// <summary>
        /// Validates the IP address format.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(IpAddress))
                return false;

            var octets = IpAddress.Split('.');
            if (octets.Length != 4)
                return false;

            foreach (var octet in octets)
            {
                if (!int.TryParse(octet, out int value) || value < 0 || value > 255)
                    return false;
            }

            return Port > 0 && Port <= 65535;
        }

        public override string ToString() => Port == 22 ? IpAddress : $"{IpAddress}:{Port}";
    }
}
