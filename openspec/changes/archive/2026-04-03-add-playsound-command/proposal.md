# Change: Add playsound Command for Local Audio Playback

## Why
Scripts currently cannot emit audible cues for operator attention or workflow checkpoints. Teams need a first-class command to play local sound files (including MP3) without shelling out to external tools.

## What Changes
- Add a new `playsound` scripting command for local audio playback.
- Support local file path resolution via script variable substitution and Windows environment variable expansion.
- Support WAV and MP3 playback using an in-process backend.
- Add configurable playback options: `wait`, `volume`, `max_seconds`, `into`, and `on_error`.
- Add parser/validation support for command shape, required keys, and option constraints.
- Add Flow Canvas support for import/export and visual block editing.
- Update scripting documentation with command syntax and examples.

## Impact
- Affected specs:
  - `scripting-runtime`
  - `scripting-validation`
- Affected code:
  - `SSH_Helper.csproj`
  - `Services/Scripting/Models/ScriptStep.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Scripting/ScriptExecutor.cs`
  - `Services/Scripting/Commands/PlaySoundCommand.cs` (new)
  - `Services/FlowCanvasBridge.cs`
  - `FlowCanvas/src/blockDefs/registry.ts`
  - `SCRIPTING.md`
  - `SSH_Helper.Tests/...` (scripting/parser/editor/bridge tests)

## Notes
- Scope is local playback only (no remote SSH-side playback).
- This change avoids interactive prompts so it remains safe in scheduled/background runs.
