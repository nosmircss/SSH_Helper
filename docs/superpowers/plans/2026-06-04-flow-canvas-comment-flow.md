# Flow Canvas Comment Round-Trip — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make YAML `#` comments flow bidirectionally between a preset script and the Flow Canvas — imported comments are never dropped, render on the canvas, are editable, and new canvas-authored comments export as real `#` lines.

**Architecture:** Comments are modelled as the existing `CommentNode` (a `type:'comment'` React-Flow node) extended with `kind: 'comment' | 'sticky'` and an `anchor` keyed to the block's `_stepPath`. `kind:'comment'` exports as `#`; `kind:'sticky'` is visual-only (today's behavior). On import, `FlowCanvasBridge` captures `#` lines and emits comment nodes; on export it re-injects them from those nodes on **both** the snippet round-trip path and the regeneration paths, so comments survive edits. A "Compact comments" Display Setting (default ON) renders anchored comments as slim pills.

**Tech Stack:** C# (.NET 8, xUnit/FluentAssertions tests), React 18 + TypeScript + Zustand + @xyflow/react (vitest + Playwright), WebView2 JSON bridge.

**Conventions used throughout:**
- Run C# tests: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- Run React unit tests (from `FlowCanvas/`): `npm test`
- Run e2e (from `FlowCanvas/`): `npm run test:e2e`
- Build .NET only (no Node): `dotnet build SSH_Helper.sln -p:SkipFlowCanvasBuild=true`
- Per Development Guideline 8, the React and C# sides change together. Commit after each task.
- Line numbers below are from the 2026-06-04 recon; re-confirm with a quick read before editing (the files change often).

**Shared data shapes (defined once, referenced by later tasks):**

```text
NoteAnchor (logical):  { type: 'header' | 'leading' | 'inline'; stepPath?: string; lineOffset?: number }

React  CommentNodeData adds:   kind?: 'comment' | 'sticky';  anchor?: NoteAnchor
React  CommentData (export) adds:  kind?: string;  anchor?: NoteAnchor
C#     StepSnippetInfo adds:   IReadOnlyList<string> LeadingComments;  string? InlineComment
C#     comment node JSON: { id, type:'comment', position, data:{ commentId, blockType:'comment',
                            kind:'comment', text, anchor:{type,stepPath?,lineOffset?}, attachedToNodeId } }
```

Comment **text is stored without the leading `#`** (e.g. `Get hostname`); export re-emits `# {text}`. The bridge **strips captured comments out of `_yamlSnippet`** so the snippet is comment-free and the comment node is the single source of truth — this prevents double-emission across the round-trip and regeneration paths.

---

## Phase 1 — C# round-trip preservation (the data-loss fix)

End state: a YAML→graph→YAML round-trip preserves header, leading (incl. inside-branch on the round-trip path), and best-effort inline comments, with comments carried as comment nodes. This phase is independently valuable (it fixes the bug) and fully testable at the bridge level.

### Task 1: Capture leading & inline comments in `SplitYamlSteps`

**Files:**
- Modify: `Services/FlowCanvasBridge.cs` — `StepSnippetInfo` (line ~4221), `SplitYamlSteps` (line ~4229-4319)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Make `SplitYamlSteps` testable and extend the record.** Change the record struct and the method's accessibility (`InternalsVisibleTo("SSH_Helper.Tests")` is already set in the csproj).

Replace line ~4221:
```csharp
internal readonly record struct StepSnippetInfo(
    string Snippet,
    int BlankLinesBefore,
    IReadOnlyList<string> LeadingComments,
    string? InlineComment);
```

Add a helper just above `SplitYamlSteps` that splits an unquoted trailing comment off a line (best-effort; ignores `#` inside single/double quotes):
```csharp
/// <summary>
/// Splits a trailing YAML comment off a line. Returns false when there is no
/// safe unquoted trailing comment (e.g. the only '#' is inside a quoted string).
/// </summary>
internal static bool TrySplitTrailingComment(string line, out string code, out string comment)
{
    code = line;
    comment = string.Empty;
    bool inSingle = false, inDouble = false;
    for (int i = 0; i < line.Length; i++)
    {
        char c = line[i];
        if (c == '\'' && !inDouble) inSingle = !inSingle;
        else if (c == '"' && !inSingle) inDouble = !inDouble;
        else if (c == '#' && !inSingle && !inDouble && i > 0 && char.IsWhiteSpace(line[i - 1]))
        {
            code = line.Substring(0, i).TrimEnd();
            comment = StripHash(line.Substring(i));
            return true;
        }
    }
    return false;
}

/// <summary>Removes the leading '#' and a single following space from a comment line.</summary>
internal static string StripHash(string hashLine)
{
    var t = hashLine.TrimStart();
    if (t.StartsWith("#")) t = t.Substring(1);
    if (t.StartsWith(" ")) t = t.Substring(1);
    return t.TrimEnd();
}
```

- [ ] **Step 2: Write failing tests for the new capture behavior.**

Add to `FlowCanvasBridgeTests.cs`:
```csharp
[Fact]
public void SplitYamlSteps_LeadingCommentAttachesToNextStep_AndIsStrippedFromSnippet()
{
    var yaml = "steps:\n  # Get hostname\n  - send:\n      command: hostname\n";
    var steps = FlowCanvasBridge.SplitYamlSteps(yaml);

    Assert.Single(steps);
    Assert.Equal(new[] { "Get hostname" }, steps[0].LeadingComments);
    Assert.DoesNotContain("#", steps[0].Snippet);
    Assert.Contains("- send:", steps[0].Snippet);
}

[Fact]
public void SplitYamlSteps_InlineComment_IsCapturedAndStripped()
{
    var yaml = "steps:\n  - send:\n      command: cfg  # needs vdom\n";
    var steps = FlowCanvasBridge.SplitYamlSteps(yaml);

    Assert.Single(steps);
    Assert.Equal("needs vdom", steps[0].InlineComment);
    Assert.DoesNotContain("needs vdom", steps[0].Snippet);
}

[Fact]
public void SplitYamlSteps_HashInsideQuotes_IsNotTreatedAsComment()
{
    var yaml = "steps:\n  - send:\n      command: \"echo #1\"\n";
    var steps = FlowCanvasBridge.SplitYamlSteps(yaml);

    Assert.Single(steps);
    Assert.Null(steps[0].InlineComment);
    Assert.Contains("#1", steps[0].Snippet);
}
```

