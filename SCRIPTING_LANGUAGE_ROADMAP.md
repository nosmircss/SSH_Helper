# SSH_Helper YAML Scripting Language — Enhancement Roadmap

## Executive Summary

This roadmap prioritizes 66 reviewed enhancements to the SSH_Helper YAML scripting language, weighting each by the adversarial reviewer's verdict, value-to-effort ratio, and architectural risk. A recurring theme dominates: the engine's **flat, string-only variable model** (no dotted member access, two parallel expression evaluators, `List<string>` as the canonical collection type) silently invalidates the headline syntax of many ambitious proposals — so several "marginal" items were demoted because their flagship examples do not actually work against the code.

The highest-leverage, lowest-risk wins are **safety and ergonomics fixes that ride existing seams**: universal `when:` guards, did-you-mean diagnostics, unknown-key strictness, loop scoping, and crypto/networking helper functions. The largest justified investments are **correctness hardening** (race-free parallel execution, unified expression engine, structured command results, guaranteed teardown on cancel) and **multi-branch expect** — all genuine gaps for an SSH automation tool, but each requiring careful phasing because reviewers repeatedly found effort and breaking-change surface underestimated.

Roughly a third of proposals are deferred outright: either the capability already ships, the design rests on non-existent infrastructure, or the change fights the single-shared-shell execution model.

---

## Tier 1 — Quick Wins

*High value, low/medium effort, low risk. Additive changes that ride existing seams and close real footguns. Ranked by value-to-effort ratio.*

### 1.1 Networking helper functions (IP/CIDR/URL)

**Problem:** This is an SSH/network automation product, yet there are zero networking helpers callable in expressions. No `is_valid_ip`, `ip_in_cidr`, `parse_url`, or host/port splitting — operators hand-roll brittle regex to gate on "is this host in 10.0.0.0/8".

**Design:** Add a `NetworkFunctions` `IFunctionCategory` (mirroring `EncodingFunctions`): `is_valid_ip(s)`, `ip_version(s)`, `ip_in_cidr(ip, cidr)` using .NET 8 `IPNetwork.TryParse` (not hand-rolled mask math), plus optional `parse_url`/`url_host`/`url_port`. Pure, deterministic, no I/O. No new dependency — `IPAddress`/`Uri` already used in the codebase.

```yaml
- if: "{{ ip_in_cidr(Host_IP, '10.0.0.0/8') }}"
  then:
    - send: "hostname"
- assert: "{{ is_valid_ip(Host_IP) }}"
  message: "{{ Host_IP }} is not a valid address"
```

**Effort:** S–M · **Risk:** Low · **Breaking:** No
**Rationale:** Excellent architectural fit, near-zero risk, pairs directly with `Host_IP` and host-grid columns — the clearest domain-fit win in the set. Lead with IP/CIDR; treat URL helpers as optional.

### 1.2 Universal step-level `when:` guard on every command

**Problem:** Only `foreach` and the standalone `if` step support `when:`. Guarding a single `send` requires wrapping it in `if: ... then: [ ... ]`, adding two indent levels per conditional step — and almost every step in real SSH automation is conditional.

**Design:** Promote `when:` to a common step option. In `ExecuteStepCoreAsync`, before dispatch, evaluate `step.When`; if false, mark the step skipped (reuse the existing `StepCompleted.Skipped` flag) and return `Ok()`. Scope the universal guard to non-foreach steps (foreach already overloads `When` as a per-item filter). The `When` property, parser path, and per-step evaluator pattern already exist.

```yaml
- send: { command: systemctl is-active nginx, into: nginx_state, on_error: continue }
- send: { command: systemctl restart nginx }
  when: nginx_state != "active"
```

**Effort:** M · **Risk:** Medium · **Breaking:** No
**Rationale:** Highest-leverage idiomatic ergonomics win with the plumbing already half-built. Phase 1 = executor guard + `ScriptDependencyAnalyzer` update for all step types. Phase 2 (gate explicitly) = Flow Canvas round-trip parity to avoid silent guard-loss. Do **not** take the breaking "rename to `where:`" arm.

### 1.3 Did-you-mean suggestions for misspelled keys, commands, and enum values

**Problem:** Error messages are bare (`Unknown http key 'tieout'`) and a misspelled command silently becomes `StepType.Unknown` — despite the parser already owning complete tables of valid keys.

**Design:** Add a static `SuggestClosest(input, candidates)` helper using bounded Levenshtein, plus an `AddUnknownKeyWarning(command, rawKey, line)` overload that looks up the existing key tables. Append `Did you mean 'X'?` as a **suffix** (the offending token must stay first for squiggle positioning). Suppress suggestions for short/ambiguous keys (`mode`/`host`/`port`).

```text
- snd: show version   # ERROR Line 1: Unknown command 'snd'. Did you mean 'send'?
```

**Effort:** S · **Risk:** Low · **Breaking:** No
**Rationale:** Cheap message-quality win that converts guess-and-retry into a single edit. **Defer the enum-value half** (`Invalid http method 'GHET'`) — it has no failure site today and requires building enum-during-parse validation first.

