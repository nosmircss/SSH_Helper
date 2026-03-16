# Tasks: Add folder-level base environment inheritance

## 1. Persistence and metadata
- [x] 1.1 Extend folder metadata with an optional persisted base-environment override
- [x] 1.2 Normalize invalid folder base-environment values during preset-manager load
- [x] 1.3 Add preset-manager helpers for assigning, clearing, and repairing folder base-environment references

## 2. UI behavior
- [x] 2.1 Add a preset-folder context-menu submenu for assigning or clearing folder base environments
- [x] 2.2 Resolve the effective folder base environment from the nearest ancestor override or the global base
- [x] 2.3 Apply that effective base when loading presets and when selecting/running folders

## 3. Lifecycle repair
- [x] 3.1 Preserve folder base-environment metadata when folders are renamed
- [x] 3.2 Update folder base-environment references when environments are renamed
- [x] 3.3 Clear folder base-environment references when environments are deleted

## 4. Verification
- [x] 4.1 Add focused regression tests for folder base resolution and metadata persistence
- [x] 4.2 Run targeted `dotnet test` coverage for the touched services/utilities
- [x] 4.3 Run `dotnet build SSH_Helper.csproj`
- [x] 4.4 Run `openspec validate update-folder-base-environments --strict --no-interactive`
