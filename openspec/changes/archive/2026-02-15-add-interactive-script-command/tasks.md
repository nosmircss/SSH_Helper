# Tasks: Add interactive script command

## 1. Models and parser
- [x] 1.1 Add `StepType.Interactive`, `InteractiveOptions`, `InteractiveSessionMode`, and `InteractiveEmulationMode`
- [x] 1.2 Parse `interactive` as map-only and apply defaults (`session=separate`, `emulation=full`)
- [x] 1.3 Validate invalid enum values and unknown keys under `interactive`

## 2. Runtime behavior
- [x] 2.1 Wire `interactive` in `ScriptExecutor` and add `InteractiveCommand`
- [x] 2.2 Add in-app terminal form/service for separate/shared full mode with ANSI color rendering
- [x] 2.3 Block script progress until terminal closes; treat close as step success
- [x] 2.4 Cancel/stop closes the terminal session and cancels script execution

## 3. Execution orchestration guards
- [x] 3.1 Add dependency analysis flagging for interactive usage
- [x] 3.2 Reject interactive scripts in multi-host runs before execution
- [x] 3.3 Reject interactive scripts in folder runs before execution

## 4. Editor and validation integration
- [x] 4.1 Add parser metadata so autocomplete/highlighting includes `interactive`, `session`, `emulation`
- [x] 4.2 Add parser/validation tests for map-only shape, defaults, and invalid values

## 5. Docs and QA artifacts
- [x] 5.1 Document `interactive` syntax and behavior in `SCRIPTING.md`
- [x] 5.2 Add `interactive` coverage presets in `qa_presets.json`
