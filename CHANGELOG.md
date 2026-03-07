# Changelog

## Changes Since `12c1b7f` (0.51.7)

### Environment CSV Freshness Detection

A file fingerprinting system detects when a remembered CSV file has changed on disk since it was last loaded into an environment. `CsvFileFingerprint` records `LastWriteTimeUtc` and `FileSizeBytes` for each loaded CSV. `CsvFileSyncEvaluator` compares the stored fingerprint against the current file state and returns a `CsvFileSyncStatus`:

| Status | Meaning |
|--------|---------|
| `NotTracked` | No CSV file associated with the environment |
| `Current` | File on disk matches the stored fingerprint |
| `ChangedOnDisk` | File has been modified externally since last load |
| `MissingOnDisk` | Remembered file no longer exists at the stored path |
| `Unknown` | Fingerprint unavailable or comparison error |

When switching to an environment whose CSV has changed on disk, the user is prompted to reload or keep the in-memory version. `EnvironmentConfig` now stores `LastCsvFingerprint` alongside `LastCsvPath`, and `ApplicationState` carries the fingerprint through save/restore cycles.

**Hosts file indicator** — `HostsFileIndicatorFormatter.Format` produces a display string for the loaded CSV status, combining the filename with state suffixes such as `(unsaved)`, `(disk changed)`, or `(missing on disk)`.

### Folder-Level Base Environment Inheritance

Preset folders can now declare a **base environment override** that applies to all presets within that folder and its subfolders. `FolderInfo.BaseEnvironment` stores an optional environment name per folder.

**Resolution chain** — `PresetBaseEnvironmentResolver.Resolve` walks from the preset's folder up through ancestor folders, returning the first non-null `BaseEnvironment` it finds. If no folder in the chain declares an override, the global base environment is used. The result includes `SourceKind` (`GlobalBase` or `FolderBase`) and `SourceFolderPath` for UI display.

**Folder context menu** — A new **Base Environment** submenu on the folder right-click context menu lists all available environments plus an inherit option. Selecting an environment calls `PresetManager.SetFolderBaseEnvironment`. The inherit choice label shows the resolved parent environment via `FolderBaseEnvironmentSummaryFormatter.FormatInheritChoiceLabel`.

**Environment rename/delete propagation** — `PresetManager.RenameFolderBaseEnvironment` updates all folder references when an environment is renamed. `PresetManager.ClearFolderBaseEnvironment` removes references to a deleted environment. Both are called from `EnvironmentDialog` during rename/delete operations.

**Base environment toolbar indicator** — `BaseEnvironmentIndicatorFormatter.Format` produces a `Base: <name>` label visible in the toolbar only when the active environment differs from the resolved base environment.

### Script-Declared Environment Switching on Preset Load

Scripts can now declare a top-level `environment` key that triggers an automatic environment switch when the preset is loaded into the editor:

```yaml
---
name: Production Health Check
environment: prod

steps:
  - print: "Running against ${Host_IP}"
```

**Load behavior** — `PresetEnvironmentLoadPlanner.Plan` determines the action when a preset is selected:
- If the script declares an `environment` that differs from the active environment, the active environment switches to the declared one
- If the script has no `environment` declaration, the active environment restores to the base environment
- If the declared environment does not exist, the current environment stays active and a non-blocking status message is shown via `PresetEnvironmentStatusFormatter.FormatMissingEnvironmentMessage`

The base environment (set by manual environment switches or folder overrides) is never changed by script declarations — only the active environment is affected.

### Suppress Missing Column Warning

A new `suppress_missing_column_warning` script header flag disables the pre-execution dialog that warns about referenced grid columns not present in the current host grid. `ScriptDependencyAnalyzer.AnalyzePresetDetails` returns a `PresetColumnDependencyResult` that includes the `SuppressMissingColumnWarning` flag from the parsed script.

```yaml
---
suppress_missing_column_warning: true
steps:
  - if: "${optional_column}" == ""
    then:
      - input:
          prompt: "Column missing. Enter a value:"
          into: optional_column
```

### Preset Header Unsaved State Indicator

`PresetHeaderIndicatorFormatter` formats the preset tree header label with contextual information:
- When a folder is selected: `Folder: <name>`
- When a preset is selected: `Preset: <name>` or `Preset: <name> (unsaved)` when dirty
- When no preset is selected: `Presets` or `Presets (unsaved)`

`FormatCommandSectionTitle` and `FormatSaveButtonLabel` provide parallel formatting for the command section header and save button, appending `(unsaved)` or `*` respectively when changes are pending. The indicators auto-refresh via `TextChanged` handlers on the command editor, preset name, and timeout fields.

### Connection Testing

`SshExecutionService.TestConnectionAsync` performs a lightweight TCP reachability check against a host, returning a `ConnectionTestResult` record with `Success`, `ErrorCategory` (`Timeout`, `Network`, `Cancelled`, `Unknown`), `ErrorMessage`, and `LatencyMs`. This is a TCP connect/disconnect test rather than full SSH authentication.

### Recent Files Menu

`AppConfiguration.RecentFiles` stores the most recently opened CSV file paths (newest first), capped at `MaxRecentFiles` (default 10). A **Recent Files** submenu is added to the File menu, rebuilt dynamically as files are opened.

### Autocomplete Behavior Improvements

**Trailing blank line suppression** — `ScriptAutocompleteProvider.ShouldAutoSuggestBlankTopLevelKeys` prevents the autocomplete popup from appearing on blank lines after `vars:` or `steps:` blocks, where top-level key suggestions are no longer contextually appropriate.

