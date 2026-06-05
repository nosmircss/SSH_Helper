# Flow Canvas Per-Preset Layout Mode — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give each preset an explicit, visible layout mode — **Auto-flow** (canvas re-tidies itself) or **Manual** (the user's arrangement is preserved across edits and reopens) — with a global default, replacing the global "Auto-layout on edits" toggle.

**Architecture:** A per-preset `LayoutMode` (on `PresetInfo`) plus a global `DefaultLayoutMode` (on `WindowState`) become the single source of truth. The C# load path resolves the effective mode and decides whether to keep saved positions (prefix-safe tuple match), near-neighbor-place new blocks, or clean-reflow. The React store gates `reflowLayout` on the mode, places new blocks near their neighbor, and surfaces a toolbar toggle + a settings default.

**Tech Stack:** C# .NET 8 / WinForms (xUnit + FluentAssertions + Moq), React + TypeScript + Zustand (`@xyflow/react`), Vitest, WebView2 JSON bridge.

**Spec:** `docs/superpowers/specs/2026-06-05-flow-canvas-layout-mode-design.md`

**Deviation log (kept current during execution):**
- 2026-06-05, Task 1.2: the legacy `SaveAndLoad_FlowCanvasAutoReflow_RoundTrips` test was **deleted** in Task 1.2 (the migration nulls `FlowCanvasAutoReflow` on every load, so the round-trip assertion is obsolete). The plan originally deferred this to Task 5.3 — Task 5.3 Step 1 is updated below accordingly. `DefaultLayoutMode` config persistence is already covered by `ConfigurationServiceLayoutModeTests.DefaultLayoutMode_roundTrips`.

**Design decisions (locked):**
- Mode is per-preset; a global default (Auto-flow) applies to unset presets.
- Auto-flow = transient positions, always re-lays-out on reopen/edit. Manual = positions preserved.
- Switching INTO Manual freezes current on-screen positions. Switching is explicit (no auto-switch on drag).
- New blocks in Manual land near their neighbor, nudged off overlaps, briefly highlighted.
- Preservation across structural edits is **prefix-safe**: pure moves + end-appends are preserved by `(stepPath:blockType)` tuple match; a mid-body edit that breaks the saved-tuple-subset falls back to a clean reflow (never mis-maps).
- The new mode **replaces** `autoReflowEnabled` / `FlowCanvasAutoReflow`; the old value migrates to `DefaultLayoutMode`.

