# Changelog

## Changes Since `f8d02fa` (0.51.21)

This release pairs a ground-up visual and execution overhaul of the Flow Canvas with a substantial expansion of the YAML scripting language: universal step guards, a do-while loop, corrected loop scoping, stricter parse-time validation, and new networking/date/regex helper functions.

### Scripting: Control-Flow Ergonomics and Loop Correctness

A set of control-flow changes (tracked as OpenSpec change `update-scripting-control-flow`) makes conditional logic terser and fixes long-standing loop-scoping bugs.

**Universal `when:` guard** — A step-level `when:` condition can now be added to *any* command, not just `if`/`foreach`. The step is skipped when the condition evaluates false, removing the need to wrap a single conditional step in an `if`/`then` block (two indent levels). The guard is evaluated in `ScriptExecutor.ExecuteStepCoreAsync`, so it applies uniformly across every step type.

```yaml
# Restart only when the service is not already active — no if/then wrapper
- send:
    command: "systemctl restart nginx"
    when: nginx_state != "active"
```

**`repeat`/`until` (do-while) loop** — A new `RepeatCommand` runs its `do` block at least once and re-runs until the `until` condition becomes true (bottom-tested). This replaces the old pattern of duplicating a body before a `while` loop for "run, then poll until healthy" workflows. Both a nested form and a scalar shorthand are supported, and a `max_iterations` safety limit (default 10,000) prevents runaway loops. `break`, `continue`, `exit`, and `return` are all honored inside the body.

```yaml
# Start a service, then poll until it reports active
- repeat:
    until: state == "active"
    do:
      - send:
          command: "systemctl is-active nginx"
          into: state
      - wait: 2

# Scalar shorthand: condition inline, body in a sibling `do`
- repeat: state == "active"
  do:
    - send: { command: "systemctl is-active nginx", into: state }
```

**Loop block scoping (BREAKING)** — `foreach` and `while` now save and restore the iterator variable, removing it (or restoring its prior value) when the loop ends. Previously a loop iterator was written into the shared context and never cleaned up, silently clobbering a same-named global. **This is breaking for scripts that read a loop iterator's value after the loop has finished** — move any such read inside the loop body or capture it to a differently named variable before the loop exits.

**Flat loop metadata scalars** — Each loop now exposes additional iterator-prefixed metadata alongside the existing `<item>_index`:

| Variable | Meaning |
|----------|---------|
| `<item>_number` | One-based position (`index + 1`) |
| `<item>_first` | `true` on the first iteration |
| `<item>_last` | `true` on the last iteration |
| `<item>_count` | Total number of items |

**Dictionary iteration** — `foreach` can iterate an object's key/value pairs directly: `foreach: k, v in {{ map }}`.

**Soft-assert run summary** — Every `assert` with `severity: warning` is now tallied across the run. At completion the script reports `Soft assertions: N passed, M failed`, turning warning-level checks into a lightweight test report that does not halt execution. The aggregation lives in `ScriptContext`, with the totals surfaced by `AssertCommand`.

### Scripting: Parse-Time Validation Hardening

Validation changes (folded in from the archived OpenSpec change `update-scripting-validation`) catch typos and malformed shorthand before a script runs instead of failing mid-execution.

- **Strict typo-key validation with did-you-mean** — Unrecognized (typo-class) keys are now *blocking errors* rather than silently ignored. `ScriptParser` enriches each `Unknown <scope> key '<key>'` message with a closest-match suggestion (`Did you mean '<suggestion>'?`). Genuine deprecation notices remain warnings.
- **Misspelled-command did-you-mean** — A misspelled step root (e.g. `prnt:` instead of `print:`) is reported as the step's own `Unknown step key` error with a suggestion. The step is flagged via `ScriptStep.HasUnknownStepKey` so the generic fallback validation does not emit a redundant second error for the same line.
- **Parse-time shorthand grammar validation** — The `foreach`/`set` shorthand grammars are validated when the script is parsed, so malformed `foreach: x in ...` / `set: x = ...` expressions surface as errors up front.
- **Unified interpolation scanner** — `{{ ... }}` and `${ ... }` interpolation now route through a single balanced-brace scanner, so both syntaxes parse identically (including nested braces and function calls).

### Scripting: Networking, Date/Time, and Regex Helper Functions

A new function category and several extensions (OpenSpec change `add-scripting-helper-functions`) close gaps in a tool whose whole purpose is network automation. All additions are pure, deterministic, and non-breaking — `now()` semantics are unchanged.

**New `NetworkFunctions` category** (`Services/Scripting/Functions/NetworkFunctions.cs`):

| Function | Example | Result |
|----------|---------|--------|
| `is_valid_ip(s)` | `is_valid_ip(Host_IP)` | `true`/`false` for IPv4/IPv6 parse |
| `ip_version(s)` | `ip_version("::1")` | `4` or `6`, empty if invalid |
| `ip_in_cidr(addr, cidr)` | `ip_in_cidr(Host_IP, "10.0.0.0/8")` | `true` if the address is inside the CIDR range (family-aware, prefix-validated) |
| `url_host(url)` | `url_host("https://h:8443/p")` | `h` |
| `url_port(url)` | `url_port("https://h:8443/p")` | `8443` |

**`DateTimeFunctions` extensions** — `now_local()` and `now_utc()` make the time base explicit (fixing a latent local-vs-UTC mix between `now()` and `epoch()`); `parse_date(input, format[, outFormat])` parses a string with an explicit input format; `date_add`/`date_diff` gain `week`/`month`/`year` units.

```yaml
- set:
    expression: ts = now_utc("yyyy-MM-dd HH:mm:ss")
- set:
    expression: dt = parse_date("15-01-2026", "dd-MM-yyyy")
```

**Regex functions in `StringFunctions`** — `regex_match(s, pattern[, group])` returns the first match or a chosen capture group; `regex_match_all(s, pattern)` returns a list of all matches; `regex_groups(s, pattern)` returns the capture groups of the first match. All reuse `regex_replace`'s 5-second timeout and `/.../` delimiter handling.

```yaml
- set:
    expression: ip = regex_match(line, '/inet (\d+\.\d+\.\d+\.\d+)/', 1)
```

**Named-capture `extract`** — The `extract` command now surfaces named regex capture groups as named variables. The change is gated so positional behavior is byte-for-byte preserved when a pattern has no named groups.

### Flow Canvas: Premium Visual and Execution Overhaul

The Flow Canvas received a wave-by-wave redesign (scoped in `FLOW_CANVAS_ENHANCEMENTS.md`) that makes it read as a flagship node editor while leaving the YAML export path untouched — every visual and transient-runtime change serializes nothing new into scripts.

**OKLCH design-token foundation** — A single CSS-custom-property layer (`src/styles/tokens.css` + `src/utils/tokens.ts`) authored in OKLCH replaces ~300+ scattered hex literals across nodes, edges, and panels. `theme.ts` is slimmed and `categoryColors` now point at the token layer; a no-hex test gate guards against regressions. This unblocks coherent theming and a working light mode.

**Block icons and node redesign** — Lucide stroke icons are vendored inline as `BlockIcon.tsx` (no runtime dependency) and rendered as a category-tinted chip in each block header and Palette item, finally using the previously dead `def.icon` field. The node card was iterated from an accent-rail design to the final **neon block** treatment: a category-hued border carries block identity with an idle neon ring, and the separate accent rail was retired. A `BranchBandsLayer` renders branch membership as background bands derived from `computeBranchBands`.

**Live Wires — gradient edges with data packets** — A single universal `AnimatedEdge` renders all edges with a branch-color gradient stroke and a tokenized arrowhead (`EdgeMarkers.tsx`), replacing the old flat `#666` markerless smoothstep. Active edges carry a traveling pulse-dot packet via SVG `offset-path`, gated behind reduced-motion.

**Execution cinematics** — Live runs now animate: a running block shows a comet sweep plus a breathing halo, errors shake with a ripple, success draws an SVG checkmark (draw-in + pop), and a duration badge counts up live via `requestAnimationFrame` before settling on the measured duration. Effects are class/data-attribute driven (compositor-only) and defined in `execution-cinematics.css`.

**Loop and branch instrumentation** — `CommandResult` and `StepExecutionEventArgs` gain `IterationCount` and `BranchTaken`. `foreach`/`while`/`repeat` report body-execution counts and `if`/`switch` report the taken branch scope-key; these flow over the `execution-update` message into transient store Maps and render as a static loop/branch instrumentation badge on each block.

**Execution path highlight** — After a run, the traversed path is drawn persistently on edges (new traversed-edge token and overlay CSS), with a `pathVisible` flag reset at run start and a new **Clear Path** toolbar control. Imported preset edges (which carry no `data.branchPath`) are correlated to the path via child `_stepPath` metadata, so highlighting works for both canvas-built and imported branch edges.

**Hierarchical auto-layout** — A new in-house layout engine (`src/utils/layout/`: `types.ts`, `branchScope.ts`, `treeBuilder.ts`, `hierarchicalLayout.ts`) replaces the `dagre` dependency. Fresh imports are auto-laid-out hierarchically, the Auto-organize toolbar button drives the same engine, and a `hasUserLayout` flag is sent with `load-graph` so previously hand-positioned graphs are not reorganized. The canvas now owns positions end to end — dead C# layout math and the old `autoLayout.ts` were removed.

**Straight-spine edge routing** — Top-level blocks use a fixed 280px width and the Start node matches it, so the first edge and the main spine align as straight vertical paths; smoothstep is retained only for branch corridors.

**Run heatmap overlay** — A toggleable overlay tints blocks by execution cost, with the preference persisted through `AppConfiguration.WindowState.FlowCanvasHeatmapEnabled`. A fix populates `blockTimings` on `execution-update` so the duration badge renders during live runs (previously duration arrived on the wire but was dropped).

**Problems panel with click-to-fix** — C# now emits structured per-node diagnostics (with `NodeId`) on the apply-result instead of flattening them to strings. A new `ProblemsPanel.tsx` reads them and lets the user click a diagnostic to focus the offending node.

**Connection-validity guards** — A pure `isConnectionAllowed` predicate (`src/utils/connectionRules.ts`) backs `isValidConnection`/`onConnect` guards, with an in-canvas `ConnectionNotice` explaining rejected connections.

**Reduced-motion kill switch** — A single `.fc-reduced-motion` body class disables every animation at once. It auto-detects the OS `prefers-reduced-motion` setting, exposes a manual Toolbar toggle, and persists through `AppConfiguration.WindowState.FlowCanvasReducedMotion` (null defers to the OS preference) — making aggressive motion safe over RDP and software-GPU sessions.

### Dependency Changes

FlowCanvas (`FlowCanvas/package.json`) only; no C# package changes.

| Package | Change | Purpose |
|---------|--------|---------|
| `@dagrejs/dagre`, `@types/dagre` | Removed | Replaced by the in-house hierarchical layout engine |
| `vitest` | Added `^4.1.7` | Component/unit test runner (`npm test`) |
| `jsdom` | Added `^29.1.1` | DOM environment for vitest |
| `@testing-library/react` | Added `^16.3.2` | React component testing |
| `@testing-library/jest-dom` | Added `^6.9.1` | DOM assertion matchers |

### Documentation

- **`SCRIPTING.md`** — Documents the universal `when:` guard, `repeat`/`until` loop, loop scoping and metadata, dictionary iteration, the soft-assert summary, and the new networking/date-time/regex helper functions.
- **`FLOW_CANVAS_ENHANCEMENTS.md`** (new) — The full enhancement scope: current-state assessment, design vision (OKLCH token foundation, depth/motion principles), and a 44-proposal catalog sequenced into waves.
- **`SCRIPTING_LANGUAGE_ROADMAP.md`** and **`SCRIPTING_DEFERRED_REVIEW.md`** (new) — Forward-looking scripting language roadmap and a log of deferred review items.
- **OpenSpec** — New changes `add-scripting-helper-functions` and `update-scripting-control-flow`; `update-scripting-validation` archived with its deltas folded into the live `scripting-runtime`/`scripting-validation` specs. `docs/superpowers/` adds the design specs and implementation plans for each Flow Canvas wave.

### Test Coverage

**C# (`SSH_Helper.Tests/`):**

| Test class | Coverage |
|------------|----------|
| `Scripting/NetworkFunctionTests.cs` | `is_valid_ip`, `ip_version`, `ip_in_cidr`, `url_host`, `url_port` |
| `Scripting/DateTimeFunctionEnhancementTests.cs` | `now_local`/`now_utc`, `parse_date`, week/month/year units |
| `Scripting/RegexFunctionTests.cs` | `regex_match`, `regex_match_all`, `regex_groups` |
| `Scripting/ScriptExecutorWhenGuardTests.cs` | Universal `when:` step guard |
| `Scripting/ScriptRepeatLoopTests.cs` | `repeat`/`until` do-while semantics, break/continue, max-iterations |
| `Scripting/ScriptExecutorLoopScopingTests.cs` | Iterator save/restore and removal after loops |
| `Scripting/LoopBranchInstrumentationTests.cs` | `IterationCount`/`BranchTaken` reporting |
| `Scripting/ScriptExecutorSoftAssertTests.cs` | Soft-assert run summary aggregation |
| `Scripting/ScriptStrictKeyValidationTests.cs` | Blocking typo-key errors + did-you-mean |
| `Scripting/ScriptShorthandGrammarValidationTests.cs` | Parse-time `foreach`/`set` shorthand validation |
| `Scripting/ScriptInterpolationScannerTests.cs` | Unified `{{ }}`/`${ }` balanced-brace scanner |
| `UI/FlowCanvasFormLayoutTests.cs` | Flow Canvas layout/load-graph host wiring |
| `UI/Form1FlowCanvasReducedMotionTests.cs` | Reduced-motion preference persistence |

Existing suites extended: `ExtractCommandTests` (named captures), `ScriptParserTests`, `ScriptDependencyAnalyzerTests`, `SetCommandTests`, and `Services/FlowCanvasBridgeTests`.

**Flow Canvas (`FlowCanvas/`):** A new vitest + jsdom + Testing Library harness (`npm test`) covers `AnimatedEdge`, `BaseBlock`, `nodeStyle`, the edge-path selector, the `pathVisible` execution slice, and the layout engine (`branchScope`, `treeBuilder`, `hierarchicalLayout`). Fourteen new Playwright e2e specs cover auto-layout, block icons, branch bands, connection guards, edge geometry, execution cinematics, the execution path, live wires, loop/branch instrumentation, the node redesign, the Problems panel, reduced motion, run timing, and a token sweep that fails on stray hex.

---

## Changes Since `28cbf8c` (0.51.19)

### `readfile` Path Capture Modes

`readfile` now exposes the resolved absolute file path to subsequent steps. Two new modes cover the most common picker-driven workflows:

- **Companion variable (normal read mode)** — After a successful file read, the runtime automatically stores the resolved absolute path in `<into>_path` unless `path_into` is given an explicit name. Scripts can reference the path immediately in the next step with no additional configuration.
- **Path-only mode** — Setting `path_only: true` validates and resolves the path but skips reading file contents entirely. Useful when a later `localcmd` or `set` step needs the path string rather than the parsed lines.

```yaml
# Read a file and capture the resolved path alongside contents
- readfile:
    select_file: true
    fileext: "txt"
    into: selected_hosts
    path_into: selected_hosts_file   # Optional; defaults to selected_hosts_path

- print:
    message: "Loaded ${selected_hosts.length} entries from ${selected_hosts_file}"

# Capture only the selected path for a later PowerShell step
- readfile:
    select_file: true
    fileext: "txt"
    message: "Choose the text file to inspect."
    path_only: true
    path_into: selected_path

- set:
    expression: selected_path_ps = replace(selected_path, "'", "''")

- localcmd:
    command: "Get-Content -LiteralPath '${selected_path_ps}'"
    into: file_lines
```

**New `readfile` properties:**

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `path_only` | No | `false` | Capture only the resolved absolute path and skip reading file contents |
| `path_into` | No, but required when `path_only: true` | `<into>_path` in normal read mode | Variable name to store the resolved absolute path |
| `autobrowse` | No | `true` when `select_file` and `path_only` are both `true`; otherwise `false` | When `select_file: true`, open the native browse dialog immediately and skip the intermediate custom path-entry form |

**Runtime behavior:**

- When `path_into` is omitted in normal read mode, the companion variable is named `<into>_path`. When it is given the same name as `into`, the parser reports an error.
- In `path_only` picker mode, `autobrowse` defaults to `true` so the native file browser opens immediately. Set `autobrowse: false` explicitly to keep the custom path-entry form.
- Picker cancellation in `path_only` mode sets the `path_into` variable to an empty string and exits with `Cancelled` status. `on_error: continue` does not suppress picker cancellation.
- The blocked-path security check applies in both modes — `path_only` still validates that the resolved path is not inside `C:\Windows`, `C:\Program Files`, or other user directories.
- Scheduled-job and Job List `Run Now` executions reject `readfile` steps with `select_file: true` in `path_only` mode with the same `ManualOnlySelectionMessage` as ordinary `select_file` steps.

**`ScriptDependencyAnalyzer`** — `ResolveReadfilePathOutputVariable` mirrors the runtime logic: `path_into` when set, otherwise `<into>_path` in normal mode, `null` in `path_only` mode. The resolved variable name is added to `definedVars` so no false-positive "undefined variable" warnings appear when downstream steps reference the path variable.

**`ScriptParser`** — `autobrowse`, `path_into`, and `path_only` added to the allowed `readfile` option key list. Validation emits `"Readfile with 'path_only' requires 'path_into'"` when `path_only: true` is set without a `path_into` value.

**Flow Canvas parity:**

- `FlowCanvas/src/blockDefs/registry.ts` adds `autobrowse`, `path_only`, and `path_into` to the `readfile` block. `autobrowse` includes a helpText noting it defaults to `true` in `select_file + path_only` mode.
- `FlowCanvas/src/panels/Properties.tsx` computes the displayed `autobrowse` value dynamically — when `autobrowse` has no explicit value and both `select_file` and `path_only` are `true`, the Properties panel shows `true` as the effective default. The required-field logic is extended: `into` is no longer required when `path_only: true`; `path_into` becomes required in that same case; `path` is not required when `select_file: true`.
- `FlowCanvasBridge` exports `path_into` (string), `autobrowse` (nullable bool), and `path_only` (bool, omitted when `false`) into the YAML block and reads all three back on import. The `readfile` canonical key list in `ExportKeyOrder` is updated to include `path_only` and `path_into` in their documented order.

### Flow Canvas Omits Default Property Values on Export

When the canvas sends the executable graph payload to the host (Apply YAML, Run, test-step, debug), property values that exactly match the block definition's `defaultValue` are now stripped from each node's `props`. Only properties with explicitly non-default values, or properties with no declared default, are included.

**`stripDefaultProps(node)`** (`FlowCanvas/src/utils/exportGraph.ts`) — Iterates `def.properties` for the node's `blockType`, and for each `PropertyDef` that has a `defaultValue`, removes the matching key from `props` when the current value equals the default. Deep equality is handled by `areEquivalentValues`, which covers scalars, arrays with element-wise comparison, and plain objects with key-set and value comparison. The function returns the original node reference unchanged when no keys are stripped, and a new node with a shallow-copied `data.props` when at least one key is removed.

`buildExecutableGraphPayload` calls `stripDefaultProps` for every non-comment export node. Comment nodes are extracted to the `comments` list as before and are unaffected.

**Example** — A `send` node configured with only `command: "show version"` (all other fields at their defaults: `suppress: false`, `retry: 0`, `retry_delay: 1`, `fail_on_nonzero: false`, `on_error: "stop"`) now exports:

```json
{ "command": "show version" }
```

instead of the full properties object with every default repeated.

### Test Coverage