**Nested table column highlighting** — `YamlSshSyntaxHighlighter` now recognizes `header` and `field` as step option keys within `table` column definitions, applying syntax highlighting to nested column configuration keys.

### Terminal Output Chunked Streaming Improvements

**Trailing space preservation** — `TerminalOutputProcessor.Normalize` accepts a new `preserveTrailingSpacesOnFinalLine` parameter. When true, trailing spaces on the final (unfinished) line are preserved to prevent word-joining artifacts when tokens arrive split across network chunks (e.g., `"set "` then `"resource"`). `SshShellSession.ProcessChunk` enables this for real-time UI output.

**Streaming zsh PROMPT_SP stripping** — `TerminalOutputProcessor.StripZshPromptSpStreaming` replaces the single-pass `StripZshPromptSp` for live stream processing. It buffers ambiguous suffixes (a `%` that might be a real character or the start of a prompt redraw sequence) across chunks and flushes on stream completion, preventing half-processed artifacts from appearing in output.

### Editor Line Index Fix

`EditorTextUtilities` line-start computation now includes the final line even when the text ends with a newline character, fixing off-by-one issues in syntax highlighting and autocomplete positioning for files with trailing blank lines.

### Grep Command for Sensitive Data Detection

A new grep-based search command in settings scans configuration data for patterns matching sensitive information such as credentials, tokens, and connection strings.

### Build Configuration

`SSH_Helper.csproj` `DefaultItemExcludes` and a new `Compile Remove` item use forward-slash glob patterns (`artifacts/**`, `bin/**`, `obj/**`) for more robust exclusion of non-source directories.

### Documentation

`SCRIPTING.md` updated with:
- New `environment` top-level key in the script structure reference with behavior documentation and examples
- New `suppress_missing_column_warning` header flag with usage guidance
- `environment` added to the list of metadata-only keys that do not trigger YAML script detection

### Test Coverage

New test suites added:

- **Utilities** — `CsvFileSyncEvaluatorTests` (file fingerprint matching, missing file detection, column/row snapshot comparison), `HostsFileIndicatorFormatterTests` (label formatting with dirty/sync combinations), `BaseEnvironmentIndicatorFormatterTests` (visibility logic when active differs from base), `PresetBaseEnvironmentResolverTests` (folder chain walk, global fallback), `PresetEnvironmentLoadPlannerTests` (switch/restore/no-op decisions), `PresetEnvironmentStatusFormatterTests` (restore, switch, and missing environment message formatting), `FolderBaseEnvironmentSummaryFormatterTests` (explicit, inherited, and global summary lines), `PresetHeaderIndicatorFormatterTests` (folder/preset/dirty label permutations), `TerminalOutputProcessorTests` (trailing space preservation, streaming zsh PROMPT_SP stripping)
- **Services** — `EnvironmentServiceTests` (expanded with base environment get/set, fingerprint round-trip), `PresetManagerFolderBaseEnvironmentTests` (set, clear, rename folder base environments, orphan cleanup on load)
- **Scripting** — `ScriptParserTests` (expanded with `environment` and `suppress_missing_column_warning` parsing), `ScriptDependencyAnalyzerTests` (expanded with `SuppressMissingColumnWarning` detection)
- **Editor** — `ScriptAutocompleteProviderTests` (expanded with trailing blank line suppression), `YamlSshSyntaxHighlighterTests` (expanded with nested table column key highlighting), `EditorTextUtilitiesTests` (expanded with trailing newline line-start fix), `ScintillaScriptEditorControlTests` (expanded with additional editor behavior tests)

---

## Changes Since `d588087` (0.51.6)

### Interactive Terminal Transcript Handling

**Mirrored transcript normalization** — When `mirror_output: true` is set on a non-capture interactive step (shared session without a `command`), the transcript text is now normalized through `InteractiveTerminalService.NormalizeMirroredTranscript` before emission. This removes control artifacts such as `^D`, backspace sequences, and ANSI escape codes from mirrored output that previously appeared as visual noise in the script output pane. The normalization pipeline chains `TerminalOutputProcessor.Sanitize` (strips ANSI codes) with `TerminalOutputProcessor.Normalize` (processes CR, LF, TAB, BS, CSI) to produce clean readable text.

**Transcript and mirror output capping** — Interactive capture sessions now enforce line-count caps to prevent unbounded memory growth during long-running captures:

| Limit | Default | Purpose |
|-------|---------|---------|
| `InteractiveTranscriptMaxLines` | 500,000 lines | Caps the internal `transcriptBuilder` used for `interactive.capture` variable storage |
| `InteractiveMirrorOutputMaxLines` | 50,000 lines | Caps the chunks emitted to the script output pane via `mirror_output` |

When a cap is reached, further chunks are dropped and a `[... interactive transcript capped ...]` or `[... interactive mirror output capped ...]` notice is appended. The capping logic uses `AppendTranscriptWithCap` and `ApplyMirrorOutputCap`, both exposed as `internal static` for testability.

**Cross-chunk control sequence handling** — `PrepareMirroredChunkForEmission` buffers partial control sequences (e.g., `^` arriving at the end of one chunk and `D\b\b` at the start of the next) to avoid emitting half-processed escape artifacts. On final flush (`flush: true`), any remaining buffered content is normalized and emitted.

### Preset Timeout Reset

