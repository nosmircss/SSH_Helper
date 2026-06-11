# Subsystem map: Built-in function library (Services/Scripting/Functions)

Audit date: 2026-06-10. All paths relative to repo root `C:\Users\nos\source\repos\nosmircss\Test\SSH_Helper`.

## Architecture overview — THREE dispatch layers, not one

The "function library" is larger than the 8 `IFunctionCategory` classes. There are three
distinct dispatch layers, and only the first is discoverable via the registry:

1. **FunctionRegistry singleton** (`Services/Scripting/FunctionRegistry.cs:13-23`) —
   case-insensitive `Dictionary<string, ScriptFunction>` (`:25`); 8 categories registered at
   `:92-102` (String, Math, Collection, Type, DateTime, Encoding, Network, Vault) = **71 functions**.
   Delegate shape: `object? ScriptFunction(string argsString, ScriptContext context)`
   (`Services/Scripting/Functions/IFunctionCategory.cs:9`) — every handler receives the RAW
   unsplit argument string and parses it itself (almost always via
   `JsonUtilities.SplitTopLevelCommas`).
2. **Legacy hardcoded switch** in `JsonUtilities.TryEvaluateFunctionExpression`
   (`Services/Scripting/JsonUtilities.cs:336-565`) — **20 functions** that predate the registry:
   `length, list, trim, upper, lower, replace, split, join, substring, sort, compact, trim_all,
   lower_all, upper_all, distinct, push_unique, first, last, indexof, concat`.
   The registry is consulted first (`JsonUtilities.cs:343-345`), so a future registry
   registration silently shadows a legacy name.
3. **`json.*` family** dispatched by `TryEvaluateJsonExpression` (`JsonUtilities.cs:671-770`)
   to the static `JsonFunctions` class (`Services/Scripting/Commands/JsonFunctions.cs` — lives in
   Commands/ but is NOT a command): `json()` constructor (`:19`) plus 20 methods —
   `get(:77), set(:101), delete(:136), merge(:160), format(:180), exists(:222), len(:243),
   type(:295), keys(:342), values(:361), items(:382), push(:415), pop(:445), last(:468),
   unshift(:480), shift(:515), first(:537), slice(:550), concat(:596), indexof(:619)`.

**Total callable surface ≈ 112 functions.** `FunctionRegistry.Count`/`RegisteredNames`
(`FunctionRegistry.cs:85-90`) only see 71 of them.

### Call sites (how a script reaches a function)

- `ExpressionParser.ResolveFunction` (`Services/Scripting/ExpressionParser.cs:422-437`) —
  used by conditions (`if`/`while`/`assert` via `ExpressionEvaluator`) and lambda bodies.
  Tries registry → legacy switch → json.* → **throws** `FormatException("Unknown function: …")` (`:436`).
- `JsonUtilities.ResolveJsonValue` (`JsonUtilities.cs:307-315`) — value-position resolution;
  unknown function **silently falls through** to variable lookup / literal parsing (`:317-333`).
- `ValueResolver` (`Services/Scripting/ValueResolver.cs:260-264`) and
  `SetCommand` (`Services/Scripting/Commands/SetCommand.cs:178-181`) — `set:` expressions.
- Function-call syntax parsing: `JsonUtilities.TryParseFunctionCall` (`JsonUtilities.cs:567-629`)
  — name must be `[A-Za-z0-9_.]`, whole expression must be one call (`name(...)` ending at `)`).

## Feature inventory

### StringFunctions (`Services/Scripting/Functions/StringFunctions.cs`, 16 functions)

