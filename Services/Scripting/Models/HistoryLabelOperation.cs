using System;

namespace SSH_Helper.Services.Scripting.Models
{
    /// <summary>
    /// A single sethistorylabel mutation that can be replayed in a deterministic order.
    /// </summary>
    public sealed class HistoryLabelOperation
    {
        public const string ReplaceMode = "replace";
        public const string AppendMode = "append";
        public const string PrependMode = "prepend";
        public const string ClearMode = "clear";

        public static readonly string[] KnownModes =
        [
            ReplaceMode,
            AppendMode,
            PrependMode,
            ClearMode
        ];

        public string Mode { get; set; } = ReplaceMode;
        public string Value { get; set; } = string.Empty;
        public string Separator { get; set; } = string.Empty;
        public bool? ReplaceAddress { get; set; }

        public static bool IsValidMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return true;

            return Array.Exists(
                KnownModes,
                candidate => string.Equals(candidate, mode, StringComparison.OrdinalIgnoreCase));
        }

        public static string NormalizeMode(string? mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
                return ReplaceMode;

            foreach (var candidate in KnownModes)
            {
                if (string.Equals(candidate, mode, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return ReplaceMode;
        }

        public HistoryLabelOperation Clone()
        {
            return new HistoryLabelOperation
            {
                Mode = Mode,
                Value = Value,
                Separator = Separator,
                ReplaceAddress = ReplaceAddress
            };
        }

        public void ApplyTo(ref string? historyLabel, ref bool replacesAddress)
        {
            var normalizedMode = NormalizeMode(Mode);
            if (string.Equals(normalizedMode, ClearMode, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(Value))
            {
                historyLabel = null;
                replacesAddress = false;
                return;
            }

            var currentLabel = string.IsNullOrWhiteSpace(historyLabel) ? null : historyLabel;
            switch (normalizedMode)
            {
                case AppendMode:
                    historyLabel = currentLabel == null
                        ? Value
                        : currentLabel + Separator + Value;
                    break;

                case PrependMode:
                    historyLabel = currentLabel == null
                        ? Value
                        : Value + Separator + currentLabel;
                    break;

                default:
                    historyLabel = Value;
                    break;
            }

            if (string.Equals(normalizedMode, ReplaceMode, StringComparison.Ordinal))
            {
                replacesAddress = ReplaceAddress ?? false;
            }
            else if (ReplaceAddress.HasValue)
            {
                replacesAddress = ReplaceAddress.Value;
            }
        }
    }
}
