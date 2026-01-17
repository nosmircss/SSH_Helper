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
                        if (c >= ' ' && c <= '~')
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

            return Regex.Replace(input, @"[^\u0020-\u007E\r\n\t\b\u001B]", "");
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

            // Match pager prompts with optional surrounding whitespace and control sequences
            // This handles: "--More--", "-- More --", and variations
            // The pager prompt is typically followed by the echoed space character and a carriage return
            // Pattern breakdown:
            //   \r? - optional carriage return before pager
            //   (?:--\s*More\s*--|(?:-+\s*More\s*-+)) - the pager prompt itself
            //   [ ]? - optional literal space (the echoed space from pressing space to continue)
            //   \r? - optional carriage return after (cursor repositioning)
            var pagerRegex = new Regex(
                @"\r?(?:--\s*More\s*--|(?:-+\s*More\s*-+))[ ]?\r?",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (pagerRegex.IsMatch(chunk))
            {
                sawPager = true;
                chunk = pagerRegex.Replace(chunk, string.Empty);
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
            // 1. \r (carriage return to go to column 0)
            // 2. Spaces to overwrite the "--More--" prompt
            // 3. \r (carriage return again to go back to column 0)
            // 4. Then the actual content continues with proper indentation
            //
            // Pattern: \r followed by spaces followed by \r
            // We strip this entire clearing sequence, leaving just the actual content
            return Regex.Replace(chunk, @"^\r[ ]+\r", string.Empty);
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
            string[] parts = parameters.Split(';', StringSplitOptions.RemoveEmptyEntries);

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
                col = ParseIntOrDefault(parts, 0, 1);

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
            if (index < parts.Length && int.TryParse(parts[index], out int value) && value > 0)
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
