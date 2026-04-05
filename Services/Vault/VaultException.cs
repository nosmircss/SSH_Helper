namespace SSH_Helper.Services.Vault
{
    /// <summary>
    /// Exception thrown when a Vault operation fails, with user-friendly error messages.
    /// </summary>
    public class VaultException : Exception
    {
        public VaultException(string message) : base(message) { }

        public VaultException(string message, Exception innerException) : base(message, innerException) { }
    }
}
