# Scripting Core Map — `Services/Scripting/*` + `Services/Scripting/Models/*`

Audit briefing for the YAML scripting engine core (execution model, parsing, validation, expression
language, variable resolution, debugging). Command handlers (`Commands/`) and function categories
(`Functions/`) are separate audit areas; this map covers the engine that hosts them.

File inventory (root, LOC): ScriptParser 5401 · ScriptStep (Models) 1642 · ScriptDependencyAnalyzer 1224 ·
ScriptContext 1129 · JsonUtilities 1026 · ScriptExecutor 670 · ValueResolver 562 · BrowserCallbackUiHost 554 ·
ExpressionParser 539 · ExpressionEvaluator 419 · ScriptSubroutineRegistryBuilder 362 · JsonPathNavigator 317 ·
DebugState 260 · LambdaExpression 161 · Script 157 · ScriptFileAccessValidator 135 ·
ScriptSubroutineRegistry 134 · HistoryLabelOperation 104 · FunctionRegistry 104 ·
BrowserCallbackWebViewProfileManager 96 · VaultStepOptions 55 · ScriptValidationFormatter 28 ·
ScriptRegexDefaults 12 · IterationFrame 10.

---

## 1. Feature inventory

### 1.1 Script document model (`Models/Script.cs`)
- `Script` holds top-level YAML: `Name/Description/Version/Environment`, behavior flags
  `Debug`, `NoBanner`, `CompactErrors`, `SuppressMissingColumnWarning`, `Library` (Script.cs:24-66),
  `Vars` defaults (71), `Imports` (76), `Subroutines` (81), `Preconnect` bootstrap steps (88),
  `Steps` (93), and the resolved `SubroutineRegistry` (98 — built during *validation*, reused at runtime).
- `DeclaredTopLevelKeys` (Script.cs:14) records key presence for presence-based validation
  (e.g. library files may not declare `steps`, ScriptParser.cs:4247-4266).
- `ScriptImport` (path + alias, absolute path required) and `ScriptSubroutine`
  (params/outputs/steps + per-definition ParseErrors) at Script.cs:104-156.

### 1.2 Step model (`Models/ScriptStep.cs`)
- One `ScriptStep` class is the union of all 43 commands: scalar commands (`Send`, `Print`, `Set`,
  `Exit`, `If`, `Foreach`, `While`, `Until`, `Switch`) plus typed option objects
  (`ExtractOptions`, `ReadfileOptions`, `HttpOptions`, `SftpOptions`, `LocalCmdOptions`,
  `VaultStepOptions`, `NotifyOptions`, `InteractiveOptions`, etc. — ScriptStep.cs:52-370, 449-1640).
- Common per-step options honored by the executor: `capture`, `suppress`, `expect`, `timeout`,
  `on_error` ("stop"/"continue"), `retry`/`retry_delay`, universal `when:` guard, `max_iterations`
  (ScriptStep.cs:277-355).
- `GetStepType()` (ScriptStep.cs:375-422) infers the type from whichever property is populated,
  falling back to `DeclaredStepType` — first-match priority order; an empty `send:` string
  falls through to `DeclaredStepType` set by the parser.
- `StepType` enum has 43 members (ScriptStep.cs:1400-1446). Adding a command = enum member +
  parser case (ScriptParser.cs:919-1155) + executor dictionary entry (ScriptExecutor.cs:128-173) +
  KnownStepKeys (ScriptParser.cs:24-69) + CommandOptionKeys (106-149). Five manual registration
  points that must stay in sync.

### 1.3 Parsing (`ScriptParser.cs`)
- **YAML detection**: `IsYamlScript()` (ScriptParser.cs:411-458) decides Simple vs YamlScript preset
  type using strong indicators only (`---`, `steps:`, `vars:`, `preconnect:`, `imports:`,
  `subroutines:`, or a `- <command>:` line).
- **Preprocessing**: `PreprocessYaml()` (597-635) auto-quotes plain scalars whose value contains
  `: ` or ` #` (ternaries, URLs with text) for a fixed set of `ScalarValueKeys` (582-590) so users
  can write `set: x = a ? "y" : "z"` without YAML quoting. Line count is preserved.
- **Event-driven parse** (`Parse()`, 466-573): hand-rolled YamlDotNet event walking (not object
  deserialization), giving exact line numbers on every step (`step.LineNumber`,
  ScriptParser.cs:912) and structured per-step/per-script `ParseErrors`.
