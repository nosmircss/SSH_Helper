## Context
`ScriptParser` already separates blocking errors (`AddScriptParseError`/`AddStepParseError` → collections every execution caller honors) from a soft `_warnings` list surfaced via `Warnings`. Roughly 45 call sites funnel unrecognized keys through `AddUnknownKeyWarning(message, line)` into `_warnings`; three of those are genuine deprecations (`interactive.columns`, `interactive.rows`, `interactive.emulation`). The parser also owns complete canonical key tables per command (e.g. `["assert"] = ["condition","message","severity"]`) and already models a `severity: error|warning` concept on `assert`. A misspelled command resolves to `StepType.Unknown`. Two ad-hoc interpolation scanners exist for `{{ }}` and `${ }`.

## Goals / Non-Goals
- Goals: make typo-class unknown keys fatal; helpful did-you-mean diagnostics; parse-time shorthand grammar checks; one interpolation scanner.
- Non-Goals: a tri-state strictness enum or new top-level `parse:` config key (rejected as over-engineered); enum-value validation for option *values* (no failure site today — deferred); changing the canonical interpolation form away from `{{ }}`.

## Decisions
- **Decision: classify unknown-key findings by severity instead of one flat list.** Typo-class keys call the existing `AddStepParseError`/`AddScriptParseError` path (already honored by execution callers); the three `interactive.*` calls stay on `AddUnknownKeyWarning`. This is the smallest change that closes the footgun.
  - The requirement `Non-fatal parser warnings` is renamed to `Unknown-key diagnostic severity` because it now governs both error and warning severities.
- **Decision: did-you-mean is a static `SuggestClosest(input, candidates)` (bounded Levenshtein), appended as a suffix.** The offending token stays first in the message so editor squiggle positioning is unaffected. Suggestions are suppressed for short/ambiguous keys (`mode`, `host`, `port`).
- **Decision: keep `{{ }}` canonical, `${ }` alias.** Every existing script and sample uses `{{ }}`; documenting `${ }` as canonical (as the source roadmap suggested) would be churn for no user benefit. The work is replacing two scanners with one balanced-brace scanner so nesting/escaping behave identically.

## Risks / Trade-offs
- **Risk: promoting typos to errors is BREAKING** — any existing script or stored preset with an unrecognized key stops executing until fixed. Accepted per the strict-now decision. Mitigation below.
- **Risk: over-broad classification** could flag a legitimately-supported-but-unspecced key as a typo. Mitigation: drive errors strictly from the parser's existing canonical key tables; anything in a table is valid, only out-of-table keys error; audit `ScriptSamples/` and run validation across them before merge.

## Migration Plan
1. Audit `ScriptSamples/` and the shipped sample/preset content; fix any unrecognized keys surfaced by the new errors.
2. Run `dotnet test` and a validation pass over all samples; zero unexpected errors before merge.
3. Release note documents that typo-class keys now block execution, with the did-you-mean message guiding the fix.
4. Rollback: the error-promotion commit is isolated from the did-you-mean, grammar, and interpolation commits and can be reverted independently.

## Open Questions
- None blocking.