### 1.4 Robust, timezone-aware date/time functions

**Problem:** `now()`/`epoch_to_date` use local time while `epoch()` uses UTC — silently mixing time bases (a latent correctness bug). `date_add`/`date_diff` support only s/m/h/d; parsing relies on a hardcoded format list.

**Design:** Extend `DateTimeFunctions` in place: add `now_utc()`/`now_local()` (or a `utc` flag) so the time base is explicit; extend the unit switch to `w/week`, `mo/month`, `y/year` via `AddMonths`/`AddYears` with integer amounts; add an explicit-format parameter for parsing. **Drop** the speculative IANA/Windows timezone-id feature.

```yaml
- set: cutoff
  value: "{{ date_add(now_utc(), -3, 'months') }}"
```

**Effort:** S · **Risk:** Low · **Breaking:** No
**Rationale:** Fixes a real cross-host scheduling bug at small cost; one file plus tests. Keep existing exact-format tests green.

### 1.5 `repeat`/`until` loop (do-while)

**Problem:** `while` always tests before the first iteration. The common "run the command, then poll until healthy" pattern requires duplicating the body or a manual `while: true` + break.

**Design:** A `RepeatCommand` near-cloning `WhileCommand` with the condition check at the bottom; reuse the shared `Do`/`MaxIterations` properties and increment `LoopDepth` so break/continue work. Implement only `repeat:` + `until:` (drop the `while:` alias).

```yaml
- send: "systemctl start app"
- repeat:
    until: "{{state}} == active"
    max_iterations: 30
  do:
    - send: { command: "systemctl is-active app", into: state }
    - wait: 2
```

**Effort:** S–M · **Risk:** Low · **Breaking:** No
**Rationale:** Fills a genuine ergonomic gap, purely additive. Treat the ~12-file integration tax (StepType, six parser arrays, validation, autocomplete, FlowCanvasBridge, React registry) as a mandatory checklist; require a YAML→canvas→YAML round-trip test and a break-inside-repeat test.

### 1.6 Promote unknown-key warnings to errors via a parse-strictness mode

**Problem:** A typo like `tieout: 30` parses cleanly, the option is silently dropped, and the step runs with defaults against real hosts. Unknown keys go to a side `_warnings` list every execution caller ignores. **The single most dangerous gap in the language.**

**Design:** Give unknown-key findings a severity (typo → error, deprecation → warning) instead of one flat list, and have `Validate()` append error-severity findings to its returned `errors` collection (which all execution callers already honor). Keep the three `interactive.*` deprecation notices as warnings. Skip the heavier tri-state enum and new top-level `parse:` key.

```yaml
- send:
    command: show version
    tieout: 5   # ERROR Line 3: Unknown 'send' option 'tieout'. Did you mean 'timeout'?
```

**Effort:** S–M · **Risk:** Medium · **Breaking:** Yes
**Rationale:** High-value safety fix the architecture already supports. Mitigate breakage with a one-release transition logging would-be errors loudly before enforcing; update affected parser tests. Reject the over-engineered tri-state delivery.

### 1.7 Regex match/extract functions and named-capture extract

**Problem:** There is no inline regex match/capture — you cannot pull `eth0` out of `inet 10.0.0.5` inside an `if`/`set`. `extract` is positional-only and silently drops named groups.

**Design:** Add `regex_match(source, pattern[, group])`, `regex_match_all`, `regex_groups` to `StringFunctions` (mechanical clones of `RegexReplace`, reusing the 5s timeout + delimiter stripping). Teach `ExtractCommand` to detect named groups via `regex.GetGroupNames()`, gated strictly so positional behavior is byte-for-byte preserved when no named groups exist. **Defer/drop** `parse_json`/`to_json` (mostly aliases of existing `GetJsonNode`/`str()`).

```yaml
- set: ip
  value: "{{ regex_match(ifconfig_output, 'inet (\\d+\\.\\d+\\.\\d+\\.\\d+)', 1) }}"
```

**Effort:** S–M · **Risk:** Low · **Breaking:** No
**Rationale:** The regex-match family is the one genuinely missing capability here; split it from the redundant JSON aliases.

### 1.8 Real loop scoping with per-loop metadata and dict iteration

**Problem:** `foreach` writes the iterator into the shared context and never removes it, silently clobbering a same-named global variable; `while` has no private scope. Loop metadata is thin (`<item>_index` only) and `foreach` over an object yields nothing.

**Design:** Save-and-restore block scope for `foreach`/`while` using existing `HasVariable`/`GetVariable`/`SetVariable`/`RemoveVariable` primitives (restore in a `finally` to survive break/return). Ship the canonical `<item>_loop` object with `index`/`number`/`first`/`last`/`count` plus `<item>_index`/`_iteration` aliases. Add two-variable dict iteration (`foreach: k, v in {{map}}`). **Cut** stride.

```yaml
- foreach: host in {{hosts}}
  do:
    - if: "{{host_loop.first}}"
      then: [ { print: "=== {{host_loop.count}} hosts ===" } ]
```

