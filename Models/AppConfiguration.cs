namespace SSH_Helper.Models
{
    /// <summary>
    /// Root configuration object persisted to config.json
    /// </summary>
    public class AppConfiguration
    {
        public Dictionary<string, PresetInfo> Presets { get; set; } = new();
        public string Username { get; set; } = string.Empty;
        public int Delay { get; set; } = 500;
        public int Timeout { get; set; } = 10;
    }
}
