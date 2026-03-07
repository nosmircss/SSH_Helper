namespace SSH_Helper.Models
{
    /// <summary>
    /// Stored host-grid profile for a named environment (dev/staging/prod, etc.).
    /// </summary>
    public class EnvironmentConfig
    {
        public const string DefaultName = "Default";

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int? LabelColor { get; set; }
        public List<string> HostColumns { get; set; } = new();
        public List<Dictionary<string, string>> Hosts { get; set; } = new();
        public List<int> SelectedHostIndices { get; set; } = new();
        public string? LastCsvPath { get; set; }
        public CsvFileFingerprint? LastCsvFingerprint { get; set; }
        public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public static EnvironmentConfig FromApplicationState(string name, ApplicationState? state)
        {
            var environment = new EnvironmentConfig
            {
                Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name
            };

            if (state == null)
                return environment;

            environment.HostColumns = state.HostColumns?.ToList() ?? new List<string>();
            environment.Hosts = state.Hosts?
                .Select(row => new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase))
                .Cast<Dictionary<string, string>>()
                .ToList()
                ?? new List<Dictionary<string, string>>();
            environment.SelectedHostIndices = state.SelectedHostIndices?.ToList() ?? new List<int>();
            environment.LastCsvPath = state.LastCsvPath;
            environment.LastCsvFingerprint = state.LastCsvFingerprint?.Clone();
            return environment;
        }

        public EnvironmentConfig Clone()
        {
            return new EnvironmentConfig
            {
                Name = Name,
                Description = Description,
                LabelColor = LabelColor,
                HostColumns = HostColumns?.ToList() ?? new List<string>(),
                Hosts = Hosts?
                    .Select(row => new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase))
                    .Cast<Dictionary<string, string>>()
                    .ToList()
                    ?? new List<Dictionary<string, string>>(),
                SelectedHostIndices = SelectedHostIndices?.ToList() ?? new List<int>(),
                LastCsvPath = LastCsvPath,
                LastCsvFingerprint = LastCsvFingerprint?.Clone(),
                Variables = Variables != null
                    ? new Dictionary<string, string>(Variables, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };
        }

        public void Normalize(string fallbackName)
        {
            Name = string.IsNullOrWhiteSpace(Name) ? fallbackName : Name;
            HostColumns ??= new List<string>();
            Hosts ??= new List<Dictionary<string, string>>();
            SelectedHostIndices ??= new List<int>();
            LastCsvFingerprint = LastCsvFingerprint?.Clone();
            LastCsvFingerprint?.Normalize();
            Variables = Variables != null
                ? new Dictionary<string, string>(Variables, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < Hosts.Count; i++)
            {
                Hosts[i] = Hosts[i] != null
                    ? new Dictionary<string, string>(Hosts[i], StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }
}