- Top-level keys parsed at 500-557 (duplicates reported, unknown keys → did-you-mean errors).
- Steps support both canonical map syntax (`send: {command: ..., capture: ...}`) and legacy inline
  + step-root options; `CanonicalMapCommands` (197-212) and `AddLegacyInlineError` (1842-1847)
  push toward canonical form. `UsesStepRootOnError` (ScriptStep.cs:31) flags deprecated root-level
  `on_error` for commands that require it inside the command map
  (`CommandMapOnErrorStepTypes`, ScriptParser.cs:213-235; error at 4379-4388).
- Per-command option-key catalogs (`CommandOptionKeys` 106-149, `EnumLikeOptionValues` 248-288,
  `EnumLikeOptionValuesByCommand` 290-328) are exposed via public getters (335-398) and consumed by
  the Scintilla editor autocomplete/validation layer (`Services/Editor/*`).
- **Did-you-mean** suggestions on unknown keys via Levenshtein with threshold
  `max(2, len/3)` (5326-5390). Unknown keys are *blocking errors*; recognized deprecations
  (interactive.columns/rows/emulation, 2621-2653) are warnings (`AddDeprecationWarning`, 5318-5321).

### 1.4 Validation (`ScriptParser.Validate`, 4181-5190)
- Entry: `Validate(script, originalYaml, enforceCanonicalSyntax, allowLibraryDefinitions)`.
  All production call sites pass `enforceCanonicalSyntax: true`
  (SshExecutionService.cs:511,918,1110; Form1.cs:5790; FlowCanvasBridge round-trip tests).
- Validates: parse errors, unknown-key errors, library-only rules (4202-4210, 4247-4266),
  empty-steps, preconnect restrictions (send/interactive forbidden — they need a live SSH session,
  4345-4350, 5197-5200), per-command required fields and enum values for all 38 step cases
  (4391-5175), `break`/`continue` only inside loops (4515-4529), `return` only inside
  subroutines (4531-4537), retry/retry_delay non-negative (5178-5187).
- Notable cross-field rules: `send.fail_on_nonzero` × `send.expect` mutually exclusive (4393-4398);
  http auth basic/bearer credential requirements (4789-4813); localcmd interactive×background and
  keep_open×interactive constraints (5002-5013); interactive headless mode requires command +
  max_seconds/max_lines (5096-5109); readfile path_into/path_only coupling (4603-4623).
- `IsDynamicValue()` (5202-5205) skips enum validation when the value contains `${`/`{{` —
  runtime-substituted values are unvalidatable at parse time.
- Error strings embed the offending source line (`GetLineContent`, 5295-5305).
- `ScriptValidationFormatter` (whole file, 28 LOC) formats success/failure/exception messages for UI.
- Recursion into child blocks is **per-case** (If at 4426-4448, loops at 4462-4498, Try at
  4507-4512, Switch at 5138-5147, Parallel at 5156-5157) — no generic recursion; a new container
  type that forgets its recursion silently skips all nested validation (known footgun, see memory).

### 1.5 Subroutines & libraries (`ScriptSubroutineRegistryBuilder.cs`, `Models/ScriptSubroutineRegistry.cs`)
- `subroutines:` declares named blocks with `params`/`outputs`/`steps`; `imports:` loads
  file-backed libraries (absolute path, must declare `library: true`, validated through
  `ScriptFileAccessValidator.ValidateReadPath`) — ScriptSubroutineRegistryBuilder.cs:52-140.
- Call-site validation: required args present, no unknown args, output bindings must be declared
  outputs and bare identifiers (188-245). Local recursive call cycles are *rejected* via DFS
  (247-297).
- `ScriptSubroutineRegistry.TryResolve` (ScriptSubroutineRegistry.cs:44-78): bare names resolve in
  the current library first then root; `alias.name` resolves only through imports.
- Runtime: `ScriptExecutor.ExecuteSubroutineAsync` (ScriptExecutor.cs:643-668) runs the body in an
  isolated child variable scope (`ScriptContext.CreateChildScope`, ScriptContext.cs:1016-1038,
  deep-cloning initial args), hard call-depth cap **32** (ScriptExecutor.cs:651), and copies only
  declared outputs back on success/return (`CopyOutputsFromChild`, ScriptContext.cs:1061-1081).
- Step paths inside subroutines: `subroutines/{name}/steps/{i}` (ScriptExecutor.cs:211-219).

