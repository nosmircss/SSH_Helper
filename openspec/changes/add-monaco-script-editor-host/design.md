## Context
SSH Helper currently uses a custom WinForms script editor control with incremental improvements for syntax, validation, completion, and YAML ergonomics. Even after recent fixes, the editing surface still diverges from VS Code-like behavior in areas users notice immediately: caret/viewport smoothness, completion interactions, and lower-friction large-document editing.

The user request is to use "the same editor VS Code uses." Technically, VS Code is an Electron workbench application, while Monaco is the editor engine inside VS Code. Embedding the full VS Code workbench in this WinForms app is not practical for this scope; embedding Monaco is practical and directly targets the editor UX concerns.

## Goals / Non-Goals
- Goals:
  - Provide a Monaco-backed script editor in the existing WinForms application.
  - Preserve existing parser-driven scripting semantics (completion vocabulary, diagnostics, YAML-specific behavior).
  - Improve editing UX parity with VS Code expectations for scroll, caret reveal, selection, and responsiveness.
  - Keep deployment offline-capable with local assets and deterministic behavior.
  - Support safe rollback via runtime fallback to the existing native editor.
- Non-Goals:
  - Embed full VS Code workbench UI (Explorer, extensions marketplace, terminal, SCM).
  - Add language-server infrastructure.
  - Change scripting runtime semantics or parser contracts.

## Decisions
- Decision: Monaco host via WebView2
  - Introduce `MonacoScriptEditorControl` that implements `IScriptEditor` and hosts Monaco in WebView2.
  - Rationale: most direct path to VS Code-like text editing behavior in a WinForms app.

- Decision: Keep parser/validation authority in C#
  - Continue using existing C# parser/validation/completion extraction and forward diagnostics/suggestions into Monaco.
  - Rationale: avoids semantic drift and duplicate parser logic in JavaScript.

- Decision: Local asset packaging
  - Bundle Monaco host assets with the application and disable remote dependency fetches.
  - Rationale: predictable behavior in restricted environments and improved startup determinism.

- Decision: Explicit scroll and reveal contract
  - Configure Monaco and editor bridge to support:
    - scroll-past-end (last line can be near top)
    - reveal-on-enter for caret progression at file end
    - deterministic viewport/caret synchronization after programmatic edits.
  - Rationale: directly addresses user-reported UX regressions.

- Decision: Incremental feature allowlist
  - Start with the subset required by current scripting workflows (syntax colorization, completion, markers, hover, indentation/smart-enter, clipboard, save shortcuts).
  - Rationale: reduce integration risk while leaving room to enable additional Monaco capabilities later.

- Decision: Fallback strategy
  - Add config-controlled editor engine selection with automatic fallback to current native editor on WebView2/host initialization failure.
  - Rationale: protects reliability and deployment scenarios where WebView2 is unavailable or policy-restricted.

## Risks / Trade-offs
- WebView2 runtime dependency and enterprise policy restrictions.
  - Mitigation: startup capability check + automatic fallback + explicit user-visible status.

- C# <-> JS bridge latency for high-frequency events.
  - Mitigation: debounce/coalesce bridge messages; use push updates only for changed diagnostics/completion contexts.

- Two editor engines to maintain during migration.
  - Mitigation: retain single `IScriptEditor` contract and keep behavior tests engine-agnostic where possible.

- Theme/font mismatch between WinForms and web-rendered editor.
  - Mitigation: centralize theme token mapping and run parity checks for dark/light + custom font settings.

## Migration Plan
1. Add Monaco host control + minimal text/selection bridge behind `IScriptEditor`.
2. Wire parser-driven diagnostics/completion into Monaco APIs.
3. Implement UX parity items (scroll-past-end, reveal-on-enter, completion dismissal rules, caret behaviors).
4. Add engine setting/fallback path and startup checks.
5. Run side-by-side validation and targeted perf testing on 500+ line scripts.
6. Promote Monaco engine to default only after parity gate passes.

## Open Questions
- Whether Monaco should become default immediately after parity, or remain opt-in for one release.
- Whether to keep smart-enter/indentation in C# bridge logic or move fully to Monaco command handlers after parity baseline.

