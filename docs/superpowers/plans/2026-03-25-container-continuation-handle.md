# Container Continuation Handle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a diamond-shaped "continue" handle to all container blocks so users can connect blocks that execute after the container finishes.

**Architecture:** Three-layer change — (1) frontend handle rendering + connection logic in React, (2) backend export guards in C# to prevent continuation edges from being misinterpreted as branches, (3) backend import rewrite to emit a single continuation edge from the container instead of convergence edges from branch ends.

**Tech Stack:** React + @xyflow/react (frontend), C# .NET 8 (backend), xUnit (tests)

**Spec:** `docs/superpowers/specs/2026-03-25-container-continuation-handle-design.md`

---

### Task 1: Backend — Add ColorContinue constant and update PendingEdge

**Files:**
- Modify: `Services/FlowCanvasBridge.cs:85-87` (add constant after ColorBranch)
- Modify: `Services/FlowCanvasBridge.cs:748-762` (update PendingEdge class)

- [ ] **Step 1: Add ColorContinue constant**

In `Services/FlowCanvasBridge.cs`, after line 87 (`ColorBranch`), add:

```csharp
private const string ColorContinue = "#4a9eff";
```

- [ ] **Step 2: Add Dashed property to PendingEdge**

Replace the `PendingEdge` class (lines 748-762) with:

```csharp
private sealed class PendingEdge
{
    public string NodeId { get; }
    public string? SourceHandle { get; }
    public string? Color { get; }
    public string? Label { get; }
    public bool Dashed { get; }

    public PendingEdge(string nodeId, string? sourceHandle = null, string? color = null, string? label = null, bool dashed = false)
    {
        NodeId = nodeId;
        SourceHandle = sourceHandle;
        Color = color;
        Label = label;
        Dashed = dashed;
    }
}
```

**Important:** The default is `false` (solid edge). This preserves existing behavior for:
- `new PendingEdge(be)` — convergence edges from branch ends (currently solid)
- `new PendingEdge(nodeId)` — sequential flow edges (currently solid)

The IF-without-else skip edge at line ~368 must be updated to explicitly pass `dashed: true`:

```csharp
// Before:
pendingConnections.Add(new PendingEdge(nodeId, "false", ColorElse, "else"));

// After:
pendingConnections.Add(new PendingEdge(nodeId, "false", ColorElse, "else", dashed: true));
```

- [ ] **Step 3: Separate label rendering from dash styling in edge creation**

In the `TextToGraph` method, find the edge creation loop where `pendingConnections` are consumed (around lines 320-341). The current code couples `strokeDasharray` to `pe.Label != null`. Change it to use `pe.Dashed`:

Find this pattern:
```csharp
if (pe.Label != null)
{
    edge["label"] = pe.Label;
    edge["labelStyle"] = new JObject
    {
        ["fill"] = pe.Color ?? "#555",
        ["fontSize"] = 11,
        ["fontWeight"] = 600,
    };
    edge["type"] = "smoothstep";
    edge["style"]!["strokeDasharray"] = "5,5";
}
```

Replace with:
```csharp
if (pe.Label != null)
{
    edge["label"] = pe.Label;
    edge["labelStyle"] = new JObject
    {
        ["fill"] = pe.Color ?? "#555",
        ["fontSize"] = 11,
        ["fontWeight"] = 600,
    };
    edge["type"] = "smoothstep";
}
if (pe.Dashed)
{
    edge["style"]!["strokeDasharray"] = "5,5";
}
```

- [ ] **Step 4: Build and verify no compilation errors**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeded

- [ ] **Step 5: Run existing tests to verify no regressions**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FlowCanvasBridge"`
Expected: All existing tests pass (the `Dashed` property defaults to `false` = solid edges, preserving existing behavior for sequential and convergence edges)

- [ ] **Step 6: Commit**

```bash
git add Services/FlowCanvasBridge.cs
git commit -m "feat(flow-canvas): add ColorContinue constant and Dashed flag to PendingEdge"
```

---

### Task 2: Backend — Guard export against continuation edges

**Files:**
- Modify: `Services/FlowCanvasBridge.cs:1131-1156` (HasGraphAuthoredContainerBranches)
- Modify: `Services/FlowCanvasBridge.cs:1254-1256` (TryGenerateContainerFromGraph — nodeEdges filter)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write failing test — IF with continue edge must not consume continuation target as elif**

In `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`, add:

```csharp
[Fact]
public void ExportGraphToYaml_IfWithContinueEdge_ContinuationTargetNotConsumedAsBranch()
{
    var bridge = new FlowCanvasBridge();
    var graph = new JObject
    {
        ["nodes"] = new JArray
        {
            CreateStartNode(),
            CreateBlockNode("if-1", "if", new JObject { ["condition"] = "true" }),
            CreateBlockNode("then-1", "print", new JObject { ["message"] = "inside-then" }),
            CreateBlockNode("after-1", "print", new JObject { ["message"] = "after-if" }),
        },
        ["edges"] = new JArray
        {
            CreateEdge("__start__", "if-1"),
            CreateEdge("if-1", "then-1", branchPath: "then"),
            CreateEdge("if-1", "after-1", sourceHandle: "continue"),
        }
    };

    var result = bridge.ExportGraphToYaml(graph);
    Assert.True(result.Success, string.Join(" | ", result.Errors));

    var parser = new ScriptParser();
    var script = parser.Parse(result.Yaml);
    var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
    Assert.Empty(errors);

    // IF should have exactly one then step, no elif, no else
    Assert.Equal(2, script.Steps.Count);
    var ifStep = script.Steps[0];
    Assert.Equal(StepType.If, ifStep.GetStepType());
    Assert.Single(ifStep.Then ?? new List<ScriptStep>());
    Assert.Null(ifStep.Elif);
    Assert.Null(ifStep.Else);

    // The continuation target should appear as the second top-level step
    var afterStep = script.Steps[1];
    Assert.Equal(StepType.Print, afterStep.GetStepType());
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "IfWithContinueEdge"`
Expected: FAIL — the continuation target is consumed as an elif or then branch

- [ ] **Step 3: Write failing test — FOREACH with continue edge must not consume continuation target as do body**

```csharp
[Fact]
public void ExportGraphToYaml_ForeachWithContinueEdge_ContinuationTargetNotConsumedAsDo()
{
    var bridge = new FlowCanvasBridge();
    var graph = new JObject
    {
        ["nodes"] = new JArray
        {
            CreateStartNode(),
            CreateBlockNode("for-1", "foreach", new JObject { ["iterator"] = "item in ${items}" }),
            CreateBlockNode("do-1", "print", new JObject { ["message"] = "inside-loop" }),
            CreateBlockNode("after-1", "print", new JObject { ["message"] = "after-loop" }),
        },
        ["edges"] = new JArray
        {
            CreateEdge("__start__", "for-1"),
            CreateEdge("for-1", "do-1", branchPath: "do"),
            CreateEdge("for-1", "after-1", sourceHandle: "continue"),
        }
    };

    var result = bridge.ExportGraphToYaml(graph);
    Assert.True(result.Success, string.Join(" | ", result.Errors));

    var parser = new ScriptParser();
    var script = parser.Parse(result.Yaml);
    var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
    Assert.Empty(errors);

    // FOREACH should have exactly one do step
    Assert.Equal(2, script.Steps.Count);
    var forStep = script.Steps[0];
    Assert.Equal(StepType.Foreach, forStep.GetStepType());
    Assert.Single(forStep.Do ?? new List<ScriptStep>());

    // The continuation target should appear as the second top-level step
    var afterStep = script.Steps[1];
    Assert.Equal(StepType.Print, afterStep.GetStepType());
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "ContinueEdge"`
Expected: Both FAIL

- [ ] **Step 5: Filter continuation edges in TryGenerateContainerFromGraph**

In `Services/FlowCanvasBridge.cs`, find `TryGenerateContainerFromGraph` at the `nodeEdges` assignment (line ~1254):

Replace:
```csharp
var nodeEdges = outgoing.TryGetValue(nodeId, out var edgesFromNode)
    ? edgesFromNode
    : new List<EdgeInfo>();
```

With:
```csharp
var nodeEdges = outgoing.TryGetValue(nodeId, out var edgesFromNode)
    ? edgesFromNode.Where(e => !string.Equals(e.SourceHandle, "continue", StringComparison.OrdinalIgnoreCase)).ToList()
    : new List<EdgeInfo>();
```

- [ ] **Step 6: Add explicit guard in HasGraphAuthoredContainerBranches**

In `HasGraphAuthoredContainerBranches` (line ~1139), add the `"continue"` guard before the existing `BranchPath` check:

Replace:
```csharp
foreach (var edge in edges)
{
    // Require explicit branch metadata. SourceHandle-only false skip edges
    // (used for imported if-without-else visualization) should not trigger
    // regeneration from graph.
    if (string.IsNullOrWhiteSpace(edge.BranchPath))
        continue;
```