### 1.6 Execution model (`ScriptExecutor.cs`)
- `ExecuteAsync(script, context, ct)` (183-283): imports `vars` defaults (CSV columns win,
  ScriptContext.cs:1043-1056), applies script-level `debug:`, resets `DebugState`, assigns
  canonical step paths (`AssignStepPaths`, 581-638: `steps/2/then/0`, `.../elif/{i}/then`,
  `.../cases/{i}/do`, `.../parallel/{i}`), runs steps, maps the result to
  `ScriptResult{Status: Success|Failure|Cancelled|Error, Message, FullOutput}`
  (ScriptContext.cs:73-90). `finally` emits the soft-assert summary (274-279) and cleans up
  tracked localcmd background processes (281).
- `ExecuteStepsAsync` (299-440) per step: cancellation check → debug-pause check → disabled-node
  skip (330-344) → universal `when:` guard skip (348-365, foreach evaluates per-item instead) →
  `StepStarting` event → stopwatch-timed dispatch → `StepCompleted` event with
  Success/Output/DurationMs/IterationCount/BranchTaken/SuppressedError/IterationStack.
- **Output attribution contract** (90-98, 379-393): container step types report the output of the
  send *preceding* their start (`carriedOutput`); leaf steps report `LastCommandOutput`.
- **Control flow**: `CommandResult.IsControlFlow` (exit/break/continue/return) propagates up
  unmodified (422-423); failures stop the list and set `_last_error` (426-431); suppressed errors
  (`on_error: continue`) set `_last_error` but continue (412-415); `_last_error` is cleared on
  ordinary success unless `preserveLastErrorOnSuccess` (Try/Catch uses this).
- **Retry** (445-494): `retry: N` re-runs the step up to N extra attempts with linear
  `retry_delay` seconds (default 1); non-final attempts force `on_error` to "stop" by temporarily
  mutating `step.OnError` (456-471) so failures surface to the retry loop. No retry on
  exit/break/continue/return/suppressed.
- **Error funnel**: exceptions from handlers become `CommandResult.ApplyOnError(step, msg)`
  (517-531); `StepType.Unknown` at runtime is *skipped with a warning*, treated as success
  (506-510).
- **Events** (103-113): `StepStarting`, `StepCompleted`, `DebugPauseStateChanged`; payload
  `StepExecutionEventArgs` (13-60) is the Flow Canvas debug-bridge contract (StepPath, BranchTaken
  vocabulary, IterationStack snapshots, SuppressedError flag).
- Loop-iteration stack: AsyncLocal immutable-array frames (`ScriptContext.PushIterationFrame` /
  `SetCurrentIterationFrame` / `PopIterationFrame`, ScriptContext.cs:330-362;
  `IterationFrame` record) — parallel-arm-safe, consumed by the canvas iteration stepper.

### 1.7 Debugging (`Models/DebugState.cs` + executor pause logic)
- Two breakpoint systems: line-number breakpoints (`ConcurrentDictionary<int,byte>`,
  DebugState.cs:26-145) and Flow-Canvas node-ID breakpoints mapped through a
  nodeId↔stepPath bimap (`SetNodeToStepPathMap`, 158-172; `ShouldPauseAtStep`, 201-214).
- Disabled nodes: `ToggleNodeDisabled`/`IsNodeDisabled` (222-230) — executor emits a synthetic
  Skipped StepCompleted (ScriptExecutor.cs:330-344).
- Step mode + pause/resume: `WaitForResumeAsync` (96-113) is a TCS-based async signal (no polling);
  parallel pause waiters share one signal so a single Step/Continue releases all paused branches
  (documented at 93-95). `HandleDebugPauseAsync` (ScriptExecutor.cs:538-579) raises paired
  pause/resume events carrying `ResumeAction`.
- `Reset()` (247-258) clears pause/requests but intentionally **keeps breakpoints and StepMode**.
- Legacy compatibility shims for step-index maps (177-185, 219-220, 241-242).

### 1.8 Execution context & variables (`ScriptContext.cs`)
- One `ScriptContext` per host run; `SharedScriptExecutionState` (105-133) holds the cross-scope
  state (session, output buffers, debug state, soft-assert counters, history-label ops,
  localcmd approvals, event handler chains) under a single `StateLock`; child scopes share it while
  keeping an isolated case-insensitive `_variables` dictionary (508-523).
- Variables: `SetVariable/GetVariable/HasVariable/RemoveVariable/GetAllVariables` (528-619), with
  reserved dynamic names `_timestamp`, `_output` (LastCommandOutput), `_outputwindow`, `_prompt`
  (541-548). Other engine-set names: `_last_error` (executor), `_iteration`, `_writefile`
  (commands; listed in ScriptDependencyAnalyzer.cs:57-60).