**Effort:** M · **Risk:** Medium · **Breaking:** Yes (low-impact)
**Rationale:** Genuine correctness bug consistent with the engine's own subroutine isolation. Budget for the dotted-property resolver work in `ScriptContext` (currently only `.length`/`[index]`) — the hidden cost — and update `ScriptDependencyAnalyzer`, the parser whitelist, and autocomplete.

### 1.9 Soft-assert aggregation with a test summary

**Problem:** `assert severity:warning` keeps going with no recorded failure count; `severity:error` aborts on the first failure. Health-check/post-deploy scripts cannot run all checks and report "3 of 10 failed".

**Design:** Add `severity: soft` that records into a lock-guarded ledger in `SharedScriptExecutionState` and continues. Increment counters for **all** severities and expose `_assert_total`/`_passed`/`_failed` via the existing reserved-variable special-casing. Prefer a dedicated `assert_summary` step with `fail_if_any` over overloading condition-less `assert`.

```yaml
- assert: { condition: "{{cpu}} < 80", message: "cpu under 80", severity: soft }
- assert: { condition: "{{disk}} < 90", message: "disk under 90", severity: soft }
- assert_summary: { fail_if_any: true }
```

**Effort:** M · **Risk:** Low · **Breaking:** No
**Rationale:** Fits SSH_Helper's trajectory as a device-verification tool; the ledger + counters are the high-value core hard to replicate in-script. Budget for the real 5–6 file blast radius and a parallel-block thread-safety test.

### 1.10 Parse-time grammar validation for `set` / `foreach` / `exit` shorthand

**Problem:** `foreach item of items` (wrong keyword) and `exit oops something broke` (silently rewritten to `success <message>`) parse cleanly and fail late or do the wrong thing silently.

**Design:** Add per-StepType validators to the existing `ValidateSteps` switch: a `foreach` regex check with a "did you mean `in`" hint; an `exit` validator that warns (errors under the existing `enforceCanonicalSyntax` flag) when the implicit-success fallback fires; tighten the existing `set` `=` check to require non-empty identifier lvalue and rvalue. **Drop** the invented foreach index/dict-form validation.

```text
- foreach: item of hosts  # ERROR: foreach expects '<var> in <collection>'. Did you mean 'in'?
- exit: disk almost full  # WARNING: bare 'exit' message defaulted to status 'success'.
```

**Effort:** M · **Risk:** Low · **Breaking:** No
**Rationale:** Additive validation on a covered code path; reuse `enforceCanonicalSyntax` rather than a new strict mode. Run the QA preset catalog tests to confirm no sample trips the new `exit` warning.

### 1.11 Unify the two interpolation dialects on one balanced-brace scanner

**Problem:** `${...}` uses a depth-counting balanced scanner; `{{...}}` just finds the first `}}`, so content literally containing `}}` or truly nested `{{...}}` truncates silently. Both already call the same `ResolveVariableExpression`.

**Design:** Replace the `IndexOf("}}")` logic with a balanced `{{`/`}}` depth scanner symmetric to `TryExtractDollarExpression`. Document `${...}` as canonical, keep `{{...}}` as a permanent alias and `{{vault:...}}` as the inline-secret form. **Drop** the linter hint and the mass doc rewrite.

**Effort:** S · **Risk:** Low · **Breaking:** No
**Rationale:** Eliminates the last divergence in the resolution path at small cost. Add regression tests for literal `}}`, nested `{{}}`, adjacent/unbalanced braces, and `{{vault:...}}`. Reframe as hygiene — the flagship "common inline function breaks" example does not actually break today.

---

## Tier 2 — High-Impact Investments

*High value but larger, phased efforts. Correctness hardening and capability gaps genuine for SSH automation. Each requires explicit phasing because reviewers found effort/breaking surface underestimated.*

### 2.1 Structured command result register: rc, stdout_lines, failed

**Problem:** `send.capture` stores only raw stdout. There is no exit code, no line-split view, no failed flag — the remote exit status is computed but only usable to abort the step. For heterogeneous SSH (grep returning 1 for "no match"), this forces brittle regex scraping. **The biggest fidelity gap for SSH automation.**

**Design:** Use the codebase's **existing flat-sibling convention** (as `HttpCommand`/`NotifyCommand` already do) — **not** the proposed `${result.rc}` member access, which does not exist. Add `send.exit_status: true|auto` (default `auto`) that always wraps captured POSIX sends with the existing sentinel and exposes `${capture}_rc`, `${capture}_failed`, `${capture}_success` decoupled from `fail_on_nonzero`; expose `${capture}_lines` as a `List<string>`. **Drop** `stderr` (single merged PTY stream) and `changed` (no idempotency model).

```yaml
- send: { command: grep -c ERROR /var/log/app.log, into: errs, exit_status: auto }
- log: "exit=${errs_rc} lines=${errs_lines.length}"
```