**Wire contract (C# ↔ React):**
- `load-graph` gains `layoutMode: 'auto'|'manual'`, `layoutAction: 'reflow'|'keep'`, `newNodeIds: string[]`.
- New outgoing `set-layout-mode` message: `{ type:'set-layout-mode', mode:'auto'|'manual' }`.
- `defaultLayoutMode: 'auto'|'manual'` rides the existing `layout-save`/`layout-restore` channel.
- `layout-autosave` `positions` entries gain `stepPath` + `blockType` (so the C# tuple match has stable keys).

---

## Phase 1 — C# data model, global default, migration, persistence

5 files. No behavior change yet; everything compiles and persists.

### Task 1.1: `LayoutMode` enum + `PresetInfo.LayoutMode`

**Files:**
- Create: `Models/LayoutMode.cs`
- Modify: `Models/PresetInfo.cs`
- Test: `SSH_Helper.Tests/Models/PresetInfoLayoutModeTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// SSH_Helper.Tests/Models/PresetInfoLayoutModeTests.cs
using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class PresetInfoLayoutModeTests
{
    [Fact]
    public void LayoutMode_defaults_to_null_meaning_inherit_default()
    {
        new PresetInfo().LayoutMode.Should().BeNull();
    }

    [Fact]
    public void Clone_copies_layout_mode()
    {
        var p = new PresetInfo { Commands = "print: hi", LayoutMode = LayoutMode.Manual };
        p.Clone().LayoutMode.Should().Be(LayoutMode.Manual);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~PresetInfoLayoutModeTests`
Expected: FAIL — `LayoutMode` type / `PresetInfo.LayoutMode` does not exist.

- [ ] **Step 3: Create the enum**

```csharp
// Models/LayoutMode.cs
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SSH_Helper.Models
{
    /// <summary>
    /// Per-preset (and global-default) Flow Canvas layout behavior.
    /// AutoFlow: the canvas re-lays-out on edits/reopen (positions are transient).
    /// Manual: the user's arrangement is preserved and never auto-reflowed.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum LayoutMode
    {
        AutoFlow,
        Manual,
    }
}
```

- [ ] **Step 4: Add the field + Clone in `PresetInfo.cs`**

In `Models/PresetInfo.cs`, add the property after `CanvasLayout` (line 48):

```csharp
        /// <summary>
        /// Per-preset Flow Canvas layout mode. Null = inherit the global default
        /// (WindowState.FlowCanvasDefaultLayoutMode, itself defaulting to AutoFlow).
        /// </summary>
        public LayoutMode? LayoutMode { get; set; }
```

And add to the `Clone()` initializer (after `CanvasLayout = CanvasLayout?.Clone()`):

```csharp
                CanvasLayout = CanvasLayout?.Clone(),
                LayoutMode = LayoutMode,
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~PresetInfoLayoutModeTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Models/LayoutMode.cs Models/PresetInfo.cs SSH_Helper.Tests/Models/PresetInfoLayoutModeTests.cs
git commit -m "feat(flow-canvas): add per-preset LayoutMode model

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.2: Global `DefaultLayoutMode` + migration from `FlowCanvasAutoReflow`

**Files:**
- Modify: `Models/AppConfiguration.cs:519-520` (WindowState)
- Modify: `Services/ConfigurationService.cs` (migration on load)
- Test: `SSH_Helper.Tests/Services/ConfigurationServiceLayoutModeTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// SSH_Helper.Tests/Services/ConfigurationServiceLayoutModeTests.cs
using System.IO;
using FluentAssertions;
using Newtonsoft.Json;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class ConfigurationServiceLayoutModeTests
{
    private static string TempConfig() =>
        Path.Combine(Path.GetTempPath(), "sshhelper-layoutmode-" + Path.GetRandomFileName(), "config.json");

    [Fact]
    public void Legacy_autoReflow_true_migrates_to_AutoFlow_default()
    {
        var path = TempConfig();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Hand-write a config carrying only the legacy field.
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            WindowState = new { FlowCanvasAutoReflow = true }
        }));

        var loaded = new ConfigurationService(path).Load();

        loaded.WindowState.FlowCanvasDefaultLayoutMode.Should().Be(LayoutMode.AutoFlow);
    }

    [Fact]
    public void Legacy_autoReflow_false_migrates_to_Manual_default()
    {
        var path = TempConfig();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            WindowState = new { FlowCanvasAutoReflow = false }
        }));

        var loaded = new ConfigurationService(path).Load();

        loaded.WindowState.FlowCanvasDefaultLayoutMode.Should().Be(LayoutMode.Manual);
    }

    [Fact]
    public void DefaultLayoutMode_roundTrips()
    {
        var path = TempConfig();
        var svc = new ConfigurationService(path);
        svc.Update(c => c.WindowState.FlowCanvasDefaultLayoutMode = LayoutMode.Manual);

        new ConfigurationService(path).Load().WindowState.FlowCanvasDefaultLayoutMode
            .Should().Be(LayoutMode.Manual);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~ConfigurationServiceLayoutModeTests`
Expected: FAIL — `FlowCanvasDefaultLayoutMode` does not exist.

- [ ] **Step 3: Add the field to `WindowState`**

In `Models/AppConfiguration.cs`, replace the `FlowCanvasAutoReflow` line (520) so the legacy field stays readable for migration but is marked obsolete, and add the new field:

```csharp
        public bool? FlowCanvasCompactComments { get; set; }

        /// <summary>
        /// LEGACY: replaced by FlowCanvasDefaultLayoutMode. Still deserialized so the value
        /// can be migrated on load; never written back. Remove after one release cycle.
        /// </summary>
        public bool? FlowCanvasAutoReflow { get; set; }

        /// <summary>
        /// Global default Flow Canvas layout mode for presets that have not set their own.
        /// Null = AutoFlow (the historical default).
        /// </summary>
        public LayoutMode? FlowCanvasDefaultLayoutMode { get; set; }
```

- [ ] **Step 4: Add the migration in `ConfigurationService.Load()`**

Find the post-deserialize section of `Load()` (where the config object exists and other legacy migrations run — search for where `config.WindowState` is known non-null, e.g. just before returning the loaded config). Add:

```csharp
            // Migrate the legacy global "Auto-layout on edits" toggle into the new default layout mode.
            // autoReflow ON  -> AutoFlow default; OFF -> Manual default. Runs once: after this the
            // new field is set and FlowCanvasAutoReflow is ignored.
            if (config.WindowState != null
                && config.WindowState.FlowCanvasDefaultLayoutMode == null
                && config.WindowState.FlowCanvasAutoReflow.HasValue)
            {
                config.WindowState.FlowCanvasDefaultLayoutMode =
                    config.WindowState.FlowCanvasAutoReflow.Value ? LayoutMode.AutoFlow : LayoutMode.Manual;
                config.WindowState.FlowCanvasAutoReflow = null;
            }
```

> NOTE: if `Load()` can return early on a corrupt file before this point, place the migration after `WindowState` is guaranteed non-null. If `WindowState` may be null on a minimal config, the `config.WindowState != null` guard already covers it; the round-trip test exercises the normal path.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~ConfigurationServiceLayoutModeTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Models/AppConfiguration.cs Services/ConfigurationService.cs SSH_Helper.Tests/Services/ConfigurationServiceLayoutModeTests.cs
git commit -m "feat(flow-canvas): add global DefaultLayoutMode + migrate legacy autoReflow

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 1.3: `PresetManager.UpdateLayoutMode`

**Files:**
- Modify: `Services/PresetManager.cs:223` (next to `UpdateCanvasLayout`)
- Test: `SSH_Helper.Tests/Services/PresetManagerLayoutModeTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// SSH_Helper.Tests/Services/PresetManagerLayoutModeTests.cs
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class PresetManagerLayoutModeTests
{
    [Fact]
    public void UpdateLayoutMode_sets_mode_on_existing_preset()
    {
        var mgr = new PresetManager();
        mgr.AddOrUpdate("p1", "print: hi");

        mgr.UpdateLayoutMode("p1", LayoutMode.Manual);

        mgr.Get("p1")!.LayoutMode.Should().Be(LayoutMode.Manual);
    }

    [Fact]
    public void UpdateLayoutMode_unknown_preset_is_noop()
    {
        var mgr = new PresetManager();
        var act = () => mgr.UpdateLayoutMode("missing", LayoutMode.Manual);
        act.Should().NotThrow();
    }
}
```

> If `PresetManager`'s real constructor/`AddOrUpdate` signatures differ, mirror the arrangement used by the existing PresetManager tests in `SSH_Helper.Tests/Services/` (grep for `new PresetManager(`). Keep the assertion identical.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~PresetManagerLayoutModeTests`
Expected: FAIL — `UpdateLayoutMode` not defined.

- [ ] **Step 3: Implement next to `UpdateCanvasLayout` (after line 230)**

```csharp
        /// <summary>
        /// Updates the per-preset layout mode without triggering PresetsChanged.
        /// Pass null to clear (inherit the global default).
        /// </summary>
        public void UpdateLayoutMode(string name, LayoutMode? mode)
        {
            if (_presets.TryGetValue(name, out var preset))
            {
                preset.LayoutMode = mode;
                PersistToConfig();
            }
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~PresetManagerLayoutModeTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Services/PresetManager.cs SSH_Helper.Tests/Services/PresetManagerLayoutModeTests.cs
git commit -m "feat(flow-canvas): PresetManager.UpdateLayoutMode

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase 2 — Persistence contract + prefix-safe tuple merge (C# + autosave)

5 files. Teaches save/load to carry `(stepPath, blockType)` per position and to do a prefix-safe partial merge.

### Task 2.1: Persist `stepPath` + `blockType` per saved position

**Files:**
- Modify: `Models/CanvasLayoutData.cs:72-76` (`NodePosition`)
- Modify: `FlowCanvas/src/utils/layoutAutosave.ts:42-47`
- Modify: `Form1.cs:2491-2498` (`ApplyLayoutAutosave` positions loop)
- Test: `SSH_Helper.Tests/Models/NodePositionTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// SSH_Helper.Tests/Models/NodePositionTests.cs
using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class NodePositionTests
{
    [Fact]
    public void Clone_copies_stepPath_and_blockType()
    {
        var layout = new CanvasLayoutData();
        layout.Positions["node-0"] = new NodePosition { X = 1, Y = 2, StepPath = "steps/0", BlockType = "print" };

        var clone = layout.Clone();

        var p = clone.Positions["node-0"];
        p.X.Should().Be(1);
        p.StepPath.Should().Be("steps/0");
        p.BlockType.Should().Be("print");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~NodePositionTests`
Expected: FAIL — `NodePosition.StepPath` does not exist.

- [ ] **Step 3: Extend `NodePosition` and its clone**

In `Models/CanvasLayoutData.cs`, replace the `NodePosition` class (lines 72-76):

```csharp
    public class NodePosition
    {
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>Stable structural key (e.g. "steps/2/then/0"); null for pre-migration layouts.</summary>
        public string? StepPath { get; set; }

        /// <summary>Block type at save time; pairs with StepPath to form the match tuple.</summary>
        public string? BlockType { get; set; }
    }
```

In `CanvasLayoutData.Clone()` (line 45-47), copy the new fields:

```csharp
                Positions = Positions.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new NodePosition { X = kvp.Value.X, Y = kvp.Value.Y, StepPath = kvp.Value.StepPath, BlockType = kvp.Value.BlockType }),
```

- [ ] **Step 4: Run the C# test to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~NodePositionTests`
Expected: PASS

- [ ] **Step 5: Send `stepPath`+`blockType` from React autosave**

In `FlowCanvas/src/utils/layoutAutosave.ts`, replace the `else` block (lines 42-47) that builds `positions[node.id]`:

```typescript
    } else {
      const data = node.data as Record<string, unknown> | undefined;
      const props = data?.props as Record<string, unknown> | undefined;
      positions[node.id] = {
        x: node.position?.x ?? 0,
        y: node.position?.y ?? 0,
        stepPath: typeof props?._stepPath === 'string' ? props._stepPath : undefined,
        blockType: typeof data?.blockType === 'string' ? data.blockType : undefined,
      };
    }
```

Update the `positions` declaration type (line 22):

```typescript
  const positions: Record<string, { x: number; y: number; stepPath?: string; blockType?: string }> = {};
```

- [ ] **Step 6: Store the new fields in `ApplyLayoutAutosave`**

In `Form1.cs`, replace the positions loop body (lines 2493-2497):

```csharp
                    layout.Positions[prop.Name] = new Models.NodePosition
                    {
                        X = prop.Value["x"]?.Value<double>() ?? 0,
                        Y = prop.Value["y"]?.Value<double>() ?? 0,
                        StepPath = prop.Value["stepPath"]?.ToString(),
                        BlockType = prop.Value["blockType"]?.ToString(),
                    };
```

- [ ] **Step 7: Type-check React + build C#**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: no errors.
Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: build succeeds.

- [ ] **Step 8: Commit**

```bash
git add Models/CanvasLayoutData.cs FlowCanvas/src/utils/layoutAutosave.ts Form1.cs SSH_Helper.Tests/Models/NodePositionTests.cs
git commit -m "feat(flow-canvas): persist stepPath+blockType per saved node position

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 2.2: Extract shared aux-merge + add `TryMergeLayoutByTuple`

**Files:**
- Modify: `Services/FlowCanvasBridge.cs:5329` (`MergeLayout` + new method)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTupleMergeTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// SSH_Helper.Tests/Services/FlowCanvasBridgeTupleMergeTests.cs
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Models;
using SSH_Helper.Services;
using Xunit;

namespace SSH_Helper.Tests.Services;

public class FlowCanvasBridgeTupleMergeTests
{
    private static JObject Block(string id, string stepPath, string blockType, double x = 0, double y = 0) => new()
    {
        ["id"] = id,
        ["type"] = "block",
        ["position"] = new JObject { ["x"] = x, ["y"] = y },
        ["data"] = new JObject
        {
            ["blockType"] = blockType,
            ["props"] = new JObject { ["_stepPath"] = stepPath },
        },
    };

    private static CanvasLayoutData LayoutWith(params (string id, string sp, string bt, double x, double y)[] entries)
    {
        var l = new CanvasLayoutData();
        foreach (var e in entries)
            l.Positions[e.id] = new NodePosition { X = e.x, Y = e.y, StepPath = e.sp, BlockType = e.bt };
        return l;
    }

    [Fact]
    public void Append_at_end_is_safe_keeps_existing_returns_new_id()
    {
        // Saved A,B. Current A,B,C (C appended).
        var nodes = new JArray { Block("node-0", "steps/0", "send"), Block("node-1", "steps/1", "print"), Block("node-2", "steps/2", "print") };
        var layout = LayoutWith(("node-0", "steps/0", "send", 100, 200), ("node-1", "steps/1", "print", 150, 400));

        var (safe, newIds) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeTrue();
        newIds.Should().BeEquivalentTo(new[] { "node-2" });
        nodes[0]["position"]!["x"]!.Value<double>().Should().Be(100); // A kept
        nodes[1]["position"]!["y"]!.Value<double>().Should().Be(400); // B kept
    }

    [Fact]
    public void Mid_body_insert_is_unsafe()
    {
        // Saved A(steps/0),B(steps/1),C(steps/2). Current inserts X at steps/1, shifting B->2, C->3.
        var nodes = new JArray
        {
            Block("node-0", "steps/0", "send"),
            Block("node-1", "steps/1", "wait"),   // new, took steps/1
            Block("node-2", "steps/2", "print"),  // was B
            Block("node-3", "steps/3", "print"),  // was C
        };
        var layout = LayoutWith(
            ("node-0", "steps/0", "send", 1, 1),
            ("node-1", "steps/1", "print", 2, 2),   // saved B was print@steps/1 -> tuple now missing
            ("node-2", "steps/2", "print", 3, 3));

        var (safe, _) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeFalse("a shifted step path breaks the saved-tuple subset; clean reflow instead of mis-mapping");
    }

    [Fact]
    public void Pure_move_no_structural_change_is_safe_no_new_ids()
    {
        var nodes = new JArray { Block("node-0", "steps/0", "send"), Block("node-1", "steps/1", "print") };
        var layout = LayoutWith(("node-0", "steps/0", "send", 10, 10), ("node-1", "steps/1", "print", 20, 20));

        var (safe, newIds) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeTrue();
        newIds.Should().BeEmpty();
        nodes[1]["position"]!["x"]!.Value<double>().Should().Be(20);
    }

    [Fact]
    public void PreMigration_layout_without_stepPath_is_unsafe()
    {
        var nodes = new JArray { Block("node-0", "steps/0", "send") };
        var layout = new CanvasLayoutData();
        layout.Positions["node-0"] = new NodePosition { X = 5, Y = 5 }; // no StepPath/BlockType

        var (safe, _) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);

        safe.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~FlowCanvasBridgeTupleMergeTests`
Expected: FAIL — `TryMergeLayoutByTuple` not defined.

- [ ] **Step 3: Refactor `MergeLayout` to extract aux-merge, then add the tuple method**

In `Services/FlowCanvasBridge.cs`, change `MergeLayout` (line 5329) so the position loop and the comment/disabled/expanded handling are separable. Keep `MergeLayout`'s public behavior identical; extract the non-position work into a private helper and call it from both methods. Replace the body of `MergeLayout` so that after the position loop it calls the shared helper:

```csharp
        public static void MergeLayout(JArray nodes, CanvasLayoutData layout)
        {
            // Override positions for existing nodes (id-keyed; valid because the caller only uses
            // this path when the structure hash matches exactly).
            foreach (var node in nodes)
            {
                var id = node["id"]?.ToString();
                if (id != null && layout.Positions.TryGetValue(id, out var pos))
                    node["position"] = new JObject { ["x"] = pos.X, ["y"] = pos.Y };
            }
            MergeAuxiliaryLayout(nodes, layout);
        }
```

Move the existing comment-merge + disabled + expanded blocks (current lines 5340-end of `MergeLayout`) into a new private method `MergeAuxiliaryLayout(JArray nodes, CanvasLayoutData layout)` — keep that code verbatim (the disabled/expanded marking loop and the comment reconcile/append loop), just relocated. Then add the new public method:

```csharp
        /// <summary>
        /// Prefix-safe partial merge for Manual mode when the structure has changed.
        /// Matches saved positions to nodes by (stepPath:blockType) tuple. Returns Safe=true only
        /// when every saved tuple still exists (pure move / end-append); in that case applies the
        /// saved positions and returns the ids of genuinely-new blocks (no saved tuple) for the
        /// caller to near-neighbor place. Returns Safe=false (mid-body edit / removal / pre-migration
        /// data) so the caller clean-reflows instead of mis-mapping.
        /// </summary>
        public static (bool Safe, System.Collections.Generic.List<string> NewNodeIds) TryMergeLayoutByTuple(
            JArray nodes, CanvasLayoutData layout)
        {
            var savedByTuple = new System.Collections.Generic.Dictionary<string, NodePosition>(StringComparer.Ordinal);
            foreach (var p in layout.Positions.Values)
            {
                if (string.IsNullOrEmpty(p.StepPath)) continue; // pre-migration entry: no stable key
                savedByTuple[$"{p.StepPath}:{p.BlockType}"] = p;
            }
            if (savedByTuple.Count == 0) return (false, new System.Collections.Generic.List<string>());

            var currentTuples = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var n in nodes)
            {
                if (n["id"]?.ToString() == "__start__" || n["type"]?.ToString() == "comment") continue;
                var bt = n["data"]?["blockType"]?.ToString() ?? "";
                var sp = n["data"]?["props"]?["_stepPath"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(sp)) currentTuples.Add($"{sp}:{bt}");
            }

            // Safe only if every saved tuple survives (move/append). A vanished tuple = mid-edit/removal.
            foreach (var t in savedByTuple.Keys)
                if (!currentTuples.Contains(t)) return (false, new System.Collections.Generic.List<string>());

            var newIds = new System.Collections.Generic.List<string>();
            foreach (var n in nodes)
            {
                var id = n["id"]?.ToString();
                if (id == null || id == "__start__" || n["type"]?.ToString() == "comment") continue;
                var bt = n["data"]?["blockType"]?.ToString() ?? "";
                var sp = n["data"]?["props"]?["_stepPath"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(sp) && savedByTuple.TryGetValue($"{sp}:{bt}", out var pos))
                    n["position"] = new JObject { ["x"] = pos.X, ["y"] = pos.Y };
                else
                    newIds.Add(id); // genuinely new block -> React places it near its neighbor
            }

            MergeAuxiliaryLayout(nodes, layout);
            return (true, newIds);
        }
```

- [ ] **Step 4: Run the tuple tests + the existing persistence tests to verify all pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~FlowCanvasBridge`
Expected: PASS (both `FlowCanvasBridgeTupleMergeTests` and the existing `FlowCanvasBridgeLayoutPersistenceTests` — the refactor must not change `MergeLayout` behavior).

- [ ] **Step 5: Commit**

```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTupleMergeTests.cs
git commit -m "feat(flow-canvas): prefix-safe TryMergeLayoutByTuple for Manual reopen

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase 3 — Load path: resolve mode + decide action (C# + bridge contract)

4 files. Wires the effective-mode resolution into `LoadGraph` and the React `load-graph` handler. Reflow gating + near-neighbor come in Phase 4, so until then the manual `keep` path keeps positions and leaves new blocks at their default spot (verified by Phase 4).

### Task 3.1: `LoadGraph` carries mode + action + new ids

**Files:**
- Modify: `UI/FlowCanvasForm.cs:332-335` (`LoadGraph`)
- Test: `SSH_Helper.Tests/UI/FlowCanvasFormLayoutTests.cs` (add a case)

- [ ] **Step 1: Add the failing test**

Append to the existing `SSH_Helper.Tests/UI/FlowCanvasFormLayoutTests.cs` (mirror its existing `[WinFormsFact]` style and however it captures `SendMessage`/pending payloads — grep the file for how it asserts on outbound JSON). The new test asserts the overload serializes the new fields:

```csharp
    [WinFormsFact]
    public void LoadGraph_includes_layoutMode_action_and_newNodeIds()
    {
        using var form = NewFormForTest(); // use this file's existing helper to construct the form
        form.LoadGraph(new JArray(), new JArray(), LayoutMode.Manual, "keep", new JArray { "node-2" });

        var payload = LastOutboundMessage(form); // use this file's existing capture helper
        payload["type"]!.ToString().Should().Be("load-graph");
        payload["layoutMode"]!.ToString().Should().Be("manual");
        payload["layoutAction"]!.ToString().Should().Be("keep");
        ((JArray)payload["newNodeIds"]!).Select(t => t.ToString()).Should().BeEquivalentTo(new[] { "node-2" });
    }
```

> If the file has no `NewFormForTest`/`LastOutboundMessage` helpers, follow the construction + capture pattern already used by its other tests (the form queues outbound JSON in `_pendingMessages` before `ready`). Keep the four assertions.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~FlowCanvasFormLayoutTests`
Expected: FAIL — `LoadGraph` has no 5-arg overload.

- [ ] **Step 3: Replace `LoadGraph` (lines 326-335)**

```csharp
        /// <summary>
        /// Sends a load-graph message to display nodes and edges.
        /// <paramref name="layoutMode"/> is the active preset's effective mode (drives the toolbar
        /// toggle + future reflow gating). <paramref name="layoutAction"/> is what to do on THIS load:
        /// "reflow" (auto presets, or manual presets whose saved layout can't be safely reused) or
        /// "keep" (positions already merged; near-neighbor the ids in <paramref name="newNodeIds"/>).
        /// </summary>
        public void LoadGraph(object nodes, object edges, LayoutMode layoutMode, string layoutAction, object newNodeIds)
        {
            SendMessage(new
            {
                type = "load-graph",
                nodes,
                edges,
                layoutMode = layoutMode == LayoutMode.Manual ? "manual" : "auto",
                layoutAction,
                newNodeIds,
            });
        }

        /// <summary>Back-compat overload: auto-flow, clean reflow, no new ids.</summary>
        public void LoadGraph(object nodes, object edges)
            => LoadGraph(nodes, edges, LayoutMode.AutoFlow, "reflow", new JArray());
```

Add `using SSH_Helper.Models;` to the file if not already present (for `LayoutMode`).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~FlowCanvasFormLayoutTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add UI/FlowCanvasForm.cs SSH_Helper.Tests/UI/FlowCanvasFormLayoutTests.cs
git commit -m "feat(flow-canvas): LoadGraph carries layoutMode/action/newNodeIds

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 3.2: `LoadCurrentScriptIntoCanvas` resolves the effective mode + action

**Files:**
- Modify: `Form1.cs:6824-6865`

- [ ] **Step 1: Replace the body of `LoadCurrentScriptIntoCanvas` (the try-block, lines 6837-6864)**

```csharp
            try
            {
                var bridge = new FlowCanvasBridge();
                var (nodes, edges) = bridge.TextToGraph(scriptText);

                // Resolve the effective mode: preset override, else the global default (else AutoFlow).
                var ws = _configService.GetCurrent().WindowState;
                var defaultMode = ws?.FlowCanvasDefaultLayoutMode ?? Models.LayoutMode.AutoFlow;
                var effectiveMode = defaultMode;
                var layoutAction = "reflow";
                var newNodeIds = new JArray();

                if (!string.IsNullOrEmpty(_activePresetName))
                {
                    var preset = _presetManager.Get(_activePresetName);
                    effectiveMode = preset?.LayoutMode ?? defaultMode;
                    var layout = preset?.CanvasLayout;

                    // Only Manual presets preserve positions. Auto-flow always re-lays-out.
                    if (effectiveMode == Models.LayoutMode.Manual && layout != null && layout.Positions.Count > 0)
                    {
                        var currentHash = FlowCanvasBridge.ComputeStructureHash(nodes);
                        if (string.Equals(currentHash, layout.StructureHash, StringComparison.Ordinal))
                        {
                            FlowCanvasBridge.MergeLayout(nodes, layout); // identical structure: id-keyed
                            layoutAction = "keep";
                        }
                        else
                        {
                            var (safe, ids) = FlowCanvasBridge.TryMergeLayoutByTuple(nodes, layout);
                            if (safe)
                            {
                                layoutAction = "keep";
                                foreach (var id in ids) newNodeIds.Add(id);
                            }
                            // unsafe -> layoutAction stays "reflow" (clean), never mis-maps
                        }
                    }
                }

                _flowCanvasForm.LoadGraph(nodes, edges, effectiveMode, layoutAction, newNodeIds);
            }
            catch
            {
                // Silently fail — canvas will show empty state
            }
```

- [ ] **Step 2: Build C#**

Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Form1.cs
git commit -m "feat(flow-canvas): resolve effective layout mode on canvas load

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 3.3: React `load-graph` handler keys off mode/action

**Files:**
- Modify: `FlowCanvas/src/communication-message-types.ts:38` (add outgoing `setLayoutMode`; update the header comment)
- Modify: `FlowCanvas/src/stores/messageBridge.ts:129-170`
- Test: deferred to Phase 4 (`layoutMode.test.ts`) once the store actions exist.

- [ ] **Step 1: Add the outgoing message type**

In `FlowCanvas/src/communication-message-types.ts`, add to `outgoing` (after `prefSave: 'pref-save',`):

```typescript
    prefSave: 'pref-save',
    setLayoutMode: 'set-layout-mode',
```

And update the top-of-file comment block to document the new `load-graph` shape:

```typescript
/**
 * Note: the host also sends a `load-graph` message (handled directly in messageBridge.ts):
 *   { type: 'load-graph', nodes, edges, layoutMode?: 'auto'|'manual',
 *     layoutAction?: 'reflow'|'keep', newNodeIds?: string[] }
 * layoutMode  = the active preset's mode (drives the toolbar toggle + reflow gating).
 * layoutAction= 'reflow' runs computeHierarchicalLayout; 'keep' preserves merged positions and
 *               near-neighbor-places the ids in newNodeIds.
 */
```

- [ ] **Step 2: Replace the `load-graph` handler block (messageBridge.ts lines 135-147)**

Replace the `hasUserLayout` branch with mode/action handling. (Note: `restoreLayoutMode`, `placeNewBlocksNearNeighbors`, and the `layoutMode` store field are added in Phase 4; import/add them there. Until Phase 4 lands, this file will not type-check on its own — Phase 4 Task 4.2 + 4.3 complete it. Implement Phase 3 Task 3.3 and Phase 4 Tasks 4.1–4.3 together before running the React build.)

```typescript
        // Mode-aware layout: the host tells us the preset's mode and what to do on this load.
        const layoutMode = (msg as { layoutMode?: string }).layoutMode === 'manual' ? 'manual' : 'auto';
        const layoutAction = (msg as { layoutAction?: string }).layoutAction === 'keep' ? 'keep' : 'reflow';
        const newNodeIds: string[] = Array.isArray((msg as { newNodeIds?: unknown[] }).newNodeIds)
          ? ((msg as { newNodeIds: unknown[] }).newNodeIds).map(String)
          : [];
        store.getState().restoreLayoutMode(layoutMode); // host-driven, no echo

        const s = store.getState();
        const sizing = { blockWidth: s.blockWidth, density: s.density, textScale: s.textScale, compactComments: s.compactCommentsEnabled };
        if (layoutAction === 'reflow') {
          // computeHierarchicalLayout already places anchored comments (band-aware) + reserves space.
          store.getState().setNodes(computeHierarchicalLayout(s.nodes, s.edges, sizing));
        } else {
          // Manual keep: positions already merged by the host. Place only the new blocks near their
          // neighbor, then (re-)anchor comments above their block/band.
          const placed = placeNewBlocksNearNeighbors(s.nodes, s.edges, new Set(newNodeIds), sizing);
          store.getState().setNodes(placeAnchoredComments(placed, store.getState().compactCommentsEnabled));
        }
```

Add the import near the top of `messageBridge.ts` (next to the existing `computeHierarchicalLayout` import on line 14):

```typescript
import { computeHierarchicalLayout, placeNewBlocksNearNeighbors } from '../utils/layout/hierarchicalLayout';
```

(Remove the old standalone `computeHierarchicalLayout` import line to avoid a duplicate.)

- [ ] **Step 3: Commit (will compile after Phase 4)**

```bash
git add FlowCanvas/src/communication-message-types.ts FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): mode-aware load-graph handler

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase 4 — Reflow gating, store mode state, near-neighbor placement

4 files. Completes the React behavior so the build is green again.

### Task 4.1: Near-neighbor placement for new blocks

**Files:**
- Modify: `FlowCanvas/src/utils/layout/hierarchicalLayout.ts` (add export)
- Test: `FlowCanvas/src/utils/layout/__tests__/placeNewBlocksNearNeighbors.test.ts`

- [ ] **Step 1: Write the failing test**

```typescript
// FlowCanvas/src/utils/layout/__tests__/placeNewBlocksNearNeighbors.test.ts
import { describe, it, expect } from 'vitest';
import type { Node, Edge } from '@xyflow/react';
import { placeNewBlocksNearNeighbors, DEFAULT_BLOCK_SIZING } from '../hierarchicalLayout';

const blk = (id: string, x: number, y: number): Node => ({
  id, type: 'block', position: { x, y }, data: { blockType: 'print', props: {} },
});

describe('placeNewBlocksNearNeighbors', () => {
  it('places a new block just below its predecessor and leaves existing blocks untouched', () => {
    const nodes = [blk('A', 100, 100), blk('B', 100, 999)]; // B is "new" at a junk y
    const edges: Edge[] = [{ id: 'e', source: 'A', target: 'B' }];

    const out = placeNewBlocksNearNeighbors(nodes, edges, new Set(['B']), DEFAULT_BLOCK_SIZING);

    const a = out.find((n) => n.id === 'A')!;
    const b = out.find((n) => n.id === 'B')!;
    expect(a.position).toEqual({ x: 100, y: 100 });       // untouched
    expect(b.position.x).toBe(100);                        // aligned under predecessor
    expect(b.position.y).toBeGreaterThan(a.position.y);    // below it
  });

  it('nudges a new block off an existing block at the same spot', () => {
    const nodes = [blk('A', 100, 100), blk('C', 100, 100), blk('B', 0, 0)];
    // B is new; predecessor A is at (100,100); C already occupies the slot below A.
    const edges: Edge[] = [{ id: 'e1', source: 'A', target: 'B' }];
    const out = placeNewBlocksNearNeighbors(nodes, edges, new Set(['B']), DEFAULT_BLOCK_SIZING);
    const b = out.find((n) => n.id === 'B')!;
    const c = out.find((n) => n.id === 'C')!;
    expect(Math.abs(b.position.y - c.position.y) + Math.abs(b.position.x - c.position.x))
      .toBeGreaterThan(0); // not exactly on top of C
  });

  it('tags placed new blocks so the UI can highlight them', () => {
    const nodes = [blk('A', 100, 100), blk('B', 0, 0)];
    const edges: Edge[] = [{ id: 'e', source: 'A', target: 'B' }];
    const out = placeNewBlocksNearNeighbors(nodes, edges, new Set(['B']), DEFAULT_BLOCK_SIZING);
    const b = out.find((n) => n.id === 'B')!;
    expect((b.data as Record<string, unknown>)._justPlaced).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/utils/layout/__tests__/placeNewBlocksNearNeighbors.test.ts`
Expected: FAIL — `placeNewBlocksNearNeighbors` not exported.

- [ ] **Step 3: Implement at the bottom of `hierarchicalLayout.ts`**

```typescript
/**
 * Manual-mode placement for blocks that have no saved position yet (added in the text editor while
 * the preset was Manual). Existing/placed blocks never move. Each new block is dropped just below
 * its predecessor (the source of its incoming edge) in the USER's arrangement, aligned to the
 * predecessor's x, then nudged downward off any block already occupying that point. New blocks are
 * tagged data._justPlaced so the node can show a brief "new" highlight. Comments are handled by the
 * caller (placeAnchoredComments).
 */
export function placeNewBlocksNearNeighbors(
  nodes: Node[],
  edges: Edge[],
  newIds: Set<string>,
  sizing: BlockSizing,
): Node[] {
  if (newIds.size === 0) return nodes;
  const step = Math.round(LAYOUT.NODE_SPACING_Y * sizing.density);
  const predOf = new Map<string, string>(); // target -> source
  for (const e of edges) if (!predOf.has(e.target)) predOf.set(e.target, e.source);

  const posById = new Map<string, Point>();
  for (const n of nodes) if (n.position) posById.set(n.id, { x: n.position.x, y: n.position.y });

  // Occupied points from blocks that are NOT new (so new ones don't pile onto existing layout).
  const occupied = (): Point[] =>
    nodes.filter((n) => n.type !== 'comment' && !newIds.has(n.id))
      .map((n) => posById.get(n.id)!).filter(Boolean);

  // Resolve in edge order so a chain of new blocks stacks correctly.
  const ordered = nodes.filter((n) => newIds.has(n.id));
  for (const node of ordered) {
    const pred = predOf.get(node.id);
    const base = (pred && posById.get(pred)) || { x: LAYOUT.NODE_START_X, y: LAYOUT.NODE_START_Y };
    let y = base.y + step;
    const x = base.x;
    // Nudge downward off any already-placed block sharing this column near this y.
    const taken = occupied();
    while (taken.some((p) => Math.abs(p.x - x) < 1 && Math.abs(p.y - y) < step - 1)) y += step;
    const p = { x, y };
    posById.set(node.id, p);
  }

  return nodes.map((n) => {
    if (!newIds.has(n.id)) return n;
    const p = posById.get(n.id)!;
    return { ...n, position: p, data: { ...(n.data as object), _justPlaced: true } };
  });
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/utils/layout/__tests__/placeNewBlocksNearNeighbors.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/utils/layout/hierarchicalLayout.ts FlowCanvas/src/utils/layout/__tests__/placeNewBlocksNearNeighbors.test.ts
git commit -m "feat(flow-canvas): near-neighbor placement for new Manual-mode blocks

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 4.2: Store mode state replaces `autoReflowEnabled`

**Files:**
- Modify: `FlowCanvas/src/stores/slices/uiSlice.ts` (lines 30, 66-67, 95, 161-169)
- Modify: `FlowCanvas/src/utils/layoutAutosave.ts` (export a flush helper for the freeze)

- [ ] **Step 1: Update the `UISlice` interface (uiSlice.ts)**

Replace `autoReflowEnabled: boolean;` (line 30) with:

```typescript
  layoutMode: 'auto' | 'manual';          // active preset's mode (drives reflow gating + toolbar)
  defaultLayoutMode: 'auto' | 'manual';   // global default for new/unset presets (settings popover)
```

Replace the two action signatures (lines 66-67) `toggleAutoReflow` / `restoreAutoReflow` with:

```typescript
  setLayoutMode: (mode: 'auto' | 'manual') => void;       // user toggle: echoes + side effects
  restoreLayoutMode: (mode: 'auto' | 'manual') => void;   // host-driven (load-graph), no echo
  setDefaultLayoutMode: (mode: 'auto' | 'manual') => void;
  restoreDefaultLayoutMode: (mode: 'auto' | 'manual') => void;
```

- [ ] **Step 2: Update the initial state**

Replace `autoReflowEnabled: true,` (line 95) with:

```typescript
  layoutMode: 'auto',
  defaultLayoutMode: 'auto',
```

- [ ] **Step 3: Replace the `toggleAutoReflow`/`restoreAutoReflow` implementations (lines 161-169)**

```typescript
  setLayoutMode: (mode) => {
    if (get().layoutMode === mode) return;
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.setLayoutMode, mode });
    set({ layoutMode: mode });
    if (mode === 'auto') {
      // Auto-flow tidies immediately; the arrangement stays in host storage for a later flip back.
      if (get().nodes.length > 0) reflowLayout(get);
    } else {
      // Switching INTO Manual freezes the current on-screen positions as the saved layout.
      flushLayoutAutosave();
    }
  },
  restoreLayoutMode: (mode) => set({ layoutMode: mode }), // host-driven, no echo/reflow

  setDefaultLayoutMode: (mode) => {
    messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, defaultLayoutMode: mode });
    set({ defaultLayoutMode: mode });
  },
  restoreDefaultLayoutMode: (mode) => set({ defaultLayoutMode: mode }),