- **Interpolation**: `SubstituteVariables` (625-644) handles `${...}` and `{{...}}` identically
  with balanced-brace scanning and *recursive* inner substitution (`SubstituteVariableTokens`,
  758-786; `TryExtractBalanced`, 794-836). `ResolveVariableExpression` (649-705) supports:
  inline vault refs `{{vault:[profile@]path#key}}` (707-756), `.length` (656-660),
  `arr[index]` with variable index (663-685), inline function calls when the expr contains `(`
  (688-701), then plain lookup. Missing variables resolve to **empty string** silently.
- Output plumbing: `RecordCommandOutput` (855-867, sets LastCommandOutput + appends to FullOutput +
  optional capture var), `EmitOutput` (872-893, Debug-typed messages suppressed unless DebugMode),
  pane transcript `OutputWindowText` (488-497, 910-934). `ScriptOutputType` enum (58-68).
- UI bridges: events `OutputReceived`, `ColumnUpdateRequested` (updatecolumn command →
  grid write-back), `EnvironmentUpdateRequested` (397-455, 941-978) — handler chains stored in
  shared state so child scopes inherit subscribers.
- Service injection points: `VaultService` + `EnvironmentVaultProfile` (367-372),
  `NotificationService` (377), `Session`/`CurrentHost`/`ResolvedUsername`/`ResolvedPassword`/
  `Timeouts` (162-257).
- History-label ops recorded for deterministic replay (`AddHistoryLabelOperation` /
  `GetHistoryLabelOperationsSnapshot`, 227-248; `HistoryLabelOperation.ApplyTo` implements
  replace/append/prepend/clear semantics, HistoryLabelOperation.cs:63-102).
- Interactive-terminal session audit records (`AddInteractiveSession` /
  `GetInteractiveSessionsSnapshot`, 983-1011) — deep-cloned, consumed by history details.
- Deep-clone discipline for cross-scope copies (`CloneVariableValue`, 1083-1111: lists, JsonNode,
  JsonElement, dictionaries).

### 1.9 Condition language (`ExpressionEvaluator.cs`)
- Used by if/elif/while/repeat-until/`when:`/assert. Operators (string-scan based, precedence
  or < and < not < comparisons): `or`/`and`/`not`, `is [not] empty`, `is [not] defined`,
  `matches` (regex, case-insensitive, 5s timeout via ScriptRegexDefaults), `in`/`not in`
  (collection membership), `contains`/`startswith`/`endswith` (always OrdinalIgnoreCase),
  `!=`/`==` (numeric-first then case-insensitive string, epsilon 0.0001 — 399-416),
  `>= <= > <` (numeric only, non-numeric coerces to 0 — 368-376), bare-value truthiness fallback
  (205-207).
- Operator detection respects quotes and parens (`FindLogicalOperator`, 210-258) and strips
  redundant outer parens (260-318).
- `ResolveValue` (320-344): resolves via `ValueResolver.ResolveExpressionValue` first; if the value
  came back as the literal input *and* "looks computable" (`+ - * / % ?? ? ( )` —
  346-360) it re-parses through `ExpressionParser`, swallowing any parse failure.

### 1.10 Value expression language (`ExpressionParser.cs`)
- Recursive-descent parser for `set:` right-hand sides, function args, lambdas, ternaries
  (grammar at 8-19): ternary `?:`, null-coalesce `??` (null/empty-string only, 175-180),
  comparisons (==/!=/>=/<=/>/<, numeric-first with string fallback, 89-161), polymorphic `+`
  (numeric add or string concat, 182-215), `- * / %` numeric with **division/modulo by zero
  returning 0 with a warning** (232-258), unary +/-, parens, single/double-quoted string literals
  with escapes and embedded `${var}` substitution (316-344), function calls dispatched through
  `FunctionRegistry.Instance` → `JsonUtilities.TryEvaluateFunctionExpression` →
  `TryEvaluateJsonExpression` (422-437, throws `Unknown function` otherwise).
- Token value resolution (439-493): true/false/null literals, invariant doubles, `.length`,
  typed variable lookup with numeric coercion of numeric-looking strings, `${...}` substitution
  fallback, undefined identifier → null (enables `x ?? default`).

