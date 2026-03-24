# Start Block Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a mandatory, non-deletable Start Block to the Flow Canvas that serves as the visual entry point and script-property editor, replacing the hidden `__preamble__` metadata node.

**Architecture:** New React Flow node type (`StartNode`) registered alongside `block` and `comment`. Properties panel detects `_start` blockType and renders a custom script-settings form. C# bridge parses/serializes preamble fields into Start node props instead of raw YAML snippet. Delete/copy/paste operations are guarded to protect the Start node.

**Tech Stack:** React 18, TypeScript, @xyflow/react, Zustand, C# (.NET 8), Newtonsoft.Json

**Spec:** `docs/superpowers/specs/2026-03-24-start-block-design.md`

---

### Task 1: Create StartNode React Component

**Files:**
- Create: `FlowCanvas/src/nodes/StartNode.tsx`

- [ ] **Step 1: Create `StartNode.tsx` with basic structure**

```tsx
// FlowCanvas/src/nodes/StartNode.tsx
import { memo, type CSSProperties } from 'react';
import { Handle, Position, type NodeProps } from '@xyflow/react';

export interface StartNodeData {
  blockType: '_start';
  label?: string;
  props?: {
    name?: string;
    description?: string;
    environment?: string;
    version?: number;
    debug?: boolean;
    nobanner?: boolean;
    suppress_missing_column_warning?: boolean;
    library?: boolean;
    vars?: Record<string, unknown>;
    imports?: string[];
    _yamlSnippet?: string;
  };
  [key: string]: unknown;
}

/** Boolean flags shown as badges on the Start node when active. */
const FLAG_KEYS: { key: string; label: string }[] = [
  { key: 'debug', label: 'debug' },
  { key: 'nobanner', label: 'nobanner' },
  { key: 'suppress_missing_column_warning', label: 'no-warn' },
  { key: 'library', label: 'library' },
];

function StartNode({ data, selected }: NodeProps) {
  const startData = data as StartNodeData;
  const props = startData.props ?? {};
  const scriptName = props.name || startData.label || 'Untitled Script';

  // Collect active boolean flag badges
  const activeBadges: string[] = [];
  for (const flag of FLAG_KEYS) {
    if (props[flag.key as keyof typeof props]) {
      activeBadges.push(flag.label);
    }
  }

  // Count badges for vars and imports
  const varsCount = props.vars ? Object.keys(props.vars).length : 0;
  const importsCount = props.imports ? props.imports.length : 0;
  if (varsCount > 0) activeBadges.push(`${varsCount} var${varsCount !== 1 ? 's' : ''}`);
  if (importsCount > 0) activeBadges.push(`${importsCount} import${importsCount !== 1 ? 's' : ''}`);

  const containerStyle: CSSProperties = {
    background: 'linear-gradient(135deg, #1a2a3a, #0d1a2a)',
    border: `2px solid ${selected ? '#fff' : '#4a9eff'}`,
    borderRadius: 8,
    minWidth: 260,
    maxWidth: 300,
    overflow: 'hidden',
    boxShadow: selected
      ? '0 0 12px rgba(255,255,255,0.15)'
      : '0 0 12px rgba(74, 158, 255, 0.15)',
    transition: 'box-shadow 0.2s, border-color 0.2s',
  };

  return (
    <div style={containerStyle}>
      {/* Header */}
      <div style={{
        padding: '6px 10px',
        borderBottom: '1px solid rgba(74,158,255,0.2)',
        display: 'flex',
        alignItems: 'center',
        gap: 8,
      }}>
        <span style={{
          background: '#4a9eff',
          color: '#000',
          fontSize: 10,
          fontWeight: 700,
          padding: '2px 6px',
          borderRadius: 3,
          textTransform: 'uppercase',
          letterSpacing: '0.5px',
          flexShrink: 0,
        }}>
          START
        </span>
        <span style={{
          color: '#ccc',
          fontSize: 12,
          fontWeight: 600,
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
        }}>
          {scriptName}
        </span>
      </div>

      {/* Badge area */}
      {activeBadges.length > 0 && (
        <div style={{ padding: '6px 10px', display: 'flex', gap: 4, flexWrap: 'wrap' }}>
          {activeBadges.map((badge) => (
            <span key={badge} style={{
              background: 'rgba(74,158,255,0.1)',
              border: '1px solid rgba(74,158,255,0.25)',
              borderRadius: 3,
              padding: '1px 5px',
              fontSize: 9,
              color: '#8aafdb',
            }}>
              {badge}
            </span>
          ))}
        </div>
      )}

      {/* Source handle only (bottom) — no target handle */}
      <Handle
        type="source"
        position={Position.Bottom}
        style={{ background: '#4a9eff', width: 8, height: 8, border: 'none' }}
      />
    </div>
  );
}

export default memo(StartNode);
```

