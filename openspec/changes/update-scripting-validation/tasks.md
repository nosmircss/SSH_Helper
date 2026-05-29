# Status: Part 1 implemented (commit 1d3646e). RESUME AT section 3.

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

## 3. Parse-time shorthand grammar validation — RESUME HERE (additive, low risk)
- [ ] 3.1 Validate `foreach` shorthand against `item in collection` at parse time (ForeachCommand already has `ForeachPattern`/`DictPattern`; the parser shorthand path should reject malformed forms in `Validate`/parse instead of at runtime)
- [ ] 3.2 Validate `set` shorthand (name/expression present) at parse time
- [ ] 3.3 Validate `exit` shorthand at parse time
- [ ] 3.4 Tests: malformed forms rejected at parse time; well-formed forms unchanged
- NOTE: see `scripting-validation` spec "Supported shorthand syntax acceptance" + scripting-runtime "Shorthand aliases for single-primary-field commands" for the canonical forms.

## 4. Unified interpolation scanner — RISKY, reconfirm before doing
- [ ] 4.1 Replace the two ad-hoc `{{ }}`/`${ }` scanners with one balanced-brace scanner in `ScriptContext`/`ValueResolver`
- [ ] 4.2 Keep `{{ }}` canonical, `${ }` as alias; identical nesting/escaping
- [ ] 4.3 Tests: nested/adjacent interpolation; `{{ }}` and `${ }` equivalence; existing substitution tests stay green
- WARNING: high blast radius (core substitution path), lowest roadmap value (4). Confirm worth doing before starting.

## 5. Verification
- [~] 5.1/5.2 build + non-UI suite green (1936/1936) for part 1; re-run full after sections 3/4
- [ ] 5.3 `openspec validate update-scripting-validation --strict --no-interactive`
- [ ] 5.4 CHANGELOG/release-note for BOTH breaking changes (this strict-keys + Proposal B loop-iterator scoping); use the `release-notes` skill

## How to verify cleanly (UI tests are pre-existing scheduling-fragile — see project memory)
`dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName!~SSH_Helper.Tests.UI.&FullyQualifiedName!~ReadFileCommandTests" --blame-hang-timeout 120s`