- [ ] **Step 3: Run the tests — expect FAIL** (the method does not yet populate these fields).

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter SplitYamlSteps`
Expected: FAIL (build error or assertion — fields not populated).

- [ ] **Step 4: Implement capture in `SplitYamlSteps`.** Rewrite the method to (a) buffer comment-only lines into `pendingComments`, (b) flush them onto the next step's `LeadingComments`, (c) split an inline comment off the step's first content line, (d) keep comment-only lines OUT of the snippet.

```csharp
internal static List<StepSnippetInfo> SplitYamlSteps(string yamlText)
{
    var steps = new List<StepSnippetInfo>();
    var lines = yamlText.Split('\n');

    int stepsLineIndex = -1;
    for (int i = 0; i < lines.Length; i++)
    {
        var t = lines[i].TrimEnd('\r');
        if (t == "steps:" || t == "steps: ") { stepsLineIndex = i; break; }
    }
    if (stepsLineIndex < 0) return steps;

    int stepIndent = -1;
    var currentStep = new StringBuilder();
    bool inStep = false;
    int blankLinesBefore = 0, currentBlankLines = 0;
    var pendingComments = new List<string>();          // leading comments for the NEXT step
    var currentLeading = new List<string>();           // leading comments for the step being built
    string? currentInline = null;                      // inline comment for the step being built
    bool firstContentLineSeen = false;                 // have we taken the inline from the step's first line yet

    void FinalizeStep()
    {
        steps.Add(new StepSnippetInfo(
            currentStep.ToString().TrimEnd('\r', '\n') + "\n",
            currentBlankLines,
            currentLeading.ToArray(),
            currentInline));
    }

    for (int i = stepsLineIndex + 1; i < lines.Length; i++)
    {
        var line = lines[i].TrimEnd('\r');

        if (string.IsNullOrWhiteSpace(line))
        {
            if (inStep) currentStep.AppendLine(line);
            blankLinesBefore++;
            continue;
        }

        var indent = line.Length - line.TrimStart().Length;
        var trimmed = line.TrimStart();

        // Comment-only line: buffer it for the next step instead of dropping it.
        if (trimmed.StartsWith("#"))
        {
            pendingComments.Add(StripHash(trimmed));
            blankLinesBefore = 0;
            continue;
        }

        if (trimmed.StartsWith("- ") || trimmed == "-")
        {
            if (stepIndent < 0) stepIndent = indent;

            if (indent == stepIndent)
            {
                if (inStep && currentStep.Length > 0) FinalizeStep();

                currentStep.Clear();
                currentLeading = new List<string>(pendingComments);
                pendingComments.Clear();
                currentInline = null;
                firstContentLineSeen = false;

                AppendStepLine(currentStep, line, ref currentInline, ref firstContentLineSeen);
                currentBlankLines = inStep ? blankLinesBefore : 0;
                blankLinesBefore = 0;
                inStep = true;
                continue;
            }
        }

        blankLinesBefore = 0;

        if (inStep && (indent > stepIndent ||
            (indent <= stepIndent && !trimmed.StartsWith("- "))))
        {
            AppendStepLine(currentStep, line, ref currentInline, ref firstContentLineSeen);
        }
    }

    if (inStep && currentStep.Length > 0) FinalizeStep();
    return steps;
}

// Appends a step line, taking the inline comment off the first content line only.
private static void AppendStepLine(StringBuilder sb, string line,
    ref string? inlineComment, ref bool firstContentLineSeen)
{
    if (!firstContentLineSeen)
    {
        firstContentLineSeen = true;
        if (TrySplitTrailingComment(line, out var code, out var comment))
        {
            inlineComment = comment;
            sb.AppendLine(code);
            return;
        }
    }
    sb.AppendLine(line);
}
```

Note: branch-internal comment lines (inside a container snippet) remain in the snippet untouched here — they survive the container round-trip path for free; full regeneration fidelity is Task 5b (deferrable).

- [ ] **Step 5: Run the tests — expect PASS.**

Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter SplitYamlSteps`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit.**
```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "feat(flow-canvas): capture leading & inline YAML comments in SplitYamlSteps"
```

### Task 2: Emit comment nodes from `TextToGraph`

**Files:**
- Modify: `Services/FlowCanvasBridge.cs` — `TextToGraph` step props region (line ~364-396) and the preamble/start emission
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write the failing test.**
```csharp
[Fact]
public void TextToGraph_LeadingComment_EmitsAnchoredCommentNode()
{
    var bridge = new FlowCanvasBridge();
    var yaml = "steps:\n  # Get hostname\n  - send:\n      command: hostname\n";

    var (nodes, _) = bridge.TextToGraph(yaml);

    var comment = nodes.OfType<JObject>().FirstOrDefault(n =>
        n["data"]?["blockType"]?.ToString() == "comment");
    Assert.NotNull(comment);
    Assert.Equal("comment", comment!["data"]?["kind"]?.ToString());
    Assert.Equal("Get hostname", comment["data"]?["text"]?.ToString());
    Assert.Equal("leading", comment["data"]?["anchor"]?["type"]?.ToString());
    Assert.Equal("steps/0", comment["data"]?["anchor"]?["stepPath"]?.ToString());
}
```

- [ ] **Step 2: Run it — expect FAIL** (no comment node emitted).
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter TextToGraph_LeadingComment`
Expected: FAIL (comment is null).

- [ ] **Step 3: Add a comment-node factory and emit nodes.** Add a helper near `TextToGraph`:
```csharp
private int _commentCounter;

private JObject BuildCommentNode(string text, string anchorType, string stepPath, string attachedToNodeId)
{
    var id = $"comment-c{_commentCounter++}";
    return new JObject
    {
        ["id"] = id,
        ["type"] = "comment",
        ["position"] = new JObject { ["x"] = 0, ["y"] = 0 },
        ["data"] = new JObject
        {
            ["commentId"] = id,
            ["blockType"] = "comment",
            ["kind"] = "comment",
            ["text"] = text,
            ["anchor"] = new JObject
            {
                ["type"] = anchorType,
                ["stepPath"] = stepPath,
            },
            ["attachedToNodeId"] = attachedToNodeId,
        },
    };
}
```

In the `TextToGraph` step loop (after the node is created and `stepProps`/`_stepPath` set, ~line 396), append comment nodes for this step. `nodes` is the `JArray` being built — confirm the variable name when editing.
```csharp
foreach (var c in snippetInfo.LeadingComments)
    nodes.Add(BuildCommentNode(c, "leading", stepPath, nodeId));
if (!string.IsNullOrEmpty(snippetInfo.InlineComment))
    nodes.Add(BuildCommentNode(snippetInfo.InlineComment!, "inline", stepPath, nodeId));
```

For **header** comments, parse them out of the preamble where the Start node is built and emit one `header`-anchored comment node per `#` line (anchored to `__start__`):
```csharp
// after the start node is created, with its id (e.g. "__start__"):
foreach (var line in ExtractPreambleComments(preamble))
    nodes.Add(BuildCommentNode(line, "header", "preamble", "__start__"));
```
Add the extractor:
```csharp
private static IEnumerable<string> ExtractPreambleComments(string preamble)
{
    foreach (var raw in preamble.Split('\n'))
    {
        var t = raw.TrimEnd('\r').TrimStart();
        if (t.StartsWith("#")) yield return StripHash(t);
    }
}
```
Also strip those `#` lines from the Start node's stored `_yamlSnippet` preamble so they are not double-emitted on export (find where `props["_yamlSnippet"] = preamble` is set in the start/preamble parse and filter comment-only lines out of the stored value).