- [ ] **Step 2: Verify the file compiles**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: No errors related to `StartNode.tsx`

- [ ] **Step 3: Commit**

```bash
git add FlowCanvas/src/nodes/StartNode.tsx
git commit -m "feat: add StartNode component for flow canvas"
```

---

### Task 2: Register StartNode in App.tsx

**Files:**
- Modify: `FlowCanvas/src/App.tsx:18-39` (imports and nodeTypes)

- [ ] **Step 1: Add StartNode import and register in nodeTypes**

Add import after line 19 (`import CommentNode ...`):
```tsx
import StartNode from './nodes/StartNode';
```

Update nodeTypes (line 36-39):
```tsx
const nodeTypes = {
  block: BaseBlock,
  comment: CommentNode,
  start: StartNode,
};
```

- [ ] **Step 2: Update MiniMap nodeColor to handle Start node**

In the `nodeColor` callback (around line 301-304), the `_start` blockType won't be in `blockDefMap`, so `def` will be null. The current fallback `'#4a9eff'` already returns blue, which is correct. No change needed — verify this.

- [ ] **Step 3: Verify the app compiles**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/App.tsx
git commit -m "feat: register StartNode type in flow canvas"
```

---

### Task 3: Add Start Node Delete Protection in graphSlice

**Files:**
- Modify: `FlowCanvas/src/stores/slices/graphSlice.ts:66-78,105-115`

- [ ] **Step 1: Define the Start node ID constant**

Add at the top of the file, after the imports:
```ts
/** Reserved node ID for the mandatory Start block. */
export const START_NODE_ID = '__start__';
```

- [ ] **Step 2: Guard `onNodesChange` against Start node removal**

In `onNodesChange` (line 66), filter out `remove` changes for `__start__` before passing to `applyNodeChanges`:

```ts
  onNodesChange: (changes) => {
    // Protect the Start node from deletion via React Flow internals
    const filtered = changes.filter(
      (c) => c.type !== 'remove' || c.id !== START_NODE_ID,
    );
    set((state) => {
      const nextNodes = applyNodeChanges(filtered, state.nodes);
      const hasSelectionChange = filtered.some((c) => c.type === 'select');
      const hasGraphMutation = filtered.some((c) => c.type !== 'select');
      return {
        nodes: nextNodes,
        selectedNodeIds: hasSelectionChange
          ? new Set(nextNodes.filter((n) => !!n.selected).map((n) => n.id))
          : state.selectedNodeIds,
        ...(hasGraphMutation ? clearedExportStatusState() : {}),
      };
    });
  },
```

- [ ] **Step 3: Guard `removeNodes` against Start node removal**

In `removeNodes` (line 105), filter `__start__` out of the ID set:

```ts
  removeNodes: (ids) => {
    const filtered = ids.filter((id) => id !== START_NODE_ID);
    if (filtered.length === 0) return;
    get().pushSnapshot('Delete blocks');
    const idSet = new Set(filtered);
    set((state) => ({
      nodes: state.nodes.filter((n) => !idSet.has(n.id)),
      edges: state.edges.filter((e) => !idSet.has(e.source) && !idSet.has(e.target)),
      selectedNodeIds: new Set([...state.selectedNodeIds].filter((id) => !idSet.has(id))),
      isDirty: true,
      ...clearedExportStatusState(),
    }));
  },
