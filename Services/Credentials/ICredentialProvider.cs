namespace SSH_Helper.Services
{
    /// <summary>
    /// Abstraction for credential storage providers.
    /// </summary>
    public interface ICredentialProvider
    {
        bool IsAvailable { get; }

        bool TryGetPassword(string target, out string username, out string password);

        bool SavePassword(string target, string username, string password, string? comment = null);

        bool DeletePassword(string target);
    }
}
