using System;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Shared regex defaults for user-provided script patterns.
    /// </summary>
    internal static class ScriptRegexDefaults
    {
        internal static readonly TimeSpan UserPatternTimeout = TimeSpan.FromSeconds(5);
    }
}