`PresetManager.ApplyDefaults(int)` is replaced by `PresetManager.ClearAllTimeouts()`, which clears `Timeout` overrides from all presets (setting them to `null`) so they inherit the global default. The method returns the count of modified presets.

A new **Reset All Preset Timeouts to Default** button in `SettingsDialog` (General tab, under Default Values) calls `ClearAllTimeouts()` and reports the number of cleared presets. The result is tracked via `SettingsDialog.PresetTimeoutsWereCleared` so `Form1` can update the active editor timeout field after the dialog closes, preventing stale override values from being re-persisted on the next save.

The timeout header field (`txtTimeoutHeader`) now uses `PlaceholderText` set to the global default timeout value, providing a visual hint of the inherited timeout when a preset has no explicit override.

### Preset Tree View Improvements

**Expand/Collapse All Subfolders** — Right-clicking a folder node that contains nested subfolders now shows **Expand All Subfolders** and **Collapse All Subfolders** context menu items (with a separator). These recursively expand or collapse all descendant folder nodes, persist the expanded state via `PresetManager.SetFolderExpanded`, and anchor the viewport to prevent scroll jumps during the operation.

**Startup scroll-to-selection** — After startup restore, `Form1.EnsureSelectedPresetNodeVisible` scrolls the selected preset node into view unless it lives under a deliberately collapsed ancestor, preserving saved collapse state.

### Autocomplete Popup Dismiss on External Click

`ScintillaScriptEditorControl` registers a `CompletionDismissMessageFilter` (an `IMessageFilter`) that intercepts mouse-down messages (`WM_LBUTTONDOWN`, `WM_RBUTTONDOWN`, `WM_MBUTTONDOWN`, `WM_XBUTTONDOWN`, and their non-client variants). When a mouse-down targets a window outside the editor control hierarchy, the completion popup is dismissed. The filter uses a `WeakReference<ScintillaScriptEditorControl>` to avoid preventing garbage collection. Additionally, `_editor.LostFocus` now hides the popup. The message filter is removed in `Dispose`.

### Test Coverage

- **InteractiveCommandTests** — `ExecuteAsync_WithMirrorOutputInSharedMode_NormalizesControlArtifacts` verifies that `^D\b\b` sequences are stripped from mirrored transcript output
- **InteractiveTerminalServiceTranscriptFilterTests** — 14 new tests covering `AppendTranscriptWithCap` (line capping, post-cap suppression, large single-line passthrough), `ApplyMirrorOutputCap` (cap triggering, post-cap suppression), `NormalizeMirroredTranscript` (control artifact removal), `BuildMirroredStartupPromptPrefix`, `PrependStartupPromptIfMissing` (empty/duplicate/normal), `ResolveStartupPromptLiteral` (preference order), `PrepareMirroredChunkForEmission` (cross-chunk control sequences), and `ResolveBannerAcceptKey` (press-to-accept and press-any-key patterns)
- **SshExecutionServiceOutputFormattingTests** — 3 new tests for `NormalizeScriptOutputBoundary` covering non-raw-after-raw boundary insertion, duplicate boundary prevention, and raw-chunk passthrough
- **ScintillaScriptEditorControlTests** — `CompletionPopup_OutsideClickDismissesSuggestions` verifies external-click dismissal via the message filter

---

## Changes Since `6901b46` (0.51.5)

### Unsaved Preset Diff Dialog

When switching away from or closing a preset with unsaved changes, the confirmation prompt now displays an inline diff view instead of a plain text question. `UnsavedPresetDiffDialog` compares the saved and current preset state, showing:

- **Name changes** — displayed as a `~ Name: "old" -> "new"` meta line
- **Timeout changes** — displayed as a `~ Timeout: old -> new` meta line
- **Command text changes** — rendered as a color-coded inline diff with `+` (added), `-` (removed), and context lines

The diff is computed by `InlineDiffBuilder`, a new LCS-based utility in `Utilities/InlineDiffBuilder.cs`. It normalizes line endings, computes the longest common subsequence between original and updated lines, and produces a list of `InlineDiffLine` entries tagged as `Context`, `Added`, `Removed`, or `Meta`. For very large inputs (over 2 million LCS cells), it falls back to a linear line-by-line comparison. Output is capped at a configurable `maxOutputLines` with a `... diff truncated` marker. An `includeAllLines` flag disables context-line collapsing to show the entire script body with changes highlighted.

The dialog replaces the previous `MessageBox.Show("Save changes to preset?")` prompt at five call sites in `Form1.cs`: preset list selection changes, preset tree double-click, and application exit.

### Themed Message Box Replacement

All `MessageBox.Show` calls across the application (90 instances) are replaced with `DialogTheme.Show`, a new drop-in replacement that renders themed, dark-mode-aware message dialogs consistent with the rest of the UI.

`DialogTheme.Show` provides overloads matching the standard `MessageBox.Show` signatures:

| Overload | Parameters |
|----------|------------|
| `Show(string)` | Message only |
| `Show(string, string)` | Message + title |
| `Show(string, string, MessageBoxButtons)` | Message + title + buttons |
| `Show(string, string, MessageBoxButtons, MessageBoxIcon)` | Full parameters |
| `Show(IWin32Window?, string, string, MessageBoxButtons, MessageBoxIcon)` | With owner |

The internal `ShowCore` method dynamically lays out the dialog based on content: auto-sized label with configurable maximum width, system icon alignment, and a variable-width button row generated from `GetButtonSpecs` for all standard `MessageBoxButtons` combinations (OK, OKCancel, YesNo, YesNoCancel, RetryCancel, AbortRetryIgnore). Dark mode and font are auto-resolved from the owner window or `Application.OpenForms` via `ResolveDarkMode` and `ResolveDialogFont`.

