# Tasks: Add multi-protocol workflow steps

## 1. Step model and parser
- [ ] 1.1 Add `StepType` entries for `Http`, `Ping`, `Dns`, `Portcheck`, and `Sftp`
- [ ] 1.2 Add options models and properties in `ScriptStep.cs`
- [ ] 1.3 Update `ScriptParser.cs` known keys and parse paths
- [ ] 1.4 Add validation rules for required fields on each new step type

## 2. Command implementations
- [ ] 2.1 Implement `HttpCommand.cs` with method/auth/headers/body/timeout support
- [ ] 2.2 Implement `PingCommand.cs` with count/timeout metrics capture
- [ ] 2.3 Implement `DnsCommand.cs` for A/AAAA/PTR lookup
- [ ] 2.4 Implement `PortcheckCommand.cs` for TCP open/closed/timeout checks
- [ ] 2.5 Implement `SftpCommand.cs` for upload/download over Rebex

## 3. Runtime integration
- [ ] 3.1 Register all new commands in `ScriptExecutor.cs`
- [ ] 3.2 Standardize `into` capture variables and derived fields
- [ ] 3.3 Preserve and regression-test existing `webhook` behavior

## 4. Verification
- [ ] 4.1 Add parser validation tests for each new step syntax
- [ ] 4.2 Add command unit tests for success/failure paths
- [ ] 4.3 Add mixed workflow integration test across SSH and non-SSH steps

## 5. Documentation
- [ ] 5.1 Document new YAML syntax and examples in `SCRIPTING.md`
- [ ] 5.2 Mark `webhook` as supported legacy option with `http` as preferred command