```

- [ ] **Step 4: Verify the app compiles**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 5: Commit**

```bash
git add FlowCanvas/src/stores/slices/graphSlice.ts
git commit -m "feat: protect Start node from deletion in graph store"
```

---

### Task 4: Guard Copy/Paste to Exclude Start Node

**Files:**
- Modify: `FlowCanvas/src/utils/clipboard.ts:14-29`

- [ ] **Step 1: Import the Start node constant**

Add at top:
```ts
import { START_NODE_ID } from '../stores/slices/graphSlice';
```

- [ ] **Step 2: Filter Start node from `copyNodes`**

In `copyNodes` (line 19), filter out the Start node:

```ts
  const selectedNodes = nodes.filter(
    (n) => selectedIds.has(n.id) && n.id !== START_NODE_ID,
  );
```

- [ ] **Step 3: Verify the app compiles**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/utils/clipboard.ts
git commit -m "feat: exclude Start node from copy/paste operations"
```

---

### Task 5: Guard Context Menu for Start Node

**Files:**
- Modify: `FlowCanvas/src/panels/BlockContextMenu.tsx:60-103`

- [ ] **Step 1: Import the Start node constant**

Add at top:
```ts
import { START_NODE_ID } from '../stores/slices/graphSlice';
```

- [ ] **Step 2: Conditionally hide destructive menu items for Start node**

After line 60 (`const { x, y, nodeId } = contextMenu;`), add:
```ts
  const isStartNode = nodeId === START_NODE_ID;
```

Then filter the `menuItems` array to exclude "Delete Block", "Toggle Breakpoint", and "Disable Block" for the Start node. Replace the menuItems definition (lines 69-103):

```ts
  const menuItems: MenuEntry[] = [
    ...(!isStartNode ? [
      {
        label: 'Toggle Breakpoint',
        icon: '\uD83D\uDD34',
        action: () => {
          toggleBreakpoint(nodeId);
          hideContextMenu();
        },
      } as MenuItem,
      {
        label: disabled ? 'Enable Block' : 'Disable Block',
        icon: disabled ? '\u25B6' : '\u23ED',
        action: () => {
          toggleDisabled(nodeId);
          hideContextMenu();
        },
      } as MenuItem,
    ] : []),
    {
      label: 'Add Comment',
      icon: '\uD83D\uDCDD',
      action: () => {
        addComment(commentPos, nodeId);
        hideContextMenu();
      },
    },
    ...(!isStartNode ? [
      { separator: true } as Separator,
      {
        label: 'Delete Block',
        icon: '\uD83D\uDDD1',
        action: () => {
          removeNodes([nodeId]);
          hideContextMenu();
        },
      } as MenuItem,
    ] : []),
  ];
```

- [ ] **Step 3: Verify the app compiles**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/panels/BlockContextMenu.tsx
git commit -m "feat: hide delete/breakpoint/disable options for Start node context menu"
```

---

### Task 6: Add Start Properties Form to Properties Panel

**Files:**
- Modify: `FlowCanvas/src/panels/Properties.tsx:382-660`

- [ ] **Step 1: Create the StartProperties sub-component**

Add this component above the `export default function Properties()` function (before line 382). It reuses the existing `PropertyField` component and `useBufferedInput` hook from the same file:

```tsx
const START_BOOL_FIELDS: { key: string; label: string }[] = [
  { key: 'debug', label: 'Debug Mode' },
  { key: 'nobanner', label: 'No Banner' },
  { key: 'suppress_missing_column_warning', label: 'Suppress Missing Column Warning' },
  { key: 'library', label: 'Library (non-executable)' },
];