Affected files: `Form1.cs` (72 replacements), `EnvironmentDialog.cs` (9), `ExecutionDetailsDialog.cs` (3), `UpdateDialog.cs` (6), `SettingsDialog.cs` (1).

### Debug Popup Gallery

A new debug-only menu item **Edit > View All Popups** (`viewAllPopupsToolStripMenuItem`) walks through all themed popup styles in sequence: No Icon, Information, Warning, Error, Question (Yes/No), Question (Yes/No/Cancel), and the Unsaved Preset Diff dialog. Each step shows a sample popup, then asks whether to continue to the next. This provides a single entry point for visually verifying dialog theming, layout, icon alignment, and button styling.

### Test Coverage

- **Utilities** — `InlineDiffBuilderTests` covering identical content with different line endings, replaced-line detection (removed + added lines), collapsed marker insertion for distant changes, and `includeAllLines` mode verification

---

## Changes Since `c8e6a68` (0.51.4)

### Interactive Terminal Scripting

A new `interactive` scripting command opens an in-app SSH terminal window directly from YAML scripts, providing live terminal access during automated workflows. Two session modes are supported:

**Separate mode** (`session: separate`) opens a dedicated SSH connection with its own terminal emulation. The script pauses until the operator closes the terminal window.

**Shared mode** (`session: shared`) attaches to the active script SSH session. Pressing `Ctrl+D` or typing `exit`/`logout` detaches the window without sending those commands to the underlying shell, preserving the shared session for subsequent script steps.

```yaml
# Open an interactive troubleshooting session
- interactive:
    session: separate
    title: "Troubleshooting - ${Host_IP}"
    width: 1200
    height: 760
```

**Capture mode** adds automated command execution with transcript collection. Setting `command` auto-sends the command once the terminal is ready. The step completes when Ctrl+C is pressed, a timeout fires, or the line limit is reached:

```yaml
# Capture a packet sniffer with timeout
- interactive:
    session: separate
    command: "diagnose sniffer packet any 'host 10.0.0.1' 4 10 a"
    capture: sniffer_output
    max_seconds: 120
    max_lines: 500
    mirror_output: false
```

When `show_window: true` (the default), pressing Ctrl+C or hitting a limiter completes the step while the terminal window remains open as a detached read-only view for copy/review. Setting `show_window: false` runs capture headlessly with no terminal window displayed.

| Parameter | Default | Description |
|-----------|---------|-------------|
| `session` | `separate` | `separate` or `shared` |
| `title` | host-based | Custom window title |
| `command` | — | Enables capture mode; auto-runs this command |
| `capture` | — | Variable name for the captured transcript |
| `max_seconds` | — | Auto-sends Ctrl+C after this timeout |
| `max_lines` | — | Auto-sends Ctrl+C after this many captured lines |
| `width` | `980` | Window width in pixels |
| `height` | `620` | Window height in pixels |
| `show_window` | `true` | `false` for headless capture (requires `command` + a limiter) |
| `mirror_output` | `false` | Mirror captured chunks into script output in real time |
| `on_error` | `stop` | `continue` or `stop` |

The terminal viewport (`InteractiveTerminalViewportControl`) is a custom double-buffered cell-based renderer with full Rebex `VirtualTerminal` color fidelity, PuTTY-like text selection (highlight auto-copies, right-click pastes), system menu commands (Copy All to Clipboard, Clear Scrollback, Reset Terminal), scrollback history rendering via `GetRegion` negative-row indexing, and follow-tail resize compensation that keeps the prompt anchored at the bottom.

**Preflight enforcement** — `interactive` is restricted to single-host runs. Multi-host script executions and folder runs are rejected in preflight with a message naming the blocked preset(s). `SshExecutionService` and `FolderExecutionDialog` both enforce this restriction via `ScriptDependencyAnalyzer.AnalyzeSshRequirements`.

### Interactive Session Audit Trail

Interactive terminal sessions now capture audit metadata into execution details. Each session records host address, session mode (`separate`/`shared`), emulation mode, start/end timestamps, close reason, and a filtered transcript. The `ExecutionDetailsDialog` displays a dedicated "Interactive" tab with a session grid and transcript viewer. Transcript filtering strips alternate-screen application redraws (`vi`/`less`/`top`) while preserving normal shell output.

### New Scripting Commands

Four new scripting commands expand control flow and output formatting:

**`assert` — Condition Validation**

Validates that a condition is true using the same expression syntax as `if` conditions. Supports `severity: error` (stops the script, default) and `severity: warning` (logs and continues). Custom failure messages support `${variable}` substitution.

```yaml
- assert:
    condition: "status == 'up'"
    message: "Host ${Host_IP} is down"

- assert:
    condition: "latency < 100"
    message: "High latency: ${latency}ms"
    severity: warning
```

**`switch` — Multi-Branch Dispatch**

Dispatches execution based on a value matching one of several cases. Case comparison is case-insensitive. Prefix a case value with `matches` to use regex matching. An optional `else` block handles unmatched values.

```yaml
- switch: "${os_type}"
  cases:
    - value: linux
      do:
        - send:
            command: uname -a
            capture: sys_info
    - value: "matches ^7\\.0"
      do:
        - print:
            message: "Version 7.0.x detected"
  else:
    - print:
        message: "Unknown: ${os_type}"
```

**`parallel` — Concurrent Execution**

