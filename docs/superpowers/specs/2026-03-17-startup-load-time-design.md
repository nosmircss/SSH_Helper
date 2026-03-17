# Startup Load Time Design

## Context

The application's startup bottleneck is in `Form1`, not `Program.Main()`. Current startup does substantial synchronous work on the UI thread before the application reaches a stable ready state:

- `config.json` is loaded and parsed multiple times during startup through `ConfigurationService.Load()`.
- Preset tree construction rereads configuration during the same startup session.
- Host-grid restore rebuilds the `DataGridView` row-by-row while scrollbar/layout logic is subscribed to row and column mutation events.
- Scheduler startup runs from the constructor path even though it is not required to complete the initial state restore.

The goal for this pass is to improve the time until the application is fully ready while allowing only minor safe deferrals and preserving current startup behavior.

## Goals

- Reduce redundant synchronous disk I/O and JSON parsing during startup.
- Reduce repeated UI layout work during bulk host-grid restore.
- Move only clearly non-critical startup work out of the constructor path.
- Preserve the same restored state from the user's perspective: presets, host grid, history list, theme, fonts, and scheduler availability.

## Non-Goals

- Redesign the startup architecture across the entire application.
- Change feature behavior or remove startup restoration features.
- Aggressively defer major restore work until long after first paint.
- Optimize unrelated runtime performance outside startup.

## Current Findings

### Repeated configuration loads

Startup currently performs real disk reads and JSON deserialization more than once in the initial path. Relevant call sites include:

- `Form1` constructor setup in [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L267)
- `InitializeFromConfiguration()` in [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L413)
- `RefreshPresetList()` in [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L8134)
- `PresetManager.Load()` in [PresetManager.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Services/PresetManager.cs#L69)

`ConfigurationService.Load()` in [ConfigurationService.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Services/ConfigurationService.cs#L46) performs synchronous file I/O, JSON parsing, saved-state inflation, and may also write the config back to disk.

### Bulk grid restore is not actually bulk

The host grid is restored row-by-row in [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L1954) and [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L10768), while `RowsAdded`, `RowsRemoved`, `ColumnAdded`, `ColumnRemoved`, `ColumnWidthChanged`, and resize events all trigger `UpdateDataGridViewScrollbars()` from [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L669) through [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L680). That causes repeated layout recalculation during the restore pass.

### Startup auto-size can hitch on large host tables

`Form1_Shown()` triggers one-time column autosizing in [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L326), and `AutoSizeColumnsToContent()` currently resizes every column with `AllCells` in [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L2579). This is expensive when many rows are restored.

### Scheduler startup is synchronous in the constructor-era path

`InitializeSchedulerServices()` in [Form1.cs](/c:/Users/nos/source/repos/nosmircss/Test/SSH_Helper/Form1.cs#L11035) loads jobs, performs crash recovery work, records missed runs, and starts the scheduler before startup is otherwise settled.

## Approaches Considered

### 1. Configuration cleanup only

Replace redundant startup `Load()` calls with cached configuration access and stop there.

Pros:

- Lowest-risk change
- Minimal behavioral impact

Cons:

- Leaves the heavier UI restore churn untouched
- Unlikely to materially improve startup for large saved host grids

### 2. Bulk startup optimization pass

Clean up config reuse, make host-grid restore a real bulk operation, and move non-critical scheduler startup into a controlled post-startup continuation.

Pros:

- Targets the highest-leverage startup costs without redesigning the app
- Fits the "fully ready" target while allowing only minor safe deferrals
- Preserves behavior with relatively small, focused code changes

Cons:

- Touches several startup seams in `Form1`
- Needs careful verification to avoid startup regressions

### 3. Aggressive staged hydration

Defer history, scheduler, and other restore work more broadly until after the window is visible.

Pros:

- Largest perceived startup gain

Cons:

- Exceeds the allowed level of behavior change for this pass
- Increases the chance of visible "startup settling" behavior

## Decision

Approach 2 is the chosen design.

## Architecture

### Configuration snapshot

Load configuration once at startup and pass that snapshot through startup-sensitive initialization instead of rereading `config.json` from disk.

Planning should standardize on explicit `AppConfiguration` threading for the startup-only path, with `ConfigurationService.GetCurrent()` reserved for later runtime access after startup is complete.

This means:

- Keep the constructor's initial `Load()` result as the source of truth for the first startup pass.
- Pass that `AppConfiguration` snapshot into startup-sensitive helpers that currently reread configuration.
- Avoid rereading config in preset-tree construction when the same data is already available in memory.

### Bulk grid restore guard

Add a startup/bulk-restore guard around host-grid population so row and column mutation events do not recalculate scrollbar state and related layout on every mutation. At the end of the restore, run a single flush/update pass.

This should be implemented as a narrow UI-state guard in `Form1`, not a general-purpose event system rewrite.

### Minor safe deferral of scheduler startup

Move the entire `InitializeSchedulerServices()` path out of the constructor and into a controlled post-startup continuation, ideally on the existing shown/idle startup path once the main restore work is complete.

The scheduler should still become available automatically during startup. This is a timing cleanup, not a feature change.

### Startup auto-size review

Keep current column sizing behavior unless it is clearly redundant during startup. If autosizing remains necessary, ensure it runs after the restored grid is stable and after bulk-restore suppression is released, so the cost is paid once rather than during the restore itself.

## Expected Startup Sequence

1. `Program.Main()` remains unchanged except for any future instrumentation that might be added for measurement.
2. `Form1` constructor loads config once, initializes core services, restores core UI state, and avoids redundant config reloads.
3. Host-grid restore runs under bulk-update suppression and performs one final layout/scrollbar refresh after population completes.
4. Preset and history restoration remain available during startup with no intended user-visible feature loss.
5. Scheduler startup runs immediately after the primary restore path completes, using a shown/idle continuation rather than blocking constructor-era startup.

## Verification Plan

### Behavioral verification

- Startup still restores the same preset selection or folder selection.
- Startup still restores host rows, selected/checked hosts, theme, fonts, and history index.
- Scheduler still becomes available automatically without a manual trigger.

### Focused automated verification

- Add tests for any new startup helper that threads a config snapshot through startup-sensitive code.
- Add tests for the bulk-restore suppression path so repeated row and column adds do not trigger repeated expensive recomputation.
- Add tests only where the new seams are pure enough to cover reliably; do not force brittle UI tests where a targeted helper test is sufficient.

### Measured verification

- Capture startup timing around the main phases before and after the change on the same machine.
- Use those timings to confirm the constructor/restore path is doing less synchronous work.

## Risks And Mitigations

- Risk: stale config data during startup if snapshot usage is inconsistent.
  Mitigation: use one authoritative startup snapshot and keep later mutable operations on the existing cached config service path.

- Risk: deferred scheduler startup changes event timing.
  Mitigation: keep deferral narrow and automatic, and verify scheduler status/UI still initializes during startup.

- Risk: bulk grid suppression leaves scrollbars or counters stale.
  Mitigation: explicitly flush dependent UI updates once after restore completes and cover the new helper with focused tests.
