# Change: Add nested preset folders

## Why
Users need to organize large numbers of presets into hierarchical categories. The current single-level folder system becomes unwieldy with many presets spanning different network devices, environments, and purposes. Multi-level folders (e.g., "Network/Cisco/Switches") enable intuitive organization that mirrors how users think about their command presets.

## What Changes
- **BREAKING**: Folder paths change from flat names to forward-slash paths (e.g., "Cisco" becomes "Network/Cisco/Switches")
- PresetInfo.Folder stores full path instead of single name
- FolderInfo keyed by full path in PresetFolders dictionary
- TreeView renders nested hierarchy based on path parsing
- Folder operations (create, rename, delete, move) support path-based navigation
- Creating a nested folder auto-creates parent folders if they don't exist

## Impact
- Affected specs: New capability `preset-organization`
- Affected code:
  - `Models/PresetInfo.cs` - Folder property semantics (path instead of flat name)
  - `Models/FolderInfo.cs` - No model changes, but keyed by path
  - `Models/AppConfiguration.cs` - PresetFolders dictionary keyed by paths
  - `Services/PresetManager.cs` - All folder CRUD operations
  - `Form1.cs` - TreeView rendering, drag-drop, context menus
- Migration: Existing single-level folders remain valid (no slash = root-level folder)
