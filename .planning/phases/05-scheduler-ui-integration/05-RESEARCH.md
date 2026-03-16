# Phase 5: Scheduler UI & Integration - Research

**Researched:** 2026-03-07
**Domain:** WinForms dialog construction, event-driven UI updates, job export/import serialization
**Confidence:** HIGH

## Summary

Phase 5 is a pure UI integration phase. All backend services (JobStorageService, JobExecutionService, JobHistoryService, SchedulingService) are complete from Phases 1-4. The work is building four new dialogs (Job List, Job Editor, Run Output Viewer, Import Preview), integrating scheduler state into Form1 (menu, status bar, output panel notifications), and implementing job export/import serialization.

The project has deeply established UI patterns from SettingsDialog (1363 lines, tabbed), EnvironmentDialog (869 lines, modeless with SplitContainer), ExecutionDetailsDialog (tabbed viewer), and CronBuilderControl (visual UserControl). All use code-only layout (no Designer.cs), DialogTheme for dark/light support, and manual DI via constructor parameters. These patterns are the blueprint for Phase 5 dialogs.

**Primary recommendation:** Follow EnvironmentDialog for the Job List dialog (modeless, SplitContainer, live-refresh timer), SettingsDialog for the Job Editor (tabbed, BorderlessTabControl, validation on save), and ExecutionDetailsDialog for the Run Output Viewer (tabbed read-only content with host selector). Export/import follows PresetManager's proven GZip+Base64 and JSON file patterns.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- Job list dialog: Separate modeless dialog, split panel layout (top: job list grid, bottom: run history), dense table columns (Name, enabled, schedule, next run, last result, target preset), context menu + toolbar actions, live refresh via timer
- Job editor dialog: Tabbed (General, Hosts, Credentials, Advanced), CronBuilderControl embedded in General tab, mini-grid for hosts in Hosts tab, CredentialMode radio buttons in Credentials tab, drift warning banner with "Review & Acknowledge" button
- Notification behavior: Output panel log entries with differentiated prefixes ([Scheduled:], [Run Now:], [Skipped:]), status bar showing scheduler state with click-to-open
- Run output viewer: Host selector dropdown, read-only RichTextBox, FindDialog for search, Copy All button, themed via DialogTheme
- Export/import: .sshjobs JSON file + GZip+Base64 clipboard string, version wrapper, credentials stripped on export, import preview dialog with conflict handling

### Claude's Discretion
- Dialog dimensions and control sizing
- Exact toolbar icons/images (or text-only buttons)
- Timer intervals for live refresh and status bar updates
- How "Copy from Main Grid" determines which rows to copy
- Run history grid column widths and sorting defaults
- Status bar text truncation for long job names
- Job list "currently running" visual state

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| UI-01 | User can view all jobs in a list showing status, next run time, and last result | Job List dialog with DataGridView, live refresh via timer, JobStorageService.Jobs + SchedulingService.GetNextRunLocal + JobHistoryService.GetRunsForJob for data |
| UI-02 | User can create and edit jobs via a dedicated editor dialog with cron preview and host/credential options | Job Editor dialog with BorderlessTabControl, CronBuilderControl embedded, host mini-grid, CredentialMode selection, validation via InputValidator |
| UI-03 | User receives in-app notifications on job completion and failures | JobExecutionService.JobStateChanged + JobCompleted events, AppendOutputText integration, status bar ToolStripStatusLabel |
| JMGT-05 | User can export job definitions to a file for sharing | .sshjobs JSON file export + GZip+Base64 clipboard string, credentials stripped, version wrapper |
| JMGT-06 | User can import job definitions from an exported file | File and clipboard import, Import Preview dialog with conflict resolution, missing preset warnings |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Windows.Forms | .NET 8.0 | All dialog UI construction | Project standard, all existing dialogs use this |
| Newtonsoft.Json | 13.0.3 | Job definition serialization for export/import | Already used by all services; project standard |
| System.IO.Compression | .NET 8.0 | GZip compression for clipboard export | Already used by PresetManager for same pattern |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| DialogTheme (internal) | N/A | Dark/light theme colors and ApplyTo/StyleButton/StyleDataGridView | Every new dialog and all controls within |
| BorderlessTabControl (internal) | N/A | Custom TabControl without borders | Job Editor dialog tabs |
| CronBuilderControl (internal) | N/A | Visual cron expression builder | Embedded in Job Editor General tab |
| InputValidator (internal) | N/A | Validation for names, cron, dates | Job editor save validation |
| ContentHasher (internal) | N/A | SHA256 hash for drift detection | Drift warning check on editor open |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Custom export format | Protocol Buffers | Overkill; JSON is human-readable and matches existing patterns |
| Timer for live refresh | FileSystemWatcher | Timer is simpler and already proven in the project; FSW is unreliable on some systems |

