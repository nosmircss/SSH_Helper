# Canvas Layout Persistence Design

## Context

When users export presets, the Flow Canvas layout (block positions, comments, disabled state) is lost. Currently, `PresetInfo` stores only YAML text, and `FlowCanvasBridge.TextToGraph()` regenerates node positions algorithmically on every canvas open. Comment nodes are ephemeral -- they exist only in React memory and are filtered out before any message reaches C#. Disabled block state is similarly session-only.

This means a user who carefully arranges their flow canvas, adds explanatory comments, and disables certain blocks for testing loses all of that work when exporting to another user or even when closing and reopening the canvas after editing YAML.

**Goal:** Persist block positions, comment nodes, and disabled block state alongside the preset so they survive export/import and canvas reloads.

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| What to persist | Positions, comments, disabled blocks | User-requested scope |
| Where to store | `CanvasLayout` field on `PresetInfo` | Travels automatically with export/import |
| When to save | On "Apply YAML" click | Piggybacks on existing save flow, no new UX |
| Structural change handling | Structure-hash gated (all-or-nothing) | Simple, predictable, avoids partial layouts |
| Hash sensitivity | Block types + step paths only | Value edits preserve layout; structural changes reset it |

## Data Model

### New Models (`Models/CanvasLayoutData.cs`)

```csharp
public class CanvasLayoutData
{
    public string StructureHash { get; set; }
    public Dictionary<string, NodePosition> Positions { get; set; }
    public List<CanvasComment> Comments { get; set; }
    public List<string> DisabledBlockIds { get; set; }
}

public class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

public class CanvasComment
{
    public string Id { get; set; }
    public string Text { get; set; }
    public string Color { get; set; }           // default "#e0c040"
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }            // default 200
    public double Height { get; set; }           // default 100
    public string? AttachedToNodeId { get; set; }
}
```

### PresetInfo Change (`Models/PresetInfo.cs`)

```csharp
public CanvasLayoutData? CanvasLayout { get; set; }
```

Nullable so existing presets (without layout data) remain valid with no migration needed.

## Structure Hash

Computed from the ordered list of `(stepPath, blockType)` tuples in the graph. Only the tree shape and command types matter -- not values, conditions, or arguments.

**Example:**
```yaml
steps:
  - send: "show version"
  - if:
      condition: "{{x}} > 0"
      then:
        - print: "positive"
```

Structure string: `"steps.0:send|steps.1:if|steps.1.then.0:print"`
Hash: SHA256 of that string.

**Preserved by:** Changing `"show version"` to `"show interfaces"`, editing conditions, changing print messages, modifying block options.

**Invalidated by:** Adding a step, removing a step, reordering steps, changing a block's command type.

Computed in `FlowCanvasBridge` since it already has access to each node's `_stepPath` and `blockType` during `TextToGraph`.

## Save Flow (Apply YAML)

```
1. User clicks "Apply YAML" in React toolbar
2. React builds the payload:
   - Splits nodes into executable nodes (for YAML) and comment nodes (for layout)
   - Reads disabledBlocks from DebugSlice
   - Sends: { type: 'apply-yaml', graphChanged, nodes, edges, comments, disabledBlocks }
3. FlowCanvasForm receives the message
4. Calls ExportGraphToYaml(graphData) for YAML text (existing, unchanged)
5. Builds CanvasLayoutData:
   - positions: { nodeId: {x, y} } from each node (including __start__)
   - comments: comment node data array
   - disabledBlocks: string[] from the message
   - structureHash: computed from nodes' _stepPath + blockType
6. Sets preset.CanvasLayout = layoutData
7. Updates YAML in text editor (existing behavior)
```

## Load Flow (Open Canvas)

```
1. User opens Flow Canvas for a preset
2. C# calls TextToGraph(yaml) -> nodes with algorithmic positions
3. C# computes structure hash of the generated graph
4. Checks preset.CanvasLayout:
   a. null -> send graph as-is (current behavior, backward compatible)
   b. structureHash matches:
      - Override each node's position with the stored position
      - Append stored comment nodes to the nodes array
      - Set data.disabled = true on disabled nodes
   c. structureHash doesn't match -> send graph as-is (stale layout discarded)
5. Send load-graph to React with the final nodes + edges
```

