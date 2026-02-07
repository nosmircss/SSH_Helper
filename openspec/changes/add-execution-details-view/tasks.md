## 1. Model & Storage
- [ ] 1.1 Create `Models/ExecutionDetails.cs` with `ExecutionDetails` and `HostExecutionContext` classes
- [ ] 1.2 Extend `Services/HistoryResultStore.cs` with `_details` dictionary and `SetDetails`/`TryGetDetails`/`HasDetails` methods
- [ ] 1.3 Update `RemoveResults()` and `Clear()` to also clean up details

## 2. Capture Execution Details
- [ ] 2.1 Add `BuildExecutionDetails()` helper method to `Form1.cs`
- [ ] 2.2 Modify `StoreExecutionHistory()` to return `entryId` and accept/store `ExecutionDetails`
- [ ] 2.3 Modify `StoreFolderExecutionHistory()` to return `entryId` and accept/store `ExecutionDetails`
- [ ] 2.4 Wire detail capture into `ExecutePresetOnRowsAsync()` (capture startTime, build details after execution)
- [ ] 2.5 Wire detail capture into `ExecuteFolderWithOptionsAsync()` (capture startTime, build details after execution)

## 3. Dialog
- [ ] 3.1 Create `ExecutionDetailsDialog.cs` with `BorderlessTabControl` and 4 tabs (Summary, Hosts, Settings, Context)
- [ ] 3.2 Implement Summary tab (read-only TextBox with preset name, commands, timing, host counts)
- [ ] 3.3 Implement Hosts tab (DataGridView with per-host IP, status, timestamp)
- [ ] 3.4 Implement Settings tab (read-only TextBox with username, timeouts, pooling, run mode)
- [ ] 3.5 Implement Context tab (DataGridView with per-host variable substitutions)
- [ ] 3.6 Implement export: Copy to Clipboard and Save to File buttons with `FormatDetailsAsText()`
- [ ] 3.7 Apply dark/light theme support via `DialogTheme`

## 4. Context Menu Integration
- [ ] 4.1 Add `viewDetailsToolStripMenuItem` and separator to `contextHistoryLst` in `Form1.Designer.cs`
- [ ] 4.2 Update `contextHistoryLst_Opening()` to enable/disable View Details based on `HasDetails()`
- [ ] 4.3 Add `ViewExecutionDetails()` handler in `Form1.cs`

## 5. Testing
- [ ] 5.1 Add unit tests for `HistoryResultStore` details methods (set/get, has, remove, clear)
- [ ] 5.2 Build and run existing tests to verify no regressions