With:
```csharp
foreach (var edge in edges)
{
    // Continuation edges (sourceHandle="continue") are not branch edges.
    if (string.Equals(edge.SourceHandle, "continue", StringComparison.OrdinalIgnoreCase))
        continue;

    // Require explicit branch metadata. SourceHandle-only false skip edges
    // (used for imported if-without-else visualization) should not trigger
    // regeneration from graph.
    if (string.IsNullOrWhiteSpace(edge.BranchPath))
        continue;
```

- [ ] **Step 7: Build and run tests**

Run: `dotnet build SSH_Helper.sln && dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FlowCanvasBridge"`
Expected: All tests pass including the two new ones

- [ ] **Step 8: Commit**

```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "feat(flow-canvas): guard export against continuation edges in container blocks"
```

---

### Task 3: Backend — Import with continuation edges

**Files:**
- Modify: `Services/FlowCanvasBridge.cs:347-374` (TextToGraph — pendingConnections for containers)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write failing test — import creates continuation edge from container, not convergence edges**

```csharp
[Fact]
public void TextToGraph_IfWithThenAndElse_CreatesContinuationEdgeFromContainer()
{
    var bridge = new FlowCanvasBridge();
    var yaml = """
        steps:
          - if:
              condition: "true"
              then:
                - print: "a"
              else:
                - print: "b"
          - print: "after"
        """;

    var (nodes, edges) = bridge.TextToGraph(yaml);

    // Find the IF node and the "after" print node
    var ifNode = nodes.Cast<JObject>().First(n => n["data"]?["blockType"]?.ToString() == "if");
    var afterNode = nodes.Cast<JObject>().First(n =>
        n["data"]?["blockType"]?.ToString() == "print" &&
        n["data"]?["props"]?["_isChildOf"] == null &&
        (n["data"]?["props"]?["_preview"]?.ToString() == "after" ||
         n["data"]?["props"]?["message"]?.ToString() == "after"));

    var ifId = ifNode["id"]!.ToString();
    var afterId = afterNode["id"]!.ToString();

    // There should be exactly one edge from IF to after, using sourceHandle="continue"
    var continueEdges = edges.Cast<JObject>().Where(e =>
        e["source"]?.ToString() == ifId &&
        e["target"]?.ToString() == afterId).ToList();

    Assert.Single(continueEdges);
    Assert.Equal("continue", continueEdges[0]["sourceHandle"]?.ToString());

    // The edge should NOT be dashed (no strokeDasharray)
    var style = continueEdges[0]["style"] as JObject;
    Assert.Null(style?["strokeDasharray"]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "CreatesContinuationEdge"`
Expected: FAIL — currently creates convergence edges from branch-end nodes, not from the container

- [ ] **Step 3: Update TextToGraph to emit continuation edges**

In `Services/FlowCanvasBridge.cs`, in the `TextToGraph` method, find the container branch-end handling (around line 350-360):

Replace:
```csharp
if (branchEnds.Count > 0)
{
    foreach (var be in branchEnds)
        pendingConnections.Add(new PendingEdge(be));
}
else
{
    // No children expanded — this node connects to the next step
    pendingConnections.Add(new PendingEdge(nodeId));
}
```

With:
```csharp
if (branchEnds.Count > 0)
{
    // Single continuation edge from the container's diamond handle
    pendingConnections.Add(new PendingEdge(nodeId, "continue", ColorContinue, "next", dashed: false));
}
else
{
    // No children expanded — this node connects to the next step
    pendingConnections.Add(new PendingEdge(nodeId));
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FlowCanvasBridge"`
Expected: All tests pass

- [ ] **Step 5: Write round-trip test — import then export produces valid YAML**

```csharp
[Fact]
public void ImportExportRoundTrip_IfWithContinuation_ProducesValidYaml()
{
    var bridge = new FlowCanvasBridge();
    var yaml = """
        steps:
          - if:
              condition: "${x} > 0"
              then:
                - print: "positive"
              else:
                - print: "non-positive"
          - print: "done"
        """;

    // Import
    var (nodes, edges) = bridge.TextToGraph(yaml);
    var graph = new JObject { ["nodes"] = nodes, ["edges"] = edges };

    // Export
    var result = bridge.ExportGraphToYaml(graph);
    Assert.True(result.Success, string.Join(" | ", result.Errors));

    // Re-parse and validate
    var parser = new ScriptParser();
    var script = parser.Parse(result.Yaml);
    var errors = parser.Validate(script, result.Yaml, enforceCanonicalSyntax: true);
    Assert.Empty(errors);

    // Should have IF + print at top level
    Assert.Equal(2, script.Steps.Count);
    Assert.Equal(StepType.If, script.Steps[0].GetStepType());
    Assert.Equal(StepType.Print, script.Steps[1].GetStepType());
}
```

