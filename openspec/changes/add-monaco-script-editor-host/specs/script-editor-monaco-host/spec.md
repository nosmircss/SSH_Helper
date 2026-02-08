## ADDED Requirements

### Requirement: Monaco-based editor engine
The application SHALL provide a script editor engine backed by Monaco, hosted inside the WinForms UI via WebView2, while preserving existing `IScriptEditor` workflow integration.

#### Scenario: Monaco host initializes for script editing
- **WHEN** the Commands editor is opened and Monaco hosting is available
- **THEN** script authoring uses the Monaco-backed engine through the existing editor abstraction

### Requirement: Scoped feature baseline
The Monaco editor integration SHALL prioritize only features required by SSH Helper script authoring workflows, with optional future features enabled explicitly rather than by default.

#### Scenario: Feature set is intentionally scoped
- **WHEN** Monaco host is initialized
- **THEN** editing features required for scripting (typing, selection, completion, diagnostics, indentation, hover, save shortcuts) are enabled
- **AND** unrelated workbench features are not required for initial rollout

### Requirement: Parser-driven semantics parity
Autocomplete and diagnostics in the Monaco host SHALL remain parser-driven from existing scripting services and SHALL NOT introduce an independent hard-coded command grammar.

#### Scenario: Completion and validation remain parser-aligned
- **WHEN** parser/runtime metadata changes supported commands/options
- **THEN** Monaco suggestions and diagnostics reflect parser-supported behavior without a separate static editor command list

### Requirement: Scroll and caret reveal parity
The Monaco editor SHALL support VS Code-like scroll behavior, including scroll-past-end and deterministic caret reveal after newline insertion at the end of the document.

#### Scenario: Last line can scroll near top
- **WHEN** the operator scrolls to the end of a script
- **THEN** the viewport can position the last content line near the top of the editor area

#### Scenario: Enter at final line reveals new caret line
- **WHEN** the caret is on the last line and the operator presses `Enter`
- **THEN** the editor scroll position updates so the caret remains visible on the newly inserted line

### Requirement: Completion interaction predictability
Autocomplete suggestions in the Monaco editor SHALL dismiss when the user explicitly relocates the caret by mouse click.

#### Scenario: Click-to-move caret closes suggestions
- **WHEN** autocomplete suggestions are visible and the operator clicks to move the caret
- **THEN** the suggestion popup closes immediately

### Requirement: Local asset and runtime security boundary
Monaco host assets SHALL be loaded from local application resources/files and SHALL NOT require remote CDN fetches at runtime.

#### Scenario: Offline startup
- **WHEN** the application starts in an offline environment
- **THEN** the Monaco editor host initializes without network access to editor assets

### Requirement: Engine fallback safety
The application SHALL provide a fallback path to the native script editor when Monaco host initialization is unavailable or fails at runtime.

#### Scenario: Automatic fallback on host failure
- **WHEN** Monaco/WebView2 initialization fails
- **THEN** the app falls back to the native script editor without blocking script authoring
- **AND** editor actions remain usable