- [ ] **Step 4: Run it — expect PASS.**
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter TextToGraph_LeadingComment`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "feat(flow-canvas): emit anchored comment nodes on import"
```

### Task 3: Re-inject comments on the export round-trip path

**Files:**
- Modify: `Services/FlowCanvasBridge.cs` — `ExportGraphToYaml` (line ~1037-1138; comment-node skip ~1053-1060; round-trip append ~1106-1111)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write the failing round-trip test.**
```csharp
[Fact]
public void RoundTrip_LeadingAndInlineComments_ArePreserved()
{
    var yaml = "steps:\n  # Create the address object\n  - send:\n      command: cfg  # needs vdom\n";
    var result = RoundTripThroughBridge(yaml);

    Assert.True(result.Success, string.Join(" | ", result.Errors));
    Assert.Contains("# Create the address object", result.Yaml);
    Assert.Contains("# needs vdom", result.Yaml);
    // No duplication of the inline comment.
    Assert.Equal(1, CountOccurrences(result.Yaml, "# needs vdom"));
}

private static int CountOccurrences(string haystack, string needle)
{
    int count = 0, idx = 0;
    while ((idx = haystack.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
    { count++; idx += needle.Length; }
    return count;
}
```

- [ ] **Step 2: Run it — expect FAIL** (comment nodes are skipped; comments absent from YAML).
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter RoundTrip_LeadingAndInline`
Expected: FAIL.

- [ ] **Step 3: Collect comment nodes before the emit loop, and inject.** Near the top of `ExportGraphToYaml`, before the `foreach (var nodeId in orderedIds)` loop (~line 1037), build indexes from comment-kind nodes:
```csharp
// Index comment-kind notes by their anchor for re-injection. Sticky-kind notes are visual-only.
var leadingByPath = new Dictionary<string, List<string>>(StringComparer.Ordinal);
var inlineByPath = new Dictionary<string, string>(StringComparer.Ordinal);
var headerComments = new List<string>();
foreach (var n in nodeMap.Values)
{
    var d = n["data"];
    if (!string.Equals(d?["blockType"]?.ToString(), "comment", StringComparison.OrdinalIgnoreCase)) continue;
    if (!string.Equals(d?["kind"]?.ToString(), "comment", StringComparison.OrdinalIgnoreCase)) continue;
    var text = d?["text"]?.ToString() ?? string.Empty;
    var anchor = d?["anchor"];
    var atype = anchor?["type"]?.ToString();
    var spath = anchor?["stepPath"]?.ToString() ?? string.Empty;
    if (atype == "header") headerComments.Add(text);
    else if (atype == "inline" && spath.Length > 0) inlineByPath[spath] = text;
    else if (atype == "leading" && spath.Length > 0)
    {
        if (!leadingByPath.TryGetValue(spath, out var list)) leadingByPath[spath] = list = new List<string>();
        list.Add(text);
    }
}
```

Change the comment-node **skip** (~1053-1060) so comment nodes are silently consumed (no warning — they are now first-class):
```csharp
if (string.Equals(blockType, "comment", StringComparison.OrdinalIgnoreCase))
    continue;
```

Add two local helpers (e.g. at the end of the class):
```csharp
private static void AppendLeadingComments(StringBuilder sb, IEnumerable<string>? comments, int indent)
{
    if (comments == null) return;
    var prefix = new string(' ', indent);
    foreach (var c in comments) sb.AppendLine($"{prefix}# {c}");
}

private static string AppendInlineComment(string stepYaml, string? inline)
{
    if (string.IsNullOrEmpty(inline)) return stepYaml;
    var nl = stepYaml.IndexOf('\n');
    if (nl < 0) return stepYaml.TrimEnd() + $"  # {inline}";
    return stepYaml.Substring(0, nl).TrimEnd() + $"  # {inline}" + stepYaml.Substring(nl);
}
```

In the **round-trip path** (~1102-1111), before `sb.Append(normalizedSnippet)`, prepend leading comments at indent 0 and apply the inline to the snippet's first line:
```csharp
var stepPathForComments = result.NodeToStepPathMap.TryGetValue(nodeId, out var sp) ? sp : existingStepPath ?? "";
AppendLeadingComments(sb, leadingByPath.GetValueOrDefault(stepPathForComments), 0);
var injected = AppendInlineComment(normalizedSnippet, inlineByPath.GetValueOrDefault(stepPathForComments));
sb.Append(injected);
```

Also emit `headerComments` into the preamble where the start/preamble YAML is written (find where the preamble is appended to `sb` at the top of export) — `AppendLeadingComments(sb, headerComments, 0)` immediately after any leading `---`/before `steps:` as appropriate. Verify placement against the preamble write site.

- [ ] **Step 4: Run it — expect PASS.**
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter RoundTrip_LeadingAndInline`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "feat(flow-canvas): re-inject comments on export round-trip path"
```

### Task 4: Re-inject comments on the leaf regeneration path

**Files:**
- Modify: `Services/FlowCanvasBridge.cs` — leaf path (~1117-1119, `TryGenerateStepYaml` caller)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write the failing test** (force regeneration so the snippet is bypassed):
```csharp
[Fact]
public void Export_LeafRegeneration_StillEmitsComments()
{
    var bridge = new FlowCanvasBridge();
    var yaml = "steps:\n  # label\n  - send:\n      command: hostname  # inline\n";
    var (nodes, edges) = bridge.TextToGraph(yaml);
    // Force the leaf to regenerate from props instead of the snippet.
    foreach (var n in nodes.OfType<JObject>())
    {
        var props = n["data"]?["props"] as JObject;
        if (props != null && props["_yamlSnippet"] != null) props["_forceGraphExport"] = true;
    }
    var result = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });

    Assert.True(result.Success, string.Join(" | ", result.Errors));
    Assert.Contains("# label", result.Yaml);
    Assert.Contains("# inline", result.Yaml);
}
```

- [ ] **Step 2: Run it — expect FAIL** (regeneration path doesn't inject comments).
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter Export_LeafRegeneration`
Expected: FAIL.

- [ ] **Step 3: Inject around the leaf path.** Replace the `TryGenerateStepYaml` success branch (~1117-1119):
```csharp
if (TryGenerateStepYaml(blockType, props, out var generatedYaml, out var error))
{
    var sp = result.NodeToStepPathMap.TryGetValue(nodeId, out var p) ? p : existingStepPath ?? "";
    AppendLeadingComments(sb, leadingByPath.GetValueOrDefault(sp), 0);
    sb.AppendLine(AppendInlineComment(generatedYaml, inlineByPath.GetValueOrDefault(sp)));
}
```