Executes multiple steps concurrently with an optional `max_concurrent` limit. All parallel steps share the same script context with last-write-wins variable semantics. `send` steps running on the same SSH session are serialized for stream safety. `break`/`continue` signals from parallel children propagate to enclosing loops.

```yaml
- parallel:
    max_concurrent: 3
    steps:
      - ping:
          target: "${Host_IP}"
          capture: ping_result
      - portcheck:
          host: "${Host_IP}"
          port: 443
          capture: https_check
      - dns:
          query: "${Host_IP}"
          type: PTR
          capture: ptr_record
```

**`table` — Formatted Table Output**

Formats data into aligned columns for display. Accepts `List<string>`, JSON arrays of objects, or newline-delimited strings. Columns auto-detect from data keys or can be explicitly defined with header, field mapping, alignment (`left`/`right`/`center`), and fixed width. The formatted text can be captured into a variable via `into`.

```yaml
- table:
    data: "${json_data}"
    columns:
      - header: Host
        field: host
        width: 15
      - header: Status
        field: status
        align: center
    into: report_text
```

### List Expression Helpers

Five new list mutation functions operate on script variables directly:

| Function | Description |
|----------|-------------|
| `push(list, value)` | Appends a value to the end of a list |
| `pop(list)` | Removes and returns the last element |
| `unshift(list, value)` | Prepends a value to the beginning |
| `shift(list)` | Removes and returns the first element |
| `slice(list, start, end)` | Returns a sub-list between start and end indices |

These complement the existing `json.push()`/`json.pop()` functions and work with both `List<string>` variables and JSON arrays. `push` and `unshift` create a new list if the target variable does not exist.

### History Storage Refactoring

Execution history payloads have been moved from inline `config.json` storage to external per-run JSON files under `%LocalAppData%\SSH_Helper\history\`. A lightweight index file (`history.index.json`) maintains the history list for UI display, while full per-run payloads (`history/<run-id>.json`) are loaded on demand.

`HistoryStorageService` handles payload serialization, index management, and orphan cleanup. `HistoryRunPayload` encapsulates the full execution output, details, and per-host results for each run. Lightweight deserialization via `Utf8JsonReader` extracts metadata without fully parsing large payload fields.

### Modeless Script Prompt Dialogs

Script prompt dialogs (`input`, `choose`, `multiselect`, `confirm`) now run modeless on the UI thread via `ScriptPromptDialogRunner`. The main form's control tree is temporarily disabled except the Stop button ancestor chain, so operators can cancel script execution while a prompt is displayed. `ScriptPromptDialogRunner.ShowAsync<TDialog, TResult>` shows the dialog modeless with `Show(owner)` and awaits a `TaskCompletionSource` that resolves when the dialog closes or cancellation is requested.

### Dialog Title Support

`input`, `choose`, `multiselect`, and `confirm` scripting commands now accept an optional `title` parameter that overrides the dialog window title. When omitted, the default title is used.

```yaml
- choose:
    prompt: "Select target environment:"
    into: target_env
    title: "Environment Selection"
    options:
      - dev
      - staging
      - prod
```

### Dynamic Choice Options

`choose.options` and `multiselect.options` now accept a scalar runtime source in addition to inline YAML lists. Setting `options: ${interface_list}` or `options: interface_list` resolves the options from a `List<string>`, JSON array, or comma-delimited string variable at runtime. `ChoiceOptionResolver` handles resolution with error reporting when the source variable is empty or unresolvable.

### WriteFile Save Path Prompt

`writefile` now prompts for a save location when the configured path is relative (not rooted). A Save File dialog is shown on the UI thread, and the selected path is stored in the `_writefile` runtime variable for use in subsequent steps. Cancelling the dialog respects `on_error` handling. Runtime variable references in `writefile.content` are now resolved before writing.

### ScriptContext Thread Safety

`ScriptContext` variable access (`SetVariable`, `GetVariable`, `HasVariable`, `RemoveVariable`, `GetAllVariables`) is now synchronized with locks for safe concurrent access during `parallel` step execution. `LoopDepth` uses `AsyncLocal<int>` to maintain per-task loop depth in parallel branches.

### QA Presets

New QA presets added for validating the new commands:
- **QA Assert** — Tests condition evaluation, custom messages, warning severity, and variable substitution
- **QA Switch** — Tests case matching, regex cases, default branches, and variable dispatch
- **QA Parallel** — Tests concurrent execution, max_concurrent limiting, and variable capture
- **QA Table** — Tests JSON array formatting, list formatting, explicit columns, and alignment
- **QA Control Flow Primitives** — Updated with assert, switch, and parallel coverage

### Documentation

`SCRIPTING.md` updated with full reference sections for `interactive`, `assert`, `switch`, `parallel`, and `table` including syntax, parameter tables, behavior notes, and usage examples. New sections on built-in retry and manual retry patterns added.

### Test Coverage

New test suites added:

- **Scripting** — `InteractiveCommandTests`, `TableCommandTests`, `ChoiceOptionResolverTests`, `QaPresetsSyntaxTests`, `ScriptParserTests` (expanded with assert/switch/parallel/table/interactive parsing), `ScriptExecutorControlFlowTests` (expanded), `ScriptDependencyAnalyzerTests` (expanded with interactive detection), `SetCommandTests` (expanded), `WriteFileCommandTests` (expanded), `ChooseCommandTests` (expanded with dynamic options), `MultiselectCommandTests` (expanded with dynamic options), `ScriptContextTests` (expanded)
- **Services** — `HistoryStorageServiceTests`, `InteractiveTerminalServiceTranscriptFilterTests`, `SshExecutionServiceInteractivePreflightTests`, `SshExecutionServiceOutputFormattingTests`, `ConfigurationServiceExecutionDetailsTests` (expanded), `HistoryResultStoreTests` (expanded)
- **UI** — `ExecutionDetailsDialogTests`
- **Utilities** — `PromptDetectorTests`
- **Editor** — `ScriptAutocompleteProviderTests` (expanded with new command completions)

---

## Changes Since `86f4dc2` (0.51.3)

### Interactive Scripting Commands

Three new scripting commands let scripts prompt users for input during execution:

**`choose` — Single-Select from List**

Presents a dialog where the user picks one option from a list. Options can be simple strings or label/value pairs with a different display label from the stored value. Supports a `default` pre-selection and variable substitution in prompts and option text.

```yaml
- choose:
    prompt: "Select management protocol:"
    into: mgmt_port
    options:
      - label: "SSH (22)"
        value: "22"
      - label: "HTTPS (443)"
        value: "443"
    default: "22"