- [ ] **Step 6: Run all tests**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FlowCanvasBridge"`
Expected: All pass

- [ ] **Step 7: Commit**

```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "feat(flow-canvas): import creates continuation edges from container diamond handle"
```

---

### Task 4: Frontend — Diamond handle on container blocks

**Files:**
- Modify: `FlowCanvas/src/nodes/BaseBlock.tsx:208-219` (add diamond handle after existing handles)

- [ ] **Step 1: Add diamond continuation handle for container blocks**

In `FlowCanvas/src/nodes/BaseBlock.tsx`, after the "Second output for IF blocks" section (line 219, before the closing `</div>` at line 220), add:

```tsx
{/* Continuation handle for container blocks (diamond, bottom-left) */}
{def.isContainer && (
  <Handle
    type="source"
    position={Position.Bottom}
    id="continue"
    style={{
      background: '#4a9eff',
      width: 10,
      height: 10,
      border: 'none',
      borderRadius: 2,
      transform: 'rotate(45deg)',
      left: 15,
      bottom: -5,
      // Larger clickable area via transparent border trick
      boxShadow: '0 0 0 5px transparent',
    }}
  />
)}
```

- [ ] **Step 2: Verify the app builds**

Run: `cd FlowCanvas && npm run build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/nodes/BaseBlock.tsx
git commit -m "feat(flow-canvas): add diamond continuation handle to container blocks"
```

---

### Task 5: Frontend — Connection logic for continuation edges

**Files:**
- Modify: `FlowCanvas/src/stores/slices/graphSlice.ts:295-331` (onConnect handler)

- [ ] **Step 1: Update onConnect to handle continuation edges**

In `FlowCanvas/src/stores/slices/graphSlice.ts`, replace the `onConnect` handler (lines 295-331) with:

```typescript
onConnect: (connection) => {
  // Push undo snapshot before connecting
  get().pushSnapshot('Connect edge');
  set((state) => {
    // Determine if this connection originates from a container block's branch handle
    const sourceNode = state.nodes.find((n) => n.id === connection.source);
    const blockType = (sourceNode?.data as Record<string, unknown>)?.blockType as string | undefined;
    const def = blockType ? blockDefMap.get(blockType) : undefined;

    const isContinuation = connection.sourceHandle === 'continue';
    const isContainer = !!def?.isContainer;
    const branchMetadata = (isContainer && !isContinuation)
      ? inferDefaultBranchMetadata(
          blockType ?? '',
          connection.sourceHandle,
          state.edges.filter((edge) => edge.source === connection.source),
        )
      : {};

    const edgeProps: Record<string, unknown> = {
      ...connection,
    };

    if (isContinuation) {
      // Continuation edges get explicit styling — bypass getBranchVisual
      edgeProps.style = { stroke: '#4a9eff' };
      edgeProps.label = 'next';
      edgeProps.labelStyle = { fill: '#4a9eff', fontSize: 9, fontWeight: 600 };
      // No data assignment — continuation edges carry no branch metadata
    } else {
      const branchVisual = isContainer
        ? getBranchVisual(blockType, branchMetadata)
        : { style: { stroke: '#666' } };
      edgeProps.style = branchVisual.style;
      if (branchVisual.label) edgeProps.label = branchVisual.label;
      if (branchVisual.labelStyle) edgeProps.labelStyle = branchVisual.labelStyle;
      if (isContainer) edgeProps.data = branchMetadata;
    }

    return {
      edges: addEdge(edgeProps as Edge, state.edges),
      isDirty: true,
      ...clearedExportStatusState(),
    };
  });
},
```

- [ ] **Step 2: Verify the app builds**

Run: `cd FlowCanvas && npm run build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/stores/slices/graphSlice.ts
git commit -m "feat(flow-canvas): handle continuation edges in onConnect — blue solid styling, no branch metadata"
```

---

### Task 6: Integration verification

**Files:**
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` (existing tests)

- [ ] **Step 1: Run full backend test suite**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
Expected: All tests pass

- [ ] **Step 2: Run frontend build**

Run: `cd FlowCanvas && npm run build`
Expected: Build succeeds with no errors

- [ ] **Step 3: Verify existing E2E parity tests still pass**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FlowCanvasBridge"`
Expected: All FlowCanvasBridge tests pass, including the 4 new tests from Tasks 2-3

- [ ] **Step 4: Commit any remaining changes**

If all clean, no commit needed. Otherwise:
```bash
git add -A
git commit -m "chore: integration verification cleanup"
```