- [ ] **Step 4: Run it — expect PASS.**
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter Export_LeafRegeneration`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "feat(flow-canvas): re-inject comments on leaf regeneration path"
```

### Task 5: Re-inject leading comments on the container regeneration path

**Files:**
- Modify: `Services/FlowCanvasBridge.cs` — container path (~1087-1100)
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write the failing test** (a leading comment above a top-level container that regenerates):
```csharp
[Fact]
public void Export_ContainerRegeneration_EmitsLeadingComment()
{
    var bridge = new FlowCanvasBridge();
    var yaml = "steps:\n  # guard the loop\n  - foreach:\n      items: ${hosts}\n      as: h\n      do:\n        - print:\n            message: ${h}\n";
    var (nodes, edges) = bridge.TextToGraph(yaml);
    foreach (var n in nodes.OfType<JObject>())
    {
        var props = n["data"]?["props"] as JObject;
        if (props != null && props["_yamlSnippet"] != null) props["_forceGraphExport"] = true;
    }
    var result = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });

    Assert.True(result.Success, string.Join(" | ", result.Errors));
    Assert.Contains("# guard the loop", result.Yaml);
}
```

- [ ] **Step 2: Run it — expect FAIL.**
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter Export_ContainerRegeneration`
Expected: FAIL.

- [ ] **Step 3: Inject before the container is appended** (~line 1097). Replace:
```csharp
if (TryGenerateContainerFromGraph(
        blockType, props, nodeId, outgoing, nodeMap, incomingCount,
        consumedByContainer, result, out var containerYaml))
{
    var sp = result.NodeToStepPathMap.TryGetValue(nodeId, out var p) ? p : existingStepPath ?? "";
    AppendLeadingComments(sb, leadingByPath.GetValueOrDefault(sp), 0);
    sb.AppendLine(AppendInlineComment(containerYaml, inlineByPath.GetValueOrDefault(sp)));
    continue;
}
```

- [ ] **Step 4: Run it — expect PASS.**
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter Export_ContainerRegeneration`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add Services/FlowCanvasBridge.cs SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "feat(flow-canvas): re-inject leading comments on container regeneration"
```

> **Task 5b (deferrable, highest risk): branch-internal comments through regeneration.** Comments inside a container's branches survive the container round-trip path today (they ride the container snippet once Task 1 leaves them in place). Surviving *regeneration* additionally requires importing them as `leading`/`inline` comment nodes keyed to the **nested** `_stepPath` (e.g. `steps/0/do/0`) and injecting inside `TryGenerateBranchYaml` (~2087-2115) at the branch `indent` via `AppendLeadingComments(sb, leadingByPath.GetValueOrDefault(nestedPath), indent)`. Import-side capture of nested comments means recursing into branch bodies during graph construction. Scope this only if Phase 1 has budget; the core feature does not depend on it.

### Task 6: Byte-stability round-trip over a real sample

**Files:**
- Test: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write the test** asserting comments from a representative sample survive a no-op round-trip:
```csharp
[Fact]
public void RoundTrip_SystemInfoSample_PreservesSectionLabels()
{
    var path = System.IO.Path.Combine(
        TestPaths.RepoRoot, "ScriptSamples", "bash", "system_info.yaml");
    var yaml = System.IO.File.ReadAllText(path);
    var result = RoundTripThroughBridge(yaml);

    Assert.True(result.Success, string.Join(" | ", result.Errors));
    foreach (var label in new[] { "# Get hostname", "# Get OS info", "# Get memory info" })
        Assert.Contains(label, result.Yaml);
}
```
If `TestPaths.RepoRoot` does not exist, resolve the path relative to `AppContext.BaseDirectory` walking up to the repo root, or hardcode via an existing test helper used elsewhere in the suite (grep `ScriptSamples` in the test project for the established pattern).

- [ ] **Step 2: Run it — expect PASS** (Tasks 1–5 make it pass). If a label is missing, the gap reveals which comment kind still drops; fix in the owning task.
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter RoundTrip_SystemInfoSample`
Expected: PASS.

- [ ] **Step 3: Run the full bridge suite to catch regressions.**
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter FlowCanvasBridge`
Expected: PASS (all).

- [ ] **Step 4: Commit.**
```bash
git add SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs
git commit -m "test(flow-canvas): byte-stable comment round-trip over system_info sample"
```

**Phase 1 checkpoint:** comments now survive YAML→graph→YAML on every path. Review before Phase 2.

---

## Phase 2 — React: render & edit comments

End state: imported comment nodes render on the canvas (pills when compact, anchored above their block), and are editable.

### Task 7: Extend the comment data model (React)

**Files:**
- Modify: `FlowCanvas/src/nodes/CommentNode.tsx` (interface, line ~6-11)
- Test: `FlowCanvas/src/stores/slices/__tests__/commentSlice.kind.test.ts` (create)

- [ ] **Step 1: Extend the interface.** Replace lines ~6-11:
```typescript
export interface NoteAnchor {
  type: 'header' | 'leading' | 'inline';
  stepPath?: string;
  lineOffset?: number;
}

export interface CommentNodeData {
  commentId: string;
  text: string;
  color?: string;
  kind?: 'comment' | 'sticky';
  anchor?: NoteAnchor;
  attachedToNodeId?: string;
  [key: string]: unknown;
}
```

- [ ] **Step 2: Write the failing test** that `updateComment` preserves `kind`/`anchor`:
```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
import { useFlowStore } from '../../useFlowStore';

describe('commentSlice kind/anchor', () => {
  beforeEach(() => { useFlowStore.setState({ nodes: [], edges: [] }); vi.clearAllMocks(); });

  it('updateComment preserves kind and anchor', () => {
    useFlowStore.setState({ nodes: [{
      id: 'c1', type: 'comment', position: { x: 0, y: 0 },
      data: { commentId: 'c1', text: 'x', kind: 'comment', anchor: { type: 'leading', stepPath: 'steps/0' } },
    }] as never });
    useFlowStore.getState().updateComment('c1', { text: 'y' });
    const n = useFlowStore.getState().nodes.find((m) => m.id === 'c1')!;
    expect((n.data as Record<string, unknown>).kind).toBe('comment');
    expect((n.data as Record<string, unknown>).anchor).toEqual({ type: 'leading', stepPath: 'steps/0' });
    expect((n.data as Record<string, unknown>).text).toBe('y');
  });
});
```

- [ ] **Step 3: Run it — expect PASS** (the existing `updateComment` spreads `...n.data`, so kind/anchor already survive). This test is a guard; if it fails, fix `updateComment` to spread existing data first.
Run (from `FlowCanvas/`): `npm test -- commentSlice.kind`
Expected: PASS.

- [ ] **Step 4: Commit.**
```bash
git add FlowCanvas/src/nodes/CommentNode.tsx FlowCanvas/src/stores/slices/__tests__/commentSlice.kind.test.ts
git commit -m "feat(flow-canvas): add kind/anchor to comment data model"
```

### Task 8: Render comment vs sticky, pill vs full

**Files:**
- Modify: `FlowCanvas/src/nodes/CommentNode.tsx` (render, line ~64-149)
- Test: `FlowCanvas/src/nodes/__tests__/CommentNode.test.tsx` (create)

- [ ] **Step 1: Write the failing render test** using the existing vitest+jsdom harness (mirror `flow-canvas-vitest-harness` memory; assert at string/DOM level, not computed color):
```typescript
import { describe, it, expect, vi } from 'vitest';
import { render } from '@testing-library/react';
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (sel: (s: unknown) => unknown) => sel({
    updateComment: vi.fn(), removeComment: vi.fn(), compactCommentsEnabled: true,
  }),
}));
import CommentNode from '../CommentNode';

