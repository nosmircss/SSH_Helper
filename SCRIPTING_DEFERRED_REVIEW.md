# Considered & Deferred — Decision Workbook

Expansion of the 38 deferred items from `SCRIPTING_LANGUAGE_ROADMAP.md` into **problem / design / effort / rationale**, each re-grounded against the actual engine code. Generated to help decide which deferred work (if any) to pull forward.

**How to read this**

- **Value / Effort / Risk** are the original review scores (0–10). Higher value = more useful; higher effort = more work; higher risk = more likely to break things.
- **Recommendation** is the *salvage lens*, not the original verdict:
  - ✅ **Salvage now** — a cheap, high-value sliver is clearly worth doing on its own.
  - 🟡 **Reconsider later** — the whole feature has merit but is gated on a prerequisite (usually dotted member access or a structured-error/result model).
  - ⛔ **Skip** — low value, redundant with shipped features, or fights the architecture.
- Items are sorted by recommendation, then by value-to-effort ratio.

**Totals:** 14 salvage-now · 11 reconsider-later · 13 skip

---

## Decision summary

| # | Item | Area | V | E | R | Brk | Recommendation |
|---|------|------|---|---|---|-----|----------------|
| 1 | Status functions for conditionals: success(), failure(),… | Control flow | 4 | 4 | 3 | — | ✅ Salvage now |
| 2 | Raw send primitives: control characters, no-newline send,… | Interactive SSH | 4 | 5 | 5 | — | ✅ Salvage now |
| 3 | Counter helpers incr/decr/tally to kill manual accumulato… | Syntax | 4 | 5 | 3 | — | ✅ Salvage now |
| 4 | Configurable retry policy: backoff, jitter, error filter,… | Control flow | 5 | 7 | 5 | — | ✅ Salvage now |
| 5 | Dry-run / simulate mode with a mock SSH session and a pla… | DevEx | 5 | 7 | 6 | — | ✅ Salvage now |
| 6 | Snippet scaffolding on command completion | DevEx | 4 | 6 | 6 | — | ✅ Salvage now |
| 7 | Stored-type-honest type inspection: typeof/is_number repo… | Data types | 3 | 5 | 7 | yes | ✅ Salvage now |
| 8 | Distinguish null/missing/empty, and add recursion-safe in… | Data types | 3 | 5 | 5 | — | ✅ Salvage now |
| 9 | raise/rethrow from catch, cause chaining, and a durable _… | Error handling | 4 | 7 | 5 | — | ✅ Salvage now |
| 10 | Format Document command (auto-fix YAML hygiene) | DevEx | 4 | 7 | 7 | — | ✅ Salvage now |
| 11 | Cross-host result aggregation and a fleet-level after_all… | Modularity | 5 | 9 | 8 | — | ✅ Salvage now |
| 12 | Strict-by-default identifier/function resolution with dia… | Expressions | 4 | 8 | 7 | yes | ✅ Salvage now |
| 13 | Honest numbers: preserve integer identity, add exponent/i… | Data types | 3 | 8 | 9 | yes | ✅ Salvage now |
| 14 | Robust YAML normalization to retire the PreprocessYaml re… | Syntax | 3 | 8 | 7 | — | ✅ Salvage now |
| 15 | Heredoc-style remote file push: render a templated docume… | Interactive SSH | 5 | 5 | 3 | — | 🟡 Reconsider later |
| 16 | Generic structured parse: json/csv/kv/ini, whitespace tab… | Functions | 6 | 7 | 6 | — | 🟡 Reconsider later |
| 17 | Script-level strict mode (set -euo pipefail) for fail-fas… | Error handling | 5 | 7 | 7 | — | 🟡 Reconsider later |
| 18 | Default-value, required-value, and ?? / ?: coalescing ins… | Data types | 5 | 7 | 7 | yes | 🟡 Reconsider later |
| 19 | Executor-enforced per-step and per-block timeouts | Control flow | 4 | 6 | 6 | — | 🟡 Reconsider later |
| 20 | Operator aliasing (&& || !), whitespace-tolerant operator… | Expressions | 4 | 6 | 7 | yes | 🟡 Reconsider later |
| 21 | Idempotency primitives: unless/creates, changed_when, fai… | Control flow | 5 | 8 | 6 | — | 🟡 Reconsider later |
| 22 | Inventory groups with layered group/host variables and ru… | Modularity | 5 | 8 | 5 | — | 🟡 Reconsider later |
| 23 | Typed structured errors with catch-as binding, when-filte… | Error handling | 4 | 7 | 6 | — | 🟡 Reconsider later |
| 24 | Namespaced contexts: env.*, host.*, and secret() to disam… | Data types | 4 | 8 | 6 | — | 🟡 Reconsider later |
| 25 | First-class typed list/object values with one unified dee… | Data types | 4 | 9 | 9 | yes | 🟡 Reconsider later |
| 26 | First-class string interpolation/format() with padding an… | Functions | 2 | 2 | 2 | — | ⛔ Skip |
| 27 | Multi-line send_block / heredoc for device CLI config sta… | Interactive SSH | 5 | 6 | 5 | — | ⛔ Skip |
| 28 | Handlers: notify-on-change deferred steps that run once a… | Control flow | 4 | 6 | 5 | — | ⛔ Skip |
| 29 | Pipeline operator |> for left-to-right value transforms | Expressions | 4 | 6 | 5 | — | ⛔ Skip |
| 30 | Robust, shell-portable remote exit-status capture (decoup… | Interactive SSH | 4 | 6 | 5 | — | ⛔ Skip |
| 31 | Template rendering command with for/if expansion for mult… | Interactive SSH | 5 | 8 | 5 | — | ⛔ Skip |
| 32 | Device-type drivers: declare the platform, get pagination… | Interactive SSH | 4 | 8 | 6 | — | ⛔ Skip |
| 33 | First-class matrix fan-out inside a single script | Control flow | 4 | 9 | 7 | — | ⛔ Skip |
| 34 | Inline remote command substitution ($sh{...} / $local{...… | Interactive SSH | 4 | 9 | 9 | — | ⛔ Skip |
| 35 | Built-in test harness: test blocks with mocked send/http… | DevEx | 4 | 9 | 5 | — | ⛔ Skip |
| 36 | Portable imports: relative paths, library search path, tr… | Modularity | 3 | 7 | 6 | — | ⛔ Skip |
| 37 | YAML anchors/aliases/merge-keys plus a defaults:/use: opt… | Syntax | 3 | 8 | 7 | — | ⛔ Skip |
| 38 | Single machine-readable DSL schema replacing ~38 hand-wri… | DevEx | 3 | 9 | 8 | — | ⛔ Skip |

---

## ✅ Salvage now

### 1. Status functions for conditionals: success(), failure(), always(), changed(), cancelled()

**Area:** Control flow  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 4/10 (M) · risk 3/10 · risk band medium · breaking: no

> **Recommendation — ✅ Salvage now:** `success()`/`failure()` are cheap, useful readers of existing flat state and pair naturally with the cleanup/teardown work; the rest is dead weight.

**Problem.** There is no way inside a conditional to ask 'did the previous step fail?' except by inspecting the flat `_last_error` string (`if: _last_error is defined`). That's the entire status surface today. Operators can't write a clean post-step gate; `always()`/`failure()`/`changed()` don't exist, so cleanup-on-failure patterns are awkward.

**Design.** The proposal wanted `success()`, `failure()`, `always()`, `changed()`, `cancelled()` callable in `if`/`when`. Functions are registered as `ScriptFunction` taking `(argsString, ScriptContext)` (FunctionRegistry.cs:60-72), so `success()`/`failure()` are buildable: they'd read `_last_error` from context. The obstacles, per-function: `cancelled()` can never observe true — cancellation throws `OperationCanceledException` and unwinds straight out of `ExecuteAsync` (ScriptExecutor.cs:222-231) before any conditional could run. `always()` is literally the constant `true`. `changed()` has no backing infrastructure — there is no idempotency/change-tracking model anywhere in the engine (confirmed: no `changed_when`, no change tally, only `_status` siblings for HTTP/webhook). So 3 of 5 functions are vacuous or unbuildable.

**Effort.** M. `success()`/`failure()` plus an aggregate would be a small new `IFunctionCategory` (mirroring existing categories) registered in `FunctionRegistry.RegisterBuiltInCategories`, plus autocomplete/`RegisteredNames` (already automatic) and dependency-analyzer awareness. The blast radius is small precisely because the useful subset is small; the rest is blocked.

**Rationale (why deferred).** Deferred because 3 of the 5 advertised functions are vacuous or impossible against the architecture: `cancelled()` can't be true (cancel unwinds), `always()` is a literal, and `changed()` needs a change-tracking subsystem that doesn't exist. Only `success`/`failure` map to real (flat) state.

**Salvageable sliver.** Ship `success()`/`failure()` as thin readers of `_last_error`, plus an `any_failed`-style aggregate backed by the existing soft-assert counters (`SoftAssertFailed` in ScriptContext) or a simple failure tally. Skip `always`/`changed`/`cancelled`.

---

### 2. Raw send primitives: control characters, no-newline send, and a standalone read/expect step

**Area:** Interactive SSH  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 5/10 (S) · risk 5/10 · risk band low · breaking: no

> **Recommendation — ✅ Salvage now:** The Ctrl-C/Ctrl-Z abort is a small, isolated, genuinely-missing primitive; the rest is already covered by respond/expect and accepted item 2.3.

**Problem.** `send` always appends `\r` (SshShellSession.ExecuteAsync line 512, ExecuteWithRespondsAsync line 588), so there is no way to send a bare keystroke or a control character (Ctrl-C/Ctrl-Z) to abort a hung remote pager or runaway process. There is also no standalone read/expect step: matching output requires attaching `expect:`/`respond:` to an actual `send`, so you cannot passively wait for a banner or drain output without issuing a command.

**Design.** Proposal wanted three primitives: control-char send, no-newline send, and a standalone `expect:` step. Realistic minimal shape: add an optional `send` sub-option (e.g. `keys: ctrl-c` or `raw: true` to suppress the `\r`) that routes to a new SshShellSession method calling `_scripting.Send("\x03")` without the carriage return. The standalone read/expect step largely overlaps with accepted Tier-2 item 2.3 (multi-branch expect), which already proposes a standalone `expect:` StepType. The obstacle: the no-newline/raw arm collides with the prompt-detection loop — ReadUntilPromptCore assumes a command was sent and waits for a prompt terminator; a bare keystroke produces no prompt cycle, so it needs a distinct fire-and-read path.

**Effort.** S for the control-char abort alone (one new SshShellSession method + one `send` key in ScriptParser line 109 + FlowCanvasBridge line 257 + bridge serializer line 2314-2330). The standalone read/expect step is M and is subsumed by accepted item 2.3.

**Rationale (why deferred).** Deferred marginal because `send.expect`/`respond` already drive non-shell interactive programs — the proposal's flagship `passwd` example is verbatim in the docs and works via ExecuteWithRespondsAsync. The roadmap explicitly called out that the ONE real gap is the Ctrl-C/Ctrl-Z abort, and that the standalone-expect ambition duplicates accepted item 2.3. Low value relative to existing respond/expect coverage.

**Salvageable sliver.** Add a single `send` option to transmit a control character / no-newline keystroke (Ctrl-C abort) — the one capability genuinely missing today, isolated to SshShellSession + the send key tables.

---

### 3. Counter helpers incr/decr/tally to kill manual accumulator boilerplate

**Area:** Syntax  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 5/10 (S) · risk 3/10 · risk band low · breaking: no

> **Recommendation — ✅ Salvage now:** Compound assignment is a 2-3 file change that both removes real boilerplate and fixes an existing silent-corruption footgun, while the full incr/decr/tally StepType is unjustified sugar.

**Problem.** Accumulating a counter today requires the verbose `set:` form `expression: passed = passed + 1`, repeated by hand at every branch — ScriptSamples/generic/compliance_report.yaml has ~8 such lines, and the pattern also appears in file_ip_processor.yaml and bash/loop_interfaces.yaml. Worse, the obvious shorthand silently misfires: `set: x += 1` is split on the first `=` in SetCommand.ExecuteAsync (step.Set.Split('=', 2)), yielding varName `"x +"` and value `"1"`. SetVariable does no name validation (ScriptContext.cs:487 just does `_variables[name] = value`), so it creates a junk variable literally named `"x +"` and never increments x. The parse-time validator (ScriptParser.cs:4539 case StepType.Set) only checks the name-before-`=` is non-empty, so `"x +"` passes — no error at parse or runtime.

**Design.** The proposal wanted dedicated `incr`/`decr`/`tally` step keywords with auto-initialization (treat an undefined counter as 0). A faithful build means a new StepType per the engine's command-handler pattern, a handler under Commands/, plus parser wiring. The architectural obstacle is the integration tax the roadmap already documented for repeat/until (commit 8cf6eba): a new step keyword must be registered in StepType (Models/ScriptStep.cs), KnownStepKeys (ScriptParser.cs:24), a parser `case` (~line 933 region), the ValidateSteps switch (~line 4539), ScriptAutocompleteProvider, FlowCanvasBridge import/export + the React blockDefs registry, and ScriptDependencyAnalyzer. That is the ~12-touchpoint checklist for sugar over `x = x + 1`. Auto-init also masks typos — `incr: requsts` silently starts a new counter at 0 rather than erroring on the misspelled variable.

**Effort.** S (label) but the blast radius is the full new-StepType checklist if done as keywords: StepType enum + KnownStepKeys + parser case + ValidateSteps + autocomplete + FlowCanvasBridge (import & export) + React blockDefs + ScriptDependencyAnalyzer (~12 touchpoints). The salvage (`+=`/`-=` compound assignment) is far smaller: only SetCommand.cs (split on the compound operator, evaluate `current op rhs`), the `set` validator at ScriptParser.cs:4539-4554 (accept `+=`/`-=`, reject malformed lvalues), and an autocomplete/doc note — 2-3 files, no new StepType.

**Rationale (why deferred).** Deferred as marginal: it is pure sugar over the existing `set: x = x + 1` idiom (which routes correctly through HasExpressionOperator -> ExpressionParser), justified by a handful of demo files, and the auto-init behavior actively introduces a typo-masking footgun in a string-keyed variable store with no name validation. It does not fight the architecture; it just buys little for the new-StepType integration cost. This is low-value, not false-premise.

**Salvageable sliver.** Compound assignment operators (`+=`, `-=`, and optionally `*=`/`/=`) handled inside the existing `set` command — no new StepType. This collapses `expression: passed = passed + 1` to `passed += 1` across the sample catalog. As a bonus it closes the current silent footgun where `set: x += 1` creates a garbage variable named `"x +"` because of the naive `Split('=')`. Implement in SetCommand.cs plus the `set` validator at ScriptParser.cs:4539.

---

### 4. Configurable retry policy: backoff, jitter, error filter, and _attempt variable

**Area:** Control flow  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 7/10 (M) · risk 5/10 · risk band medium · breaking: no

> **Recommendation — ✅ Salvage now:** Backoff+jitter+`_attempt` is a self-contained ~15-line change to an existing loop with clear value for device polling; only the error-filter half is blocked.

**Problem.** Today retry is fixed-interval only. `ScriptExecutor.ExecuteStepAsync` (lines 396-440) reads `step.Retry`/`step.RetryDelay`, and on failure does `await Task.Delay(TimeSpan.FromSeconds(delay))` with a constant delay (default 1s). There is no exponential backoff, no jitter, no way to restrict retries to certain failures, and no per-attempt counter exposed to the body. An operator polling a flaky device either hammers it every second or hand-rolls a `while: true` + `set: attempt = attempt + 1` + `wait` loop. Worse, retry blindly re-runs on ANY failure, so a genuinely non-retryable error (bad credentials, syntax error) burns all attempts before failing.

**Design.** The proposal wanted `retry: { count, backoff: exponential, jitter, when: <error-filter>, _attempt }`. Backoff+jitter are a trivial, well-fitting change inside the existing retry loop in ScriptExecutor.cs: replace the flat `delay` with a computed value and inject `_attempt` via `context.SetVariable` before each `ExecuteStepCoreAsync`. The architectural obstacle is the `when:` error filter. There is no error taxonomy: the only error data available mid-script is the flat string `_last_error` (set in ExecuteStepsAsync lines 360-375) and command messages like `"Command failed: ..."` (SendCommand.EmitFailure). There is no error `kind`/`category`/exit-code object to match against, so a structured `when: error.kind == timeout` is unresolvable. The honest filter would be a substring/regex match against `_last_error`, which is brittle. Prerequisite for a real filter: the deferred 'structured command result register' (Tier 2.1) and an error-type model.

**Effort.** M. Backoff+jitter+`_attempt`: ~15 lines in `ScriptExecutor.ExecuteStepAsync`, plus 2 new options (`backoff`, `jitter`) added to the common-step parse branches in ScriptParser.cs (lines ~988-995 and ~1212-1219), the `_commonStepOptions`/per-command key tables (ScriptParser ~93-148, FlowCanvasBridge ~165-277), validation (ScriptParser ~5177-5186), and FlowCanvasBridge props round-trip (~2316). The `when` filter would additionally need an error-object model threaded through all ~40 command handlers — that is the L-XL part. Salvageable slice avoids touching the analyzer.

**Rationale (why deferred).** Deferred as marginal because the highest-value piece (the `when` error filter) rests on a non-existent error taxonomy, and the reviewer noted the thread-safety justification didn't hold. So most of the proposal's value is blocked by missing infrastructure, while the genuinely cheap part (backoff/jitter) is real but modest.

**Salvageable sliver.** Add `backoff: exponential|linear` + `jitter: true` to the existing retry loop, and expose `_attempt` (1-based) as a context variable inside the retried step. Also fold in the `step.OnError` save/restore hygiene already half-present at ScriptExecutor.cs:402-417 (it temporarily overrides `step.OnError` to force failures to surface on non-final attempts — make that pattern explicit/tested).

---

### 5. Dry-run / simulate mode with a mock SSH session and a plan summary

**Area:** DevEx  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 7/10 (L) · risk 6/10 · risk band high · breaking: no

> **Recommendation — ✅ Salvage now:** The interpolation preview is cheap (reuses ValueResolver, no session abstraction) and directly answers the 'what will this run on my device' question that the full mock-session design cannot honestly deliver.

**Problem.** There is no way to preview what a script will do without connecting to a host. ScriptContext.Session is a concrete SshShellSession? (a sealed Rebex-backed class), so commands that need a session simply fail or block when none is present. An operator cannot answer 'which commands will this run against {{Host_IP}}, with variables interpolated' short of pointing it at a live device — risky for config-changing FortiGate/Cisco scripts.

**Design.** The proposal wanted a mock SSH session returning stubbed output plus a rendered plan summary of every send. Realistic shape: inject a fake ISendCommandSession via SendCommand's existing Func<ScriptContext, ISendCommandSession?> sessionResolver (ctor) and collect interpolated commands. The obstacle: only SendCommand has that seam; InteractiveCommand, SftpCommand, HttpCommand/PingCommand and others reach for context.Session or do real I/O directly, with no equivalent injection point, so a 'mock session' is not one swap but an abstraction over the sealed SshShellSession across many commands. Worse, control-flow preview is systematically wrong for stateful scripts — a send captured into a variable feeds if/while/assert branches; stubbing empty output makes every verify branch take the wrong path, so the 'plan' diverges from reality after the first conditional. The OnTestStep event name in FlowCanvasForm.cs is already a shipped per-step test feature, so the proposed hook collides.

**Effort.** L. Would touch SendCommand plus every session-bound command (interactive, sftp, http, ping, dns, portcheck), require introducing a session abstraction over the concrete SshShellSession, a new executor 'simulate' mode flag in ScriptExecutor.cs, and FlowCanvasForm/MessageBus wiring distinct from the existing OnTestStep.

**Rationale (why deferred).** Deferred as marginal because the headline capability (a faithful control-flow preview) cannot be delivered against a stateful single-shell model with stubbed output, and the realistic subset (interpolation-only preview of sends) is much smaller than 'mock SSH session'. The naming collision with the shipped OnTestStep confirms the design wasn't grounded in the current bridge.

**Salvageable sliver.** Ship a read-only command-interpolation preview: walk the parsed Script, resolve {{ }}/${ } against current variables/host columns using the existing ValueResolver, and list the literal commands each send would issue (flagging unresolved vars) without executing or branching. High value for 'what will this push to the device', no fake session, no false branch claims.

---

### 6. Snippet scaffolding on command completion

**Area:** DevEx  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 6/10 (S) · risk 6/10 · risk band low · breaking: no

> **Recommendation — ✅ Salvage now:** Emitting indented body skeletons for the ~6 block commands reuses the existing required-key data and the editor's indentation helpers, capturing most of the value without building a tab-stop engine.

**Problem.** When you accept a command from autocomplete, ScriptAutocompleteProvider inserts only the bare token (InsertText is the command name; the step path prepends '- ', so you get '- send'). There are no tab stops and no body scaffold, so accepting a block command like 'try' or 'switch' leaves the author to hand-build do:/catch:/finally: or value:/cases: from memory — exactly where the required-key knowledge would help most.

**Design.** The proposal wanted schema-driven snippets that expand a command into a templated body with tab stops (e.g. 'send' -> command:/capture: with the cursor parked on the first field). Realistic shape: extend CompletionItem.InsertText into a templated string and add a tab-stop navigation engine to the Scintilla control. Two obstacles: (1) the 'schema-driven' premise is only partly true — RequiredOptionKeysByCommand is a hand-maintained dict in ScriptAutocompleteProvider.cs, not derived; (2) there is no snippet/tab-stop engine anywhere in ScintillaScriptEditorControl, so this is net-new editor machinery. The design also ignores that block commands (if/foreach/while/try/switch/parallel) need multi-line, correctly-indented body scaffolds, not a flat key list.

**Effort.** S as scored, but the S only covers flat single-line scaffolds; a correct version is M once a tab-stop engine and block-body templates are added. Touch points: ScriptAutocompleteProvider (InsertText/template generation), ScintillaScriptEditorControl (tab-stop navigation, indentation-aware insertion), and the required-key source.

**Rationale (why deferred).** Deferred as marginal because ~80% of the value already exists via required-key tagging in completion detail, the 'schema-driven' framing is inaccurate (hand-maintained dict), and the missing tab-stop engine plus block-body scaffolding make the honest version larger than the S label, for an ergonomic-only payoff.

**Salvageable sliver.** For block commands only, make InsertText emit a minimal correctly-indented body skeleton (e.g. 'switch' -> 'switch:\n    value: \n    cases:'), reusing the existing required-key list. No tab-stop engine — just better starting structure where authors most often guess wrong. Cheap and high-value for the handful of container commands.

---

### 7. Stored-type-honest type inspection: typeof/is_number report actual CLR type, not string content

**Area:** Data types  ·  **Original verdict:** marginal  ·  **Scores:** value 3/10 · effort 5/10 (M) · risk 7/10 · risk band medium · breaking: yes

> **Recommendation — ✅ Salvage now:** `looks_like_number` is a trivial additive function that makes the sniffing behavior explicit and self-documenting, while changing typeof/is_number would break documented patterns for negative value.

**Problem.** `typeof(x)` and `is_number(x)` report what a value's string CONTENT looks like, not what is stored. In TypeFunctions.cs, when the resolved value is a string, TypeOf calls InferStringType (lines 110-113, 175-185) which returns "number" for `"42"` and "bool" for `"true"`; IsNumber (lines 120-121) returns true for any string that `double.TryParse` accepts. So a captured SSH output of `"8080"` reports as a number even though it is stored as a string, and there is no way to distinguish "the host returned the text 8080" from "this is a numeric 8080".

**Design.** The proposal wanted typeof/is_number to reflect the actual stored CLR type (string vs int vs JsonArray). The realistic shape: delete the InferStringType fallback so a `string` reports "string" and is_number on a string returns false. The obstacle is that this content-sniffing is load-bearing and deliberate: nearly every value in the engine is a string (CSV columns, captured output, interpolation results are all strings — see ScriptContext._variables population and GetVariableString), so honest typing would make typeof return "string" for essentially everything, defeating its purpose. The two patterns the docs teach (numeric comparison of captured output, JSON-shaped string detection) depend on the sniff.

**Effort.** M. Blast radius: TypeFunctions.cs only (TypeOf, IsNumber, InferStringType, IsList/IsJson string branches) plus the ~test fixtures that assert the current sniffing behavior. No parser/StepType/FlowCanvas/analyzer touch points — it is a pure-function semantics change. But it is breaking for any script relying on `is_number(captured_output)`.

**Rationale (why deferred).** Deferred as marginal because the premise (sniffing is a bug) is actually a deliberate design choice for a string-dominated domain. Honest typing would make the functions nearly useless (everything is "string") while breaking the two documented patterns. It is low value AND mildly fights the architecture.

**Salvageable sliver.** Add an opt-in `looks_like_number(x)` (and optionally `looks_like_json`) as a new TypeFunctions entry that explicitly names the content-sniff semantics, leaving typeof/is_number untouched. Confirmed absent today (grep for looks_like_number returns nothing). This gives authors an honestly-named tool without breaking existing scripts.

---

### 8. Distinguish null/missing/empty, and add recursion-safe interpolation

**Area:** Data types  ·  **Original verdict:** marginal  ·  **Scores:** value 3/10 · effort 5/10 (M) · risk 5/10 · risk band medium · breaking: no

> **Recommendation — ✅ Salvage now:** The pre-splice fix is a contained, correctness-positive change to one method that closes a real (if narrow) re-interpolation footgun; the null/missing redefinition is breaking for negligible benefit.

**Problem.** `set: x = null` does NOT store a real null — ExpressionParser.ResolveTokenValue maps the literal `null` to a C# null, but if it arrives as a bare token through other paths it stringifies; more importantly the engine cannot distinguish "variable absent" from "variable set to empty" from "variable set to null" in interpolation: GetVariableString returns empty string for all three (ScriptContext.cs:518-524), and HasVariable is the only discriminator. Separately, `${_output}` is spliced into the string by a literal `.Replace("${_output}", lastOutput)` (ScriptContext.SubstituteVariables, line 598) BEFORE the balanced-brace scanner runs (SubstituteVariableTokens) — so if captured output happens to contain `${...}` or `{{...}}`, that injected text is then re-scanned and re-interpolated, an unintended second expansion.

**Design.** The proposal wanted distinct null/missing/empty semantics plus 'recursion-safe interpolation' to prevent a re-expansion DoS. The realistic shape: a real null sentinel in the variable store, `is null`/`is missing` operators in ExpressionEvaluator, and moving the `_output`/`_outputwindow` pre-splice (lines 597-599) INTO the balanced scanner as a proper variable lookup so injected output is never re-scanned. The obstacle for the null/missing half is the string-canonical store again: ToString() collapses null and empty, and adding a distinct null would ripple through GetVariableString, IsEmptyValue, and every command that reads a variable. The recursion premise is mostly false — SubstituteVariableTokens does recurse into the EXPRESSION it extracted (line 728), but resolved values are appended via output.Append, not re-scanned; the only real re-scan is the `_output` pre-splice.

**Effort.** M. Blast radius for the safe sliver: ScriptContext.SubstituteVariables/SubstituteVariableTokens only (move `_output`/`_outputwindow` from literal Replace into the scanner's resolution path). The full null/missing/empty change would additionally touch GetVariableString, ValueResolver.IsEmptyValue, ExpressionEvaluator (new operators), and the dependency analyzer's built-in list — but that part is breaking-adjacent.

**Rationale (why deferred).** Deferred as reject because the premises are false: `set x = null` already stores a usable null via ExpressionParser, and the engine does NOT recursively re-expand resolved values (no DoS) — the recursion is over the extracted expression text, not the result. So the headline 'recursion-safe' framing is solving a non-problem.

**Salvageable sliver.** The `${_output}` pre-splice fix: resolve `_output`/`_outputwindow` inside the balanced scanner like any other variable instead of the literal `.Replace` at lines 598-599, so captured output containing brace tokens is not re-interpolated. The roadmap names exactly this.

---

### 9. raise/rethrow from catch, cause chaining, and a durable _failure variable

**Area:** Error handling  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 7/10 (M) · risk 5/10 · risk band medium · breaking: no

> **Recommendation — ✅ Salvage now:** Bare `raise:` plus a non-self-clearing `_failure` string are small, self-contained, and immediately useful, while rethrow/cause-chaining stay parked behind the typed-error prerequisite.

**Problem.** There is no way to deliberately throw an error from a script, and no way to re-propagate a caught failure: TryCommand.cs runs the catch arm and returns its result (success unless the catch itself fails), so a catch that handles-then-wants-to-abort must fake it with `exit: {status: failure}`. There is no error object to chain a cause onto. And `_last_error` is not durable — ScriptExecutor.ExecuteStepsAsync removes it on the very next successful step (line 364), so after a try/catch recovers, the record of what failed is gone; a later step cannot inspect it.

**Design.** The proposal wanted `raise:` (throw a typed error), `rethrow` (re-propagate the caught error with the original preserved), cause chaining, and a `_failure` variable that survives past recovery. Realistic shape: a `raise:` command producing a failing CommandResult (trivial), a `rethrow` signal in TryCommand, and a write-once-per-failure `_failure` string the executor stops auto-clearing. The architectural obstacle: rethrow and cause chaining presuppose a `ScriptError` value to re-raise and chain — which does not exist (errors are strings) — and presuppose named catch binding (also absent). So three of the four pieces depend on unbuilt infrastructure from the typed-errors item; only bare `raise:` and a durable string stand alone.

**Effort.** M. Blast radius: a new `raise` StepType + handler (StepType enum, six ScriptParser arrays, ValidateSteps, autocomplete, FlowCanvasBridge step tables, React block registry — the standard ~12-file new-command tax); plus a small ScriptExecutor change to populate/preserve a `_failure` variable (mirroring the `_last_error` set sites at 360/375 but skipping the success-clear at 364). rethrow + cause chaining are blocked on the typed-error model and are not implementable cheaply.

**Rationale (why deferred).** Deferred as marginal because, like its sibling, the high-value half (rethrow, cause chaining) depends on three unbuilt features — named catch binding, a `ScriptError` type, and nested property resolution — none of which exist (verified: errors are flat strings, catch has no binding). The standalone pieces (a `raise` keyword, a durable failure string) are real but low-novelty: `raise` overlaps `exit: {status: failure}`, and a durable `_failure` is a small executor tweak.

**Salvageable sliver.** Two cheap slivers: (1) a bare `raise: <message>` command that returns CommandResult.Fail (or ApplyOnError) — distinct from `exit` because it is catchable by try/catch; (2) a durable `_failure` string the executor sets on failure and does NOT auto-clear on subsequent success, giving post-recovery scripts something to inspect.

---

### 10. Format Document command (auto-fix YAML hygiene)

**Area:** DevEx  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 7/10 (M) · risk 7/10 · risk band medium · breaking: no

> **Recommendation — ✅ Salvage now:** The lexical tabs->spaces/trim pass reuses existing EditorTextUtilities helpers, is comment-safe, and closes the loop on warnings the validator already emits — high value without the nonexistent serializer.

**Problem.** There is no canonicalize/format action for scripts. Authors fix tabs, trailing whitespace, and inconsistent indentation by hand. The editor surfaces YAML-hygiene warnings (EnableYamlHygieneWarnings in ScriptEditorValidationService) but cannot apply fixes.

**Design.** The proposal wanted a Format Document command that auto-fixes YAML hygiene, implicitly by parsing to the model and re-serializing canonically. The central premise is false: there is no Script-to-YAML serializer in the engine. The only YAML emission is FlowCanvasBridge.TrySerializeStepYaml, which serializes a graph JObject per-step (built for canvas round-trips), not the parsed Script model — and even that goes through a fresh SerializerBuilder that would discard comments, blank-line structure, key ordering, and any keys the model doesn't capture. So a parse-and-reserialize formatter would silently destroy comments and drop unknown/round-tripped content, which is unacceptable for source files. A faithful formatter would instead need a comment-preserving lexical rewriter, not a model round-trip.

**Effort.** M as scored for the naive model round-trip, but that path is wrong; a comment-preserving formatter is larger. Touch points: a new editor command in ScintillaScriptEditorControl/validation service, and either a new Script serializer (does not exist) or a lexical pass over EditorTextUtilities primitives.

**Rationale (why deferred).** Deferred because the implied implementation (parse + reserialize) is destructive given no Script serializer and a graph-only bridge emitter, and the safe implementation is a different, narrower piece of work than 'format document'. Classic false-premise deferral.

**Salvageable sliver.** Ship a purely lexical hygiene pass: tabs->spaces (EditorTextUtilities already maps a tab to 2 spaces in GetLeadingWhitespace/CountIndent), strip trailing whitespace (TrimEnd already used), and normalize final newline — applied as an editor command, never touching the parser. Comment- and structure-safe, and it directly satisfies the EnableYamlHygieneWarnings the validator already raises.

---

### 11. Cross-host result aggregation and a fleet-level after_all/diff summary

**Area:** Modularity  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 9/10 (XL) · risk 8/10 · risk band high · breaking: no

> **Recommendation — ✅ Salvage now:** The full fleet-aggregation feature fights the per-host isolation model across three dispatch paths, but the cheap `diff()` built-in is genuinely missing and pairs directly with the already-working writefile-append cross-host pattern.

**Problem.** Each host runs the script in total isolation: `SshExecutionService.ExecuteScriptAsync` loops hosts and runs `ExecuteScriptOnHost` in a fresh `Task.Run`, each building its own `ScriptContext` — there is no shared fleet-level state and no post-loop hook inside the engine. A script cannot natively say 'after every host finishes, compare their outputs' or 'print 3 of 10 hosts drifted'. The only cross-host channel today is filesystem side-effects: `WriteFileCommand` with `mode: append` (which exists), letting each host append to a shared file that something else reads afterward. There is also no `diff()` function (grep confirms none in Functions/ or JsonUtilities), so even comparing two captured configs requires hand-rolled logic.

**Design.** The proposal wanted cross-host result collection plus a top-level `after_all:` block that runs once after the fleet completes, including a diff/summary view. A realistic shape would need a fleet-scoped result store living *above* the per-host `ScriptContext`, plus an `after_all` phase invoked after the host loop. The blocking obstacles are structural: (1) `after_all` has no single place to run — there are at least three host-looping dispatch paths (`ExecuteScriptAsync` sequential per-host, the folder-execution parallel host-batch path with `runPresetsInParallel`, and `JobExecutionService`), and the engine returns `List<ExecutionResult>` to those callers rather than owning the fleet boundary. (2) The per-host `Task.Run`/parallel-batch model means results converge only at the caller, not inside the script engine, so `after_all` would have to be hoisted out of `ScriptExecutor` entirely. (3) Even single-host parallelism is a no-op for sends because of the `(1,1)` `_commandExecutionLock` in `SshShellSession`, so the 'aggregate parallel branch results' framing rests on concurrency that does not exist for shell work. Prerequisite: a fleet-orchestration layer the engine currently does not have.

**Effort.** XL. Blast radius: a new top-level `after_all` key in `ScriptParser.KnownTopLevelKeys` + parse/validate, a new fleet-result store and second execution phase plumbed into all three host-looping paths (`SshExecutionService.ExecuteScriptAsync`, the folder-execution batch loop, `JobExecutionService`), changes to how results flow back to Form1, plus StepType/validation/autocomplete/FlowCanvasBridge work for the new block. High risk because it reshapes the host-loop boundary that multiple callers depend on.

**Rationale (why deferred).** Deferred (XL, high risk) because the headline use case already works via `writefile` append (documented) and the `after_all` construct collides with three independent dispatch paths plus the parallel-batch model — there is no single fleet boundary inside the engine to attach it to. The parallel-aggregation motivation is further undercut by the `(1,1)` session lock that serializes sends regardless. This 'fights the architecture' (per-host isolation by design) far more than it delivers value.

**Salvageable sliver.** Add a pure `diff(a, b)` string built-in (unified-diff style) to a Functions category — confirmed absent today. It is small, self-contained, and immediately useful with the existing `writefile`-append + read-back idiom for config-drift comparison, with none of the fleet-orchestration risk.

---

### 12. Strict-by-default identifier/function resolution with diagnostics and a tolerant escape hatch

**Area:** Expressions  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 8/10 (L) · risk 7/10 · risk band medium · breaking: yes

> **Recommendation — ✅ Salvage now:** The invalid-regex editor warning is a cheap, non-breaking, scope-model-free win that catches a real silent failure, while the strict-resolution core rests on a false premise and a missing scope model.

**Problem.** Undefined identifiers and misspelled variables resolve silently to their own literal name. In ValueResolver.ResolveExpressionValue, an unknown bare token falls through to `return expr;` (the literal string), so `${hostnam}` becomes the text 'hostnam' and a condition like `usrname is empty` resolves the undefined name to the non-empty string 'usrname' and returns FALSE — the opposite of the author's intent, with no warning. Misspelled functions are also quiet in the string-path: ResolveVariableExpression only enters the function branch on a `(`, and ExpressionEvaluator's `matches` operator swallows a bad regex via `catch { return false; }` (lines 100-103), so a typo'd pattern is indistinguishable from a legitimate non-match.

**Design.** The proposal wanted strict-by-default resolution: unknown identifiers/functions raise diagnostics (ideally at edit time) with a tolerant opt-out. Realistic shape would require threading an 'undefined' sentinel distinct from a literal string through ValueResolver.ResolveExpressionValue and every consumer (IsEmptyValue, AreEqual, ResolveNumeric, the `??` path in ExpressionParser that deliberately returns null for undefined to enable coalescing), plus a strict/tolerant flag on ScriptContext. The architectural obstacle: there is no scope model. ScriptContext keys a flat variable dictionary, foreach injects iterators into that shared dict, and host-grid columns/env vars are merged in dynamically — so an editor cannot statically know which identifiers are defined (the roadmap notes ExtractDynamicSymbols is flat/global, not scope-aware). The flagship example in the proposal is also factually wrong (`usrname is empty` yields false, not true, confirmed above), and the regex-swallow it wants to make strict is documented intentional behavior. Prerequisite: real loop/block scoping (Tier 1.8) before any edit-time undefined-var detection is even feasible.

**Effort.** L, breaking — touches ValueResolver.cs (the core return-literal behavior at ~line 212), ExpressionEvaluator.cs and ExpressionParser.cs (every resolution site that currently treats undefined as a literal or null), ScriptContext (new strict flag + the `??`/coalescing interaction), and the editor validation/autocomplete pipeline for diagnostics. Flipping the default reinterprets any script that relies on a bare unknown token resolving to its own text, and breaks the deliberate undefined->null contract that powers `??`.

**Rationale (why deferred).** Deferred because two of its premises are false (the flagship `is empty` example, and treating the documented regex-swallow as a bug), and the headline edit-time undefined-variable detection is not feasible without a scope model the engine doesn't have. It fights the architecture: the flat shared dictionary plus dynamically-merged host/env vars means 'undefined' cannot be determined statically, and the runtime return-literal behavior is load-bearing for `??` coalescing.

**Salvageable sliver.** Surface invalid regex patterns instead of swallowing them. In ScriptEditorValidationService, compile `extract.pattern`/`matches`/expect patterns at validation time and emit an Error on RegexParseException (the service today only re-maps parser messages and never compiles user regex). This is self-contained, non-breaking, and is the same Phase-1 sliver Tier 3.3 already names — high value, no scope model required.

---

### 13. Honest numbers: preserve integer identity, add exponent/integer-division, and make == tolerance explicit

**Area:** Data types  ·  **Original verdict:** reject  ·  **Scores:** value 3/10 · effort 8/10 (L) · risk 9/10 · risk band high · breaking: yes

> **Recommendation — ✅ Salvage now:** `approx()` is a tiny additive MathFunctions entry that unblocks the long-term goal of removing the surprising implicit equality tolerance, while the rest of the proposal fights the string-canonical model.

**Problem.** Numbers have no stable identity. `set: i = 0` stores an `int` (ValueResolver.ResolveExpressionValue, line 206), but the documented loop idiom `set: i = i + 1` routes through ExpressionParser, where ResolveTokenValue coerces every numeric variable to `double` (ExpressionParser.cs lines 462-465) and ParseAddSubtract returns `double` — so the counter silently becomes a floating-point value after one iteration. Equality is even more inconsistent: ExpressionEvaluator.AreEqual (ExpressionEvaluator.cs:411) treats two numeric strings as equal when within a hardcoded 0.0001 tolerance (so `1.00001 == 1` is TRUE), while ExpressionParser.CompareValues (ExpressionParser.cs:146) does exact `CompareTo`. There is no integer division and no exponent operator.

**Design.** The proposal wanted real integer identity, an integer-division operator (`//`), an exponent operator, and an explicit-only `==` tolerance. Realistically this means making ExpressionParser track int vs double through every arithmetic node, changing ResolveTokenValue to stop blanket-coercing to double, and reworking both AreEqual and CompareValues onto one comparison helper with no implicit epsilon. The specific obstacle is that the entire engine is string-canonical: variables live in `Dictionary<string,object?>` and are rendered via `value?.ToString()` (ScriptContext.GetVariableString), so even if ExpressionParser preserved `int`, the value is re-stringified on the next interpolation and re-sniffed on the next read — there is no type to preserve across a `set`. `//` also collides with URLs/paths in string-concatenation contexts. Prerequisite: a single shared comparison/truthiness helper (roadmap 2.6 Phase 1).

**Effort.** L. Blast radius: ExpressionParser.cs (arithmetic + token-value coercion, ~8 methods), ExpressionEvaluator.AreEqual/ResolveNumeric, ValueResolver.ResolveExpressionValue numeric parsing, MathFunctions int/double return convention (already mixes int/double via IsInteger), plus the operator-detection in SetCommand.HasExpressionOperator. New operators touch the parser grammar and the locked arithmetic tests. No StepType/FlowCanvas changes, but breaks the documented loop-counter pattern (ScriptSamples/README.md:117-121) and the 0.0001-tolerance tests.

**Rationale (why deferred).** Deferred as reject — it fights the architecture (string-canonical variable store) rather than being merely low value. The flagship integer-identity guarantee cannot hold because there is no persistent typed slot; values round-trip through strings. `pow()`/`floor()`/`ceil()` already exist in MathFunctions, removing most of the exponent/floor motivation. `//` is genuinely ambiguous with URLs. The breaking-change surface (loop idiom + tolerance tests) is large for the payoff.

**Salvageable sliver.** `approx(a, b, epsilon)` as an explicit-tolerance comparison function (mirrors the existing iif/clamp pattern in MathFunctions), so authors who actually want fuzzy float comparison opt in, paving the way to later strip the implicit 0.0001 from AreEqual. This is the one sliver the roadmap names.

---

### 14. Robust YAML normalization to retire the PreprocessYaml regex hack

**Area:** Syntax  ·  **Original verdict:** marginal  ·  **Scores:** value 3/10 · effort 8/10 (L) · risk 7/10 · risk band high · breaking: no

> **Recommendation — ✅ Salvage now:** Extending the allowlist with the handful of missing free-text keys delivers the entire real-world benefit at S effort, while the full normalizer rewrite is unjustified L-effort work against false premises.

**Problem.** Today ScriptParser.PreprocessYaml (ScriptParser.cs:597) line-scans the source with PlainScalarLineRegex and, for a hardcoded allowlist of ~24 keys in ScalarValueKeys (ScriptParser.cs:582), auto-single-quotes plain scalar values that contain `: ` or ` #` so YamlDotNet does not misparse them as nested maps or comments. The real gap is the allowlist's coverage: free-text-bearing keys NOT in the set (e.g. log/notify message variants, `title`, `label`, `level`, reply text, etc.) still hit raw YAML, so an operator who writes an unquoted message containing a colon gets a confusing mapping parse error rather than the intended string. The regex itself is a maintenance smell, but it is functionally narrow, not broken.

**Design.** The proposal wanted to delete the regex hack and replace it with a principled normalization pass (or rely on a real deserializer). The realistic shape, given the engine, is modest: extend ScalarValueKeys with the missing free-text keys. The reason a wholesale replacement is hard is the parse architecture: ScriptParser.Parse (ScriptParser.cs:466) does NOT use the YamlDotNet `_deserializer` it builds in the constructor (ScriptParser.cs:402) — that field is dead. Parsing is a hand-rolled `new Parser(reader)` event walk (ScriptParser.cs:477). So there is no object-model round-trip to harden; any 'robust normalizer' would have to either re-architect onto a typed deserializer (huge, fights the whole flexible-step grammar) or reimplement quoting decisions the regex already makes. The proposal's premise that the regex shifts line numbers is false: PreprocessYaml rewrites lines in place (`lines[i] = ...` then `string.Join("\n", lines)`), never inserting, so diagnostics line numbers are preserved.

**Effort.** L (label) for the proposed full replacement — it would mean re-pointing the parser at a real deserializer or building a tokenizing normalizer, touching the entire hand-rolled parse path in ScriptParser.cs and risking every step-grammar edge case. The salvage is S: add ~8 missing free-text keys to the ScalarValueKeys HashSet (ScriptParser.cs:582), one line each, plus a regression test per added key.

**Rationale (why deferred).** Deferred as marginal on false premises. The roadmap correctly notes line counts are preserved (verified) and there is no DSL-schema dependency to build first. The 'robust normalization' framing implies the current approach is fragile in ways it is not — it is a small, in-place, allowlist-gated rewrite. The high effort/risk score reflects the cost of replacing a working narrow hack with a general parser change for near-zero added correctness.

**Salvageable sliver.** Add the missing free-text keys (message/title/label/level/reply and similar string-valued option keys) to the ScalarValueKeys allowlist in ScriptParser.cs:582 so unquoted values containing `: ` stop producing surprise parse errors. Cheap, targeted, no architecture change.

---

## 🟡 Reconsider later

### 15. Heredoc-style remote file push: render a templated document straight to a remote path

**Area:** Interactive SSH  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 5/10 (M) · risk 3/10 · risk band low · breaking: no

> **Recommendation — 🟡 Reconsider later:** An `sftp content:` option is a clean, low-risk ergonomic win, but low-demand — worth doing only when SFTP work is already open.

**Problem.** Pushing a generated config/script file to a remote path requires two steps and a local temp file: `writefile` to a local path (WritefileOptions, content rendered with ${} substitution) then `sftp: { action: upload, local_path, remote_path }` (SftpCommand only supports upload/download, line 25; it reads bytes from LocalPath, no inline content). There is no single 'render this document to /etc/foo.conf' step, and remote `owner`/`mode` cannot be set over pure SFTP.

**Design.** Proposal wanted a heredoc-style step that renders a templated document directly to a remote path. Realistic shape (roadmap's recommendation): add an inline `content:` mode to the existing SftpCommand — when `action: upload` and `content:` is present instead of `local_path`, substitute variables and stream the rendered string to a temp/MemoryStream and `sftp.UploadFile`. The obstacle: SftpCommand currently branches strictly on upload/download reading from `LocalPath` (line 37/74-79); the `owner`/`mode` part of the proposal is impossible over the SFTP protocol surface Rebex exposes (would need a follow-up `send: chown` over the shell session), so that sub-feature is dead on arrival.

**Effort.** M — confined to SftpCommand.cs (new content branch), the `sftp` key table in ScriptParser (line 133) + FlowCanvasBridge (line 257) + bridge serializer, and autocomplete. No new StepType needed if added as an `sftp` option, which keeps the dependency-analyzer Sftp case unchanged.

**Rationale (why deferred).** Deferred marginal because `writefile`+`sftp upload` already accomplishes the file push, and the differentiating `owner` feature can't be done over pure SFTP. Low incremental value: the only real win is eliminating the local temp file. Not fighting the architecture — just thin.

**Salvageable sliver.** Add an inline `content:` field to the existing `sftp upload` action (render ${}/{{ }} then upload the rendered string), avoiding a new StepType. Drop `owner`/`mode` entirely.

---

### 16. Generic structured parse: json/csv/kv/ini, whitespace tables, and a json get command

**Area:** Functions  ·  **Original verdict:** marginal  ·  **Scores:** value 6/10 · effort 7/10 (L) · risk 6/10 · risk band low · breaking: no

> **Recommendation — 🟡 Reconsider later:** The csv/kv parsers are real wins, but the headline record ergonomics stay broken until general dotted member access lands, so the full feature should wait on that prerequisite while the parser sliver can be cherry-picked.

**Problem.** Today the parse command only supports FortiGate-family formats. ParserFactory._parsers (ParserFactory.cs:11-16) registers exactly two keys, both mapping to FortiGateParser; there is no json/csv/kv/ini/whitespace-table parser. The parser key-table accepts parse with [format, from, into, sections] (ScriptParser.cs:135) but does not validate the format value, so an unknown format like csv parses cleanly and only fails at runtime via ParserFactory.GetParser's ArgumentException (caught in ParseCommand.cs:50-53). For tabular CLI output (e.g. Cisco show commands) operators are stuck scraping one field at a time with extract ... match: all into a flat List<string> (see ScriptSamples/cisco/ios_foreach_interfaces.yaml:19-30) — they cannot pull a whole row of fields into a structured record.

**Design.** The proposal wanted a generic parse front-end (json/csv/kv/ini + whitespace tables) plus a json get command, with the showcase `foreach r in records` then `${r.iface}`. The realistic shape: new IConfigParser implementations registered in ParserFactory, returning the existing Dictionary<string,object> shape that ParseCommand.cs:59 already stores. The hard obstacle is the consumption side, not the parsing side: there is no general dotted member access. ResolveVariableExpression (ScriptContext.cs:608-664) only special-cases `.length` (via ValueResolver.TryResolveLengthExpression, 614-619), `[index]` (621-644), and inline `(...)` function calls (646-660) — `${r.iface}` falls through to GetVariableString("r.iface") and resolves to empty. So the flagship per-row record access cannot work without first building member resolution. The separate json get is redundant: json.get already does deep path access via JsonPathNavigator.Navigate (JsonFunctions.cs:91-95, dispatched at JsonUtilities.cs:706-708). Note dict iteration (`foreach: k, v in map`) HAS since landed (ForeachCommand.cs:20,52-69), partially eroding the original 'no object iteration' premise — but single-record member access still does not exist.

**Effort.** L. Per-format parser work is modest (one IConfigParser + ParserFactory registration each, no StepType change since parse already exists), but the value-unlocking prerequisite is the expensive part: adding general dotted member resolution touches ScriptContext.ResolveVariableExpression and ValueResolver (the third resolution path), plus parity in lambda bodies (LambdaExpression/ExpressionParser) and the foreach metadata naming. Secondary touch points: format-value whitelisting/did-you-mean in ScriptParser, ScriptDependencyAnalyzer (currently blind to parse format), autocomplete for new format names, and FlowCanvasBridge if the Properties panel offers a format dropdown.

**Rationale (why deferred).** Deferred as marginal because the showcase does not work against the engine: structured records are useless without member access, and the json get half duplicates the existing json.get/JsonPathNavigator path. It is partly a false premise (get already exists; dict iteration now exists too) and partly fights the architecture (flat string-keyed variable model with no dotted resolution). The csv/json parsers themselves are genuinely missing and have real value for tabular CLI output.

**Salvageable sliver.** Land the csv and kv parsers as new IConfigParser implementations (mirroring FortiGateParser, registered in ParserFactory.cs), and consume them with the already-shipped json.get path-string access (e.g. json.get(records, "0.iface")) plus the existing `foreach: k,v in map` form — skip the new get command entirely and skip the member-access-dependent `${r.iface}` showcase. Also cheap: validate the parse format value at parse time against ParserFactory.GetAvailableFormats() with a did-you-mean, converting the runtime-only ArgumentException into an editor error.

---

### 17. Script-level strict mode (set -euo pipefail) for fail-fast and undefined-variable safety

**Area:** Error handling  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 7/10 (M) · risk 7/10 · risk band medium · breaking: no

> **Recommendation — 🟡 Reconsider later:** The undefined-variable lint is worth doing but, per Tier-3 item 3.6, needs scope-aware symbol tracking the analyzer lacks today, so it should follow that prerequisite rather than ship as part of a bash-style strict mode.

**Problem.** Two real footguns. (1) Remote command failure is silent by default: `send.FailOnNonZero` defaults to `false` (ScriptStep.cs:319), so a remote command that exits non-zero still reports step success unless the author opts in per-send. (2) Undefined-variable references resolve to empty string silently: ScriptContext.GetVariableString returns `value?.ToString() ?? ""`, so a typo like `${hostnam}` produces an empty interpolation, not an error — a command can run against a host with a blank argument and no warning.

**Design.** The proposal wanted a bash-style `set -euo pipefail` toggle: fail-fast on any non-zero remote command, error on undefined variables, plus 'companion flags.' Realistic shape if built: a script-level flag that flips the `send` exit-status default to on and a pre-execution lint for undefined `${...}`/`{{...}}`. The architectural obstacle is the `-e` half: making non-zero fail by default assumes a POSIX shell, but SendCommand's only exit-status mechanism is a POSIX-specific wrapper — `eval '<cmd>'; $?; printf sentinel` (SendCommand.cs:134-137) parsed by a regex (line 20) — which produces garbage on FortiGate/Cisco/network CLIs that have no `$?`/`eval`. It is also mutually exclusive with `expect` (a hard error at SendCommand.cs:41), so a blanket strict mode would break every interactive flow. The advertised companion flags (`-u`/`-o pipefail` equivalents) have no engine representation at all — no strict/pipefail/euo symbol exists anywhere in Services/Scripting.

**Effort.** M. Blast radius for a responsible version: the `-u` (undefined-variable) half is a self-contained static lint over interpolation tokens in the validation service (no runtime risk); the `-e` half would touch SendCommand exit-status defaulting, the POSIX sentinel path, the send/expect mutual-exclusion check, and every device-shell sample — high risk for low portability. A real `set -e` across heterogeneous shells is not safely achievable.

**Rationale (why deferred).** Deferred because the central `-e` premise is dangerous against the engine's multi-shell reality: fail-on-nonzero is opt-in precisely because the sentinel mechanism only works on POSIX shells, and forcing it on would mis-handle FortiGate/Cisco targets and collide with `expect`. The companion flags it advertises do not exist. This 'fights the architecture' rather than being merely low-value.

**Salvageable sliver.** The undefined-variable safety, implemented as a static pre-execution lint (not a runtime `-u`): scan `${...}`/`{{...}}` interpolations and warn on references to symbols never assigned by `set`/`vars`/`extract`/CSV columns/loop iterators. Scope it to interpolation tokens only (bare identifiers collide with `is defined`/`is empty`). This catches `${hostnam}` typos without touching the shell-portability minefield.

---

### 18. Default-value, required-value, and ?? / ?: coalescing inside interpolation

**Area:** Data types  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 7/10 (M) · risk 7/10 · risk band medium · breaking: yes

> **Recommendation — 🟡 Reconsider later:** Inline coalescing is genuinely useful but only becomes safe and cheap after the two expression engines are unified (roadmap 2.5) so interpolation can reuse ExpressionParser without a dual-mode flag; ship the editor diagnostic in the meantime.

**Problem.** `??` and `?:` already work in `set:` (SetCommand.HasExpressionOperator detects `?`, and ExpressionParser.ParseNullCoalesce/ParseTernary handle them, lines 51-87), but NOT inside plain `${...}`/`{{...}}` interpolation — ScriptContext.ResolveVariableExpression has no coalescing path, so `${name ?? "default"}` resolves the whole thing as a (missing) variable name and yields empty. There is also no `${name?}` required-or-fail and no inline default syntax. So an operator cannot write a one-liner fallback in a `print:` or a `send:` command argument.

**Design.** The proposal wanted `??`, ternary, an inline-default, and a required-marker usable directly inside interpolation braces. Realistic shape: route the interior of `${...}` through ExpressionParser (which already implements `??`/`?:`) instead of the bespoke ResolveVariableExpression, OR add a coalesce check in ResolveVariableExpression. The obstacle named by the review is the breaking 'null-only redefinition': to make `??` meaningful inside interpolation you must redefine what counts as the falsy/null trigger, and ExpressionParser.IsNullOrEmpty (line 175) treats only null and empty-string as triggering — reconciling that with interpolation's empty-string-for-missing semantics forces either a behavior change or a permanent dual-mode flag (interpolation-coalesce vs set-coalesce), which the review judged not worth carrying.

**Effort.** M. Blast radius: ScriptContext.ResolveVariableExpression (add coalesce/ternary dispatch), and a decision on whether to reuse ExpressionParser there — which pulls the expression engine into the 149-call-site SubstituteVariables hot path. Plus autocomplete/editor hints, and the dependency analyzer (VarRefPattern would capture `name ?? "default"` as a single ref and AddResolvedVarName would mis-parse it). Breaking because it changes how existing `${a ?? b}` literals (currently treated as a var name) resolve.

**Rationale (why deferred).** Deferred as marginal: `??` already works in `set:`, so the gap is only the interpolation site, and closing it requires either a breaking redefinition of null-handling or a permanent dual-mode flag. The cost/risk of touching the SubstituteVariables hot path outweighs the convenience of inline fallbacks.

**Salvageable sliver.** The editor diagnostic — warn when `${...}` contains `??`/`?:` (which silently resolves to empty today), pointing the author to use `set:` first. The roadmap explicitly calls the diagnostic 'the real value.' This is a validation-service-only addition with no runtime risk.

---

### 19. Executor-enforced per-step and per-block timeouts

**Area:** Control flow  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 6/10 (M) · risk 6/10 · risk band medium · breaking: no

> **Recommendation — 🟡 Reconsider later:** A real per-step timeout requires every handler to honor cancellation (a large prerequisite); only the between-iterations loop budget is safely deliverable now.

**Problem.** `send.timeout` exists and works (SendCommand.cs:68 passes `timeoutSeconds` into the session, which honors it via the Rebex polling loop). But there is no generic per-step timeout for non-send commands and no per-block (foreach/try/parallel) wall-clock cap. A `localcmd`, `http`, `input` prompt, or an entire `foreach` can hang indefinitely. The motivation cited was bounding non-cooperative handlers — but those are exactly the ones a token can't interrupt.

**Design.** The proposal wanted an executor-level `timeout:` on any step/block that cancels via `CancellationTokenSource.CancelAfter`. The realistic shape: wrap `ExecuteStepCoreAsync`/`ExecuteStepsAsync` in a linked CTS with `CancelAfter`. The specific obstacle is cooperative cancellation. The SSH path doesn't observe the token for interruption — `SshShellSession` drives timeouts by mutating `_scripting.Timeout` (Rebex) inside a polling loop (SshShellSession.cs:692-876), not by awaiting the token; and synchronous handlers like `LocalCmdCommand` or a modal `InputCommand` dialog won't observe a cancel either. So `CancelAfter` would fire the token but the in-flight operation keeps running until its own internal timeout — the timeout becomes advisory, not enforced, for precisely the hang cases cited. `category: timeout` in the proposal references a typed-error system that doesn't exist (only the flat `_last_error` string).

**Effort.** M. Mechanically small — a linked CTS around dispatch in `ScriptExecutor.ExecuteStepCoreAsync` (~line 463) and around block iteration, plus a `timeout` key on more commands in the parser key tables. But verifying it actually interrupts each of ~40 handlers (most of which don't honor a token) is the real cost, and several never will (Rebex polling loop, modal dialogs, `Process` waits).

**Rationale (why deferred).** Deferred because the flagship justification is false: `CancelAfter` cannot interrupt the non-cooperative handlers it was meant to bound, and `category: timeout` references a non-existent typed-error system. `send` — the one place where a timeout is genuinely enforceable — already has one. So the feature would ship a guarantee it can't keep.

**Salvageable sliver.** A per-block soft wall-clock budget for `foreach`/`while`/`repeat` that checks elapsed time at the top of each iteration (cooperative, between steps) and aborts the loop with a clear message. This is honest (it only bounds at iteration boundaries) and needs no token plumbing into handlers.

---

### 20. Operator aliasing (&& || !), whitespace-tolerant operators, chained comparisons, and between

**Area:** Expressions  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 6/10 (M) · risk 7/10 · risk band medium · breaking: yes

> **Recommendation — 🟡 Reconsider later:** The valuable core (consistent operators across both engines) is really a slice of the 2.5 engine-unification work, so revisit it there rather than as a standalone breaking patch to one evaluator.

**Problem.** Logical operators only exist as space-padded words in ExpressionEvaluator (`FindLogicalOperator` searches for the literal `" or "`, `" and "`, `" not "` with required surrounding spaces). `a&&b`, `a && b`, or `!flag` are not recognized as logical operators at all — they fall through to the truthy-check branch (EvaluateComparison final `return ValueResolver.IsTruthyValue`), so `a && b` is evaluated as one truthy string (non-empty -> true) with no error. Comparisons are strictly binary: `1 < x < 10` has no chained-comparison support and `between` does not exist. Worse, the arithmetic engine (ExpressionParser) used by `set:` and lambda bodies has no logical operators at all, so `x => x > 5 and x < 10` silently misbehaves inside a lambda.

**Design.** The proposal wanted C-style aliases (`&&`/`||`/`!`), whitespace tolerance so `a&&b` parses, `a < b < c` chaining, and `x between 1 and 10`. Realistic shape: pre-normalize aliases to word operators and relax the space requirement inside ExpressionEvaluator.FindLogicalOperator (tokenize rather than substring-match), then add `between`/chained-comparison handling in EvaluateComparison. The specific obstacle: there are two engines and this only touches ExpressionEvaluator (if/while/assert/foreach), leaving `set:`/lambda bodies (ExpressionParser) still operator-less — so `&&` would work in `if:` but not in a `filter()` lambda, deepening the existing split the roadmap calls out as the root cause in item 2.5. Relaxing whitespace is also genuinely breaking: a value like `flag&&other` that today reads as a single truthy token would flip to a logical-AND, and the `!` alias collides with `!=` detection order. Prerequisite: this should ride on 2.5 (unify the engines) rather than patch one side.

**Effort.** M, breaking — ExpressionEvaluator.cs (FindLogicalOperator tokenization, EvaluateComparison new operators) is the core; to avoid the half-working split it must also touch ExpressionParser.cs (ParseComparison for chaining/aliases). Autocomplete/highlighter operator lists. The whitespace-relaxation and `!`-vs-`!=` ordering need careful test coverage; ExpressionEvaluatorTests exists but has no `&&`/chained/`between` cases (the path for these new forms is currently untested).

**Rationale (why deferred).** Deferred as marginal because word operators (`and`/`or`/`not`) already cover the actual need, so aliases are cosmetic; and it targets the wrong (single) engine, so it would create a new inconsistency rather than fix one. It is also genuinely breaking — relaxing whitespace reinterprets strings that currently pass the truthy fallback — which is high risk for low payoff on a feature most users won't miss.

**Salvageable sliver.** The unspaced-operator silent-truthiness footgun: `a && b` / `a and b` (no surrounding spaces) silently degrading to a single truthy check is a real correctness trap. A cheap, non-breaking sliver is a parse/edit-time WARNING when an expression contains `&&`/`||`/`!` or a word-operator with missing spaces, telling the author to use spaced word operators — caught in the validation service, no semantic change.

---

### 21. Idempotency primitives: unless/creates, changed_when, failed_when, and a per-host change tally

**Area:** Control flow  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 8/10 (L) · risk 6/10 · risk band medium · breaking: no

> **Recommendation — 🟡 Reconsider later:** `changed_when`/`failed_when`/tally need the not-yet-built structured result register, but the `creates`/`unless` skip-guard sliver is worth pulling forward independently.

**Problem.** The engine has no idempotency surface. `send` cannot skip when a file already exists (`creates:`), cannot redefine success/failure based on output (`changed_when`/`failed_when`), and there is no per-host 'N changed / M ok' tally. Today the closest is `fail_on_nonzero` (SendCommand.cs:90-100), which keys purely on POSIX exit status — unreliable on network CLIs (FortiGate/Cisco) that don't return meaningful rc, and there's nothing for 'changed'. So a re-run of a config script can't report or skip already-applied changes.

**Design.** The proposal wanted Ansible-style `unless`/`creates` (skip guards), `changed_when`/`failed_when` (output-driven outcome redefinition), and a change tally. Realistic shape: `unless`/`creates` are buildable as pre-execution guards (an extra send + check before the main send, similar to how `when:` gates a step in ExecuteStepsAsync:313-329). `changed_when`/`failed_when` would need to evaluate an expression against captured output AFTER the send and override `CommandResult`. The core obstacle: these are built on the 'structured command result register' (rc/stdout_lines/failed) that does NOT exist — `send.capture` stores only raw stdout (SendCommand.cs:102 `RecordCommandOutput`), no rc/lines/failed siblings. And `failed_when` directly overlaps `fail_on_nonzero` while being more general, creating two competing failure models. The change tally needs a thread-safe per-host counter (the soft-assert ledger in ScriptContext is the only precedent).

**Effort.** L. Touches SendCommand (pre-guard for `creates`/`unless`, post-evaluation for `changed_when`/`failed_when`), ScriptStep model (4+ new options), ScriptParser send key table + per-branch parsing + validation, FlowCanvasBridge props, dependency-analyzer (new defined/used vars for any tally), plus a new tally counter in ScriptContext/SharedScriptExecutionState. `failed_when` vs `fail_on_nonzero` reconciliation adds design risk.

**Rationale (why deferred).** Deferred because it's built on a structured command-result register that doesn't exist, `failed_when` overlaps the existing `fail_on_nonzero`, and rc is unreliable on the network CLIs this tool targets — so the 'changed' semantics would be guesswork. The dependency on Tier 2.1 makes it premature.

**Salvageable sliver.** `creates:`/`unless:` skip guards on `send` are independently valuable and buildable now without the result register — run a probe command, evaluate, and skip the main send (reusing the existing `when:`-skip path and StepCompleted.Skipped flag). That delivers the most-requested idempotency win cheaply.

---

### 22. Inventory groups with layered group/host variables and run-time limit selection

**Area:** Modularity  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 8/10 (L) · risk 5/10 · risk band medium · breaking: no

> **Recommendation — 🟡 Reconsider later:** Group-branching already works and layered vars fight the flat model, but the carved-out run-time host `limit:` selector is genuinely useful and worth revisiting as a standalone host-selection feature.

**Problem.** There is no inventory abstraction: hosts arrive as a flat `List<HostConnection>` and their CSV columns are injected into each per-host `ScriptContext` as a flat `Dictionary<string,string>` of `initialVariables` (ScriptContext constructor). There are no group definitions and no layered group-then-host variable precedence — to branch on group membership today an operator puts a `group` column in the CSV and writes `if: "'web' in split(groups, ',')"`, leaning on `ExpressionEvaluator`'s `in` operator (which calls `ValueResolver.CollectionContains`). More importantly there is no run-time `limit:` selector: host selection happens entirely in the UI/job target lists before the engine is invoked, so a script cannot say 'run only against the db group this time'.

**Design.** The proposal wanted a declared inventory of groups with layered group-vars and host-vars (Ansible-style precedence) plus a `limit:` to subset the run at execution time. A realistic shape would add an `inventory:`/`groups:` top-level construct, a variable-layering resolver that merges group-vars under host-vars before seeding `ScriptContext`, and a host-selection filter applied in `SshExecutionService.ExecuteScriptAsync` before the per-host loop. The architectural obstacles: (1) variable resolution is *not* centralized as the proposal assumes — values are seeded once into the flat per-host dict and read through `ScriptContext`/`ValueResolver`, with no layering hook; adding precedence means a new merge stage and touching the seed sites in `ExecuteScriptOnHost`. (2) The valuable `limit:` selector is really a host-selection concern that belongs above the engine (host grid / `JobDefinition` targets), not inside the YAML, so it does not fit the script model cleanly. Prerequisite for clean group-vars: a non-flat variable model or at least a namespaced layer the dependency analyzer understands.

**Effort.** L. Blast radius: new top-level keys in `ScriptParser.KnownTopLevelKeys` + parse/validate paths for `inventory`/`groups`; a new layering merge in the per-host seed path (`SshExecutionService.ExecuteScriptOnHost` / `ScriptContext` initialVariables); host filtering in `ExecuteScriptAsync` and the parallel folder-execution batch path; `ScriptDependencyAnalyzer.ReferencedColumns` logic if group-vars introduce non-column variables; and UI/`JobDefinition` work if `limit:` becomes a target selector. Autocomplete and FlowCanvasBridge would need new node/key awareness for the inventory block.

**Rationale (why deferred).** Deferred as marginal because the headline capability (branch on group) already works via a CSV column + `split()` + `in`, and the claim that resolution is centralized is false — it is flat per-host seeding with no layering seam. The genuinely useful part, run-time `limit:`, is a host-selection feature that is better scoped as a UI/job-target capability than as inventory-in-YAML, so bundling it here inflates effort for low incremental value.

**Salvageable sliver.** Split out run-time host selection as its own small proposal: a `limit:`/target-filter applied in `SshExecutionService.ExecuteScriptAsync` (and the job target path) using the existing `group` CSV column, with no inventory/layering machinery. That captures the one piece operators actually want without the flat-variable-model fight.

---

### 23. Typed structured errors with catch-as binding, when-filter, and re-raise

**Area:** Error handling  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 7/10 (L) · risk 6/10 · risk band medium · breaking: no

> **Recommendation — 🟡 Reconsider later:** The full feature needs the dotted-member-access prerequisite to land first, but the catch-`when` filter is a genuinely useful, cheap sliver worth pulling forward independently.

**Problem.** Today error handling exposes exactly one error datum: the string variable `_last_error`, set in ScriptExecutor.ExecuteStepsAsync (lines 360/375) and wiped on the next successful step (line 364). A `catch:` arm parses to a bare `List<ScriptStep>` (ScriptParser.cs:1144) with no binding and no filter, so inside a catch you cannot tell *what* failed (which command, exit code, regex error vs. SSH timeout vs. assert) — you only have a flat human-readable message. Operators cannot branch a catch on error category, and a catch swallows every failure indiscriminately.

**Design.** The proposal wanted an exception-object model: `catch as err` binding plus `{{err.kind}}` / `{{err.details.exit_code}}` member access and a `when:` filter to catch selectively. The realistic shape would be a `ScriptError` type carrying kind/message/source/details, a populated catch-binding variable, and a nested-property resolver. The hard architectural obstacle is the variable model itself: ScriptContext._variables is `Dictionary<string, object?>` (ScriptContext.cs:99) and resolution in ScriptContext.ResolveVariableExpression only understands `.length` (ValueResolver.TryResolveLengthExpression) and `[index]` (ArrayExpressionRegex) — there is no dotted member access, so the flagship `{{err.kind}}`/`{{err.details.exit_code}}` syntax is literally unresolvable today. Prerequisite: the nested-property resolver from the deferred typed-object work, plus enriching error production across ~38 command handlers (each currently returns CommandResult.Fail(string) via IScriptCommand.cs:77) so a kind/details payload even exists to bind.

**Effort.** L. Blast radius: new ScriptError model; CommandResult (IScriptCommand.cs) gains a structured error payload threaded through every command's Fail path (~38 handlers in Commands/); TryCommand.cs catch dispatch; ScriptExecutor catch-binding population and `_last_error` plumbing; a nested-property resolver in ScriptContext/ValueResolver; ScriptParser catch parsing (1144/1704) + key tables + ValidateSteps; autocomplete and ScriptDependencyAnalyzer for the new `err.*` symbols; FlowCanvasBridge.cs (757-763 renders try/catch/finally as flat branch arms with no per-arm metadata surface).

**Rationale (why deferred).** Deferred because the headline syntax fights the architecture, not because the goal is wrong. The whole value proposition rests on member access (`err.kind`, `err.details.exit_code`) that the flat string-dict + length/index-only resolver cannot express — it is a false-premise flagship. Even the structured payload is gated on enriching dozens of handlers that today only emit a string. High effort for a feature whose marquee example does not run.

**Salvageable sliver.** The `when:` filter on `catch` (catch only when an expression is true, else re-propagate the failure). It rides existing seams: add a `When` to the catch arm in ScriptParser, and in TryCommand.cs gate the existing `if (shouldHandleFailure && step.Catch...)` block on `ExpressionEvaluator.Evaluate(step.CatchWhen)` against the already-set `_last_error`. No new type system needed.

---

### 24. Namespaced contexts: env.*, host.*, and secret() to disambiguate variable origin

**Area:** Data types  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 8/10 (L) · risk 6/10 · risk band medium · breaking: no

> **Recommendation — 🟡 Reconsider later:** The full namespacing fights the flat-store + dependency-analyzer design, but the collision lint is a worthwhile standalone safety win and `host.*` alone is a defensible smaller follow-up once the analyzer learns dotted prefixes.

**Problem.** All variable origins are flattened into one namespace. CSV host columns, script `vars:`, captured output, and loop iterators all land in the same `Dictionary<string,object?>` (ScriptContext._variables), so an operator cannot tell whether `${region}` came from the host grid, a `set:`, or an environment profile — and a host column silently shadows a script var (ImportScriptVars only sets if absent, line 1009, but a later `set:` clobbers a column with no warning). There is no `env.*`, no `host.*`, and no `secret()`; grep confirms zero `env.`/`host.` handling in the resolver.

**Design.** The proposal wanted `env.NAME`, `host.column`, and `secret(name)` prefixes so variable provenance is explicit and secrets are redacted. Realistic shape: special-case the `env.`/`host.` prefixes in BOTH resolution paths (ScriptContext.ResolveVariableExpression for interpolation AND ValueResolver.ResolveExpressionValue for conditions/set), backed by separate maps for environment vs host columns. The concrete obstacle is ScriptDependencyAnalyzer: VarRefPattern is `\$\{([^}]+)\}|...` and AddResolvedVarName truncates a dotted reference at the first `.` (lines 1047-1052), so `${host.region}` would register `host` as a missing grid column and trigger a spurious missing-column warning, while `${env.PATH}` would never be recognized as resolvable. Dot-notation collides with the existing `var.length`/`var.prop` member grammar too. `secret()` additionally depends on a redaction subsystem that does not exist (roadmap 2.9 is unbuilt; grep for secret()/reveal() returns nothing).

**Effort.** L. Blast radius: ScriptContext (split _variables into namespaced stores or add prefix routing in two methods), ValueResolver.ResolveExpressionValue, ScriptDependencyAnalyzer (VarRefPattern + AddResolvedVarName must learn the namespaces or they false-positive), TypeFunctions/GetRawVariable (`${...}` unwrap assumes a flat name), autocomplete, and FlowCanvasBridge variable handling. `secret()` would also need the entire redaction sink (EmitOutput + debugger snapshots).

**Rationale (why deferred).** Deferred as marginal: dot-notation breaks the dependency analyzer's column inference (a real, demonstrated obstacle, not theoretical), requires parallel changes in two resolution paths, and `secret()` rides on an unbuilt redaction subsystem. The provenance problem is real but the solution is invasive for the benefit.

**Salvageable sliver.** Two cheaper pieces the roadmap names: (1) a collision LINT that warns at parse/validate time when a `set:`/iterator name shadows a known host column or script var — pure analyzer work, no resolution change; and (2) `host.*` ALONE as a separate, narrower proposal (host columns are a bounded, known set, unlike open-ended env), avoiding the env/secret complexity.

---

### 25. First-class typed list/object values with one unified deep-path grammar

**Area:** Data types  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 9/10 (XL) · risk 9/10 · risk band high · breaking: yes

> **Recommendation — 🟡 Reconsider later:** The full re-canonicalization is XL and architecture-hostile, but the foreach-over-object member-access sliver becomes cheap and worthwhile once a shared single-hop path resolver exists (a prerequisite shared with roadmap 3.7 TextFSM records).

**Problem.** There is no single way to reach into nested data. Interpolation only supports `var.length` and `var[index]` (ScriptContext.ResolveVariableExpression / ValueResolver.TryResolveLengthExpression + ArrayExpressionRegex); deeper access requires the function form `json.get(data, "a.b.c")` (JsonUtilities.TryDispatchJsonFunction). foreach over an object only works through the special `key, value in map` form (ForeachCommand.DictPattern) and `foreach r in records` then `${r.iface}` does NOT resolve — there is no member access on the loop variable. Lists are canonically `List<string>`, while structured data is `JsonNode`/`JsonElement`, and the two are converted ad hoc all over ValueResolver and JsonUtilities.

**Design.** The proposal wanted one canonical typed value model (lists and objects as first-class) with a unified `a.b.c[0].d` deep-path grammar working everywhere — interpolation, conditions, set, foreach, lambdas. Realistically this means re-canonicalizing on `JsonNode` as the single container type and adding a real path parser shared by ScriptContext.SubstituteVariables, ValueResolver, ExpressionEvaluator, ExpressionParser, and ForeachCommand. The architectural obstacle is scale: `List<string>` is threaded through dozens of call sites (ValueResolver.ResolveCollectionItems/ResolveListValue, every Collection/JSON function, SetCommand push/pop/shift, CloneVariableValue), and the Flow Canvas round-trip goes through FlowCanvasBridge.cs (4,968 LOC) which serializes the graph, not typed values — re-canonicalizing would ripple through all of it. Prerequisite: the unified expression engine (roadmap 2.5) and a shared path resolver.

**Effort.** XL. Blast radius: ValueResolver, JsonUtilities, ScriptContext interpolation, both expression engines, LambdaExpression, ForeachCommand, SetCommand nested-assignment, ScriptDependencyAnalyzer (AddResolvedVarName truncates at first `.`, line 1047 — a deep-path grammar breaks its column inference), plus the ~5K-LOC bridge. Touches StepType-adjacent foreach grammar and autocomplete.

**Rationale (why deferred).** Deferred as marginal/XL because deep indexing already works via `json.get` and the cost is enormous (~5K LOC plus the 4,914-LOC bridge) for low incremental real-world value. It is the textbook case of fighting the architecture: the dual List<string>/JsonNode model is everywhere.

**Salvageable sliver.** Cherry-pick the foreach-over-object bug fix only: make `foreach r in records` (where records is a JSON array of objects) expose `${r.field}` for the current item. This is the one concrete gap the roadmap names; it likely means having ForeachCommand keep the current item as a JsonNode and teaching ResolveVariableExpression to resolve a single `.field` hop off a loop variable — far narrower than the full deep-path grammar.

---

## ⛔ Skip

### 26. First-class string interpolation/format() with padding and number formatting

**Area:** Functions  ·  **Original verdict:** reject  ·  **Scores:** value 2/10 · effort 2/10 (S) · risk 2/10 · risk band low · breaking: no

> **Recommendation — ⛔ Skip:** The headline functionality already ships and is verified in StringFunctions.cs; rebuilding it is pure duplication with no operator-facing gain.

**Problem.** There is no concrete gap today. An operator wanting C#-style formatting, fixed-width columns, or repeated strings already has working primitives: StringFunctions.cs registers format (line 34 -> Format at 254-272, which delegates to string.Format with full .NET format-spec support and CultureInfo.InvariantCulture), pad_left/pad_right (lines 27-28 -> 71-99, with custom pad char), and repeat (line 28 -> 101-114, with a 10000 safety cap). These are callable inside any ${...}/{{...}} interpolation because ResolveVariableExpression routes parenthesized expressions through ValueResolver.ResolveExpressionValue (ScriptContext.cs:646-660). The only real micro-gaps are cosmetic (e.g. format silently returns the raw template on FormatException at 268-271 rather than erroring, and number-formatting must go through the .NET format string rather than a dedicated number() helper).

**Design.** The proposal wanted a first-class interpolation/format() layer with padding and number formatting baked in. The realistic implementation shape is essentially a no-op: format() with the full .NET format-spec, pad_left, pad_right, and repeat already ship and are registered in the FunctionRegistry, so a from-scratch build would duplicate StringFunctions.Format/PadLeft/PadRight/Repeat. The only architecturally meaningful sliver would be number-aware formatting (thousands separators, locale-explicit decimals) which already falls out of format("{0:N2}", x); there is no obstacle to add a thin number(value, spec) alias, but it buys almost nothing the existing format() doesn't. No prerequisite work is required.

**Effort.** S. Blast radius is essentially zero-to-trivial: any genuinely new sliver (e.g. a number() or thousands() alias) is one registration line in StringFunctions.Register plus one method in StringFunctions.cs, with no StepType, parser-array, FlowCanvasBridge, or ScriptDependencyAnalyzer changes (these are functions, invisible to the parser's command tables). Autocomplete picks up new names automatically via FunctionRegistry.RegisteredNames.

**Rationale (why deferred).** Deferred as a false-premise reject: roughly 90% of the proposal already exists and is documented. The flagship capabilities (format with format-spec, padding, repeat) are present and verified in StringFunctions.cs, so the item is low value not because it fights the architecture but because it asks to rebuild shipped functionality.

**Salvageable sliver.** None worth a slot. If anything, the only defensible micro-fix is making format() surface a FormatException as a warning instead of silently returning the template (StringFunctions.cs:268-271), and optionally adding a number()/thousands() alias over format("{0:N}", x) for discoverability. Neither is load-bearing.

---

### 27. Multi-line send_block / heredoc for device CLI config stanzas

**Area:** Interactive SSH  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 6/10 (M) · risk 5/10 · risk band low · breaking: no

> **Recommendation — ⛔ Skip:** Existing split/foreach idiom covers it and the multi-touchpoint StepType cost is unjustified for verbosity reduction alone.

**Problem.** Configuring a device stanza is one `send` step per line: ScriptSamples/fortigate/block_ip.yaml uses ~10 separate `send:` blocks to push one address-object + group stanza. Each line is its own step (its own prompt cycle, its own output echo, its own node in Flow Canvas), which is verbose and noisy. An operator cannot paste a multi-line config block as a single unit.

**Design.** Proposal wanted a `send_block:`/heredoc that takes a multi-line string and emits each line as a command. Realistic shape: either a documented idiom (`send: { command: "line1\nline2\nend" }` — embedded `\n` is already transmitted raw by SshShellSession since it only appends `\r`), or a `send.lines:` list that loops `ExecuteAsync` per line under one step. The architectural friction the proposal got backwards: the safety property of per-line prompt detection (waiting for each line's prompt before sending the next) was proposed as opt-IN, when it must be the default to avoid overrunning the device input buffer — and SshShellSession's prompt loop is per-command, so a true block send must iterate anyway, giving little saving over `split()`+`foreach`.

**Effort.** M — new StepType or send sub-key would touch ScriptStep.cs (new property + GetStepType + StepType enum), ScriptParser ParseSendStep (line 1178) + key table (line 109), FlowCanvasBridge duplicate table (line 257) + serializer (line 2330) + node rendering, ScriptDependencyAnalyzer Send case, autocomplete, and the React block registry. Heavy for the saving.

**Rationale (why deferred).** Deferred marginal: `split()`+`foreach` over a multi-line string already approximates it, the verbosity claim is inflated (the per-line steps are explicit but trivial), and the proposed opt-in per-line prompt detection is backwards (it is the safety guarantee, not an option). Fights the per-command prompt-cycle model in SshShellSession for narrow ergonomic gain.

**Salvageable sliver.** Document the existing idiom: a single `send` with embedded `\n` is transmitted raw (SshShellSession appends only `\r`), or `foreach line in split(block, '\n')` with a per-line `send`. No code change required.

---

### 28. Handlers: notify-on-change deferred steps that run once at the end

**Area:** Control flow  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 6/10 (L) · risk 5/10 · risk band medium · breaking: no

> **Recommendation — ⛔ Skip:** The differentiating 'on change' value is blocked by missing change-tracking, and the remainder duplicates an existing one-line idiom.

**Problem.** There is no deferred-step mechanism: a step that's 'flagged' during the run and executed once at the end (Ansible handlers). The use case — 'three config edits all request a service restart, restart once at the end' — requires either repeating the restart or hand-rolling a boolean and a final `if:`. The 'notify on change' part can't be expressed at all because there is no change-tracking: no command reports a `changed` flag (verified — only HTTP/webhook expose `_status` siblings; no idempotency model exists).

**Design.** The proposal wanted top-level `handlers:` plus `notify: <handler>` on steps that fires the handler once at end-of-run if any notifier triggered. Realistic shape: a `handlers:` block parsed into the Script model, a `notify`/`flag` option on steps that records a name into a per-execution set, and a flush in `ScriptExecutor.ExecuteAsync`'s `finally` (lines 242-252) that runs flagged handlers once. The architectural obstacle is two-fold: (1) the headline value ('notify ON CHANGE') needs change detection the engine doesn't have, so in practice `notify` reduces to an unconditional flag-set; (2) `notify` already exists as a StepType (NotifyCommand), so the keyword collides and would confuse the parser/model. With change-tracking absent, the feature degenerates to 'set a boolean, run an if at the end' — which `set:` + a final `if:` already expresses.

**Effort.** L. New `handlers:` top-level block in Script model + ScriptParser (a new parse path alongside `subroutines`), a step-level flag option across the common-step parse branches, end-of-run flush wiring in ScriptExecutor, plus FlowCanvasBridge (no node concept for handlers) and dependency-analyzer. Keyword collision with the existing `notify` command forces a rename, widening the surface.

**Rationale (why deferred).** Deferred because the core value ('on change') depends on a non-existent change-tracking model, and the fallback (a deferred boolean) is already trivially expressible with `set:` + a final `if:`. High integration cost for a pattern users can already write.

**Salvageable sliver.** None worth extracting now. If anything, document the `set: flag = true` + final `if: flag` idiom as the supported pattern. Revisit only if/after a real `changed` model lands (Tier 2.1 structured results).

---

### 29. Pipeline operator |> for left-to-right value transforms

**Area:** Expressions  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 6/10 (M) · risk 5/10 · risk band low · breaking: no

> **Recommendation — ⛔ Skip:** Pure syntactic sugar over working nested calls, targets a layer the interpolation router doesn't reach, and yields no debugger benefit — its prerequisites (2.8) are themselves deferred.

**Problem.** Nested transforms must be written inside-out today: to uppercase-then-trim-then-split you write `split(trim(upper(x)), ',')`, which reads right-to-left and grows unreadable as the chain deepens. There is no `|>` token anywhere in the engine (confirmed: ExpressionParser.cs has no pipe handling; ExpressionEvaluator.cs only knows keyword operators; ScriptContext.ResolveVariableExpression only special-cases `(`). So the only way to compose N functions is manual nesting, and the debugger shows the whole nested call as one opaque step with no intermediate values.

**Design.** The proposal wanted `x |> upper |> trim |> split(',')` desugaring left-to-right with the carried value injected as the first argument of the next call. The natural home is ExpressionParser as a new low-precedence binary level (a `ParsePipeline` between ParseExpression and ParseTernary) that, on `|>`, evaluates the left side then rewrites `fn` / `fn(args)` into `fn(left)` / `fn(left, args)` via the existing ResolveFunction path. The architectural obstacle the roadmap flags: the flagship examples live in `{{ }}`/`${ }` interpolation and `set:`, but interpolation resolution (ScriptContext.ResolveVariableExpression at line 647) only routes to the expression engine when it sees a `(`; a bare `x |> upper` has no parens, so it would never reach ExpressionParser — the proposal effectively targeted the string-path, not the parser. Making `|>` work everywhere means teaching the interpolation router and ValueResolver.ResolveExpressionValue to detect the operator too. Prerequisite: argument-splicing only works cleanly once functions go through one dispatch with known arity (Tier 2.8), otherwise `fn(left, args)` reconstruction is fragile against the raw-args-string model.

**Effort.** M — ExpressionParser.cs (new precedence level + first-arg splice into ResolveFunction), plus the routing fix in ScriptContext.ResolveVariableExpression and ValueResolver.ResolveExpressionValue so non-paren pipelines reach the parser; reserve `|>` so it doesn't collide with the no-existing pipe handling. No StepType/parser-array/FlowCanvasBridge changes (it is an expression-level token), but autocomplete/highlighter would want the operator added and ExpressionParserTests needs a new matrix.

**Rationale (why deferred).** Deferred as low-value sugar over capability that already exists (nested function calls work today). It targets the wrong layer — the headline `x |> upper` examples never hit ExpressionParser because the interpolation router gates the engine on a literal `(`. The audience is niche, and because the desugared chain still resolves as a single nested call, it stays opaque to the step-level debugger, removing the one ergonomic benefit (seeing intermediate values) that might justify it.

**Salvageable sliver.** None worth extracting standalone. If readability is the real pain, the cheaper answer already on the roadmap is the collection/aggregate helpers (Tier 3.1) plus the unified function registry (Tier 2.8); a pipeline operator only becomes attractive after 2.8 makes first-arg splicing safe.

---

### 30. Robust, shell-portable remote exit-status capture (decouple from send.fail_on_nonzero, support non-POSIX devices)

**Area:** Interactive SSH  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 6/10 (M) · risk 5/10 · risk band medium · breaking: no

> **Recommendation — ⛔ Skip:** The valuable core is already captured as accepted item 2.1; the remaining cross-device-rc ambition rests on a false premise and fights the merged-PTY/POSIX-only design.

**Problem.** Today the only way to react to a remote exit code is `send.fail_on_nonzero: true`, which (a) wraps the command in a POSIX `eval '...'; $?; printf SENTINEL` shim (SendCommand.BuildCommandWithExitStatusSentinel, line 134) that is meaningless on FortiGate/Cisco/IOS shells, and (b) only lets you abort the step — you cannot capture rc=1 from `grep` and branch on it, because the rc is consumed internally and never written to a variable. The captured value (`step.Capture` / RecordCommandOutput, line 102) is raw stdout only: no exit code, no line count, no failed flag.

**Design.** The proposal wanted a portable exit-status capture decoupled from fail_on_nonzero, with a `shell:` knob for non-POSIX devices. Realistic shape (and what the roadmap's accepted Tier-2 item 2.1 already absorbs): keep the existing flat-sibling convention used by HttpCommand/NotifyCommand and add `send.exit_status: true|auto` that wraps captured POSIX sends with the existing SendCommand.ExitStatusSentinel and exposes `${capture}_rc`/`_failed`/`_success`/`_lines` as separate context variables — NOT the proposal's `${result.rc}` member access, which ScriptContext.ResolveVariableExpression cannot do (it only supports `.length` and `[index]`, lines 614-644). The architectural obstacle for the non-POSIX half: the sentinel scheme is hardwired to POSIX `$?` semantics in SendCommand; there is no per-device rc convention, and SshShellSession streams a single merged PTY (no stderr, no real channel exit code).

**Effort.** M — but the high-value core is already accepted as Tier-2 item 2.1 (Structured command result register). Net-new blast radius for THIS item's non-POSIX ambition: SendCommand.cs (rc extraction is POSIX-only), the `send` key table in ScriptParser.cs (line 109) AND the duplicate table in FlowCanvasBridge.cs (line 257), plus ScriptDependencyAnalyzer Send case. The cross-device rc piece has no clean seam.

**Rationale (why deferred).** Deferred as marginal on a partly-false premise: the fail_on_nonzero+expect 'silent conflict' the proposal cited is actually already a hard error (SendCommand line 41-47 returns EmitFailure 'fail_on_nonzero is not supported with send.expect'), and the example referenced a non-existent `send.into` (the engine uses `capture:`). The genuinely valuable part — exposing rc/lines/failed as variables — was lifted into accepted item 2.1; the portable-across-devices remainder fights the single-merged-PTY, POSIX-only architecture for little real payoff (network CLIs rarely surface a usable rc).

**Salvageable sliver.** Already salvaged: the rc/stdout_lines/failed register is accepted as Tier-2 item 2.1 using flat siblings. Nothing further to extract here — the non-POSIX rc ambition is the part correctly dropped.

---

### 31. Template rendering command with for/if expansion for multi-line config generation

**Area:** Interactive SSH  ·  **Original verdict:** marginal  ·  **Scores:** value 5/10 · effort 8/10 (L) · risk 5/10 · risk band low · breaking: no

> **Recommendation — ⛔ Skip:** High effort for a third templating syntax whose headline use case (record loops) is blocked on member access the engine lacks; existing map/join covers the realistic cases.

**Problem.** Generating a multi-line config from data requires chaining `map`+`join`+`set` (or a `foreach` accumulating into a variable). There is no template command with inline `for`/`if` expansion, so producing, say, an interface stanza per item in a list is awkward and the intermediate string-building is hard to read.

**Design.** Proposal wanted a Jinja-lite template command with for/if directives expanding into multi-line output. Realistic shape: a new `template:` StepType taking a template string with `{% for %}`/`{% if %}` blocks plus the existing `{{ }}` interpolation, rendered into a capture variable. The architectural obstacle is severe: this introduces a THIRD templating delimiter and a whole control-flow mini-grammar parser on top of the two existing interpolation dialects (`${ }`/`{{ }}` unified onto the balanced scanner in ScriptContext.SubstituteVariableTokens, line 717) and the two expression engines. The for/if loop variables would also need dotted member access on records — which ScriptContext does not have (only `.length`/`[index]`). So the marquee 'loop over parsed records' use case can't even render today.

**Effort.** L — a new template grammar/parser (large net-new code), new StepType across ScriptStep/StepType enum/GetStepType, ScriptParser, both FlowCanvasBridge tables + serializer + node, dependency analyzer, autocomplete, plus a prerequisite (member access in loops). Largest of the interactive-ssh set.

**Rationale (why deferred).** Deferred marginal: `map`+`join`+`set` already covers flat-list generation, and the proposal adds a third delimiter and a new grammar for narrow incremental value. It also silently presumes record member access that doesn't exist — the same false premise that defers the TextFSM/structured-parse items (roadmap 3.7). Fights the 'one balanced-brace scanner, two expression engines' direction the roadmap is actively consolidating (item 1.11, 2.5).

**Salvageable sliver.** None as a template engine. The cheap sliver is documenting the `map(list, x => ...)` + `join("\n")` idiom for flat lists, which already works.

---

### 32. Device-type drivers: declare the platform, get pagination/enable/config-mode for free

**Area:** Interactive SSH  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 8/10 (L) · risk 6/10 · risk band medium · breaking: no

> **Recommendation — ⛔ Skip:** The marquee benefits are already provided generically by SshShellSession's pager handling and prompt-change tracking; the remainder is a high-maintenance, untestable profile corpus with a config-mode-stranding hazard.

**Problem.** An operator must manually handle device idioms in-script: disable paging (ScriptSamples/cisco/ios_backup_config.yaml line 9 sends `terminal length 0` by hand), enter enable mode, and manage config-mode entry/exit. There is no `device: cisco_ios` declaration that bundles these behaviors, so each script re-implements them.

**Design.** Proposal wanted declarable platform drivers (cisco_ios, fortios, etc.) that auto-provide pagination handling, enable-mode entry, and config-mode management. Realistic shape: a profile registry mapping device type to prompt patterns, paging-disable commands, and enable/config sequences, consulted by SshShellSession on session init. The obstacle is that much of this is ALREADY handled generically: SshShellSession does automatic mid-output pager handling (the `-- More --` ProcessChunk path sends space, line 778-786) and automatic config-mode prompt tracking (UpdatePromptIfChanged, line 1187, rebuilds the prompt regex when the device enters config mode). So a driver layer would duplicate existing generic behavior; its only net-new value (auto-enable, auto-disable-paging-on-connect) is narrow, and auto-exit-on-error can strand a device IN config mode.

**Effort.** L — six+ device profiles each carrying untestable real-hardware behavior, new declaration syntax (top-level key + Script model), SshShellSession init hooks, and a maintenance burden with no CI device to validate against. High risk for the auto-exit-on-error stranding hazard.

**Rationale (why deferred).** Deferred marginal because the painful parts (auto-paging via the `-- More --` handler, config-mode prompt re-detection via UpdatePromptIfChanged) are already solved generically in SshShellSession. Net new value is narrow (auto-enable, connect-time paging-disable), the six profiles are an untestable maintenance liability, and the auto-config-exit behavior can leave devices half-configured on error — a real safety regression.

**Salvageable sliver.** None as a driver framework. If anything, expose the existing generic auto-paging more visibly and document that `terminal length 0` is optional (the session already drains `-- More --`).

---

### 33. First-class matrix fan-out inside a single script

**Area:** Control flow  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 9/10 (XL) · risk 7/10 · risk band medium · breaking: no

> **Recommendation — ⛔ Skip:** Both differentiating properties (isolation, concurrency) are defeated by the shared session and the (1,1) channel lock; the safe case already works via nested foreach.

**Problem.** There is no matrix construct (run the same block across the cross-product of variable sets). Operators approximate it with nested `foreach`. The pitch was parallel, isolated fan-out — but neither property holds in this engine.

**Design.** The proposal wanted `matrix: { os: [...], version: [...] }` expanding to the cross-product, each combination run in an isolated, concurrent context. Realistic shape would be a new StepType that computes the product and dispatches each cell. Two hard architectural obstacles: (1) Isolation is illusory. `CreateChildScope` (ScriptContext.cs:975-997) forks only the per-context `_variables` dict — it shares the same `_sharedState`, meaning the same `Session`, same output stream, same history label state. So matrix cells targeting the same host would interleave on one shell. (2) Concurrency is a no-op for send-blocks. `SshShellSession` serializes all command execution behind `_commandExecutionLock = new SemaphoreSlim(1, 1)` (SshShellSession.cs:71) — one stateful channel per host. Parallel cells issuing `send` would queue single-file. `ParallelCommand` already demonstrates the hazard: it runs branches via `Task.Run` over one shared mutable context (ParallelCommand.cs:40-61), a self-documented data race. Matrix fan-out would inherit and multiply that race.

**Effort.** XL. New StepType + enum + parser path + cross-product expansion + scoping semantics + FlowCanvasBridge rendering (no matrix node) + dependency-analyzer + validation + autocomplete + React block registry. And it would either re-expose the ParallelCommand data race or require the Tier 2.2 race-free-parallel refactor (isolated per-branch contexts) as a prerequisite first.

**Rationale (why deferred).** Deferred as marginal/XL because the two selling points are false against the architecture: isolation is illusory (shared session via CreateChildScope) and concurrency is a no-op for send-blocks (the (1,1) session lock). Nested `foreach` already covers the safe, sequential case, so the feature adds large surface for a non-working promise.

**Salvageable sliver.** None as a fan-out feature. The only adjacent useful sliver — sequential cross-product iteration — is already achievable with nested `foreach`. If demand is real, document a 'matrix via nested foreach' recipe.

---

### 34. Inline remote command substitution ($sh{...} / $local{...})

**Area:** Interactive SSH  ·  **Original verdict:** reject  ·  **Scores:** value 4/10 · effort 9/10 (L) · risk 9/10 · risk band high · breaking: no

> **Recommendation — ⛔ Skip:** Directly fights the synchronous-interpolation / (1,1)-locked single-channel architecture and re-enters async SSH I/O — correctly rejected.

**Problem.** There is no way to inline a remote command's output into a string, e.g. `send: "echo $sh{hostname}"`. Operators must do `send: { command: hostname, capture: h }` then reference `${h}` on a later line — an extra step per substitution.

**Design.** Proposal wanted `$sh{...}` (remote) / `$local{...}` (local) substitution evaluated inline during interpolation. Realistic shape would hook into ScriptContext.ResolveVariableExpression (line 608), the same place vault inline resolution lives (`{{vault:...}}` bridges async via `.GetAwaiter().GetResult()`, line 707). The fatal architectural obstacle: SubstituteVariables is synchronous and called from ~149 sites (163 total occurrences across 50 files, including SshShellSession itself). A `$sh{}` would have to synchronously block on `session.ExecuteAsync` from inside interpolation — but interpolation runs inside command handlers that may already hold, or are about to acquire, the session's `_commandExecutionLock` SemaphoreSlim(1,1) (SshShellSession line 71). Re-entering ExecuteAsync from within a send's own argument resolution corrupts the single stateful PTY channel and can deadlock the (1,1) lock. Worse, interpolation also runs at design-time / validation with a null session.

**Effort.** L and high-risk — would touch the single hottest path in the engine (SubstituteVariables, 149 call sites) and require making the entire interpolation chain async or introducing sync-over-async into a lock-guarded stateful channel. Effectively unbounded blast radius.

**Rationale (why deferred).** Rejected (not merely marginal): it hooks synchronous interpolation into async, stateful, lock-guarded SSH I/O; re-enters and corrupts the single channel; and runs at design-time with a null session. This is a genuine architecture conflict, not low value — the (1,1) lock and synchronous SubstituteVariables make it unsafe by construction.

**Salvageable sliver.** None. The existing capture-then-reference pattern (`send` with `capture:` + later `${var}`) is the correct, safe expression of this; `$local{}` overlaps the existing `localcmd` command with `into:`.

---

### 35. Built-in test harness: test blocks with mocked send/http output and a test runner

**Area:** DevEx  ·  **Original verdict:** marginal  ·  **Scores:** value 4/10 · effort 9/10 (XL) · risk 5/10 · risk band medium · breaking: no

> **Recommendation — ⛔ Skip:** An in-YAML harness is XL, blocked on a deferred mock session, and mis-fits a GUI-only app; the existing xUnit + FakeSendSession pattern already covers the genuine need.

**Problem.** There is no in-language way to assert that a script behaves correctly given stubbed remote output. Authors validate scripts by running them against live hosts or by writing C# xUnit tests externally. For a script that branches on send output, regressions in logic go uncaught until they hit a real device.

**Design.** The proposal wanted YAML 'test' blocks declaring mocked send/http responses plus a test runner (implicitly CLI-driven). Realistic shape would layer on a dry-run mock session (item above) and add a parser StepType for test blocks, an assertion collector, and a runner UI. Two pillars don't survive contact with the code: (1) there is no mock-session infrastructure to build on — ScriptContext.Session is the sealed SshShellSession and only SendCommand exposes an injectable ISendCommandSession; (2) SSH_Helper is WinExe/UseWindowsForms with no CLI entry point, so a 'test runner' has nowhere to live except a new WinForms surface. The harness would also need every session-bound command mockable, which the dry-run item shows is not the case today.

**Effort.** XL. New StepType + parser block + ValidateSteps recursion + autocomplete catalog entries + FlowCanvasBridge serialization, a mock-session abstraction across all session commands, an assertion ledger, and a results UI — all gated on the (unbuilt) dry-run mock session as a prerequisite.

**Rationale (why deferred).** Deferred because it is XL, depends on a prerequisite that itself was deferred (mock session), and one of its pillars (a CLI runner) does not fit a GUI-only WinForms app. The reviewer's 'lean on existing xUnit + fake-session pattern' is correct: the codebase already tests script logic via FakeSendSession injected through ReplaceCommand reflection in ScriptExecutorControlFlowTests, which covers the real need without a new in-language subsystem.

**Salvageable sliver.** Document and lightly formalize the existing test pattern: SendCommand.ISendCommandSession + FakeSendSession + the executor's _commands replacement seam already let maintainers (and could let power users via a sample test project) drive scripts with mocked output. Optionally expose a small public helper to construct a ScriptExecutor with an injected fake send resolver so script regression tests are first-class.

---

### 36. Portable imports: relative paths, library search path, transitive loading, cycle detection, and version pinning

**Area:** Modularity  ·  **Original verdict:** marginal  ·  **Scores:** value 3/10 · effort 7/10 (M) · risk 6/10 · risk band medium · breaking: no

> **Recommendation — ⛔ Skip:** The portability core is defeated by scripts-as-strings with no file anchor, and the only clean sliver (import cycle detection) is moot until transitive imports — which themselves lack demand — are first enabled.

**Problem.** An `imports:` system already ships, but it is rigidly local: `ScriptSubroutineRegistryBuilder.LoadImports` requires every import `path` to be absolute (`Path.IsPathRooted` check, errors otherwise), so a script and its library cannot move together — relocate the folder and every import breaks. Libraries also cannot import other libraries: `ScriptParser.ValidateLibraryTopLevel` forbids the `imports` key inside any `library: true` file (line 4252), so there is no transitive loading at all and no way to compose libraries. There is no search path, no version pinning, and the existing `ValidateCallCycles` only walks local subroutine call cycles — it never sees an import graph because that graph is structurally capped at depth 1.

**Design.** The proposal wanted Ansible-style portable imports: paths relative to the importing file, a configurable library search path, transitive (recursive) loading, import-graph cycle detection, and version pinning. A realistic implementation would: thread an originating-file path into `ScriptParser`/`ScriptSubroutineRegistryBuilder` so relative resolution has an anchor; lift the `imports`-forbidden-in-library restriction and make `LoadImports` recurse, merging nested `ScriptImportedLibrary` definitions into `ScriptSubroutineRegistry`; and add an import-graph DFS mirroring `DetectCycles`. The blocking obstacle is that scripts are strings, not files — `SshExecutionService.ExecuteScriptAsync(... string scriptText ...)` and the preset store hand `ScriptParser.Parse` raw text with no path of origin, so 'relative to the importing file' has literally no anchor to be relative to. Prerequisite: a first-class notion of a script's on-disk identity, which presets (stored in config.json) deliberately do not have.

**Effort.** M. Blast radius: `ScriptSubroutineRegistryBuilder.LoadImports`/`CreateDefinitions` (recursion + relative resolution), `ScriptSubroutineRegistry`/`ScriptImportedLibrary` (nested-library merge + import-graph storage), `ScriptParser.ValidateLibraryTopLevel` (drop the imports ban) and `ParseImports` (new keys for version/search-path), an import-cycle DFS alongside `DetectCycles`, and a new origin-path parameter plumbed through `ScriptParser.Parse` and its callers in `SshExecutionService` and the preset/job execution paths. The autocomplete/dependency-analyzer touch is small (imports add subroutine names, already handled via the registry).

**Rationale (why deferred).** Deferred because the flagship value (portability) rests on a false premise against this architecture: presets are strings in config.json with no file identity, so relative paths cannot resolve. Search paths just relocate the same portability problem. Transitive loading and import-graph cycle detection are solutions to a problem that cannot exist yet (libraries cannot import libraries), and the roadmap notes ~zero demonstrated demand. This is 'fights the architecture' (no file anchor) plus 'low value' (single-level absolute imports already cover the real reuse case).

**Salvageable sliver.** Import-graph cycle detection, but only as a guard that lands *with* transitive loading — extend the existing `DetectCycles` DFS in ScriptSubroutineRegistryBuilder to walk imported-library edges so a future depth-N import chain can't infinite-loop the parser. On its own today it has no graph to protect (depth capped at 1), so it is only worth building as part of enabling transitive imports.

---

### 37. YAML anchors/aliases/merge-keys plus a defaults:/use: option-fragment template

**Area:** Syntax  ·  **Original verdict:** reject  ·  **Scores:** value 3/10 · effort 8/10 (L) · risk 7/10 · risk band medium · breaking: no

> **Recommendation — ⛔ Skip:** Even the MergingParser salvage is undermined by FlowCanvasBridge silently flattening anchors on round-trip, and existing subroutines/imports already cover reuse, so the whole feature is low-value against the canvas constraint.

**Problem.** Operators cannot DRY up repeated option blocks across steps — there is no way to define a `defaults:` fragment and reuse it, and standard YAML anchors/aliases (`&name`/`*name`) and merge keys (`<<:`) are not honored by the engine in a way that survives. The hand-rolled event parser (`new Parser(reader)` at ScriptParser.cs:477) walks Scalar/MappingStart/SequenceStart events directly and has no anchor/alias resolution, so referencing `*name` does not expand. Even if it parsed, a Flow Canvas round-trip would erase it: FlowCanvasBridge.ExportGraphToYaml (FlowCanvasBridge.cs:1021, serializing via `new SerializerBuilder().Build()` at lines 3986/4523) emits a fresh object graph, so anchors/aliases/merge structure is gone after any canvas import/export.

**Design.** The proposal wanted full YAML anchors/aliases/merge-keys plus a bespoke `defaults:`/`use:` template mechanism. Realistic shape: wrap the event stream in YamlDotNet's `MergingParser` so `<<:` merge keys resolve before the hand-rolled walk consumes events, and rely on YamlDotNet's native anchor/alias support at the event level. The blocking obstacles: (1) the proposal was written against the dead `_deserializer` field (ScriptParser.cs:402, never used) — the live path is the manual `Parser`, so 'just turn on the deserializer' does not apply; (2) anchors that survive parsing are still destroyed on Flow Canvas re-serialization (confirmed: the bridge serializes a graph model, not the source token stream), so the feature is silently lossy for any script that touches the canvas; (3) the `defaults:`/`use:` template layer is net-new grammar with its own StepType/validation/autocomplete/bridge surface on top.

**Effort.** L. Anchors/aliases/merge: wrap the parser at ScriptParser.cs:477 with MergingParser (small) but verify every downstream Consume<> still works, plus accept the canvas-strips-anchors data loss. The `defaults:`/`use:` template arm is the heavy part: new top-level key parsing, a fragment-merge step into option maps, ValidateSteps coverage, autocomplete, and FlowCanvasBridge round-trip — none of which the bridge's flat graph model represents.

**Rationale (why deferred).** Rejected, and correctly: it is built on a dead `_deserializer` field, the live parser is a hand-rolled event reader with no anchor logic, and Flow Canvas re-serialization silently strips anchors — so the headline reuse feature would vanish the moment a user opens the canvas. The engine already offers reuse via subroutines/imports, undercutting the value. This fights the architecture, not merely low-value.

**Salvageable sliver.** Merge-key support only, via wrapping the event stream in YamlDotNet's MergingParser at the ScriptParser.cs:477 parse site, so `<<:` resolves before the manual walk. Skip the bespoke `defaults:`/`use:` template and accept that anything routed through the Flow Canvas will lose the merge structure (document it).

---

### 38. Single machine-readable DSL schema replacing ~38 hand-written option switches and duplicated editor catalogs

**Area:** DevEx  ·  **Original verdict:** marginal  ·  **Scores:** value 3/10 · effort 9/10 (XL) · risk 8/10 · risk band high · breaking: no

> **Recommendation — ⛔ Skip:** XL/high-risk internal refactor with near-zero user value because the schema is already substantially centralized and consistency-tested; only the tiny dedupe sliver is worth doing and it rides the already-accepted did-you-mean work.

**Problem.** The grammar is defined imperatively across ~38 per-command parse methods in ScriptParser.cs, each with its own key switch and AddUnknownKeyWarning("Unknown <cmd> key") site (98 switch/default constructs total in the file). Adding or renaming an option means editing the parse switch, the per-command option-key table (CommandOptionKeys / StepRootOptionKeysByCommand / EnumLikeOptionValues), and several hand-kept editor dictionaries. The proposal's premise that editors hold a duplicated catalog is only half true: ScriptAutocompleteProvider already derives commands/keys/enum-values from ScriptParser.Get* accessors (ctor lines 246-300), so the schema source is mostly singular already.

**Design.** The proposal wanted one declarative schema (e.g. a resource document) describing every command, its options, types, and enum values, with parser, validator, autocomplete, FlowCanvasBridge, and dependency-analyzer all reading from it. Realistic shape: a static descriptor table feeding a generic option-loop that replaces the 38 hand-written switches. The architectural obstacle is that the per-command parse methods aren't pure key/value loops — each does bespoke shorthand handling (send scalar vs map, exit 'success <msg>' rewrite, foreach 'item in items', if/then nesting), type coercion, and the (1,1)-style nested-block recursion that ValidateSteps does per-case. A generic schema cannot express that without becoming a second imperative language. Genuinely hand-maintained duplication is narrow: CommandDescriptions, RequiredOptionKeysByCommand, BuiltInSymbols in ScriptAutocompleteProvider.cs.

**Effort.** XL. Blast radius: rewrite of ~38 parse methods in the 5,401-line ScriptParser.cs, the six exposed schema accessors, every CommandOptionKeys/EnumLikeOptionValues table, plus re-pointing ScriptDependencyAnalyzer.cs (54K LOC of per-command logic) and the 4,968-line FlowCanvasBridge TrySerializeStepYaml path. Risk of regressing the CanonicalCommandMapSyntaxTests and QaPresetCatalog/QaPresetsSyntax consistency tests that currently pin behavior.

**Rationale (why deferred).** Deferred because it fights the architecture and rests on a partly-false premise. The reviewer correctly noted editors already read from the parser and DriftGuard-style tests (CanonicalCommandMapSyntaxTests, QaPreset* suites) already enforce consistency, so the user-visible payoff of unification is near zero while the refactor is XL and high-risk. The bespoke shorthand/recursion per command is the real reason a single schema can't subsume the switches.

**Salvageable sliver.** Collapse only the genuinely duplicated hand-maintained editor dicts: derive CommandDescriptions/RequiredOptionKeysByCommand from parser-exposed metadata (or move them next to the parser tables) so descriptions and required-key tags stop drifting. Also extract the repeated AddUnknownKeyWarning("Unknown <cmd> key") strings into one helper keyed off the existing option tables, which directly enables the did-you-mean work already accepted in 1.3/1.6.

---