**Effort:** M · **Risk:** Low–Medium · **Breaking:** No
**Rationale:** Highest-value Tier 2 item — closes the exit-code/line-count gap that drives the most common automation decisions. Correcting the mechanism (flat siblings, not dotted access) drops effort from L to M. Foundation for several other ideas.

### 2.2 Race-free parallel: isolated per-branch contexts, fail-fast/wait-all, collected results

**Problem:** `ParallelCommand` runs every branch via `Task.Run` sharing one mutable `ScriptContext` and `_variables` dictionary — concurrent `set`/capture/`_last_error` writes are an unguarded data race. A self-documented hazard.

**Design (phased):** **(1)** Give each branch `CreateChildScope` and stop sharing the mutable dict, with explicit `export:`-based merge-back mirroring subroutine outputs (high value, low risk — subroutines already prove the pattern). **(2)** Add `mode: fail_fast | wait_all`, resolving shell-state consistency after sibling cancellation against the `(1,1)`-locked session. **(3)** Structured `collect:` results, after confirming the lambda/function path supports member traversal. Soften the `break`/`continue` hard parse-error to a warning.

```yaml
parallel:
  mode: wait_all
  collect: results
  steps:
    - { ping: { host: a, into: r }, export: [ r ] }
    - { ping: { host: b, into: r }, export: [ r ] }
```

**Effort:** L · **Risk:** High · **Breaking:** Yes
**Rationale:** Fixes a known correctness hazard; architecture fits perfectly (subroutines are the template). Split into three increments; the default merge-back change is the highest-surprise breakage and needs a migration note.

### 2.3 Multi-branch expect with per-pattern actions, loop-until, and timeout case

**Problem:** `send.expect` accepts a single pattern; `send.respond` is a fixed linear sequence. Real login/upgrade/reload flows are non-linear — "handle a password prompt OR an error OR the shell prompt, whichever appears" — and SSH_Helper cannot express them.

**Design:** Extend `send` (and add a standalone `expect:` step) to accept a `cases:` list, each with `pattern`, optional `reply`/`then:` block, `repeat: true`, and a reserved `timeout:` case. Build a genuine **ordered N-regex session matcher with named captures** — do **not** assume the existing single-regex polling loop is reusable. Make `send` an executor-backed container like `switch`; decide Flow Canvas rendering up front. Keep `respond:` as sugar.

```yaml
- send:
    command: copy running-config startup-config
    expect:
      cases:
        - { pattern: '/\[confirm\]/', reply: "y", repeat: true }
        - { pattern: '/%Error|Invalid/', then: [ { exit: { status: failure, message: "Save rejected: {{matched}}" } } ] }
        - { pattern: prompt, then: [ { print: "Config saved" } ] }
        - { timeout: 30, then: [ { exit: { status: failure, message: "Save timed out" } } ] }
```

**Effort:** M–L · **Risk:** Medium · **Breaking:** No
**Rationale:** The gap is real and core to interactive automation. Reject the "reuse the loop / M" framing — the pager handling is hardcoded, not an N-pattern matcher. Test repeat termination, timeout firing, captures, and control-flow exit.

### 2.4 Cleanup/trap-style guaranteed teardown that survives cancel and abort

**Problem:** Cancellation throws `OperationCanceledException` past `TryCommand`, so YAML `finally` blocks do **not** run when the operator clicks Stop — a script that stages a device into a half-configured state and is then cancelled leaves every host half-configured with no rollback.

**Design (scoped v1):** A top-level `cleanup:`/`on_exit:` step list invoked from the existing C# `finally` in `ScriptExecutor.ExecuteAsync`; read-only `${exit_reason}` from the existing `ScriptExitStatus`; a hard grace timeout with prompt re-sync before teardown sends; skip device-targeting cleanup when the session is already dead. Market it as "guaranteed teardown on operator **Stop**" (timeouts already let `finally` run). **Drop** subroutine-scoped cleanup; validate the connection-pool interaction.

```yaml
cleanup:
  - if:
      condition: exit_reason != "success"
      then:
        - send: { command: "config firewall policy\nabort\nend" }
        - log: { message: "Rolled back staged policy on ${Host_IP}", level: warning }
steps:
  - send: { command: "config firewall policy\nedit 0\nset name STAGED" }
```

**Effort:** L · **Risk:** Medium · **Breaking:** No
**Rationale:** Genuinely dangerous gap for network-device automation; `try/finally` provably does not cover the cancel path. Require tests for cleanup-on-Stop with live session, graceful skip on dead session, and grace-timeout enforcement.

### 2.5 Unify the two expression engines into one grammar

**Problem:** Two disjoint engines: `ExpressionEvaluator` (if/while/assert/foreach, keyword operators, no arithmetic) and `ExpressionParser` (set/lambdas, arithmetic/ternary/??, no logical/string operators). The same `==` gives different results depending on which runs; `x => x > 5 and x < 10` silently fails inside a lambda. **The root cause of nearly every other expression limitation.**