| Test class | Coverage |
|------------|----------|
| `SSH_Helper.Tests/Scripting/ReadFileCommandTests.cs` | `ExecuteAsync_SelectFileTrue_PathOnly_CapturesResolvedPathWithoutReadingContents` — `path_only` mode resolves path, skips file read, and stores the absolute path in `path_into`; `ExecuteAsync_SelectFileTrue_AutoBrowse_PassesFlagToPrompt` — explicit `autobrowse: true` forwards `AutoBrowse = true` to the prompt request; `ExecuteAsync_SelectFileTrue_PathOnly_ImpliedAutoBrowse_PassesFlagToPrompt` — `path_only + select_file` with no explicit `autobrowse` defaults to `true`; `ExecuteAsync_SelectFileTrue_PathOnly_AutoBrowseFalse_PassesFalseFlagToPrompt` — explicit `autobrowse: false` overrides the default; `ExecuteAsync_SelectFileTrue_PathOnly_UsesNativeFileDialogByDefault` — `autobrowse = true` routes through the native `OpenFileDialog` code path; `ExecuteAsync_SelectFileCancelled_PathOnly_ClearsPathVariableAndReturnsCancelledExit` — cancellation in `path_only` mode clears the path variable and returns `Cancelled`. |
| `SSH_Helper.Tests/Scripting/ScriptParserTests.cs` | `Parse_ReadfilePathOnly_WithPathInto_ParsesAndValidates` — `path_only` and `path_into` round-trip correctly; `Parse_ReadfileAutoBrowse_ParsesAndValidates` — `autobrowse: true` parses to `AutoBrowse = true`; `Parse_ReadfilePathOnlyWithoutAutoBrowse_LeavesAutoBrowseUnset` — omitting `autobrowse` leaves `AutoBrowse` null. |
| `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs` | `AnalyzePresets_ReadfilePathOnlyOutput_IsNotReportedAsMissingColumn` — `path_into` in `path_only` mode registers as a defined variable; `AnalyzePresets_ReadfileImplicitPathOutput_IsNotReportedAsMissingColumn` — implicit `<into>_path` companion variable is registered as defined. |
| `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs` | `readfile` option-key completion now contains `autobrowse`, `path_into`, and `path_only`. |
| `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` | `ExportGraphToYaml_ReadfilePathOnlyWithPathInto_ExportsSuccessfully` — `path_only: true` and `path_into` export to YAML; `ExportGraphToYaml_ReadfileAutoBrowse_ExportsSuccessfully` and `ExportGraphToYaml_ReadfileAutoBrowseFalse_ExportsSuccessfully` — `autobrowse` round-trips for both `true` and `false`; `TextToGraph_ReadfilePathOnly_ImportsPathCaptureProps` — YAML with `path_only` and `path_into` imports into the correct block props. |
| `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs` | `RunNowAsync_CustomPresetReadfilePathOnlySelectFile_FailsWithoutPrompt` — `path_only + select_file` in a non-interactive run fails with the manual-only message. |
| `FlowCanvas/e2e/flow-canvas-interactions.spec.ts` | `apply yaml payload omits schema default props` — `send` and `interactive` nodes with default-valued props export only the non-default properties in the Apply YAML message. |

### Documentation

`SCRIPTING.md` updated with:

- `readfile` reference updated with `autobrowse`, `path_only`, and `path_into` in the YAML skeleton, the parameter table, and the notes section.
- New examples: reading a file and capturing the path into a companion variable; using `path_only` with a `localcmd` to pass the selected path to a PowerShell `Get-Content` command.
- Notes clarify `autobrowse` default behavior per mode, picker-cancellation variable reset semantics, and `path_only` security validation.

---

## Changes Since `7f349e7` (0.51.18)

### `notify` SMTP Email Attachments

The `notify` command gains an optional `attachments:` field for SMTP/email channels. Each entry is a file path that goes through normal variable substitution and is attached to the outgoing message.

```yaml
- notify:
    profile: ops-mail
    title: "Compliance scan"
    message: "Found {{ violations }} violations on {{ now() }}"
    level: error
    attachments:
      - "C:\\reports\\{{ Host_IP }}-compliance.txt"
      - "C:\\reports\\summary.csv"
    into: mail_result
    on_error: continue
```

**Channel scope** — Attachments are only honored when the resolved channel is SMTP. Slack, Teams, Discord, and toast accept the field for round-trip safety but discard the list. A test that points `channel: toast` at a deliberately missing file still succeeds, confirming attachments are never opened for non-SMTP channels.

**Path resolution** — `NotifyCommand` calls `context.SubstituteVariables(raw)` on every entry and skips empty/whitespace-only entries after substitution. The substituted list is forwarded into a new `NotificationService.SendAsync` overload (the existing seven-argument overload now delegates to it with `attachments: null`).

**`SmtpDispatcher.SendAsync(profile, password, title, message, level, attachments, cancellationToken)`** — New attachment-aware overload (`Services/Notifications/SmtpDispatcher.cs`):

- Trims each path; empty entries are silently skipped.
- Missing file (`File.Exists == false`) → returns `NotificationResult.Failure("smtp", "SMTP attachment not found: <path>")` before any SMTP connection is opened.
- I/O error opening the stream → returns `NotificationResult.Failure("smtp", "SMTP attachment '<path>' could not be read: <ex.Message>")`.
- On success each file is added as an `Attachment` over a `FileStream(FileMode.Open, FileAccess.Read, FileShare.Read)` and `Path.GetFileName(path)` becomes the displayed filename. The legacy six-argument overload is preserved and forwards to the new one with `attachments: null`.

Attachment failures follow normal `on_error` handling — `stop` (default) aborts the script and `continue` lets the run proceed after recording the error into the `<into>.error` slot.

**Parser** — `ScriptParser` registers `attachments` in the `notify` option key list and parses it via `ParseStringList(parser)`. `NotifyOptions.Attachments` is added as a nullable `List<string>` on `ScriptStep` (`Services/Scripting/Models/ScriptStep.cs`).

**Flow Canvas** — `FlowCanvas/src/blockDefs/registry.ts` adds an `attachments` `textarea` field to the `notify` block (placeholder `["C:\\reports\\summary.csv"]`, helpText calls out SMTP-only behavior). `FlowCanvasBridge` lists `attachments` in `ArrayOptionKeys` and exports it as a `JArray` when non-empty so import/export round-trips preserve the list.

**Autocomplete** — `ScriptAutocompleteProvider` includes `attachments` in the suggested option keys under `notify:`.

### Toast Channel Level Attribution Becomes Opt-In

Toast notifications no longer append a level attribution line ("Info" / "Warning" / "Error" / "Success") unless the script explicitly sets `level:`. Other channels are unaffected — webhook colors, Teams title styling, and SMTP subject prefixes still default to `info` when `level:` is omitted.

**`NotifyOptions.Level`** (`Services/Scripting/Models/ScriptStep.cs`) is now `string?` with a `null` default instead of `string = "info"`. `NotifyCommand.ExecuteAsync` distinguishes the two cases:

```csharp
var resolvedLevel = options.Level == null ? null : context.SubstituteVariables(options.Level);
var levelRaw = string.IsNullOrWhiteSpace(resolvedLevel) ? "info" : resolvedLevel;
var includeToastLevelAttribution = !string.IsNullOrWhiteSpace(resolvedLevel);
```

The level enum still parses through `TryParseLevel(levelRaw, out var level)`, so non-toast channels see `NotificationLevel.Info` and their level-mapped styling is unchanged.

**`NotificationService.SendAsync`** gains an `includeToastLevelAttribution` parameter (defaults to `true` for backward compatibility) that is forwarded into `ToastDispatcher.SendAsync`. `ToastDispatcher` wraps the existing `builder.AddAttributionText(...)` block in an `if (includeLevelAttribution)` guard. The attribution-text mapping itself is unchanged.

**Flow Canvas** — `FlowCanvas/src/blockDefs/registry.ts` drops `defaultValue: 'info'` from the `level` select on the `notify` block so the dropdown can be left empty and that state round-trips to YAML without injecting an unwanted `level: info`.

### Host Grid Special Column Indicators

Columns in the host grid that drive built-in SSH connection behavior now render a `*` marker after the header text and expose a hover tooltip explaining what the column does. Renaming a column away from a special name (or to one) updates the marker and tooltip on the fly.

**Recognized columns and tooltips** (`Form1.SpecialHostGridColumnTooltips`):

| Column | Tooltip |
|--------|---------|
| `Host_IP` | Host IP/DNS/custom |
| `port` | Optional per-host SSH port. Host_IP with explicit :port overrides this value. |
| `username` | Optional per-host SSH username override. |
| `password` | Optional per-host SSH password override. |
| `vault_path` | Optional Vault credential path for per-host username/password resolution. |

`ApplySpecialHostGridColumnDecoration` runs in three places: once at startup over every column, in `Dgv_Variables_ColumnAdded` for new columns, and at the end of column rename. Non-special columns get their tooltip cleared so renaming a special column back to a generic name removes both the marker and the tooltip.

**Marker rendering** — Implemented in `Dgv_Variables_CellPainting` for header cells (`e.RowIndex == -1 && e.ColumnIndex >= 0`). The handler calls `e.Paint(e.CellBounds, DataGridViewPaintParts.All)` for the standard header, measures the header text with `TextRenderer.MeasureText`, then draws `*` in a 12-wide bounds positioned `markerTextGap = 4` pixels to the right of the text. The marker color tracks the column header's `ForeColor` (falling back to the grid `ForeColor`) so it follows the active dark/light theme.

`dgv_variables.ShowCellToolTips = true` is enabled at startup so the header tooltip surfaces on hover.

### Host Grid Port Column Now Honored in Manual Runs

`Form1.GetHostConnections` now reads the `port` column when building `HostConnection` for manual runs. Previously the manual-run path used only the port parsed from `Host_IP`; the column was ignored. Scheduler runs (`JobExecutionService.BuildHostConnections`) already consumed the column but did not gate it on the presence of an explicit `:port` in `Host_IP`.

**Unified precedence** — Both code paths now route through a `TryGetExplicitPortFromHostValue(hostValue, out int port)` helper. If `Host_IP` ends with `:<port>` and the digits parse to a valid port (1-65535), that port wins and the `port` column is skipped. Otherwise the row's `port` column is consulted; invalid values fall back to the default port (`22`).

| `Host_IP` | `port` column | Effective port |
|-----------|----------------|----------------|
| `192.0.2.10` | `2222` | `2222` |
| `192.0.2.10:2022` | `2222` | `2022` |
| `192.0.2.10` | `abc` | `22` |
| `192.0.2.10` | (empty) | `22` |

### Host Grid Insert Row Context Menu

The host grid right-click menu gains an **Insert Row** item that inserts a blank row directly above the right-clicked row.

**`Form1.InsertRow(int rowIndex)`** validates the index, calls `dgv_variables.Rows.Insert(rowIndex, 1)`, selects the inserted row, sets `CurrentCell` to the first cell, marks `_csvDirty = true`, and refreshes the host count via `UpdateHostCount()`. The new menu item (`insertRowToolStripMenuItem`) is hidden when the right-click hits a column header and shown otherwise; the separator/visibility logic in `UpdateHostGridContextMenuSeparators` treats Insert Row alongside Delete Row when deciding whether the row-action separator should appear.

### Host Grid Row Header Widens Past 1000 Rows

The row header has a fixed designer width of 50px, but the row number is custom-drawn via `Dgv_Variables_RowPostPaint`. Once row count crossed 1000 the 4-digit number no longer fit and the default `StringFormat` wrapped it onto a second line.

**`Form1.EnsureRowHeaderWidthFitsRowCount`** is called after every bulk row insert. It measures `new string('9', digitCount)` against the row-header font with `TextRenderer.MeasureText`, computes `HostGridRowHeaderGlyphReservationWidth + textWidth + 12`, and only ever grows the width (never shrinks) so flipping between large and small datasets does not make the header jitter. The handler also flips `RowHeadersWidthSizeMode` from `AutoSizeToAllHeaders` to `EnableResizing` because the auto-size mode only measures literal header text content and would otherwise immediately reset the manual width. The custom paint handler now also passes `StringFormatFlags.NoWrap` so the row number never wraps even if the column happens to be narrower than the digits.

### Host Grid Paste Performance for Large Datasets

`PasteFromClipboard` is rewritten to scale to multi-thousand-row pastes. The previous loop grew the grid one row at a time and paid for per-cell `DataGridView` bookkeeping on every assignment; a 2000-row paste turned into O(rows²) autosize work.

**Pre-parse and pre-allocate** — The clipboard text is parsed into a `string[][] parsed` and `maxCols` is computed before any grid mutation. Missing columns are added in a single up-front loop. Per-column `ReadOnly` flags are cached into a `bool[]` so the inner cell loop avoids the `Columns[]` indexer and the `ReadOnly` property read.

**Off-grid row build** — Rows that extend past the current row count are built off-grid (`(DataGridViewRow)dgv_variables.RowTemplate.Clone()`, `CreateCells(dgv_variables)`, `Cells[idx].Value = …`) and then added in a single `dgv_variables.Rows.AddRange(newRows)` call. Setting `Value` on an off-grid cell skips all the per-cell `DataGridView` bookkeeping that an attached cell triggers — this is the dominant speedup for large pastes.

**Suppress paint and side effects during the paste:**

- `NativeMethods.SendMessage(gridHandle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero)` halts repaints; restored with `new IntPtr(1)` and a final `Invalidate()` in `finally`.
- `AutoSizeColumnsMode` is parked at `None` for the duration; the original mode is restored at the end so a single resize pass happens after the paste settles.
- `CellValueChanged` is unhooked so the per-cell handler does not dirty state, request refreshes, and re-style Host_IP rows once per cell. The handler is reattached in `finally`.
- The grid cursor is forced to `Cursors.WaitCursor` for visual feedback and restored after the paste completes.

**Connection-test visual state cleanup** — Side effects that the detached handler would have applied are now applied once after the paste. The overwrite range (existing rows that received new values) is walked, and any row that had a `_connectionTestRowStates` entry has its Host_IP visual state cleared via `ClearConnectionTestVisualState`. Newly-added rows are skipped — they cannot have had a prior connection-test state.

### Build Environment Pinned to `windows-2022`

`.github/workflows/build-release.yml` pins the `build` job from `windows-latest` to `windows-2022` so the release pipeline stays on the runner image that the project has been validated against. The `flowcanvas-browser-tests` job is unchanged.

### Test Coverage

| Test class | Coverage |
|------------|----------|
| `SSH_Helper.Tests/Scripting/NotifyCommandTests.cs` | Extended for: `attachments` resolved through `context.SubstituteVariables` and forwarded to the SMTP dispatcher in declaration order; toast channel ignores `attachments` and never opens missing files; SMTP path with no `attachments:` forwards an empty list. Plus new `ToastChannel_WithoutLevel_DoesNotRequestAttribution` asserting `includeLevelAttribution = false` when `level:` is omitted. |
| `SSH_Helper.Tests/Scripting/ScriptParserTests.cs` | `Parse_NotifyStep_Attachments_ParsesCorrectly` — `attachments:` parses as an ordered string list. |
| `SSH_Helper.Tests/Services/NotificationServiceTests.cs` | `SmtpChannel_ForwardsAttachmentsToDispatcher` — list passes through `NotificationService.SendAsync` to the dispatcher; `SmtpChannel_MissingAttachment_ReturnsFailureBeforeSend` — missing file returns `Failure("smtp", …)` that contains the offending path before any SMTP connection is opened. |
| `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs` | `notify` option-key completion now contains `attachments`. |
| `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` | `notify` round-trip exports `attachments` as a JSON array with preserved order; registry block exposes the `attachments` field in its property set. |
| `SSH_Helper.Tests/Services/SshExecutionServiceOutputWindowTests.cs` | Test toast dispatcher signature updated to match the new `includeLevelAttribution` parameter on `ToastDispatcher.SendAsync`. |
| `SSH_Helper.Tests/Services/JobExecutionServiceTests.cs` | `BuildHostConnections_UsesPortColumnWhenHostIpHasNoExplicitPort` and `BuildHostConnections_WhenHostIpHasExplicitPort_OverridesPortColumn` cover the new precedence ordering between `Host_IP:port` and the `port` column. |
| `SSH_Helper.Tests/UI/Form1ConnectionTestStatusTests.cs` | `GetHostConnections_WhenHostIpHasNoExplicitPort_UsesPortColumn`, `GetHostConnections_WhenHostIpHasExplicitPort_OverridesPortColumn`, `GetHostConnections_WhenPortColumnInvalid_FallsBackToDefaultPort` — manual-run port resolution mirrors the scheduler path. |

### Documentation

`SCRIPTING.md` updated with:

- `notify` reference adds `attachments: [<path>, …]` to the YAML skeleton, a setup-rules bullet noting that the field is SMTP-only and that missing/unreadable files fail the step under normal `on_error` handling, and an updated SMTP example that demonstrates `{{ Host_IP }}` substitution inside an attachment path.
- **Special Grid Columns** table cleaned up. The legacy `delay`, `timeout`, `transport`, and `personality` columns are removed; the table now lists only the columns with implemented connection semantics (`Host_IP`, `port`, `username`, `password`, `vault_path`).

`CLAUDE.md` and `.planning/codebase/INTEGRATIONS.md` synced to the same column list so onboarding docs match SCRIPTING.md.

---

## Changes Since `2fc99ed` (0.51.17)

### `notify` Scripting Command

A new `notify` command dispatches a single message to Slack, Microsoft Teams, Discord, a Windows desktop toast, or SMTP email. Secrets (webhook URLs, SMTP passwords) are stored in Windows Credential Manager; per-channel payload shape, color, and mention handling are selected automatically from the resolved profile or `channel:` override.

```yaml
- notify:
    profile: ops-alerts          # Optional. Channel inferred from profile.Kind.
    channel: <channel>           # Optional override: slack | teams | discord | toast | smtp
    title: "Backup complete"
    message: "{{ hosts_ok }}/{{ hosts_total }} hosts succeeded"
    level: success               # info (default) | warn | error | success
    mention:
      - "upn:alice@contoso.com|Alice"
    into: result
    on_error: continue           # stop (default) | continue
```

**Channel / profile resolution** — Implemented in `NotificationService.SendAsync` (`Services/Notifications/NotificationService.cs`):

| `profile:` | `channel:` | Behavior |
|------------|------------|----------|
| set | unset | Channel inferred from `profile.Kind`. |
| set | set | Override must match `profile.Kind`; mismatch returns `"Channel 'x' does not match profile 'y' kind 'z'"`. |
| unset | `toast` | No profile needed; dispatched via `ToastDispatcher`. |
| unset | unset | Falls back to `NotificationSettings.DefaultProfileName`; fails if unset. |
| unset | webhook/smtp | Fails — those channels require a profile. |

Toast is the only channel that works with `NotificationSettings.Enabled = false`; all other channels are gated on the Notifications settings being enabled.

**Channel dispatchers** — Separate classes under `Services/Notifications/`:

- `WebhookDispatcher` (Slack / Teams / Discord) — POSTs JSON to an incoming webhook.
  - Slack: colored `attachment` block, member-ID mention normalization (`U12345678` → `<@U12345678>`, `here` → `<!here>`, `channel` → `<!channel>`, `everyone` → `<!everyone>`).
  - Discord: embed with `color` int, plus `content` line built from normalized mentions (`user:ID` → `<@ID>`, `role:ID` → `<@&ID>`, `channel:ID` → `<#ID>`, `here`, `everyone`). Existing raw mention markup (`<@123>`, `<@&123>`, `<#123>`) passes through unchanged.
  - Teams: full Adaptive Card envelope (`application/vnd.microsoft.card.adaptive`, schema version `1.2`) with `msteams.entities` for typed mentions — see below.
- `ToastDispatcher` — Windows 10/11 toast via `Microsoft.Toolkit.Uwp.Notifications.ToastContentBuilder`. Adds a level-based attribution line ("Info", "Warning", "Error", "Success").
- `SmtpDispatcher` — `System.Net.Mail.SmtpClient` with optional STARTTLS (`UseStartTls`) and level-prefixed subject (`[INFO]`, `[WARN]`, `[ERROR]`, `[OK]`).

