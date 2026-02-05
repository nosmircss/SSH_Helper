using System.Collections.Generic;

namespace SSH_Helper.Services.Scripting.Parsers
{
    /// <summary>
    /// Interface for configuration parsers that transform raw device config text into structured data.
    /// </summary>
    public interface IConfigParser
    {
        /// <summary>
        /// The format identifier for this parser (e.g., "fortigate", "cisco-ios").
        /// </summary>
        string FormatName { get; }

        /// <summary>
        /// Parses raw configuration text into a structured dictionary.
        /// </summary>
        /// <param name="configText">The raw configuration text from the device.</param>
        /// <returns>A nested dictionary representing the parsed configuration.</returns>
        Dictionary<string, object> Parse(string configText);

        /// <summary>
        /// Parses raw configuration text, filtering to specific sections.
        /// </summary>
        /// <param name="configText">The raw configuration text from the device.</param>
        /// <param name="sections">Optional list of section paths to parse (e.g., "system interface").</param>
        /// <returns>A nested dictionary representing the parsed configuration.</returns>
        Dictionary<string, object> Parse(string configText, IEnumerable<string>? sections);
    }
}