function StartProperties({
  nodeId,
  data,
  onPropChange,
  onLabelChange,
}: {
  nodeId: string;
  data: { label?: string; props?: Record<string, unknown> };
  onPropChange: (key: string, value: unknown) => void;
  onLabelChange: (label: string) => void;
}) {
  const props = data.props ?? {};

  const nameInput = useBufferedInput(
    String(props.name ?? ''),
    `${nodeId}:start-name`,
    (val) => {
      onPropChange('name', val || undefined);
      onLabelChange(val || 'Untitled Script');
    },
  );

  const descInput = useBufferedInput(
    String(props.description ?? ''),
    `${nodeId}:start-desc`,
    (val) => onPropChange('description', val || undefined),
  );

  const envInput = useBufferedInput(
    String(props.environment ?? ''),
    `${nodeId}:start-env`,
    (val) => onPropChange('environment', val || undefined),
  );

  const versionInput = useBufferedInput(
    String(props.version ?? ''),
    `${nodeId}:start-version`,
    (val) => onPropChange('version', val ? Number(val) : undefined),
  );

  const colors = { text: '#8aafdb', border: '#4a9eff', bg: '#0d1a2a' };

  const inputStyle: React.CSSProperties = {
    width: '100%',
    padding: '4px 6px',
    background: 'var(--fc-input-bg, #0d1117)',
    border: `1px solid ${colors.border}44`,
    borderRadius: 4,
    color: 'var(--fc-text, #ccc)',
    fontSize: 12,
    outline: 'none',
  };

  const varsCount = props.vars ? Object.keys(props.vars as Record<string, unknown>).length : 0;
  const importsCount = Array.isArray(props.imports) ? (props.imports as unknown[]).length : 0;

  return (
    <div
      data-testid="properties-panel"
      style={{
        flex: 1,
        overflowY: 'auto',
        padding: 12,
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
      }}
    >
      {/* Header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 6,
        paddingBottom: 8,
        borderBottom: '1px solid var(--fc-panel-border, #2a2a4a)',
      }}>
        <span style={{
          background: '#4a9eff',
          color: '#000',
          fontSize: 10,
          fontWeight: 700,
          padding: '2px 6px',
          borderRadius: 3,
          textTransform: 'uppercase',
        }}>
          START
        </span>
        <span style={{ color: 'var(--fc-text, #ccc)', fontSize: 12, fontWeight: 600 }}>
          Script Settings
        </span>
      </div>

      {/* Name */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Name
        </label>
        <input
          data-testid="start-name-input"
          type="text"
          value={nameInput.value}
          placeholder="Untitled Script"
          onChange={(e) => nameInput.onChange(e.target.value)}
          onFocus={nameInput.onFocus}
          onBlur={nameInput.onBlur}
          style={inputStyle}
        />
      </div>

      {/* Description */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Description
        </label>
        <textarea
          data-testid="start-description-input"
          value={descInput.value}
          placeholder="What does this script do?"
          onChange={(e) => descInput.onChange(e.target.value)}
          onFocus={descInput.onFocus}
          onBlur={descInput.onBlur}
          rows={2}
          style={{ ...inputStyle, resize: 'vertical' }}
        />
      </div>

      {/* Environment */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Environment
        </label>
        <input
          data-testid="start-environment-input"
          type="text"
          value={envInput.value}
          placeholder="Optional environment name"
          onChange={(e) => envInput.onChange(e.target.value)}
          onFocus={envInput.onFocus}
          onBlur={envInput.onBlur}
          style={inputStyle}
        />
      </div>

      {/* Version */}
      <div>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)', display: 'block', marginBottom: 3 }}>
          Version
        </label>
        <input
          data-testid="start-version-input"
          type="number"
          value={versionInput.value}
          placeholder="1"
          onChange={(e) => versionInput.onChange(e.target.value)}
          onFocus={versionInput.onFocus}
          onBlur={versionInput.onBlur}
          style={inputStyle}
        />
      </div>

      {/* Boolean flags */}
      <div style={{
        borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
        paddingTop: 10,
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
      }}>
        <label style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)' }}>Flags</label>
        {START_BOOL_FIELDS.map((field) => (
          <label
            key={field.key}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              fontSize: 12,
              color: 'var(--fc-text-secondary, #aaa)',
              cursor: 'pointer',
            }}
          >
            <input
              data-testid={`start-${field.key}-input`}
              type="checkbox"
              checked={!!props[field.key]}
              onChange={(e) => onPropChange(field.key, e.target.checked)}
              style={{ accentColor: '#4a9eff' }}
            />
            {field.label}
          </label>
        ))}
      </div>

      {/* Read-only summaries for vars and imports */}
      {(varsCount > 0 || importsCount > 0) && (
        <div style={{
          borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
          paddingTop: 10,
          display: 'flex',
          flexDirection: 'column',
          gap: 4,
        }}>
          {varsCount > 0 && (
            <div style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)' }}>
              {varsCount} variable{varsCount !== 1 ? 's' : ''} defined
            </div>
          )}
          {importsCount > 0 && (
            <div style={{ fontSize: 11, color: 'var(--fc-text-muted, #666)' }}>
              {importsCount} import{importsCount !== 1 ? 's' : ''}
            </div>
          )}
        </div>
      )}

      {/* Footer */}
      <div style={{
        marginTop: 'auto',
        paddingTop: 12,
        borderTop: '1px solid var(--fc-panel-border, #2a2a4a)',
        fontSize: 11,
        color: 'var(--fc-text-muted, #555)',
        lineHeight: 1.5,
      }}>
        Script-level settings that control execution behavior. These appear in the YAML preamble above the steps.
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Add the early return for Start node in the Properties component**

