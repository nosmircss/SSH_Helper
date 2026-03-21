# Startup Load Time Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce full-ready startup time by removing redundant startup config loads, batching host-grid restore UI work, and deferring heavy scheduler bootstrap until after the main restore path completes.

**Architecture:** Thread one `AppConfiguration` snapshot through constructor-time startup helpers, coalesce host-grid event-driven UI refresh during startup restore into a single flush, and move scheduler service bootstrap out of the constructor into a once-only post-startup continuation. Keep changes concentrated in `Form1` and `PresetManager`, with one small helper extracted only for the host-grid batching seam.

**Tech Stack:** C# 12, .NET 8 WinForms, xUnit, Newtonsoft.Json

---

## File Structure

- Modify: `Form1.cs`
  Responsibility: load config once for startup, pass the snapshot through startup-sensitive helpers, batch host-grid startup restore UI updates, and defer heavy scheduler bootstrap until after the primary restore path completes.
- Modify: `Services/PresetManager.cs`
  Responsibility: support loading presets/folders from an already-loaded `AppConfiguration` snapshot so startup does not reread `config.json`.
- Create: `UI/HostGridRestoreBatcher.cs`
  Responsibility: coalesce scrollbar refresh, host-count refresh, and dirty-state requests while startup grid restore is in progress, then flush them once at the end.
- Modify: `SSH_Helper.Tests/Services/PresetManagerTests.cs`
  Responsibility: lock the new snapshot-loading path for preset/folder population.
- Create: `SSH_Helper.Tests/UI/HostGridRestoreBatcherTests.cs`
  Responsibility: verify batched startup restore requests collapse into one final flush.
- Reuse: `SSH_Helper.Tests/Services/SchedulingServiceMissedRunIntegrationTests.cs`
  Responsibility: protect missed-run persistence and scheduler bootstrap side effects while moving initialization timing.
- Reuse: `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs`
  Responsibility: protect scheduler execution/bootstrap behavior after deferral.
- Reuse: `SSH_Helper.Tests/UI/SchedulerNotificationTests.cs`
  Responsibility: protect scheduler status-bar formatting/visibility expectations after bootstrap timing changes.

### Task 1: Thread The Startup Configuration Snapshot

**Files:**
- Modify: `Services/PresetManager.cs`
- Modify: `Form1.cs`
- Test: `SSH_Helper.Tests/Services/PresetManagerTests.cs`

- [ ] **Step 1: Write the failing test for snapshot-based preset loading**

```csharp
[Fact]
public void Load_FromSuppliedConfiguration_PopulatesPresetsAndFoldersWithoutDiskReload()
{
    var config = new AppConfiguration
    {
        Presets = new Dictionary<string, PresetInfo>
        {
            ["Alpha"] = new PresetInfo { Commands = "show version", Folder = "Ops/Core" }
        },
        PresetFolders = new Dictionary<string, FolderInfo>
        {
            ["Ops"] = new FolderInfo { IsExpanded = true },
            ["Ops/Core"] = new FolderInfo { IsExpanded = false }
        }
    };

    var manager = new PresetManager(new ConfigurationService(_configPath));

    manager.Load(config);

    Assert.Equal("Ops/Core", manager.Get("Alpha")?.Folder);
    Assert.True(manager.Folders.ContainsKey("Ops"));
    Assert.True(manager.Folders.ContainsKey("Ops/Core"));
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetManagerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-preset-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-preset-tests\obj\
```

Expected: FAIL because `PresetManager.Load(AppConfiguration)` does not exist yet.

- [ ] **Step 3: Add snapshot-aware preset loading in `PresetManager`**

Implement a new overload and keep the existing method delegating to it:

```csharp
public void Load(AppConfiguration config)
{
    ArgumentNullException.ThrowIfNull(config);
    // existing normalization / folder synthesis logic
}

public void Load()
{
    Load(_configService.Load());
}
```

- [ ] **Step 4: Thread the startup snapshot through `Form1`**

Use the constructor's first loaded config as the startup source of truth and update the startup-only call path to pass it explicitly:

