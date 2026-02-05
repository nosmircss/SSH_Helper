using System;
using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Parsers
{
    /// <summary>
    /// Factory for creating configuration parsers based on format name.
    /// </summary>
    public static class ParserFactory
    {
        private static readonly Dictionary<string, Func<IConfigParser>> _parsers =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "fortigate", () => new FortiGateParser() },
                { "fortios", () => new FortiGateParser() },
            };

        /// <summary>
        /// Gets a parser for the specified format.
        /// </summary>
        /// <param name="format">The format identifier (e.g., "fortigate").</param>
        /// <returns>The parser instance.</returns>
        /// <exception cref="ArgumentException">If the format is not supported.</exception>
        public static IConfigParser GetParser(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentException("Format cannot be empty", nameof(format));

            if (_parsers.TryGetValue(format, out var factory))
                return factory();

            var available = string.Join(", ", _parsers.Keys);
            throw new ArgumentException(
                $"Unsupported configuration format: '{format}'. Available formats: {available}",
                nameof(format));
        }

        /// <summary>
        /// Checks if a format is supported.
        /// </summary>
        public static bool IsFormatSupported(string format)
        {
            return !string.IsNullOrWhiteSpace(format) && _parsers.ContainsKey(format);
        }

        /// <summary>
        /// Gets all available format names.
        /// </summary>
        public static IEnumerable<string> GetAvailableFormats()
        {
            return _parsers.Keys;
        }
    }
}
