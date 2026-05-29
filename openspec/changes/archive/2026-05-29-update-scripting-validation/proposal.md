# Change: Strict script validation and diagnostics

## Why
A typo like `tieout: 30` parses cleanly, the option is silently dropped, and the step runs with defaults against real hosts — the single most dangerous gap in the language. Unknown keys go to a `_warnings` list that every execution caller ignores. Diagnostics are bare (`Unknown http key 'tieout'`) even though the parser already owns canonical key tables, and a misspelled command silently becomes `StepType.Unknown`. The engine also maintains two subtly different interpolation scanners.

## What Changes
- Promote clearly-unrecognized (typo-class) keys from ignored warnings to **blocking parse errors**, while keeping the recognized `interactive.*` deprecation notices as non-fatal warnings. **BREAKING**: scripts/presets containing unrecognized keys will fail validation until fixed.
- Add did-you-mean suggestions (bounded Levenshtein) to unknown-key and unknown-command diagnostics, sourced from the existing canonical key tables; suppress for short/ambiguous tokens.
- Validate `set`/`foreach`/`exit` shorthand grammar at parse time instead of deferring failure to runtime.
- Unify the two interpolation scanners onto one balanced-brace scanner, keeping `{{ }}` as the canonical form and `${ }` as a supported alias.

## Impact
- Affected specs: `scripting-validation` (unknown-key severity, did-you-mean, shorthand grammar), `scripting-runtime` (unified interpolation)
- Affected code: `ScriptParser` (typo-class `AddUnknownKeyWarning` calls → error path, new `SuggestClosest` helper, shorthand grammar checks), `ScriptContext`/`ValueResolver` (single interpolation scanner), `ScriptEditorValidationService` (surfaces errors as squiggles), `ScriptAutocompleteProvider`, tests, `ScriptSamples/` audit
- **BREAKING**: unknown (typo-class) keys now block execution. See `design.md` for migration.