```

**`multiselect` — Multiple-Select from Checklist**

Presents a checkbox list for selecting multiple items. Stores the result as a list accessible via `${var[0]}`, `${var.length}`, and `foreach` iteration. Also sets `${var}_count`. Supports optional `min`/`max` selection constraints with inline validation.

```yaml
- multiselect:
    prompt: "Select interfaces to configure:"
    into: selected_ifaces
    options:
      - GigabitEthernet0/0
      - GigabitEthernet0/1
      - Loopback0
    min: 1
    max: 3
```

**`confirm` — Yes/No Confirmation**

Presents a simple yes/no dialog. Stores `"true"` or `"false"` as a string. Unlike `input`, confirm never fails — it always stores a value regardless of which button is pressed. The `default` field controls which button is pre-focused.

```yaml
- confirm:
    prompt: "Apply configuration changes?"
    into: confirmed
    default: false
```

All three commands:
- Support variable substitution in prompts and option text
- Respect `on_error: continue` for error handling
- Auto-detect dark mode and render themed dialogs
- Integrate with the dependency analyzer for column reference tracking

### Local Script Execution

Scripts that don't require an SSH session are now detected and executed locally without establishing an SSH connection. A static analyzer walks the parsed script tree and checks whether any `send` or default-host `sftp` steps are present.

When a script contains only local commands (e.g., `set`, `print`, `choose`, `http`, `dns`, `readfile`, `writefile`, control flow), it runs in a local execution path that:
- Skips SSH connection setup entirely
- Skips invalid host validation when no SSH session is needed
- Shows a `LOCAL SCRIPT` banner instead of the SSH connection header
- Still receives host context variables (IP, columns, environment variables)

This means scripts that only do local work (file processing, HTTP calls, user prompts, variable manipulation) no longer require valid SSH credentials or reachable hosts.

### List Variable Rendering

`ScriptContext.GetVariableString` now joins `List<string>` values with `, ` when interpolated via `${var}`. This makes multiselect results and DNS result lists readable in `print` and `log` output without manual iteration.

### QA Presets

Three new QA presets added under `QA/Interactive`:
- **QA Choose Basic** — Tests simple options, label/value pairs, default selection, and conditional branching
- **QA Multiselect Basic** — Tests min/max constraints, count variable, foreach iteration, and index access
- **QA Confirm Basic** — Tests default values, conditional branching, and value validation

### Documentation

`SCRIPTING.md` updated with full reference sections for `choose`, `multiselect`, and `confirm` including syntax, parameter tables, feature notes, and usage examples.

---

## Changes Since `f34fb7c` (0.51.0)

### Environment Management

A full environment system allows managing multiple named profiles (e.g., dev, staging, prod), each with independent host grids, variables, and visual identity.

- **Environment profiles** — Each environment stores its own host grid columns, host entries, selected host indices, last CSV path, and a set of key-value variables
- **Toolbar integration** — A dropdown button on the toolbar shows the active environment name with an optional color swatch; switching environments swaps the entire host grid and variable context
- **Management dialog** — A dedicated resizable dialog provides CRUD operations: create, duplicate, rename, delete, and edit description, label color, and variables per environment
- **Import/Export** — Environments serialize to `.sshenv.json` files for sharing across machines or teams, with conflict resolution on import (overwrite or rename)
- **Variable scoping** — Each environment has its own variable dictionary; active environment variables are injected into SSH execution context and script runtime
- **Script integration** — A new `updateenvironment:` command allows YAML scripts to persist variable updates back to the active environment during execution, with the updated value immediately available to subsequent steps
- **Label colors** — Optional ARGB color per environment provides at-a-glance identification in the toolbar dropdown and management dialog list
- **Window title** — The application title bar now shows the active environment name
- **Default environment** — A reserved "Default" environment is always present and cannot be renamed or deleted; legacy state is automatically captured into Default on first use

### Multi-Protocol Network Commands

Six new scripting commands extend workflow capabilities beyond SSH:

| Command | Protocol | Captures | Key Capabilities |
|---------|----------|----------|------------------|
| `http:` | HTTP/HTTPS | body, status code, headers | GET/POST/PUT/PATCH/DELETE/HEAD/OPTIONS, Basic/Bearer auth, custom headers, TLS control, redirect following |
| `dns:` | DNS | record list, count | A/AAAA/PTR lookups, returns empty list (not error) when no records found |
| `ping:` | ICMP | status, avg latency, packet loss % | Multi-probe aggregation with per-probe timeout |
| `portcheck:` | TCP | status (open/closed/timeout), latency | Connection timing with configurable timeout |
| `sftp:` | SFTP over SSH | status, bytes transferred | Upload/download with endpoint override, environment variable expansion in paths |
| `updateenvironment:` | N/A | N/A | Persists a variable to the active environment and updates the running script context |

All network commands support:
- **Variable capture** via `into:` with command-specific suffixed derivatives (e.g., `${result}_status`, `${result}_count`, `${result}_avg`, `${result}_loss`)
- **Error handling** via step-level `on_error: continue` to suppress failures
- **Variable substitution** in all user-provided fields (`${var}` and `{{var}}`)
- **Cancellation** through linked cancellation tokens respecting both script-level and per-command timeouts

### SFTP Backend: SSH.NET

The SFTP runtime backend has been switched from Rebex SFTP to SSH.NET (`Renci.SshNet`). SFTP operations no longer depend on the Rebex SFTP package or its licensing. Endpoint resolution follows a priority chain: explicit `host`/`port`/`username`/`password` options, then host context variables from the grid, then toolbar defaults.

### Scintilla5.NET Script Editor

The command editor has been replaced with a Scintilla5.NET-powered control, providing a code-editor-grade authoring experience for YAML scripts.

**Syntax highlighting** — Eight token types with dual color palettes for light and dark themes: top-level keys, step commands, step options, variables (`${...}` / `{{...}}`), string literals, numbers, booleans/null, and comments. Highlighting is scoped to known parser keywords and re-paints only changed lines for performance.

**Context-aware autocomplete** — Suggestions adapt to structural position in the YAML document:

| Context | Trigger | Suggestions |
|---------|---------|-------------|
| Root level | Typing at indent 0 | `steps`, `vars`, `description`, `timeout`, etc. |
| Step command | After `- ` at step indent | `send`, `capture`, `set`, `http`, `ping`, `dns`, etc. |
| Step option | Indented under a command | Command-specific options (e.g., `capture`, `timeout`, `on_error` for `send`) |
| Option value | After `key: ` | Enum-like values (e.g., `continue`/`stop` for `on_error`) |
| Interpolation | Inside `${...}` or `{{...}}` | Built-in symbols, script-declared variables, grid column names |

Autocomplete commits with Enter/Tab and auto-appends `: ` after key completions. The popup is non-activating so typing is never interrupted.

**Inline diagnostics** — Real-time validation with debounced re-parsing surfaces errors (red squiggle underlines) and warnings (yellow squiggles) directly in the editor. Hover tooltips show the diagnostic message. Optional YAML hygiene warnings flag tab indentation, mixed indent styles, and duplicate keys within the same scope.

**Variable inspector tooltips** — Hovering over `${var}` or `{{column}}` tokens shows a tooltip with the resolved value from vars, environment variables, or grid preview data.

**Smart editing** — Tab/Shift+Tab indent/outdent selected lines by configurable spaces. Enter inserts context-aware indentation based on YAML structure (deeper after `:`, sibling after `-`). Blank-line preservation between steps is supported.

**Theme support** — Full dark and light mode theming for the editor, autocomplete popup, diagnostic indicators, and native scrollbars via Windows UX theme APIs.

### Command Editor Settings

A new "Command Editor" tab in Settings provides granular control over the script editor:

- **Features** — Toggle syntax highlighting, autocomplete, and auto-show-on-typing
- **Validation & Diagnostics** — Toggle inline validation, adjust debounce timing (150–2000ms), control warning visibility, enable/disable diagnostic and variable inspector tooltips, toggle YAML hygiene warnings
- **Indentation** — Choose spaces vs. tabs, set indent size (2–8), toggle smart-enter and blank-line preservation between steps

All settings persist in `config.json` under `CommandEditor` and apply immediately.

### Unified Command Map Syntax

All script commands now use a canonical map syntax where the command name is a YAML key and its options are nested underneath:

```yaml
# Canonical syntax (new default)
- send:
    command: show version
    capture: version_output
    on_error: continue