**Teams Adaptive Cards with typed mentions** — `TeamsAdaptiveCardPayloadBuilder` replaces the legacy MessageCard JSON. Typed mention strings:

| Form | Meaning | Entity written |
|------|---------|----------------|
| `upn:alice@contoso.com\|Alice` | User by Microsoft Entra UPN | `<at>Alice</at>` → `mentioned.id = alice@contoso.com` |
| `entra:87d349ed-...|Adele` | User by Entra Object ID (GUID validation) | `<at>Adele</at>` → `mentioned.id = 87d349ed-...` |

If the `|display` segment is omitted, the identifier itself becomes the visible label. Invalid typed strings (e.g. missing `@` in a UPN, non-GUID Entra ID, bare `@Bob`) are emitted as literal text and surface a runtime warning via `context.EmitOutput(..., ScriptOutputType.Warning)` instead of failing the step.

**Level → channel-native styling:**

| Level | Slack color | Teams title color | Discord embed | SMTP subject prefix |
|-------|-------------|-------------------|---------------|---------------------|
| `info` | `#2196F3` | `Accent` | `3447003` | `[INFO]` |
| `warn` | `#FFC107` | `Warning` | `16776960` | `[WARN]` |
| `error` | `#F44336` | `Attention` | `15158332` | `[ERROR]` |
| `success` | `#4CAF50` | `Good` | `3066993` | `[OK]` |

**`into:` capture** — Stores four script variables:

- `<name>.sent` — `"true"` / `"false"`
- `<name>.channel` — `"slack"`, `"teams"`, `"discord"`, `"toast"`, or `"smtp"`
- `<name>.status_code` — webhook HTTP status (absent for toast/smtp)
- `<name>.error` — error message when `sent` is false

**Parser** — `ScriptParser` registers `notify` in the known-command list with option keys `profile, channel, title, message, level, mention, into, on_error`. `NotifyCommand` is added to the set of commands that participate in YAML flow analysis. A new `NotificationSettings` model (`Models/NotificationSettings.cs`) on `AppConfiguration` holds `Enabled`, `Profiles` (list of `NotificationProfile` with Name/Kind/DefaultTitle/SMTP fields), and `DefaultProfileName`. Secrets are keyed via `CredentialTargets` (`NotifyWebhookTarget`, `NotifySmtpPasswordTarget`).

**Flow Canvas block** — `FlowCanvas/src/blockDefs/registry.ts` registers the `notify` block in the `io` category with properties `profile, channel, title, message, level, mention, into, on_error`.

### `sethistorylabel` Scripting Command

A new command attaches a label to the current host's history entry, enabling friendlier identifiers than raw IPs in the run history dashboard. Supports scalar shorthand and an options object.

```yaml
# Scalar form - replaces the label
- sethistorylabel: "Core Router"

# Options form - compose an existing label
- sethistorylabel:
    value: "{{ site_code }}"
    mode: append          # replace (default) | append | prepend | clear
    separator: " / "
    replace: true          # When true, history shows only the label (hides IP)
```

**`HistoryLabelOperation`** (`Services/Scripting/Models/HistoryLabelOperation.cs`) — Encapsulates a single mutation so it can be replayed deterministically across parallel preset runs:

- `Mode` normalized through `NormalizeMode` to one of `replace` / `append` / `prepend` / `clear`. Unknown values fall back to `replace`.
- `ApplyTo(ref label, ref replacesAddress)` — `clear` or empty `Value` nulls the label and clears `replacesAddress`. `append` / `prepend` compose via `Separator`. `replace` also writes `ReplaceAddress ?? false` into `replacesAddress`; compose modes only touch `replacesAddress` when `ReplaceAddress` is explicitly set.

**Multi-preset aggregation** — `SshExecutionService` now threads a `finalContext` out of each preset run and accumulates operations in a shared list. When multiple presets target the same host:

- **Sequential**: `ApplyHistoryLabelResults` folds each preset's operations into the aggregate `ExecutionResult` in preset-order.
- **Parallel**: Preset results are captured into a position-indexed `ExecutionResult?[]` during `Task.WhenAll`; aggregation then replays operations in their original preset order so the final label is deterministic regardless of completion order.

**History plumbing** — `ExecutionResult` gains `HistoryLabel`, `HistoryLabelReplacesAddress`, `HistoryLabelTouched`, and `HistoryLabelOperations` (`Models/ExecutionResult.cs`). `JobHostOutput` gains `Label` and `LabelReplacesAddress` so scheduled job runs persist and display the same label. `HistoryStorageService` and `JobHistoryService` carry the new fields through serialization; `RunOutputViewerDialog` reads `LabelReplacesAddress` to decide between `"IP"`, `"IP - Label"`, and `"Label"` display.

**Autocomplete and Flow Canvas** — `ScriptAutocompleteProvider` suggests `value, replace, mode, separator` under `sethistorylabel`, plus enum-like values `replace | append | prepend | clear` for `mode:`. `FlowCanvasBridge` exports both scalar and options forms and registers a `Set History Label` block in the `data` category (36 total commands).

### `${_outputwindow}` Built-in Variable

A new built-in variable returns the pane-formatted transcript accumulated for the current host so far.

```yaml
- notify:
    channel: toast
    title: "Run summary"
    message: "${_outputwindow}"
    level: info
```

**`ScriptContext.OutputWindowText`** (`Services/Scripting/ScriptContext.cs`) — Reads from a new `StringBuilder` inside `SharedScriptExecutionState.OutputWindow`. Writes happen via `SetOutputWindowText` / `AppendOutputWindowText`, which are invoked by the per-host output relay in `SshExecutionService` after the relay is attached. The variable is:

- **Host-scoped** during multi-host runs — multi-host aggregations never contaminate each other.
- **Empty before relay attach** — local-only scripts with no SSH session still receive any output produced after relay wiring.
- Accessible via both `${_outputwindow}` direct replacement and `context.GetVariable("_outputwindow")`. Added to `HasVariable` and variable-snapshot helpers so condition checks (`is defined`) and logging surfaces see it.
- Recognized by `ScriptAutocompleteProvider` and treated as a positive reference in `ScriptDependencyAnalyzer` so preflight warnings never flag it as missing.

### Script Prompt Font Size

Interactive prompts (`input`, `choose`, `multiselect`, `confirm`) now honor a `font_size:` override that scales the dialog text without resizing the rest of the app.

```yaml
- input:
    prompt: "Enter password"
    into: pw
    password: true
    font_size: 14
```

**`AppConfiguration.FontSettings.ScriptPromptFontSize`** (new, default `9f`) provides the baseline when a step omits `font_size:`. Each prompt options class (`InputOptions`, `ChooseOptions`, `MultiselectOptions`, `ConfirmOptions`) gains a nullable `FontSize` (points). `ScriptPromptDialogRunner` resolves the effective size per prompt and applies it to the constructed WinForms dialog. `SettingsDialog` exposes a matching "Script prompt font size" spinner under the Fonts tab.

**Flow Canvas** — `FlowCanvas/src/blockDefs/registry.ts` inserts `font_size` into the properties panel for the four prompt blocks; `FlowCanvasBridge` serializes it via `SetIfDouble` so import/export round-trips preserve the per-step value.

### Preset Folder Subtree Export

A new context menu action "Export Folder..." exports a selected folder and every descendant preset as a standalone JSON bundle, rebased so the selected folder becomes the bundle root.

**`PresetManager.ExportFolderSubtreeToFile(string folderPath, string filePath)`** — Enumerates presets whose `Folder` equals `folderPath` or starts with `folderPath + "/"`, clones each preset, and calls `RebaseFolderPathForExport(originalPath, sourceRoot, exportRoot)` (via `FolderPathUtility.RenamePath`) so bundled folder paths strip the ancestor chain. Emits the standard `{ version: 2, exportDate, presets, folders }` envelope.

**Form1** — `ExportFolder(bool preferContextSource)` resolves the target folder from the context menu, active preset tree, or last-selected folder; opens a `SaveFileDialog` seeded with `{FolderName}_presets.json`; and displays a post-export summary showing the bundled preset count from `PresetManager.CountPresetsInFolderAndDescendants`. The context menu shows `ctxExportFolder` only when the right-click target is a folder node.

### Preset Deletion Confirmation

Deleting a preset now surfaces a Yes/No prompt (`"Are you sure you want to delete the preset '<name>'?"` with `MessageBoxIcon.Warning`) through the shared `ShowPromptDialog` helper. Cancelling aborts the delete before any tree mutation, history-label scrubbing, or undo bookkeeping runs.

### Flow Canvas Moved to Top-Level Menu

Flow Canvas is promoted out of the Edit submenu into its own top-level menu item (`"Flow Canvas"`, `Name = "_menuFlowCanvas"`) inserted immediately before the Help menu. The existing `Ctrl+Shift+F` shortcut is preserved. The old separator + Edit submenu entry is removed.

### Commands Editor Comment / Uncomment

The script editor right-click menu gains **"Comment Selected Lines"** and **"Uncomment Selected Lines"** (`ctxCommentSelectedLines` / `ctxUncommentSelectedLines`).

**`ScintillaScriptEditorControl.CommentSelectedLines` / `UncommentSelectedLines`** — Both route through a shared `TransformSelectedLines(Func<string,string>)` helper that:

- Expands the selection to full lines by resolving `startLine` from `SelectionStart` and `endLine` from `SelectionStart + SelectionLength - 1`.
- Preserves each line's original line ending (`\r\n` vs `\n` vs none) via `SplitLineEnding`.
- Inserts or removes a single `#` at the first non-whitespace position so indentation is never disturbed. Uncomment also strips a single trailing space after the `#` (the common "`# `" form).
- Skips whitespace-only lines to avoid polluting blank separators.
- Runs inside `BeginUndoAction` / `EndUndoAction` for single-undo toggle, and suspends `_suppressTextProcessing` so autocomplete and diagnostics do not fire on the in-progress edit.

### Quote-Aware YAML Comment Highlighting

`YamlSshSyntaxHighlighter` no longer treats `#` inside quoted strings as a comment start.

