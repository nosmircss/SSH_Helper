# Change: Add scripting helper functions (networking, date/time, regex)

## Why
The DSL is an SSH/network automation tool yet exposes zero networking helpers, silently mixes local and UTC time bases across its date functions (a latent correctness bug — `now()`/`epoch_to_date` use local time while `epoch()` uses UTC), and offers no inline regex match/capture. Operators hand-roll brittle regex and string math to compensate.

## What Changes
- Add a `NetworkFunctions` category: `is_valid_ip`, `ip_version`, `ip_in_cidr`, and optional `parse_url`/`url_host`/`url_port`. Pure and deterministic, no new dependency (`System.Net.IPNetwork`/`IPAddress`/`Uri` are already in the BCL).
- Extend `DateTimeFunctions`: add explicit `now_utc()`/`now_local()` to remove the silent local-vs-UTC mix; extend `date_add`/`date_diff` units with `week`/`month`/`year`; add an explicit parse-format parameter. `now()` is retained for backward compatibility.
- Add regex functions `regex_match`, `regex_match_all`, `regex_groups` to `StringFunctions`, reusing `RegexReplace`'s 5s timeout and delimiter handling.
- Teach the `extract` command to surface named capture groups as named variables, gated so positional behavior is byte-for-byte preserved when no named groups exist.

## Impact
- Affected specs: `scripting-expressions` (new function families), `scripting-runtime` (named-capture extraction)
- Affected code: `Services/Scripting/Functions/NetworkFunctions.cs` (new), `DateTimeFunctions.cs`, `StringFunctions.cs`, `FunctionRegistry.cs` (registration), `Services/Scripting/Commands/ExtractCommand.cs`, `Services/Editor/ScriptAutocompleteProvider.cs`, tests
- **Non-breaking** — every change is additive; `now()` semantics are unchanged.