In the `Properties` component, **before** the existing `if (!node || !def || !blockData || !selectedNodeId)` guard (line 456), add:

```tsx
  // Start node: render custom script-settings form (before def check,
  // since _start is not in the block registry and def will be null)
  if (selectedNodeId && node && blockData?.blockType === '_start') {
    return (
      <StartProperties
        nodeId={selectedNodeId}
        data={blockData}
        onPropChange={updateProp}
        onLabelChange={updateLabel}
      />
    );
  }
```

- [ ] **Step 3: Verify the app compiles**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/panels/Properties.tsx
git commit -m "feat: add Start Block properties form for script settings"
```

---

### Task 7: Update C# Bridge — Import (TextToGraph)

**Files:**
- Modify: `Services/FlowCanvasBridge.cs:377-393` (preamble node creation)

- [ ] **Step 1: Replace hidden preamble node with visible Start node**

Replace the current preamble node creation block (lines 377-393) with code that:
1. Creates a `start` type node with id `__start__` at position (250, 40)
2. Parses individual preamble fields into `props`
3. Stores any unrecognized preamble content in `_yamlSnippet`
4. Creates an edge from `__start__` to the first step node

Replace:
```csharp
            // Store preamble in a special metadata node (hidden, used for export)
            if (!string.IsNullOrWhiteSpace(preamble))
            {
                var metaNode = new JObject
                {
                    ["id"] = "__preamble__",
                    ["type"] = "block",
                    ["position"] = new JObject { ["x"] = -9999, ["y"] = -9999 },
                    ["hidden"] = true,
                    ["data"] = new JObject
                    {
                        ["blockType"] = "_preamble",
                        ["props"] = new JObject { ["_yamlSnippet"] = preamble },
                    },
                };
                nodes.Add(metaNode);
            }
```

With:
```csharp
            // Create the Start node (always present, visible)
            var startProps = new JObject();
            if (!string.IsNullOrWhiteSpace(preamble))
            {
                ParsePreambleIntoProps(preamble, script, startProps);
            }

            var startNode = new JObject
            {
                ["id"] = "__start__",
                ["type"] = "start",
                ["position"] = new JObject { ["x"] = NodeStartX, ["y"] = 0 },
                ["data"] = new JObject
                {
                    ["blockType"] = "_start",
                    ["label"] = script.Name ?? "Untitled Script",
                    ["props"] = startProps,
                },
            };
            nodes.Add(startNode);

            // Connect Start to the first step node (if any steps exist)
            string? firstStepId = null;
            for (int n = 0; n < nodes.Count; n++)
            {
                var nid = nodes[n]["id"]?.ToString();
                if (nid != null && nid != "__start__")
                {
                    firstStepId = nid;
                    break;
                }
            }
            if (firstStepId != null)
            {
                edges.Add(new JObject
                {
                    ["id"] = $"edge-start-{firstStepId}",
                    ["source"] = "__start__",
                    ["target"] = firstStepId,
                    ["style"] = new JObject { ["stroke"] = "#666" },
                });
            }
```

Also adjust `currentY` initialization (line 274) to account for Start node height. Change:
```csharp
            var currentY = NodeStartY;
```
To:
```csharp
            var currentY = NodeStartY + NodeSpacingY; // leave room for Start node at Y=0
