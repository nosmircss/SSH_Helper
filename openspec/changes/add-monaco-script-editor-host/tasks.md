# Tasks: Add Monaco-based script editor host

## 1. Host foundation
- [ ] 1.1 Add WebView2-backed `MonacoScriptEditorControl` implementing `IScriptEditor`.
- [ ] 1.2 Package local Monaco host assets (`Web/EditorHost/*`) and load them without remote CDN dependencies.
- [ ] 1.3 Add startup capability checks for Monaco/WebView2 availability.

## 2. Bridge contract
- [ ] 2.1 Define C# <-> JS message contract for text, selection, caret position, and focus events.
- [ ] 2.2 Implement parser-driven completion bridge (commands/options/symbols) into Monaco completion provider.
- [ ] 2.3 Implement diagnostics bridge from `ScriptEditorValidationService` into Monaco marker model.
- [ ] 2.4 Map existing context menu and keyboard actions (`Ctrl+S`, validate, pretty-format) through the new host.

## 3. UX parity and behavior fixes
- [ ] 3.1 Implement scroll-past-end behavior so last line can be positioned near top of viewport.
- [ ] 3.2 Ensure pressing `Enter` on the final line reveals caret on the newly inserted line.
- [ ] 3.3 Close autocomplete suggestions when user clicks to reposition caret.
- [ ] 3.4 Preserve bottom-of-editor completion visibility with above/below placement and viewport clamping.
- [ ] 3.5 Preserve YAML-focused indentation and smart-enter behavior parity with existing settings.

## 4. Settings, fallback, rollout
- [ ] 4.1 Add editor engine setting (Monaco vs native) with safe default and migration behavior.
- [ ] 4.2 Implement automatic fallback to native editor when Monaco host initialization fails.
- [ ] 4.3 Surface fallback/host status for diagnostics and supportability.

## 5. Verification
- [ ] 5.1 Add unit/integration tests for bridge serialization and diagnostics/completion synchronization.
- [ ] 5.2 Add behavior tests for scroll-past-end and reveal-on-enter scenarios.
- [ ] 5.3 Run manual UX smoke tests for typing, completion, diagnostics, and theme/font parity.
- [ ] 5.4 Run 500+ line script responsiveness checks and document observed latency improvements/regressions.

## 6. Documentation
- [ ] 6.1 Document architecture and runtime dependency expectations in engineering docs.
- [ ] 6.2 Update user-facing docs for editor engine setting and fallback behavior.