it('renders a compact pill for an anchored comment-kind note', () => {
  const { container } = render(
    <CommentNode id="c1" data={{ commentId: 'c1', text: 'Get hostname', kind: 'comment',
      anchor: { type: 'leading', stepPath: 'steps/0' } }} /> as never);
  const pill = container.querySelector('[data-testid="comment-pill"]');
  expect(pill).not.toBeNull();
  expect(pill!.textContent).toContain('Get hostname');
});
```
(If `CommentNode` needs more `NodeProps` fields to render under jsdom, pass minimal stubs; follow an existing node test in the repo for the required prop shape.)

- [ ] **Step 2: Run it — expect FAIL** (no `comment-pill` testid).
Run (from `FlowCanvas/`): `npm test -- CommentNode`
Expected: FAIL.

- [ ] **Step 3: Branch the render.** In `CommentNode`, read `kind`, `anchor`, and the compact setting, then render a pill for compact anchored comment-kind notes and the full box otherwise. Add near the top of the component:
```typescript
const compact = useFlowStore((s) => s.compactCommentsEnabled);
const kind = (commentData.kind as 'comment' | 'sticky' | undefined) ?? 'sticky';
const anchorType = commentData.anchor?.type;
const isComment = kind === 'comment';
const renderPill = compact && isComment && (anchorType === 'leading' || anchorType === 'header');
```
Before the existing `return (` full box, add:
```typescript
if (renderPill && !editing) {
  return (
    <div
      data-testid="comment-pill"
      onDoubleClick={handleDoubleClick}
      style={{
        display: 'inline-flex', alignItems: 'center', gap: 6,
        background: 'var(--fc-comment-pill-bg, rgba(126,224,138,0.10))',
        borderLeft: '3px solid var(--fc-comment-pill-accent, #7ee08a)',
        borderRadius: 3, padding: '2px 9px', fontFamily: 'ui-monospace, Consolas, monospace',
        fontSize: 11.5, color: 'var(--fc-comment-pill-ink, #9fd6ab)', cursor: 'grab',
      }}
      title="Double-click to edit"
    >
      <span style={{ color: 'var(--fc-accent)', fontWeight: 700 }}>#</span>
      {text || 'comment'}
    </div>
  );
}
```
Add a `data-testid="comment-full"` to the existing full-box root `<div>` for symmetry.

- [ ] **Step 4: Run it — expect PASS.**
Run (from `FlowCanvas/`): `npm test -- CommentNode`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add FlowCanvas/src/nodes/CommentNode.tsx FlowCanvas/src/nodes/__tests__/CommentNode.test.tsx
git commit -m "feat(flow-canvas): render comment pills vs full sticky notes"
```

### Task 9: Edit comments in the Properties panel

**Files:**
- Create: `FlowCanvas/src/panels/CommentProperties.tsx`
- Modify: `FlowCanvas/src/panels/Properties.tsx` (branch after `_start` check, ~line 1596)
- Test: `FlowCanvas/src/panels/__tests__/CommentProperties.test.tsx` (create)

- [ ] **Step 1: Create `CommentProperties.tsx`** — a small panel that edits `text`, `color`, shows `kind` and read-only anchor, mirroring the field/label styling from `Properties.tsx` (`var(--fc-...)` tokens):
```typescript
import { useFlowStore } from '../stores/useFlowStore';
import type { CommentNodeData } from '../nodes/CommentNode';

export function CommentProperties({ nodeId, data }: { nodeId: string; data: CommentNodeData }) {
  const updateComment = useFlowStore((s) => s.updateComment);
  const labelStyle = { fontSize: 11, color: 'var(--fc-text-muted)', display: 'block', marginBottom: 3 } as const;
  const inputStyle = {
    width: '100%', background: 'var(--fc-input-bg)', border: '1px solid var(--fc-border)',
    borderRadius: 4, color: 'var(--fc-text)', fontSize: 12, padding: '6px 8px',
  } as const;
  return (
    <div data-testid="comment-properties" style={{ flex: 1, padding: 16, overflowY: 'auto' }}>
      <label style={labelStyle}>Text</label>
      <textarea
        data-testid="comment-text-input"
        value={String(data.text ?? '')}
        onChange={(e) => updateComment(nodeId, { text: e.target.value })}
        rows={3}
        style={{ ...inputStyle, resize: 'vertical' }}
      />
      <label style={{ ...labelStyle, marginTop: 10 }}>Kind</label>
      <select
        data-testid="comment-kind-input"
        value={(data.kind as string) ?? 'sticky'}
        onChange={(e) => updateComment(nodeId, { kind: e.target.value })}
        style={inputStyle}
      >
        <option value="comment">comment (exports as #)</option>
        <option value="sticky">sticky (visual only)</option>
      </select>
      {data.anchor && (
        <div style={{ marginTop: 10, fontSize: 11, color: 'var(--fc-text-muted)' }}>
          Anchor: {data.anchor.type}{data.anchor.stepPath ? ` · ${data.anchor.stepPath}` : ''}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Branch Properties.tsx to it.** After the `_start` check (~line 1596) add:
```typescript
if (selectedNodeId && node && node.type === 'comment') {
  return <CommentProperties nodeId={selectedNodeId} data={blockData as unknown as CommentNodeData} />;
}
```
Add the import at the top of `Properties.tsx`:
```typescript
import { CommentProperties } from './CommentProperties';
import type { CommentNodeData } from '../nodes/CommentNode';
```

- [ ] **Step 3: Write the test.**
```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, fireEvent } from '@testing-library/react';
const updateComment = vi.fn();
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: (sel: (s: unknown) => unknown) => sel({ updateComment }),
}));
import { CommentProperties } from '../CommentProperties';

