# Change: Add Flow Canvas Preset Parity Process

## Why
Flow Canvas needs a repeatable parity process proving that anything users can manually author in YAML can also be authored from Flow Canvas and exported back to equivalent valid YAML.

Current gaps block this guarantee:
- Start-node advanced sections (`vars`, `imports`, `subroutines`) are not authorable.
- Graph-native export for `try`, `switch`, and `parallel` is incomplete without stored snippets.
- There is no automated matrix that rebuilds QA presets through front-end construction paths.

## What Changes
- Add graph-native branch modeling and export generation for `try`, `switch`, `parallel`, and `if` `elif` branches.
- Add Start-panel advanced editors for `vars`, `imports`, and `subroutines` and include them in YAML export.
- Add an automated parity process that:
  - reads `qa_presets.json`,
  - rebuilds each valid preset through front-end construction hooks (no preset `load-graph`),
  - exports YAML through the existing host payload path,
  - validates parser/canonical validation success and semantic equivalence.
- Add synthetic parity coverage for `browser_callback` (missing from QA catalog).
- Add a dedicated negative suite for intentional-invalid QA presets.
- Add gesture-path smoke tests (drag/connect/edit) for real UI interaction coverage.
- Keep this phase manual-run only (no CI gating changes).

## Impact
- Affected specs: `flow-canvas`
- Affected code:
  - `FlowCanvas/src/*`
  - `Services/FlowCanvasBridge.cs`
  - `FlowCanvas/e2e/*`
  - `SSH_Helper.Tests/*`
- Out of scope:
  - PR/nightly CI integration