**`FindCommentStartIndex`** scans each line left-to-right tracking `inSingleQuote` / `inDoubleQuote` state. Double-quote state flips on unescaped `"` (escape via `IsEscapedByBackslash`); single-quote state flips on `'` and is immune to backslash escapes (YAML single-quoted scalars don't process escapes). Only `#` outside any quote starts a comment. The entire line is then split at `commentStart`, and keyword / option / variable / string / number / boolean matchers run against the pre-comment prefix only. Post-comment text is emitted as a single `Comment` span.

### SSH Output Banner Side-Padding Reduced

Script output banner side padding drops from twenty `#` characters to ten on each side (`Services/SshExecutionService.cs` `BannerSidePadding = 10`). Affects the script-connected-to-host, SCRIPT, LOCAL SCRIPT, CONNECTED TO, and error-title banners. The horizontal separator still matches header length, so framing stays symmetric.

### Busy Cursor During Execution

`Form1.SetExecutionMode` now drives a whole-window busy cursor while a script or command runs.

**`ApplyExecutionCursorState(bool executing)`** — Walks the control tree with `SetUseWaitCursorRecursive` to set `UseWaitCursor` on every descendant, then forces `Cursor.Current = WaitCursor` to flip the currently-hovered control immediately without waiting for a mouse move. On completion, unwinds recursively so no child control is left stuck on `WaitCursor`.

**Scintilla integration** — The Scintilla control draws its own text-caret cursor via native messaging and ignores `UseWaitCursor`. A new nested `ExecutionCursorAwareScintilla : Scintilla` uses `DirectMessage(SCI_SETCURSOR, ScCursorWait)` to temporarily force the wait cursor and `SCI_GETCURSOR` to capture / restore the prior Scintilla cursor ID. `ScintillaScriptEditorControl.SetExecutionCursorOverride(bool)` routes the outer busy-state toggle into the nested control.

### Stop Button Visual Refinement

`btnStopAll` is replaced with a new `SSH_Helper.UI.FlatVisualButton` (`UI/FlatVisualButton.cs`) — a `Button` subclass that paints its own flat background / hover / pressed states and respects `FlatAppearance.MouseOverBackColor` / `MouseDownBackColor` / `BorderColor`. It uses `TextRenderer.DrawText` with end-ellipsis truncation and single-line rendering so the "Cancelling..." state always fits the button. `UpdateStopButtonLayout()` centralizes layout during Execute / Stop / Cancel transitions; the default width is captured once in the constructor and restored after cancellation.

### Dependency Changes

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Toolkit.Uwp.Notifications` | 7.1.3 | Windows 10/11 toast notification builder for the `notify` command `channel: toast` path |

**Target framework bumped** — `TargetFramework` upgraded from `net8.0-windows` to `net8.0-windows10.0.17763.0` and `SupportedOSPlatformVersion` set to `10.0.17763.0` (Windows 10 1809) so `ToastContentBuilder` is available at build time.

### Test Coverage

| Test class | Coverage |
|------------|----------|
| `SSH_Helper.Tests/Scripting/NotifyCommandTests.cs` | Channel / profile resolution matrix, typed Teams mention emission, Slack member-ID and special-mention normalization, Discord `user:` / `role:` / `channel:` / raw markup pass-through, `into:` capture keys, `on_error: continue` vs `stop`, toast success without profile, dispatch failure when notifications disabled. |
| `SSH_Helper.Tests/Services/NotificationServiceTests.cs` | Profile not found, channel/profile kind mismatch, default profile fallback, toast-only path when `Enabled=false`, credential provider invocation, Adaptive Card shape assertions (schema URL, `msteams.entities`), SMTP subject prefix per level. |
| `SSH_Helper.Tests/UI/Form1NotificationInitializationTests.cs` | `NotificationService` wiring in Form1 startup, credential provider hookup, default-profile resolution from `AppConfiguration`. |
| `SSH_Helper.Tests/Scripting/SetHistoryLabelCommandTests.cs` | Scalar form, options form, replace / append / prepend / clear semantics, separator handling, `ReplaceAddress` tri-state, history-label-touched flag propagation. |
| `SSH_Helper.Tests/Services/SshExecutionServiceHistoryLabelTests.cs` | Sequential multi-preset aggregation preserves preset order, parallel multi-preset aggregation replays operations in definition order regardless of completion order, `JobHostOutput.Label` round-trip, folder-run label retention. |
| `SSH_Helper.Tests/Services/SshExecutionServiceOutputWindowTests.cs` | `${_outputwindow}` empty before relay attach, accumulates after relay attach on local and SSH runs, host-scoped isolation in multi-host runs, survives cross-step `send` + `extract` invocations. |
| `SSH_Helper.Tests/Services/SshExecutionServiceBannerFormattingTests.cs` | `BannerSidePadding = 10` applied to script / local-script / error banners, separator length matches header length. |
| `SSH_Helper.Tests/Services/SshConnectionPoolCompatibilityTests.cs` | Legacy two-argument `ReleaseSession` / `RemoveAsync` overloads still function; key derivation remains stable for hosts without identity overrides. |
| `SSH_Helper.Tests/Editor/YamlSshSyntaxHighlighterTests.cs` | `#` inside `"..."` is not a comment; `#` inside `'...'` is not a comment; `\"` escapes inside double quotes; `#` after a closed string remains a comment; mixed-quote lines highlight keyword + comment correctly. |
| `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs` | `sethistorylabel` option keys + `mode` enum values; `notify` option keys; `_outputwindow` emitted as a built-in variable; prompt-step `font_size:` emitted as a numeric option hint. |
| `SSH_Helper.Tests/Scripting/ScriptContextTests.cs` | `OutputWindowText` accumulation and isolation per context; `${_outputwindow}` substitution in `SubstituteVariables`. |
| `SSH_Helper.Tests/Scripting/ScriptDependencyAnalyzerTests.cs` | `_outputwindow` treated as a built-in reference (not a missing column). |
| `SSH_Helper.Tests/Scripting/ScriptParserTests.cs` | `sethistorylabel` scalar and options forms; `notify` option validation; `font_size` parsed to `float?` on prompt options. |
| `SSH_Helper.Tests/Scripting/SendCommandTests.cs` | Extended to cover `${_outputwindow}` substitution in send command text. |
| `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs` | `sethistorylabel` round-trip (scalar and options), `notify` block properties including mention JSON array, prompt `font_size` export ordering in the properties panel. |
| `SSH_Helper.Tests/Services/PresetManagerTests.cs` | `ExportFolderSubtreeToFile` rebases folder paths, bundles only descendants, `CountPresetsInFolderAndDescendants` returns correct counts including nested folders. |
| `SSH_Helper.Tests/UI/Form1FolderExportTests.cs` | "Export Folder..." context menu visibility on folder vs preset nodes, save-file dialog injection via `_saveFilePathPickerOverrideForTests`, success / no-selection / exception paths display the correct prompt dialog. |
| `SSH_Helper.Tests/UI/Form1DeleteUndoTests.cs` | Yes-vs-No paths through the new `ConfirmPresetDeletion` dialog; cancelling the confirm leaves the preset, tree, and undo stack untouched. |
| `SSH_Helper.Tests/UI/Form1MenuInitializationTests.cs` | Flow Canvas menu item is a top-level `MenuStrip` item named `_menuFlowCanvas`, placed immediately before the Help menu; `Ctrl+Shift+F` shortcut preserved. |
| `SSH_Helper.Tests/UI/Form1ScriptContextMenuTests.cs` | `ctxCommentSelectedLines` / `ctxUncommentSelectedLines` appear in the command-box context menu and invoke the editor's transform helpers. |
| `SSH_Helper.Tests/UI/ScintillaScriptEditorControlTests.cs` | Comment preserves indentation, uncomment removes leading `#` and optional trailing space, transform skips whitespace-only lines, undo toggles as a single action, line-ending round-trip. |
| `SSH_Helper.Tests/UI/Form1StopButtonStateTests.cs` | `btnStopAll` rendered as `FlatVisualButton`, default width restored after cancellation, "Cancelling..." label does not resize the button past its default width, layout updates run on Execute / Stop / Cancel transitions. |
| `SSH_Helper.Tests/UI/Form1ExecutionCursorTests.cs` | `SetExecutionMode(true)` sets `UseWaitCursor` on the form and all descendants including the Scintilla editor; `SetExecutionMode(false)` restores the default cursor everywhere; nested control children are walked recursively. |
| `SSH_Helper.Tests/UI/Form1BuiltInEditorVariableTests.cs` | Built-in variable insertion dialog lists `_outputwindow` alongside `_output`, `_timestamp`, and `_prompt`. |
| `SSH_Helper.Tests/UI/ScriptPromptDialogFontTests.cs` | Per-step `font_size` override wins over `FontSettings.ScriptPromptFontSize`; fallback uses `ScriptPromptFontSize` when the step omits `font_size`; dialog font assigned in points. |
| `SSH_Helper.Tests/Models/FontSettingsTests.cs` / `ConfigurationServiceFontSettingsTests.cs` | `ScriptPromptFontSize` default value and persistence round-trip through `config.json`. |
| `SSH_Helper.Tests/UI/Form1PresetTreeIncrementalMutationTests.cs` | Extended to cover delete-confirm cancellation path. |

### Documentation

`SCRIPTING.md` updated with:

- Full **notify** reference section: syntax, channel / profile resolution table, Slack / Teams / Discord mention rules, level styling table, `into:` result structure, and multiple worked examples (profile-implied channel, Slack member-ID shorthand, Discord typed shorthand, Teams typed UPN / Entra mentions, raw Discord markup pass-through, toast without profile, SMTP with `on_error: continue`).
- **Teams mention rules** explicitly call out that SSH Helper sends Adaptive Cards, accepts only `upn:` / `entra:` typed strings for live mentions, and downgrades invalid mentions to literal text with a runtime warning.
- **Discord Developer Mode note** under mention rules explaining that Discord user / role / channel IDs require enabling "Developer Mode" in Discord settings before right-click -> "Copy ID" is available.
- New `${_outputwindow}` entry in the built-in variables table, with a host-scoping note clarifying that the transcript is per-host during multi-host runs rather than the merged global pane.
- New **sethistorylabel** example demonstrating scalar, options, append/prepend, and clear forms.
- `font_size:` listed under the per-step options for `input`, `choose`, `multiselect`, and `confirm`.

---

## Changes Since `e1efcd7` (0.51.16)

### Preconnect Phase for Host-Scoped Auth Bootstrap

Scripts gain an optional top-level `preconnect:` section that runs **once per host before SSH authentication**. The phase is intended for local bootstrap work that must complete before the SSH login can succeed — for example, fetching an ephemeral certificate, requesting a short-lived password from a secrets backend, or provisioning a per-host identity file.

```yaml
---
preconnect:
  - localcmd:
      command: "Get-CertForHost {{Host_IP}}"
      into: cert_bootstrap
  - set:
      expression: _ssh_identity_file = cert_bootstrap_stdout
  - set:
      expression: _ssh_identity_passphrase = cert_bootstrap_stderr

steps:
  - send: whoami
```

**Reserved override variables** — Setting any of these inside `preconnect` redirects the resolved value into the SSH login that follows for the same host:

| Variable | Effect on SSH login |
|----------|---------------------|
| `_ssh_username` | Replaces base username for this host only |
| `_ssh_password` | Replaces base password for this host only |
| `_ssh_identity_file` | Sets the private key path used for key-based auth |
| `_ssh_identity_passphrase` | Sets the passphrase decrypting the identity file |

Overrides are scoped to the current host execution and never persist across hosts.

**`SshExecutionService.ResolveEffectiveScriptAuthContext`** — New per-host orchestration step that runs before `ExecuteScriptLocal` / `ExecuteScriptWithPool` / `ExecuteScriptWithoutPool`:

1. If `script.Preconnect` is empty, returns the base `HostConnection` / username / password unchanged.
2. Otherwise builds a fresh `ScriptContext` with `Session = null`, seeds connection variables, imports `script.Vars`, and executes `script.Preconnect` via `ScriptExecutor.ExecuteStepsAsync`.
3. Cancellation (`Operation cancelled`) and validation failures abort the host run before any SSH connect is attempted.
4. `IsControlFlow` results from preconnect (exit/break/continue/return) throw `InvalidOperationException` — control flow is not allowed in preconnect.
5. Reads the four reserved override variables back from the context and constructs a new `HostConnection` with the effective `IdentityFile`, `IdentityFilePassphrase`, and merged `Variables` dictionary, plus effective username/password strings.

**Pooled session keying** — `SshConnectionPool.CreateConnectionKey` now incorporates password, identity file, and identity passphrase so that a host with overridden auth never reuses a session authenticated with different effective credentials.

```
Old key:  "{ip}:{port}:{username}"
New key:  "{ip}:{port}:{username}:{passwordHash}:{identityFile}:{passphraseHash}"
```

Secrets are SHA-256 hashed (`HashSecret`) before being embedded in the key so leased session bookkeeping never holds raw passwords. Empty values become `-`. `ReleaseSession` and `RemoveAsync` gain a `password` parameter; the previous two-argument overloads are marked `[Obsolete]` and forward to the new ones with empty-string passwords for backward compatibility.

**Validation** — `ScriptParser` reserves `preconnect` as a known top-level key and parses it via `ParseSteps`. New validation rules:

- `preconnect` must be a YAML sequence — scalar / mapping values produce `"preconnect must be a sequence of steps"`.
- `send` and `interactive` are rejected inside `preconnect` (and inside any nested `then`/`else`/`do`/`try`/`catch`/`finally`/`cases`/`elif`/`parallel.steps` inherited via the new `insidePreconnect` flag) with: `"<command> is not allowed in preconnect because it requires an active SSH session"`.
- `preconnect` is added to the forbidden-key list for `library: true` files (alongside `steps`, `vars`, `imports`, `environment`).
- Reserved override variable names (`_ssh_*`) are preserved verbatim — typos like `_ssh_identitty_file` are not silently mapped to the correct name.

**Auto-detection** — `IsYamlScript` now treats a `preconnect:` top-level section as a positive YAML script indicator (alongside `steps:`, `vars:`, `imports:`, `subroutines:`).

**Sensitive value redaction** — `SshExecutionService.FormatScriptOutput` applies a new compiled regex (`SensitiveSetOutputRegex`) to every emitted message. The pattern matches `Set _ssh_password = ...` and `Set _ssh_identity_passphrase = ...` and replaces the value with `[REDACTED]` so debug traces and history payloads never contain raw secrets.

**Progress and output messaging** — `OnProgressChanged` emits `"Running preconnect for {host}"` and `"Preconnect completed for {host}"` for the status bar. The matching `"Preconnect started for {host}"` / `"Preconnect completed for {host}"` info-level output lines are gated by the effective debug flag (`DebugMode || script.Debug`) so non-debug runs stay quiet.

**`ScriptDependencyAnalyzer.AnalyzeReferences`** — Now walks `script.Preconnect` in addition to `script.Steps` so any host columns or vars referenced from preconnect contribute to the cross-step reference set used for missing-column preflight warnings.

### Manual Sort Position Preserved on Preset Rename

Renaming a preset while the tree is in manual sort mode now keeps the preset at its existing position among siblings instead of falling back to alphabetical insertion or losing its slot in the order list.

- **`Form1.RenamePresetInFolderOrder(string oldPresetName, string newPresetName, string? folder)`** replaces the previous direct mutation of `_manualPresetOrder`. It loads the per-folder order list (`config.ManualPresetOrderByFolder[folderKey]`) — falling back to the live preset enumeration if no entry exists — replaces `oldPresetName` with `newPresetName` in place, appends if not present, then calls `SyncLegacyRootPresetOrder` to keep the legacy root-folder order list in sync. Both inline rename (`trvPresets_AfterLabelEdit`) and dialog rename now route through this helper.
- **`MovePresetTreeNode` short-circuit** — When a tree node is being updated and the preset's `Folder` is unchanged from the parent node's folder, the method now calls `UpdatePresetTreeNodeDisplay(node)` and returns without `DetachTreeNode` + `InsertPresetNode`. This preserves both the visual selection and the sibling order during an in-place rename.
- New regression test `Form1PresetTreeIncrementalMutationTests.RenamePreset_ManualSort_KeepsPresetAtSameTreePosition` asserts the renamed node has the same `PrevNode` / `NextNode` references and that `ManualPresetOrderByFolder[""]` reads `["Alpha", "Bravo Renamed", "Charlie"]` after renaming `Bravo`.

### Script Editor Autocomplete Enhancements

`ScriptParser` now exposes a second tier of enum-like option value suggestions that are scoped per-command, plus a broader set of boolean-valued option keys for the global tier.

**Command-scoped option values** — `ScriptParser.GetEnumLikeOptionValuesByCommand` returns a `Dictionary<command, Dictionary<key, values>>`. The autocomplete provider checks the enclosing command via `FindEnclosingCommandName(text, lineStart, currentIndent)` first and falls back to the global enum-like map only when no scoped entry matches. Initial command-scoped registrations:

| Command | Option key | Suggested values |
|---------|------------|------------------|
| `dns` | `type` | `A`, `AAAA`, `PTR` |
| `exists` | `type` | `any`, `file`, `directory` |
| `input` | `password` | `true`, `false` |
| `confirm` | `default` | `true`, `false` |

The previous global `type` entry (which always suggested DNS values) is removed from the global map so `exists.type:` no longer offers `AAAA` and `dns.type:` no longer offers `directory`.

**Global boolean-valued option keys** — Added: `required`, `skip_empty_lines`, `trim_lines`, `pretty`, `suppress`, `overwrite`, plus the top-level script flags `debug`, `nobanner`, `compact_errors`, `suppress_missing_column_warning`, `library`. Typing any of these followed by `: ` now suggests `true` / `false`.

**Localcmd option values** — Added enum-like suggestions for `shell` (`powershell`, `custom`), `interactive` / `keep_open` / `kill_on_cancel` (`true` / `false`), `run_mode` (`foreground`, `background`), `lifetime` (`detached`, `script`, `app`), `confirm` (`always`, `once`, `never`).

`ScriptAutocompleteProvider` consumes the new map via `_enumLikeOptionValuesByCommand` and routes scoped lookups through canonicalized keys. New tests cover each command-scoped scenario and assert that boolean suggestions don't bleed across commands (e.g. `sftp.password:` is a free-text field and intentionally returns `CompletionContextKind.None`).

### Flow Canvas Local Command Block — `cmd` Shell Option Removed

`FlowCanvas/src/blockDefs/registry.ts` now lists `['powershell', 'custom']` for the local command block's `shell` field (default still `powershell`). Authors who want the `cmd` shell can still enter `cmd` via raw YAML — the parser allow-list is unchanged — but the visual block dropdown no longer offers it.

### Test Coverage

| Test class | Coverage |
|------------|----------|
| `SSH_Helper.Tests/Services/SshExecutionServicePreconnectTests.cs` | Local-only script with preconnect succeeds; `send` inside preconnect fails validation with `"not allowed in preconnect"`; preconnect emits start/completion progress + output messages; preconnect assertion failure aborts before SSH; cancellation during a `wait:` step inside preconnect marks the result `WasCancelled` with `"Operation cancelled"`. |
| `SSH_Helper.Tests/Services/SshConnectionPoolKeyTests.cs` | `CreateConnectionKey` is deterministic for identical inputs; differing identity file, passphrase, password, or username produce distinct keys; secrets are hashed (no raw values appear in keys). |
| `SSH_Helper.Tests/Services/SshExecutionServiceOutputFormattingTests.cs` | `FormatScriptOutput` redacts `_ssh_password` / `_ssh_identity_passphrase` Set lines to `[REDACTED]`. |
| `SSH_Helper.Tests/Scripting/ScriptParserTests.cs` | `IsYamlScript` recognizes `preconnect:` as a YAML indicator; `Parse` accepts `preconnect` as a sequence of steps; `Validate` rejects scalar `preconnect` and `send` inside `preconnect`. |
| `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs` | DNS/exists/input/confirm command-scoped option values; cross-command isolation (`sftp.password` returns nothing); boolean values for `extract.required`, `send.suppress`, `readfile.skip_empty_lines`/`trim_lines`, `writefile.pretty`, `sftp.overwrite`; top-level boolean keys (`debug`, `nobanner`, `compact_errors`, `suppress_missing_column_warning`, `library`); `localcmd.run_mode` and `localcmd.confirm`. |
| `SSH_Helper.Tests/UI/Form1PresetTreeIncrementalMutationTests.cs` | `RenamePreset_ManualSort_KeepsPresetAtSameTreePosition` asserts the renamed node retains its `PrevNode` / `NextNode` neighbors and that `ManualPresetOrderByFolder[""]` reflects the relabeled position. |

### Documentation

`SCRIPTING.md` updated with:

- New `preconnect:` example in the top-level script anatomy block (cert bootstrap pattern using `localcmd` + `set _ssh_identity_file` / `_ssh_identity_passphrase`).
- Explanatory paragraph: preconnect runs once per host before SSH authentication; supported override variables are `_ssh_identity_file`, `_ssh_identity_passphrase`, `_ssh_username`, `_ssh_password`; `send` and `interactive` are not allowed inside preconnect.
- `preconnect:` added to the auto-detection top-level section list.

---

## Changes Since `c350105` (0.51.15)

### Vault OIDC Authentication

`VaultAuthMethod.Oidc` is added as a fifth authentication method for Vault profiles (`Models/VaultSettings.cs`). OIDC uses a browser-based sign-in with PKCE and a local loopback callback listener; the resulting Vault token is persisted in Windows Credential Manager and reused until it expires.

**`VaultOidcLoginFlow`** (`Services/Vault/VaultOidcLoginFlow.cs`) — Orchestrates the browser handshake:

- Starts an `HttpListener` bound to the configured loopback host and port
- Opens the Vault-provided `auth_url` via `Process.Start` with `UseShellExecute`
- Waits for the IdP redirect on the callback path, returning the `state`, `code`, `error`, and `error_description` query parameters
- Writes a small HTML completion page to the browser tab on success or failure
- Honors the configured timeout (15 second minimum) and propagates script cancellation

**`VaultOidcCallbackSettings`** (`Services/Vault/VaultOidcCallbackSettings.cs`) — Normalizes and validates callback bindings:

- Allowed loopback hosts: `127.0.0.1`, `localhost`, `::1` (any other host is rejected with a friendly error before HTTP calls begin)
- Port range validated to 1-65535 (default `8250`)
- Path defaults to `/oidc/callback`; leading slash is added if missing
- Generates the `RedirectUri` (wrapping IPv6 hosts in brackets) and `ListenerPrefix` for `HttpListener`

**`VaultService.AuthenticateWithOidcAsync`** — Full OIDC flow:

1. Attempt `TryAuthenticateWithPersistedOidcTokenAsync` first — runs `auth/token/lookup-self` against any previously saved token; valid tokens skip the browser entirely. HTTP 401/403 falls back to fresh login; transport errors (e.g. `HttpRequestException`) propagate without starting a new browser flow.
2. Generate random `state` (32 bytes), `nonce` (32 bytes), and PKCE `verifier` (64 bytes); derive the SHA-256 `code_challenge` with `S256` method.
3. POST to `auth/<mount>/oidc/auth_url` with `role`, `redirect_uri`, `state`, `nonce`, `code_challenge`.
4. Delegate to `IVaultOidcLoginFlow` to open the browser and capture the callback.
5. Validate `state` matches; surface `error`/`error_description` from the callback as `VaultException`.
6. POST to `auth/<mount>/oidc/callback` with the code and verifier; extract `client_token` and `lease_duration` (TTL refreshes at 75% of lease).
7. Invoke the `tokenSaver` callback so the main form can persist the token to `CredentialTargets.VaultAuthTarget(profileName, "token")`.

**Settings UI** — `SettingsDialog` gains an OIDC auth panel (`_pnlVaultAuthOidc`) with fields for Auth Mount (default `oidc`), Role, Callback Host (default `127.0.0.1`), Callback Port (default `8250`), Callback Path (default `/oidc/callback`), and Timeout in seconds (default `180`, range 15-3600). `ValidateVaultProfiles` runs on save and blocks persistence when OIDC role is empty or when the callback host is not a loopback address. `Test Connection` uses the in-memory `tokenSaver` so a test login can complete without persisting until the user clicks Save.

**Form1 integration** — `Form1` wires a `tokenSaver` delegate into `VaultService` construction that routes saved tokens to `CredentialTargets.VaultAuthTarget(profileName, "token")` via the shared `_credentialProvider`.

### SSH Algorithm Fallback with Caching

`SshConnectionPool.CreateConnectionAsync` gains a multi-tier algorithm fallback strategy for endpoints that reject the default Rebex algorithm set. Each tier is tried in order and the successful tier is cached per `host:port` so subsequent connections skip straight to the known-good tier.

**Tiers:**

| Tier | Host key algorithms | Encryption | MAC |
|------|---------------------|------------|-----|
| `Default` | Rebex defaults | Rebex defaults | Rebex defaults |
| `NonRsa` | `ssh-ed25519`, `ecdsa-sha2-nistp256/384/521` | Rebex defaults | Rebex defaults |
| `Ed25519Only` | `ssh-ed25519` | `aes256-ctr`, `aes128-ctr`, `aes256-cbc`, `aes128-cbc` | `hmac-sha2-256`, `hmac-sha2-512`, `hmac-sha1` |

**Detection** — `HasUnsupportedKeyAlgorithmError` walks the exception chain looking for the literal string `"key algorithm is not supported"`. Only this specific failure class triggers the fallback ladder; all other connection errors bubble up unchanged.

**Cache** — `SshExecutionService.HostAlgorithmCache` is a static `ConcurrentDictionary<string, HostKeyAlgorithmTier>` keyed by `ip:port`. A cache hit performs a single connection attempt with the cached tier; if that attempt fails the cache entry is removed and full discovery resumes. Hosts with explicit `HostKeyAlgorithms` from SSH config bypass the cache entirely.

**Ed25519 support** — Adds `libs/RebexElliptic/net8.0/Rebex.Castle.dll`, `Rebex.Curve25519.dll`, and `Rebex.Ed25519.dll` as conditional references in `SSH_Helper.csproj`. `Program.RegisterRebexEllipticPlugins` registers `EllipticCurveAlgorithm`, `Curve25519`, and `Ed25519` with Rebex's `AsymmetricKeyAlgorithm` at startup. The full NuGet package layout (`libs/RebexElliptic/{monoandroid40,net20,net40,net6.0,net8.0,netcf35,netstandard1.5,netstandard2.0,netstandard2.1,xamarinios10}/`) is committed so the project builds without requiring the companion NuGet feed.

**Connection pool concurrency** — `SshConnectionPool` now uses a per-key creation lock (`_creationLocks` keyed by `user@host:port`) plus a global throttle (`_globalCreationGate`, default 12 concurrent creations via `DefaultMaxConcurrentConnectionCreations`). Previously a single `_creationLock` serialized all connection creations across the pool.

**Rebex.SshShell upgraded** from `7.0.9448` to `7.0.9561`.

### `compact_errors` Script Flag

A new top-level `compact_errors: true` flag on `Script` (`Services/Scripting/Models/Script.cs`) switches connection/authentication/timeout/network/cancellation/generic error output from multi-line banner blocks to single lines.

```yaml
---
name: "Fast Checks"
compact_errors: true

steps:
  - send:
      command: show version
```

Banner output:

```
########################################################################
#################### CONNECTION ERROR: 10.79.50.228 ####################
########################################################################
SshException: Connection attempt timed out.
```

Compact output:

```
AUTHENTICATION ERROR: 10.79.50.231: SshException: A supplied password or user name is incorrect.
CONNECTION ERROR: 10.79.50.228: SshException: Connection attempt timed out.
```

`SshExecutionService.FormatError` accepts `compactErrors` and renders a one-line `"{category}: {host}: {exception}"` string when enabled. The effective debug flag (`DebugMode || script.Debug`) still controls whether full stack traces are appended.

**Integration points:**

- `ScriptParser` reserves `compact_errors` as a known top-level key, parses it via `ParseBooleanOrDefault`, and validates it cannot appear in `library:` imports (`ValidateForbiddenLibraryKey`)
- `FlowCanvasBridge` serializes the flag in the YAML preamble during export and reads it back on import
- `FlowCanvas/src/nodes/StartNode.tsx` adds a `compact-errors` flag badge
- `FlowCanvas/src/panels/Properties.tsx` adds a "Compact Errors" boolean field under Start block properties
- `ScriptAutocompleteProvider` adds `compact_errors` to the top-level key completion list

### LocalCmd Command Hardening

The `localcmd` command shipped in 0.51.14 receives significant behavioral fixes:

**Interactive detached launch** — When `interactive: true` is combined with an **explicit** `lifetime: detached`, the terminal window is started and the script continues immediately without waiting for the window to close. A new `LocalCmdOptions.LifetimeSpecified` flag distinguishes explicit from default `detached` values so the implicit default (unspecified) preserves the prior behavior of waiting for the window.

```yaml
- localcmd:
    command: "ping 8.8.8.8"
    shell: cmd
    interactive: true
    lifetime: detached
    into: ping_window
```

For detached interactive launches the `into` prefix captures startup metadata (`_pid`, `_started`, `_start_error`) and does not capture `_exit_code`. `fail_on_nonzero` cannot be evaluated and emits a warning to the output stream. `ScriptDependencyAnalyzer` recognizes this variant and declares the correct variable set for cross-step reference checking.

**Cancellation-aware confirmation** — `ILocalCmdConfirmation.ConfirmAsync` now takes a `CancellationToken` parameter that is threaded through from the script's execution token. Cancelling the running script while the confirmation dialog is open throws `OperationCanceledException` from the confirmation task and no process is launched. `LocalCmdConfirmationDialog` forwards the token to `ScriptPromptDialogRunner.ShowAsync` instead of the previous `CancellationToken.None`.

**Unattended preflight** — `SshExecutionService.TryBuildUnattendedLocalCmdPreflightMessage` walks the script tree (including nested `then`/`else`/`do`/`try`/`catch`/`finally`/`cases`/`elif`/`parallel` branches and `call`-resolved subroutines via `ScriptSubroutineRegistry`) and fails the script before execution if any `localcmd` step has a confirm policy other than `never` while `allowFileSelectionDialogs: false`. The error points the user at `localcmd.confirm: never`. This applies to scheduler runs (`JobExecutionService`) and any other unattended execution path.

**PowerShell EncodedCommand** — Replaces the previous `-Command "escaped string"` path with `-EncodedCommand <Base64 UTF-16>`. `EncodePowerShellCommand` uses `Convert.ToBase64String(Encoding.Unicode.GetBytes(command))`. `PrepareNonInteractivePowerShellCommand` prepends `$ProgressPreference = 'SilentlyContinue';` to suppress PowerShell CLIXML progress records on stderr. Adds `-NoProfile` to non-interactive invocations. Interactive audit capture (Tee-Object wrapper for `cmd` shell) also uses `-EncodedCommand`.

**Quoted executable passthrough** — `TryParseQuotedExecutableInvocation` detects commands starting with a quoted `.exe` path (e.g. `'C:\Program Files\Git\usr\bin\bash.exe' -l -i -c 'echo hi'`) and launches the executable directly with the remaining tokens as arguments, bypassing the PowerShell interpreter entirely when possible. When PowerShell is still needed (e.g. interactive keep-open), `PreparePowerShellCommand` adds a `&` call operator so quoted paths are invoked rather than parsed as strings.

**Argument quoting** — `QuoteCommandLineArgument` implements full Windows command-line escaping: arguments containing whitespace or `"` are wrapped in double quotes; embedded backslashes preceding a quote are doubled; trailing backslashes are escaped to avoid breaking the closing quote. `JoinCommandLineArguments` replaces the previous `string.Join(" ", args)` for all shells.

**Shell allow-list change** — `cmd` / `cmd.exe` is now accepted by `ScriptParser.IsValidLocalCmdShell`. `pwsh` and `pwsh.exe` are **no longer** recognized as PowerShell variants — only `powershell` / `powershell.exe`. This matches the Flow Canvas block registry (`FlowCanvas/src/blockDefs/registry.ts`), which lists `['powershell', 'cmd', 'custom']` as the shell options. The parser error now reads `localcmd 'shell' must be one of powershell, cmd, custom`.

**Confirmation dialog labels** — `LocalCmdConfirmationDialog` renames the middle button from "Run All" to "Run Same Command" (140px wide) and adds a scope explanation label: _"Run Same Command approves this resolved command for the current host for the rest of this run."_ Buttons repositioned to (90, 195, 350).

**Flow Canvas bridge** — `FlowCanvasBridge` exports `lifetime` whenever `LifetimeSpecified` is true (or the value is not the default `detached`), so the explicit flag round-trips through YAML → graph → YAML without collapsing back to the default. The registry field label changes from "Background Lifetime" to "Process Lifetime" with updated help text covering both interactive and background semantics.

### Folder Execution Concurrency Limits

- **Hard cap** — `SshExecutionService.MaxParallelHosts = 100`. Both `SshExecutionService.ExecuteFolderAsync` and `FolderExecutionDialog` clamp `ParallelHostCount` to `[1, 100]`. Values exceeding the cap are adjusted at runtime with a warning printed to the first host's output: `"Parallel hosts value 'X' adjusted to 100. Maximum supported value is 100."`
- **Parallel preset mode gate** — When `ParallelHostCount > 1`, the option `RunPresetsInParallel` is forced off and a warning is emitted: `"Preset parallel mode is disabled when running multiple hosts in parallel. Falling back to sequential presets per host."` The `FolderExecutionDialog` radio button relabels to `"Parallel (disabled when running multiple hosts in parallel)"` and disables when the parallel host count exceeds 1. `UpdateExecutionModeConstraints` enforces this in the dialog on both text-change and blur events.
- **Host-result thread safety** — Parallel preset execution now acquires `hostResultLock` before mutating `hostResult.Success` / `hostResult.ErrorMessage`. The previous code wrote to the shared `ExecutionResult` from multiple preset tasks without synchronization. The error message is also preserved on a `??=` basis (first error wins) instead of being overwritten by each subsequent failure.

### Manual Sort Preset Insertion Positioning

Add, Duplicate, and Import Preset now insert the new preset directly below the currently selected preset when the preset tree is in manual sort mode, instead of appending at the end.

- **`PositionCreatedPresetAfterReference`** in `Form1` picks a reference preset (explicit parameter or the current tree selection, provided it's in the same folder) and calls `InsertIntoPresetOrder(presetName, folder, referenceName, DropPosition.Below)`.
- **Add Preset** captures the selected preset name as `insertAfterPresetName` before calling `PresetManager.AddOrUpdate`, then positions the new preset.
- **Duplicate Preset** uses the source preset name as the reference and now calls `SelectPresetByName(finalName, ensureVisible: true)` + `EnsureTreeNodeFullyVisible` so the duplicate is scrolled into view.
- **Import Preset** positions the imported preset after the selection and scrolls into view.
- **`SyncLegacyRootPresetOrder`** keeps the legacy `_manualPresetOrder` list in sync with `config.ManualPresetOrderByFolder[""]` so the two code paths don't drift. Called from `HandlePresetReorderRequest` and `HandleManualReorder`.

### Flow Canvas Reopen Refresh

`Form1.OpenFlowCanvas` now calls `LoadCurrentScriptIntoCanvas()` and `SendTargetHostToCanvas()` when reusing an existing `_flowCanvasForm` instance. Previously, switching presets while the Flow Canvas was closed and then reopening the existing window would show a stale graph. The canvas now rehydrates with the currently selected preset's YAML on every reopen.

### History UI Disposal Safety

Race conditions during form close that could throw `ObjectDisposedException` on `lstOutput`, `txtOutput`, or `historySplitContainer` are closed:

- **`IsHistoryUiUnavailable`** — Returns true when the form or any of the history-related controls are disposed. Checked by `ArmHistorySelectionOnIdle` and `ApplySelectedHistoryEntry`.
- **`CancelPendingHistorySelectionHydration`** — Clears `_historySelectionArmPending` and detaches `Application.Idle -= ArmHistorySelectionOnIdle`. Called from both `IsHistoryUiUnavailable` guard paths and from the `FormClosed` handler.
- **`IsOutputTextBoxUnavailable`** — Guards `AppendOutputToUi`, `SetOutputText`, `ClearOutput`, `ScrollOutputToEnd`, and `RecreateOutputTextBoxIfNeeded` so output events arriving after form close become no-ops. `RecreateOutputTextBoxIfNeeded` also checks `parent.IsDisposed`.
- **`FormClosed`** — Now detaches `Application.Idle -= BootstrapSchedulerAfterStartupRestoreOnIdle` alongside the existing `_uiOutputThrottler` disposal.

### Execution Start Debug Message

`Form1.BuildExecutionStartDebugMessage` inspects the preset before execution and emits one of three debug log strings depending on whether the script actually needs an SSH session:

- `"Calling ExecutePresetAsync - SSH connection starting"` — legacy command presets or YAML scripts that analyze as requiring SSH
- `"Calling ExecutePresetAsync - Local execution starting"` — YAML scripts where `ScriptDependencyAnalyzer.AnalyzeSshRequirements(script).RequiresSshSession` is false
- `"Calling ExecutePresetAsync - Execution starting"` — fallback if parsing throws

This clarifies debug logs for scripts composed entirely of `localcmd`, `exists`, `playsound`, `set`, and similar local-only commands.

### Dialog Seam for Import Preset

`Form1.ShowPromptDialog` wraps `DialogTheme.Show` with a test-override hook (`_dialogPromptOverrideForTests`). `ImportPreset` now routes its three message boxes (success, `FormatException`, generic failure) through the seam so tests can intercept the dialogs without ShowDialog blocking.

### Script Editor Autocomplete on Backspace

`ScintillaScriptEditorControl` adds `Keys.Back` to the navigation-key list that suppresses autocomplete popup re-triggering on key-up. Without this fix, pressing Backspace after accepting an autocomplete suggestion would immediately reopen the popup.

### Job History Tests — Relative Timestamps

`JobHistoryServiceTests.RecentUtc(hour, minute, second, dayOffset)` helper replaces hardcoded `new DateTime(2026, 3, 8, ...)` literals in retention/cancellation/skipped-run tests. Timestamps are now anchored to `DateTime.UtcNow.Date.AddDays(-1)` so tests keep passing as retention policy windows roll forward over time.

### QA Preset Expected Marker

`QaPresetCatalogTests` and `QaPresetExecutionTests` now match `"Expected: pass"` instead of `"Expected: pass."`. The trailing period is no longer required in the QA preset description convention.

### Dependency Changes

| Package | Version | Purpose |
|---------|---------|---------|
| `Rebex.SshShell` | `7.0.9448` → `7.0.9561` | Rebex SSH shell update |
| `Rebex.Castle` | bundled (`libs/RebexElliptic/net8.0/`) | Required for `Rebex.Ed25519` |
| `Rebex.Curve25519` | bundled (`libs/RebexElliptic/net8.0/`) | Curve25519 key exchange plugin |
| `Rebex.Ed25519` | bundled (`libs/RebexElliptic/net8.0/`) | Ed25519 host key signing/verification plugin |

### Documentation

`SCRIPTING.md` updated with:

- **Vault profile auth methods** — OIDC listed alongside Token/AppRole/LDAP/Userpass, with notes on the browser flow, recommended callback defaults, and Windows Credential Manager token storage
- **`localcmd` shell option** — `cmd` added to the allowed values; table and comments updated
- **`localcmd` lifetime semantics** — Interactive + explicit `lifetime: detached` documented as fire-and-forget, with captured variable lists for foreground / tracked interactive / interactive detached / background modes
- **`localcmd` unattended guidance** — Scheduler runs require `confirm: never`; preflight failure behavior noted
- **`compact_errors` section** — New header section under "Script Preamble" showing before/after output
- **`localcmd` detached interactive example** — Added ping example under the localcmd examples block

### Test Coverage

New tests added:

- **Vault** — `VaultServiceTests` (OIDC success flow with persisted token, state mismatch detection, invalid callback host rejection before any HTTP call, persisted-token fast path, 401 fallback to browser login, transport-error propagation without fallback, IPv6 loopback redirect URI normalization), `VaultSettingsTests` (OIDC default fields, OIDC profile round-trip serialization, `VaultAuthMethod.Oidc` enum value)
- **UI — SettingsDialog** — `SettingsDialogVaultTests.SelectingOidcAuthMethod_ShowsOidcPanelAndHidesOthers`, `SavingOidcProfile_PersistsOidcConfiguration`, `SavingOidcProfile_WithNonLoopbackCallbackHost_ShowsValidationAndDoesNotPersist` (with `RecordingSettingsDialogPromptService`)
- **Scripting — LocalCmd** — `Interactive_Detached_ReturnsImmediatelyWithoutWaitingAndSetsStartupMetadata`, `Confirmation_CancelledByExecutionToken_ThrowsWithoutStartingProcess`, `Interactive_PowerShell_QuotedExecutablePath_UsesCallOperatorInAuditWrapper`, `BuildProcessArgs_Powershell_QuotedExecutablePath_ExecutesDirectly`, `BuildProcessArgs_Powershell_QuotesArgsContainingSpaces`, `BuildProcessArgs_Custom_QuotesArgsContainingSpaces`, `BuildProcessArgs_Powershell_UsesNoProfileAndPrependsProgressSuppression`, `BuildInteractiveKeepOpenArgs_Powershell_QuotedExecutablePath_AddsCallOperator`, `Interactive_Cmd_WrapsCommandForAuditCapture` (updated for EncodedCommand). Adds `DecodePowerShellEncodedCommand` / `NormalizePowerShellCommandForAssertions` helpers.
- **Scripting — Parser** — `LocalCmdParserTests.Validate_CmdShell_DoesNotReturnShellValidationError`, `Validate_PwshShell_ReturnsShellValidationError`, `ScriptParserTests.Parse_ScriptWithCompactErrors_ParsesFlag`
- **Scripting — Dependency analyzer** — `ScriptDependencyAnalyzerTests.AnalyzePresets_LocalCmdInteractiveDetachedInto_DefinesStartupMetadataVariables`
- **Services — FlowCanvasBridge** — `ExportGraphToYaml_LocalCmdCmdShell_ExportsSuccessfully`, `Registry_LocalCmdShellOptions_ExcludePwsh`, `TextToGraph_LocalCmdInteractiveDetached_PreservesExplicitLifetimeProp`, `ImportExportRoundTrip_LocalCmdInteractiveDetached_PreservesExplicitLifetime`
- **Services — Job execution** — `RunNowAsync_CustomPresetLocalCmdConfirmAlways_FailsWithoutPrompt`, `ExecuteScheduledJobAsync_CustomPresetLocalCmdConfirmAlways_FailsWithoutPrompt`
- **Services — Job history** — Retention/cancellation/skipped-run tests converted to `RecentUtc` helper
- **UI — Form1** — `Form1ExecutionStartDebugMessageTests` (SSH vs local vs fallback messages), `Form1FlowCanvasPresetSyncTests.ReopeningExistingFlowCanvas_AfterPresetSwitch_QueuesCurrentPresetGraph`, `Form1HistorySelectionLifecycleTests.ArmHistorySelectionOnIdle_AfterFormDisposal_DoesNotTouchDisposedOutputControls`, `Form1PresetTreeIncrementalMutationTests.AddPreset_ManualSort_InsertsNewPresetBelowSelectedPresetAndSelectsIt`, `DuplicatePreset_ManualSort_InsertsDuplicateBelowSelectedPresetAndSelectsIt`, `ImportPreset_ManualSort_InsertsImportedPresetBelowSelectedPresetAndSelectsIt`
- **UI — Scintilla** — `ScintillaScriptEditorControlTests` autocomplete Backspace suppression case

---

## Changes Since `fe629ed` (0.51.14)

### `localcmd` Command — Local Process Execution

`LocalCmdCommand` (`Services/Scripting/Commands/LocalCmdCommand.cs`) executes arbitrary local processes from within YAML scripts, with a mandatory user-confirmation dialog before execution:

```yaml
- localcmd:
    command: "Get-Process | Select-Object -First 5"
    shell: powershell
    into: result
    confirm: always
```

**Execution modes:**

- **Foreground** (default) — Captures stdout and stderr into `${into}_stdout`, `${into}_stderr`, and `${into}_exit_code`. Output is streamed to the script output panel in real time. Supports `timeout` and `max_output_bytes` (default 1 MB) with truncation markers.
- **Background** (`run_mode: background`) — Starts the process detached from the script's execution flow. Populates `${into}_pid`, `${into}_started`, and `${into}_start_error`. Lifetime management via `lifetime` (`script`, `app`, `detached`) and `kill_on_cancel` options. Background processes registered against the `ScriptContext` are cleaned up on script completion; app-lifetime processes are cleaned up on `AppDomain.ProcessExit`.
- **Interactive** (`interactive: true`) — Opens a visible terminal window. Supports `keep_open` to leave the window open after the command completes. Tracked interactive runs capture `${into}_exit_code`; explicitly setting `lifetime: detached` returns immediately and captures `${into}_pid`, `${into}_started`, and `${into}_start_error`. Mutually exclusive with background mode.

**Confirmation system:**

- `ILocalCmdConfirmation` interface with `LocalCmdConfirmResult` enum (`Run`, `RunAll`, `Cancel`)
- `LocalCmdConfirmationDialog` (`UI/LocalCmdConfirmationDialog.cs`) shows the resolved command, shell, and working directory before execution
- `confirm` policy: `always` (default), `once` (per command+host), or `never`
- `RunAll` approval persists per-host across subsequent commands within the same script execution via `ScriptContext.LocalCmdRunAllApproved`
- Unattended scheduler runs require `localcmd.confirm: never`; otherwise script preflight fails with a clear message instead of blocking on a modal prompt

**Shell support:**

- `powershell` (default) — `powershell.exe -NoLogo -NonInteractive -EncodedCommand`
- `cmd` — `cmd.exe /c`
- `custom` — Uses `shell_path` for arbitrary executables with `args` passthrough
- Custom `env` dictionary for injecting environment variables into the child process

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `command` | Yes | — | Command string to execute |
| `shell` | No | `powershell` | Shell: `powershell`, `cmd`, or `custom` |
| `shell_path` | No | — | Executable path when `shell: custom` |
| `args` | No | — | Extra CLI arguments for custom shell |
| `env` | No | — | Environment variable key/value pairs |
| `working_dir` | No | — | Working directory (supports `%VAR%` expansion) |
| `interactive` | No | `false` | Open a visible terminal window |
| `keep_open` | No | `false` | Keep interactive terminal open after command completes |
| `run_mode` | No | `foreground` | `foreground` or `background` |
| `lifetime` | No | `detached` | Background process lifetime: `script`, `app`, or `detached`; explicit detached also enables fire-and-forget interactive launch |
| `kill_on_cancel` | No | `false` | Kill tracked background process on script cancellation |
| `fail_on_nonzero` | No | `true` | Fail step on non-zero exit code |
| `success_codes` | No | `[0]` | List of acceptable exit codes |
| `max_output_bytes` | No | `1048576` | Max captured output size before truncation |
| `confirm` | No | `always` | Confirmation policy: `always`, `once`, `never` |
| `quiet` | No | `false` | Suppress command echo but show output |
| `into` | No | — | Variable prefix for captured results |
| `timeout` | No | — | Timeout in seconds |
| `on_error` | No | `stop` | Error handling: `stop` or `continue` |

`ScriptDependencyAnalyzer` recognizes `localcmd` steps as not requiring an SSH session. The `into` variable tracking accounts for all three execution modes with their distinct output variable sets.

### HashiCorp Vault Integration

A full HashiCorp Vault integration provides secret management across scripts, jobs, environments, and credentials.

**VaultService** (`Services/Vault/VaultService.cs`) — Core service implementing:

- **Authentication** — Five auth methods: `Token`, `AppRole`, `LDAP`, `Userpass`, and `OIDC`. All secrets (tokens, secret IDs, passwords) are stored in Windows Credential Manager via `CredentialTargets.VaultAuthTarget(profileName, authType)`. Token TTL tracking with 75% expiry threshold for automatic re-authentication.
- **KV v1/v2 support** — `AutoDetect` mode queries the Vault mount's `options.version` to determine the KV engine version. V2 paths are automatically adjusted to include `/data/` and `/metadata/` segments. Secret version pinning supported on V2 reads.
- **Read/Write/Patch/List operations** — `ReadSecretAsync`, `ReadSecretKeysAsync`, `WriteSecretAsync`, `PatchSecretAsync`, `ListSecretsAsync`. Write and patch accept structured JSON values — `TryParseStructuredJson` detects JSON objects/arrays in value strings (including escaped payloads) and preserves their structure.
- **Caching** — Per-profile TTL-based cache with `CacheTtlSeconds` (default 300s). Cache keys are scoped to `profile:path:key:version`. Path-level invalidation on writes/patches.
- **TLS** — Custom CA certificate support via `CaCertificatePath` and `SkipTlsVerification` for development environments.
- **Error translation** — HTTP errors are translated into `VaultException` with context: 403 → "access denied", 404 → "secret not found", etc.
- **Connection testing** — `TestConnectionAsync` validates authentication and runs a `v1/sys/health` check, surfacing sealed/uninitialized states.

**Configuration** (`Models/VaultSettings.cs`, `Models/AppConfiguration.cs`) — `VaultSettings` on `AppConfiguration.Vault` with named `VaultProfileConfig` profiles. Each profile specifies `Address`, `Namespace`, `MountPath`, `AuthMethod`, `KvVersion`, `CacheTtlSeconds`, `CaCertificatePath`, and `SkipTlsVerification`. Auth-method-specific fields: `AppRoleRoleId`, `LdapUsername`, `UserpassUsername`.

**`vault` Script Command** (`Services/Scripting/Commands/VaultCommand.cs`) — Reads, writes, or patches Vault secrets from YAML scripts:

```yaml
# Read a single key
- vault:
    path: ssh/creds/server01
    key: password
    into: ssh_password
    profile: prod

# Read multiple keys
- vault:
    path: ssh/creds/server01
    keys:
      username: ssh_user
      password: ssh_pass

# Write secrets
- vault:
    path: app/config/myservice
    write:
      api_key: "${generated_key}"
      endpoint: "https://api.example.com"

# Patch existing secrets (KV v2 merge)
- vault:
    path: app/config/myservice
    patch:
      api_key: "${new_key}"
```

**Vault Functions** (`Services/Scripting/Functions/VaultFunctions.cs`) — Three inline functions registered in `FunctionRegistry`:

| Function | Signature | Description |
|----------|-----------|-------------|
| `vault` | `vault(path, key [, profile])` | Read a single secret value |
| `vault_list` | `vault_list(prefix [, profile])` | List secret keys at a path |
| `vault_clear_cache` | `vault_clear_cache()` | Flush the secret cache |

**VaultCredentialProvider** (`Services/Vault/VaultCredentialProvider.cs`) — Adapts `VaultService` to the `ICredentialProvider` interface for SSH credential resolution. Vault path syntax: `[profile@]path[#usernameKey,passwordKey]` (defaults to `username` and `password` fields).

**Job Scheduler Integration** — `CredentialMode.Vault` added to `JobDefinition` with `VaultCredentialPath` and optional `VaultProfileName` override. `JobExecutionService` resolves SSH credentials from Vault at runtime when this mode is selected.

**Environment Integration** — `EnvironmentConfig.VaultProfileName` enables per-environment Vault profile overrides. The `EnvironmentDialog` gains a "Vault Profile" dropdown populated from configured profiles. Environment switches propagate the profile override to `SshExecutionService` and `JobExecutionService`.

**Settings UI** — A new "Vault" tab in `SettingsDialog` with:

- Profile list with add/remove
- Connection details (address, namespace, mount path, KV version)
- Auth method switcher with context-sensitive panels (Token, AppRole, LDAP, Userpass, OIDC)
- Credentials stored/retrieved from Windows Credential Manager
- CA certificate path browser and TLS skip toggle
- Cache TTL configuration
- Default profile designation
- "Test Connection" button that validates auth + health check

**Flow Canvas** — `vault` block registered in `blockDefs/registry.ts` under the `network` category with `keyvalue` property type for keys/write/patch maps. `FlowCanvasBridge` dispatch maps updated for YAML round-tripping.

### `random_string` and `uuid` Functions

Two new functions registered in `StringFunctions`:

- **`random_string([length] [, charset])`** — Generates a cryptographically random string using `RandomNumberGenerator.GetInt32`. Default length 16, max 4096. Default charset: alphanumeric (a-z, A-Z, 0-9). Supports bracket-style charset expansion (e.g., `[a-z0-9]`).

```yaml
- set:
    name: api_key
    value: "${random_string(32)}"
- set:
    name: hex_token
    value: "${random_string(16, '[0-9a-f]')}"
```

- **`uuid()`** — Returns an RFC 4122 UUID v4 string via `Guid.NewGuid().ToString("D")`.

```yaml
- set:
    name: request_id
    value: "${uuid()}"
```

### Credential Manager Behavior Change

The `CredentialSettings.UseCredentialManager` toggle is narrowed in scope — it now controls only whether the **main form default password** is persisted and auto-loaded from Windows Credential Manager. Host-level credentials, job credentials, and Vault authentication tokens always use Credential Manager whenever the provider is available. The `CredentialManagerProvider` is now always initialized regardless of the toggle state. The checkbox label in Settings is updated to "Store main form password in Windows Credential Manager". A `ClearStoredDefaultPassword` method is added for cleanup when the toggle is disabled.

### UI Changes

- **"Run All" button removed** — `btnExecuteAll` and its click handler are removed from `Form1`. The execute panel retains "Run Selected" and "Stop All". `btnStopAll` shifts left to fill the gap.

### Flow Canvas Bridge Fix

`FlowCanvasBridge.CompareStepPathSegments` replaces string-based step path comparison with numeric-segment-aware ordering. Previously, branch first-child detection used `string.Compare` which sorted `steps/10/then/0` before `steps/2/then/0`. The new method splits paths on `/` and compares numeric segments numerically, ensuring correct ordering for scripts with 10+ top-level steps.

### Test Coverage

New test suites added:

- **Scripting** — `LocalCmdCommandTests` (foreground/background/interactive execution, confirmation flow, environment variables, timeout, output truncation), `LocalCmdParserTests` (YAML parsing of all `localcmd` options), `VaultCommandTests` (read single/multiple keys, write, patch, on_error handling, structured JSON values), `VaultFunctionsTests` (`vault`, `vault_list`, `vault_clear_cache` functions), `VaultInlineSyntaxTests` (inline `${vault(...)}` expression evaluation), `VaultParserTests` (YAML parsing of vault step options)
- **Services** — `VaultServiceTests` (auth methods, KV v1/v2 read/write/patch/list, caching, TLS, error translation), `VaultSettingsTests` (model serialization), `VaultCredentialProviderTests` (vault path parsing, credential resolution), `FlowCanvasBridgeTests` (vault/localcmd YAML round-tripping), `EnvironmentServiceTests` (vault profile propagation), `JobExecutionServiceTests` (Vault credential mode)
- **UI** — `Form1CredentialManagerPreferenceTests` (narrowed credential toggle behavior), `SettingsDialogVaultTests` (Vault tab profile management, auth panel switching, test connection), `JobEditorDialogVaultCredentialTests` (Vault credential mode in job editor), `JobEditorValidationTests` (Vault path validation)
- **Editor** — `ScriptAutocompleteProviderTests` (vault/localcmd autocomplete candidates)

---

## Changes Since `a5e5905` (0.51.12)

### Portable Release Build

A second release artifact is now supported for portable distribution:

- **Portable executable** — `SSH_Helper_Portable.exe` is published alongside `SSH_Helper.exe`.
- **Compile-time build flavor** — `PortableBuild=true` enables portable storage behavior via `PORTABLE_BUILD`.
- **Storage root behavior**
  - Standard build: `%LocalAppData%\\SSH_Helper`
  - Portable build: executable directory
- **Credential Manager scope isolation** — Credential targets are now build-flavor specific:
  - Standard build: `SSH_Helper:*`
  - Portable build: `SSH_Helper_Portable:*`
- **Portable startup guard** — Portable mode validates executable-folder write access at startup and shows a clear error if the location is not writable.
- **Portable-aware runtime paths** — Flow Canvas WebView2 user data and Scintilla native extraction now resolve from the app storage root, so they follow the selected build flavor.
- **Release workflow** — GitHub Actions now publishes both standard and portable executables and emits separate SHA256 checksum files for each.

### Flow Canvas Visual Script Editor

A complete visual script editor is introduced as a React/TypeScript application built with Vite and @xyflow/react (React Flow), hosted in a WebView2 window alongside the existing Scintilla text editor. Scripts are edited as node-based graphs with bidirectional YAML round-tripping.

**Architecture** — The editor is split across three layers:

- **React frontend** (`FlowCanvas/src/`) — Canvas with drag-and-drop block creation, property editing, debug visualization, and keyboard shortcuts. State is managed via a single Zustand store composed from 9 slices: `GraphSlice`, `ExecutionSlice`, `DebugSlice`, `VariableSlice`, `UndoSlice`, `TimelineSlice`, `UISlice`, `CommentSlice`, and `HostSlice`.
- **WinForms host** (`UI/FlowCanvasForm.cs`) — Modeless window embedding WebView2 with virtual host mapping (`flowcanvas.local`) for proper ES module support. Window size and position persist across open/close within the session and to `AppConfiguration.WindowState`.
- **Bridge** (`Services/FlowCanvasBridge.cs`) — Bidirectional converter between YAML `Script`/`ScriptStep` models and React Flow graph JSON. Each graph node stores the verbatim YAML snippet for its step, preserving comments and formatting through round-trips. Auto-layout positions nodes with configurable spacing, branch coloring (green for `then`, red for `else`/`catch`, yellow for `elif`/`case`/`loop`, blue for `finally`/`continue`), and nesting up to 5 levels deep. Export returns `FlowCanvasExportResult` with diagnostics, node-to-step-path mappings, and computed success/error/warning counts.

**Block system** — 35 block types across 7 categories (`ssh`, `control-flow`, `data`, `network`, `io`, `grid`, `timing`), each with category-driven color theming. `BaseBlock` renders execution state (running/success/error/skipped/disabled) with animated glows, duration badges, breakpoint toggles, and preview text from the block's key property. Block definitions in `blockDefs/registry.ts` declare typed properties (`text`, `number`, `boolean`, `select`, `code`, `textarea`) with groups (`core`, `advanced`, `on_error`), file browse support, and shared templates for `on_error` and `timeout`.

**Panels:**

- **Palette** — Fixed 180px sidebar with draggable category-grouped block pills. Drag sets `application/flowcanvas-block` for canvas drop handling.
- **Properties** — Right-side inspector with buffered input management to prevent stale-closure bugs during fast focus/blur. Supports variable name validation, choice option editors (source/static modes), and file browsing via MessageBus.
- **Debug Panel** — Floating overlay at bottom-left during execution. Shows running/paused state indicator, Continue/Step/Stop controls, and a monospace call stack trace with the current frame highlighted.
- **Output Preview** — Walks backward through the graph (BFS) from the selected node to find the nearest ancestor `send` or `interactive` block with captured SSH output.
- **Variable Inspector** — Live runtime variable display during execution.
- **Timeline** — Execution event history.
- **Search Overlay** — Node search with CSS-highlighted matches and keyboard navigation.
- **Toolbar** — Run (F5), Test Step, Apply YAML, undo/redo, snap-to-grid toggle, auto-layout, panel visibility toggles, and theme switch. Run is disabled when export has validation errors or no target host is selected.

**Start Block** — A mandatory `StartNode` at the top of every graph representing script-level metadata (`name`, `description`, `vars`, `imports`, `debug`, `nobanner`, `no-warn`, `library`). Renders with a dark green gradient and bright green border, displays active flag badges and variable/import counts. Protected from deletion, copy/paste, and irrelevant context menu actions. On import, preamble YAML keys are extracted into Start node properties; on export, they are emitted back as the YAML preamble.

**Container continuation handle** — Container blocks (`if`, `foreach`, `while`, `try`, `switch`, `parallel`) gain a diamond-shaped continuation handle at their bottom edge. Continuation edges are rendered as blue solid lines and are excluded from YAML export (they represent visual flow, not script structure). `onConnect` applies continuation styling automatically. Export guards prevent continuation edges from corrupting child node step-path mappings.

**Edge context menu** — Right-clicking an edge opens a context menu for deletion. Edge and block context menus are positioned at cursor coordinates and dismissed on canvas click.

**C#-to-React communication** — `MessageBus.ts` detects WebView2 (`window.chrome.webview`) for production or falls back to `window.postMessage` for development. Typed publish/subscribe API with `sendReady()` handshake. `FlowCanvasForm` queues messages in a `ConcurrentQueue` until React signals ready, then flushes. Test hook support via `window.__FLOWCANVAS_TEST_HOOKS__` for Playwright interception.

**Asset resolution** — `FlowCanvasDistLocator` resolves the React build via a three-tier strategy: `<exe>/FlowCanvas/dist` (development), project root (walking up for `.csproj`/`.sln`), or embedded assembly resources (single-file publish). Embedded extraction writes to `%LocalAppData%/SSH_Helper/flow-canvas-dist/<version>/` with incremental file skipping and `BuildTimestamp` versioning.

**Build integration** — A `BuildFlowCanvas` MSBuild target runs `npm run build` in `FlowCanvas/` before .NET compilation. The `IncludeFlowCanvasDistEmbeddedResources` target embeds all `dist/**` files as assembly resources with `SSH_Helper.Resources.FlowCanvasDist/` prefixed logical names. `.gitattributes` enforces LF line endings on `FlowCanvas/dist/**` to prevent Windows checkout churn.

### Flow Canvas Layout Persistence

Canvas node positions, comment annotations, and disabled block state persist with the preset via `CanvasLayoutData` on `PresetInfo.CanvasLayout`.

- **Structure hash gating** — `CanvasLayoutData.StructureHash` stores a SHA-256 hash of the script's block types and step paths. Saved positions are only applied when the hash matches the current script, preventing stale layouts from misaligning after structural edits. `FlowCanvasBridge.ComputeStructureHashFromYaml` computes the hash from YAML text.
- **Autosave** — Layout is autosaved to the preset after every node drag-stop. `ApplyLayoutAutosave` in `Form1` deserializes the `layout-autosave` message from React and updates `CanvasLayoutData.Positions`, `Comments`, and `DisabledBlockIds`.
- **Canvas layout state indicator** — The preset header shows layout state via `PresetHeaderIndicatorFormatter.CanvasLayoutState` (`None`, `Saved`, `WillReset`). A debounced 500ms timer checks whether the current script's structure hash still matches the stored layout.
- **Persisted model** — `CanvasLayoutData` contains `Positions` (dictionary of `NodePosition` with X/Y), `Comments` (list of `CanvasComment` with text, color, dimensions, and optional `AttachedToNodeId`), and `DisabledBlockIds` (list of node IDs skipped during execution).

### Flow Canvas Preset Sync

The main form and Flow Canvas stay synchronized as the user edits:

- **Preset load** — `LoadCurrentScriptIntoCanvas` sends the active preset's YAML to the canvas when a preset is selected.
- **Breakpoint persistence** — `_pendingBreakpoints` and `_pendingDisabledBlocks` in `Form1` carry node-level breakpoints and disabled states across preset loads and execution cycles.
- **Debug bootstrap** — `SshExecutionService` step lifecycle events (`StepStarting`, `StepCompleted`, `DebugPauseStateChanged`) are wired to `Form1` handlers that forward execution state to the canvas via `FlowCanvasForm.PostMessage`.
- **Node-to-step mapping** — Bidirectional `_nodeToStepPathMap`/`_stepPathToNodeIdMap` dictionaries enable the host to resolve canvas highlights from `StepPath` identifiers emitted by `ScriptExecutor`, with legacy integer-index fallback via `_nodeToStepIndexMap`/`_stepIndexToNodeIdMap`.

### `exists` Command

`ExistsCommand` (`Services/Scripting/Commands/ExistsCommand.cs`) checks whether a local filesystem path exists as a file, directory, or either:

```yaml
- exists:
    path: "%UserProfile%\\Documents\\hosts.txt"
    into: has_hosts
    type: file
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | - | Local path (supports `${var}` and `%NAME%` expansion) |
| `into` | Yes | - | Variable receiving `true`/`false` |
| `type` | No | `any` | Match mode: `any`, `file`, or `directory` |
| `on_error` | No | `stop` | Error handling: `continue` or `stop` |

The `path` is resolved through variable substitution, `Environment.ExpandEnvironmentVariables`, and `Path.GetFullPath`. When `into` is specified, the command also populates `${into}_meta` with `exists`, `is_file`, `is_directory`, `path`, `type`, and optionally `error`. Dynamic type resolution in `ScriptDependencyAnalyzer` detects `exists` steps as not requiring an SSH session.

### `playsound` Command

`PlaySoundCommand` (`Services/Scripting/Commands/PlaySoundCommand.cs`) plays local WAV or MP3 audio files using the NAudio library:

```yaml
- playsound:
    path: "%LocalAppData%\\SSH_Helper\\sounds\\ready.wav"
    wait: true
    volume: 65
    max_seconds: 5.0
```

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `path` | Yes | - | Local path to `.wav` or `.mp3` file |
| `wait` | No | `true` | Block until playback completes |
| `volume` | No | `100` | Playback volume 0-100 |
| `max_seconds` | No | - | Timeout in seconds (supports fractions) |
| `into` | No | - | Variable for success bool and `_meta` dict |
| `on_error` | No | `continue` | Error handling (defaults to continue, not stop) |

Playback uses `AudioFileReader` + `WaveOutEvent` with a `ManualResetEventSlim` for completion signaling. In fire-and-forget mode (`wait: false`), playback runs on a background `Task.Run` and the step returns immediately. `max_seconds` uses a timed wait with `TimeoutException` on expiry. The `${into}_meta` dictionary includes `path`, `wait`, `volume`, `backend` (`naudio`), `duration_ms`, and `error`.

### Script Editor Autocomplete

`ScriptAutocompleteProvider` (`Services/Editor/ScriptAutocompleteProvider.cs`) provides context-aware completion for YAML script editing in the Scintilla editor.

**Context detection** — Regex-based classification of cursor position into 6 `CompletionContextKind` values: `TopLevelKey`, `StepCommand`, `StepOptionKey`, `OptionValue`, `Interpolation`, and `None`. Each context produces different completion candidates.

**Built-in knowledge:**

- Command descriptions for all 33 script command types
- Top-level key descriptions (`name`, `description`, `vars`, `imports`, `subroutines`, `steps`, `debug`, etc.)
- Required option keys per command, prioritized in completion order
- Built-in symbols (`_output`, `_timestamp`, `_iteration`, `_last_error`, `_host`, `_port`, `_username`, `_password`) for `${}` and `{{}}` interpolation contexts
- Derived suffixes (`_status`, `_headers`, `_count`, `_avg`, `_min`, `_max`) for `into:`-derived variables

**Editor integration** — `ScintillaScriptEditorControl.SetAutocompleteProvider` wires the provider. Ctrl+Space triggers manual popup; auto-popup triggers on typing when `AutocompleteShowOnTyping` is enabled. Theme-aware autocomplete list colors: dark mode uses dark background with blue selection; light mode uses white with bootstrap-blue selection. Accepting a completion in `StepCommand` or `TopLevelKey` context immediately applies smart-enter indentation.

### Smart Enter Indentation

`EditorTextUtilities.ApplySmartEnter` (`Services/Editor/EditorTextUtilities.cs`) provides context-aware Enter key behavior in the script editor:

- After `- command:` lines: adds extra indentation of `2 + indentSize` spaces for the first option
- After plain `- item` lines: continues the list with `- ` prefix
- After mapping keys ending in `:`: increases indent for nested step option keys (`respond`, `headers`, `cases`, `then`, `elif`, `else`, `do`, `catch`, `finally`, `options`, `required_fields`, `columns`, `data`, `steps`, `args`, `out`); otherwise stays at current indent
- Empty lines between steps: preserves blank-line spacing when `preserveBlankLineBetweenSteps` is set
- Payload-indent heuristic for empty step bodies

`ApplyIndentation` provides block indent/outdent for multi-line selections. Both methods return `EditorTextEdit` structs with adjusted selection ranges.

### Step-Level Events and Duration Tracking

`ScriptExecutor` now fires fine-grained step lifecycle events used by both the main form and Flow Canvas:

- **`StepStarting`** — Fired before each step begins. `StepExecutionEventArgs` carries `StepIndex`, `StepPath` (scope-aware, e.g., `steps/2/then/0`), `StepType`, `LineNumber`, and `StepName`.
- **`StepCompleted`** — Fired after each step finishes. Adds `DurationMs` (wall-clock milliseconds from `Stopwatch`), `Success`, `Output`, and `Skipped`.
- **`DebugPauseStateChanged`** — Fired on pause/resume transitions. `DebugPauseStateChangedEventArgs` includes `IsPaused`, `StepPath`, `LineNumber`, and `ResumeAction`.

Disabled nodes (via `DebugState.IsNodeDisabled`) now fire `StepCompleted` with `Skipped = true` rather than being silently bypassed.

`DebugState` is upgraded with async resume signaling via `WaitForResumeAsync(CancellationToken)` using a shared `TaskCompletionSource<DebugResumeAction>`, replacing the previous 100ms polling loop. Node-ID breakpoints (`ToggleNodeBreakpoint`, `HasNodeBreakpoint`) and node disable/enable (`ToggleNodeDisabled`, `IsNodeDisabled`) are supported alongside traditional line-number breakpoints. `SetNodeToStepPathMap` populates bidirectional node-ID/step-path dictionaries for Flow Canvas integration.

### HTTP Command Debug Logging

The `http` command (`Services/Scripting/Commands/HttpCommand.cs`) now emits verbose request/response traces when `ScriptOutputType.Debug` output is enabled:

- **Pre-request**: method, URL, auth/timeout/redirect/TLS options, resolved request headers (including `Authorization`), and request body
- **Post-response**: timing details (endpoint, status, `api_ms`, `total_ms`), response status + reason phrase, response headers (JSON), and full response body

Debug output is invisible during normal execution and only appears when the script's `debug` flag is active.

### Interactive Terminal Transcript System

`InteractiveTerminalService` (`Services/Terminal/InteractiveTerminalService.cs`) gains a structured transcript system:

- **Line-capped transcript** — `AppendTranscriptWithCap` enforces a 500,000-line hard cap on transcript accumulation. Once the cap is reached, a `[... interactive transcript capped ...]` notice is inserted and further text is discarded.
- **Input resolution** — `ResolveTranscriptAssemblyInput` chooses between raw terminal data and escape-stripped captured text based on alternate-screen mode and private-mode escape sequence detection.
- **Debug tracing** — When debug mode is active, `BuildTranscriptChunkDebugMessage` emits per-chunk diagnostics tagged with phase (`capture-window`, `capture-headless`, `interactive-window`), alternate-screen transitions, and truncated representations of raw, stripped, and captured data. `ShouldEmitTranscriptChunkDebug` triggers only when the chunk contains cursor-movement, backspace, tab, or escape characters, or when stripped and captured text diverge.
- **Buffer-relative selection** — `InteractiveTerminalViewportControl` selection coordinates are computed relative to the terminal buffer, with clipboard support for copying selected text.

### Script Dependency Analyzer Enhancements

`ScriptDependencyAnalyzer` (`Services/Scripting/ScriptDependencyAnalyzer.cs`) gains two new analysis capabilities:

- **SSH requirement analysis** — `AnalyzeSshRequirements(Script)` walks all steps including nested scopes and subroutines, returning `SshRequirementResult` with `RequiresSshSession`, `UsesSftp`, `UsesInteractive`, `UsesBrowserCallbackCapture`, `SftpUsesDefaultHost`, and `SftpUsesDefaultCredentials`. Scripts that only use local commands (`exists`, `playsound`, `set`, `print`, etc.) are detected as not requiring an SSH session.
- **Preset details** — `AnalyzePresetDetails(PresetInfo)` returns `PresetColumnDependencyResult` which extends `ColumnDependencyResult` with `SuppressMissingColumnWarning`, surfacing whether a script explicitly opts out of the missing-column warning dialog.

### Extract Command Debug Output

`ExtractCommand` debug output now preserves full extracted values with newline characters shown as `\n`, replacing the previous behavior of truncating values at 50 characters.

### Path Browser Context Menu

A **Browse Path** context menu item is added to `ScintillaScriptEditorControl` for inserting file paths directly into the script editor. The path browser is also available in Flow Canvas Properties panel for fields with `browse: 'file'` type, communicating via MessageBus to the WinForms host.

### Flow Canvas E2E Test Suite

Playwright-based end-to-end tests for the Flow Canvas are added under `FlowCanvas/e2e/`:

- **Gesture smoke tests** — Canvas interaction basics (drag, drop, selection)
- **Preset parity tests** — Round-trip validation that YAML-to-canvas-to-YAML preserves script semantics
- **Properties typing tests** — Property editor input validation and persistence
- **Variable inspector tests** — Runtime variable display during execution
- **Interaction tests** — Block context menu, edge operations, undo/redo
- **Negative tests** — Error handling for invalid presets and malformed graphs

Test infrastructure includes `FlowCanvas/e2e/support/harness.ts` for WebView2 message interception, `qaPresetLoader.ts` for loading QA fixtures, and `parityCli.ts` for round-trip comparison via `FlowCanvasParityCli` (`InternalsVisibleTo` granted in `SSH_Helper.csproj`).

### Dependency Changes

| Package | Version | Purpose |
|---------|---------|---------|
| `NAudio` | 2.2.1 | In-process WAV/MP3 audio playback for `playsound` command |

### Documentation

`SCRIPTING.md` updated with:
- `exists` command with full parameter table, output variables, and file/directory/OneDrive fallback examples
- `playsound` command with parameter table, fire-and-forget vs. blocking examples, and `on_error: continue` default
- `http` debug logging section documenting verbose request/response trace output
- Path placeholder tokens (`%LocalAppData%`, `%UserProfile%`, `%TEMP%`, etc.) documented for `readfile.path` and `writefile.path`
- `extract` debug output behavior change (full values preserved, newlines shown as `\n`)
- `portcheck.host` marked as required field
- `browser_callback_capture.callback_path` changed from required to optional (defaults to `/oauth_callback`)

### Test Coverage

New test suites added:

- **Scripting** — `ExistsCommandTests` (path resolution, type filtering, metadata output, error handling), `PlaySoundCommandTests` (playback lifecycle, volume, timeout, fire-and-forget mode), `DebugStateStepPathTests` (node-ID breakpoints, step-path mapping, async resume), `ScriptExecutorDebugStepTests` (debug step-through with StepPath events), `ScriptExecutorStepPathTests` (scope-aware step-path identity across nested blocks)
- **Services** — `FlowCanvasBridgeTests` (YAML-to-graph round-tripping, auto-layout, export diagnostics, structure hash), `SshExecutionServiceFlowCanvasDebugBootstrapTests` (debug bootstrap with node-to-step-path mapping)
- **UI** — `Form1FlowCanvasBreakpointPersistenceTests` (breakpoint state across preset loads), `Form1FlowCanvasBrowsePathTests` (path browser integration), `Form1FlowCanvasPresetSyncTests` (canvas-preset synchronization), `Form1FlowCanvasTestStepScopingTests` (test-step prerequisite slicing), `Form1ScriptContextMenuTests` (context menu wiring), `InteractiveTerminalFormTests` (terminal form lifecycle), `ScintillaScriptEditorControlTests` (autocomplete integration, smart enter, context menu passthrough)
- **Utilities** — `FlowCanvasDistLocatorTests` (three-tier resolution, embedded extraction, project root detection)
- **Editor** — `ScriptAutocompleteProviderTests` (context detection, completion candidates), `EditorTextUtilitiesTests` (smart enter indentation, block indent/outdent)

---

## Changes Since `f7d3ac5` (0.51.10)

### Required Field Alignment (Parser, Export, and Flow Canvas)

- Parser validation now enforces missing required checks for `choose.into/options`, `multiselect.into/options`, `confirm.into`, `webhook.url`, and `log.message`.
- Flow Canvas export required-option checks are now parser-led:
  - Added enforcement for `extract.from` and `browser_callback_capture.into`.
  - Removed incorrect hard requirements for `input.prompt`, `choose.prompt`, `multiselect.prompt`, `confirm.prompt`, `portcheck.port`, and `writefile.content`.
  - Added conditional required enforcement for `readfile.path` (`select_file` aware), HTTP auth credentials (`basic`/`bearer`), and headless interactive constraints (`show_window=false`).
- Flow Canvas Properties `*` markers now evaluate requiredness dynamically for conditional fields while preserving existing visual styling.

### Flow Canvas Correctness Recovery (Partial Rollout)

Flow Canvas execution/export/debug contracts were hardened for correctness and loss prevention across the WinForms host and ReactFlow surface:

- **Unified execution trigger**: Run/Test now route through `execute-canvas` with graph payload, replacing split trigger paths and eliminating keyboard/toolbar drift.
- **Structured export diagnostics**: Host now returns `apply-result` with `success`, `errors`, `warnings`, and `nodeStepMap`; invalid exports block run/test instead of silently proceeding.
- **StepPath runtime identity**: Executor now emits scope-aware `StepPath` on step lifecycle and debug pause/resume events, and host resolves canvas highlights via `StepPath -> nodeId` (with legacy index fallback).
- **Debug bootstrap mapping update**: Active debug state now receives node-to-step-path mapping instead of index-only mapping.
- **Interaction fixes in FlowCanvas UI**:
  - Move undo snapshots captured at drag start
  - Breakpoint visual toggle parity fixed
  - Right-click context menu separated from breakpoint toggle gesture
  - Comments promoted to persistent ReactFlow nodes
  - Store selection synchronized with ReactFlow multi/box selection changes
- **Bridge/export hardening**:
  - Export now surfaces explicit diagnostics for unsupported nodes
  - Comment nodes are explicitly ignored with warnings
  - Child node step-path mappings are preserved for nested debug correlation

Known follow-up scope (not included in this pass):
- Deeply nested test-step prerequisite slicing still needs strict branch-level pruning.

### Comprehensive Scripting Function Library (55+ Built-in Functions)

The scripting language gains a full-featured expression and function system built on a new `FunctionRegistry` singleton (`Services/Scripting/FunctionRegistry.cs`) with category-based registration via `IFunctionCategory`. Six function categories are implemented, each in its own class under `Services/Scripting/Functions/`:

**Math** (`MathFunctions`) — `abs`, `min`, `max`, `round`, `floor`, `ceil`, `random`, `pow`, `sqrt`, `clamp`, `iif`

**String** (`StringFunctions`) — `contains`, `startswith`, `endswith`, `pad_left`, `pad_right`, `repeat`, `reverse`, `regex_replace`, `format`, `char_at`, `index_of`

**Collection** (`CollectionFunctions`) — `map`, `filter`, `reduce`, `find`, `any`, `all`, `count`, `range`, `slice`, `flatten`, `zip`

**DateTime** (`DateTimeFunctions`) — `now`, `epoch`, `epoch_to_date`, `date_add`, `date_diff`, `date_format`

**Encoding** (`EncodingFunctions`) — `base64_encode`, `base64_decode`, `url_encode`, `url_decode`, `hash` (SHA256/MD5/SHA1/SHA512), `hex_encode`, `hex_decode`

**Type** (`TypeFunctions`) — `int`, `float`, `str`, `bool`, `typeof`, `is_number`, `is_list`, `is_json`, `is_empty`

Functions are dispatched by name through `FunctionRegistry.TryEvaluate` and are case-insensitive. All functions accept a raw argument string and the current `ScriptContext`, enabling variable resolution within arguments.

### Inline Function Expressions and Expression Parser

A new unified recursive-descent `ExpressionParser` (`Services/Scripting/ExpressionParser.cs`) replaces the previous `ArithmeticParser`. The grammar supports:

- **Arithmetic**: `+`, `-`, `*`, `/`, `%` with standard precedence
- **Comparison**: `==`, `!=`, `<`, `>`, `<=`, `>=`
- **Ternary**: `condition ? trueVal : falseVal`
- **Null coalescing**: `value ?? fallback`
- **Unary**: `-x`, `+x`
- **Nested function calls**: `upper(trim(name))`
- **String literals**: `'single'` and `"double"` quoted
- **Variable references**: resolved from `ScriptContext`

**Lambda expressions** (`LambdaExpression`) enable arrow-style inline functions for collection operations:

```yaml
- set:
    filtered: "${filter(items, x => x > 10)}"
    totals: "${reduce(values, (acc, x) => acc + x, 0)}"
```

`LambdaExpression.TryParse` handles both single-parameter (`x => body`) and multi-parameter (`(acc, x) => body`) forms. Lambda evaluation saves and restores existing variables to avoid scope leakage.

### Browser Callback Capture Command

`browser_callback_capture` is a new scripting command (`Services/Scripting/Commands/BrowserCallbackCaptureCommand.cs`) that captures localhost callback values from browser-driven OAuth/SSO flows:

```yaml
- browser_callback_capture:
    start_url: "https://idp.example.com/auth?redirect_uri=http://127.0.0.1:8086/oauth_callback"
    callback_path: "/oauth_callback"
    local_port: 8086
    capture_mode: auto       # auto | query | fragment | body
    browser_mode: external   # external | webview2
    into: callback
    required_fields: ["access_token"]
    timeout: 300
    show_after_seconds: 2
```

The command starts an `HttpListener` on the specified local port, opens a browser (external system browser or embedded WebView2), and waits for the callback URL to be hit. Captured fields are stored as variables in the script context.

**WebView2 mode** — `BrowserCallbackWebViewDialog` (`UI/BrowserCallbackWebViewDialog.cs`) provides an embedded WebView2 browser window with dark mode support. `BrowserCallbackWebViewProfileManager` manages a shared user data directory under `%LocalAppData%\SSH_Helper\WebView2\BrowserCallback\` with session-aware lifecycle and a `ClearEmbeddedBrowserData` method in Settings. `BrowserCallbackUiHost` (`Services/Scripting/BrowserCallbackUiHost.cs`) coordinates UI session lifecycle, supporting `show_after_seconds` delayed display and `keep_window_open_on_success`.

**Focus restoration** — `BrowserCallbackFocusRestorer` (`Services/Scripting/Commands/BrowserCallbackFocusRestorer.cs`) restores application focus after browser callback completion using Win32 `AttachThreadInput`/`SetForegroundWindow` with a retry loop at 350ms, 650ms, 1000ms, 1500ms, and 2200ms intervals.

**Template literals** — The capture command supports template literal syntax for constructing dynamic URLs with variable substitution and cleanup logic for stale capture fields.

### Session-Scoped Undo for Preset and Folder Deletes

`PresetDeleteUndoService` (`Services/PresetDeleteUndoService.cs`) provides a session-scoped undo stack (max 50 entries) for preset and folder deletions. On delete, the service records a `PresetDeleteUndoEntry` containing:

- The target name and whether it is a folder
- A `PresetLibrarySnapshot` of the full preset/folder tree before deletion
- Deep-cloned copies of any `JobDefinition` objects that referenced the deleted item

`UndoLatest` restores the library snapshot via `PresetManager.RestoreLibrarySnapshot` and re-enables affected scheduler jobs via `JobStorageService.RestoreSnapshots`. The undo stack is cleared on application exit (session-scoped, not persistent).

### Connection Test Visual State in Row Headers

Connection test results now display visual indicators directly in DataGridView row headers for selected hosts. `ApplyConnectionTestCellResult` colors both the `Host_IP` cell and the row header based on `ConnectionTestResult` (success/failure with timing). Colors are theme-aware and regenerated on theme changes via `ApplyTheme`. `ClearConnectionTestIndicators` resets row header state. The progress bar and status label track test completion, with guards against queued per-host progress callbacks overwriting the final completion status.

### Preset and Favorites Tab Selection Sync

`PresetTabHeaderStrip` (`UI/PresetTabHeaderStrip.cs`) is a custom owner-drawn tab header control for switching between Presets and Favorites views. Selection state synchronizes across tab switches — switching to Favorites preserves the selected preset, and switching back restores it. The control features dark-mode styling, hover effects, and a blue accent underline on the selected tab.

### Startup Performance Optimizations

Three targeted optimizations reduce application startup time:

- **Deferred scheduler bootstrap** — `JobExecutionService` initialization is deferred until after the main form is visible, preventing scheduler tick evaluation from blocking the startup path
- **Batched host grid restore** — `HostGridRestoreBatcher` (`UI/HostGridRestoreBatcher.cs`) batches scrollbar refreshes, host count updates, and dirty-marking during grid restoration using `IDisposable` scoped batches (`BeginRestoreScope`/`BeginMutationScope`). Pending operations flush once when the outermost scope ends
- **Reused startup config snapshot** — The configuration snapshot read during startup is reused across consumers rather than re-reading `config.json` multiple times

### UI Flicker Reduction

`BufferedPanel` and `BufferedSplitContainer` (`UI/BufferedPanel.cs`, `UI/BufferedSplitContainer.cs`) are double-buffered WinForms control subclasses that suppress `WM_ERASEBKGND` when all child controls are fully opaque. These replace standard `Panel` and `SplitContainer` in the main form layout to eliminate visible flicker during resize and layout operations. `ScintillaScriptEditorControl` receives additional painting and resize optimizations.

### Incremental Preset Tree Mutations

The presets `TreeView` now supports incremental add/rename/delete operations that preserve expand/collapse state and selection, rather than rebuilding the entire tree on every change. The add-preset button visibility is corrected for edge cases.

### Enhanced Error Handling and Scripting Improvements

- **`SetCommand` hyphenated values** — `set:` correctly handles right-hand-side values containing hyphens (e.g., `name: "my-value"`) without interpreting the hyphen as an arithmetic operator
- **`SendCommand` improvements** — Enhanced error handling for malformed YAML preprocessing and validation in `ScriptParser`
- **`random()` edge cases** — `random(n, n)` returns `n` instead of throwing; `random()` with no args returns a random integer in the default range
- **`iif()` function** — `iif(condition, trueVal, falseVal)` provides inline conditional evaluation as an alternative to the ternary operator in expressions

### Dependency Changes

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Web.WebView2` | 1.0.3124.44 | Embedded browser for `browser_callback_capture` WebView2 mode |

The `WebView2LoaderPreference` is set to `Static` in the project file. A custom MSBuild target (`RemoveWebView2WpfReference`) strips the WPF assembly reference since this is a WinForms-only project.

### Documentation

`SCRIPTING.md` updated with:
- `browser_callback_capture` command with full option table, capture modes, browser modes, and examples
- Complete function reference tables for all 55+ built-in functions organized by category (Math, String, Collection, DateTime, Encoding, Type)
- Lambda expression syntax documentation with `map`, `filter`, `reduce` examples
- Inline function expression syntax in `print` and `set` commands
- Service restart command examples with `fail_on_nonzero`

New script samples:
- `ScriptSamples/browser_callback_self_contained_presets.json` — Self-contained preset examples for browser callback workflows

### Test Coverage

New test suites added:

- **Scripting** — `BrowserCallbackCaptureCommandTests` (listener lifecycle, capture modes, timeout, required fields), `BrowserCallbackSelfContainedPresetTests` (preset integration), `CollectionFunctionTests` (map/filter/reduce/find/any/all/count/range/slice/flatten/zip with lambdas), `DateTimeFunctionTests` (now/epoch/date_add/date_diff/date_format), `EncodingFunctionTests` (base64/url/hex encoding, hash algorithms), `ExpressionParserTests` (arithmetic, comparison, ternary, null coalescing, nested functions), `FunctionRegistryTests` (registration, dispatch, case insensitivity), `MathFunctionTests` (all math functions including edge cases), `StringFunctionTests` (contains/startswith/endswith/pad/repeat/reverse/regex_replace/format/char_at/index_of), `TypeFunctionTests` (conversion and type inspection), `QaPresetExecutionTests` (end-to-end QA fixture validation)
- **Services** — `PresetDeleteUndoServiceTests` (record/undo/clear lifecycle, max entries, job restoration), `PresetManagerDeleteBehaviorTests` (delete behavior with undo integration), `BrowserCallbackUiHostTests` (UI session lifecycle), `BrowserCallbackWebViewProfileManagerTests` (profile directory management, session counting, clear blocking)
- **UI** — `BorderlessTabControlTests` (custom tab control rendering), `BrowserCallbackFocusRestorerTests` (focus restoration with native method mocking), `BrowserCallbackWebViewDialogTests` (dialog lifecycle), `BufferedContainerControlTests` (double-buffering and WM_ERASEBKGND suppression), `Form1BufferedSurfacesTests` (main form buffered control verification), `Form1ConnectionTestStatusTests` (connection test visual state, progress handling, theme reapplication), `Form1DeleteUndoTests` (undo UI integration), `Form1PresetTabSelectionTests` (tab sync across Presets/Favorites), `Form1PresetTreeIncrementalMutationTests` (incremental tree operations), `HistoryListBoxTests` (variable-height list rendering), `HostGridRestoreBatcherTests` (batched restore/mutation scopes), `SettingsDialogBrowserCallbackTests` (WebView2 data clear integration)

---

## Changes Since `729f4e6` (0.51.8)

### Job Scheduler

A complete job scheduling system enables unattended execution of presets and folders on cron schedules, one-time schedules, or on-demand via Run Now. The scheduler is built across five service layers with a full UI integration.

**Job Definitions** — `JobDefinition` is the core model, storing a GUID-based ID, name, enabled flag, target (preset, folder, or custom inline commands via `JobTargetType`), host grid data, cron expression or one-time UTC datetime, credential mode (`InheritFromApp`, `Stored`, `PerHostColumn`), timeout overrides, and history retention overrides. `ContentHasher` computes SHA256 hashes of preset content stored on the job definition to detect drift between saved jobs and current presets.

**Job Storage** — `JobStorageService` provides CRUD operations for `JobDefinition` objects backed by `jobs.json` in `%LocalAppData%\SSH_Helper`. Enforces unique case-insensitive names (max 100 characters), writes with `.bak` backup on every save, handles corrupt-file backup on load, fires `JobsChanged` events, and provides reverse-lookup methods (`GetJobsReferencingPreset`, `GetJobsReferencingFolder`).

**Scheduling Engine** — `SchedulingService` is a pure logic service (no timer, no execution) built on the Cronos library. Parses and validates cron expressions, generates human-readable descriptions via `CronExpressionDescriptor`, computes next-run times, and detects missed runs at startup. `MarkOneTimeCompleted` auto-disables one-time jobs after successful execution.

**Execution Pipeline** — `JobExecutionService` is a timer-driven scheduler (30-second evaluation tick) with configurable concurrency via `SemaphoreSlim` (default 3 parallel jobs, controlled by `AppConfiguration.MaxConcurrentJobs`) and FIFO overflow queue. Each running job has its own `CancellationTokenSource` so `CancelJob(jobId)` cancels only that job. Crash recovery via `Initialize()` clears orphaned `RunningJobState` markers from previous sessions. `RunNowAsync` triggers immediate out-of-schedule execution, bypassing the semaphore. Folder jobs support sequential or parallel execution modes. Raises `JobStateChanged` and `JobCompleted` events.

**Execution History** — `JobHistoryService` persists, queries, prunes, and searches job run history. Each job gets a subdirectory (`job-history/{jobId}/`) with `index.json` and individual `{runId}.json` payload files containing per-host output. Features include consecutive-failure deduplication (collapses repeated identical failures into one record), per-host output truncation (default 1 MB), age-based and count-based retention pruning (defaults: 50 max runs, 30 retention days via `JobHistoryRetentionOptions`), skipped-run recording for missed schedules, and full-text search across host outputs. `SchedulerHistoryPolicyResolver` resolves effective retention options using a three-tier priority: job-level override > global app config > built-in defaults.

**Job Export/Import** — `JobExportService` serializes job definitions to `.sshjobs` JSON files and GZip+Base64 clipboard strings. On export, credentials are stripped and `RunningState` is cleared. On import, new GUIDs are generated and conflicting names receive deterministic `(imported)` / `(imported N)` suffixes.

**Preset Reference Integrity** — `PresetManager` now accepts an optional `JobStorageService` dependency. On preset **rename**, all jobs referencing the old name have their `TargetName` updated automatically. On preset or folder **delete**, all referencing jobs are auto-disabled with a `DisabledReason` message. `PresetSaveImpactResolver` computes which jobs are affected when a preset is saved, enabling the UI to warn the user before saving a preset with dependent scheduled jobs.

### Job Scheduler UI

Four new dialogs and a custom control provide the scheduler's user interface.

**Job Editor** — `JobEditorDialog` is a five-tab modal dialog for creating and editing jobs:
- **General** — Name, target type (Preset/Folder/Custom), target dropdown, schedule type (None/Recurring/OneTime) with inline `CronBuilderControl` or `DateTimePicker`
- **Content** — Scintilla-based YAML/command editor for custom preset content with syntax highlighting and autocomplete
- **Hosts** — DataGridView with toolbar for add/remove/import CSV, copy from main grid, paste from clipboard
- **Credentials** — Three-way radio: inherit from app, stored credentials via Windows Credential Manager (`JobPasswordTarget`), or per-host column mapping
- **Advanced** — Folder execution mode (sequential/parallel), stop-on-error, command/connection timeout overrides, history retention overrides

Validation is handled by `JobEditorValidator`, a pure static class that validates job name, target selection, cron expression, one-time date (must be future), host list (at least one non-empty `Host_IP`), stored credentials, per-host credentials (columns present and populated), and timeout overrides.

**Job List** — `JobListDialog` is the primary job management dashboard (1000x700, resizable). Split-panel layout: top panel shows all jobs in a `DataGridView` (name, target, schedule, last run, next run, status, enabled), bottom panel shows run history for the selected job. Toolbar and context menu provide New, Edit, Run Now, Cancel, Enable/Disable, Delete, Duplicate, Export/Import. History panel has View Output and Clear History buttons. Refreshes on a 5-second timer, preserving selection across refreshes.

**Import Preview** — `ImportPreviewDialog` shows all jobs about to be imported in a grid with Import checkbox, Name, Schedule, Target, and Status columns. Status cells are color-coded: green for OK, amber for renamed conflicts, red for missing targets. A summary label shows "N of M jobs selected."

**Run Output Viewer** — `RunOutputViewerDialog` displays historical per-host SSH output from a single job run. Host selector dropdown (with OK/FAIL/CANCELLED indicators), monospace `RichTextBox` output area, Find button with collapsible search bar (`Ctrl+F`), Copy All button, and bidirectional text search with wrap-around.

**Cron Builder Control** — `CronBuilderControl` is a self-contained `UserControl` for building cron expressions. Features 10 preset template buttons (Every 5 min through Quarterly), 5-field dropdown selectors (Minute, Hour, Day-of-Month, Month, Day-of-Week) each with full value ranges, a raw monospace `TextBox` with bidirectional sync to/from dropdowns, a human-readable description label, a next-run preview label (local time), and inline validation error display. Supports dark/light theming.

**Main Form Integration** — `Form1.InitializeSchedulerServices` instantiates the full scheduler stack and wires it together. A clickable `ToolStripStatusLabel` in the status strip and a **Scheduler** menu item open the `JobListDialog` as a modeless single-instance dialog via `ModelessDialogManager<T>`. A 5-second timer drives `UpdateSchedulerStatusBar`, showing active job count and countdown to next scheduled run. `CleanupSchedulerServices` handles disposal on app close, writing `LastAppShutdownUtc` to config for missed-run detection on next startup.

### Script Subroutines and Libraries

Scripts now support reusable subroutines defined locally or imported from external library files.

**Defining subroutines** — A `subroutines:` top-level section declares named subroutines with typed `params`, declared `outputs`, and a `steps` list:

```yaml
subroutines:
  normalize_csv:
    params: [raw_values]
    outputs: [normalized]
    steps:
      - set:
          normalized: "${distinct(compact(trim_all(split(raw_values, ','))))}"
```

**Importing libraries** — An `imports:` section loads external `.yaml` files marked with `library: true`. Each import specifies an absolute `path` and an `as` alias for qualified name resolution:

```yaml
imports:
  - path: "C:\\Path\\To\\SSH_Helper\\ScriptSamples\\libraries\\string_sections.yaml"
    as: common

steps:
  - call:
      subroutine: common.normalize_csv
      args:
        raw_values: "a, b, , a, c"
      out:
        normalized: clean_values
```

**Call semantics** — `CallCommand` invokes subroutines in an isolated child variable scope. Arguments are expression-resolved before scope entry. Declared outputs are copied back to the caller on return. `on_error: continue` suppresses subroutine failures. A call depth limit of 32 prevents unbounded recursion. `ScriptSubroutineRegistryBuilder` validates all call sites at parse time: target existence, required params provided, no unknown args, no unknown out bindings, and DFS cycle detection over local call graphs.

**Return** — `ReturnCommand` unwinds the subroutine call stack without terminating the entire script. Already-assigned declared outputs are copied back to the caller on return.

**Runtime model** — `ScriptSubroutineRegistry` holds local definitions plus imported libraries, resolving bare names vs. `alias.name` qualified names. `ScriptContext.CreateChildScope` populates `CallDepth`, `SubroutineRegistry`, and `CurrentSubroutine` on each child scope.

### Send Command Failure Detection

`send` now supports opt-in exit-status detection via `fail_on_nonzero: true`. When enabled, the command is wrapped with a sentinel-based exit-code extraction pattern:

```yaml
- send:
    command: "systemctl restart nginx"
    fail_on_nonzero: true
```

The wrapper appends `eval '...'; __ssh_helper_send_status=$?; printf '\n<SENTINEL>:%s\n' "$__ssh_helper_send_status"` and `TryExtractExitStatus` strips the sentinel from output, returning `Ok()` for exit code 0 or failing the step with `"Command exited with status N"`. The feature is restricted to prompt-waiting steps without `expect` or `respond`, and is POSIX-shell-oriented only.

### Enhanced Manual Execution Progress

Manual execution of folders and multi-host runs now reports progress in the status bar. `ManualExecutionStatusProgress.ShouldShowProgress` returns true when total operations exceed 1, and `Advance` computes a percentage string like `"Running... 42%"` using monotonic progress tracking. A `_manualExecutionProgressRunId` guard prevents stale updates from racing runs.

### Variable-Height History List

`HistoryListBox` is a new `ListBox` subclass supporting variable-height owner-drawn items. `HistoryListLayout` provides pure static layout math: item height computed from wrapped text (capped at `MaxVisibleLines = 3`), draw rectangle with `HorizontalPadding = 4` and `VerticalPadding = 4`, and line height via `TextRenderer` with fallback. `HistoryListCollectionUpdater` handles newest-first insertion with duplicate removal and tail trimming. `HistoryStartupSelectionHydration` decides whether an already-selected history entry should be hydrated during startup.

### Improved Drag-and-Drop in Presets TreeView

The preset tree drag-and-drop system is overhauled with precise drop position targeting. A `DropPosition` enum (`None`, `Above`, `Inside`, `Below`) replaces the previous single background-highlight approach. `GetDropPosition` computes position from the cursor's relative Y offset within node bounds (top 25% = Above, bottom 25% = Below, middle = Inside for folders). Drop indicators are rendered via `trvPresets.Invalidate()` rather than mutating `BackColor`. Drop handling is split into `HandleDropOnEmptySpace`, `HandleDropInside`, and `HandleDropAdjacentTo`, supporting precise above/below insertion in addition to folder nesting.

### Host Grid Snapshot and Unsaved Indicator

`HostGridUtilities` builds immutable `HostGridSnapshot` objects (column + row data) for comparing grid state. `IsHostsGridUnsaved()` now compares the live grid against a stored snapshot rather than relying solely on the `_csvDirty` flag, correctly detecting when minor round-trip changes produce no real difference.

### Autosave on Environment Switch

`TrySwitchEnvironment` now unconditionally calls `SaveCurrentGridToEnvironment` when switching environments, eliminating the save/discard/cancel prompt that previously appeared when the grid had unsaved changes.

### CSV Save Improvements

A `CsvSaveAttemptResult` enum (`Saved`, `Cancelled`, `Failed`) replaces raw boolean returns from save operations. The form-closing path uses `TryResolvePendingCsvChangesForExit` which handles all three outcomes, including a fallback "exit without saving?" prompt. `CsvManager.ParseCsvLine` adds a proper RFC-4180-style single-line CSV parser for use by `JobStorageService` and other consumers.

### Cancellation Handling Improvements

**Per-job cancellation** — `JobExecutionService` stores per-job `CancellationTokenSource` instances so `CancelJob(jobId)` cancels only that specific job without affecting other running jobs.

**Manual cancellation tracking** — `_manualCancellationRequested` tracks whether the user explicitly clicked Cancel during manual execution. `WasCancelled` propagates through `ExecutionResult`, history details, and history list icons.

**Scheduler file picker suppression** — `SshExecutionService.ExecutePresetAsync` accepts an `allowFileSelectionDialogs` parameter (default `true`) so the scheduler can suppress interactive file picker dialogs during automated runs.

### Job Duplication with Credential Copying

Jobs can be duplicated from the Job List dialog. The duplicate receives a new GUID and a `(copy)` name suffix. If the source job uses stored credentials, the credential is read from Windows Credential Manager and written to a new target keyed to the duplicate's job ID.

### Expression Evaluation Improvements

**Indexed variable access** — `ValueResolver.TryResolveIndexedExpressionValue` enables `varname[i]` expressions in conditions and `set:` right-hand sides without requiring `${varname[i]}` substitution syntax. An out-of-bounds index returns `null` without throwing. The index itself can be a variable.

**JSON expression normalization** — `JsonUtilities.TryEvaluateJsonExpression` accepts a `normalizeStructured` parameter. When `false`, JSON expression results are not coerced back to `JsonElement`, preventing a double-parse round-trip that was corrupting structured values in condition evaluation contexts.

**Empty source handling in extract** — `ExtractCommand` now initializes all `into` targets to empty string (or list of empty strings for multi-variable captures) when the source variable resolves to empty or null, rather than leaving targets in their prior state.

### Unsaved Preset Diff Dialog Enhancements

`UnsavedPresetDiffDialog` now accepts an optional `PresetSaveImpact` and `PresetSavePromptMode` to show an "affected scheduler jobs" impact panel with count label, summary, and expandable list box. New `PresetSaveImpactAction` enum (`Cancel`, `SaveExisting`, `RenameExisting`, `CreateNew`, `Discard`) and four prompt modes replace raw `DialogResult` returns.

### Dialog Ownership Fixes

All `DialogTheme.Show(...)` calls in `SettingsDialog` and `EnvironmentDialog` now pass `this` as the owner form parameter, fixing modality/z-order issues where dialogs could appear behind the parent window.

### Streaming Terminal Output Buffer

`TerminalOutputProcessor.BufferIncompleteFinalLineStreaming` is a stateful streaming buffer that holds back the last incomplete line of live terminal output so backspace/CR edits can resolve before text is committed to an append-only UI surface. `StripPagerArtifacts` uses `ReferenceEquals` comparison after `Regex.Replace` to detect whether a replacement actually occurred, avoiding a redundant `IsMatch` call.

### Utility Extractions

Several shared utilities are extracted from duplicated inline code into reusable classes:

| Utility | Extracted From | Purpose |
|---------|---------------|---------|
| `AppDataPaths` | Multiple services | Centralized `%LocalAppData%\SSH_Helper` path resolution |
| `GZipBase64Utility` | `PresetManager`, `ConfigurationService` | Shared GZip + Base64 compress/decompress |
| `JsonFileWriter` | `HistoryStorageService`, `ConfigurationService` | Atomic JSON file writes with temp-file swap and `.bak` backup |
| `HostGridUtilities` | `Form1` | DataGridView snapshot, comparison, clipboard, paste, and column utilities |
| `ModelessDialogManager<T>` | Inline code | Generic single-instance modeless dialog lifecycle management |

### Dependency Changes

| Package | Version | Purpose |
|---------|---------|---------|
| `Cronos` | 0.11.1 | Cron expression parsing and next-occurrence calculation |
| `CronExpressionDescriptor` | 2.45.0 | Human-readable cron expression descriptions |

### Documentation

`SCRIPTING.md` updated with:
- `call` and `return` commands with full parameter tables, scope isolation semantics, and examples
- `library: true` top-level flag and `imports:` section for external library loading
- `subroutines:` section for defining reusable named subroutines with params and outputs
- `fail_on_nonzero` option for the `send` command with POSIX-shell restriction notes
- `call` and `return` added to the step-keyword auto-detection list and command table

New script samples added:
- `ScriptSamples/libraries/string_sections.yaml` — Reference library demonstrating `print_section` and `normalize_csv_values` subroutines
- `ScriptSamples/generic/library_import_demo.yaml` — End-to-end import and call workflow
- `ScriptSamples/qa/catalog_library.yaml` and `catalog_runner.yaml` — QA fixture library and runner for automated testing

### Test Coverage

New test suites added:

- **Models** — `JobDefinitionTests` (model construction, enums, credential modes), `ExecutionPipelineModelTests` (queue, running state, skipped run models), `MaxConcurrentJobsTests` (configuration validation)
- **Services** — `JobStorageServiceTests` (CRUD, persistence, reverse lookup), `SchedulingServiceTests` (cron validation, next-run, descriptions), `SchedulingServiceMissedRunIntegrationTests` (missed-run detection, one-time completion), `JobExecutionServiceTests` (timer-driven execution, concurrency, crash recovery, cancellation), `JobHistoryServiceTests` (save, query, prune, search, consecutive-failure dedup), `JobExportServiceTests` (export/import, conflict naming, credential stripping), `PresetManagerJobReferenceTests` (rename propagation, delete disable), `SchedulerHistoryPolicyResolverTests` (three-tier resolution), `SshExecutionServiceCancellationTests` (cancellation token propagation), `SshExecutionServiceProgressTests` (folder progress reporting)
- **Scripting** — `ScriptSubroutineParserValidationTests` (subroutine/import parsing), `ScriptSubroutineExecutionTests` (call/return execution, scope isolation), `ScriptSubroutineDependencyAnalyzerTests` (cycle detection, call-site validation), `ScriptSubroutineEditorTests` (editor integration), `SendCommandTests` (fail_on_nonzero sentinel extraction), `QaPresetCatalogTests` (QA fixture coverage), `ExtractCommandTests` (empty source initialization)
- **UI** — `CronBuilderControlTests` (bidirectional sync, preset templates), `CronBuilderControlLayoutTests` (layout and theming), `JobEditorDialogLayoutTests` (tab structure, controls), `JobEditorDialogCustomPresetTests` (custom preset editing), `JobEditorDialogHostGridParityTests` (host grid copy/paste), `JobEditorDialogStoredCredentialTests` (credential mode switching), `JobEditorDialogTimeoutOverrideTests` (timeout override fields), `JobEditorValidationTests` (full validation chain), `JobListDialogRunNowTests` (Run Now flow), `HistoryListCollectionUpdaterTests` (insert, dedup, trim), `HistoryListLayoutTests` (height calculation, text bounds), `HistoryStartupSelectionHydrationTests` (startup restore), `HostGridUtilitiesTests` (snapshot, comparison, clipboard), `ManualExecutionStatusProgressTests` (progress percentage), `ModelessDialogManagerTests` (single-instance lifecycle), `SchedulerNotificationTests` (status bar and completion formatting), `UnsavedPresetDiffDialogTests` (impact panel, action enum), `ScriptPromptDialogRunnerTests` (prompt dialog flows), `ScriptReadFileOpenPathDialogTests` (file picker suppression)
- **Utilities** — `ContentHasherTests` (SHA256 hashing), `InputValidatorCronTests` (cron validation), `PresetSaveImpactResolverTests` (affected job resolution), `SchedulerJobIntegrityUtilitiesTests` (import disable, credential hints)

---

## Changes Since `12c1b7f` (0.51.7)

### Readfile Manual File Picker

`readfile` now supports `select_file: true` to let the operator choose the input file at runtime instead of hard-coding `path`.

- `path` remains required for normal `readfile` usage, but becomes optional when `select_file: true`
- If `path` is supplied with `select_file: true`, it is used only to seed the picker
- Scheduler executions, including Job List `Run Now`, do not open the picker and fail cleanly with a manual-only message
- Cancelling the picker sets the target `into` variable to an empty list and stops the script immediately

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
