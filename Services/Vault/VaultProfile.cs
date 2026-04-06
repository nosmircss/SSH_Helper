using SSH_Helper.Models;

namespace SSH_Helper.Services.Vault
{
    /// <summary>
    /// Runtime state for a single Vault connection profile.
    /// </summary>
    internal sealed class VaultProfile : IDisposable
    {
        public VaultProfileConfig Config { get; }
        public HttpClient HttpClient { get; }
        public string? ClientToken { get; set; }
        public DateTime TokenExpiry { get; set; }
        public VaultKvVersion? DetectedKvVersion { get; set; }

        public bool IsTokenExpired => ClientToken == null || DateTime.UtcNow >= TokenExpiry;

        public VaultKvVersion EffectiveKvVersion
        {
            get
            {
                if (Config.KvVersion != VaultKvVersion.AutoDetect)
                    return Config.KvVersion;

                return DetectedKvVersion ?? VaultKvVersion.V2;
            }
        }

        public VaultProfile(VaultProfileConfig config, HttpMessageHandler handler)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            HttpClient = new HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = new Uri(config.Address.TrimEnd('/') + "/")
            };
        }

        public void Dispose()
        {
            HttpClient.Dispose();
        }
    }
}
