namespace SSH_Helper.Models
{
    /// <summary>
    /// Preset sort modes for the preset list.
    /// </summary>
    public enum PresetSortMode
    {
        Ascending,
        Descending,
        Manual
    }

    /// <summary>
    /// Root configuration object persisted to config.json
    /// </summary>
    public class AppConfiguration
    {
        public Dictionary<string, PresetInfo> Presets { get; set; } = new();
        public string Username { get; set; } = string.Empty;
        public int Delay { get; set; } = 500;
        public int Timeout { get; set; } = 10;

        // Window state
        public WindowState WindowState { get; set; } = new();

        // Preset sorting
        public PresetSortMode PresetSortMode { get; set; } = PresetSortMode.Ascending;
        public List<string> ManualPresetOrder { get; set; } = new();
    }

    /// <summary>
    /// Stores window position and splitter settings.
    /// </summary>
    public class WindowState
    {
        public int? Left { get; set; } = 50;
        public int? Top { get; set; } = 50;
        public int? Width { get; set; } = 1850;
        public int? Height { get; set; } = 1050;
        public bool IsMaximized { get; set; } 

        // Splitter positions
        public int? MainSplitterDistance { get; set; } = 400;
        public int? TopSplitterDistance { get; set; } = 800;
        public int? CommandSplitterDistance { get; set; } = 350;
        public int? OutputSplitterDistance { get; set; } = 300;
    }
}