**Installation:**
```bash
# No new packages needed -- all dependencies already in SSH_Helper.csproj
```

## Architecture Patterns

### Recommended Project Structure
```
SSH_Helper/
  JobListDialog.cs         # Modeless dialog, ~800-1000 lines (follows EnvironmentDialog pattern)
  JobEditorDialog.cs       # Modal dialog, ~1000-1200 lines (follows SettingsDialog pattern)
  RunOutputViewerDialog.cs # Modal dialog, ~300-400 lines (follows ExecutionDetailsDialog pattern)
  ImportPreviewDialog.cs   # Modal dialog, ~250-350 lines (simpler data review dialog)
  Services/
    JobExportService.cs    # Export/import serialization logic, ~200-300 lines
  Models/
    JobExportDocument.cs   # Export file wrapper model
  Form1.cs                 # Add #region Scheduler for menu, status bar, event handlers
```

### Pattern 1: Modeless Dialog with Live Refresh (Job List)
**What:** A non-modal dialog that stays open while the user works in the main form, with a timer refreshing display data.
**When to use:** For the Job List dialog, which acts as an operations dashboard.
**Example:**
```csharp
// Source: EnvironmentDialog.cs pattern
internal sealed class JobListDialog : Form
{
    private readonly JobStorageService _jobStorage;
    private readonly JobExecutionService _executionService;
    private readonly JobHistoryService _historyService;
    private readonly SchedulingService _schedulingService;
    private readonly PresetManager _presetManager;
    private readonly bool _darkMode;
    private System.Windows.Forms.Timer? _refreshTimer;

    // SplitContainer: top = job grid, bottom = history grid
    private readonly SplitContainer _mainSplit;
    private readonly DataGridView _gridJobs;
    private readonly DataGridView _gridHistory;

    public JobListDialog(
        JobStorageService jobStorage,
        JobExecutionService executionService,
        JobHistoryService historyService,
        SchedulingService schedulingService,
        PresetManager presetManager,
        bool darkMode)
    {
        // ... constructor wires services, builds layout, starts timer
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (s, e) => RefreshJobList();
        _refreshTimer.Start();
    }

    // Single-instance tracking in Form1:
    // private JobListDialog? _jobListDialog;
    // if (_jobListDialog == null || _jobListDialog.IsDisposed) { ... Show() }
    // else { _jobListDialog.BringToFront(); }
}
```

### Pattern 2: Tabbed Modal Dialog with Validation (Job Editor)
**What:** A modal dialog with BorderlessTabControl organizing related settings into tabs, with validation on save.
**When to use:** For the Job Editor dialog.
**Example:**
```csharp
// Source: SettingsDialog.cs pattern
internal sealed class JobEditorDialog : Form
{
    private readonly BorderlessTabControl _tabControl;
    private readonly JobDefinition _job; // clone for editing
    private readonly bool _isNew;

    // General tab
    private readonly TextBox _txtName;
    private readonly RadioButton _rbPreset;
    private readonly RadioButton _rbFolder;
    private readonly ComboBox _cboTarget;
    private readonly ComboBox _cboScheduleType;
    private readonly CronBuilderControl _cronBuilder;
    private readonly DateTimePicker _dtpOneTime;
    private readonly Panel _driftBanner; // yellow/orange warning

    // Hosts tab
    private readonly DataGridView _gridHosts; // mini-grid

    // Credentials tab
    private readonly RadioButton _rbInheritFromApp;
    private readonly RadioButton _rbStored;
    private readonly RadioButton _rbPerHostColumn;

    // Advanced tab
    private readonly RadioButton _rbSequential;
    private readonly RadioButton _rbParallel;
    private readonly CheckBox _chkStopOnError;
    private readonly NumericUpDown _numMaxHistoryRuns;
    private readonly NumericUpDown _numRetentionDays;

    public JobDefinition? Result { get; private set; } // null if cancelled

    private bool ValidateAndSave()
    {
        // Validate name not empty, unique
        // Validate target selected
        // Validate schedule (cron valid if Recurring, future date if OneTime)
        // Validate at least one host
        // Return false with MessageBox on failure
    }
}
```

