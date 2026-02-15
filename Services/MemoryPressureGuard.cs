using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Caps large in-memory text payloads so history and transcript data do not grow unbounded.
    /// </summary>
    public static class MemoryPressureGuard
    {
        public const int MaxHistoryOutputChars = 1_000_000;
        public const int MaxHostOutputChars = 750_000;
        public const int MaxInteractiveTranscriptChars = 400_000;
        public const int MaxCommandSnapshotChars = 200_000;
        public const int MaxDetailVariableValueChars = 20_000;
        public const int MaxVisibleOutputChars = 750_000;
        public const int MaxInMemoryOutputBufferChars = 1_500_000;

        public static bool TrimApplicationState(ApplicationState? state)
        {
            if (state?.History == null || state.History.Count == 0)
                return false;

            var changed = false;
            foreach (var entry in state.History)
            {
                if (TrimHistoryEntry(entry))
                {
                    changed = true;
                }
            }

            return changed;
        }

        public static bool TrimHistoryEntry(HistoryEntry? entry)
        {
            if (entry == null)
                return false;

            var changed = false;

            var trimmedOutput = TrimHistoryOutput(entry.Output);
            if (!string.Equals(entry.Output, trimmedOutput, StringComparison.Ordinal))
            {
                entry.Output = trimmedOutput;
                changed = true;
            }

            if (entry.HostResults != null)
            {
                foreach (var hostResult in entry.HostResults)
                {
                    if (hostResult == null)
                        continue;

                    var trimmedHostOutput = TrimHostOutput(hostResult.Output);
                    if (!string.Equals(hostResult.Output, trimmedHostOutput, StringComparison.Ordinal))
                    {
                        hostResult.Output = trimmedHostOutput;
                        changed = true;
                    }
                }
            }

            if (TrimExecutionDetails(entry.Details))
            {
                changed = true;
            }

            return changed;
        }

        public static bool TrimRuntimeHistoryEntry(HistoryListItem? entry)
        {
            if (entry == null)
                return false;

            var trimmedOutput = TrimHistoryOutput(entry.Output);
            if (string.Equals(entry.Output, trimmedOutput, StringComparison.Ordinal))
                return false;

            entry.Output = trimmedOutput;
            return true;
        }

        public static bool TrimExecutionDetails(ExecutionDetails? details)
        {
            if (details == null)
                return false;

            var changed = false;

            var trimmedCommands = TrimCommandSnapshot(details.Commands);
            if (!string.Equals(details.Commands, trimmedCommands, StringComparison.Ordinal))
            {
                details.Commands = trimmedCommands;
                changed = true;
            }

            if (details.Hosts != null)
            {
                foreach (var host in details.Hosts)
                {
                    if (host?.Variables == null || host.Variables.Count == 0)
                        continue;

                    var keys = new List<string>(host.Variables.Keys);
                    foreach (var key in keys)
                    {
                        var currentValue = host.Variables.TryGetValue(key, out var value)
                            ? value ?? string.Empty
                            : string.Empty;
                        var trimmedValue = TrimDetailVariableValue(currentValue);
                        if (string.Equals(currentValue, trimmedValue, StringComparison.Ordinal))
                            continue;

                        host.Variables[key] = trimmedValue;
                        changed = true;
                    }
                }
            }

            if (details.InteractiveSessions != null)
            {
                foreach (var session in details.InteractiveSessions)
                {
                    if (session == null)
                        continue;

                    var currentTranscript = session.Transcript ?? string.Empty;
                    var trimmedTranscript = TrimInteractiveTranscript(currentTranscript);
                    if (string.Equals(currentTranscript, trimmedTranscript, StringComparison.Ordinal))
                        continue;

                    session.Transcript = trimmedTranscript;
                    changed = true;
                }
            }

            return changed;
        }

        public static string TrimHistoryOutput(string? value)
        {
            return TrimMiddle(value, MaxHistoryOutputChars, "history output");
        }

        public static string TrimHostOutput(string? value)
        {
            return TrimMiddle(value, MaxHostOutputChars, "host output");
        }

        public static string TrimInteractiveTranscript(string? value)
        {
            return TrimMiddle(value, MaxInteractiveTranscriptChars, "interactive transcript");
        }

        public static string TrimCommandSnapshot(string? value)
        {
            return TrimMiddle(value, MaxCommandSnapshotChars, "command snapshot");
        }

        public static string TrimDetailVariableValue(string? value)
        {
            return TrimMiddle(value, MaxDetailVariableValueChars, "detail variable value");
        }

        public static string TrimVisibleOutput(string? value)
        {
            return KeepLatest(value, MaxVisibleOutputChars, "visible output");
        }

        public static string TrimInMemoryOutputBuffer(string? value)
        {
            return KeepLatest(value, MaxInMemoryOutputBufferChars, "live output buffer");
        }

        private static string TrimMiddle(string? value, int maxChars, string label)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length <= maxChars || maxChars <= 0)
                return value;

            var removedChars = value.Length - maxChars;
            var marker = $"{Environment.NewLine}[... {label} trimmed {removedChars:N0} chars ...]{Environment.NewLine}";
            var remainingChars = maxChars - marker.Length;
            if (remainingChars <= 0)
                return marker.Substring(0, Math.Min(marker.Length, maxChars));

            var headChars = remainingChars / 2;
            var tailChars = remainingChars - headChars;

            var head = value.Substring(0, headChars);
            var tail = value.Substring(value.Length - tailChars, tailChars);
            return string.Concat(head, marker, tail);
        }

        private static string KeepLatest(string? value, int maxChars, string label)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Length <= maxChars || maxChars <= 0)
                return value;

            var removedChars = value.Length - maxChars;
            var marker = $"[... {label} trimmed {removedChars:N0} chars from start ...]{Environment.NewLine}";
            var tailChars = maxChars - marker.Length;
            if (tailChars <= 0)
                return marker.Substring(0, Math.Min(marker.Length, maxChars));

            var tail = value.Substring(value.Length - tailChars, tailChars);
            return marker + tail;
        }
    }
}
