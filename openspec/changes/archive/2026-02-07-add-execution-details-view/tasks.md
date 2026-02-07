## 1. Model & Storage
- [x] 1.1 Create `Models/ExecutionDetails.cs` with `ExecutionDetails` and `HostExecutionContext` classes
- [x] 1.2 Extend `Services/HistoryResultStore.cs` with `_details` dictionary and `SetDetails`/`TryGetDetails`/`HasDetails` methods
- [x] 1.3 Update `RemoveResults()` and `Clear()` to also clean up details

## 2. Capture Execution Details
- [x] 2.1 Add `BuildExecutionDetails()` helper method to `Form1.cs`
- [x] 2.2 Modify `StoreExecutionHistory()` to return `entryId` and accept/store `ExecutionDetails`
- [x] 2.3 Modify `StoreFolderExecutionHistory()` to return `entryId` and accept/store `ExecutionDetails`
- [x] 2.4 Wire detail capture into `ExecutePresetOnRowsAsync()` (capture startTime, build details after execution)
- [x] 2.5 Wire detail capture into `ExecuteFolderWithOptionsAsync()` (capture startTime, build details after execution)

## 3. Dialog
- [x] 3.1 Create `ExecutionDetailsDialog.cs` with `BorderlessTabControl` and 4 tabs (Summary, Hosts, Settings, Context)
- [x] 3.2 Implement Summary tab (read-only TextBox with preset name, commands, timing, host counts)
- [x] 3.3 Implement Hosts tab (DataGridView with per-host IP, status, timestamp)
- [x] 3.4 Implement Settings tab (read-only TextBox with username, timeouts, pooling, run mode)
- [x] 3.5 Implement Context tab (DataGridView with per-host variable substitutions)
- [x] 3.6 Implement export: Copy to Clipboard and Save to File buttons with `FormatDetailsAsText()`
- [x] 3.7 Apply dark/light theme support via `DialogTheme`

## 4. Context Menu Integration
- [x] 4.1 Add `viewDetailsToolStripMenuItem` and separator to `contextHistoryLst` in `Form1.Designer.cs`
- [x] 4.2 Update `contextHistoryLst_Opening()` to enable/disable View Details based on `HasDetails()`
- [x] 4.3 Add `ViewExecutionDetails()` handler in `Form1.cs`

## 5. Testing
- [x] 5.1 Add unit tests for `HistoryResultStore` details methods (set/get, has, remove, clear)
- [x] 5.2 Build and run existing tests to verify no regressions
