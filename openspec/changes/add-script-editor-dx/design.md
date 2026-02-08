## Context
`Form1` currently uses `txtCommand` (`TextBox`) as the script authoring surface. That control is wired into multiple behaviors: cursor-position reporting, context menu actions, shortcut handling (`Ctrl+S`), theming, font settings, and script validation actions.

The scripting parser/validator (`Services/Scripting/ScriptParser.cs`) is the current source of truth for recognized step keys/options and validation constraints. Inline editor DX should align with parser behavior rather than introducing a separate hard-coded grammar list in UI code.

## Goals / Non-Goals
- Goals:
  - Replace `txtCommand` with a dedicated code-editor control while preserving existing user workflow.
  - Provide syntax highlighting, parser-aligned autocomplete, and inline diagnostics with column spans.
  - Keep UI responsive for 500+ line scripts.
  - Keep dependency version flexible at spec level.
- Non-Goals:
  - Introduce a full language server.
  - Re-implement parser/validator logic in editor services.
  - Change script runtime semantics.

## Decisions
- Decision: Editor abstraction and wrapper
  - Introduce `IScriptEditor` and `ScriptEditorControl` wrapper so `Form1` depends on stable editor behaviors, not third-party control APIs.
  - Rationale: minimizes migration risk and keeps replacement feasible if control choice changes later.

- Decision: Parser-driven completion schema
  - Autocomplete command and option vocabularies are sourced from parser/model contracts (for example `ScriptParser` + `ScriptStep` semantics), not hard-coded editor arrays.
  - Rationale: prevents drift between editor hints and actual parser acceptance.

- Decision: Validation pipeline model
  - Use trailing `400ms` debounce with one in-flight validation and cancellation of stale work.
  - Inline diagnostics only run in YAML mode (`ScriptParser.IsYamlScript` true); diagnostics are cleared for plain command mode.
  - Rationale: keeps typing responsive while still providing near-real-time feedback.

- Decision: Column-level diagnostics mapping
  - `EditorDiagnostic` includes `LineNumber`, `ColumnStart`, `ColumnEnd`, severity, and message.
  - Parser error/warning strings are mapped to token spans when token localization is available; otherwise use full-line span fallback.
  - Rationale: delivers column-addressable diagnostics even though parser currently emits mostly line-oriented messages.

- Decision: Variable hover source of truth
  - `{{column}}` hover preview uses selected host-grid row first, then first non-new row fallback.
  - Execution-time "active host context" is not used for editor hover previews.
  - Rationale: editor hover should reflect authoring-time UI context; execution context can be multi-host and unavailable outside a running script.

- Decision: Dependency version strategy
  - Spec does not lock a fixed package version; implementation selects any compatible release for target framework support.
  - Rationale: allows routine dependency updates without reopening the spec for version-only churn.

## Risks / Trade-offs
- Parser-driven completion extraction may require maintenance when parser internals evolve.
  - Mitigation: unit tests that assert completion schema remains aligned with parser/model-supported commands/options.
- Column mapping from line-oriented validation messages can be approximate for some diagnostics.
  - Mitigation: explicit full-line fallback with clear hover message and future parser enhancements can improve precision.
- Third-party editor control behavior may differ from `TextBox` for selection/indexing semantics.
  - Mitigation: adapter tests and explicit parity checks for cursor/selection/clipboard flows.

## Migration Plan
1. Add editor dependency and adapter (`IScriptEditor`, `ScriptEditorControl`).
2. Replace `txtCommand` wiring with adapter-backed control in `Form1`.
3. Add syntax highlighting + parser-driven autocomplete services.
4. Add validation service with debounce/cancellation and diagnostics mapping.
5. Integrate theme/font updates and context menu/shortcut parity.
6. Add tests and run large-script responsiveness verification.

## Open Questions
- None; current scope is implementation-ready with this design.