### Pattern 3: Event-Driven Notifications (Form1 Integration)
**What:** Subscribe to service events, format notification text, append to output panel.
**When to use:** For scheduler notifications in Form1.
**Example:**
```csharp
// Source: Form1.cs SshService_OutputReceived pattern
// In Form1 constructor or init region:
_executionService.JobStateChanged += OnJobStateChanged;
_executionService.JobCompleted += OnJobCompleted;

private void OnJobCompleted(object? sender, JobRunResult result)
{
    var prefix = /* determine from RunningJobInfo.IsRunNow */ ? "Run Now" : "Scheduled";
    var duration = result.CompletedUtc - result.StartedUtc;
    var status = result.Success
        ? $"Completed -- {result.HostsSucceeded}/{result.HostsSucceeded + result.HostsFailed} hosts succeeded ({duration:mm\\:ss})"
        : $"Failed -- {result.HostsFailed}/{result.HostsSucceeded + result.HostsFailed} hosts failed ({duration:mm\\:ss})";
    var line = $"[{DateTime.Now:HH:mm:ss}] [{prefix}: {result.JobName}] {status}";
    AppendOutputText(Environment.NewLine + line + Environment.NewLine);
}
```

### Pattern 4: Export/Import with Version Wrapper
**What:** JSON file with version envelope for forward compatibility, matching PresetManager file export.
**When to use:** For .sshjobs file format.
**Example:**
```csharp
// Source: PresetManager.ExportAllToFile / JobsFileWrapper pattern
public class JobExportDocument
{
    public int Version { get; set; } = 1;
    public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;
    public List<JobDefinition> Jobs { get; set; } = new();
}

// Credential stripping on export:
var exportJob = CloneForExport(job);
exportJob.CredentialMode = CredentialMode.InheritFromApp;
// Clear any stored credential references
```

### Anti-Patterns to Avoid
- **Blocking the UI thread with service calls:** All service calls from dialogs happen on the UI thread (they are synchronous I/O). Keep them fast by using indexed data (GetRunsForJob already returns cached index entries). Do NOT move to async unless measured as slow.
- **Sharing mutable JobDefinition references:** The editor dialog MUST work on a deep clone of the job. Mutating the live object before save would corrupt state if the user cancels.
- **Putting business logic in dialog classes:** Export/import serialization belongs in a service (JobExportService), not in the dialog. Dialogs call services.
- **Using ShowDialog for the Job List:** It must be modeless (Show) so the user can interact with Form1 while viewing jobs.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Dark/light theming | Custom color logic per dialog | DialogTheme.ApplyTo + StyleButton + StyleDataGridView + SetDarkTitleBar | 500+ lines of theme code already exists, handles edge cases |
| Cron expression editing | Text field with manual validation | CronBuilderControl (embed in editor) | Already built in Phase 2, bidirectional sync, preset buttons |
| GZip+Base64 encoding | Custom compression code | Copy PresetManager.CompressAndEncode / DecompressEncoded | Proven pattern, handles stream lifecycle correctly |
| CSV host import | New CSV parser | JobStorageService.ImportHostsFromCsv or its ParseCsvLine | Already handles quoting, escaping, Host_IP requirement |
| Content hash for drift | Custom hashing | ContentHasher.ComputeHash | Consistent with all existing drift detection |
| Cron description text | Manual cron-to-English | SchedulingService.GetDescription | Uses CronExpressionDescriptor NuGet, already integrated |
| Input validation | Inline validation code | InputValidator methods | Centralized, tested, includes cron validation |
| Inline diff display | Custom diff rendering | UnsavedPresetDiffDialog + InlineDiffBuilder | Pattern exists for exactly this use case (drift warning) |

**Key insight:** Phase 5 is almost entirely composition of existing components. Every non-trivial sub-problem has an existing solution in the codebase. The risk is not technical difficulty but dialog layout complexity (1000+ line files) and correct event wiring across multiple services.

## Common Pitfalls

### Pitfall 1: Cross-Thread Event Handling
**What goes wrong:** JobExecutionService fires events on ThreadPool threads (timer callback). Directly updating WinForms controls from these events throws InvalidOperationException.
**Why it happens:** The 30-second evaluation timer runs on a ThreadPool thread, and JobStateChanged/JobCompleted events fire from there.
**How to avoid:** Always use `BeginInvoke` or `Invoke` when handling service events in Form1 or dialogs. The existing pattern from SshService_OutputReceived shows this.
**Warning signs:** `InvalidOperationException: Cross-thread operation not valid` at runtime.