| Function | Signature | Behavior / notes |
|---|---|---|
| `contains(s, sub)` | →bool | `OrdinalIgnoreCase`, hardcoded (`:48`); <2 args → `false` |
| `startswith(s, prefix)` | →bool | ignore-case (`:58`) |
| `endswith(s, suffix)` | →bool | ignore-case (`:68`) |
| `pad_left(s, width[, padChar])` | →string | default pad `" "`; non-int width returns source unchanged (`:77-78`); only first char of padChar used |
| `pad_right(s, width[, padChar])` | →string | mirror of pad_left (`:86-99`) |
| `repeat(s, count)` | →string | negative→source; **silent cap at 10000** (`:111`) |
| `reverse(x)` | →string\|list | type-preserving: `List<string>` var reversed as list (`:120-127`); `List<object>` stringified (`:131-136`); else char-reverse |
| `regex_replace(s, pattern, repl)` | →string | pattern delimiters `/…/ '…' "…"` stripped (`:415-427`); 5s timeout; timeout/bad-pattern → returns source silently (`:169-176`) |
| `regex_match(s, pattern[, group])` | →string | group 0 default; no-match/bad-pattern → `""` (`:197-207`) |
| `regex_match_all(s, pattern)` | →List<string> | all full-match values; errors → empty list (`:224-225`) |
| `regex_groups(s, pattern)` | →List<string> | groups 1..N of first match (`:244-245`) |
| `format(template, args…)` | →string | `string.Format` InvariantCulture; `FormatException` → returns raw template (`:268-271`) |
| `char_at(s, i)` | →string\|null | out-of-range → null (`:283`) |
| `index_of(s, sub)` | →int | ignore-case, −1 if absent (`:294`); **distinct from legacy list `indexof`** |
| `random_string([len[, charset]])` | →string | default len 16, max 4096 clamp (`:16-17,309`); crypto RNG (`:327`); charset accepts `[a-z0-9]` bracket-range spec (`:341-371`) |
| `uuid()` | →string | `Guid.NewGuid()` "D" format (`:338`) |

### MathFunctions (`Services/Scripting/Functions/MathFunctions.cs`, 11 functions)

| Function | Signature | Behavior / notes |
|---|---|---|
| `abs(n)` | →int\|double | int-collapse when whole (`IsInteger`, `:217-222`) |
| `min(a, b, …)` / `max(a, b, …)` | →int\|double | **scalar varargs only — does NOT accept a list** (`:37-71`); non-numeric args skipped silently |
| `round(n[, decimals])` | →int\|double | `AwayFromZero`, decimals clamped 0–15 (`:88`) |
| `floor(n)` / `ceil(n)` | →int | (`:92-104`) |
| `random([min][, max])` | →int | defaults 0–100 inclusive (`:110,130`); shared `Random` under lock (`:12,128`) — NOT crypto |
| `pow(b, e)` / `sqrt(n)` | →int\|double | (`:135-154`) |
| `clamp(v, min, max)` | →int\|double | (`:156-168`) |
| `iif(cond, then, else)` | →any | YAML-safe ternary; condition via `ExpressionEvaluator`; **bare `catch` falls back to truthiness** (`:184-193`) so condition typos silently degrade |

No `mod`, `sum`, `avg`, `log`, `exp`, `trunc`, or list-aggregate forms.

### CollectionFunctions (`Services/Scripting/Functions/CollectionFunctions.cs`, 11 functions; lambdas via `Services/Scripting/LambdaExpression.cs`)

| Function | Signature | Behavior / notes |
|---|---|---|
| `map(list, x => expr)` | →List<string> | **results stringified** (`:34-40`) — numeric typing lost |
| `filter(list, x => cond)` | →List<string> | truthiness via `ValueResolver.IsTruthyValue` (`:52`) |
| `reduce(list, (acc,x) => expr, init)` | →object | only function preserving accumulator type (`:58-78`); lambda must have exactly 2 params (`:67`) |
| `find(list, x => cond)` | →string\|null | first match (`:80-92`) |
| `any(list, x => cond)` / `all(list, x => cond)` | →bool | (`:94-120`) |
| `count(list[, x => cond])` | →int | **unparseable lambda silently degrades to plain count** (`:130-131`) |
| `range(start, end[, step])` | →List<string> | end-exclusive; step 0 → null; **silent 100000-item cap** (`:158`) |
| `slice(list, start[, end])` | →List<string> | negative indices supported (`:183,189`) |
| `flatten(listOfLists)` | →List<string> | **one level only** (`:196-241`); items parsed as JSON arrays opportunistically |
| `zip(l1, l2)` | →List<string> | pairs encoded as JSON-array **strings** with hand-rolled escaping (`:256,299-302`); consuming a pair requires manual json parsing |
| (helpers) | | `SplitCollectionAndLambda` (`:266-278`); list coercion via `ValueResolver.ResolveListValue` |

