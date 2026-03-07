namespace SSH_Helper.Utilities
{
    internal enum PresetEnvironmentLoadActionKind
    {
        None,
        SwitchActiveEnvironment,
        RestoreBaseEnvironment
    }

    internal readonly record struct PresetEnvironmentLoadAction(
        PresetEnvironmentLoadActionKind Kind,
        string? TargetEnvironment)
    {
        public static PresetEnvironmentLoadAction None() => new(PresetEnvironmentLoadActionKind.None, null);
    }

    internal static class PresetEnvironmentLoadPlanner
    {
        public static PresetEnvironmentLoadAction Plan(
            string activeEnvironment,
            string baseEnvironment,
            string? declaredEnvironment)
        {
            if (string.IsNullOrWhiteSpace(activeEnvironment))
                throw new ArgumentException("Active environment is required.", nameof(activeEnvironment));

            if (string.IsNullOrWhiteSpace(baseEnvironment))
                throw new ArgumentException("Base environment is required.", nameof(baseEnvironment));

            if (!string.IsNullOrWhiteSpace(declaredEnvironment))
            {
                var normalizedDeclaredEnvironment = declaredEnvironment.Trim();
                return string.Equals(activeEnvironment, normalizedDeclaredEnvironment, StringComparison.OrdinalIgnoreCase)
                    ? PresetEnvironmentLoadAction.None()
                    : new PresetEnvironmentLoadAction(
                        PresetEnvironmentLoadActionKind.SwitchActiveEnvironment,
                        normalizedDeclaredEnvironment);
            }

            return string.Equals(activeEnvironment, baseEnvironment, StringComparison.OrdinalIgnoreCase)
                ? PresetEnvironmentLoadAction.None()
                : new PresetEnvironmentLoadAction(
                    PresetEnvironmentLoadActionKind.RestoreBaseEnvironment,
                    baseEnvironment);
        }
    }
}
