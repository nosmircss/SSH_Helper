## 1. Networking functions
- [x] 1.1 Add `Services/Scripting/Functions/NetworkFunctions.cs` implementing `IFunctionCategory` with `is_valid_ip`, `ip_version`, `ip_in_cidr` (byte-mask membership; robust to non-canonical CIDR base)
- [x] 1.2 Add `url_host`/`url_port` via `System.Uri` (combined `parse_url` object omitted — the variable resolver has no member access, so scalar accessors fit the engine)
- [x] 1.3 Register `NetworkFunctions` in `FunctionRegistry`
- [~] 1.4 Autocomplete entries — N/A: built-in functions are not surfaced by `ScriptAutocompleteProvider` in this codebase (it completes commands/keys/enum values, not function names). No function-autocomplete subsystem exists to extend; adding one is out of scope.
- [x] 1.5 Unit tests: valid/invalid IPv4 + IPv6, CIDR boundary + non-canonical base, malformed inputs, URL host/port (`NetworkFunctionTests.cs`)

## 2. Date/time functions
- [x] 2.1 Add `now_utc()` and `now_local()`; `now()` retained unchanged
- [x] 2.2 Extend `date_add` units with `w/week`, `mo/month`, `y/year`; `date_diff` with `w/week`
- [x] 2.3 Add `parse_date(input, format[, out_format])` for explicit-format parsing
- [~] 2.4 Autocomplete entries — N/A (see 1.4)
- [x] 2.5 Tests: calendar arithmetic (month clamp, year, week), week diff, UTC/local format, explicit parse (`DateTimeFunctionEnhancementTests.cs`)

## 3. Regex functions
- [x] 3.1 Add `regex_match`, `regex_match_all`, `regex_groups` to `StringFunctions`, reusing `StripDelimiters` + 5s timeout
- [~] 3.2 Autocomplete entries — N/A (see 1.4)
- [x] 3.3 Tests: group capture, default full-match, no-match, invalid pattern, match-all, groups (`RegexFunctionTests.cs`)

## 4. Extract named capture
- [x] 4.1 In `ExtractCommand`, expose named groups via `Group.Name` (numeric/positional groups skipped, so positional behavior is byte-for-byte unchanged)
- [x] 4.2 Tests: named-group extraction + positional-preserved regression (`ExtractCommandTests.cs`)

## 5. Verification
- [x] 5.1 `dotnet build` — succeeds (built as part of the test run)
- [x] 5.2 `dotnet test` — 28/28 new tests pass; full suite 2296 passing. The single failure (`ScriptParserTests.Parse_ReadfilePathOnlyWithoutAutoBrowse_LeavesAutoBrowseUnset`) is pre-existing (fails on baseline with these changes stashed) and unrelated — it belongs to the prior `add-readfile-path-capture` change.
- [x] 5.3 `openspec validate add-scripting-helper-functions --strict --no-interactive` — valid

## Follow-ups (not in original scope)
- [ ] Document the new functions in `SCRIPTING.md` (discoverability) — offered, pending approval
- [ ] Investigate/fix the pre-existing `Parse_ReadfilePathOnlyWithoutAutoBrowse_LeavesAutoBrowseUnset` failure (belongs to readfile capability, not this change)
