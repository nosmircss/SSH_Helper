## 1. Runtime and Parser

- [x] 1.1 Add `path_only` and `path_into` to the `readfile` model, parser, and validation contract.
- [x] 1.2 Update `ReadFileCommand` so picker and path-based runs can capture the resolved absolute path, with `path_only` skipping file reads.

## 2. Authoring Surfaces

- [x] 2.1 Update script editor autocomplete for the new `readfile` options.
- [x] 2.2 Update Flow Canvas readfile authoring and import/export parity for the new options.

## 3. Tests and Docs

- [x] 3.1 Add focused readfile, validation/job-run, autocomplete, and Flow Canvas coverage for path capture behavior.
- [x] 3.2 Update `SCRIPTING.md` with path-only and read-plus-path examples, including a safe PowerShell `localcmd` usage pattern.
- [x] 3.3 Run focused verification and strict OpenSpec validation.