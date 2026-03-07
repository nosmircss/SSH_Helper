# Change: Add script-declared environment switching on preset load

## Why
Some presets are authored for a specific environment, but loading the preset today leaves the active environment unchanged. That creates mismatches between the script being edited or run and the host grid/environment variables currently in scope. Once a preset does switch environments, a later preset without an explicit environment should return the operator to their chosen baseline instead of leaving the active environment stranded.

## What Changes
- Add an optional top-level YAML `environment` key
- Persist a separate base environment that follows manual environment changes
- Switch to the named environment when a YAML script preset is loaded into the editor, if that environment exists, without changing the base environment
- Restore the active environment back to the base environment when a later preset is loaded without a top-level `environment`
- Keep the current environment and show a non-blocking status message when the named environment is missing
- Show the base environment in the toolbar only while active and base environments differ
- Consolidate preset load behavior so all preset-load paths use the same script metadata handling

## Impact
- Affected specs:
  - `environment-management`
  - `scripting-validation`
- Affected code:
  - `Models/AppConfiguration.cs`
  - `Services/ConfigurationService.cs`
  - `Services/EnvironmentService.cs`
  - `Services/Scripting/Models/Script.cs`
  - `Services/Scripting/ScriptParser.cs`
  - `Services/Editor/ScriptAutocompleteProvider.cs`
  - `Form1.cs`
  - `SCRIPTING.md`