### 1.11 Shared value semantics (`ValueResolver.cs`)
- The single source of collection/emptiness/truthiness semantics:
  `ResolveLength` (24-37: strings parse as JSON first — a JSON-array string's length is its element
  count, not char count), `ResolveCollectionItems` (45-108: JSON arrays expand, newline strings
  become non-empty lines, scalars → 1-item list), `ResolveListValue` (127-175: same but scalar
  strings stay single unless JSON array / multiline), `IsEmptyValue` (331-345),
  `IsTruthyValue` (347-368), `CollectionContains` with optional `ordinal`/`ignore_case` comparer
  (370-394), `.length` helper (400-409), indexed access `name[expr]` (291-329, out-of-range → null).
- `ResolveExpressionValue` (252-289) is the engine-wide scalar resolution pipeline:
  `.length` → JSON expressions → function expressions → quoted strings (with substitution) →
  interpolation → variable → indexed → int → double → literal passthrough.
- Standalone `${...}`/`{{...}}` unwrapping preserves rich types instead of stringifying (177-240).

### 1.12 Functions & lambdas (`FunctionRegistry.cs`, `LambdaExpression.cs`, `JsonUtilities.cs`)
- `FunctionRegistry` singleton registers 8 categories (StringFunctions, MathFunctions,
  CollectionFunctions, TypeFunctions, DateTimeFunctions, EncodingFunctions, NetworkFunctions,
  VaultFunctions — FunctionRegistry.cs:92-102); name → `ScriptFunction(argsString, context)`
  dispatch, case-insensitive, silent overwrite on re-register (37-43).
- `JsonUtilities` (1026 LOC) is the JSON expression engine: `json.*` evaluation
  (`TryEvaluateJsonExpression`, 671), a **legacy fallback switch** for core functions not in the
  registry (`TryEvaluateFunctionExpression`, 336-: length/list/trim/upper/lower/replace/split/
  join/substring/...), node↔value conversion, top-level-comma splitting for argument parsing
  (944-1010), and `BuildJsonObject` (924).
- `JsonPathNavigator` (whole file): dot/bracket path parse (26-78), `Navigate` read (83-135),
  `PathExists` (140-179, distinguishes null vs missing), `SetAtPath` with auto-creation of
  intermediate objects/arrays (184-247), `DeleteAtPath` (252-293), `GetNodeType` (298-315).
- `LambdaExpression` (`x => body`, `(acc, x) => body`): top-level `=>` detection respecting
  quotes/brackets (92-124), evaluation by temporarily binding parameters as context variables with
  save/restore (60-90) — used by collection functions (map/filter/reduce).

### 1.13 Static analysis (`ScriptDependencyAnalyzer.cs`)
- Pre-run grid-column dependency analysis: walks all step types collecting referenced vs defined
  variables; unresolved references are reported as probable CSV column dependencies so the user is
  warned before execution about columns that would silently resolve to empty
  (class doc 40-44; AnalyzeScript 106; AnalyzePresetDetails 83-101 honors
  `suppress_missing_column_warning`).
- `AnalyzeSshRequirements` (222) determines whether a script needs an SSH session at all
  (`SshRequirementResult`: RequiresSshSession/UsesSftp/UsesInteractive/UsesBrowserCallbackCapture/
  Sftp default host/credentials, 30-38) — enables session-less presets (HTTP/local-only scripts).
- Knows built-ins (`_output`, `_outputwindow`, `_timestamp`, `_iteration`, `_last_error`,
  `_writefile`, 57-60) and expression keywords (62-66); masks quoted content, recognizes
  function calls vs bare identifiers (1091-1184); only *reachable* local subroutines contribute
  (915-968).

### 1.14 File access guard (`ScriptFileAccessValidator.cs`)
- `ValidateReadPath` (35-72): blocks fixed system directories (`C:\Windows`, Program Files, etc.,
  12-20) and other users' profile dirs (54-63).
- `ValidateWritePath` (80-133): read rules + blocked executable/script extensions (22-27) +
  allowlist of user-writable roots (UserProfile/MyDocuments/Desktop/LocalAppData/Temp, 101-118).
- Used by readfile/writefile commands and library import loading
  (ScriptSubroutineRegistryBuilder.cs:88).

### 1.15 Browser callback hosting (`BrowserCallbackUiHost.cs`, `BrowserCallbackWebViewProfileManager.cs`)
- UI host abstraction for the `browser_callback_capture` command (OAuth-style local-callback
  capture): launch request DTO, owned-window/session/dialog-adapter interfaces, WebView2 dialog
  factory, deferred-show ("show_after_seconds") and completion-state handling
  (BrowserCallbackUiHost.cs:14-110, 274-350). `BrowserCallbackWebViewProfileManager.Shared`
  manages the WebView2 user-data profile. Injected into `ScriptExecutor`'s ctor (ScriptExecutor.cs:120-125),
  default-constructed when not supplied — production wiring passes the app's host
  (SshExecutionService.cs:1558).

