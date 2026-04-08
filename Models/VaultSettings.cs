namespace SSH_Helper.Models
{
    /// <summary>
    /// Root configuration for HashiCorp Vault integration.
    /// </summary>
    public class VaultSettings
    {
        /// <summary>
        /// When true, Vault credential resolution is available for jobs and environments.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Named Vault connection profiles.
        /// </summary>
        public List<VaultProfileConfig> Profiles { get; set; } = new();

        /// <summary>
        /// The profile name used when no explicit profile is specified.
        /// </summary>
        public string DefaultProfileName { get; set; } = "";
    }

    /// <summary>
    /// Configuration for a single named Vault connection profile.
    /// </summary>
    public class VaultProfileConfig
    {
        /// <summary>
        /// Unique name for this profile (e.g. "prod", "staging").
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Vault server address including scheme and port (e.g. "https://vault.example.com:8200").
        /// </summary>
        public string Address { get; set; } = "";

        /// <summary>
        /// Vault namespace (Enterprise only). Leave empty for open-source Vault.
        /// </summary>
        public string Namespace { get; set; } = "";

        /// <summary>
        /// KV secrets engine mount path. Default: "secret".
        /// </summary>
        public string MountPath { get; set; } = "secret";

        /// <summary>
        /// Authentication method used to obtain a Vault token.
        /// </summary>
        public VaultAuthMethod AuthMethod { get; set; } = VaultAuthMethod.Token;

        /// <summary>
        /// AppRole Role ID (used when <see cref="AuthMethod"/> is <see cref="VaultAuthMethod.AppRole"/>).
        /// The Secret ID is stored in Windows Credential Manager.
        /// </summary>
        public string AppRoleRoleId { get; set; } = "";

        /// <summary>
        /// LDAP username (used when <see cref="AuthMethod"/> is <see cref="VaultAuthMethod.Ldap"/>).
        /// The password is stored in Windows Credential Manager.
        /// </summary>
        public string LdapUsername { get; set; } = "";

        /// <summary>
        /// Userpass username (used when <see cref="AuthMethod"/> is <see cref="VaultAuthMethod.Userpass"/>).
        /// The password is stored in Windows Credential Manager.
        /// </summary>
        public string UserpassUsername { get; set; } = "";

        /// <summary>
        /// OIDC auth mount path (used when <see cref="AuthMethod"/> is <see cref="VaultAuthMethod.Oidc"/>).
        /// Default: "oidc".
        /// </summary>
        public string OidcAuthMountPath { get; set; } = "oidc";

        /// <summary>
        /// OIDC role name configured in Vault (used when <see cref="AuthMethod"/> is <see cref="VaultAuthMethod.Oidc"/>).
        /// </summary>
        public string OidcRole { get; set; } = "";

        /// <summary>
        /// Host component used for the local callback listener during OIDC login.
        /// Default: "127.0.0.1".
        /// </summary>
        public string OidcCallbackHost { get; set; } = "127.0.0.1";

        /// <summary>
        /// Port used for the local callback listener during OIDC login.
        /// Default: 8250.
        /// </summary>
        public int OidcCallbackPort { get; set; } = 8250;

        /// <summary>
        /// Callback path used for OIDC redirect handling.
        /// Default: "/oidc/callback".
        /// </summary>
        public string OidcCallbackPath { get; set; } = "/oidc/callback";

        /// <summary>
        /// Maximum time in seconds to wait for the OIDC callback before failing.
        /// Default: 180.
        /// </summary>
        public int OidcTimeoutSeconds { get; set; } = 180;

        /// <summary>
        /// How long (in seconds) to cache retrieved secrets before re-fetching. Default: 300 (5 minutes).
        /// </summary>
        public int CacheTtlSeconds { get; set; } = 300;

        /// <summary>
        /// Optional path to a custom CA certificate for TLS verification.
        /// </summary>
        public string CaCertificatePath { get; set; } = "";

        /// <summary>
        /// When true, TLS certificate validation is skipped. Use only in development/test environments.
        /// </summary>
        public bool SkipTlsVerification { get; set; } = false;

        /// <summary>
        /// KV secrets engine version. AutoDetect queries the Vault mount to determine the version.
        /// </summary>
        public VaultKvVersion KvVersion { get; set; } = VaultKvVersion.AutoDetect;
    }

    /// <summary>
    /// Vault authentication method used to obtain a token.
    /// </summary>
    public enum VaultAuthMethod
    {
        /// <summary>Direct token authentication. Token stored in Windows Credential Manager.</summary>
        Token = 0,

        /// <summary>AppRole authentication. Role ID stored in config; Secret ID in Credential Manager.</summary>
        AppRole = 1,

        /// <summary>LDAP authentication. Username stored in config; password in Credential Manager.</summary>
        Ldap = 2,

        /// <summary>Userpass authentication. Username stored in config; password in Credential Manager.</summary>
        Userpass = 3,

        /// <summary>OIDC authentication. Browser-based sign-in exchanges callback for a Vault token.</summary>
        Oidc = 4
    }

    /// <summary>
    /// KV secrets engine version.
    /// </summary>
    public enum VaultKvVersion
    {
        /// <summary>Query the mount to determine the version automatically.</summary>
        AutoDetect = 0,

        /// <summary>KV version 1 (no versioning).</summary>
        V1 = 1,

        /// <summary>KV version 2 (with secret versioning).</summary>
        V2 = 2
    }
}
