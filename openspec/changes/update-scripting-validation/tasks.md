# Status: Part 1 (commit 1d3646e) + sub-feature 3 implemented. RESUME AT section 4 (RISKY — reconfirm before doing).

## 1. Strict typo-class keys (BREAKING) — DONE (except 1.3)
- [x] 1.1 Typo-class unknown keys now block: `AddUnknownKeyWarning` (ScriptParser.cs) routes deprecation messages to `_warnings` and typo-class messages to a new `_unknownKeyErrors` list; `Validate()` merges `_unknownKeyErrors` into its returned errors. (Cleaner than editing ~45 call sites.)
- [x] 1.2 The three `interactive.*` deprecation calls stay on the warning path (routed by the "deprecated" substring)
- [ ] 1.3 Promote misspelled-command (`StepType.Unknown`) to a blocking parse error with did-you-mean — NOT DONE (still a runtime warning)
- [x] 1.4 `ScriptEditorValidationService` surfaces them (it consumes `Validate()` errors, which now include the key errors)
- [x] 1.5 Samples validate clean (QaPreset parse/validate tests pass in the non-UI suite)
- [x] 1.6 Tests: `ScriptStrictKeyValidationTests`; updated `Validate_UnknownStepKey_IsBlockingErrorWithLineNumber`, `Validate_UnknownOptionKey_IsBlockingError`, `Validate_ExistsUnknownKey_IsBlockingError`; interactive deprecation tests still warn

## 2. Did-you-mean suggestions — DONE
- [x] 2.1 `SuggestClosest` + `LevenshteinDistance` (ScriptParser.cs); candidates from `CommandOptionKeys` + `CommonStepOptionKeys` + `StepRootOptionKeysByCommand` via `GetCandidateKeys`
- [x] 2.2 `AppendDidYouMean` parses `Unknown <cmd> key '<value>'` and appends `Did you mean 'X'?`
- [x] 2.3 Suppressed for short (<=2 char) and distant tokens (threshold `max(2, len/3)`)
- [x] 2.4 Tests: close key (tieout->timeout), distant key (no suggestion). NOTE: unknown-*command* suggestion still pending with 1.3.

## 3. Parse-time shorthand grammar validation — DONE
- [x] 3.1 Foreach iterator grammar validated at parse time. New `ForeachCommand.IsValidIteratorSyntax(string?)` shares the runtime `DictPattern`/`ForeachPattern` regexes (one source of truth, no parse/runtime drift); `ValidateSteps` `case StepType.Foreach` adds "Invalid foreach syntax: '<v>'. Expected 'item in collection' or 'key, value in map'" for non-empty malformed iterators. Covers BOTH scalar shorthand and the canonical `foreach.iterator` mapping form.
- [x] 3.2 Set grammar validated at parse time. `ValidateSteps` `case StepType.Set` keeps the existing no-`=`/empty → "Set requires 'variable = value' format", and adds empty-name → "Set requires a variable name before '='". DECISION: an empty value after `=` (e.g. `x =`) is ACCEPTED as initialize-to-empty — the runtime supports it and the shipped `qa_presets.json` "QA Nested Loops" preset relies on it (`dir_listing = `). Parse-time acceptance is aligned to runtime: only forms the runtime would Fail are rejected. Spec scenarios updated accordingly.
- [x] 3.3 Exit shorthand: confirmed free-text by design — every non-empty exit scalar is a valid status and/or message at runtime (`ExitCommand`), so there is NO malformed form to reject. No production change; regression tests lock that well-formed exit shorthands (status-only, status+message, plain message) stay accepted. Spec scenario added.
- [x] 3.4 Tests: `Scripting/ScriptShorthandGrammarValidationTests.cs` (19 tests) — malformed foreach/set rejected (incl. mapping form and repeat-nested), well-formed/initialize-to-empty/equality-in-value/dotted-name/interpolation/function-call collections accepted, exit shorthand accepted.
- [x] 3.5 Bug found by adversarial review + FIXED: `ValidateSteps` `case StepType.Repeat` never recursed into `step.Do`, so ALL type-specific validation (incl. the new grammar checks, plus pre-existing foreach-do/break-depth/etc.) was skipped for steps nested in a `repeat`/`until` body — deferring those failures to runtime. Added the missing `ValidateSteps(step.Do, ... loopDepth + 1 ...)` recursion (mirrors While/Foreach). Pre-existing structural omission from Proposal B's repeat/until; zero shipped-sample exposure (`repeat:` unused in ScriptSamples/qa_presets).
- NOTE: pre-implementation risk audit (79 files) + adversarial multi-lens review both clean apart from the repeat-recursion bug above; no sample/preset migration needed.

## 4. Unified interpolation scanner — RISKY, reconfirm before doing
- [ ] 4.1 Replace the two ad-hoc `{{ }}`/`${ }` scanners with one balanced-brace scanner in `ScriptContext`/`ValueResolver`
- [ ] 4.2 Keep `{{ }}` canonical, `${ }` as alias; identical nesting/escaping
- [ ] 4.3 Tests: nested/adjacent interpolation; `{{ }}` and `${ }` equivalence; existing substitution tests stay green
- WARNING: high blast radius (core substitution path), lowest roadmap value (4). Confirm worth doing before starting.

## 5. Verification
- [x] 5.1/5.2 build + non-UI suite green — **1953/1953** after sections 1+3 (was 1936 at part 1; +17 grammar/coverage tests). Re-run full after section 4.
- [x] 5.3 `openspec validate update-scripting-validation --strict --no-interactive` → valid (after section 3 spec deltas)
- [ ] 5.4 CHANGELOG/release-note for BOTH breaking changes (this strict-keys + Proposal B loop-iterator scoping); use the `release-notes` skill — defer until section 4 decided / change finalized

## How to verify cleanly (UI tests are pre-existing scheduling-fragile — see project memory)
`dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName!~SSH_Helper.Tests.UI.&FullyQualifiedName!~ReadFileCommandTests" --blame-hang-timeout 120s`
