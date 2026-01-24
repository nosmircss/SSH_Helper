using System.Text;
using System.Text.RegularExpressions;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Detects and manages SSH shell prompts for command output parsing.
    /// </summary>
    public class PromptDetector
    {
        private static readonly char[] PromptTerminators = { '#', '>', '$', '%', '\u2192', '\u276F', '\u279C' };

        /// <summary>
        /// Builds a regex pattern to match the given prompt, allowing for mode changes
        /// (e.g., "hostname (config)#" vs "hostname#").
        /// </summary>
        public static Regex BuildPromptRegex(string promptLiteral)
        {
            // Generic fallback if nothing known yet
            if (string.IsNullOrWhiteSpace(promptLiteral))
                return CreateFallbackRegex();

            // Trim trailing whitespace and ANSI
            var trimmed = Regex.Replace(promptLiteral, @"\s+$", "");
            trimmed = Regex.Replace(trimmed, @"\x1B\[[0-9;]*[A-Za-z]", "");

            // Ensure it ends with a typical prompt terminator
            if (!Regex.IsMatch(trimmed, @"[#>$%\u2192\u276F\u279C]\s*$"))
                return CreateFallbackRegex();

            // Extract base hostname and terminator
            char terminator = trimmed[^1];
            string body = trimmed[..^1].TrimEnd();

            // Split off any mode/context portion (parenthetical)
            // e.g., "MSD903-DFWB (setting)" => baseHost = "MSD903-DFWB"
            string baseHost;
            int parenIdx = body.IndexOf('(');
            if (parenIdx > 0)
                baseHost = body[..parenIdx].TrimEnd();
            else
                baseHost = body;

            if (string.IsNullOrWhiteSpace(baseHost))
                baseHost = body;

            // If baseHost doesn't contain alphanumeric characters, it's likely a
            // status indicator (e.g., ○, ●) from a starship/oh-my-zsh style prompt,
            // not a real hostname. Use a terminator-only pattern.
            if (!Regex.IsMatch(baseHost, @"[a-zA-Z0-9]"))
            {
                string terminatorEsc = Regex.Escape(terminator.ToString());
                // Use (?:^|[\r\n]) instead of bare ^ so pattern works both with
                // RegexOptions.Multiline (direct IsMatch) and without it (Rebex ScriptEvent)
                string fallbackPattern = $@"(?:^|[\r\n]).*{terminatorEsc}\s*$";
                return new Regex(fallbackPattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            }

            string baseEsc = Regex.Escape(baseHost);

            // Use (?:^|[\r\n]) for Rebex ScriptEvent compatibility (pattern is used
            // via .ToString() in contexts without RegexOptions.Multiline)
            string pattern = $"(?:^|[\\r\\n]){baseEsc}(?:\\s*\\([^)]+\\))?\\s*[{Regex.Escape(terminator.ToString())}#>$%]\\s*$";

            return new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// Attempts to detect a prompt from the buffer by finding the last line
        /// that looks like a shell prompt.
        /// </summary>
        public static bool TryDetectPrompt(string buffer, out string prompt)
        {
            prompt = string.Empty;
            if (string.IsNullOrEmpty(buffer))
                return false;

            var lines = buffer.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var candidate = lines[i].TrimEnd();
                if (IsLikelyPrompt(candidate))
                {
                    prompt = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Attempts to detect a prompt from the tail of the buffer (optimized for large buffers).
        /// </summary>
        public static bool TryDetectPromptFromTail(string buffer, out string prompt, int lookbackChars = 4096)
        {
            prompt = string.Empty;
            if (string.IsNullOrEmpty(buffer))
                return false;

            int lookback = Math.Min(lookbackChars, buffer.Length);
            string tail = buffer.Substring(buffer.Length - lookback);

            return TryDetectPrompt(tail, out prompt);
        }

        /// <summary>
        /// Checks if the buffer ends with a known prompt pattern.
        /// </summary>
        public static bool BufferEndsWithPrompt(StringBuilder sb, Regex promptRegex)
        {
            if (sb.Length == 0)
                return false;

            int lookback = Math.Min(4096, sb.Length);
            string tail = sb.ToString(sb.Length - lookback, lookback);

            var lines = tail.Split(new[] { "\r\n" }, StringSplitOptions.None);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Length == 0)
                    continue;
                if (promptRegex.IsMatch(line))
                    return true;
                break; // Only test the last non-empty line
            }
            return false;
        }

        /// <summary>
        /// Detects if the buffer ends with a different prompt than the current one
        /// (e.g., when entering/exiting configuration mode).
        /// </summary>
        public static bool TryDetectDifferentPrompt(StringBuilder sb, Regex currentPromptRegex, out string newPrompt)
        {
            newPrompt = string.Empty;
            if (sb.Length == 0)
                return false;

            int lookback = Math.Min(4096, sb.Length);
            string tail = sb.ToString(sb.Length - lookback, lookback);

            var lines = tail.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].TrimEnd();
                if (IsLikelyPrompt(line) && !currentPromptRegex.IsMatch(line))
                {
                    newPrompt = line;
                    return true;
                }
                if (line.Length > 0)
                    break;
            }
            return false;
        }

        /// <summary>
        /// Determines if a line looks like a shell prompt based on common conventions.
        /// Rejects lines that appear to be natural language or warning text.
        /// </summary>
        public static bool IsLikelyPrompt(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            line = line.TrimEnd();
            if (line.Length == 0)
                return false;

            char last = line[^1];
            if (!PromptTerminators.Contains(last))
                return false;

            // Real prompts are short — reject very long lines (likely wrapped text/warnings)
            if (line.Length > 80)
                return false;

            // Lines containing paired quotes are likely instructional text, not prompts
            // e.g., "Please run 'execute disk list' and then 'execute disk scan <ref#"
            if (line.Count(c => c == '\'') >= 2 || line.Count(c => c == '"') >= 2)
                return false;

            return true;
        }

        private static Regex CreateFallbackRegex()
        {
            // Use (?:^|[\r\n]) instead of bare ^ so pattern works both with
            // RegexOptions.Multiline (direct IsMatch) and without it (Rebex ScriptEvent)
            return new Regex(@"(?:^|[\r\n]).*(?:[#>$%\u2192\u276F\u279C])[ \t]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }
    }
}
