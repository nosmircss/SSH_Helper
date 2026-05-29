## 1. Strict typo-class keys (BREAKING)
- [ ] 1.1 Route the ~45 typo-class `AddUnknownKeyWarning("Unknown <cmd> key …")` call sites to `AddStepParseError`/`AddScriptParseError` (blocking errors)
- [ ] 1.2 Keep the three `interactive.*` deprecation calls on the warning path
- [ ] 1.3 Promote misspelled-command (`StepType.Unknown`) to a blocking parse error
- [ ] 1.4 Confirm `ScriptEditorValidationService` surfaces the new errors as squiggles
- [ ] 1.5 Audit `ScriptSamples/` and shipped samples/presets; fix any unrecognized keys
- [ ] 1.6 Tests: typo key blocks execution; `interactive.*` deprecations still warn; samples validate clean

## 2. Did-you-mean suggestions
- [ ] 2.1 Add a static `SuggestClosest(input, candidates)` helper (bounded Levenshtein)
- [ ] 2.2 Append `Did you mean 'X'?` as a suffix to unknown-key and unknown-command messages, sourced from the existing canonical key tables; keep the offending token first
- [ ] 2.3 Suppress suggestions for short/ambiguous keys (`mode`, `host`, `port`) and distant tokens
- [ ] 2.4 Tests: close key, close command, no-suggestion cases

## 3. Parse-time shorthand grammar validation
- [ ] 3.1 Validate `foreach` shorthand against `item in collection` at parse time
- [ ] 3.2 Validate `set` shorthand (name/expression present) at parse time
- [ ] 3.3 Validate `exit` shorthand at parse time
- [ ] 3.4 Tests: malformed forms rejected at parse time; well-formed forms unchanged

## 4. Unified interpolation scanner
- [ ] 4.1 Replace the two ad-hoc `{{ }}`/`${ }` scanners with one balanced-brace scanner in `ScriptContext`/`ValueResolver`
- [ ] 4.2 Keep `{{ }}` canonical, `${ }` as alias; identical nesting/escaping
- [ ] 4.3 Tests: nested/adjacent interpolation; `{{ }}` and `${ }` equivalence; existing substitution tests stay green

## 5. Verification
- [ ] 5.1 `dotnet build SSH_Helper.sln`
- [ ] 5.2 `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- [ ] 5.3 `openspec validate update-scripting-validation --strict --no-interactive`
- [ ] 5.4 Add a release-note entry for the BREAKING strict-key-validation change