```csharp
var startupConfig = _configService.Load();
InitializeFromConfiguration(startupConfig);
RestoreWindowState(startupConfig);
```

Also update startup-sensitive helpers so they can consume the passed snapshot instead of rereading config:

- `InitializeFromConfiguration(AppConfiguration config)`
- `RefreshPresetList(..., AppConfiguration? configOverride = null)` for startup call sites
- any startup-only `Load()` calls in `Form1` that can use the same snapshot safely

- [ ] **Step 5: Re-run the targeted preset test**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetManagerTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-preset-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-preset-tests\obj\
```

Expected: PASS.

- [ ] **Step 6: Run a config/preset regression slice**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetManagerTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~ConfigurationServiceWindowStateTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-config-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-config-regression\obj\
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add Services/PresetManager.cs Form1.cs SSH_Helper.Tests/Services/PresetManagerTests.cs
git commit -m "perf: reuse startup config snapshot"
```

### Task 2: Batch Host-Grid Startup Restore UI Work

**Files:**
- Create: `UI/HostGridRestoreBatcher.cs`
- Modify: `Form1.cs`
- Test: `SSH_Helper.Tests/UI/HostGridRestoreBatcherTests.cs`

- [ ] **Step 1: Write the failing tests for batched restore requests**

```csharp
[Fact]
public void RestoreScope_CollapsesRepeatedRequestsIntoSingleFlush()
{
    int scrollbarRefreshes = 0;
    int hostCountRefreshes = 0;
    int dirtyMarks = 0;

    var batcher = new HostGridRestoreBatcher(
        onScrollbarRefresh: () => scrollbarRefreshes++,
        onHostCountRefresh: () => hostCountRefreshes++,
        onMarkDirty: () => dirtyMarks++);

    using (batcher.BeginRestoreScope())
    {
        batcher.RequestScrollbarRefresh();
        batcher.RequestScrollbarRefresh();
        batcher.RequestHostCountRefresh();
        batcher.RequestMarkDirty();
        batcher.RequestMarkDirty();
    }

    Assert.Equal(1, scrollbarRefreshes);
    Assert.Equal(1, hostCountRefreshes);
    Assert.Equal(1, dirtyMarks);
}
```

- [ ] **Step 2: Run the targeted test to verify it fails**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~HostGridRestoreBatcherTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-grid-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-grid-tests\obj\
```

Expected: FAIL because the helper does not exist yet.

- [ ] **Step 3: Implement `HostGridRestoreBatcher`**

Create a focused helper that supports:

- nested restore scopes
- deferred scrollbar refresh requests
- deferred host-count refresh requests
- deferred dirty-mark requests
- a single final flush when the outermost scope exits

- [ ] **Step 4: Wire the batcher into `Form1`'s host-grid mutation paths**

Update the startup/bulk restore path so these operations request batched work instead of immediately doing it on every row or column change:

- `SetupDataGridViewScrollbars()` event lambdas that currently call `UpdateDataGridViewScrollbars()`
- `Dgv_Variables_RowsAdded(...)`
- `Dgv_Variables_RowsRemoved(...)`
- `Dgv_Variables_ColumnRemoved(...)`
- any startup restore path that would otherwise mark the grid dirty or recompute counts repeatedly

The final flush after restore should still leave:

- correct row heights
- correct host count text
- correct hosts-file indicator
- `_csvDirty == false` for restored state

- [ ] **Step 5: Wrap startup grid population in a restore scope**

Use the batcher around both startup restore entry points:

- `RestoreApplicationState(...)`
- `LoadEnvironmentIntoGrid(...)`

The scope must flush once after all rows/columns are populated and before post-startup work continues.

- [ ] **Step 6: Re-run the targeted grid batching tests**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~HostGridRestoreBatcherTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-grid-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-grid-tests\obj\
```

Expected: PASS.