it('edits comment text and kind', () => {
  const { getByTestId } = render(
    <CommentProperties nodeId="c1" data={{ commentId: 'c1', text: 'a', kind: 'comment' }} />);
  fireEvent.change(getByTestId('comment-text-input'), { target: { value: 'b' } });
  expect(updateComment).toHaveBeenCalledWith('c1', { text: 'b' });
  fireEvent.change(getByTestId('comment-kind-input'), { target: { value: 'sticky' } });
  expect(updateComment).toHaveBeenCalledWith('c1', { kind: 'sticky' });
});
```

- [ ] **Step 4: Run it — expect PASS.**
Run (from `FlowCanvas/`): `npm test -- CommentProperties`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add FlowCanvas/src/panels/CommentProperties.tsx FlowCanvas/src/panels/Properties.tsx FlowCanvas/src/panels/__tests__/CommentProperties.test.tsx
git commit -m "feat(flow-canvas): edit comments in the Properties panel"
```

### Task 10: Auto-place anchored comments above their block

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts` (`load-graph` handler, ~128-164) and/or the layout utility under `FlowCanvas/src/utils/layout/`
- Test: `FlowCanvas/src/utils/layout/__tests__/placeAnchoredComments.test.ts` (create)

- [ ] **Step 1: Create a pure placement helper** `FlowCanvas/src/utils/layout/placeAnchoredComments.ts`:
```typescript
import type { Node } from '@xyflow/react';

const PILL_GAP = 34;

/** Positions anchored comment nodes just above their attached block. Pure; returns a new array. */
export function placeAnchoredComments(nodes: Node[]): Node[] {
  const byId = new Map(nodes.map((n) => [n.id, n]));
  return nodes.map((n) => {
    if (n.type !== 'comment') return n;
    const data = n.data as Record<string, unknown> | undefined;
    const anchor = data?.anchor as { type?: string } | undefined;
    const attachedTo = data?.attachedToNodeId as string | undefined;
    if (!attachedTo || (anchor?.type !== 'leading' && anchor?.type !== 'header')) return n;
    const target = byId.get(attachedTo);
    if (!target) return n;
    return { ...n, position: { x: target.position.x, y: target.position.y - PILL_GAP } };
  });
}
```

- [ ] **Step 2: Write the test.**
```typescript
import { describe, it, expect } from 'vitest';
import { placeAnchoredComments } from '../placeAnchoredComments';

it('places a leading comment above its attached block', () => {
  const nodes = [
    { id: 'b1', type: 'block', position: { x: 100, y: 200 }, data: {} },
    { id: 'c1', type: 'comment', position: { x: 0, y: 0 },
      data: { attachedToNodeId: 'b1', anchor: { type: 'leading' } } },
  ] as never[];
  const out = placeAnchoredComments(nodes);
  const c1 = out.find((n) => n.id === 'c1')!;
  expect(c1.position.x).toBe(100);
  expect(c1.position.y).toBeLessThan(200);
});
```

- [ ] **Step 3: Run it — expect FAIL then PASS** after creating the file.
Run (from `FlowCanvas/`): `npm test -- placeAnchoredComments`
Expected: PASS.

- [ ] **Step 4: Call it after layout in `load-graph`.** In `messageBridge.ts`, after `computeHierarchicalLayout(...)` is applied (~line 138) and on the user-layout branch too, wrap the node set:
```typescript
store.getState().setNodes(placeAnchoredComments(store.getState().nodes));
```
Add the import: `import { placeAnchoredComments } from '../utils/layout/placeAnchoredComments';`

- [ ] **Step 5: Verify the unit suite still passes.**
Run (from `FlowCanvas/`): `npm test`
Expected: PASS.

- [ ] **Step 6: Commit.**
```bash
git add FlowCanvas/src/utils/layout/placeAnchoredComments.ts FlowCanvas/src/utils/layout/__tests__/placeAnchoredComments.test.ts FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat(flow-canvas): auto-place anchored comments above their block"
```

**Phase 2 checkpoint:** imported comments render and edit on the canvas. Review before Phase 3.

---

## Phase 3 — Authoring + the Compact-comments display setting

End state: users author new `comment`/`sticky` notes; comment-kind notes export as `#`; a Display Setting (default compact) controls pill vs full; the setting persists.

### Task 11: Preserve kind/anchor in the export payload

**Files:**
- Modify: `FlowCanvas/src/utils/exportGraph.ts` (`CommentData` ~6-15, `buildExecutableGraphPayload` ~107-139)
- Test: `FlowCanvas/src/utils/__tests__/exportGraph.comments.test.ts` (create)

- [ ] **Step 1: Extend `CommentData`** (line ~6-15):
```typescript
export interface CommentData {
  id: string;
  text: string;
  color: string;
  x: number;
  y: number;
  width: number;
  height: number;
  attachedToNodeId?: string;
  kind?: string;
  anchor?: { type: string; stepPath?: string; lineOffset?: number };
}
```

- [ ] **Step 2: Add kind/anchor to the pushed comment** in `buildExecutableGraphPayload` (the `comments.push({...})`):
```typescript
        kind: typeof data?.kind === 'string' ? data.kind : undefined,
        anchor: (data?.anchor && typeof data.anchor === 'object')
          ? (data.anchor as { type: string; stepPath?: string; lineOffset?: number })
          : undefined,
```

- [ ] **Step 3: Write the test.**
```typescript
import { describe, it, expect, vi } from 'vitest';
vi.mock('../../stores/useFlowStore', () => ({
  useFlowStore: { getState: () => ({ disabledBlocks: new Set() }) },
}));
import { buildExecutableGraphPayload } from '../exportGraph';

it('preserves kind and anchor on exported comments', () => {
  const nodes = [{ id: 'c1', type: 'comment', position: { x: 1, y: 2 },
    data: { text: 'hi', kind: 'comment', anchor: { type: 'leading', stepPath: 'steps/0' } } }] as never[];
  const payload = buildExecutableGraphPayload(nodes, []);
  expect(payload.comments[0].kind).toBe('comment');
  expect(payload.comments[0].anchor).toEqual({ type: 'leading', stepPath: 'steps/0' });
});
```

- [ ] **Step 4: Run it — expect PASS.**
Run (from `FlowCanvas/`): `npm test -- exportGraph.comments`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add FlowCanvas/src/utils/exportGraph.ts FlowCanvas/src/utils/__tests__/exportGraph.comments.test.ts
git commit -m "feat(flow-canvas): preserve comment kind/anchor in export payload"
```

> **C# consume note:** `FlowCanvasForm` forwards the `apply-yaml` payload to `FlowCanvasBridge.ExportGraphToYaml`. Comments live in the graph `nodes` array (the bridge reads comment nodes directly — Task 3). If `apply-yaml` sends comments only in the separate `comments[]` array and strips comment nodes from `nodes`, add a merge in `FlowCanvasForm`'s apply-yaml handler that re-attaches `comments[]` as `type:'comment'` nodes before calling the bridge. Verify which path is live and add the merge only if needed.

### Task 12: Two creation actions — Add comment / Add sticky

**Files:**
- Modify: `FlowCanvas/src/stores/slices/commentSlice.ts` (interface ~6-10, `addComment` ~15-35)
- Modify: `FlowCanvas/src/panels/BlockContextMenu.tsx` (menuItems ~71-118)
- Test: `FlowCanvas/src/stores/slices/__tests__/commentSlice.create.test.ts` (create)

- [ ] **Step 1: Add `kind` to `addComment` and a default.** Update the interface:
```typescript
  addComment: (position: { x: number; y: number }, attachedToNodeId?: string, kind?: 'comment' | 'sticky') => void;