Lambda mechanics (`LambdaExpression.cs`): `x => body` or `(acc, x) => body`; top-level `=>`
finder is quote/paren-aware (`:92-124`); evaluation **temporarily sets parameters as context
variables** with save/restore of collisions (`:60-90`); body evaluated by `ExpressionParser`.

### TypeFunctions (`Services/Scripting/Functions/TypeFunctions.cs`, 9 functions)

| Function | Behavior / notes |
|---|---|
| `int(x)` | bools→1/0, "true"/"false" handled, parse-fail → **0 silently** (`:27-46`); doubles truncated |
| `float(x)` | parse-fail → 0.0 (`:48-62`) |
| `str(x)` | JsonNode/JsonElement serialized to JSON text (`:68-69`) |
| `bool(x)` | truthiness (`:73-77`) |
| `typeof(x)` | →"null"\|"bool"\|"number"\|"list"\|"json"\|"string"; **infers type from string content** — `"42"` reports "number" (`:111,175-185`) |
| `is_number(x)` | numeric type or parseable string (`:116-123`) |
| `is_list(x)` / `is_json(x)` | raw-variable aware; string forms validated by full `JsonDocument.Parse` (`:187-198`) |
| `is_empty(x)` | `ValueResolver.IsEmptyValue` (`:149-153`) |

### DateTimeFunctions (`Services/Scripting/Functions/DateTimeFunctions.cs`, 9 functions)

| Function | Behavior / notes |
|---|---|
| `now([fmt])` / `now_local([fmt])` | **identical implementations** — both local time (`:37-45`); default fmt `yyyy-MM-dd HH:mm:ss` |
| `now_utc([fmt])` | UTC (`:47-50`) |
| `epoch()` | unix seconds of NOW; **args ignored** (`:52-55`) — no date→epoch conversion exists |
| `epoch_to_date(epoch[, fmt])` | seconds only (no ms detection), converts to **LocalDateTime** (`:66`) |
| `date_add(ts, amount, unit)` | units s/m/h/d/w/mo/y (months/years truncate to int, `:100-101`); unknown unit returns input unchanged (`:102`); **output format hardcoded** `yyyy-MM-dd HH:mm:ss` (`:105`) |
| `date_diff(a, b, unit)` | a−b; units up to weeks — **no months/years** (unknown → seconds, `:122-130`); always rounded to 2 decimals (`:132`) |
| `date_format(ts, fmt)` | reformat; parse-fail → null (`:135-146`) |
| `parse_date(input, fmt[, outFmt])` | exact-format parse → normalized string (`:148-171`) |
| (parsing) | fixed `ParseFormats` list tries `MM/dd/yyyy` BEFORE `dd/MM/yyyy` (`:11-22`) — `03/04/2025` always parses US-style; falls back to `DateTime.TryParse` invariant (`:185-191`) |

### EncodingFunctions (`Services/Scripting/Functions/EncodingFunctions.cs`, 7 functions)

| Function | Behavior / notes |
|---|---|
| `base64_encode(s)` / `base64_decode(s)` | UTF-8; decode failure → null (`:36-39`) |
| `url_encode(s)` / `url_decode(s)` | `Uri.EscapeDataString`/`UnescapeDataString` (`:42-52`) |
| `hash(s[, algo])` | MD5/SHA1/SHA256/SHA384/SHA512, lowercase hex; **unknown algo silently falls back to SHA256** (`:113`); null-check at `:67` is dead code |
| `hex_encode(s)` / `hex_decode(s)` | UTF-8↔lowercase hex; **odd-length hex input silently drops the last nibble** (`:86` integer division); non-hex → null |

### NetworkFunctions (`Services/Scripting/Functions/NetworkFunctions.cs`, 5 functions — pure, no I/O by design `:10`)

| Function | Behavior / notes |
|---|---|
| `is_valid_ip(s)` | `IPAddress.TryParse` (`:23-26`) — note this accepts some non-dotted forms ("1" parses as 0.0.0.1) |
| `ip_version(s)` | 4 or 6; invalid → `""` (`:28-34`) |
| `ip_in_cidr(ip, "net/prefix")` | manual byte/bit mask compare, v4+v6, family-mismatch → false (`:36-77`) |
| `url_host(u)` / `url_port(u)` | absolute URIs only; **`url_port` returns the scheme default (80/443/…) when no explicit port** — can't distinguish (`:86-92`) |