### 1.16 Script-level options honored end-to-end
- `debug: true` → context.DebugMode (executor 201-204) → Debug-typed output visible.
- `vars:` defaults imported only when not already defined (CSV columns take precedence,
  ScriptContext.cs:1043-1056).
- `environment:`, `nobanner`, `compact_errors`, `suppress_missing_column_warning` are consumed by
  callers (SshExecutionService/Form1), not the executor.
- `preconnect:` steps validated for session-independence and run before connection by the caller.

---

## 2. Integration points

- **SshExecutionService** (out-of-area consumer): constructs `ScriptExecutor` with
  browser-callback host + localcmd confirmation (SshExecutionService.cs:1558, 1693, 1751, 1902);
  parses + validates with `enforceCanonicalSyntax: true` (506-511, 905-918, 1105-1110); runs
  dependency analysis pre-run (561, 1131). It populates `ScriptContext` (session, host, creds,
  timeouts, Vault/Notification services) and subscribes to context/executor events.
- **Form1**: validation UX (Form1.cs:5790), missing-column warnings via dependency analyzer
  (12526-12651), the *only* consumer of `StepCompleted.Output` for the Flow Canvas per-block
  output panel (per memory), and wiring of `ColumnUpdateRequested`/`EnvironmentUpdateRequested`
  back into the host grid and `EnvironmentService`.
- **Flow Canvas debug bridge**: `StepStarting`/`StepCompleted`/`DebugPauseStateChanged` →
  `FlowCanvasForm` → React. Contracts that must not drift:
  - `StepPath` format produced by `AssignStepPaths` (ScriptExecutor.cs:581-638) ↔ node `_stepPath`.
  - `BranchTaken` vocabulary (`then` | `else` | `elif/{i}/then` | `cases/{i}/do` | `default`)
    ↔ `edge.data.branchPath`.
  - `IterationStack` snapshots ↔ canvas loop iteration stepper.
  - `DebugState.SetNodeToStepPathMap` + node breakpoints/disabled nodes ↔ canvas toggles.
  - Executor assigns sequential step paths; any canvas-side `_stepPath` gap kills events from
    there on (known invariant, see project memory).
- **FlowCanvasBridge.cs:390** parses YAML→graph with this parser; export validity is asserted via
  `Validate(..., enforceCanonicalSyntax: true)` in bridge tests; `FlowCanvasParityCli` reuses
  internals (`InternalsVisibleTo`).
- **Editor services** (`Services/Editor/ScriptEditorValidationService.cs:118-124`): live linting
  via Parse+Validate; autocomplete pulls the public key/enum catalogs
  (ScriptParser.cs:335-398).
- **Vault**: inline `{{vault:...}}` in `ScriptContext.ResolveVaultExpression` (707-756), the
  `vault` step (`VaultStepOptions`), VaultFunctions in the registry; profile fallback honors
  `EnvironmentVaultProfile`.
- **Notifications**: `context.NotificationService` consumed by NotifyCommand.
- **History**: `HistoryLabel*` state + recorded `HistoryLabelOperation`s and interactive-session
  snapshots are read by the history storage layer after each host run.
- **Models/PresetInfo**: preset type auto-detection calls `ScriptParser.IsYamlScript`.

---

## 3. Observed gaps & quirks (file:line evidence)

### Expression-language semantics (correctness/consistency)
1. **Two divergent truthiness rules**: `ExpressionParser.IsTruthy` treats string `"0"` as false
   (ExpressionParser.cs:169-171) while `ValueResolver.IsTruthyValue` treats `"0"` as truthy
   (ValueResolver.cs:357-359). `if x` and `x ? a : b` disagree when x = "0".
2. **Two divergent comparison rules**: evaluator `>`/`<` coerce non-numerics to 0
   (ExpressionEvaluator.cs:368-376) so `"abc" > "abd"` is `false` (0>0); the value parser falls
   back to case-insensitive *string* comparison for the same operator
   (ExpressionParser.cs:152-161). Same operator, different answers depending on which engine
   path evaluates it.
3. **Equality is always case-insensitive with a 0.0001 epsilon** (ExpressionEvaluator.cs:399-416);
   `contains/startswith/endswith` are always OrdinalIgnoreCase (125-148). No case-sensitive
   option exists in conditions, even though `ValueResolver.CollectionContains` supports an
   `ordinal` mode (ValueResolver.cs:384-394) that the evaluator never passes
   (ExpressionEvaluator.cs:112, 121).