# Inline shorthand still accepted
- send: show version
```

The parser accepts both forms. All 26 bundled script samples and QA presets have been migrated to the canonical format.

### Context-Aware Preset Operations

Preset actions (duplicate, rename, delete, export) now resolve the target preset based on invocation context. Actions triggered from the context menu operate on the right-clicked item; toolbar actions operate on the active tab or tree selection. This prevents stale tree selection from causing operations to target the wrong preset.

After deleting a preset, the nearest item above the deleted entry is selected instead of clearing context.

### Execution Details Persistence

View Details metadata attached to history entries is now persisted in the configuration and restored into the history store at startup. Execution details survive application restart.

### Dialog Theming Improvements

- **Tab control styling** — Owner-drawn tab rendering with accent lines for dark and light modes
- **Themed message dialogs** — `DialogTheme.Confirm()` and `DialogTheme.ShowMessage()` provide dark-mode-aware confirmation and message dialogs with consistent fonts
- **Native scrollbar theming** — Recursive Windows UX theme application for scrollbars, checkboxes, radio buttons, combo boxes, and other native controls in dialogs
- **Dialog font propagation** — `DialogTheme.SetDialogFont()` applies fonts without triggering auto-scale relayout

### Font Settings

The Semibold font family resolution has been improved. `ResolveSemiboldFontFamily()` properly handles font names that already end with "Semibold" to prevent double-suffixing. A dedicated dialog font is now created and managed alongside other UI fonts.

### Pretty Format Removal

The Pretty Format feature (YAML reformatting via `ScriptPrettyFormatter`) has been removed along with its associated tests. The Scintilla-based editor with inline validation and smart editing replaces the need for bulk reformatting.

### Dependency Changes

| Package | Version | Purpose |
|---------|---------|---------|
| **Scintilla5.NET** | 6.1.1 | Script editor control (new) |
| **SSH.NET** | 2024.1.0 | SFTP backend, replacing Rebex for file transfers (new) |

### Script Samples

All 26 bundled script samples across bash, Cisco, Check Point, FortiGate, and generic categories have been migrated to the canonical command map syntax.

### Documentation

SCRIPTING.md has been substantially expanded with documentation for the new network commands (`http`, `dns`, `ping`, `portcheck`, `sftp`, `updateenvironment`), unified command map syntax, and updated examples throughout.

### License

An MIT license has been added to the repository.

### Test Coverage

New test suites added:

- **Editor** — `EditorTextUtilitiesTests`, `ScriptAutocompleteProviderTests`, `ScriptEditorValidationServiceTests`, `YamlSshSyntaxHighlighterTests`, `ScintillaScriptEditorControlTests`, `ScintillaScriptEditorPerformanceTests`
- **Scripting** — `CanonicalCommandMapSyntaxTests`, `ExitCommandTests`, `NetworkCommandTests`, `NetworkStepParserTests`, `ScriptDependencyAnalyzerTests`, `UpdateEnvironmentCommandTests`
- **Services** — `ConfigurationServiceCommandEditorSettingsTests`, `ConfigurationServiceExecutionDetailsTests`, `ConfigurationServiceWindowStateTests`, `EnvironmentServiceTests`
- **UI** — `SettingsDialogAppearanceTests` (expanded)

---

## Changes Since `cc99f52` (0.50.18)

### JSON Scripting Engine

A comprehensive JSON manipulation library has been added to the scripting engine, providing 20+ functions for working with structured data:

- **Object & Array Construction** — `json()` creates objects from key-value pairs or arrays from lists
- **Path-Based Access** — `json.get()`, `json.set()`, `json.delete()` operate on nested structures using dot-path notation (e.g., `data.items[0].name`)
- **Deep Merge** — `json.merge()` combines multiple objects with recursive merging
- **Introspection** — `json.type()`, `json.exists()`, `json.len()`, `json.keys()`, `json.values()`, `json.items()` for querying structure
- **Array Operations** — `json.push()`, `json.pop()`, `json.unshift()`, `json.shift()`, `json.slice()`, `json.concat()`, `json.indexOf()` for array manipulation
- **Formatting** — `json.format()` for pretty-printing or compacting JSON output

Nested dot-path assignment is now supported in `set:` commands (e.g., `obj.key.subkey = value`), with intermediate objects created automatically.

### WriteFile Format Support

`writefile:` now supports four output formats:

| Format | Description |
|--------|-------------|
| **json** | Valid JSON output with smart append-mode merging (arrays concatenate, objects deep-merge) |
| **jsonl** | JSON Lines format, one object per line with proper boundary handling on append |
| **csv** | CSV with automatic header extraction from JSON arrays of objects, proper escaping, and nested array flattening |
| **text** | Plain text (existing behavior) |

### Pre-Execution Column Validation

A new static analysis system inspects scripts before execution to identify which grid columns are referenced. If a script references columns that don't exist in the grid, a warning dialog lists the missing columns and allows the user to proceed or cancel. This prevents silent failures where column variables would resolve to empty strings.

### Command Editor Context Menu

The command text box now has a right-click context menu with:

- Standard editing operations (Cut, Copy, Paste, Select All)
- **Validate Script** — Checks script syntax before execution

### Terminal Output Improvements

- **Trailing prompt stripping** — Command output now automatically strips trailing shell prompt lines, including metadata lines from modern prompts like Starship (timestamps, context info)
- **Cleaner captured data** — Prevents prompt artifacts from appearing in variables set from command output

### Variable Syntax

`{{variable_name}}` syntax is now supported everywhere alongside the existing `${variable_name}` syntax, including in SSH session variable substitution.

### Environment Variable Expansion

File paths in `readfile:` commands now expand Windows environment variables (`%TEMP%`, `%APPDATA%`, `%USERPROFILE%`, etc.) after script variable substitution.

### Command Normalization

All preset command text is automatically normalized to Windows line endings (CRLF), regardless of source. This prevents inconsistencies when importing presets or pasting commands from different platforms.

### Host Grid Context Menu

Separators in the host grid context menu are now shown/hidden dynamically based on which actions are available, preventing empty separator lines when menu items aren't visible.

### Documentation

New "Quoting and Escaping" section added to SCRIPTING.md, documenting YAML string literal rules — when to use double quotes (for escape sequences like `\n`, `\t`) vs. single quotes (for literal backslashes and regex patterns).

### Test Coverage

New unit tests added across the scripting subsystem covering:

- PresetInfo command normalization
- Expression evaluation with parenthesized grouping
- ExtractCommand with multiple capture groups
- ReadFileCommand with environment variable expansion
- ScriptContext dynamic array indexing and nested interpolation
- SetCommand JSON construction, list operations, and interpolation
- WriteFileCommand JSONL, CSV, and append-mode behavior
- TerminalOutputProcessor ANSI handling, cursor operations, and pager artifacts
