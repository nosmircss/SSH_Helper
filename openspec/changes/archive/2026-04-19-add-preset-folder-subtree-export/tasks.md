# Tasks: Add preset folder subtree export

## 1. Export model and persistence
- [x] 1.1 Add preset-manager support for exporting a selected folder subtree to JSON
- [x] 1.2 Rebase nested folder paths so the selected folder becomes the export bundle root
- [x] 1.3 Preserve compatibility with the existing JSON import shape

## 2. UI behavior
- [x] 2.1 Add a folder-only `Export Folder...` entry to the preset-tree context menu
- [x] 2.2 Show a save dialog that defaults to a JSON filename based on the selected folder name
- [x] 2.3 Route the form export action through the preset manager and show success/error feedback

## 3. Verification
- [x] 3.1 Add focused service tests for subtree filtering and nested-path rebasing
- [x] 3.2 Add WinForms coverage for context-menu visibility and folder export invocation
- [x] 3.3 Run targeted `dotnet test` coverage for the touched services/UI
- [x] 3.4 Run `dotnet build SSH_Helper.csproj`
- [x] 3.5 Run `openspec validate add-preset-folder-subtree-export --strict --no-interactive`