```

- [ ] **Step 2: Add the `ParsePreambleIntoProps` helper method**

Add this method to the `FlowCanvasBridge` class (near the `ExtractPreamble` method around line 2655):

```csharp
        /// <summary>
        /// Parses known preamble fields from the Script model into Start node props.
        /// Unknown preamble content is stored in _yamlSnippet for round-trip safety.
        /// </summary>
        private static void ParsePreambleIntoProps(string preamble, Script script, JObject props)
        {
            if (!string.IsNullOrEmpty(script.Name))
                props["name"] = script.Name;
            if (!string.IsNullOrEmpty(script.Description))
                props["description"] = script.Description;
            if (script.Version != 1)
                props["version"] = script.Version;
            if (!string.IsNullOrEmpty(script.Environment))
                props["environment"] = script.Environment;
            if (script.Debug)
                props["debug"] = true;
            if (script.NoBanner)
                props["nobanner"] = true;
            if (script.SuppressMissingColumnWarning)
                props["suppress_missing_column_warning"] = true;
            if (script.Library)
                props["library"] = true;

            // Store vars as JObject for read-only display
            if (script.Vars.Count > 0)
            {
                var varsObj = new JObject();
                foreach (var kv in script.Vars)
                    varsObj[kv.Key] = kv.Value != null ? JToken.FromObject(kv.Value) : JValue.CreateNull();
                props["vars"] = varsObj;
            }

            // Store imports as JArray for read-only display
            if (script.Imports.Count > 0)
            {
                var importsArr = new JArray();
                foreach (var imp in script.Imports)
                    importsArr.Add(imp.Path);
                props["imports"] = importsArr;
            }

            // Store full preamble as fallback for unrecognized keys
            props["_yamlSnippet"] = preamble;
        }
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add Services/FlowCanvasBridge.cs
git commit -m "feat: create visible Start node from preamble on import"
```

---

### Task 8: Update C# Bridge — Export (ExportGraphToYaml)

**Files:**
- Modify: `Services/FlowCanvasBridge.cs:890-934` (export root detection and preamble emission)

- [ ] **Step 1: Replace preamble export and root detection logic**

Replace the current root detection + preamble block (lines 903-934) with logic that:
1. Filters `__start__` from the roots list
2. Uses `__start__`'s outgoing edge target as the single root
3. Serializes Start node props to preamble YAML (new logic)
4. Falls back to `_yamlSnippet` for unrecognized content

Replace lines 903-934. This replaces the old root detection, chain building, AND preamble assembly with a single cohesive block:

```csharp
            // --- Start node: determine root and build ordered chain ---
            string? startTarget = null;
            if (outgoing.TryGetValue("__start__", out var startTargets) && startTargets.Count > 0)
                startTarget = startTargets[0];

            // Build ordered chain from Start's outgoing target
            var orderedIds = new List<string>();
            var visited = new HashSet<string>();
            if (startTarget != null)
            {
                BuildChain(startTarget, outgoing, orderedIds, visited);
            }

            // Warn about disconnected nodes (not reachable from Start, excluded from export)
            foreach (var n in nodes)
            {
                var nid = n["id"]?.ToString();
                if (nid == null || nid == "__start__") continue;
                if (n["hidden"]?.Value<bool>() == true) continue;
                if (!visited.Contains(nid) && !hasIncoming.Contains(nid))
                {
                    result.Diagnostics.Add(new FlowCanvasExportDiagnostic(
                        ExportDiagnosticSeverity.Warning,
                        $"Node '{nid}' is not reachable from the Start block and will be excluded from the exported YAML.",
                        nid));
                }
            }
```

Then replace the preamble assembly (old lines 918-934):
```csharp
            // Build preamble from Start node props
            var sb = new StringBuilder();
            if (nodeMap.TryGetValue("__start__", out var startNode))
            {
                var startProps = startNode["data"]?["props"] as JObject;
                if (startProps != null)
                {
                    sb.Append(SerializeStartPropsToPreamble(startProps));
                }
            }

            // Ensure "steps:" header is present
            var preambleText = sb.ToString();
            if (!preambleText.Contains("steps:"))
                sb.AppendLine("steps:");