**Design (test-first, multi-phase):** **(1)** Extract one shared truthiness/equality function used by both engines (low risk, immediate win). **(2)** Write the parity test matrix as a contract capturing every existing behavior (case-insensitive `in`, JSON-array membership, structural `is empty`) and a single truthiness table. **(3)** Incrementally add keyword operators (`and`/`or`/`not`/`contains`/`in`/`matches`/`is empty`/`is defined`) to `ExpressionParser` behind the parity tests, reconciling the **third** resolution path (`ScriptContext.SubstituteVariables`/`ValueResolver`) the proposal ignores; reserve operator words. Collapse `ExpressionEvaluator` to a wrapper only once parity is proven.

```yaml
- if: "count(filter(open_ports, p => int(p) > 1024)) > 0 and name contains 'prod'"
  then:
    - print: "matched"
```

**Effort:** L–XL · **Risk:** High · **Breaking:** Yes
**Rationale:** Architecturally correct end state with modest but real user payoff. Do **not** greenlight as "promote, one-line wrapper, purely additive" — it silently alters truthiness/equality at every condition site. Treat as a deliberate, test-first correctness refactor.

### 2.6 Single shared truthiness and predictable equality semantics

**Problem:** `if 0` and `0 ? a : b` disagree: one engine treats string `'0'` as false, the other as true, so `filter(nums, x => x)` keeps `'0'` items. Equality applies a `0.0001` tolerance (so `'1.00001' == '1'`) and is always case-insensitive (dangerous for Linux paths, tokens).

**Design (split):** **Phase 1 (do now):** Create `ScriptTruthiness.IsTruthy` (stricter rule: `null`/empty/`'false'`/`'0'`/zero/empty-collection → false) and have both engines delegate to it; unify the two `==` implementations onto one comparison helper; ship `approx(a,b,epsilon)`. **Phase 2 (separate proposal):** the case-sensitivity question — prefer **adding** an explicit case-sensitive operator over flipping the default and breaking the install base.

```yaml
- if: "0"                          # false (consistent everywhere)
- if: approx(price, 9.99, 0.01)    # explicit tolerance
```

**Effort:** M (Phase 1) · **Risk:** Medium · **Breaking:** Yes
**Rationale:** Phase 1 is a genuine cross-evaluator consistency bug producing wrong automation results — clear yes. The equality-default flip is real but must not ride along as one bundled high-risk change.

### 2.7 Harden switch: multi-value cases and surfaced regex errors

**Problem:** `switch` matches one resolved string per case and silently swallows regex compile errors to "no match" — a malformed pattern looks identical to a legitimate non-match. No list-of-values case.

**Design (split):** **Piece 1:** Add the existing `UserPatternTimeout` to the switch regex call (one-line latent-ReDoS fix); make regex compile failures route through `CommandResult.ApplyOnError` uniformly in **both** `SwitchCommand` and `ExpressionEvaluator`; add multi-value list cases (match-if-any-equal), touching the model and ~6 read sites. **Piece 2 (defer):** numeric ranges (`in 200..299`), explicit `match:` modes, `case_sensitive` — a new in-value mini-DSL for marginal benefit.

```yaml
switch:
  value: "{{code}}"
  cases:
    - value: [ 301, 302, 307, 308 ]
      do: [ { print: redirect } ]
```

**Effort:** M+ · **Risk:** Low · **Breaking:** No
**Rationale:** The regex-error surfacing + timeout fix + list cases are valuable and architecture-aligned; the full PowerShell-parity package is over-scoped.

### 2.8 Unify all functions into the FunctionRegistry with descriptors

**Problem:** Three dispatch layers with no single source of truth. ~17 core list/string functions live only in the `JsonUtilities` switch, invisible to `RegisteredNames`/autocomplete/the dependency analyzer; functions get a raw unsplit args string with no central arity checking, failing silently on typos.

**Design (split):** **Part 1 (non-breaking, do first):** Lift the ~17 bare functions into `StringFunctions` + a new `ListFunctions` category, reduce `TryEvaluateFunctionExpression` to a thin shim — makes `RegisteredNames` complete and unblocks function autocomplete, with no behavior change if semantics are mirrored exactly against the OpenSpec scenarios and ~56 tests. **Part 2 (separate, breaking):** loud `ScriptFunctionException` for unknown functions + did-you-mean. **Drop** central *type* checking (incompatible with the lazy untyped-args model); keep only central *arity* validation.

**Effort:** L–XL · **Risk:** Medium–High · **Breaking:** Yes
**Rationale:** The largest correctness/tooling liability in the language; the consolidation is the architecturally correct core. Reject the bundled "purely additive" framing — the loud-error behavior is the actual breaking change and needs its own OpenSpec proposal.

### 2.9 Sensitive value typing with automatic redaction in output and history

**Problem:** `password:true` only masks the input dialog; once a secret lands in a variable it is plain text in printed messages, captured output, and persisted execution history. Vault secrets and credentials leak.

