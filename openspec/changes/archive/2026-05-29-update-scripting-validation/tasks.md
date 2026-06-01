# Status: Sections 1–4 implemented (all code complete). Only 5.4 (release notes) remains before archive.

## 1. Strict typo-class keys (BREAKING) — DONE
- [x] 1.1 Typo-class unknown keys now block: `AddUnknownKeyWarning` (ScriptParser.cs) records to a `_unknownKeyErrors` list; `Validate()` merges `_unknownKeyErrors` into its returned errors. (Cleaner than editing ~45 call sites.)
- [x] 1.2 The three `interactive.*` deprecations stay non-blocking via a dedicated `AddDeprecationWarning` → `_warnings`. (Replaced the original fragile "deprecated" substring heuristic in 1.3 — see below — so a user key literally named `deprecated` is no longer silently downgraded.)
- [x] 1.3 Misspelled command is a blocking parse error with did-you-mean. `GetCandidateKeys("step")` now also offers `KnownStepKeys` (command names), so `snd` → `Did you mean 'send'?`. A new per-step `ScriptStep.HasUnknownStepKey` flag (set in the `ParseStep` default case) suppresses the redundant generic "Step has no recognized command" for the misspelled-command step while still reporting it for genuinely command-less steps. `AddUnknownKeyWarning` is now unconditionally blocking (the substring heuristic was removed), so the flag always implies a blocking error → suppression is safe. Three adversarial review rounds drove this: (R1) line-number-keyed suppression broke in flow/multi-line YAML → per-step flag; (R2) `deprecated`-substring collision let an unknown command pass silently → explicit `AddDeprecationWarning` + always-blocking unknown keys; (R3) clean.
- [x] 1.4 `ScriptEditorValidationService` surfaces them (it consumes `Validate()` errors, which now include the key errors)
- [x] 1.5 Samples validate clean (QaPreset parse/validate tests pass in the non-UI suite)
- [x] 1.6 Tests: `ScriptStrictKeyValidationTests`; updated `Validate_UnknownStepKey_IsBlockingErrorWithLineNumber`, `Validate_UnknownOptionKey_IsBlockingError`, `Validate_ExistsUnknownKey_IsBlockingError`; interactive deprecation tests still warn

## 2. Did-you-mean suggestions — DONE
- [x] 2.1 `SuggestClosest` + `LevenshteinDistance` (ScriptParser.cs); candidates from `CommandOptionKeys` + `CommonStepOptionKeys` + `StepRootOptionKeysByCommand` via `GetCandidateKeys`
- [x] 2.2 `AppendDidYouMean` parses `Unknown <cmd> key '<value>'` and appends `Did you mean 'X'?`
- [x] 2.3 Suppressed for short (<=2 char) and distant tokens (threshold `max(2, len/3)`)
- [x] 2.4 Tests: close key (tieout->timeout), distant key (no suggestion). Unknown-*command* suggestion (`snd`->`send`) delivered in 1.3.