## Export/Import Changes

### Single-Preset Export (`PresetManager.Export`)

Bump to v3. Add `canvasLayout` to the JSON payload:

```json
{
  "v": 3,
  "commands": "...",
  "timeout": null,
  "folder": "...",
  "isFavorite": false,
  "canvasLayout": {
    "structureHash": "a1b2c3...",
    "positions": { "node-0": { "x": 250, "y": 125 } },
    "comments": [{ "id": "c1", "text": "Auth section", "color": "#e0c040", "x": 100, "y": 80, "width": 200, "height": 100 }],
    "disabledBlockIds": ["node-3"]
  }
}
```

### Single-Preset Import (`PresetManager.Import`)

`ParseImportedPayload` reads `canvasLayout` from JSON if present. v2 payloads (without it) produce `CanvasLayout = null` -- fully backward compatible.

### Bulk Export/Import

Same pattern: `canvasLayout` included per-preset in the JSON file. Format version bumped.

## Message Bus Changes

### `apply-yaml` (React -> C#)

Add two fields:

```typescript
// Before: { type: 'apply-yaml', graphChanged: boolean, nodes: Node[], edges: Edge[] }
// After:  { type: 'apply-yaml', graphChanged: boolean, nodes: Node[], edges: Edge[],
//           comments: CommentData[], disabledBlocks: string[] }
```

### `load-graph` (C# -> React)

No format change. Comment nodes are included in the `nodes` array with `type: 'comment'`. Disabled nodes have `data.disabled: true`.

## React-Side Changes

### `exportGraph.ts`

`buildExecutableGraphPayload` currently strips comment nodes entirely. Change to:
- Split nodes into `executableNodes` and `commentNodes`
- Return both: executable nodes as `nodes`, comment data as `comments`
- Read `disabledBlocks` from the store and include it

### `messageBridge.ts`

The `load-graph` handler already calls `setNodes(nodes)`. Add:
- After setting nodes, scan for `data.disabled === true` and populate `disabledBlocks` in DebugSlice
- Comment nodes work automatically since they're nodes with `type: 'comment'` and the `CommentNode` component already renders them

## Files Changed

| File | Change |
|------|--------|
| `Models/CanvasLayoutData.cs` | **New** -- `CanvasLayoutData`, `NodePosition`, `CanvasComment` |
| `Models/PresetInfo.cs` | Add `CanvasLayout` property |
| `Services/PresetManager.cs` | Bump export to v3, serialize/deserialize `canvasLayout` |
| `Services/FlowCanvasBridge.cs` | Add `ComputeStructureHash()`, add `MergeLayout()` for applying stored positions/comments/disabled |
| `UI/FlowCanvasForm.cs` | Capture layout on `apply-yaml`, inject layout before `load-graph` |
| `FlowCanvas/src/utils/exportGraph.ts` | Send comments + disabledBlocks alongside executable nodes |
| `FlowCanvas/src/stores/messageBridge.ts` | Populate `disabledBlocks` from loaded node data |

## Backward Compatibility

- **Existing presets**: `CanvasLayout` is null. Canvas opens with algorithmic layout as before.
- **v2 imports**: No `canvasLayout` field. `CanvasLayout` stays null. No errors.
- **v3 exports imported by old versions**: Old `ParseImportedPayload` ignores unknown JSON fields. The preset imports fine but without layout data.

## Verification

1. **Round-trip test**: Open canvas, arrange blocks, add comments, disable a block, click Apply YAML, close canvas, reopen -> layout restored exactly.
2. **Value edit test**: Edit a block's value in the text editor (e.g., change a command argument), reopen canvas -> layout preserved.
3. **Structural edit test**: Add a new step in the text editor, reopen canvas -> layout resets to algorithmic.
4. **Export/import test**: Export preset, import on fresh install -> layout restored.
5. **v2 import test**: Import a v2 preset (no layout) -> canvas opens with algorithmic layout, no errors.
6. **v3 export on old version test**: Export v3 preset, import on version without this feature -> preset imports fine, layout silently dropped.
