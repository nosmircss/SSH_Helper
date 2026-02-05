## Context
Adding unlimited folder nesting to organize presets hierarchically. Users want to categorize presets like "Network/Cisco/Switches" or "Production/Database/Backup" to manage growing collections of commands.

Current system: Single-level folders stored as flat names in `PresetFolders` dictionary.

## Goals / Non-Goals

### Goals
- Unlimited folder depth using forward-slash paths
- Presets can exist at any level (root, intermediate, leaf)
- Preserve existing folder features (expand/collapse state, favorites, manual ordering)
- Backward-compatible migration for existing single-level folders

### Non-Goals
- Folder-level execution (run all presets in folder) - existing feature, unchanged
- Folder templates or inheritance
- Folder permissions or access control

## Decisions

### Decision: Path-based folder keys
Folders keyed by full path (e.g., "Network/Cisco/Switches") rather than separate parent/child references.

**Rationale:**
- Simpler storage model - single dictionary, no tree structure in JSON
- Easy path parsing with string operations
- Natural hierarchy inference from path segments
- Consistent with how presets already reference folders

**Alternatives considered:**
- Parent ID reference: More complex model, harder to serialize
- Nested JSON structure: Harder to query and update

### Decision: Implicit intermediate folders
Creating "A/B/C" automatically creates "A" and "A/B" if they don't exist.

**Rationale:**
- Prevents orphan paths
- Mirrors filesystem behavior users expect
- Simplifies UI - no need to create parent folders manually

### Decision: Virtual folder tree in UI
TreeView nodes built dynamically by parsing paths, not stored as a separate hierarchy.

**Rationale:**
- Single source of truth (paths in PresetFolders)
- Simpler model updates - change paths, tree rebuilds
- No sync issues between stored tree and paths

### Decision: Forward-slash separator
Use `/` as path separator.

**Rationale:**
- Common convention (URLs, Unix paths)
- Unlikely to appear in folder names
- Works across platforms

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Very deep paths cause display issues | TreeView has horizontal scroll; consider tooltip for truncated names |
| Rename intermediate folder is complex | Implement as atomic operation updating all descendant paths |
| Accidental recursive delete | Confirm dialog shows child count; option to move to parent |
| Path conflicts with existing folder names containing `/` | Unlikely in practice; could escape if needed |

## Migration Plan
1. Load existing config - single-level folders have no `/`, so they remain valid root-level folders
2. No migration script needed - existing data is compatible
3. First time user creates nested folder, the path format is used naturally

## Key Implementation Details

### Path Utility Methods
```csharp
// Add to Utilities/FolderPathUtility.cs or PresetManager
public static string? GetParentPath(string path)
    => path.Contains('/') ? path[..path.LastIndexOf('/')] : null;

public static string GetFolderName(string path)
    => path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;

public static string[] GetPathSegments(string path)
    => path.Split('/');

public static IEnumerable<string> GetAncestorPaths(string path)
{
    var segments = path.Split('/');
    for (int i = 1; i < segments.Length; i++)
        yield return string.Join('/', segments.Take(i));
}
```

### TreeView Building Algorithm
```
1. Get all folder paths from PresetFolders
2. Build tree structure:
   - Parse each path into segments
   - For each segment, find or create node
   - Track node by full path for preset assignment
3. Add presets to their folder nodes
4. Add root-level presets to tree root
```

## Open Questions
None - requirements clarified with user:
- Nesting depth: Unlimited
- Preset placement: Anywhere (root, intermediate, leaf)
- Path format: Forward slash