**Design (scoped):** Maintain a per-execution `HashSet<string>` of secret literals, populated automatically by the `vault` command and inline `{{vault:...}}` resolution, plus an optional `sensitive: true` on `vars:`/password inputs. Apply substring redaction in two centralized places: `EmitOutput` (covers console + persisted history in one shot) and the debugger variable-snapshot builders (mask whole flagged values). **Drop** the fictional typed `inputs:` schema and `reveal()`; document redaction as best-effort defense-in-depth (transformed/encoded secrets won't be caught).

```yaml
steps:
  - vault: { profile: prod, path: secret/ssh, key: enable_pw, into: enable }  # auto-sensitive
  - print: "token=${enable}"   # history/output shows: token=****
```

**Effort:** M–L · **Risk:** Medium · **Breaking:** No
**Rationale:** Real security value for a credential-handling tool, and the `EmitOutput` sink centralizes most of the work. Note the debugger `GetAllVariables()` path the proposal omits — a structured-map leak that substring masking won't catch.

### 2.10 Typed, defaulted, required/optional subroutine params and input schema

**Problem:** Subroutine params/outputs and `vars` are bare lists/defaults with no types, no defaults, no required/optional distinction. `ValidateCallStep` treats every param as mandatory and cannot express "count defaults to 4" or "timeout must be int 1..300".

**Design (phased):** **Phase 1 (cleanest fit):** richer subroutine params — `type` as validation-only (stored value stays string to avoid runtime-typing regressions), `required` defaulting to true (preserves behavior), `default` to relax the all-mandatory rule, optional `in`/`range`. **Phase 2:** a top-level `inputs:` schema, but only with an **engine-level** preflight (the existing required-input check is Form1-only and headless jobs skip it). **Phase 3 (optional):** `JobDefinition.Params` + "Run with parameters" dialog + documented precedence.

```yaml
subroutines:
  block_ip:
    params:
      ip:  { type: string, required: true }
      ttl: { type: int, default: 3600, range: [60, 86400] }
```

**Effort:** L–XL · **Risk:** Medium · **Breaking:** No (if required-by-default scoped to new inputs)
**Rationale:** Closes a real DSL gap for reusable subroutines. Push back on the "catches `${hostnam}` typos" framing — it does not; that is a separate strict-references feature.

---

## Tier 3 — Nice to Have

*Moderate value. Useful ergonomics worth doing when capacity allows.*

### 3.1 Aggregate & grouping collection functions (sum/avg/group_by/sort_by/chunk)

Add `sum`/`avg`/`sort_by`/`min_by`/`max_by`/`group_by`/`chunk` to `CollectionFunctions` for data-shaping over SSH output. **Effort:** M · **Risk:** Low · **Breaking:** No. **Rationale:** Real gaps for ranking/aggregating parsed output, but lambdas need `json.get` (no dotted access yet); **drop `min`/`max`** (they collide with existing math functions).

### 3.2 Crypto hygiene: HMAC, hash_file, fail-closed hash()

Land `hash_file(path, algo?)` first (streams raw bytes, fixes the `readfile` line-orientation gap, enables upload-if-changed SFTP idempotency); add `hmac(algo, key, message)` for webhook signing. **Effort:** S–M · **Risk:** Low–Medium · **Breaking:** No (trimmed). **Rationale:** Solid wins, but the marquee "fail-closed throw" cuts against the codebase-wide null-on-error convention — keep returning null + a warning. **Drop** `hash_files(glob)` and `secure_random_int`.

### 3.3 Function signature metadata + call-tips and edit-time linting

Phase 1: edit-time **regex linting** for `extract.pattern`/expect patterns (compile in the validation service, catch `RegexParseException` as an Error) — high value, self-contained. Phase 2: unknown-function did-you-mean over a unioned name source. Phase 3 (speculative): `FunctionDescriptor` metadata + CallTips. **Effort:** L · **Risk:** Low · **Breaking:** No. **Rationale:** Phase 1 alone justifies the slot; the descriptor metadata it claims to build on does not exist.

### 3.4 Unify on_error onto one model and standardize on `into:` as canonical capture

Delete the dead `OnError` fields from `NotifyOptions`/`VaultStepOptions`, route through `ApplyOnError`, switch `CallCommand` to `IsOnErrorContinue` (a ~30-min dead-code cleanup). Separately, add `into:` as a parse-time alias for `send`/`interactive` capture. **Effort:** S (cleanup) + M (capture) · **Risk:** Low · **Breaking:** No. **Rationale:** Learnability and ghost-removal; the `on_error` "inconsistency" is mostly already-dead duplication.

### 3.5 User-defined pure functions via a top-level `functions:` block

A local-only `functions:` block registering named lambdas into a per-execution overlay registry consulted before the singleton, callable inline via an `fn.` prefix. **Effort:** M (scoped) · **Risk:** Medium · **Breaking:** No. **Rationale:** Removes copy-paste of repeated transforms; ship local-only v1 and **defer** importable/aliased functions (most of the cost and risk).

### 3.6 Undefined-variable & unused-variable linting in the editor

A scoped undefined-variable **Warning** limited to `${...}`/`{{...}}` interpolations (not bare identifiers, which collide with `is defined`). First add foreach-iterator/`{iter}_index` symbols to the in-scope set, exclude imported aliases, and skip interpolated targets. **Effort:** M–L · **Risk:** Medium · **Breaking:** No. **Rationale:** Real runtime footgun, but the proposal mischaracterizes `ExtractDynamicSymbols` as scope-aware (it is flat/global). **Reject the unused-variable half** (dominated by side-effecting commands the analyzer can't see).

### 3.7 TextFSM-style template parsing into records

Ship only the S-effort `format: regex_table` beachhead (per-line named-capture-into-records, registered in `ParserFactory`) — kills the documented regex/`match:all` hacks in the Cisco samples. **Defer** the full TextFSM engine; **reject** bundling the `ntc-templates` corpus. **Effort:** S (beachhead) · **Risk:** Medium · **Breaking:** No. **Rationale:** Real pain, but record consumption requires dotted member access in `foreach` (a prerequisite) — fix the proposal's non-working example first.

---

## Considered & Deferred

*Rejected or weak-marginal items. Each carries the reviewer's reason. Several flagship examples do not work against the actual engine.*

- **First-class string interpolation/format() with padding** — *Reject.* `format()` with full .NET format-spec support, `pad_left`/`pad_right`, and `repeat` already ship and are documented; ~90% already exists.
- **Honest numbers: integer identity, exponent/integer-division** — *Reject.* Breaks the project's own loop-counter idiom (`set: i = i + 1`) and locked tests; `pow()`/`floor()` already exist; `//` collides with URLs. Salvage only `approx()`.
- **Distinguish null/missing/empty + recursion-safe interpolation** — *Reject (premises false).* `set x = null` stores the string `"null"`; the engine does not re-expand resolved values (no DoS). Salvage only the `${_output}` pre-splice fix.
- **Stored-type-honest type inspection (typeof/is_number)** — *Marginal.* Content-sniffing is deliberate for a string-dominated domain; the change breaks the two patterns the docs teach. Ship only opt-in `looks_like_number`.
- **First-class typed list/object values with unified deep-path grammar** — *Marginal/XL.* Deep indexing via `json.get` already works; re-canonicalizing on `JsonNode` touches ~5K LOC + a 4,914-LOC bridge for low real-world value. Cherry-pick the foreach-over-object bug fix.
- **Default/required/?? coalescing inside interpolation** — *Marginal.* `??` already works in `set:`; the breaking null-only redefinition and permanent dual-mode flag outweigh benefit. The editor diagnostic is the real value.
- **Operator aliasing (&&/||/!), chained comparisons, between** — *Marginal.* Targets the wrong engine; only the unspaced-operator silent-truthiness fix is valuable, and the path has zero unit tests. Word operators already exist.
- **Strict-by-default identifier/function resolution** — *Marginal.* Flagship example is factually wrong (`usrname is empty` yields false, not true); regex-swallow is documented intentional behavior; editor-time undefined-var detection isn't feasible without scope tracking. Salvage only invalid-regex warning.
- **Pipeline operator `|>`** — *Marginal.* Pure sugar over existing capability, targets the wrong layer (string-path, not `ExpressionParser`), niche audience, opaque to the debugger.
- **Typed structured errors with catch-as binding and when-filter** — *Marginal.* Flagship `{{err.kind}}`/`{{err.details.exit_code}}` syntax is unresolvable (flat dict lookup, no nested maps); requires enriching ~38 commands. Salvage only the `when:` catch filter.
- **raise/rethrow from catch, cause chaining, durable _failure** — *Marginal.* Depends on three unbuilt features (named catch binding, `ScriptError` type, nested property resolution). Salvage only a bare `raise:` and the durable `_failure` string.
- **Configurable retry policy: backoff, jitter, error filter, _attempt** — *Marginal.* The `when` error-filter needs a non-existent error taxonomy; the thread-safety justification doesn't hold. Salvage backoff+jitter+the `step.OnError` hygiene fix.
- **Executor-enforced per-step and per-block timeouts** — *Marginal.* `CancelAfter` cannot interrupt the non-cooperative handlers cited as motivation; `category: timeout` references a non-existent typed-error system. `send` already has a timeout.
- **Status functions: success()/failure()/always()/changed()** — *Marginal.* `cancelled()` can never be true (cancel unwinds), `always()` is literal true, `changed()` has no infrastructure. Salvage only `success`/`failure` + an `any_failed` aggregate.
- **Handlers: notify-on-change deferred steps** — *Marginal.* Core value depends on non-existent change-tracking; the fallback is a deferred boolean already expressible with `set:` + a final `if:`.
- **First-class matrix fan-out** — *Marginal/XL.* Isolation is illusory (`CreateChildScope` shares the single session) and concurrency is a no-op for send-blocks (the `(1,1)` session lock); nested `foreach` already covers the safe case.
- **Idempotency primitives: unless/creates, changed_when, failed_when** — *Marginal.* Built on a "structured command result register" that does not exist; `failed_when` overlaps `fail_on_nonzero`; rc unreliable on network CLIs.
- **Generic structured parse: json/csv/kv/ini + json get** — *Marginal.* The showcase `foreach r in ... ${r.iface}` cannot work (no object iteration / member access); `get` duplicates existing `json.get`. Land only json+csv parsers after building member access.
- **Counter helpers incr/decr/tally** — *Marginal.* Sugar over `set: x = x + 1` justified by a single demo file; auto-init masks typos. Prefer compound assignment (`set: passed += 1`) inside the existing `set`.
- **Robust, shell-portable remote exit-status capture** — *Marginal.* Premise false: the `fail_on_nonzero`+`expect` conflict is already a parse + runtime error; `shell: none` duplicates `send.expect`; example references a non-existent `send.into`.
- **Raw send primitives: control characters, no-newline send** — *Marginal.* `send.expect`/`respond` already drive non-shell programs (the `passwd` example is verbatim in the docs). Only the Ctrl-C/Ctrl-Z abort is a real gap — add it as a `send` option.
- **Multi-line send_block / heredoc** — *Marginal.* `split()`+`foreach` already approximates it; the boilerplate claim is inflated; per-line prompt detection (the safety property) is proposed opt-in, backwards. Prefer a documented idiom.
- **Heredoc-style remote file push** — *Marginal.* `writefile` + `sftp upload` already does this; `owner` can't be done over pure SFTP. Add an inline `content` mode to the existing `sftp` command instead of a new StepType.
- **Template rendering command with for/if expansion** — *Marginal.* `map`+`join`+`set` covers flat lists; adds a third templating delimiter and a Jinja-lite grammar for narrow incremental value.
- **Inline remote command substitution ($sh{...})** — *Reject.* Hooks synchronous `SubstituteVariables` (149 call sites) into async SSH I/O, re-enters and corrupts the single stateful channel, runs at design-time with a null session.
- **Device-type drivers (cisco_ios, fortios, etc.)** — *Marginal.* Pain mostly solved (auto-paging, prompt tracking); net value is narrow; auto-exit-on-error can leave devices in config mode; six profiles carry untestable maintenance burden.
- **Single machine-readable DSL schema replacing option switches** — *Marginal/XL.* Misdiagnoses the state — editors already read from the parser and DriftGuard tests already enforce consistency; near-zero user value for an XL/high-risk internal refactor. Cherry-pick the specific shorthand/alias fixes.
- **Robust YAML normalization to retire PreprocessYaml** — *Marginal.* False premises: line counts are preserved (no shifted numbers), no DSL-schema dependency exists. Real fix is adding ~8 free-text keys to the existing allowlist.
- **YAML anchors/aliases/merge-keys + defaults:/use:** — *Reject.* Built on a dead `_deserializer` field; the parser is a hand-rolled event reader; Flow Canvas re-serialization silently strips anchors. Salvage only merge-key support via `MergingParser`.
- **Portable imports: relative paths, search path, transitive loading** — *Marginal.* "Relative to the importing file" has no anchor (scripts are strings, not files); search paths recreate the portability problem; ~zero demonstrated demand. Salvage only cycle detection.
- **Namespaced contexts: env.*, host.*, secret()** — *Marginal.* Dot-notation breaks `ScriptDependencyAnalyzer`; needs new logic in both resolution paths; `secret()` redaction is an unbuilt subsystem. Prefer a collision lint + (separately) `host.*`.
- **Inventory groups with layered group/host variables** — *Marginal.* Branch-on-group already works via a CSV column + `split()` + `in`; resolution isn't centralized as claimed; the valuable run-time `limit:` selector is a separate, better-scoped host-selection proposal.
- **Cross-host result aggregation and after_all/diff summary** — *Marginal/XL.* The headline use case already works via `writefile` append (documented); `after_all` collides with three dispatch paths and the parallel-batch model. Salvage only a string `diff()` built-in.
- **Dry-run / simulate mode with mock SSH session** — *Marginal.* Control-flow preview is systematically wrong for stateful device scripts (stubbed empty output breaks verify branches); `OnTestStep` collides with a shipped feature. Ship a read-only command-interpolation preview instead.
- **Built-in test harness with mocked send/http output** — *Marginal/XL.* Two pillars don't survive contact: no dry-run mock session to build on, and a CLI for a GUI-only WinForms app. Lean on the existing xUnit + fake-session pattern.
- **Snippet scaffolding on command completion** — *Marginal.* "Schema-driven" premise false (hand-maintained duplicate dict); no snippet/tab-stop engine exists; block commands need body scaffolding the design ignores; ~80% already solved by required-key tagging.
- **Format Document command (auto-fix YAML hygiene)** — *Marginal.* Central premise false (no Script-to-YAML serializer; `FlowCanvasBridge` serializes a graph, not the model); parse-and-reserialize silently destroys comments and drops keys. Ship only a lexical tabs→spaces/trim pass.
- **Script-level strict mode (set -euo pipefail)** — *Marginal.* `fail_on_nonzero` default is dangerous across incompatible shell targets (POSIX vs FortiGate/Cisco); the advertised companion flags don't exist. Implement undefined-variable safety as a static pre-execution lint instead.