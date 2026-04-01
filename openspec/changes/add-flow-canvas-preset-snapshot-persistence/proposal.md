# Change: Add Flow Canvas Snapshot Persistence for Preset Portability

## Why
Preset persistence currently stores command text and preset metadata, but not the full Flow Canvas visual state. Block positions and comment nodes are lost when a preset is saved, exported, imported, or shared with another user.

This creates avoidable friction:
- operators rebuild layout context manually after import,
- comments used for review context do not travel with the preset,
- canvas and editor state can diverge in ways users cannot persist intentionally.

The change is needed to make preset sharing preserve both executable behavior and authoring context.

## What Changes

### 1. Persist Flow Canvas snapshot metadata on presets
Add optional Flow Canvas snapshot metadata to `PresetInfo`:
- `schemaVersion`
- `commandHash` (hash of normalized command text)
- `nodes` (full graph snapshot, including comment nodes)
- `edges` (full graph edge metadata)

Snapshot data is stored as raw JSON object data (not compressed) for simpler diagnostics and compatibility handling.

### 2. Introduce hash-gated script-first load policy
When loading a preset into Flow Canvas:
- If snapshot exists and `commandHash` matches current preset commands, load snapshot directly.
- If snapshot is missing or hash mismatches, rebuild graph from YAML via current bridge path.

This prevents stale snapshot rehydration after direct text edits and keeps script execution semantics authoritative.

### 3. Save-flow integration for visual and executable canvas edits
Extend save behavior so Flow Canvas changes persist without extra user ceremony:
- Visual-only edits (positions/comments) mark the preset as dirty.
- `Save Preset` persists snapshot updates even when `Apply YAML` was not clicked.
- If executable graph edits exist and are unapplied, `Save Preset` auto-runs graph export to YAML before persisting.
- If commands were edited directly in the text editor and no longer match the cached snapshot hash, stale snapshot data is cleared on save.

### 4. Add host-canvas state message for snapshot persistence
Add a host-bound canvas message carrying current graph state for persistence:
- full nodes/edges snapshot payload,
- execution-relevant dirty state for save-time auto-apply decisions.

This message is separate from executable run/apply payloads, which continue filtering visual-only comment nodes for execution/export correctness.

### 5. Include snapshot metadata in all preset sharing paths
Snapshot metadata round-trips through:
- single preset encoded export/import (payload version bump),
- bulk JSON export/import.

Import remains backward-compatible for older payloads with no snapshot metadata.

## Impact
- Affected specs: `flow-canvas`
- Affected code:
  - `Models/PresetInfo.cs`
  - `Services/PresetManager.cs`
  - `Form1.cs`
  - `UI/FlowCanvasForm.cs`
  - `FlowCanvas/src/communication-message-types.ts`
  - `FlowCanvas/src/stores/messageBridge.ts`
  - FlowCanvas store/panel logic tied to dirty state and snapshot publishing
  - tests across preset persistence, Form save flow, and canvas message contracts

## Out of Scope
- changing YAML script semantics or FlowCanvasBridge execution model,
- adding CI gating changes for this proposal,
- migration tooling for external artifacts beyond in-app backward-compatible import parsing.

## Risks and Mitigations
- **Risk: Snapshot stale relative to commands**
  - Mitigation: `commandHash` gate with script-first fallback.
- **Risk: Preset size growth due to snapshot payload**
  - Mitigation: keep schema scoped to required fields; monitor exported payload size in tests.
- **Risk: Save-flow regressions from auto-apply logic**
  - Mitigation: focused tests for dirty detection, save prompts, and save outcomes across visual-only vs executable edits.

## Compatibility
- Single-preset export payload version increments for snapshot support.
- Import logic remains backward-compatible with prior versions and missing snapshot fields.
- Existing presets continue functioning unchanged; snapshot persistence is additive.