### VaultFunctions (`Services/Scripting/Functions/VaultFunctions.cs`, 3 functions)

| Function | Behavior / notes |
|---|---|
| `vault(path, key[, profile])` | reads a secret; default profile via `VaultService.ResolveDefaultProfileName(context.EnvironmentVaultProfile)` (`:29`); **sync-over-async `.GetAwaiter().GetResult()`** (`:36`); **`VaultException` swallowed → null** (`:38-41`) |
| `vault_list(prefix[, profile])` | lists secret keys; same blocking + swallow pattern (`:63-68`) |
| `vault_clear_cache()` | clears the VaultService cache; no service → false (`:71-78`) |

Requires `context.VaultService` (set by the executor host; null → null result, `:18-19`).
NOTE: a **second, independent** inline vault-substitution path exists in
`Services/Scripting/ScriptContext.cs:709-748` (also blocking, also defaulting via
`EnvironmentVaultProfile`) — two parallel code paths for vault access.

### Legacy switch functions (`Services/Scripting/JsonUtilities.cs:347-564`, 20 functions)

`length(x)` (`:349`, via `ValueResolver.ResolveLength` `ValueResolver.cs:24` — list count/JSON
count/string length), `list(a,b,…)` (`:355`, builds List<string>), `trim/upper/lower` (`:367-384`),
`replace(s, old, new)` (`:385-396`, **Ordinal case-SENSITIVE**), `split(s[, delim])` (`:397-416`,
default `,`; empty delim → char split), `join(list[, delim])` (`:417-427`),
`substring(s, start[, len])` (`:428-466`, clamping, never throws), `sort(list[, "asc"|"desc"])`
(`:467-480`, **always OrdinalIgnoreCase string sort — numeric lists sort lexicographically**),
`compact(list)` (`:481-486`, drops blank entries), `trim_all/lower_all/upper_all` (`:487-504`),
`distinct(list[, comparer])` (`:505-515`, order-preserving; comparer modes `ordinal`/`ignore_case`
via `ValueResolver.ResolveComparisonComparer` `ValueResolver.cs:370`), `push_unique(list, item[,
comparer])` (`:516-529`, mutates+returns), `first/last(list)` (`:530-541`), `indexof(list, item)`
(`:542-552`, ignore-case — **collides conceptually with registry `index_of` for strings**),
`concat(lists…)` (`:553-561`).

Also: a property-style `.length` suffix on variables is special-cased in
`ValueResolver.TryResolveLengthExpression` (`ValueResolver.cs:400-409`) and in
`ExpressionParser.ResolveTokenValue` (`ExpressionParser.cs:454-456`).

### json.* family (`Services/Scripting/Commands/JsonFunctions.cs` via `JsonUtilities.cs:702-770`)

Constructor `json(k, v, …)` plus get/set/delete/merge/format/exists/len/type/keys/values/items/
push/pop/last/unshift/shift/first/slice/concat/indexof — structured JSON manipulation with
path navigation (`JsonPathNavigator.cs`). Results optionally normalized back to `JsonElement`
(`JsonUtilities.cs:772-797`). Documented in SCRIPTING.md (~line 630+). Not inventoried
function-by-function here (it is its own sub-area), but it overlaps names with the legacy list
functions (`first`, `last`, `slice`, `concat`, `indexof`, `format`, `len` vs `length`).

## Integration points

- **Expression evaluation**: `ExpressionEvaluator`/`ExpressionParser` call into the registry for
  every function used in `if:`/`while:`/`assert:` conditions and lambda bodies
  (`ExpressionParser.cs:425`). Numbers are coerced to `double` in expression context
  (`ExpressionParser.cs:462-474`).
- **`set:` command**: `SetCommand.cs:178-181` resolves function expressions for variable
  assignment — the primary user entry point (`- set: x = upper(host)`).