4. **Division/modulo by zero silently yields 0** (warning only, ExpressionParser.cs:232-258) —
   numeric corruption propagates instead of failing the step.
5. **Operator parsing is whitespace-sensitive string scanning**: `x==y` (no spaces) is not a
   comparison — it falls through to a truthy check of an unresolvable token
   (ExpressionEvaluator.cs:152-203, all operators are `" op "`). No syntax error is reported.
6. **Swallowed expression errors**: `ResolveValue` catches all `ExpressionParser` exceptions and
   returns the unparsed string (ExpressionEvaluator.cs:332-341); `matches` swallows regex errors
   and returns false (96-103); unterminated string literals return silently
   (ExpressionParser.cs:343). Misspelled expressions degrade to wrong answers, not errors.
7. **`??` only coalesces null/empty-string** — empty lists/arrays don't trigger the fallback
   (ExpressionParser.cs:175-180); inconsistent with `is empty` semantics
   (ValueResolver.cs:331-345).
8. **Duplicate function implementations**: the legacy switch in
   `JsonUtilities.TryEvaluateFunctionExpression` (JsonUtilities.cs:343-460+) re-implements
   length/trim/upper/lower/replace/split/join/substring behind the registry; registry and legacy
   variants can drift (e.g. legacy `replace` is Ordinal at JsonUtilities.cs:394 while conditions
   are case-insensitive everywhere else).

### Variable resolution
9. **Missing variables silently become empty strings** in interpolation
   (ScriptContext.cs:704, GetVariableString 559-565) and out-of-range `arr[i]` returns
   empty/null without diagnostics (ScriptContext.cs:682-684; ValueResolver.cs:308-315). Only grid
   columns get a pre-run warning (ScriptDependencyAnalyzer); typo'd locals are invisible.
10. **Inline vault is sync-over-async**: `VaultService.ReadSecretAsync(...).GetAwaiter().GetResult()`
    inside string substitution (ScriptContext.cs:748) — blocking call buried in every interpolation
    pass; failures set `_last_error` and return empty string, so a failed secret read looks like an
    empty password (707-756).
11. **Secret-echo risk**: a `{{vault:...}}` value substituted into a `send`/`print` string flows
    into LastCommandOutput/FullOutput/history with no masking anywhere in the context layer
    (RecordCommandOutput, ScriptContext.cs:855-867).
12. **Lambda parameter binding is not parallel-safe**: `LambdaExpression.Evaluate` mutates and
    restores shared context variables (LambdaExpression.cs:60-90). Two parallel arms evaluating
    lambdas with the same parameter name on the shared context race on save/restore.
13. `${_output}` is replaced by plain `string.Replace` before token scanning
    (ScriptContext.cs:639-640), so even occurrences the balanced scanner would treat as literal
    (e.g. inside an already-substituted value) are replaced.

### Executor
14. **`StepExecutionEventArgs.StepName` is never populated** — always passed `null`
    (ScriptExecutor.cs:374, 402) and `ScriptStep` has no name/label property at all. Users of a
    professional tool would expect nameable steps for output, history and canvas display.
15. **Retry mutates shared parse state**: the retry loop temporarily rewrites `step.OnError`
    (ScriptExecutor.cs:456-471). `ScriptStep` instances are shared across iterations and across
    Parallel branches; concurrent retries of the same step race on this field.
16. **Unknown step type is silent success at runtime** (ScriptExecutor.cs:506-510) — a step that
    slipped past validation just warns and continues.
17. **Retry policy is linear-only** (fixed seconds delay, 485-489); no backoff, no max-elapsed cap,
    no retry on specific error patterns.
18. **`when:` guard failures are invisible**: a guard that fails to parse evaluates false
    (Evaluate returns false on garbage) and the step is skipped as if intentional
    (ScriptExecutor.cs:348-365).
19. **AssignStepPaths must mirror every container shape** (581-638); a future container key missed
    here breaks canvas correlation silently (same class of bug as the ValidateSteps per-case
    recursion).
20. Hardcoded subroutine call-depth cap 32 (ScriptExecutor.cs:651); combined with the
    cycle-detection ban on local recursion (ScriptSubroutineRegistryBuilder.cs:247-297) recursion
    is effectively impossible — intentional, but the two mechanisms overlap.
21. **FullOutput is unbounded**: shared `StringBuilder Output` accumulates every line for the whole
    run with no cap or trim (ScriptContext.cs:108, 860, 883); long-running loop scripts grow
    memory without bound (localcmd has MaxOutputBytes per command, the context does not).