### Pitfall 2: Dialog Disposal and Timer Leaks
**What goes wrong:** The refresh timer in the Job List dialog keeps firing after the dialog is closed, or event subscriptions keep the dialog alive.
**Why it happens:** Not stopping the timer in FormClosing, not unsubscribing from service events.
**How to avoid:** Override `OnFormClosing` to stop and dispose the timer, unsubscribe from all service events. Use `FormClosed` to null out the instance reference in Form1.
**Warning signs:** Memory growth when repeatedly opening/closing the dialog.

### Pitfall 3: Mutable Job Clone for Editor
**What goes wrong:** Editing a job in the editor dialog mutates the live JobDefinition in JobStorageService, even if the user clicks Cancel.
**Why it happens:** C# reference types. Passing the live JobDefinition to the editor means edits are immediately visible.
**How to avoid:** Deep-clone the JobDefinition before passing to the editor. On save, call JobStorageService.Save(clone). Serialize/deserialize via JSON is the simplest deep clone.
**Warning signs:** Job state changes before clicking Save; cancelling still shows changes.

### Pitfall 4: Stale Data in Job List Grid
**What goes wrong:** The job list shows outdated next-run times, enabled states, or history after changes are made via the editor or other operations.
**Why it happens:** The grid data is a snapshot that needs explicit refresh.
**How to avoid:** Refresh the grid after every mutation (save, delete, enable/disable, run now), AND on the timer tick. Subscribe to JobStorageService.JobsChanged for external changes.
**Warning signs:** Grid shows old data until timer fires or dialog is reopened.

### Pitfall 5: Import Conflict Name Collision
**What goes wrong:** Importing a job with the same name as an existing job overwrites it silently.
**Why it happens:** Not checking for name uniqueness before import.
**How to avoid:** The import preview dialog must check all import names against existing jobs. Auto-suffix " (imported)" for duplicates. Let the user see and confirm before importing.
**Warning signs:** Existing jobs disappear or change after import.

### Pitfall 6: Export Leaking Credentials
**What goes wrong:** Exported .sshjobs files contain stored credential references or passwords.
**Why it happens:** Copying the full JobDefinition without stripping credential data.
**How to avoid:** On export, clone the job and reset CredentialMode to InheritFromApp. Clear any credential-related fields. The export is explicitly designed to be safe for sharing.
**Warning signs:** Credential data visible in exported JSON files.

### Pitfall 7: Status Bar Update Performance
**What goes wrong:** Status bar update timer queries all jobs and scheduling service on every tick, causing UI lag.
**Why it happens:** Computing next-run for all jobs involves Cronos parsing each time.
**How to avoid:** Cache the "next job" computation. Only recompute when JobsChanged fires. The timer only needs to update the countdown text (simple subtraction), not recompute schedules.
**Warning signs:** UI feels sluggish with many jobs defined.

## Code Examples

### Job List Grid Column Setup
```csharp
// Source: EnvironmentDialog DataGridView setup pattern
private DataGridView CreateJobGrid()
{
    var grid = new DataGridView
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        ReadOnly = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = true,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false
    };

    grid.Columns.AddRange(
        new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", FillWeight = 25 },
        new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "On", FillWeight = 5 },
        new DataGridViewTextBoxColumn { Name = "Schedule", HeaderText = "Schedule", FillWeight = 20 },
        new DataGridViewTextBoxColumn { Name = "NextRun", HeaderText = "Next Run", FillWeight = 15 },
        new DataGridViewTextBoxColumn { Name = "LastResult", HeaderText = "Last Result", FillWeight = 20 },
        new DataGridViewTextBoxColumn { Name = "Target", HeaderText = "Target", FillWeight = 15 }
    );

    DialogTheme.StyleDataGridView(grid, _darkMode);
    return grid;
}
```

