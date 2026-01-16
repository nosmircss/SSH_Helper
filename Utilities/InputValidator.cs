namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Provides input validation for the application.
    /// </summary>
    public static class InputValidator
    {
        /// <summary>
        /// Validates an IP address string (with optional port).
        /// </summary>
        /// <param name="ipWithPort">IP address in format "x.x.x.x" or "x.x.x.x:port"</param>
        /// <returns>True if valid</returns>
        public static bool IsValidIpAddress(string ipWithPort)
        {
            if (string.IsNullOrWhiteSpace(ipWithPort))
                return false;

            string[] parts = ipWithPort.Split(':');
            string ipAddress = parts[0];

            // Validate IP octets
            string[] octets = ipAddress.Split('.');
            if (octets.Length != 4)
                return false;

            foreach (string octet in octets)
            {
                if (!int.TryParse(octet, out int value) || value < 0 || value > 255)
                    return false;
            }

            // Validate port if specified
            if (parts.Length > 1)
            {
                if (!int.TryParse(parts[1], out int port) || port <= 0 || port > 65535)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Validates a port number.
        /// </summary>
        public static bool IsValidPort(int port)
        {
            return port > 0 && port <= 65535;
        }

        /// <summary>
        /// Validates a timeout value in seconds.
        /// </summary>
        public static bool IsValidTimeout(int timeoutSeconds)
        {
            return timeoutSeconds > 0 && timeoutSeconds <= 3600; // Max 1 hour
        }

        /// <summary>
        /// Validates a delay value in milliseconds.
        /// </summary>
        public static bool IsValidDelay(int delayMs)
        {
            return delayMs >= 0 && delayMs <= 60000; // Max 1 minute
        }

        /// <summary>
        /// Sanitizes a column name for use in a DataGridView.
        /// </summary>
        public static string SanitizeColumnName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Column";

            // Replace spaces with underscores
            return name.Trim().Replace(" ", "_");
        }

        /// <summary>
        /// Validates that a string is not empty or whitespace.
        /// </summary>
        public static bool IsNotEmpty(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Parses an integer from a string, returning a default value if parsing fails.
        /// </summary>
        public static int ParseIntOrDefault(string? text, int defaultValue)
        {
            if (int.TryParse(text, out int result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// Ensures a value is within a range.
        /// </summary>
        public static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
