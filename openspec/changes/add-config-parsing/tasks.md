# Tasks: Add Configuration Parsing

## 1. Core Infrastructure
- [x] 1.1 Create `Services/Scripting/Parsers/` directory
- [x] 1.2 Create `IConfigParser.cs` interface
- [x] 1.3 Create `ParserFactory.cs` for parser registration/lookup
- [x] 1.4 Add `ParseOptions` class to `ScriptStep.cs`
- [x] 1.5 Add `StepType.Parse` enum value
- [x] 1.6 Update `ScriptParser.cs` to parse `parse:` YAML blocks

## 2. FortiGate Parser
- [x] 2.1 Create `FortiGateParser.cs` with state machine implementation
- [x] 2.2 Handle `config <section>` directive (push context)
- [x] 2.3 Handle `edit "name"` directive (push named entry)
- [x] 2.4 Handle `set key value(s)` directive (assign value)
- [x] 2.5 Handle `next` directive (pop edit context)
- [x] 2.6 Handle `end` directive (pop config context)
- [x] 2.7 Handle nested config blocks
- [x] 2.8 Handle quoted strings and multi-value sets
- [x] 2.9 Handle `unset` directive (omit from output)

## 3. Parse Command
- [x] 3.1 Create `ParseCommand.cs` implementing `IScriptCommand`
- [x] 3.2 Register in `ScriptExecutor._commands` dictionary
- [x] 3.3 Validate format, from, into parameters
- [x] 3.4 Report parsing errors with context

## 4. Testing
- [x] 4.1 Unit tests for FortiGateParser with sample configs
- [x] 4.2 Test nested config blocks
- [x] 4.3 Test edge cases (empty sections, special characters)
- [x] 4.4 Integration test with script executor

## 5. Documentation
- [x] 5.1 Add parse command to scripting documentation
- [x] 5.2 Add FortiGate parsing examples