### Populating Job List Data
```csharp
// Source: Service API from Phases 1-4
private void RefreshJobList()
{
    _gridJobs.Rows.Clear();
    foreach (var job in _jobStorage.Jobs.Values.OrderBy(j => j.Name))
    {
        var scheduleDesc = job.ScheduleType switch
        {
            ScheduleType.Recurring => _schedulingService.GetDescription(job.CronExpression) ?? job.CronExpression,
            ScheduleType.OneTime => job.OneTimeScheduleUtc?.ToLocalTime().ToString("g") ?? "Not set",
            _ => "Manual only"
        };

        var nextRun = job.ScheduleType == ScheduleType.Recurring
            ? _schedulingService.GetNextRunLocal(job.CronExpression)?.ToString("g") ?? "--"
            : job.ScheduleType == ScheduleType.OneTime
                ? job.OneTimeScheduleUtc?.ToLocalTime().ToString("g") ?? "--"
                : "--";

        var lastRuns = _historyService.GetRunsForJob(job.Id, new JobRunFilter { MaxResults = 1 });
        var lastResult = lastRuns.Count > 0
            ? (lastRuns[0].Success ? "OK" : "FAIL") + $" ({lastRuns[0].CompletedUtc.ToLocalTime():g})"
            : "--";

        var isRunning = _executionService.IsJobRunning(job.Id);
        var targetDisplay = job.TargetType == JobTargetType.Folder
            ? $"[Folder] {job.TargetName}"
            : job.TargetName;

        _gridJobs.Rows.Add(job.Name, job.IsEnabled, scheduleDesc, nextRun, lastResult, targetDisplay);
        // Store job.Id in Tag for later retrieval
        _gridJobs.Rows[_gridJobs.Rows.Count - 1].Tag = job.Id;
    }
}
```

### Deep Clone for Editor
```csharp
// Source: Newtonsoft.Json already available
private static JobDefinition DeepClone(JobDefinition source)
{
    var json = JsonConvert.SerializeObject(source);
    return JsonConvert.DeserializeObject<JobDefinition>(json)!;
}
```

### Export to .sshjobs File
```csharp
// Source: PresetManager.ExportAllToFile pattern
public void ExportToFile(IReadOnlyList<JobDefinition> jobs, string filePath)
{
    var exportJobs = jobs.Select(CloneForExport).ToList();
    var document = new JobExportDocument
    {
        Version = 1,
        ExportedUtc = DateTime.UtcNow,
        Jobs = exportJobs
    };
    var json = JsonConvert.SerializeObject(document, Formatting.Indented);
    File.WriteAllText(filePath, json);
}

private static JobDefinition CloneForExport(JobDefinition source)
{
    var clone = DeepClone(source);
    clone.CredentialMode = CredentialMode.InheritFromApp;
    clone.RunningState = null; // strip execution state
    return clone;
}
```

### GZip+Base64 Clipboard Export
```csharp
// Source: PresetManager.CompressAndEncode pattern
public string ExportToString(IReadOnlyList<JobDefinition> jobs)
{
    var document = new JobExportDocument
    {
        Version = 1,
        ExportedUtc = DateTime.UtcNow,
        Jobs = jobs.Select(CloneForExport).ToList()
    };
    var json = JsonConvert.SerializeObject(document);
    byte[] raw = Encoding.UTF8.GetBytes(json);
    using var ms = new MemoryStream();
    using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
        gzip.Write(raw, 0, raw.Length);
    return Convert.ToBase64String(ms.ToArray());
}
```

### Status Bar Integration
```csharp
// Source: Form1.Designer.cs statusStrip pattern
// Add to Form1.Designer.cs or code-behind init:
private ToolStripStatusLabel _statusScheduler;

// In Form1 initialization:
_statusScheduler = new ToolStripStatusLabel
{
    Spring = false,
    TextAlign = ContentAlignment.MiddleLeft,
    Text = "Scheduler: 0 active"
};
_statusScheduler.Click += (s, e) => ShowJobListDialog();
statusStrip.Items.Add(_statusScheduler);

// Timer update (reuse refresh interval):
private void UpdateSchedulerStatusBar()
{
    var activeCount = _jobStorage.Jobs.Values.Count(j => j.IsEnabled);
    // Find next run across all enabled recurring jobs
    // ... cache this computation, recompute on JobsChanged
    _statusScheduler.Text = $"Scheduler: {activeCount} active -- Next: {nextJobName} in {timeRemaining}";
}
```