```

Add the import at the top of `uiSlice.ts` (next to the `sendLayoutAutosave`-related imports / the reflow import on line 6):

```typescript
import { flushLayoutAutosave } from '../../utils/layoutAutosave';
```

- [ ] **Step 4: Add `flushLayoutAutosave` to `layoutAutosave.ts`**

In `FlowCanvas/src/utils/layoutAutosave.ts`, export an immediate flush (sends now, cancelling any pending debounce):

```typescript
/** Sends the layout autosave immediately (used by the Manual-mode freeze on mode switch). */
export function flushLayoutAutosave(): void {
  if (debounceTimer) { clearTimeout(debounceTimer); debounceTimer = null; }
  doSend();
}
```

- [ ] **Step 5: Commit (compiles after 4.3 + 4.4 update remaining references)**

```bash
git add FlowCanvas/src/stores/slices/uiSlice.ts FlowCanvas/src/utils/layoutAutosave.ts
git commit -m "feat(flow-canvas): store layoutMode/defaultLayoutMode replacing autoReflowEnabled

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 4.3: Gate `reflowLayout` on mode + update its callers/restorers

**Files:**
- Modify: `FlowCanvas/src/stores/reflow.ts:20`
- Modify: `FlowCanvas/src/stores/messageBridge.ts:401` (replace `restoreAutoReflow`)