## 3. Parse-time shorthand grammar validation — DONE
- [x] 3.1 Foreach iterator grammar validated at parse time. New `ForeachCommand.IsValidIteratorSyntax(string?)` shares the runtime `DictPattern`/`ForeachPattern` regexes (one source of truth, no parse/runtime drift); `ValidateSteps` `case StepType.Foreach` adds "Invalid foreach syntax: '<v>'. Expected 'item in collection' or 'key, value in map'" for non-empty malformed iterators. Covers BOTH scalar shorthand and the canonical `foreach.iterator` mapping form.
- [x] 3.2 Set grammar validated at parse time. `ValidateSteps` `case StepType.Set` keeps the existing no-`=`/empty → "Set requires 'variable = value' format", and adds empty-name → "Set requires a variable name before '='". DECISION: an empty value after `=` (e.g. `x =`) is ACCEPTED as initialize-to-empty — the runtime supports it and the shipped `qa_presets.json` "QA Nested Loops" preset relies on it (`dir_listing = `). Parse-time acceptance is aligned to runtime: only forms the runtime would Fail are rejected. Spec scenarios updated accordingly.
- [x] 3.3 Exit shorthand: confirmed free-text by design — every non-empty exit scalar is a valid status and/or message at runtime (`ExitCommand`), so there is NO malformed form to reject. No production change; regression tests lock that well-formed exit shorthands (status-only, status+message, plain message) stay accepted. Spec scenario added.
- [x] 3.4 Tests: `Scripting/ScriptShorthandGrammarValidationTests.cs` (19 tests) — malformed foreach/set rejected (incl. mapping form and repeat-nested), well-formed/initialize-to-empty/equality-in-value/dotted-name/interpolation/function-call collections accepted, exit shorthand accepted.
- [x] 3.5 Bug found by adversarial review + FIXED: `ValidateSteps` `case StepType.Repeat` never recursed into `step.Do`, so ALL type-specific validation (incl. the new grammar checks, plus pre-existing foreach-do/break-depth/etc.) was skipped for steps nested in a `repeat`/`until` body — deferring those failures to runtime. Added the missing `ValidateSteps(step.Do, ... loopDepth + 1 ...)` recursion (mirrors While/Foreach). Pre-existing structural omission from Proposal B's repeat/until; zero shipped-sample exposure (`repeat:` unused in ScriptSamples/qa_presets).
- NOTE: pre-implementation risk audit (79 files) + adversarial multi-lens review both clean apart from the repeat-recursion bug above; no sample/preset migration needed.

## 4. Unified interpolation scanner — DONE
- [x] 4.1 The two scanner branches in `ScriptContext.SubstituteVariableTokens` (balanced `${ }` via `TryExtractDollarExpression`; naive first-`}}` `{{ }}` via `IndexOf`) are replaced by one generic `TryExtractBalanced(input, start, open, close, ...)` (+ `MatchesAt` helper) used for both `${`/`}` and `{{`/`}}`. `TryExtractDollarExpression` deleted. `ValueResolver` already only delegates to `SubstituteVariables` — no second scanner existed.
- [x] 4.2 `{{ }}` canonical, `${ }` alias. `${ }` behavior is byte-identical (the generic extractor reproduces the old balanced algorithm — verified by construction + existing `ScriptContextTests` + adversarial review). `{{ }}` is upgraded from first-match to balanced/nested so it matches `${ }`. No escape syntax in either form (backslashes literal) — "identical escaping rules" holds trivially; spec scenario clarified.
- [x] 4.3 Tests: `Scripting/ScriptInterpolationScannerTests.cs` (7 tests) — nested-brace `{{a[{{i}}]}}` resolves via balanced scan, `{{ }}`≡`${ }` for nested + simple, adjacency, dollar-in-brace, unclosed-left-literal, no-escape backslash passthrough. Existing `ScriptContextTests` substitution tests stay green.
- NOTE: was flagged RISKY (core substitution path). De-risked via: full characterization that the only observable change is `{{ }}`→balanced (differs from old only when a nested `{{` precedes the first `}}`, an unused pattern); full non-UI suite 1960/1960; 3-lens adversarial review found zero divergence from the old `${ }` scanner.

## 5. Verification
- [x] 5.1/5.2 build + non-UI suite green — **1970/1970** after sections 1(incl. 1.3)+3+4 (was 1936 at part 1; +34 grammar/coverage/interpolation/command-suggestion tests).
- [x] 5.3 `openspec validate update-scripting-validation --strict --no-interactive` → valid
- [ ] 5.4 CHANGELOG/release-note for BOTH breaking changes (strict typo-keys + Proposal B loop-iterator scoping); use the `release-notes` skill — change is now feature-complete, ready for this finalization step

## How to verify cleanly (UI tests are pre-existing scheduling-fragile — see project memory)
`dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName!~SSH_Helper.Tests.UI.&FullyQualifiedName!~ReadFileCommandTests" --blame-hang-timeout 120s`