### Notification Prefix Determination
```csharp
// The JobExecutionService.JobStateChanged event includes state info.
// For distinguishing Scheduled vs Run Now, check the RunningJobInfo.IsRunNow
// which is set in RunNowAsync. The event doesn't directly carry this,
// so extend JobStateChangedEventArgs or track IsRunNow in Form1's handler
// by checking if the job was triggered via the dialog's Run Now button.

// Approach: Add an IsRunNow property to JobStateChangedEventArgs (or track in Form1):
private readonly HashSet<string> _runNowJobIds = new();

// When user clicks "Run Now" in job list dialog:
_runNowJobIds.Add(jobId);
await _executionService.RunNowAsync(jobId);

// In OnJobCompleted handler:
var prefix = _runNowJobIds.Remove(result.JobId) ? "Run Now" : "Scheduled";
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| WinForms Designer.cs | Code-only layout | Project convention | No .Designer.cs for new dialogs |
| Sync modal dialogs | Modeless with live refresh | EnvironmentDialog pattern | Job List must use Show(), not ShowDialog() |
| Single-file export | Dual format (file + clipboard) | PresetManager pattern | Both .sshjobs file and Base64 string |

**Deprecated/outdated:**
- None. All project patterns are current .NET 8.0.

## Open Questions

1. **IsRunNow event propagation**
   - What we know: RunNowAsync sets IsRunNow on the in-memory RunningJobInfo, but JobStateChangedEventArgs does not carry this flag.
   - What's unclear: Whether to extend the event args or track run-now state externally in Form1.
   - Recommendation: Track via a HashSet in Form1 or the Job List dialog. Adding to event args would change the service interface. The dialog/Form1 approach is non-invasive and matches the "UI tracks its own triggers" pattern.

2. **Drift warning visual differentiation in Job List**
   - What we know: JobDefinition.HasDriftWarning is a boolean flag.
   - What's unclear: Exact visual treatment in the grid (icon, row color, text suffix).
   - Recommendation: Use row foreground color (orange/amber) and append " [DRIFT]" to the Name cell. This is simple and visible without custom cell painting.

3. **Copy from Main Grid row selection strategy**
   - What we know: User left this to Claude's discretion.
   - What's unclear: Whether to copy checked rows (checkbox column), selected rows, or all rows.
   - Recommendation: Copy checked rows if any are checked, otherwise all rows. This matches the existing execution behavior where checked rows are the "active set."

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| Config file | SSH_Helper.Tests/SSH_Helper.Tests.csproj |
| Quick run command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Phase05" -x` |
| Full suite command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| UI-01 | Job list grid populates from service data | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobListDialog" -x` | No - Wave 0 |
| UI-02 | Job editor validates fields on save | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobEditorDialog" -x` | No - Wave 0 |
| UI-03 | Notification formatting produces correct prefixed strings | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulerNotification" -x` | No - Wave 0 |
| JMGT-05 | Export produces valid JSON with credentials stripped | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExportService" -x` | No - Wave 0 |
| JMGT-06 | Import parses both formats, handles conflicts | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~JobExportService" -x` | No - Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Phase05" -x`
- **Per wave merge:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `SSH_Helper.Tests/Services/JobExportServiceTests.cs` -- covers JMGT-05, JMGT-06 (export/import logic is pure service, highly testable)
- [ ] `SSH_Helper.Tests/UI/SchedulerNotificationTests.cs` -- covers UI-03 (notification string formatting, can test static format methods)
- [ ] `SSH_Helper.Tests/UI/JobEditorValidationTests.cs` -- covers UI-02 (validation logic extracted as static/internal methods, testable without UI thread)

Note: Job List dialog population (UI-01) tests would require WinForms STA thread (`[WinFormsFact]`). Consider testing the data assembly logic as pure methods separate from grid binding to avoid STA complexity. The grid binding itself is visual/manual verification.

## Sources

### Primary (HIGH confidence)
- **Codebase inspection** -- EnvironmentDialog.cs, SettingsDialog.cs, ExecutionDetailsDialog.cs, DialogTheme.cs, CronBuilderControl.cs, PresetManager.cs (export/import pattern), JobStorageService.cs, JobExecutionService.cs, JobHistoryService.cs, SchedulingService.cs, InputValidator.cs, ContentHasher.cs
- **Form1.Designer.cs** -- StatusStrip structure, menu structure, existing control patterns
- **Form1.cs** -- AppendOutputText pattern, SshService_OutputReceived cross-thread pattern, dialog instance tracking

### Secondary (MEDIUM confidence)
- **Phase 1-4 PLAN files** -- Service interfaces, integration patterns, decisions accumulated in STATE.md

### Tertiary (LOW confidence)
- None. All findings are from direct codebase inspection.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- no new dependencies, all from existing codebase
- Architecture: HIGH -- all patterns come from existing dialogs in the same project
- Pitfalls: HIGH -- pitfalls derived from direct code analysis of WinForms threading model and existing patterns
- Export/Import: HIGH -- directly mirrors PresetManager pattern already working in production

**Research date:** 2026-03-07
**Valid until:** Indefinite -- patterns are internal to this codebase, not external library dependent
