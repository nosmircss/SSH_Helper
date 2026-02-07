# M1: Environments

**Status**: NOT STARTED

**Why**: The foundation for everything. Without environments, scheduling a job against "prod" vs "dev" requires manual host swaps. This is the #1 UX gap vs Postman.

---

## Progress Checklist

- [ ] Create `Models/EnvironmentConfig.cs` data model
- [ ] Add `Environments` and `ActiveEnvironment` properties to `AppConfiguration.cs`
- [ ] Add environment helper methods to `ConfigurationService.cs`
- [ ] Create `Services/EnvironmentService.cs` (CRUD, switching, events)
- [ ] Create `EnvironmentDialog.cs` (management UI with variable editor)
- [ ] Add environment selector `ToolStripComboBox` to Form1 toolbar
- [ ] Wire environment switching in Form1 (save grid → load new grid)
- [ ] Inject environment variables into `GetHostConnections()`
- [ ] Update title bar to show active environment name
- [ ] Handle migration (existing config → "Default" environment)
- [ ] Write tests for `EnvironmentService` and `EnvironmentConfig`
- [ ] Manual smoke test: create 3 environments, switch, verify hosts/variables swap

---

## Data Model

### New: `Models/EnvironmentConfig.cs`

```csharp
public class EnvironmentConfig
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? LabelColor { get; set; }                    // ARGB for visual identification
    public List<string> HostColumns { get; set; } = new();
    public List<Dictionary<string, string>> Hosts { get; set; } = new();  // Same format as ApplicationState.Hosts
    public List<int> SelectedHostIndices { get; set; } = new();
    public string? LastCsvPath { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
```

### Modified: `Models/AppConfiguration.cs`

Add two properties:

```csharp
public Dictionary<string, EnvironmentConfig> Environments { get; set; } = new();
public string? ActiveEnvironment { get; set; }
```

Existing `ApplicationState.Hosts`, `HostColumns`, `SelectedHostIndices`, `LastCsvPath` remain for backward compatibility (they represent the "Default" environment when no explicit environments exist).

---

## Service: `EnvironmentService`

### New: `Services/EnvironmentService.cs`

```csharp
public class EnvironmentService
{
    public event EventHandler<EnvironmentChangedEventArgs>? EnvironmentChanged;

    public List<string> GetEnvironmentNames();         // Always includes "Default" first
    public string GetActiveEnvironmentName();           // "Default" if none set
    public EnvironmentConfig SwitchEnvironment(string name);  // Fires EnvironmentChanged
    public void SaveCurrentGridToEnvironment(string name, List<string> columns,
        List<Dictionary<string, string>> hosts, List<int> selectedIndices, string? csvPath);
    public EnvironmentConfig CreateEnvironment(string name, string? copyFrom = null);
    public void DeleteEnvironment(string name);         // Cannot delete "Default"
    public void RenameEnvironment(string oldName, string newName);
    public Dictionary<string, string> GetActiveEnvironmentVariables();
}
```

---

## Variable Precedence

Three-tier resolution (highest wins):

1. **Grid column values** (per-host row data) — highest
2. **Environment variables** (per-environment key-value pairs) — NEW
3. **Script `vars:` section defaults** — lowest

Implementation: In `Form1.GetHostConnections()`, after collecting grid column values, merge environment variables for any keys not already present:

```csharp
var envVars = _environmentService.GetActiveEnvironmentVariables();
foreach (var kvp in envVars)
{
    if (!host.Variables.ContainsKey(kvp.Key) || string.IsNullOrEmpty(host.Variables[kvp.Key]))
        host.Variables[kvp.Key] = kvp.Value;
}
```

This keeps the environment concern in the UI layer — services remain environment-unaware.

---

## UI Integration

### Toolbar Addition

Add to the existing toolbar in `Form1.Designer.cs`:

```
..., separatorEnv, toolStripLabelEnv ("Environment:"), tsbEnvironment (ComboBox), tsbManageEnvironments (gear button)
```

- `tsbEnvironment` = `ToolStripComboBox` with `DropDownStyle = DropDownList`
- `tsbManageEnvironments` = `ToolStripButton` → opens `EnvironmentDialog`

### Environment Switch Flow

1. User selects new environment in combo box
2. `tsbEnvironment_SelectedIndexChanged` fires
3. Prompt to save if current grid is dirty
4. Save current grid to current environment via `EnvironmentService`
5. Switch to new environment, get `EnvironmentConfig`
6. Call `LoadEnvironmentIntoGrid(env)` (reuses `RestoreApplicationState` logic for hosts/columns)
7. Update `_activeEnvironmentName` and title bar

### Title Bar Format

`SSH Helper v0.50.19 — [Production]`

---

## Environment Management Dialog

### `EnvironmentDialog.cs`

Follows `SettingsDialog` pattern (programmatic layout, `internal sealed`, `DialogTheme` support).

Layout:
- **Left panel**: ListBox of environment names
- **Right panel**: Edit form for selected environment
  - TextBox: Name
  - TextBox: Description
  - Color picker: Label color
  - DataGridView: Environment variables (2-column: Variable Name, Value)
  - Buttons: New, Duplicate, Rename, Delete
  - Bottom: Save, Cancel

---

## Migration Strategy

- When `Environments` dictionary is empty (legacy config), the app operates in single-environment mode — functionally identical to today
- On first environment creation, the current grid state is automatically snapshotted into a "Default" environment
- JSON deserialization naturally handles missing fields (they get default values) — no config version bump needed
- `RestoreApplicationState()` checks: if `Environments` has entries and `ActiveEnvironment` is set, load that environment; otherwise use `SavedState.Hosts` as before

---

## Host Groups/Tags (Optional Enhancement)

Low-complexity addition to environments:
- Add optional `tags` column to the grid (comma-separated tags per host)
- Scripts can filter: `- foreach: host in hosts where tag contains "firewall"`
- Environment dialog could have a "default tags" field
- Deferred to after core environments work

---

## Key Files

| File | Action |
|------|--------|
| `Models/EnvironmentConfig.cs` | CREATE |
| `Models/AppConfiguration.cs` | MODIFY — add 2 properties |
| `Services/EnvironmentService.cs` | CREATE |
| `Services/ConfigurationService.cs` | MODIFY — add helper methods |
| `EnvironmentDialog.cs` | CREATE |
| `Form1.Designer.cs` | MODIFY — add toolbar controls |
| `Form1.cs` | MODIFY — wire switching, variable injection, state save/restore |
