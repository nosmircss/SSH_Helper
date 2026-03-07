namespace SSH_Helper.Utilities
{
    internal readonly record struct BaseEnvironmentIndicatorState(bool Visible, string Text);

    internal static class BaseEnvironmentIndicatorFormatter
    {
        public static BaseEnvironmentIndicatorState Format(string activeEnvironment, string baseEnvironment)
        {
            if (string.IsNullOrWhiteSpace(activeEnvironment))
                throw new ArgumentException("Active environment is required.", nameof(activeEnvironment));

            if (string.IsNullOrWhiteSpace(baseEnvironment))
                throw new ArgumentException("Base environment is required.", nameof(baseEnvironment));

            return new BaseEnvironmentIndicatorState(
                !string.Equals(activeEnvironment, baseEnvironment, StringComparison.OrdinalIgnoreCase),
                $"Base: {baseEnvironment}");
        }
    }
}