- [ ] **Step 1: Gate `reflowLayout` on the mode (reflow.ts)**

Replace line 20 `if (st.autoReflowEnabled) {` with:

```typescript
  if (st.layoutMode === 'auto') {
```

(The comment on lines 21-22 and the `else` freeze branch stay; update the comment's "auto-reflow" wording to "Auto-flow mode" for clarity.)

- [ ] **Step 2: Replace the `autoReflowEnabled` restore in `layout-restore` (messageBridge.ts line 401)**

```typescript
      // Restore the global DEFAULT mode (settings popover). The active preset's mode arrives via
      // load-graph (restoreLayoutMode), so this only seeds the default shown in settings.
      if (msg.defaultLayoutMode === 'auto' || msg.defaultLayoutMode === 'manual') {
        store.getState().restoreDefaultLayoutMode(msg.defaultLayoutMode);
      }
```

- [ ] **Step 3: Type-check the React app (Phases 3 + 4 together)**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: no errors. (If `SettingsPopover.tsx` / `Toolbar.tsx` still reference `autoReflowEnabled`, they're fixed in Phase 5 — to get a clean type-check now, do Phase 5 Task 5.1 + 5.2 before this step, or temporarily expect those two files to error and resolve in Phase 5. Recommended: implement 4.3 → 5.1 → 5.2, then type-check.)

- [ ] **Step 4: Migrate the store test (rename `autoReflow.test.ts`)**

Rewrite `FlowCanvas/src/stores/slices/__tests__/autoReflow.test.ts` as `layoutMode.test.ts` (delete the old file). Same seed helper; assert mode semantics instead:

```typescript
// FlowCanvas/src/stores/slices/__tests__/layoutMode.test.ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: {} } }));
import { useFlowStore } from '../../useFlowStore';
import { computeHierarchicalLayout, DEFAULT_BLOCK_SIZING } from '../../../utils/layout/hierarchicalLayout';

const yPos = (id: string) => useFlowStore.getState().nodes.find((n) => n.id === id)!.position.y;

function seed() {
  const nodes = [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', props: {} } },
    { id: 'A', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'send', props: { command: 'a', capture: 'b' } } },
    { id: 'B', type: 'block', position: { x: 0, y: 0 }, data: { blockType: 'print', props: { message: 'x' } } },
  ];
  const edges = [{ id: 'e0', source: '__start__', target: 'A' }, { id: 'e1', source: 'A', target: 'B' }];
  useFlowStore.getState().setNodes(nodes as never);
  useFlowStore.getState().setEdges(edges as never);
  const s0 = useFlowStore.getState();
  s0.setNodes(computeHierarchicalLayout(s0.nodes, s0.edges, DEFAULT_BLOCK_SIZING));
}

describe('layout mode', () => {
  beforeEach(() => {
    useFlowStore.setState({ nodes: [], edges: [], expandedNodes: new Set(), layoutMode: 'auto' });
    vi.clearAllMocks();
  });

  it('setLayoutMode flips the active mode', () => {
    useFlowStore.getState().setLayoutMode('manual');
    expect(useFlowStore.getState().layoutMode).toBe('manual');
    useFlowStore.getState().setLayoutMode('auto');
    expect(useFlowStore.getState().layoutMode).toBe('auto');
  });

  it('in Auto-flow, expanding a block pushes its successor down', () => {
    seed();
    const before = yPos('B');
    useFlowStore.getState().toggleExpanded('A');
    expect(yPos('B')).toBeGreaterThan(before);
  });

  it('in Manual, expanding a block does NOT move its successor (layout frozen)', () => {
    seed();
    useFlowStore.setState({ layoutMode: 'manual' });
    const before = yPos('B');
    useFlowStore.getState().toggleExpanded('A');
    expect(yPos('B')).toBe(before);
  });

  it('in Manual, adding an anchored comment does not move blocks', () => {
    seed();
    useFlowStore.setState({ layoutMode: 'manual' });
    const before = yPos('B');
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'A', 'comment');
    expect(yPos('B')).toBe(before);
    const c = useFlowStore.getState().nodes.find((n) => n.type === 'comment')!;
    expect(c.position.x).toBe(useFlowStore.getState().nodes.find((n) => n.id === 'A')!.position.x);
  });
});
```

- [ ] **Step 5: Run the React unit suite**

Run: `cd FlowCanvas && npm test`
Expected: PASS (new `layoutMode.test.ts` + `placeNewBlocksNearNeighbors.test.ts`; no remaining references to `autoReflowEnabled` in tests).

- [ ] **Step 6: Commit**

```bash
git add FlowCanvas/src/stores/reflow.ts FlowCanvas/src/stores/messageBridge.ts FlowCanvas/src/stores/slices/__tests__/
git commit -m "feat(flow-canvas): gate reflow on layout mode; migrate store tests

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase 5 — UI controls + host persistence of the default

4 files. Surfaces the toggle + the default, and persists the default through `layout-save`/`layout-restore` + `set-layout-mode` round-trip.

### Task 5.1: Toolbar mode toggle

**Files:**
- Modify: `FlowCanvas/src/panels/Toolbar.tsx` (add the toggle next to the Auto-Layout button, line 187)
- Test: `FlowCanvas/src/panels/__tests__/Toolbar.layoutMode.test.tsx` (if the panels dir has an existing test harness; otherwise assert via store in a unit test)

- [ ] **Step 1: Add a failing test (store-driven)**

```tsx
// FlowCanvas/src/panels/__tests__/Toolbar.layoutMode.test.tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
vi.mock('../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn(), flushLayoutAutosave: vi.fn() }));
vi.mock('../../MessageBus', () => ({ messageBus: { send: vi.fn() }, CANVAS_HOST_MESSAGES: { outgoing: { setLayoutMode: 'set-layout-mode' } } }));
import Toolbar from '../Toolbar';
import { useFlowStore } from '../../stores/useFlowStore';

describe('Toolbar layout-mode toggle', () => {
  beforeEach(() => useFlowStore.setState({ layoutMode: 'auto', nodes: [] }));

  it('toggles the preset mode when clicked', () => {
    render(<Toolbar />);
    fireEvent.click(screen.getByTitle(/manual layout|auto-flow/i));
    expect(useFlowStore.getState().layoutMode).toBe('manual');
  });
});
```

> If `panels/` has no testing-library harness yet, place this assertion in a store test instead (call `setLayoutMode` directly) and add the toolbar button without a component test. Do not block on harness setup.

- [ ] **Step 2: Run to verify it fails**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/Toolbar.layoutMode.test.tsx`
Expected: FAIL — no such control.

- [ ] **Step 3: Add the toggle in `Toolbar.tsx`**

Add near the other store hooks (after line 32 `const autoLayout = useAutoLayout();`):

```tsx
  const layoutMode = useFlowStore((s) => s.layoutMode);
  const setLayoutMode = useFlowStore((s) => s.setLayoutMode);
```

Add the control right after the Auto-Layout button (after line 189, the `⊞ Layout` button):

```tsx
      <button
        onClick={() => setLayoutMode(layoutMode === 'manual' ? 'auto' : 'manual')}
        style={btnStyle(layoutMode === 'manual' ? 'var(--fc-state-success)' : 'var(--fc-cat-data-border)', true)}
        title={layoutMode === 'manual'
          ? 'Manual layout — your arrangement is kept. Click for Auto-flow.'
          : 'Auto-flow — canvas re-tidies itself. Click to keep your arrangement (Manual).'}
      >
        {layoutMode === 'manual' ? '🔒 Manual' : '✨ Auto-flow'}
      </button>
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd FlowCanvas && npx vitest run src/panels/__tests__/Toolbar.layoutMode.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/panels/Toolbar.tsx FlowCanvas/src/panels/__tests__/Toolbar.layoutMode.test.tsx
git commit -m "feat(flow-canvas): toolbar Auto-flow/Manual mode toggle

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 5.2: Settings popover — default mode (replaces "Auto-layout on edits")

**Files:**
- Modify: `FlowCanvas/src/panels/SettingsPopover.tsx` (lines 77-78, 142)

- [ ] **Step 1: Replace the store hooks (lines 77-78)**

```tsx
  const defaultLayoutMode = useFlowStore((s) => s.defaultLayoutMode);
  const setDefaultLayoutMode = useFlowStore((s) => s.setDefaultLayoutMode);
```

- [ ] **Step 2: Replace the "Auto-layout on edits" Toggle (line 142) with a Segmented default-mode control**

```tsx
          <Segmented
            label="Default layout mode"
            value={defaultLayoutMode}
            options={[{ label: 'Auto-flow', v: 'auto' }, { label: 'Manual', v: 'manual' }] as const}
            onChange={(v) => setDefaultLayoutMode(v)}
          />
```

> `Segmented` is generic over `string | number`; `'auto' | 'manual'` satisfies it. Keep it in the "View" group where the toggle was.

- [ ] **Step 3: Type-check + run React suite**

Run: `cd FlowCanvas && npx tsc --noEmit && npm test`
Expected: no type errors; all tests pass. Grep to confirm no stragglers: `cd FlowCanvas && grep -rn "autoReflow" src` should return nothing.

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/panels/SettingsPopover.tsx
git commit -m "feat(flow-canvas): settings 'Default layout mode' replaces auto-layout toggle

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

### Task 5.3: Host wiring — `set-layout-mode` routing + persist/restore the default

**Files:**
- Modify: `UI/FlowCanvasForm.cs` (routing line 263 area; `SendPersistedLayout` line 381; `SavePanelSizes` lines 404/408/424; add `OnSetLayoutMode` event)
- Modify: `Form1.cs` (subscribe `OnSetLayoutMode`, add `ApplySetLayoutMode`)
- Test: `SSH_Helper.Tests/Services/ConfigurationServiceWindowStateTests.cs` (replace the autoReflow round-trip)

- [ ] **Step 1: Test the FlowCanvasForm persistence/routing (the actual new behavior here)**

> DEVIATION: the legacy `SaveAndLoad_FlowCanvasAutoReflow_RoundTrips` test was already deleted in Task 1.2, and `DefaultLayoutMode` config persistence is already covered by `ConfigurationServiceLayoutModeTests.DefaultLayoutMode_roundTrips`. So do NOT re-add a plain config round-trip. The new behavior in THIS task is the `FlowCanvasForm` bridge: a `layout-save` carrying `defaultLayoutMode` must persist `WindowState.FlowCanvasDefaultLayoutMode`, and a `set-layout-mode` message must raise `OnSetLayoutMode`.

Add a test to `SSH_Helper.Tests/UI/FlowCanvasFormLayoutTests.cs` (mirror that file's existing `[WinFormsFact]` construction + how it injects a `ConfigurationService` and simulates an inbound web message — follow the pattern Task 3.1 used). Assertions:

```csharp
    [WinFormsFact]
    public void Inbound_layout_save_persists_default_layout_mode()
    {
        // construct the form with a temp-path ConfigurationService (this file's existing helper)
        // simulate an inbound { type:"layout-save", defaultLayoutMode:"manual" } web message
        configService.GetCurrent().WindowState.FlowCanvasDefaultLayoutMode
            .Should().Be(SSH_Helper.Models.LayoutMode.Manual);
    }

    [WinFormsFact]
    public void Inbound_set_layout_mode_raises_OnSetLayoutMode()
    {
        JObject? received = null;
        form.OnSetLayoutMode += m => received = m;
        // simulate an inbound { type:"set-layout-mode", mode:"manual" } web message
        received.Should().NotBeNull();
        received!["mode"]!.ToString().Should().Be("manual");
    }
```

> If `FlowCanvasFormLayoutTests` cannot drive `OnWebMessageReceived` directly (it's private), assert through whatever seam the file already uses for inbound messages; if there is no such seam, cover the persistence via the `SavePanelSizes`-equivalent path the harness exposes and note any gap. Don't invent a new harness.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~FlowCanvasFormLayoutTests`
Expected: FAIL — `defaultLayoutMode` not yet persisted / `OnSetLayoutMode` not yet defined.

- [ ] **Step 3: Route `set-layout-mode` + add the event (FlowCanvasForm.cs)**

In `OnWebMessageReceived` switch, add a case (after `layout-autosave`, line 265):

```csharp
                    case "set-layout-mode":
                        OnSetLayoutMode?.Invoke(msg);
                        break;
```

Add the event next to `OnLayoutAutosave` (line 346):

```csharp
        public event Action<JObject>? OnSetLayoutMode;
```

- [ ] **Step 4: Persist + restore the default mode (FlowCanvasForm.cs)**

In `SendPersistedLayout`, replace `autoReflowEnabled = ws.FlowCanvasAutoReflow,` (line 381) with:

```csharp
                defaultLayoutMode = (ws.FlowCanvasDefaultLayoutMode ?? Models.LayoutMode.AutoFlow) == Models.LayoutMode.Manual ? "manual" : "auto",
```

In `SavePanelSizes`, replace the `autoReflow` read (line 404) and its uses (lines 408, 424):

```csharp
            var defaultLayoutMode = msg["defaultLayoutMode"]?.ToString();
```

Add `defaultLayoutMode == null` to the early-return guard (line 406-408 group), e.g. append `&& defaultLayoutMode == null` to the condition. Replace the write (line 424):

```csharp
                if (defaultLayoutMode == "auto" || defaultLayoutMode == "manual")
                    c.WindowState.FlowCanvasDefaultLayoutMode =
                        defaultLayoutMode == "manual" ? Models.LayoutMode.Manual : Models.LayoutMode.AutoFlow;
```

- [ ] **Step 5: Subscribe + apply in `Form1.cs`**

Where `OnLayoutAutosave` is wired (line 6805-6807), add alongside:

```csharp
            _flowCanvasForm.OnSetLayoutMode += (msg) =>
            {
                BeginInvoke(() => ApplySetLayoutMode(msg));
            };
```

Add the handler near `ApplyLayoutAutosave` (after line 2557):

```csharp
        private void ApplySetLayoutMode(JObject msg)
        {
            if (string.IsNullOrEmpty(_activePresetName)) return;
            var mode = msg["mode"]?.ToString();
            if (mode != "auto" && mode != "manual") return;
            _presetManager.UpdateLayoutMode(
                _activePresetName,
                mode == "manual" ? Models.LayoutMode.Manual : Models.LayoutMode.AutoFlow);
        }
```

- [ ] **Step 6: Build + run the C# test**

Run: `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
Expected: build succeeds.
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FullyQualifiedName~ConfigurationServiceWindowStateTests`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add UI/FlowCanvasForm.cs Form1.cs SSH_Helper.Tests/Services/ConfigurationServiceWindowStateTests.cs
git commit -m "feat(flow-canvas): persist default layout mode + route set-layout-mode to presets

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Phase 6 — Full verification

No new files. Confirms the whole feature end-to-end.

### Task 6.1: Full build + suites + manual smoke

- [ ] **Step 1: Full solution build (includes the React/Vite build)**

Run: `dotnet build SSH_Helper.sln`
Expected: build succeeds (the `BuildFlowCanvas` target runs `npm run build` → `tsc && vite build` with no errors).

- [ ] **Step 2: C# test suite (logic layer; exclude flaky UI namespace per the known parallel-deadlock gotcha)**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName!~SSH_Helper.Tests.UI" --blame-hang-timeout 120s`
Expected: PASS. Then run the UI namespace alone:
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SSH_Helper.Tests.UI"`
Expected: PASS.

- [ ] **Step 3: React unit + e2e**

Run: `cd FlowCanvas && npm test`
Expected: PASS.
Run: `cd FlowCanvas && npm run test:e2e`
Expected: PASS (headless per config).

- [ ] **Step 4: Manual smoke (the original bug)**

`dotnet run --project SSH_Helper.csproj`. Then:
1. Open a YAML preset in the Flow Canvas; click **🔒 Manual**; drag a few blocks; close the canvas.
2. Reopen the canvas → arrangement is restored exactly (no reflow). ✓
3. In the text editor, append a step at the end; reopen the canvas → existing blocks hold, the new block appears near its neighbor with a brief highlight. ✓
4. Insert a step in the middle; reopen → clean reflow (no mis-placed blocks). ✓
5. Switch the preset to **✨ Auto-flow**; edit a block → canvas re-tidies. Reopen → re-laid-out. ✓
6. Settings → set **Default layout mode = Manual**; create a NEW preset → it opens in Manual. ✓
7. Confirm a config that previously had "Auto-layout on edits" OFF now shows **Default layout mode = Manual** after upgrade. ✓

- [ ] **Step 5: Final commit (if any smoke fixes were needed)**

```bash
git add -A
git commit -m "test(flow-canvas): verify per-preset layout mode end-to-end

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Notes for the implementer

- **Cross-phase compile gap (intentional):** Phase 3 Task 3.3 introduces references (`restoreLayoutMode`, `placeNewBlocksNearNeighbors`, store `layoutMode`) that Phase 4 defines. Implement **3.3 → 4.1 → 4.2 → 4.3 → 5.1 → 5.2** before the first React `tsc`/build, or accept transient red between those commits. C# Phases 1–3 each build independently.
- **`No Semantic Search` follow-through:** after Phase 5, `grep -rn "autoReflow" src` (React) and a solution-wide search for `FlowCanvasAutoReflow` should show only `Models/AppConfiguration.cs` (the legacy migration field) and `Services/ConfigurationService.cs` (the migration). Nothing else may reference the old name.
- **Behavior change to flag in release notes:** Auto-flow presets now always re-lay-out on reopen (previously a hash-match kept positions). This is intentional — keeping positions is now what Manual mode is for.