```

- [ ] **Step 2: Add the `SerializeStartPropsToPreamble` helper method**

Add near `ParsePreambleIntoProps`:

```csharp
        /// <summary>
        /// Serializes Start node props back to YAML preamble text.
        /// If a _yamlSnippet exists (from import), it is used as the base
        /// to preserve unrecognized keys, comments, and formatting.
        /// Otherwise, builds preamble from individual known fields.
        /// </summary>
        private static string SerializeStartPropsToPreamble(JObject props)
        {
            // If we have the original snippet, use it as-is for maximum fidelity.
            // The individual props were parsed from this snippet on import;
            // any edits the user made in the Properties panel update the props
            // but we need to reflect those changes in the YAML.
            //
            // Strategy: rebuild from props for clean output when possible.
            var sb = new StringBuilder();

            var name = props["name"]?.ToString();
            var description = props["description"]?.ToString();
            var version = props["version"]?.Value<int?>() ?? 0;
            var environment = props["environment"]?.ToString();
            var debug = props["debug"]?.Value<bool>() == true;
            var nobanner = props["nobanner"]?.Value<bool>() == true;
            var suppressWarning = props["suppress_missing_column_warning"]?.Value<bool>() == true;
            var library = props["library"]?.Value<bool>() == true;
            var vars = props["vars"] as JObject;
            var imports = props["imports"] as JArray;
            var snippet = props["_yamlSnippet"]?.ToString();

            // Check if user has made any edits (props differ from what was parsed).
            // For simplicity, always rebuild from props — this ensures edits are reflected.
            if (!string.IsNullOrEmpty(name))
                sb.AppendLine($"name: {name}");
            if (!string.IsNullOrEmpty(description))
                sb.AppendLine($"description: {EscapeYamlString(description)}");
            if (version > 1)
                sb.AppendLine($"version: {version}");
            if (!string.IsNullOrEmpty(environment))
                sb.AppendLine($"environment: {environment}");
            if (debug)
                sb.AppendLine("debug: true");
            if (nobanner)
                sb.AppendLine("nobanner: true");
            if (suppressWarning)
                sb.AppendLine("suppress_missing_column_warning: true");
            if (library)
                sb.AppendLine("library: true");

            // Vars and imports: use snippet section if available (edits not supported yet)
            if (vars != null && vars.Count > 0 && snippet != null)
            {
                var varsSection = ExtractYamlSection(snippet, "vars:");
                if (!string.IsNullOrEmpty(varsSection))
                    sb.Append(varsSection);
            }
            if (imports != null && imports.Count > 0 && snippet != null)
            {
                var importsSection = ExtractYamlSection(snippet, "imports:");
                if (!string.IsNullOrEmpty(importsSection))
                    sb.Append(importsSection);
            }

            // Append any unrecognized sections from the original snippet
            if (snippet != null)
            {
                var unrecognized = ExtractUnrecognizedSections(snippet);
                if (!string.IsNullOrEmpty(unrecognized))
                    sb.Append(unrecognized);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Escapes a YAML string value, wrapping in quotes if it contains special characters.
        /// </summary>
        private static string EscapeYamlString(string value)
        {
            if (value.Contains('\n') || value.Contains(':') || value.Contains('#') ||
                value.StartsWith(' ') || value.EndsWith(' '))
                return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
            return value;
        }

        /// <summary>
        /// Extracts a named section (e.g. "vars:") from YAML text, including all indented lines.
        /// </summary>
        private static string ExtractYamlSection(string yaml, string sectionKey)
        {
            var lines = yaml.Split('\n');
            var sb = new StringBuilder();
            bool inSection = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r');
                if (trimmed.TrimStart() == sectionKey || trimmed.TrimStart().StartsWith(sectionKey))
                {
                    inSection = true;
                    sb.AppendLine(trimmed);
                    continue;
                }
                if (inSection)
                {
                    if (trimmed.Length > 0 && (trimmed[0] == ' ' || trimmed[0] == '\t' || trimmed.TrimStart().StartsWith("-")))
                    {
                        sb.AppendLine(trimmed);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Finds preamble sections that are not recognized known keys.
        /// </summary>
        private static string ExtractUnrecognizedSections(string snippet)
        {
            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name:", "description:", "version:", "environment:",
                "debug:", "nobanner:", "suppress_missing_column_warning:", "library:",
                "vars:", "imports:", "subroutines:", "steps:",
            };

            var lines = snippet.Split('\n');
            var sb = new StringBuilder();
            bool inUnrecognized = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimEnd('\r').TrimStart();
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                {
                    if (inUnrecognized)
                        sb.AppendLine(lines[i].TrimEnd('\r'));
                    continue;
                }

                // Check if this line starts a known section
                bool isKnown = false;
                foreach (var key in knownKeys)
                {
                    if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                    {
                        isKnown = true;
                        break;
                    }
                }

                if (!isKnown && !trimmed.StartsWith("-") && (trimmed.Length > 0 && trimmed[0] != ' ' && trimmed[0] != '\t'))
                {
                    inUnrecognized = true;
                    sb.AppendLine(lines[i].TrimEnd('\r'));
                }
                else if (inUnrecognized && (trimmed.StartsWith(" ") || trimmed.StartsWith("\t") || trimmed.StartsWith("-")))
                {
                    sb.AppendLine(lines[i].TrimEnd('\r'));
                }
                else
                {
                    inUnrecognized = false;
                }
            }

            return sb.ToString();
        }
```

- [ ] **Step 3: Update the orderedIds loop to skip `__start__`**

In the export loop that emits YAML for each node (around line 938), ensure `__start__` is skipped. The existing `if (node["hidden"]?.Value<bool>() == true) continue;` won't catch it since Start is visible. Add a check:

After `if (!nodeMap.TryGetValue(nodeId, out var node)) continue;` and `if (node["hidden"]?.Value<bool>() == true) continue;`, add:
```csharp
                if (nodeId == "__start__") continue;
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeds

- [ ] **Step 5: Commit**

```bash
git add Services/FlowCanvasBridge.cs
git commit -m "feat: export Start node props as YAML preamble"
```

---

### Task 9: Handle Empty Canvas and Migration

**Files:**
- Modify: `FlowCanvas/src/stores/messageBridge.ts:16-26` (load-graph handler)

- [ ] **Step 1: Ensure Start node exists after graph load**

In the `load-graph` handler in `messageBridge.ts`, after setting nodes and edges, check if a `__start__` node exists. If not (legacy graph or empty canvas), create one:

After `store.getState().setEdges(msg.edges as Edge[]);` (line 19), add:

```ts
        // Ensure Start node always exists
        const loadedNodes = store.getState().nodes;
        const loadedEdges = store.getState().edges;
        const hasStart = loadedNodes.some((n) => n.id === '__start__');
        if (!hasStart) {
          // Migrate: check for old __preamble__ node
          const preambleNode = loadedNodes.find(
            (n) => (n.data as any)?.blockType === '_preamble',
          );
          const startNode: Node = {
            id: '__start__',
            type: 'start',
            position: { x: 250, y: 0 },
            data: {
              blockType: '_start',
              label: 'Untitled Script',
              props: preambleNode
                ? (preambleNode.data as any)?.props ?? {}
                : {},
            },
          };
          // Remove old preamble node if present, add Start node
          const filtered = loadedNodes.filter(
            (n) => (n.data as any)?.blockType !== '_preamble',
          );
          store.getState().setNodes([startNode, ...filtered]);

          // Create edge from Start to the first root node (no incoming edges)
          const incomingTargets = new Set(loadedEdges.map((e) => e.target));
          const firstRoot = filtered.find((n) => !incomingTargets.has(n.id));
          if (firstRoot) {
            store.getState().setEdges([
              ...loadedEdges,
              {
                id: `edge-start-${firstRoot.id}`,
                source: '__start__',
                target: firstRoot.id,
                style: { stroke: '#666' },
              } as Edge,
            ]);
          }
        }
```

- [ ] **Step 2: Add Node import at top of file**

Ensure the `Node` import at the top of `messageBridge.ts` (line 7) is present (it already is).

- [ ] **Step 3: Build frontend**

Run: `cd FlowCanvas && npx tsc --noEmit`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add FlowCanvas/src/stores/messageBridge.ts
git commit -m "feat: ensure Start node exists on graph load with preamble migration"
```

---

### Task 10: Build and Manual Verification

**Files:** None (verification only)

- [ ] **Step 1: Build the full solution**

Run: `dotnet build SSH_Helper.sln`
Expected: Build succeeds with no errors

- [ ] **Step 2: Build the React frontend**

Run: `cd FlowCanvas && npm run build`
Expected: Build succeeds

- [ ] **Step 3: Commit all remaining changes**

```bash
git add -A
git commit -m "feat: complete Start Block implementation for flow canvas"
```
