## 1. Model and Utility Updates
- [x] 1.1 Update PresetInfo.Folder XML documentation to describe path support
- [x] 1.2 Create Utilities/FolderPathUtility.cs with path helper methods (GetParentPath, GetFolderName, GetPathSegments, GetAncestorPaths)

## 2. PresetManager Changes
- [x] 2.1 Update CreateFolder to auto-create parent folders for nested paths
- [x] 2.2 Update RenameFolder to cascade rename to all descendant folders and preset assignments
- [x] 2.3 Update DeleteFolder to handle subfolders (prompt for move-to-parent or recursive delete)
- [x] 2.4 Update MovePresetToFolder to validate that target path exists
- [x] 2.5 Add GetSubfolders(parentPath) method to return immediate children
- [x] 2.6 Add GetAllDescendantFolders(parentPath) method for cascade operations

## 3. UI - TreeView Rendering
- [x] 3.1 Update RefreshPresetList to build nested TreeView by parsing folder paths
- [x] 3.2 Update GetSortedFolders to return paths organized by hierarchy level
- [x] 3.3 Ensure folder expand/collapse state persists correctly for nested paths

## 4. UI - Drag and Drop
- [x] 4.1 Update drag-drop to allow dropping presets into nested folder nodes
- [x] 4.2 Update drag-drop to allow dropping folders into other folders (subfolder creation)
- [x] 4.3 Prevent dropping a folder into its own descendants (cycle prevention)

## 5. UI - Context Menus
- [x] 5.1 Update "Move to Folder" menu to show hierarchical folder picker
- [x] 5.2 Update "New Folder" to support creating as subfolder of selected folder
- [x] 5.3 Update "Rename Folder" to show full path being renamed
- [x] 5.4 Update "Delete Folder" confirmation to show descendant counts

## 6. Import/Export
- [x] 6.1 Verify Export includes full folder paths (already stores PresetInfo.Folder)
- [x] 6.2 Update Import to auto-create folder hierarchy from preset paths

## 7. Validation and Testing
- [x] 7.1 Test creating folder "A/B/C" - verify A and A/B are auto-created
- [x] 7.2 Test renaming "A" to "X" - verify A/B becomes X/B and presets update
- [x] 7.3 Test deleting folder with children - verify prompt and both options work
- [x] 7.4 Test drag-drop preset into nested folder
- [x] 7.5 Test drag-drop folder into another folder
- [x] 7.6 Test import/export round-trip preserves hierarchy
- [x] 7.7 Test existing single-level folders continue to work (backward compat)
