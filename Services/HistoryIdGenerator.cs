namespace SSH_Helper.Services
{
    /// <summary>
    /// Generates stable unique identifiers for history entries.
    /// </summary>
    public static class HistoryIdGenerator
    {
        public static string NewId() => Guid.NewGuid().ToString("N");
    }
}
