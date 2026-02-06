using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SSH_Helper.Utilities
{
    /// <summary>
    /// Processes terminal output by normalizing ANSI escape sequences,
    /// handling cursor movements, and stripping control characters.
    /// </summary>
    public static class TerminalOutputProcessor
    {
        private const int DefaultTabSize = 8;

        // Pre-compiled regex patterns for hot path optimization
        private static readonly Regex SanitizeRegex = new(
            @"[^\u0020-\u007E\u0080-\uFFFF\r\n\t\b\u001B]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex PagerRegex = new(
            @"\r?(?:--\s*More\s*--|(?:-+\s*More\s*-+))[ ]?\r?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex PagerDismissalRegex = new(
            @"^ ?\r +\r",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // zsh PROMPT_SP: When output doesn't end with newline, zsh displays % indicator
        // then clears it with spaces + \r \r sequence before showing the actual prompt.
        // Pattern 1: % followed by spaces and clearing sequence
        // Pattern 2: Standalone clearing sequence (spaces + multiple CRs)
        private static readonly Regex ZshPromptSpRegex = new(
            @"%[ ]*(?=\r)|(?<=\r)[ ]+\r+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Starship and similar prompts may prepend a timestamp/context line
        // immediately before the final prompt symbol line.
        private static readonly Regex PromptMetadataLineRegex = new(
            @"^\s*\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}\b.*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Normalizes terminal output by processing ANSI escape sequences,
        /// carriage returns, tabs, backspaces, and cursor movements.
        /// </summary>
        /// <param name="input">Raw terminal output</param>
        /// <param name="tabSize">Tab stop size (default 8)</param>
        /// <returns>Normalized plain text output</returns>
        public static string Normalize(string input, int tabSize = DefaultTabSize)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var output = new StringBuilder(input.Length + 64);
            var line = new StringBuilder(256);
            int cursor = 0;
            int savedCursor = -1;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                switch (c)
                {
                    case '\r':
                        cursor = 0;
                        break;

                    case '\n':
                        CommitLine(output, line, ref cursor);
                        break;

                    case '\t':
                        int nextStop = ((cursor / tabSize) + 1) * tabSize;
                        EnsureLineLength(line, nextStop);
                        cursor = nextStop;
                        break;

                    case '\b':
                        if (cursor > 0) cursor--;
                        break;

                    case (char)0x1B: // ESC
                        i = ProcessEscapeSequence(input, i, line, ref cursor, ref savedCursor);
                        break;

                    default:
                        if ((c >= ' ' && c <= '~') || c >= '\u0080')
                        {
                            EnsureLineLength(line, cursor + 1);
                            line[cursor] = c;
                            cursor++;
                        }
                        // Ignore other control characters
                        break;
                }
            }

            // Commit remaining line content without trailing newline
            if (line.Length > 0)
            {
                output.Append(TrimTrailingSpaces(line));
            }

            return output.ToString();
        }

        /// <summary>
        /// Sanitizes raw terminal output by removing non-printable characters
        /// except those needed for processing (ESC, CR, LF, TAB, BS).
        /// </summary>
        public static string Sanitize(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return SanitizeRegex.Replace(input, "");
        }

        /// <summary>
        /// Strips common pager artifacts (e.g., "-- More --") from terminal output.
        /// Also handles the space character echo and cursor repositioning that occurs
        /// after dismissing a pager prompt.
        /// </summary>
        /// <param name="chunk">Input text chunk</param>
        /// <param name="sawPager">True if a pager prompt was detected and removed</param>
        /// <returns>Text with pager artifacts removed</returns>
        public static string StripPagerArtifacts(string chunk, out bool sawPager)
        {
            sawPager = false;

            if (PagerRegex.IsMatch(chunk))
            {
                sawPager = true;
                chunk = PagerRegex.Replace(chunk, string.Empty);
            }

            return chunk;
        }

        /// <summary>
        /// Strips the echoed space character and line-clearing sequences that appear after dismissing a pager.
        /// This should be called on chunks received immediately after a pager was dismissed.
        /// </summary>
        /// <param name="chunk">Input text chunk</param>
        /// <returns>Text with leading pager dismissal artifacts removed</returns>
        public static string StripPagerDismissalArtifacts(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return chunk;

            // After sending space to dismiss pager, FortiGate (and similar devices) send:
            // 1. Optional echoed space character
            // 2. \r (carriage return to go to column 0)
            // 3. Spaces to overwrite the "--More--" prompt
            // 4. \r (carriage return again to go back to column 0)
            // 5. Then the actual content continues
            return PagerDismissalRegex.Replace(chunk, string.Empty);
        }

        /// <summary>
        /// Strips zsh PROMPT_SP artifacts from terminal output.
        /// zsh displays a '%' character when command output doesn't end with a newline,
        /// then clears it with spaces and carriage returns before showing the prompt.
        /// </summary>
        /// <param name="chunk">Input text chunk</param>
        /// <returns>Text with zsh PROMPT_SP artifacts removed</returns>
        public static string StripZshPromptSp(string chunk)
        {
            if (string.IsNullOrEmpty(chunk))
                return chunk;

            return ZshPromptSpRegex.Replace(chunk, string.Empty);
        }

        /// <summary>
        /// Strips trailing shell prompt artifacts from command output.
        /// </summary>
        /// <param name="output">Captured command output</param>
        /// <param name="currentPrompt">Current detected shell prompt literal</param>
        /// <returns>Output without trailing prompt lines</returns>
        public static string StripTrailingPrompt(string output, string? currentPrompt)
        {
            if (string.IsNullOrEmpty(output) || string.IsNullOrWhiteSpace(currentPrompt))
                return output;

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            if (lines.Count == 0)
                return output;

            int last = lines.Count - 1;
            while (last >= 0 && string.IsNullOrWhiteSpace(lines[last]))
            {
                last--;
            }

            if (last < 0)
                return string.Empty;

            var promptLiteral = currentPrompt.TrimEnd();
            var promptRegex = PromptDetector.BuildPromptRegex(promptLiteral);
            var lastLine = lines[last].TrimEnd();

            bool isPromptLine =
                lastLine.Equals(promptLiteral, StringComparison.Ordinal) ||
                promptRegex.IsMatch(lastLine);

            if (!isPromptLine)
                return output;

            // Remove final prompt line.
            last--;

            // Remove transient prompt metadata line if present (e.g., starship timestamp line).
            if (last >= 0 && PromptMetadataLineRegex.IsMatch(lines[last]))
            {
                last--;
            }

            // Remove blank separator lines directly preceding the prompt block.
            while (last >= 0 && string.IsNullOrWhiteSpace(lines[last]))
            {
                last--;
            }

            if (last < 0)
                return string.Empty;

            return string.Join("\r\n", lines.Take(last + 1));
        }

        /// <summary>
        /// Strips the echoed command from the beginning of terminal output.
        /// When a command is sent, devices typically echo it back before showing output.
        /// </summary>
        /// <param name="output">Raw terminal output that may contain command echo</param>
        /// <param name="command">The command that was sent</param>
        /// <returns>Output with the echoed command removed</returns>
        public static string StripCommandEcho(string output, string command)
        {
            if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(command))
                return output;

            // Normalize the command for comparison (trim whitespace)
            var normalizedCommand = command.Trim();

            // Split output into lines, preserving empty lines
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length == 0)
                return output;

            // Check if first line contains the echoed command
            // The echo typically appears as just the command text (possibly with leading space)
            var firstLine = lines[0].Trim();

            // Match if the first line equals the command or ends with it
            // (handles cases where prompt is included in echo like "hostname# command")
            if (firstLine.Equals(normalizedCommand, StringComparison.Ordinal) ||
                firstLine.EndsWith(normalizedCommand, StringComparison.Ordinal))
            {
                // Remove the first line and rejoin
                if (lines.Length == 1)
                    return string.Empty;

                return string.Join("\r\n", lines.Skip(1));
            }

            return output;
        }

        private static int ProcessEscapeSequence(string input, int startIndex, StringBuilder line, ref int cursor, ref int savedCursor)
        {
            int i = startIndex;

            // Handle simple ESC sequences (ESCs, ESCu for save/restore)
            if (i + 1 < input.Length)
            {
                if (input[i + 1] == 's')
                {
                    savedCursor = cursor;
                    return i + 1;
                }
                if (input[i + 1] == 'u')
                {
                    if (savedCursor >= 0) cursor = Math.Min(savedCursor, line.Length);
                    return i + 1;
                }
            }

            // Handle CSI sequences (ESC[...)
            if (i + 1 < input.Length && input[i + 1] == '[')
            {
                i += 2;
                var param = new StringBuilder();

                while (i < input.Length)
                {
                    char ch = input[i];
                    if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
                    {
                        ProcessCsiCommand(ch, param.ToString(), line, ref cursor, ref savedCursor);
                        break;
                    }
                    else
                    {
                        param.Append(ch);
                        i++;
                    }
                }
            }

            return i;
        }

        private static void ProcessCsiCommand(char command, string parameters, StringBuilder line, ref int cursor, ref int savedCursor)
        {
            string[] parts = parameters.Split(';');

            switch (command)
            {
                case 's': // CSI save cursor
                    savedCursor = cursor;
                    break;

                case 'u': // CSI restore cursor
                    if (savedCursor >= 0) cursor = Math.Min(savedCursor, line.Length);
                    break;

                case 'K': // Erase in Line
                    ProcessEraseInLine(parts, line, ref cursor);
                    break;

                case 'X': // Erase Character
                    ProcessEraseCharacter(parts, line, cursor);
                    break;

                case 'C': // Cursor Forward
                    cursor += ParseIntOrDefault(parts, 0, 1);
                    break;

                case 'D': // Cursor Backward
                    cursor = Math.Max(0, cursor - ParseIntOrDefault(parts, 0, 1));
                    break;

                case 'G': // Cursor Horizontal Absolute (1-based)
                    cursor = Math.Max(0, ParseIntOrDefault(parts, 0, 1) - 1);
                    break;

                case 'H': // Cursor Position
                case 'f': // Horizontal and Vertical Position
                    ProcessCursorPosition(parts, ref cursor);
                    break;

                case '@': // Insert Character
                    ProcessInsertCharacter(parts, line, cursor);
                    break;

                case 'P': // Delete Character
                    ProcessDeleteCharacter(parts, line, cursor);
                    break;

                case 'm': // SGR (colors/styles) - ignore for plain text
                    break;

                // Ignore other CSI commands
            }
        }

        private static void ProcessEraseInLine(string[] parts, StringBuilder line, ref int cursor)
        {
            int mode = ParseIntOrDefault(parts, 0, 0);

            switch (mode)
            {
                case 2: // Erase entire line
                    line.Clear();
                    cursor = 0;
                    break;

                case 0: // Erase from cursor to end
                    if (cursor < line.Length)
                        line.Remove(cursor, line.Length - cursor);
                    break;

                case 1: // Erase from start to cursor
                    if (cursor > 0)
                    {
                        int keep = line.Length - cursor;
                        var tail = keep > 0 ? line.ToString(cursor, keep) : string.Empty;
                        line.Clear();
                        line.Append(new string(' ', cursor));
                        if (keep > 0)
                        {
                            EnsureLineLength(line, cursor + keep);
                            for (int j = 0; j < keep; j++)
                                line[cursor + j] = tail[j];
                        }
                    }
                    break;
            }
        }

        private static void ProcessEraseCharacter(string[] parts, StringBuilder line, int cursor)
        {
            int n = ParseIntOrDefault(parts, 0, 1);
            if (cursor < line.Length)
            {
                int count = Math.Min(n, line.Length - cursor);
                EnsureLineLength(line, cursor + count);
                for (int j = 0; j < count; j++)
                    line[cursor + j] = ' ';
            }
        }

        private static void ProcessCursorPosition(string[] parts, ref int cursor)
        {
            int col = 1;
            if (parts.Length >= 2)
                col = ParseIntOrDefault(parts, 1, 1);
            else if (parts.Length == 1)
                col = 1; // Single parameter is row; column defaults to 1

            cursor = Math.Max(0, col - 1);
        }

        private static void ProcessInsertCharacter(string[] parts, StringBuilder line, int cursor)
        {
            int n = ParseIntOrDefault(parts, 0, 1);
            EnsureLineLength(line, cursor);
            line.Insert(cursor, new string(' ', n));
        }

        private static void ProcessDeleteCharacter(string[] parts, StringBuilder line, int cursor)
        {
            int n = ParseIntOrDefault(parts, 0, 1);
            if (cursor < line.Length)
            {
                int del = Math.Min(n, line.Length - cursor);
                line.Remove(cursor, del);
            }
        }

        private static int ParseIntOrDefault(string[] parts, int index, int defaultValue)
        {
            if (index < parts.Length && int.TryParse(parts[index], out int value) && value >= 0)
                return value;
            return defaultValue;
        }

        private static void EnsureLineLength(StringBuilder line, int length)
        {
            if (line.Length < length)
                line.Append(' ', length - line.Length);
        }

        private static void CommitLine(StringBuilder output, StringBuilder line, ref int cursor)
        {
            output.Append(TrimTrailingSpaces(line));
            output.Append("\r\n");
            line.Clear();
            cursor = 0;
        }

        private static string TrimTrailingSpaces(StringBuilder sb)
        {
            int end = sb.Length;
            while (end > 0 && sb[end - 1] == ' ') end--;
            return sb.ToString(0, end);
        }
    }
}