- [ ] **Step 7: Run a focused host-grid regression slice**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~ApplyFontSettingsTests|FullyQualifiedName~JobEditorDialogHostGridParityTests|FullyQualifiedName~HostGridRestoreBatcherTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-grid-regression\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-grid-regression\obj\
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add UI/HostGridRestoreBatcher.cs Form1.cs SSH_Helper.Tests/UI/HostGridRestoreBatcherTests.cs
git commit -m "perf: batch startup host grid restore updates"
```

### Task 3: Defer Heavy Scheduler Bootstrap Until After Startup Restore

**Files:**
- Modify: `Form1.cs`
- Test: `SSH_Helper.Tests/Services/SchedulingServiceMissedRunIntegrationTests.cs`
- Test: `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs`
- Test: `SSH_Helper.Tests/UI/SchedulerNotificationTests.cs`

- [ ] **Step 1: Lock the current scheduler behavior with the focused regression slice**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SchedulerNotificationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-scheduler-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-scheduler-tests\obj\
```

Expected: PASS.

- [ ] **Step 2: Move heavy scheduler service initialization out of the constructor**

Remove the direct constructor call to `InitializeSchedulerServices()` and run it from a once-only post-startup continuation triggered after the main restore path is complete, using the existing shown/idle startup path rather than the constructor.

- [ ] **Step 3: Keep scheduler UI shell behavior intact**

If `InitializeSchedulerStatusBar()` can remain lightweight, leave it in the constructor so the menu/status-strip shell still exists immediately. If it depends on live scheduler services, split it into:

- lightweight UI shell creation in the constructor
- post-bootstrap refresh after deferred scheduler initialization completes

- [ ] **Step 4: Preserve scheduler semantics after deferral**

Confirm the deferred path still performs the same work once it runs:

- `JobStorageService.Load()`
- job execution crash recovery / initialization
- missed-run recording
- scheduler timer start
- status bar refresh

The deferred path must be guarded so it only runs once.

- [ ] **Step 5: Re-run the focused scheduler regression slice**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SchedulerNotificationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-scheduler-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-scheduler-tests\obj\
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add Form1.cs
git commit -m "perf: defer scheduler bootstrap until post-startup"
```

### Task 4: Final Verification And Task Tracker Update

**Files:**
- Modify: `tasks/todo.md`
- Verify: `SSH_Helper.sln`

- [ ] **Step 1: Run the full focused startup regression suite**

Run:

```powershell
dotnet test SSH_Helper.Tests\SSH_Helper.Tests.csproj -nologo --filter "FullyQualifiedName~PresetManagerTests|FullyQualifiedName~PresetManagerFolderBaseEnvironmentTests|FullyQualifiedName~ConfigurationServiceWindowStateTests|FullyQualifiedName~HostGridUtilitiesTests|FullyQualifiedName~ApplyFontSettingsTests|FullyQualifiedName~JobEditorDialogHostGridParityTests|FullyQualifiedName~HostGridRestoreBatcherTests|FullyQualifiedName~SchedulingServiceMissedRunIntegrationTests|FullyQualifiedName~JobExecutionServiceTests|FullyQualifiedName~SchedulerNotificationTests" -p:UseAppHost=false -p:BaseOutputPath=artifacts\startup-load-time-final-tests\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-final-tests\obj\
```

Expected: PASS.

- [ ] **Step 2: Run the solution build**

Run:

```powershell
dotnet build SSH_Helper.sln -nologo -p:BaseOutputPath=artifacts\startup-load-time-build\bin\ -p:BaseIntermediateOutputPath=artifacts\startup-load-time-build\obj\
```

Expected: PASS with 0 errors.

- [ ] **Step 3: Capture startup timing evidence**

Manual verification:

- launch the updated app against the same representative config used before the change
- compare observed time-to-usable-ready against the baseline from the pre-change build
- if exact measurement is not practical in the execution environment, state that explicitly in `tasks/todo.md`

- [ ] **Step 4: Update `tasks/todo.md`**

Record:

- what changed
- what tests/build commands passed
- whether startup timing evidence was captured or not

- [ ] **Step 5: Commit**

```powershell
git add tasks/todo.md
git commit -m "docs: record startup load time verification"
```
