namespace SSH_Helper.Models
{
    /// <summary>
    /// Represents a saved command preset with optional per-preset delay and timeout overrides.
    /// </summary>
    public class PresetInfo
    {
        public string Commands { get; set; } = string.Empty;
        public int? Delay { get; set; }
        public int? Timeout { get; set; }

        public PresetInfo Clone()
        {
            return new PresetInfo
            {
                Commands = Commands,
                Delay = Delay,
                Timeout = Timeout
            };
        }
    }
}
