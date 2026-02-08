# Change: Add Monaco-based script editor host

## Why
The current WinForms-native script editor has improved, but it still shows UX jank under common authoring flows (focus churn, scroll behavior edge cases, and perceived typing latency). Operators explicitly expect VS Code-like editing behavior.

Using the same core editor engine as VS Code (Monaco) gives a stronger baseline for scrolling, cursor handling, selection, inline tooling, and large-document responsiveness.

## What Changes
- Add a new script editor implementation that hosts Monaco in a WebView2 surface inside the existing WinForms app.
- Keep scripting semantics parser-driven from existing C# services (`ScriptParser`, validation services, symbol extraction) and bridge results into Monaco diagnostics/completion APIs.
- Define explicit UX parity goals for:
  - smooth typing and caret movement
  - cursor reveal on `Enter`
  - scroll-past-end behavior so the last line can be positioned near the top of the viewport
  - autocomplete dismissal on explicit caret relocation clicks.
- Keep the editor feature set intentionally scoped to SSH Helper needs (not full VS Code workbench emulation), with optional future feature expansion.
- Add an engine-selection/fallback strategy so the app can safely fall back to the current native editor if Monaco host initialization fails.
- Package editor assets locally and run without remote CDN dependencies.

## Impact
- Affected specs:
  - `script-editor-monaco-host` (new capability)
- Affected code:
  - `UI/MonacoScriptEditorControl.cs` (new)
  - `UI/IScriptEditor.cs`
  - `Form1.cs`
  - `Form1.Designer.cs`
  - `SettingsDialog.cs`
  - `Models/AppConfiguration.cs`
  - `Services/Editor/*` bridge/adapter additions
  - `Web/EditorHost/*` (new local Monaco host assets and JS bridge)
  - `SSH_Helper.csproj`