```
Update the implementation's `data` block to include kind (default `sticky`):
```typescript
    data: {
      commentId: id,
      text: '',
      color: DEFAULT_COMMENT_COLOR,
      attachedToNodeId,
      kind: kind ?? 'sticky',
      ...(attachedToNodeId ? { anchor: { type: 'leading' as const } } : {}),
    },
```

- [ ] **Step 2: Write the test.**
```typescript
import { describe, it, expect, vi } from 'vitest';
vi.mock('../../../utils/layoutAutosave', () => ({ sendLayoutAutosave: vi.fn() }));
import { useFlowStore } from '../../useFlowStore';

describe('addComment kinds', () => {
  beforeEach(() => useFlowStore.setState({ nodes: [] }));
  it('creates a comment-kind note', () => {
    useFlowStore.getState().addComment({ x: 0, y: 0 }, 'b1', 'comment');
    const n = useFlowStore.getState().nodes.at(-1)!;
    expect((n.data as Record<string, unknown>).kind).toBe('comment');
  });
  it('defaults to sticky', () => {
    useFlowStore.getState().addComment({ x: 0, y: 0 });
    const n = useFlowStore.getState().nodes.at(-1)!;
    expect((n.data as Record<string, unknown>).kind).toBe('sticky');
  });
});
```

- [ ] **Step 3: Run it — expect PASS.**
Run (from `FlowCanvas/`): `npm test -- commentSlice.create`
Expected: PASS.

- [ ] **Step 4: Replace the single context-menu item** in `BlockContextMenu.tsx` (~82-89) with two entries:
```typescript
    {
      label: 'Add Comment (#)',
      icon: '💬',
      action: () => { addComment(commentPos, nodeId, 'comment'); hideContextMenu(); },
    },
    {
      label: 'Add Sticky',
      icon: '📌',
      action: () => { addComment(commentPos, nodeId, 'sticky'); hideContextMenu(); },
    },
```
(No signature change needed at the call site beyond the third arg; `addComment` is already imported at line ~23.)

- [ ] **Step 5: Run the unit suite.**
Run (from `FlowCanvas/`): `npm test`
Expected: PASS.

- [ ] **Step 6: Commit.**
```bash
git add FlowCanvas/src/stores/slices/commentSlice.ts FlowCanvas/src/panels/BlockContextMenu.tsx FlowCanvas/src/stores/slices/__tests__/commentSlice.create.test.ts
git commit -m "feat(flow-canvas): add comment vs sticky creation actions"
```

### Task 13: Compact-comments display setting (React)

**Files:**
- Modify: `FlowCanvas/src/stores/slices/uiSlice.ts` (interface ~23-27 & ~52-62, defaults ~82-88, impl ~130-135)
- Modify: `FlowCanvas/src/panels/SettingsPopover.tsx` (selectors ~71-78, toggles ~133-139)
- Modify: `FlowCanvas/src/stores/messageBridge.ts` (layout-restore ~401-402)
- Test: `FlowCanvas/src/stores/slices/__tests__/uiSlice.compactComments.test.ts` (create)

- [ ] **Step 1: Add state, setters, default, and impl** in `uiSlice.ts`, mirroring `branchBands` exactly.
  - After `branchBandsEnabled: boolean;` (line ~27): `compactCommentsEnabled: boolean;`
  - After `restoreBranchBands: (value: boolean) => void;` (line ~60): 
    ```typescript
    toggleCompactComments: () => void;
    restoreCompactComments: (value: boolean) => void;
    ```
  - After `branchBandsEnabled: true,` (line ~86): `compactCommentsEnabled: true,`
  - After the `restoreBranchBands` impl (line ~135):
    ```typescript
    toggleCompactComments: () => set((s) => {
      const next = !s.compactCommentsEnabled;
      messageBus.send({ type: CANVAS_HOST_MESSAGES.outgoing.layoutSave, compactCommentsEnabled: next });
      return { compactCommentsEnabled: next };
    }),
    restoreCompactComments: (value) => set({ compactCommentsEnabled: value }),
    ```

- [ ] **Step 2: Add the toggle UI** in `SettingsPopover.tsx`.
  - After the `branchBandsEnabled`/`toggleBranchBands` selectors (line ~73):
    ```typescript
    const compactCommentsEnabled = useFlowStore((s) => s.compactCommentsEnabled);
    const toggleCompactComments = useFlowStore((s) => s.toggleCompactComments);
    ```
  - After the Branch bands `<Toggle>` (line ~136):
    ```tsx
    <Toggle label="Compact comments" on={compactCommentsEnabled} onClick={toggleCompactComments} />
    ```

- [ ] **Step 3: Restore on load** in `messageBridge.ts` (after line ~402):
```typescript
if (typeof msg.compactCommentsEnabled === 'boolean') store.getState().restoreCompactComments(msg.compactCommentsEnabled);
```

- [ ] **Step 4: Write the test** (mirror `settingsSlice.test.ts` MessageBus mock):
```typescript
import { describe, it, expect, beforeEach, vi } from 'vitest';
vi.mock('../../../MessageBus', () => ({
  messageBus: { send: vi.fn() },
  CANVAS_HOST_MESSAGES: { outgoing: { layoutSave: 'layout-save' } },
}));
import { useFlowStore } from '../../useFlowStore';
import { messageBus } from '../../../MessageBus';

