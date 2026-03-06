using SSH_Helper.Models;

namespace SSH_Helper.Services
{
    /// <summary>
    /// Event payload for active-environment transitions.
    /// </summary>
    public sealed class EnvironmentChangedEventArgs : EventArgs
    {
        public EnvironmentChangedEventArgs(string previousEnvironment, string currentEnvironment, EnvironmentConfig currentConfiguration)
        {
            PreviousEnvironment = previousEnvironment;
            CurrentEnvironment = currentEnvironment;
            CurrentConfiguration = currentConfiguration;
        }

        public string PreviousEnvironment { get; }
        public string CurrentEnvironment { get; }
        public EnvironmentConfig CurrentConfiguration { get; }
    }

    /// <summary>
    /// Manages named environment profiles and active environment switching.
    /// </summary>
    public sealed class EnvironmentService
    {
        private readonly ConfigurationService _configService;

        public EnvironmentService(ConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public event EventHandler<EnvironmentChangedEventArgs>? EnvironmentChanged;

        public List<string> GetEnvironmentNames()
        {
            var (environments, _, _) = _configService.LoadEnvironmentState();
            var names = environments.Keys
                .Where(name => !IsDefaultName(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            names.Insert(0, EnvironmentConfig.DefaultName);
            return names;
        }

        public string GetActiveEnvironmentName()
        {
            var (environments, activeEnvironment, _) = _configService.LoadEnvironmentState();
            return ResolveActiveName(activeEnvironment, environments);
        }

        public string GetBaseEnvironmentName()
        {
            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();
            return ResolveBaseName(baseEnvironment, activeEnvironment, environments);
        }

        public EnvironmentConfig GetEnvironment(string name)
        {
            var environmentName = NormalizeName(name);
            var (environments, _, _) = _configService.LoadEnvironmentState();

            if (environments.TryGetValue(environmentName, out var environment))
                return environment.Clone();

            if (IsDefaultName(environmentName))
                return _configService.BuildLegacyDefaultEnvironment();

            throw new KeyNotFoundException($"Environment '{environmentName}' was not found.");
        }

        public EnvironmentConfig SwitchEnvironment(string name)
        {
            var target = NormalizeName(name);
            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();
            var previous = ResolveActiveName(activeEnvironment, environments);
            var currentBase = ResolveBaseName(baseEnvironment, activeEnvironment, environments);
            EnvironmentConfig selected;

            if (environments.TryGetValue(target, out var explicitEnvironment))
            {
                selected = explicitEnvironment.Clone();
            }
            else if (IsDefaultName(target))
            {
                selected = _configService.BuildLegacyDefaultEnvironment();
            }
            else
            {
                throw new KeyNotFoundException($"Environment '{target}' was not found.");
            }

            _configService.SaveEnvironmentState(environments, target, currentBase);
            RaiseEnvironmentChanged(previous, target, selected);
            return selected;
        }

        public void SetBaseEnvironment(string name)
        {
            var target = NormalizeName(name);
            var (environments, activeEnvironment, _) = _configService.LoadEnvironmentState();

            if (!IsDefaultName(target) && !environments.ContainsKey(target))
                throw new KeyNotFoundException($"Environment '{target}' was not found.");

            _configService.SaveEnvironmentState(
                environments,
                ResolveActiveName(activeEnvironment, environments),
                target);
        }

        public void SaveCurrentGridToEnvironment(
            string name,
            List<string> columns,
            List<Dictionary<string, string>> hosts,
            List<int> selectedIndices,
            string? csvPath)
        {
            var environmentName = NormalizeName(name);
            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();

            // First explicit environment adoption: capture legacy state into Default.
            if (environments.Count == 0 && !IsDefaultName(environmentName))
            {
                environments[EnvironmentConfig.DefaultName] = _configService.BuildLegacyDefaultEnvironment();
            }

            environments.TryGetValue(environmentName, out var existing);
            var snapshot = BuildSnapshot(
                environmentName,
                columns,
                hosts,
                selectedIndices,
                csvPath,
                existing);

            environments[environmentName] = snapshot;
            var active = string.IsNullOrWhiteSpace(activeEnvironment)
                ? environmentName
                : activeEnvironment;
            _configService.SaveEnvironmentState(
                environments,
                active,
                ResolveBaseName(baseEnvironment, activeEnvironment, environments));
        }

        public EnvironmentConfig CreateEnvironment(string name, string? copyFrom = null)
        {
            var newName = NormalizeName(name);
            if (IsDefaultName(newName))
                throw new InvalidOperationException($"'{EnvironmentConfig.DefaultName}' is reserved.");

            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();

            if (environments.ContainsKey(newName))
                throw new InvalidOperationException($"Environment '{newName}' already exists.");

            if (!environments.ContainsKey(EnvironmentConfig.DefaultName))
            {
                environments[EnvironmentConfig.DefaultName] = _configService.BuildLegacyDefaultEnvironment();
            }

            var sourceName = string.IsNullOrWhiteSpace(copyFrom)
                ? ResolveActiveName(activeEnvironment, environments)
                : NormalizeName(copyFrom);

            EnvironmentConfig source = environments.TryGetValue(sourceName, out var existing)
                ? existing.Clone()
                : new EnvironmentConfig();

            source.Name = newName;
            source.Normalize(newName);

            environments[newName] = source;
            var active = ResolveActiveName(activeEnvironment, environments);
            _configService.SaveEnvironmentState(
                environments,
                active,
                ResolveBaseName(baseEnvironment, activeEnvironment, environments));
            return source.Clone();
        }

        public void DeleteEnvironment(string name)
        {
            var environmentName = NormalizeName(name);
            if (IsDefaultName(environmentName))
                throw new InvalidOperationException($"'{EnvironmentConfig.DefaultName}' cannot be deleted.");

            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();
            var previous = ResolveActiveName(activeEnvironment, environments);
            var currentBase = ResolveBaseName(baseEnvironment, activeEnvironment, environments);

            if (!environments.Remove(environmentName))
                throw new KeyNotFoundException($"Environment '{environmentName}' was not found.");

            var next = string.Equals(previous, environmentName, StringComparison.OrdinalIgnoreCase)
                ? EnvironmentConfig.DefaultName
                : ResolveActiveName(activeEnvironment, environments);

            var nextBase = string.Equals(currentBase, environmentName, StringComparison.OrdinalIgnoreCase)
                ? next
                : currentBase;
            _configService.SaveEnvironmentState(environments, next, nextBase);

            if (!string.Equals(previous, next, StringComparison.OrdinalIgnoreCase))
            {
                var current = GetEnvironment(next);
                RaiseEnvironmentChanged(previous, next, current);
            }
        }

        public void RenameEnvironment(string oldName, string newName)
        {
            var sourceName = NormalizeName(oldName);
            var targetName = NormalizeName(newName);

            if (IsDefaultName(sourceName))
                throw new InvalidOperationException($"'{EnvironmentConfig.DefaultName}' cannot be renamed.");

            if (IsDefaultName(targetName))
                throw new InvalidOperationException($"'{EnvironmentConfig.DefaultName}' is reserved.");

            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();
            var previous = ResolveActiveName(activeEnvironment, environments);
            var currentBase = ResolveBaseName(baseEnvironment, activeEnvironment, environments);

            if (!environments.TryGetValue(sourceName, out var existing))
                throw new KeyNotFoundException($"Environment '{sourceName}' was not found.");

            if (environments.ContainsKey(targetName))
                throw new InvalidOperationException($"Environment '{targetName}' already exists.");

            environments.Remove(sourceName);
            existing.Name = targetName;
            existing.Normalize(targetName);
            environments[targetName] = existing;

            var next = string.Equals(previous, sourceName, StringComparison.OrdinalIgnoreCase)
                ? targetName
                : ResolveActiveName(activeEnvironment, environments);

            var nextBase = string.Equals(currentBase, sourceName, StringComparison.OrdinalIgnoreCase)
                ? targetName
                : currentBase;
            _configService.SaveEnvironmentState(environments, next, nextBase);

            if (!string.Equals(previous, next, StringComparison.OrdinalIgnoreCase))
            {
                RaiseEnvironmentChanged(previous, next, existing.Clone());
            }
        }

        public Dictionary<string, string> GetActiveEnvironmentVariables()
        {
            var (environments, activeEnvironment, _) = _configService.LoadEnvironmentState();
            var active = ResolveActiveName(activeEnvironment, environments);

            if (environments.TryGetValue(active, out var environment))
            {
                return environment.Variables != null
                    ? new Dictionary<string, string>(environment.Variables, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public void UpdateActiveEnvironmentVariable(string variableName, string value)
        {
            if (string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentException("Environment variable name is required.", nameof(variableName));

            var variableKey = variableName.Trim();
            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();
            var active = ResolveActiveName(activeEnvironment, environments);

            // Ensure a persisted default exists before mutating active Default.
            if (IsDefaultName(active) && !environments.ContainsKey(EnvironmentConfig.DefaultName))
            {
                environments[EnvironmentConfig.DefaultName] = _configService.BuildLegacyDefaultEnvironment();
            }

            if (!environments.TryGetValue(active, out var environment))
                throw new KeyNotFoundException($"Environment '{active}' was not found.");

            environment.Variables ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            environment.Variables[variableKey] = value ?? string.Empty;
            environment.Normalize(active);
            environments[active] = environment;

            _configService.SaveEnvironmentState(
                environments,
                active,
                ResolveBaseName(baseEnvironment, activeEnvironment, environments));
        }

        public void UpdateEnvironmentDetails(string name, string? description, int? labelColor, Dictionary<string, string> variables)
        {
            var environmentName = NormalizeName(name);
            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();

            if (IsDefaultName(environmentName) && !environments.ContainsKey(EnvironmentConfig.DefaultName))
            {
                environments[EnvironmentConfig.DefaultName] = _configService.BuildLegacyDefaultEnvironment();
            }

            if (!environments.TryGetValue(environmentName, out var environment))
                throw new KeyNotFoundException($"Environment '{environmentName}' was not found.");

            environment.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            environment.LabelColor = labelColor;
            environment.Variables = variables != null
                ? new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            environment.Normalize(environmentName);
            environments[environmentName] = environment;

            _configService.SaveEnvironmentState(
                environments,
                ResolveActiveName(activeEnvironment, environments),
                ResolveBaseName(baseEnvironment, activeEnvironment, environments));
        }

        public EnvironmentConfig ImportEnvironment(EnvironmentConfig environment, bool overwriteExisting = false)
        {
            if (environment == null)
                throw new ArgumentNullException(nameof(environment));

            var environmentName = NormalizeName(environment.Name);
            var (environments, activeEnvironment, baseEnvironment) = _configService.LoadEnvironmentState();

            // First explicit environment adoption: capture legacy state into Default.
            if (environments.Count == 0 && !IsDefaultName(environmentName))
            {
                environments[EnvironmentConfig.DefaultName] = _configService.BuildLegacyDefaultEnvironment();
            }

            if (environments.ContainsKey(environmentName) && !overwriteExisting)
                throw new InvalidOperationException($"Environment '{environmentName}' already exists.");

            var imported = environment.Clone();
            imported.Name = environmentName;
            imported.Normalize(environmentName);
            environments[environmentName] = imported;

            _configService.SaveEnvironmentState(
                environments,
                ResolveActiveName(activeEnvironment, environments),
                ResolveBaseName(baseEnvironment, activeEnvironment, environments));
            return imported.Clone();
        }

        private static EnvironmentConfig BuildSnapshot(
            string name,
            List<string> columns,
            List<Dictionary<string, string>> hosts,
            List<int> selectedIndices,
            string? csvPath,
            EnvironmentConfig? existing)
        {
            var environment = new EnvironmentConfig
            {
                Name = name,
                Description = existing?.Description,
                LabelColor = existing?.LabelColor,
                HostColumns = columns?.ToList() ?? new List<string>(),
                Hosts = hosts?
                    .Select(row => new Dictionary<string, string>(row, StringComparer.OrdinalIgnoreCase))
                    .Cast<Dictionary<string, string>>()
                    .ToList()
                    ?? new List<Dictionary<string, string>>(),
                SelectedHostIndices = selectedIndices?.ToList() ?? new List<int>(),
                LastCsvPath = csvPath,
                Variables = existing?.Variables != null
                    ? new Dictionary<string, string>(existing.Variables, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            };

            environment.Normalize(name);
            return environment;
        }

        private static string ResolveActiveName(string? activeEnvironment, Dictionary<string, EnvironmentConfig> environments)
        {
            if (!string.IsNullOrWhiteSpace(activeEnvironment))
            {
                if (IsDefaultName(activeEnvironment) || environments.ContainsKey(activeEnvironment))
                    return activeEnvironment;
            }

            return EnvironmentConfig.DefaultName;
        }

        private static string ResolveBaseName(
            string? baseEnvironment,
            string? activeEnvironment,
            Dictionary<string, EnvironmentConfig> environments)
        {
            if (!string.IsNullOrWhiteSpace(baseEnvironment))
            {
                if (IsDefaultName(baseEnvironment) || environments.ContainsKey(baseEnvironment))
                    return baseEnvironment;
            }

            return ResolveActiveName(activeEnvironment, environments);
        }

        private static bool IsDefaultName(string name)
        {
            return string.Equals(name, EnvironmentConfig.DefaultName, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Environment name is required.", nameof(name));

            return name.Trim();
        }

        private void RaiseEnvironmentChanged(string previousEnvironment, string currentEnvironment, EnvironmentConfig currentConfiguration)
        {
            if (string.Equals(previousEnvironment, currentEnvironment, StringComparison.OrdinalIgnoreCase))
                return;

            EnvironmentChanged?.Invoke(this, new EnvironmentChangedEventArgs(
                previousEnvironment,
                currentEnvironment,
                currentConfiguration.Clone()));
        }
    }
}