### Parser / validation
22. **Parser instance state couples Parse↔Validate**: `_unknownKeyErrors`/`_warnings` are instance
    fields cleared in `Parse` (ScriptParser.cs:470-471) and read in `Validate` (4197-4200) —
    validating a script parsed by a *different* parser instance silently drops all unknown-key
    errors; the class is also not thread-safe/reentrant.
23. **Enum catalog vs validator drift**: editor-facing
    `EnumLikeOptionValuesByCommand["localcmd"]["shell"] = ["powershell", "custom"]`
    (ScriptParser.cs:311) omits `cmd`, but `IsValidLocalCmdShell` accepts cmd/cmd.exe/full paths
    (5249-5264) and the error message says "powershell, cmd, custom" (4985). Two sources of truth
    for the same vocabulary, already inconsistent.
24. **Silent scalar coercion failures**: invalid `version:` (509-511), `timeout:` (979-982),
    `retry:`/`retry_delay:` (988-996), `max_iterations:` (1000-1004) are ignored without a
    warning when `int.TryParse` fails — `timeout: 30s` simply never applies.
25. `PreprocessYaml` rewrites user text line-by-line by regex (597-635); keys outside
    `ScalarValueKeys` (582-590) with colon-bearing values still hard-fail YAML parse; the list must
    be manually extended for each new scalar option.
26. `IsYamlScript` heuristics (411-458) will classify a plain-command preset containing a line like
    `- send: something` as a script; conversely intentionally minimal scripts using only metadata
    keys are not detected (documented as intentional, 416-417).
27. Validation has **no semantic checks on expressions**: conditions, `set:` RHS and function names
    are validated only at runtime (e.g. `Unknown function` throws mid-run,
    ExpressionParser.cs:436); the only structural check is `set` containing `=`
    (ScriptParser.cs:4539-4554).
28. `Validate` rebuilds `script.SubroutineRegistry` (4189, 4238-4242) and the executor requires it
    (ScriptExecutor.cs:192); a caller that Parses but never Validates gets `call` steps that cannot
    resolve — implicit ordering contract.
29. Library imports re-read and re-parse files on every Validate (ScriptSubroutineRegistryBuilder.cs:100-118)
    — no caching; editor-side validation re-hits disk per keystroke-validation cycle.

### File-access guard
30. **Prefix-based blocklist is bypassable and over-broad**
    (ScriptFileAccessValidator.cs:44-51): `C:\Windows` prefix also blocks `C:\WindowsTools`
    (no trailing-separator check), misses `\\?\C:\Windows` extended-length syntax, other drive
    letters/junctions, and the hardcoded `C:\Users` (54) breaks on relocated profiles.
    Extension blocklist (22-27) omits .lnk/.jar/.py/.psm1 etc. Defense-in-depth only.

### JSON utilities
31. `JsonPathNavigator.ParsePath` silently drops non-numeric bracket segments
    (JsonPathNavigator.cs:54-63) — `data[abc].x` navigates `data.x` instead of erroring; no
    quoted-key support so keys containing `.`/`[` are unaddressable.
32. `SetAtPath`/`DeleteAtPath` silently no-op when the path can't be navigated
    (JsonPathNavigator.cs:209, 224, 270, 277) — write failures invisible to scripts.

### Debugging
33. Line-number breakpoints are keyed to the *parsed* YAML's line numbers; after PreprocessYaml
    rewriting (line-count preserving, so currently safe) and any future editor transform they can
    drift — there is no document-version check between editor and DebugState.
34. `DebugState.Reset` keeps StepMode (DebugState.cs:257); a run started after a stepped session
    pauses at the first step, which may surprise users (intentional per comment, but no UI hint
    obligation is encoded anywhere).
35. Canvas-side note (cross-area): the `breakpoints` Set in the React store is not cleared across
    preset switches (project memory) — pairs with the C#-side persistence here.

### Maturity assessment
The core is well past prototype quality: line-accurate hand-rolled parsing, did-you-mean
diagnostics, canonical-syntax enforcement, cycle-detected subroutines with scoped registries,
async TCS-based debugging, AsyncLocal parallel-safe iteration tracking, and a documented
output-attribution contract. The weak layer is the *expression language*: two evaluators with
diverging coercion/truthiness/comparison semantics, whitespace-sensitive operator scanning, and
error-swallowing fallbacks — that is where user-visible "my script does the wrong thing silently"
bugs will concentrate, followed by silent-empty variable resolution and the parse-time/run-time
validation gap for expressions.
