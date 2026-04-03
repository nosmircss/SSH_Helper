## Context
We need high-confidence parity between YAML authoring and Flow Canvas authoring, with proof driven by QA preset coverage. Existing Flow Canvas export and Start-node authoring contracts do not yet cover all structures required by QA presets.

## Goals
- Rebuild all valid QA presets through front-end graph construction paths.
- Export valid YAML that is semantically equivalent to source presets.
- Cover the one missing catalog block (`browser_callback`) through synthetic parity.
- Preserve intentional-invalid QA behavior through explicit negative tests.

## Non-Goals
- CI integration changes in this phase.
- Requiring exact YAML text formatting equality.

## Decisions
- Use semantic parity (parsed canonical model) rather than text-equality parity.
- Use hybrid creation strategy:
  - bulk front-end builder hooks for full matrix scale,
  - real gesture smoke tests for UI-path confidence.
- Add Start advanced-section editors (`vars`, `imports`, `subroutines`) for parity-critical authoring.
- Add graph-native branch metadata and export support for `try`, `switch`, `parallel`, and `elif`.

## Risks / Trade-offs
- Branch metadata complexity can regress edge-edit UX; mitigate with focused gesture smoke tests and export diagnostics.
- Semantic comparator drift could hide real regressions; mitigate with explicit canonicalization rules and comparator tests.
- Large matrix runtime may be heavy; keep manual-run scope in this phase.

## Migration Plan
1. Implement branch metadata + export support and Start advanced-section authoring.
2. Add parity validator/comparator utilities.
3. Add bulk parity matrix and negative suite.
4. Add manual orchestration command and docs.
