# Tasks: Add multi-protocol workflow steps

## 1. Step model and parser
- [x] 1.1 Add `StepType` entries for `Http`, `Ping`, `Dns`, `Portcheck`, and `Sftp`
- [x] 1.2 Add options models and properties in `ScriptStep.cs` with locked defaults (`http`: GET/30/follow_redirects=true/allow_failure=false/verify_tls=true, `ping`: count=4 timeout=3000, `dns`: type=A timeout=10, `portcheck`: port=22 timeout=5, `sftp`: overwrite=true timeout=120)
- [x] 1.3 Update `ScriptParser.cs` known keys and parse paths, including shorthand `ping: "host"` and mapping form support for all new steps
- [x] 1.4 Add validation rules with line context for missing required fields, invalid enums (`http` method/auth/content_type, `dns` type, `sftp` action), and invalid `verify_tls` value type
- [x] 1.5 Ensure step-level `on_error` parsing remains shared and valid for all new steps
- [x] 1.6 Ensure enum-like fields are accepted case-insensitively and normalized for execution

## 2. Command implementations
- [x] 2.1 Implement `HttpCommand.cs` with URL validation, method/auth/headers/body/content-type/timeout/redirect support and `verify_tls` handling
- [x] 2.2 Implement HTTP failure semantics: non-2xx obeys `allow_failure`; transport/runtime failures obey `on_error`
- [x] 2.3 Implement default TLS certificate validation and per-step opt-out (`verify_tls: false`) for controlled environments
- [x] 2.4 Implement `PingCommand.cs` with count/timeout metrics capture (`success`/`failure`, `{into}_avg`, `{into}_loss`)
- [x] 2.5 Implement `DnsCommand.cs` for A/AAAA/PTR lookup with DNS capture contract (`{into}` as `List<string>`, `{into}_count`)
- [x] 2.6 Implement `PortcheckCommand.cs` for TCP `open`/`closed`/`timeout` and optional `{into}_latency`
- [x] 2.7 Implement `SftpCommand.cs` for upload/download over Rebex with endpoint/credential overrides and `{into}_bytes` capture
- [x] 2.8 Verify Rebex package coverage for SFTP API and add package reference only if required
- [x] 2.9 Implement `http` content-type shorthand mappings and ensure explicit `Content-Type` header overrides shorthand
- [x] 2.10 Apply variable substitution for all string-valued fields in new step options before execution
- [x] 2.11 Implement deterministic `into` lifecycle on failure paths (no stale prior values)
- [x] 2.12 Implement DNS no-record semantics as empty result list with count `0`
- [x] 2.13 Implement SFTP `overwrite: false` failure behavior for existing destination paths

## 3. Runtime integration
- [x] 3.1 Register all new commands in `ScriptExecutor.cs`
- [x] 3.2 Standardize `into` capture variables and derived fields across all new steps (including HTTP headers and DNS count)
- [x] 3.3 Preserve and regression-test existing `webhook` behavior
- [x] 3.4 Update `ScriptDependencyAnalyzer.cs` for new step variable definitions/usages

## 4. Verification
- [x] 4.1 Add parser validation tests for each new step syntax
- [x] 4.2 Add parser tests that verify locked defaults are applied when optional fields are omitted
- [x] 4.3 Add command unit tests for success/failure paths including `allow_failure` vs `on_error` interaction
- [x] 4.4 Add DNS tests that verify `${into[index]}` access and `${into}_count` derivation
- [x] 4.5 Add webhook regression test proving unchanged behavior
- [x] 4.6 Add mixed workflow integration test across SSH and non-SSH steps
- [x] 4.7 Add HTTP TLS tests covering default cert validation and `verify_tls: false` behavior
- [x] 4.8 Add tests for case-insensitive enum inputs across `http`, `dns`, and `sftp`
- [x] 4.9 Add tests for content-type shorthand mapping and explicit header precedence
- [x] 4.10 Add tests ensuring failure paths do not leave stale `into` values
- [x] 4.11 Add DNS tests for no-record responses (empty list, count `0`)
- [x] 4.12 Add SFTP tests for `overwrite: false` when destination exists

## 5. Documentation
- [x] 5.1 Document new YAML syntax and examples in `SCRIPTING.md`
- [x] 5.2 Document locked defaults, accepted enum values, and capture variable contracts for each new step
- [x] 5.3 Mark `webhook` as supported legacy option with `http` as preferred command
- [x] 5.4 Add a dedicated section in `SCRIPTING.md` for HTTP TLS certificate behavior with secure-default guidance
- [x] 5.5 Document case-insensitive option handling, content-type shorthand mappings, and header precedence rules
- [x] 5.6 Document DNS no-record behavior and SFTP `overwrite: false` behavior

## 6. QA Presets
- [x] 6.1 Add QA presets in `qa_presets.json` for `http`, `ping`, `dns`, `portcheck`, and `sftp`
- [x] 6.2 Include preset coverage for `http` auth modes, non-2xx handling, and TLS verification toggle behavior
- [x] 6.3 Add one mixed-protocol QA preset that combines SSH and at least two new non-SSH step types
- [x] 6.4 Add QA preset checks for case-insensitive option values and non-stale `into` behavior