- **ScriptContext**: functions read/write variables through `context.GetVariable`/`SetVariable`;
  lambdas temporarily shadow variables (`LambdaExpression.cs:66-89`). Vault functions depend on
  `context.VaultService` + `context.EnvironmentVaultProfile` (`ScriptContext.cs:367-372`),
  injected per-run by the executor host (Form1 wiring).
- **Vault subsystem**: `Services/Vault/VaultService` (`ReadSecretAsync`, `ListSecretsAsync`,
  `ClearCache`, `ResolveDefaultProfileName`) — shared with the credential system and the inline
  `ScriptContext` vault substitution.
- **Docs**: SCRIPTING.md documents the function surface extensively (string section ~line 975+,
  type conversion ~1090, datetime ~1139, encoding ~1168). Hand-maintained — the registry carries
  no metadata (name→delegate only), so docs/registry can drift silently.
- **OpenSpec**: `openspec/specs/scripting-expressions/spec.md` plus archived changes
  (`add-scripting-helper-functions`, `2026-03-13-update-scripting-collection-ergonomics`) define
  intended behavior — useful cross-check for downstream auditors.
- **Tests** (good coverage; one file per category):
  `SSH_Helper.Tests/Scripting/{String,Math,Collection,Type,DateTime,Encoding,Network}FunctionTests.cs`,
  `DateTimeFunctionEnhancementTests.cs`, `RegexFunctionTests.cs`, `VaultFunctionsTests.cs`,
  `FunctionRegistryTests.cs`, `ExpressionParserTests.cs`, `ExpressionEvaluatorTests.cs`.

## Observed gaps & quirks

### Architectural
1. **Split-brain registration** — 20 legacy functions live in a hardcoded switch
   (`JsonUtilities.cs:347-564`) outside the registry; `json.*` is a third path
   (`JsonUtilities.cs:702-770`). `FunctionRegistry.RegisteredNames` under-reports the surface by
   ~40%, so any tooling built on it (autocomplete, docs generation, validation) is born wrong.
   Registry-first dispatch (`JsonUtilities.cs:344`) means a future registry name silently shadows
   a legacy one.
2. **No editor support for functions** — `Services/Editor/ScriptAutocompleteProvider.cs` (1246
   lines) contains zero function names and never touches `FunctionRegistry`; Flow Canvas
   (`FlowCanvas/src/`) likewise. ~112 functions must be memorized or looked up in SCRIPTING.md.
3. **No static validation of function calls** — `ScriptParser` validation does not check function
   names/arity. Failure mode is inconsistent at runtime: condition context throws
   `FormatException: Unknown function` (`ExpressionParser.cs:436`); value context silently falls
   through so a typo like `uper(x)` resolves to the literal string `uper(x)`
   (`JsonUtilities.cs:312-333`).
4. **Pervasive silent-failure philosophy** — wrong arg counts/types return null/false/empty/the
   original input instead of erroring: `pad_left` bad width → source (`StringFunctions.cs:77`),
   `format` → raw template (`:268-271`), `regex_replace` bad pattern → source (`:173-176`),
   `int()` parse-fail → 0 (`TypeFunctions.cs:45`), `date_add` unknown unit → input (`DateTimeFunctions.cs:102`),
   `count` bad lambda → plain count (`CollectionFunctions.cs:130`), `iif` bare catch
   (`MathFunctions.cs:188-193`). Hard to debug in long multi-host runs; no warning channel exists.

### Correctness / surprise behavior
5. **Case-sensitivity is inconsistent and non-configurable** — `contains/startswith/endswith/
   index_of` are hardcoded ignore-case (`StringFunctions.cs:48,58,68,294`); legacy `replace` is
   case-SENSITIVE Ordinal (`JsonUtilities.cs:394`); `sort` always ignore-case (`:474`); only
   `distinct`/`push_unique` expose a comparer arg (`:512,524`). There is no case-sensitive
   `contains` at all.
6. **`sort` is string-only** — numeric lists order as `["1","10","2"]` (`JsonUtilities.cs:467-480`);
   no numeric sort, no `sort_by` lambda.
7. **`hash()` silently substitutes SHA256** for unrecognized algorithm names
   (`EncodingFunctions.cs:113`); a user asking for e.g. `"crc32"` gets a SHA256 digest with no
   error. Dead null-check at `:67`.
