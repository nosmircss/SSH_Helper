# Flow Canvas — Visual Script Builder for SSH Helper

## Context

SSH Helper has a powerful YAML scripting engine with 35 commands, 55+ built-in functions, control flow, subroutines, and a debugger infrastructure (`DebugState` with breakpoints and boolean step/continue flags polled in a 100ms loop). However, the scripting experience is entirely text-based. Users must learn YAML syntax and mentally visualize execution flow.

**Flow Canvas** adds a visual script builder that transforms the scripting experience:
- **Build scripts visually** by dragging blocks and connecting them — no YAML syntax required
- **Debug visually** with breakpoints, step-through, and live variable inspection on a flow graph
- **Interactive build mode** — execute steps on real hosts, see output, and use it to configure the next step
- **Bidirectional** — open existing YAML as a graph, edit visually, apply back to YAML

This is the "wow factor" feature — no free SSH tool offers anything like this.

---

## Architecture

### Technology Stack
- **WinForms**: `FlowCanvasForm` (separate modeless window, like InteractiveTerminalForm)
- **WebView2**: Hosts the visual editor (WebView2 is already a project dependency)
- **React Flow**: Node graph library (37k+ GitHub stars, MIT license, used by Stripe)
- **Vite + React + TypeScript**: Build toolchain for the web app
- **dagre/elkjs**: Auto-layout for YAML → graph import

### Component Overview
```
FlowCanvasForm.cs (WinForms)          ← New file
└── WebView2 Control
    └── React Flow App (bundled)       ← New directory: FlowCanvas/
         ├── Block Palette (sidebar)
         ├── Canvas (React Flow)
         │    ├── Custom Node Components
         │    ├── Animated Edges
         │    ├── Minimap + Controls
         │    └── Context Menus
         ├── Properties Panel
         ├── Variable Inspector
         └── Debug Panel

FlowCanvasBridge.cs (Service)          ← New file
├── YAML → Graph JSON conversion
├── Graph JSON → YAML conversion
├── Execution state → WebView2 events
└── Debug events ↔ DebugState
```

### Communication Protocol (PostWebMessage JSON)

**WinForms → WebView2:**
- `{ type: "load-graph", nodes: [...], edges: [...] }` — import YAML as graph
- `{ type: "execution-update", stepId, state, variables }` — animate execution
- `{ type: "debug-paused", stepId, variables, callStack }` — breakpoint hit
- `{ type: "test-step-result", stepId, output, variables }` — interactive build output

**WebView2 → WinForms:**
- `{ type: "apply-yaml", graphData }` — convert graph back to YAML
- `{ type: "run-request" }` — trigger execution from canvas
- `{ type: "debug-action", action: "continue"|"step"|"stop" }` — debugger controls
- `{ type: "test-step", stepId }` — interactive build: execute single step
- `{ type: "breakpoint-toggle", stepId }` — add/remove breakpoint

**Handshake & Error Handling:**
- `{ type: "ready" }` — React app sends on mount; WinForms queues messages until received
- `{ type: "error", code, message }` — either side can report errors
- Messages are JSON-validated on receipt; malformed messages logged and discarded
- WinForms queues outbound messages until `ready` is received (handles WebView2 init timing)

### User Flow
1. **Open**: Button in script header or Edit menu → FlowCanvasForm opens → loads current YAML
2. **Build**: Drag blocks from palette → configure in properties panel → connect with edges
3. **Test**: Right-click any block → "Test Step" → executes on real host → output appears inline
4. **Debug**: Set breakpoints → Run → canvas animates through blocks → pause/step/inspect
5. **Apply**: "Apply to YAML" → graph converts to YAML → script editor updates

---

## Block System

### Categories (7 color-coded groups)

