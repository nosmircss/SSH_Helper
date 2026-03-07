# Tasks: Add script-declared environment switching on preset load

## 1. Script metadata
- [x] 1.1 Add `environment` to the parsed script model
- [x] 1.2 Parse `environment` as an optional top-level YAML scalar
- [x] 1.3 Keep metadata-only `environment:` text out of YAML auto-detection

## 2. Preset load behavior
- [x] 2.1 Introduce a shared preset-to-editor load helper in `Form1`
- [x] 2.2 Route all preset load paths through the shared helper
- [x] 2.3 Apply script-declared environment switching from the shared helper using the existing environment switch flow
- [x] 2.4 Show a non-blocking status message when the declared environment is unavailable

## 3. Documentation and metadata consumers
- [x] 3.1 Update parser-driven editor metadata consumers to include `environment`
- [x] 3.2 Document the new root option and behavior in `SCRIPTING.md`

## 4. Verification
- [x] 4.1 Add parser regression tests for `environment`
- [x] 4.2 Add editor metadata regression tests for autocomplete/highlighting
- [x] 4.3 Run focused tests
- [x] 4.4 Run `openspec validate update-script-load-environment --strict --no-interactive`

## 5. Base environment state and restore behavior
- [x] 5.1 Persist a separate base-environment value in configuration
- [x] 5.2 Keep base environment valid across switch, rename, delete, and import flows
- [x] 5.3 Preserve base on preset-declared environment switches and restore it on no-environment preset loads
- [x] 5.4 Show the base-environment toolbar indicator only while active and base differ

## 6. Regression coverage and docs
- [x] 6.1 Add focused environment-service regression tests for base-environment persistence and repair
- [x] 6.2 Add focused utility tests for preset-load decisions and base-indicator visibility
- [x] 6.3 Update `SCRIPTING.md` with base-environment rebase/restore behavior
- [x] 6.4 Re-run focused verification for the expanded change
