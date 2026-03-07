namespace SSH_Helper.Models
{
    /// <summary>
    /// Result of a lightweight SSH connection test (connect + auth + disconnect).
    /// </summary>
    public record ConnectionTestResult(
        bool Success,
        string? ErrorCategory,
        string? ErrorMessage,
        long LatencyMs);
}