| Category | Color | Commands |
|----------|-------|----------|
| SSH | Blue (#4a9eff) | send, interactive, sftp |
| Control Flow | Amber (#f0c040) | if, while, foreach, switch, parallel, try, break, continue, call, return, exit |
| Data | Purple (#9b59b6) | extract, set, parse, table, assert |
| Network | Teal (#1abc9c) | ping, dns, portcheck, http, webhook, browser_callback |
| I/O & UI | Orange (#e67e22) | print, input, choose, multiselect, confirm, readfile, writefile, log |
| Grid Updates | Steel (#3498db) | updatecolumn, updateenvironment |
| Timing | Gray (#95a5a6) | wait |

### Block Anatomy
Every block has:
- **Flow-in port** (top) — connection from previous step
- **Flow-out port(s)** (bottom) — connection to next step(s)
- **Category badge** — color-coded type label
- **Label** — user-editable display name
- **Preview** — compact view of key parameters
- **Breakpoint gutter** — click to toggle breakpoint
- **Execution state** — idle / running (glow) / success (green) / error (red)
- **on_error badge** — shows error handling mode if set

### Control Flow as Container Nodes (React Flow Group Nodes)
- **IF/ELSE**: Expands into TRUE/FALSE lanes with nested blocks inside
- **FOREACH/WHILE**: Expands to show loop body, collapse to show step count
- **SWITCH**: Expands into N case lanes + default
- **PARALLEL**: Shows branches side-by-side
- **TRY**: Expands into try/catch/finally sections
- All containers are collapsible — collapsed shows summary + step count

### Properties Panel
When a block is selected, a side panel shows all configurable properties for that command type. Property schemas are derived from ScriptParser's YAML key definitions. Example:
- SEND: command text, timeout, on_error, expect patterns, delay
- EXTRACT: pattern (regex), source, variable name, match mode
- IF: condition expression, optional name

---

## YAML ↔ Graph Conversion

### YAML → Graph (Import)
1. `ScriptParser.Parse(yaml)` — existing C# parser → `Script` model
2. `FlowCanvasBridge.ToGraph(script)` — new C#, walks step tree, generates `{ nodes[], edges[] }` JSON
3. `PostWebMessage({ type: "load-graph", nodes, edges })` → WebView2
4. React Flow auto-layout via dagre/elkjs positions nodes in clean top-to-bottom flow

### Graph → YAML (Export) — C# only
1. React app serializes graph to JSON (nodes with properties + edges)
2. `PostWebMessage({ type: "apply-yaml", graphData })` → WinForms
3. `FlowCanvasBridge.ToYaml(graphData)` — **C# only** (has access to YamlDotNet and the Script model). No JS-side YAML generation — single source of truth avoids implementation drift.
4. YAML sent to script editor text

### Round-trip Guarantee
YAML → Graph → YAML produces **semantically equivalent** YAML. The bridge preserves: step ordering, nesting depth, all property keys, and default values. Comments and whitespace formatting will NOT survive the round-trip because `ScriptParser` uses YamlDotNet which discards them. Users opening existing scripts should be warned that formatting/comments will be normalized on "Apply to YAML". A future enhancement could add a comment-preserving parser, but v1 focuses on semantic correctness.

---

## Interactive Build Mode

The transformative feature — build scripts by interacting with real devices:

### Test Step
- Right-click any block → "Test Step" (or Ctrl+Enter)
- Executes that block and all prerequisite blocks above it on a single host
- Output appears inline on the block
- Variables carry forward for downstream blocks

### Live Pattern Tester
- EXTRACT blocks show regex matches against actual output from the previous SEND
- Matches update in real-time as the user types the pattern
- Capture groups highlighted with colors
- Match count and validation feedback

### Condition Preview
- IF/WHILE/SWITCH blocks show which branch would be taken
- Based on current variable values from executed steps
- Taken branch glows, others dim

### Variable Trail
- Variable Inspector shows timeline of how each variable changed
- Click any variable to see which block set it, before/after values

### Smart Suggestions
- After executing SEND, analyze output
- Tabular output → suggest EXTRACT with pre-built pattern
- Error in output → suggest TRY/CATCH wrapper
- Contextual "Add next step" recommendations

### Re-execute from Here
- Change a block mid-flow → right-click → "Re-execute from here"
- Runs from that point using cached variables from blocks above
- Faster iteration without re-running the entire script

### Connection Management for Interactive Build
- **Host selection**: When opening Flow Canvas, prompt for a "test host" (dropdown of hosts from the grid). This host is used for all Test Step executions.
- **Persistent session**: A single SSH connection is opened on the first Test Step and kept alive for the session. Variables and shell state persist across steps.
- **Session indicator**: Canvas toolbar shows "Connected to 10.1.1.5" with a disconnect button.
- **Cleanup**: Session is closed when the canvas window closes, or when the user disconnects manually.
- **Error recovery**: If the session drops mid-test, show an error on the block and offer "Reconnect + Retry".
- Uses `SshExecutionService` with connection pooling for session management.

---

## Block Identity Mapping

Each script step needs a stable identity that works across YAML ↔ graph conversions:
- **Graph nodes**: Use UUIDs generated by React Flow
- **YAML steps**: Identified by sequential index within their parent scope (e.g., `steps[2]`, `if.then[0]`)
- **Mapping**: `FlowCanvasBridge` maintains a bidirectional `Dictionary<string, int[]>` mapping node UUIDs ↔ step paths (array of indices representing nesting)
- **For debug**: When DebugState pauses at a step index, the bridge translates to the corresponding node UUID and sends to canvas
- **For visual-only scripts** (no YAML yet): Node UUIDs are the primary identity; step indices are assigned during graph → YAML export

---

## Full Debugger Integration

### Prerequisites (must be built first)
- **Step-level events on ScriptExecutor**: Currently `ScriptExecutor` has no `StepStarting`/`StepCompleted` events. These must be added so the canvas can animate execution. This is a small change — fire events before/after each `IScriptCommand` dispatch, passing the step index and step type.
- **DebugState upgrade**: Currently uses boolean flags (`StepRequested`, `ContinueRequested`) polled in a 100ms `Task.Delay` loop in `HandleDebugPauseAsync`. For responsive visual debugging, upgrade to `SemaphoreSlim` or `ManualResetEventSlim` for instant signal delivery.
- Both changes are backward-compatible — existing debug mode and script execution are unaffected.

### Existing Infrastructure (reused after prerequisites)
- `DebugState` (`Services/Scripting/Models/DebugState.cs`) — breakpoints, step/continue flags (upgraded to async signals)
- `ScriptExecutor` (`Services/Scripting/ScriptExecutor.cs`) — step dispatch (with new step-level events)
- `ScriptContext.Variables` — variable state
- `SshExecutionService.ExecuteScriptAsync()` — execution entry point

### New: Visual Debug Frontend
- Click block gutter → toggle breakpoint (red dot)
- Conditional breakpoints via right-click → set condition
- Step Over — advance one step, skip container contents
- Step Into — enter container blocks (if, while, etc.)
- Continue — run until next breakpoint
- Stop — cancel execution
- Variable inspector — live values, expandable objects/arrays
- Call stack — shows current nesting path through containers
- Execution trace — blocks glow blue while running, green on success, red on error
- Error tooltip — failed block shows error message on hover

### Debug Message Flow
```
User sets breakpoint → Canvas → PostWebMessage → FlowCanvasBridge → DebugState.Breakpoints.Add(stepIndex)
User clicks Run     → Canvas → PostWebMessage → FlowCanvasBridge → SshExecutionService.ExecuteScriptAsync()
Step begins         → ScriptExecutor.StepStarting event → FlowCanvasBridge → PostWebMessage → Canvas highlights block
Breakpoint hit      → DebugState pauses (SemaphoreSlim.Wait) → FlowCanvasBridge → PostWebMessage → Canvas shows debug state
User clicks Step    → Canvas → PostWebMessage → FlowCanvasBridge → DebugState.StepSignal.Release()
```

---

## Build & Deployment

### React Project Structure
```
FlowCanvas/                          ← New directory at repo root
├── package.json
├── vite.config.ts
├── tsconfig.json
├── src/
│   ├── App.tsx                      ← Main canvas layout
│   ├── MessageBus.ts                ← WebView2 ↔ React communication
│   ├── stores/                      ← State management (Zustand or React context)
│   │   ├── graphStore.ts            ← Nodes, edges, selection
│   │   ├── debugStore.ts            ← Breakpoints, execution state
│   │   └── buildStore.ts            ← Interactive build mode state
│   ├── nodes/                       ← Custom React Flow node components
│   │   ├── BaseBlock.tsx            ← Shared block chrome (ports, badge, gutter)
│   │   ├── SshBlock.tsx             ← send, interactive, sftp
│   │   ├── ControlFlowBlock.tsx     ← if, while, foreach, switch, parallel, try
│   │   ├── DataBlock.tsx            ← extract, set, parse, table, assert
│   │   ├── NetworkBlock.tsx         ← ping, dns, portcheck, http, webhook
│   │   ├── IoBlock.tsx              ← print, input, choose, readfile, writefile, log
│   │   └── GridBlock.tsx            ← updatecolumn, updateenvironment
│   ├── edges/
│   │   └── AnimatedEdge.tsx         ← Custom edge with execution animation
│   ├── panels/
│   │   ├── Palette.tsx              ← Block palette sidebar
│   │   ├── Properties.tsx           ← Selected block property editor
│   │   ├── VariableInspector.tsx    ← Variable state panel
│   │   ├── DebugPanel.tsx           ← Breakpoints, call stack, controls
│   │   └── OutputPreview.tsx        ← Inline step output display
│   ├── blockDefs/
│   │   └── registry.ts             ← Block type definitions (property schemas, defaults, categories)
│   └── utils/
│       └── layout.ts                ← dagre auto-layout
└── dist/                            ← Built output (committed or generated)
```

### Bundling Strategy
- `npm run build` → Vite produces `dist/index.html` + JS/CSS assets
- Built assets copied into WinForms project as embedded resources (follows Scintilla DLL pattern)
- WebView2 loads via `SetVirtualHostNameToFolderMapping()` or `CoreWebView2.NavigateToString()`

### WinForms Files

**New files:**
- `FlowCanvasForm.cs` + `.Designer.cs` — WebView2 host window (follows `BrowserCallbackWebViewDialog` pattern)
- `Services/FlowCanvasBridge.cs` — YAML ↔ Graph conversion + event piping

**Modified files:**
- `Form1.cs` — Add "Flow Canvas" button to script header panel, menu item under Edit
- `Form1.Designer.cs` — Wire up the button/menu
- `SSH_Helper.csproj` — Add FlowCanvas embedded resources

### Existing Code Reused
- `ScriptParser` (`Services/Scripting/ScriptParser.cs`) — YAML → Script model
- `DebugState` (`Services/Scripting/Models/DebugState.cs`) — breakpoints, debug signaling
- `ScriptExecutor` (`Services/Scripting/ScriptExecutor.cs`) — step dispatch + new events
- `SshExecutionService` (`Services/SshExecutionService.cs`) — execution entry point
- `BrowserCallbackWebViewDialog` (`UI/BrowserCallbackWebViewDialog.cs`) — WebView2 hosting pattern
- `ScriptContext` (`Services/Scripting/ScriptContext.cs`) — variable state
- `SshConnectionPool` (`Services/SshConnectionPool.cs`) — for interactive build session management

---

## Implementation Phases

### Phase 0: Prerequisites (ScriptExecutor + DebugState upgrades)
- Add `StepStarting(stepIndex, stepType)` and `StepCompleted(stepIndex, success)` events to `ScriptExecutor`
- Upgrade `DebugState` from boolean flag polling to `SemaphoreSlim` for responsive debug signaling
- Both changes are backward-compatible — verify existing tests pass
- **Files**: `Services/Scripting/ScriptExecutor.cs`, `Services/Scripting/Models/DebugState.cs`

### Phase 1: Foundation
- Create `FlowCanvas/` React project with Vite + React Flow
- Build `FlowCanvasForm.cs` WebView2 host (follow BrowserCallbackWebViewDialog pattern — modeless, not modal)
- Implement PostWebMessage protocol with ready handshake and error handling (MessageBus on both sides)
- Basic canvas with zoom/pan/minimap working
- "Flow Canvas" button opens the window and loads a hardcoded test graph
- Bundling: embedded resources (follows Scintilla DLL pattern)

### Phase 2: Block System
- Define all 35 block types in `registry.ts` with property schemas (+ `return` in Control Flow)
- Build `BaseBlock.tsx` shared component (ports, badge, label, gutter, execution state)
- Build category-specific node components (7 categories)
- Start with simple single nodes — container nodes (IF, WHILE, etc.) begin as expandable single blocks, upgraded to React Flow Group Nodes iteratively
- Build `Palette.tsx` with drag-and-drop
- Build `Properties.tsx` panel for editing block configuration
- Canvas: add/delete/connect blocks, validate connections
- Undo/Redo state management (Zustand middleware)

### Phase 3: YAML Conversion
- Build `FlowCanvasBridge.ToGraph()` — Script model → node/edge JSON with block identity mapping
- Build `FlowCanvasBridge.ToYaml()` — graph JSON → YAML (C# only, uses YamlDotNet)
- Build dagre auto-layout for imported graphs
- Wire "Apply to YAML" button → script editor updates
- Add warning dialog: "Applying will normalize formatting and remove comments. Continue?"
- Test round-trip: YAML → Graph → YAML → parse both → compare Script models
- Copy/Paste of blocks (Ctrl+C/V)

### Phase 4: Interactive Build Mode
- Host selection UI in canvas toolbar (dropdown of hosts from grid)
- Persistent SSH session management (open on first Test Step, keep alive, cleanup on close)
- Session indicator ("Connected to 10.1.1.5") with disconnect/reconnect
- "Test Step" execution (single block + prerequisites on the test host)
- Inline output preview on blocks
- Live pattern tester for EXTRACT blocks (regex against real output)
- Condition preview for IF/WHILE/SWITCH (show which branch fires)
- Variable inspector with execution state
- "Re-execute from Here" functionality
- Error recovery: reconnect on session drop

### Phase 5: Full Debugger
- Wire StepStarting/StepCompleted events (from Phase 0) to canvas animation
- Breakpoint toggle on block gutter → DebugState.Breakpoints
- Execution animation (blocks glow as they execute via step events)
- Pause at breakpoint → show debug state (variables, call stack)
- Step Over / Step Into / Continue / Stop → DebugState signals
- Call stack panel
- Conditional breakpoints (right-click → set condition)
- Error highlighting with tooltips
- Validation: show ScriptParser.Validate() errors as visual indicators on blocks

### Phase 6: Polish & Integration
- Container node collapse/expand with smooth animations
- Smart suggestions after step execution
- Variable trail timeline
- Keyboard shortcuts (delete block, Ctrl+Z undo, Ctrl+Enter test step)
- Window size/position memory (like InteractiveTerminalForm — static shared state)
- Theme integration (dark/light from SSH Helper settings via PostWebMessage)
- Context menus (right-click blocks, canvas, edges)
- Lifecycle: handle Form1 closing while canvas is open (graceful shutdown)
- Edge cases: empty scripts, unsupported commands, very large scripts (100+ steps)
