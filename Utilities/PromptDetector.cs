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

        // Arrow-style terminators that do not require alphanumeric content before them.
        private static readonly HashSet<char> ArrowStyleTerminators = new() { '\u2192', '\u276F', '\u279C' };

        // Pre-compiled regex patterns for hot path optimization
        private static readonly Regex TrailingWhitespaceRegex = new(@"\s+$", RegexOptions.Compiled);
        private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);
        private static readonly Regex TerminatorCheckRegex = new(@"[#>$%\u2192\u276F\u279C]\s*$", RegexOptions.Compiled);
        private static readonly Regex AlphanumericRegex = new(@"[a-zA-Z0-9]", RegexOptions.Compiled);

        /// <summary>
        /// Builds a regex pattern to match the given prompt while allowing common
        /// prompt-body changes (cwd, mode/context, prompt decorations).
        /// </summary>
        public static Regex BuildPromptRegex(string promptLiteral)
        {
            // Generic fallback if nothing known yet
            if (string.IsNullOrWhiteSpace(promptLiteral))
                return CreateFallbackRegex();

            // Trim trailing whitespace and ANSI
            var trimmed = TrailingWhitespaceRegex.Replace(promptLiteral, "");
            trimmed = AnsiEscapeRegex.Replace(trimmed, "");

            // Ensure it ends with a typical prompt terminator
            if (!TerminatorCheckRegex.IsMatch(trimmed))
                return CreateFallbackRegex();

            // Build around a stable prompt anchor and current terminator.
            char terminator = trimmed[^1];
            string body = trimmed[..^1].TrimEnd();
            string anchor = ExtractPromptAnchor(body);

            // If anchor does not contain alphanumeric characters, it is likely a
            // status indicator from starship/oh-my-zsh style prompts.
            if (!AlphanumericRegex.IsMatch(anchor))
            {
                string terminatorEsc = Regex.Escape(terminator.ToString());
                // Use (?:^|[\r\n]) instead of bare ^ so pattern works both with
                // RegexOptions.Multiline (direct IsMatch) and without it (Rebex ScriptEvent)
                string fallbackPattern = $@"(?:^|[\r\n]).*{terminatorEsc}\s*$";
                return new Regex(fallbackPattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
            }

            string anchorEsc = Regex.Escape(anchor);

            // Use (?:^|[\r\n]) for Rebex ScriptEvent compatibility (pattern is used
            // via .ToString() in contexts without RegexOptions.Multiline).
            // Allow changing context after the anchor (cwd, branch, mode text, etc.).
            string pattern = $"(?:^|[\\r\\n])\\s*{anchorEsc}[^\\r\\n]*\\s*[{Regex.Escape(terminator.ToString())}#>$%]\\s*$";

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

            // Real prompts are short - reject very long lines (likely wrapped text/warnings)
            if (line.Length > 80)
                return false;

            // Require minimum length - a bare terminator is not a valid prompt
            // Minimum realistic prompt: "h%" or "a$" (2 chars)
            if (line.Length < 2)
                return false;

            // Require alphanumeric content before traditional terminators (#, >, $, %)
            // Arrow-style terminators are specific to shell prompts and do not need this check
            string beforeTerminator = line[..^1];
            if (!ArrowStyleTerminators.Contains(last) && !AlphanumericRegex.IsMatch(beforeTerminator))
                return false;

            // Lines containing paired quotes are likely instructional text, not prompts
            // e.g., "Please run 'execute disk list' and then 'execute disk scan <ref#"
            if (line.Count(c => c == '\'') >= 2 || line.Count(c => c == '"') >= 2)
                return false;

            return true;
        }

        private static string ExtractPromptAnchor(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            // Prefer "user@host" token when available; this remains stable as cwd changes.
            var tokens = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (!token.Contains('@'))
                    continue;

                var candidate = token.Trim('[', ']', '(', ')');
                var atIndex = candidate.IndexOf('@');
                if (atIndex <= 0 || atIndex >= candidate.Length - 1)
                    continue;

                var separatorIndex = candidate.IndexOf(':', atIndex + 1);
                if (separatorIndex < 0)
                    separatorIndex = candidate.IndexOf('/', atIndex + 1);
                if (separatorIndex < 0)
                    separatorIndex = candidate.IndexOf('\\', atIndex + 1);

                if (separatorIndex > 0)
                    candidate = candidate[..separatorIndex];

                candidate = candidate.TrimEnd(':');
                if (AlphanumericRegex.IsMatch(candidate))
                    return candidate;
            }

            // Support prompts like "hostname (config)#" and "hostname:/path$".
            var candidateAnchor = body;
            var parenIndex = candidateAnchor.IndexOf('(');
            if (parenIndex > 0)
                candidateAnchor = candidateAnchor[..parenIndex].TrimEnd();

            if (string.IsNullOrWhiteSpace(candidateAnchor))
                candidateAnchor = body;

            var colonIndex = candidateAnchor.IndexOf(':');
            if (colonIndex > 0)
                candidateAnchor = candidateAnchor[..colonIndex];

            var whitespaceIndex = candidateAnchor.IndexOfAny(new[] { ' ', '\t' });
            if (whitespaceIndex > 0)
                candidateAnchor = candidateAnchor[..whitespaceIndex];

            return candidateAnchor.Trim();
        }

        private static Regex CreateFallbackRegex()
        {
            // Use (?:^|[\r\n]) instead of bare ^ so pattern works both with
            // RegexOptions.Multiline (direct IsMatch) and without it (Rebex ScriptEvent)
            return new Regex(@"(?:^|[\r\n]).*(?:[#>$%\u2192\u276F\u279C])[ \t]*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        }
    }
}