describe('compactComments setting', () => {
  beforeEach(() => { useFlowStore.setState({ compactCommentsEnabled: true }); vi.clearAllMocks(); });
  it('defaults ON', () => { expect(useFlowStore.getState().compactCommentsEnabled).toBe(true); });
  it('toggle flips and persists via layout-save', () => {
    useFlowStore.getState().toggleCompactComments();
    expect(useFlowStore.getState().compactCommentsEnabled).toBe(false);
    expect(messageBus.send).toHaveBeenCalledWith(
      expect.objectContaining({ type: 'layout-save', compactCommentsEnabled: false }));
  });
  it('restore applies host value', () => {
    useFlowStore.getState().restoreCompactComments(false);
    expect(useFlowStore.getState().compactCommentsEnabled).toBe(false);
  });
});
```

- [ ] **Step 5: Run it — expect PASS.**
Run (from `FlowCanvas/`): `npm test -- uiSlice.compactComments`
Expected: PASS.

- [ ] **Step 6: Commit.**
```bash
git add FlowCanvas/src/stores/slices/uiSlice.ts FlowCanvas/src/panels/SettingsPopover.tsx FlowCanvas/src/stores/messageBridge.ts FlowCanvas/src/stores/slices/__tests__/uiSlice.compactComments.test.ts
git commit -m "feat(flow-canvas): add Compact comments display setting (default on)"
```

### Task 14: Persist the Compact-comments setting (C#)

**Files:**
- Modify: `Models/AppConfiguration.cs` (`WindowState`, line ~519)
- Modify: `UI/FlowCanvasForm.cs` (layout-restore ~369-380, extract ~394-401, guard ~402-404, persist ~406-418)
- Test: `SSH_Helper.Tests/` config round-trip test (locate the existing `WindowState`/`ConfigurationService` test file and add a case)

- [ ] **Step 1: Add the property** after `FlowCanvasBranchBands` (line ~519):
```csharp
public bool? FlowCanvasCompactComments { get; set; }
```

- [ ] **Step 2: Wire `FlowCanvasForm.cs`** (four edits, mirroring `branchBands`):
  - layout-restore object (after `branchBandsEnabled = ws.FlowCanvasBranchBands,`, line ~379):
    ```csharp
    compactCommentsEnabled = ws.FlowCanvasCompactComments,
    ```
  - extract from message (after `var bands = ...`, line ~400):
    ```csharp
    var compact = msg["compactCommentsEnabled"]?.Value<bool>();
    ```
  - early-return guard (line ~402-404): append `&& compact == null` to the condition.
  - persist (after `if (bands.HasValue) ...`, line ~417):
    ```csharp
    if (compact.HasValue) c.WindowState.FlowCanvasCompactComments = compact.Value;
    ```

- [ ] **Step 3: Write/extend a config round-trip test** asserting the property persists. Locate the test (grep `FlowCanvasBranchBands` in `SSH_Helper.Tests`) and add an analogous assertion: set `FlowCanvasCompactComments = false`, save via `ConfigurationService`, reload, assert it equals `false`.

- [ ] **Step 4: Run it — expect PASS.**
Run: `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter WindowState`
Expected: PASS.

- [ ] **Step 5: Commit.**
```bash
git add Models/AppConfiguration.cs UI/FlowCanvasForm.cs SSH_Helper.Tests/
git commit -m "feat(flow-canvas): persist Compact comments setting to WindowState"
```

### Task 15: End-to-end — import shows pills, edit survives export

**Files:**
- Create: `FlowCanvas/e2e/flow-canvas-comments.spec.ts`

- [ ] **Step 1: Write the e2e spec** (mirror `flow-canvas-auto-layout.spec.ts` harness; per `flow-canvas-e2e-gotchas` memory use `toHaveCount`/DOM checks, not zoom-scaled boxes):
```typescript
import { expect, test } from '@playwright/test';
import {
  clearOutgoingMessages, installHostMessageCapture, postHostMessage, waitForOutgoingMessage,
} from './support/harness';

const graphWithComment = {
  nodes: [
    { id: '__start__', type: 'start', position: { x: 0, y: 0 }, data: { blockType: '_start', label: 'Start', props: {} } },
    { id: 'n1', type: 'block', position: { x: 0, y: 120 }, data: { blockType: 'send', label: 'send', props: { command: 'hostname', _stepPath: 'steps/0' } } },
    { id: 'c1', type: 'comment', position: { x: 0, y: 0 }, data: { commentId: 'c1', blockType: 'comment', kind: 'comment', text: 'Get hostname', anchor: { type: 'leading', stepPath: 'steps/0' }, attachedToNodeId: 'n1' } },
  ],
  edges: [{ id: 'e0', source: '__start__', target: 'n1' }],
};

test.describe('Flow Canvas comments', () => {
  test.beforeEach(async ({ page }) => {
    await installHostMessageCapture(page);
    await page.goto('/');
    await waitForOutgoingMessage(page, 'ready');
    await clearOutgoingMessages(page);
  });

  test('imported comment renders as a pill', async ({ page }) => {
    await postHostMessage(page, { type: 'load-graph', ...graphWithComment });
    await expect(page.locator('[data-testid="comment-pill"]')).toHaveCount(1);
    await expect(page.locator('[data-testid="comment-pill"]')).toContainText('Get hostname');
  });
});
```

- [ ] **Step 2: Run it — expect PASS.**
Run (from `FlowCanvas/`): `npm run test:e2e -- flow-canvas-comments`
Expected: PASS.

- [ ] **Step 3: Full build + suites green.**
Run: `dotnet build SSH_Helper.sln` then `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` and (from `FlowCanvas/`) `npm test`.
Expected: PASS (all).

- [ ] **Step 4: Commit.**
```bash
git add FlowCanvas/e2e/flow-canvas-comments.spec.ts
git commit -m "test(flow-canvas): e2e imported comment renders as pill"
```

---

## Self-review

**Spec coverage:**
- Preserve imported comments → Phase 1 (Tasks 1–6). ✓
- Show + edit on canvas → Phase 2 (Tasks 7–10). ✓
- Author new → Phase 3 (Tasks 11, 12). ✓
- `comment` vs `sticky` kind → Tasks 7, 8, 12. ✓
- Anchor by `_stepPath` → Tasks 2–5, 10. ✓
- Compact pills default + Display Setting + persistence → Tasks 8, 13, 14. ✓
- Auto-place *above* the block → Task 10. ✓
- Inline best-effort + graceful behavior → Tasks 1, 3, 4. ✓
- Branch-internal: round-trip preserved (Task 1), regeneration fidelity → Task 5b (deferrable, flagged). ✓
- "Visual never touches YAML" → export only injects `kind:'comment'` (Task 3 index filter). ✓
- CRLF: capture strips `\r` (Task 1 mirrors existing `TrimEnd('\r')`); export emits `\n` via `AppendLine`. ✓
- No new handshake: new fields ride existing `load-graph`/`apply-yaml`/`layout-save`. ✓

**Type consistency:** `kind: 'comment' | 'sticky'` and `anchor: {type, stepPath?, lineOffset?}` are identical across `CommentNodeData` (Task 7), `CommentData` (Task 11), the C# comment-node JSON (Task 2), and the export indexes (Task 3). Helper names `AppendLeadingComments`/`AppendInlineComment`/`TrySplitTrailingComment`/`StripHash`/`BuildCommentNode`/`placeAnchoredComments` are used consistently. `compactCommentsEnabled` / `FlowCanvasCompactComments` paired across React/C#.

**Open risk (flagged, not blocking):** branch-internal regeneration fidelity (Task 5b) and the `apply-yaml` comment-node merge (note under Task 11) both require confirming a live code path before editing — each has an explicit verify-first instruction.