8. **`hex_decode` truncates odd-length input** silently (`EncodingFunctions.cs:86`).
9. **Date parsing ambiguity** — fixed format list tries `MM/dd/yyyy` before `dd/MM/yyyy`
   (`DateTimeFunctions.cs:11-22`); `date_add` discards the input's format/precision
   (`:105`); `epoch_to_date` converts to local time with no TZ option (`:66`).
10. **Vault error swallowing** — `VaultException` → null (`VaultFunctions.cs:38-41,65-68`): a
    Vault outage is indistinguishable from a missing key; a script could proceed to send an empty
    password to hosts. Also blocking sync-over-async on the executor thread (`:36,63`), duplicated
    by the second inline vault path in `ScriptContext.cs:709-748`.
11. **Type erosion in collection pipeline** — `map` stringifies (`CollectionFunctions.cs:38`),
    `zip` emits JSON-string pairs with hand-rolled escaping (`:256,299-302`); only `reduce`
    preserves object types. Chained numeric processing requires repeated `float()` casts
    (documented workaround at SCRIPTING.md:1078).
12. **Silent caps** — `repeat` 10000 (`StringFunctions.cs:111`), `range` 100000
    (`CollectionFunctions.cs:158`), `random_string` 4096 (`StringFunctions.cs:309`) — all truncate
    without any signal.
13. **`typeof` infers from string content** — a variable holding the string `"42"` reports
    "number" (`TypeFunctions.cs:111,175-185`); consistent with the stringly engine but surprising.
14. **`url_port` fills scheme defaults** (`NetworkFunctions.cs:86-92`) — explicit vs default port
    indistinguishable; `is_valid_ip` accepts integer shorthand forms (`IPAddress.TryParse`
    semantics).

### Naming / discoverability
15. **Confusing near-duplicates** — `index_of` (string, registry) vs `indexof` (list, legacy) vs
    `json.indexof`; `first/last/slice/concat` exist both as legacy list functions and `json.*`
    variants; `format` (string.Format) vs `json.format`; `length` vs `count` vs `json.len`;
    `now` vs `now_local` are byte-identical duplicates (`DateTimeFunctions.cs:37-45`).

### Missing functions a power user of a multi-host SSH tool would expect
16. **String**: `trim_start`/`trim_end`, `capitalize`/`title_case`, `strip_ansi` (the docs
    recommend a raw regex_replace incantation instead, SCRIPTING.md ~1006), `truncate`,
    `lines()` helper, case-sensitive variants of search functions, `regex_match` named groups.
17. **Math/aggregation**: `sum`, `avg`, `median`, `min/max over a list` (current `min`/`max` are
    scalar varargs only, `MathFunctions.cs:37-71`), `mod`, `percent`.
18. **Date**: date→epoch (the inverse of `epoch_to_date` does not exist; `epoch()` ignores args,
    `DateTimeFunctions.cs:52-55`), timezone conversion, `date_diff` months/years units,
    ISO-8601 duration handling.
19. **Collection**: `sort_by`, `group_by`, `chunk`, `sum`, deep `flatten` (current is one level,
    `CollectionFunctions.cs:196-241`), set ops (`intersect`/`except`/`union`).
20. **Encoding**: HMAC (relevant for webhook signing), base64url, file hashing.
21. **Network**: CIDR expansion/network/broadcast/netmask helpers, hostname/FQDN validation,
    MAC validation/normalization, `ip_add`/range iteration — natural fits for this tool's domain.

### Minor
22. `FunctionRegistry` is mutable post-init with a plain Dictionary (`FunctionRegistry.cs:25,37`);
    safe today (registration happens once in the Lazy factory) but unguarded if runtime
    registration is ever added.
23. `MathFunctions._random` is a shared seeded-by-time `Random` under lock
    (`MathFunctions.cs:12,128`) — fine for jitter, not for secrets (contrast: `random_string`
    correctly uses `RandomNumberGenerator`, `StringFunctions.cs:327`).
24. Registry carries no metadata (arity, description, category) — blocks any future
    auto-generated docs, signature help, or validation without restructuring.
