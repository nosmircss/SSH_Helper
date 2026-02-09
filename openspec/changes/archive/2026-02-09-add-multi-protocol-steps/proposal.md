# Change: Add multi-protocol workflow steps

## Why
The scripting engine is SSH-centric today, which limits workflows that need API checks, network reachability probes, DNS validation, and file transfer in a single run.

## What Changes
- Add first-class `http`, `ping`, `dns`, `portcheck`, and `sftp` scripting steps
- Extend script model, parser, validation, dependency analysis, and runtime registration for all new step types
- Lock option defaults and accepted values for deterministic behavior:
  - `http`: `method=GET`, `timeout=30`, `follow_redirects=true`, `allow_failure=false`, `verify_tls=true`, `auth in {none,basic,bearer}`, `content_type in {json,form,text,xml}`
  - `ping`: `count=4`, `timeout=3000` ms
  - `dns`: `type=A`, `timeout=10` s, `type in {A,AAAA,PTR}`
  - `portcheck`: `port=22`, `timeout=5` s
  - `sftp`: `overwrite=true`, `timeout=120` s, `action in {upload,download}`
- Define normalized capture conventions (`into`, derived status/metrics fields), including DNS list capture as `List<string>` to support `${var[index]}` access
- Define HTTP failure semantics explicitly:
  - `allow_failure` only controls non-2xx HTTP responses
  - Transport/runtime failures (timeout, connection, TLS, DNS resolution) follow step-level `on_error` behavior
- Define deterministic capture lifecycle semantics so `into` values are updated per execution and do not leak stale data after failures
- Define HTTP certificate validation behavior explicitly:
  - TLS certificate validation is enabled by default
  - Validation can be disabled per-step for controlled test/lab scenarios via an explicit opt-out flag
- Define common usability contracts for basic authoring:
  - String option fields across all new steps support runtime variable substitution
  - Enum-like options are case-insensitive (`post` == `POST`, `bearer` == `BEARER`, etc.)
  - `http` `content_type` shorthand has fixed MIME mappings and explicit `Content-Type` header precedence
  - `dns` no-record responses return an empty list with count `0` (not an implicit parser/runtime error)
  - `sftp` with `overwrite: false` fails predictably when destination already exists
- Preserve existing `webhook` behavior for backward compatibility
- Add QA test presets in `qa_presets.json` that exercise the new step types and key edge cases
- Add new multi-protocol examples to `SCRIPTING.md` (including HTTP TLS verification examples)

## Impact
- Affected specs:
  - `scripting-network-steps` (new capability)
- Affected code:
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/Commands/HttpCommand.cs` (new)
  - `Services/Scripting/Commands/PingCommand.cs` (new)
  - `Services/Scripting/Commands/DnsCommand.cs` (new)
  - `Services/Scripting/Commands/PortcheckCommand.cs` (new)
  - `Services/Scripting/Commands/SftpCommand.cs` (new)
  - `Services/Scripting/ScriptDependencyAnalyzer.cs`
  - `SCRIPTING.md`
  - `qa_presets.json`
- Dependency check:
  - Verify Rebex SFTP API availability from current package references and add the package only if required by the chosen SFTP API surface
