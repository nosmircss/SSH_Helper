## Context
`Form1` currently uses `txtCommand` (`TextBox`) as the script authoring surface. That control is wired into multiple behaviors: cursor-position reporting, context menu actions, shortcut handling (`Ctrl+S`), theming, font settings, and script validation actions.

The scripting parser/validator (`Services/Scripting/ScriptParser.cs`) is the current source of truth for recognized step keys/options and validation constraints. Inline editor DX should align with parser behavior rather than introducing a separate hard-coded grammar list in UI code.

`SettingsDialog` already persists General/Updates/Appearance preferences through `AppConfiguration`. Editor-specific behavior toggles should live alongside existing settings in persisted config rather than being hard-coded editor constants.

## Goals / Non-Goals
- Goals:
  - Replace `txtCommand` with a dedicated code-editor control while preserving existing user workflow.
  - Provide syntax highlighting, parser-aligned autocomplete, and inline diagnostics with column spans.
  - Keep UI responsive for 500+ line scripts.
  - Keep dependency version flexible at spec level.
  - Provide a dedicated `Command Editor` settings tab so operators can enable/disable and tune inline editor behaviors.
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

- Decision: Symmetric interpolation autocomplete
  - `${...}` and `{{...}}` triggers share one interpolation suggestion pipeline and the same symbol set membership.
  - Rationale: runtime substitution supports both syntaxes; symmetric suggestions reduce mental overhead and prevent UX drift.

- Decision: Validation pipeline model
  - Use trailing `400ms` debounce with one in-flight validation and cancellation of stale work.
  - Inline diagnostics only run in YAML mode (`ScriptParser.IsYamlScript` true); diagnostics are cleared for plain command mode.
  - Rationale: keeps typing responsive while still providing near-real-time feedback.

- Decision: YAML-first indentation and newline behavior
  - `Tab`/`Shift+Tab` perform space-based indent/outdent with configurable `IndentSize` (default `2`) across single and multi-line selections.
  - Smart-enter remains indentation-aware but preserves intentional blank separator lines between step items.
  - Rationale: YAML is space-indented, and readable scripts often use blank lines between steps.

- Decision: Command editor settings model and UI
  - Add `CommandEditorSettings` under `AppConfiguration` and expose it in a dedicated `Command Editor` tab in `SettingsDialog`.
  - Store feature toggles and tunable values for syntax highlighting, completion, validation, tooltips, indentation, and smart-enter behavior.
  - Apply range clamping for numeric fields (`ValidationDebounceMs`, `IndentSize`) on load/save.
  - Rationale: avoids hard-coded behavior, supports user preference variance, and keeps editor behavior deterministic/persisted.

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
- Smart-enter and indentation customization can differ from default third-party editor behavior.
  - Mitigation: implement behavior in adapter-level key handling with focused UX tests for selection and caret movement.
- Third-party editor control behavior may differ from `TextBox` for selection/indexing semantics.
  - Mitigation: adapter tests and explicit parity checks for cursor/selection/clipboard flows.
- Settings complexity can increase cognitive load if defaults are poor.
  - Mitigation: ship with opinionated defaults matching recommended YAML workflows and group settings by intent in the new tab.

## Migration Plan
1. Add editor dependency and adapter (`IScriptEditor`, `ScriptEditorControl`).
2. Add `CommandEditorSettings` to `AppConfiguration` with defaults and bounds.
3. Add `Command Editor` tab in `SettingsDialog` and wire load/save.
4. Replace `txtCommand` wiring with adapter-backed control in `Form1`.
5. Add syntax highlighting + parser-driven autocomplete services.
6. Add validation service with debounce/cancellation and diagnostics mapping.
7. Apply command editor settings to runtime editor behavior.
8. Integrate theme/font updates and context menu/shortcut parity.
9. Add tests and run large-script responsiveness verification.

## Open Questions
- None; current scope is implementation-ready with this design.
