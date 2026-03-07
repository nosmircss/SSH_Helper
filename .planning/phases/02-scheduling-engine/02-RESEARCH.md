# Phase 2: Scheduling Engine - Research

**Researched:** 2026-03-07
**Domain:** Cron scheduling, visual cron builder, one-time scheduling, missed-run detection (.NET 8 / WinForms)
**Confidence:** HIGH

## Summary

Phase 2 adds scheduling capabilities to the existing JobDefinition model from Phase 1. The core technical work involves: (1) integrating two small NuGet packages -- Cronos for cron parsing/next-occurrence calculation and CronExpressionDescriptor for human-readable descriptions, (2) building a custom WinForms cron builder UI with dropdown selectors and preset templates, (3) adding a ScheduleType enum and SchedulingService to the service layer, and (4) implementing missed-run detection on startup.

The cron library ecosystem for .NET is mature and stable. Cronos (by the Hangfire team) is the dominant parser with full timezone/DST handling, and CronExpressionDescriptor is the standard companion for human-readable output. Both are MIT-licensed, lightweight, and well-maintained. No viable pre-built WinForms cron builder control exists (the only one found -- rafaelbubach/CronControl -- has 5 stars, 3 commits, last updated 2017). The custom builder approach specified in CONTEXT.md is correct.

**Primary recommendation:** Use Cronos 0.11.1 + CronExpressionDescriptor 2.45.0. Build the cron visual builder as a custom UserControl with ComboBox dropdowns for each field. Add a SchedulingService that wraps cron logic and missed-run detection as a pure service (no timer/execution -- that is Phase 3).

<user_constraints>

## User Constraints (from CONTEXT.md)

### Locked Decisions
- Combined cron builder UX: preset templates as quick-start buttons at the top, with dropdown selectors below that update live
- Selecting a preset fills the dropdowns; editing dropdowns updates the expression
- Editable raw text field showing the cron expression, synced bidirectionally with the visual builder
- 5-field standard cron format (minute, hour, day-of-month, month, day-of-week) -- no seconds field
- Comprehensive preset templates organized by frequency: Every 5/15/30 min, Hourly, Daily at midnight/3am, Weekdays 9am, Weekly Monday, Monthly 1st, Quarterly
- Human-readable cron description shown inline after the expression: "0 3 * * * -- Every day at 3:00 AM"
- Next-run preview shows the next 1 upcoming run time only
- All times displayed in user's local timezone (internal storage remains UTC)
- Standard WinForms DateTimePicker with calendar dropdown + time spinner for one-time schedule
- Past dates are blocked -- save/OK disabled with validation message "Schedule time must be in the future"
- After execution, job auto-disables with DisabledReason="One-time schedule completed" but keeps the original schedule time visible
- User can re-enable and set a new time to reuse the job
- ComboBox dropdown in the job editor with "Recurring" and "One-time" options, mutually exclusive
- Missed-run handling: always skip, never auto-execute. Jobs missed while application was closed are recorded as skipped entries on startup

### Claude's Discretion
- Next-run preview placement in the cron builder dialog (below builder or side panel)
- Exact cron library choice (STATE.md notes Cronos + CronExpressionDescriptor as research recommendation)
- Dropdown selector layout and styling within existing dialog patterns
- Validation UX for the raw cron text field (when/how to show errors)
- Missed-run notification approach (log entry, visual indicator, or popup)

### Deferred Ideas (OUT OF SCOPE)
None -- discussion stayed within phase scope

</user_constraints>

<phase_requirements>

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| SCHD-01 | User can configure a cron-based recurring schedule with standard cron expressions | Cronos 0.11.1 provides Parse/TryParse for 5-field cron; SchedulingService wraps validation and stores on JobDefinition.CronExpression |
| SCHD-02 | User can configure a one-time schedule at a specific date and time | WinForms DateTimePicker (built-in), stored as JobDefinition.OneTimeScheduleUtc; InputValidator extended for future-date validation |
| SCHD-03 | One-time jobs auto-disable after successful execution | JobDefinition.IsEnabled + DisabledReason pattern already exists from Phase 1; SchedulingService.MarkOneTimeCompleted() sets both |
| SCHD-04 | Scheduler displays human-readable cron text alongside the expression | CronExpressionDescriptor 2.45.0 ExpressionDescriptor.GetDescription() returns plain English from cron string |
| SCHD-05 | User can build cron expressions via a visual point-and-click builder | Custom CronBuilderControl (UserControl) with ComboBox dropdowns per field + preset buttons; no viable pre-built control exists |
| SCHD-06 | Scheduler shows next upcoming run times as a preview | Cronos CronExpression.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Local) returns next DateTimeOffset; displayed in local time |
| SCHD-07 | Jobs missed while application was closed are recorded as skipped, never auto-executed | SchedulingService.DetectMissedRuns() compares LastRunUtc/LastCheckedUtc against cron occurrences since last app close; records SkippedRunEntry objects |

</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| [Cronos](https://www.nuget.org/packages/Cronos) | 0.11.1 | Cron expression parsing, validation, next-occurrence calculation | By Hangfire team, MIT, 94M+ downloads, handles DST correctly, TryParse for validation |
| [CronExpressionDescriptor](https://www.nuget.org/packages/CronExpressionDescriptor) | 2.45.0 | Convert cron expressions to human-readable English | 45M+ downloads, MIT, 29 languages, pairs naturally with Cronos |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Newtonsoft.Json | 13.0.3 | Serialize ScheduleType enum and schedule fields | Already in project -- schedule data serializes via existing JobDefinition persistence |
| xUnit + FluentAssertions + Moq | 2.7.0 / 6.12.0 / 4.20.70 | Unit testing | Already in test project -- test all scheduling logic without UI |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Cronos | NCrontab | NCrontab is simpler but lacks TryParse, DST handling, and DateTimeOffset support |
| CronExpressionDescriptor | Manual string building | Would need to hand-roll 20+ cron pattern descriptions; library handles edge cases |
| Custom cron builder | rafaelbubach/CronControl | Abandoned (2017, 3 commits, 5 stars), targets old .NET Framework, no NuGet |
| Custom cron builder | WPF controls via ElementHost | Adds WPF dependency, theme mismatch, complexity for no gain |

**Installation:**
```bash
dotnet add SSH_Helper.csproj package Cronos --version 0.11.1
dotnet add SSH_Helper.csproj package CronExpressionDescriptor --version 2.45.0
```

## Architecture Patterns

### Recommended Project Structure
```
SSH_Helper/
  Models/
    JobDefinition.cs         # Add ScheduleType enum, keep existing CronExpression/OneTimeScheduleUtc fields
    SkippedRunEntry.cs       # NEW: records a missed run (JobId, ScheduledTimeUtc, DetectedUtc)
  Services/
    SchedulingService.cs     # NEW: cron validation, next-run calc, missed-run detection, one-time completion
    JobStorageService.cs     # Existing -- no changes needed, schedule data persists via JobDefinition
  UI/
    CronBuilderControl.cs   # NEW: UserControl with dropdowns, presets, raw field, description, next-run preview
    DialogTheme.cs           # Existing -- apply to CronBuilderControl
  Utilities/
    InputValidator.cs        # Extend: add ValidateCronExpression(), ValidateFutureDate()
```

### Pattern 1: SchedulingService as Pure Logic Service
**What:** A stateless service that wraps Cronos + CronExpressionDescriptor and provides all scheduling operations. It does NOT run a timer or execute jobs (that is Phase 3's SchedulerService).
**When to use:** Anywhere schedule data needs validation, description, next-run calculation, or missed-run detection.
**Example:**
```csharp
// Source: Cronos GitHub README + CronExpressionDescriptor GitHub README
public sealed class SchedulingService
{
    /// <summary>
    /// Validates a cron expression string. Returns null if valid, error message if invalid.
    /// </summary>
    public string? ValidateCronExpression(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "Cron expression cannot be empty.";

        // 5-field only, no seconds
        if (!CronExpression.TryParse(expression, out _))
            return "Invalid cron expression.";

        return null; // valid
    }

    /// <summary>
    /// Returns human-readable description of a cron expression.
    /// Returns null if expression is invalid.
    /// </summary>
    public string? GetDescription(string expression)
    {
        if (!CronExpression.TryParse(expression, out _))
            return null;

        return ExpressionDescriptor.GetDescription(expression, new Options
        {
            Use24HourTimeFormat = false,
            ThrowExceptionOnParseError = false
        });
    }

    /// <summary>
    /// Gets the next occurrence in local time. Returns null if expression is invalid
    /// or no next occurrence exists.
    /// </summary>
    public DateTime? GetNextRunLocal(string cronExpression)
    {
        if (!CronExpression.TryParse(cronExpression, out var cron))
            return null;

        var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        return next?.LocalDateTime;
    }

    /// <summary>
    /// Gets the next occurrence in UTC. Returns null if no next occurrence exists.
    /// </summary>
    public DateTime? GetNextRunUtc(string cronExpression)
    {
        if (!CronExpression.TryParse(cronExpression, out var cron))
            return null;

        return cron.GetNextOccurrence(DateTime.UtcNow);
    }

    /// <summary>
    /// Detects all cron occurrences that were missed between lastCheckedUtc and now.
    /// </summary>
    public IReadOnlyList<DateTime> GetMissedOccurrences(string cronExpression, DateTime lastCheckedUtc)
    {
        if (!CronExpression.TryParse(cronExpression, out var cron))
            return Array.Empty<DateTime>();

        return cron.GetOccurrences(lastCheckedUtc, DateTime.UtcNow, fromInclusive: false, toInclusive: false)
            .ToList()
            .AsReadOnly();
    }
}
```

### Pattern 2: CronBuilderControl as Reusable UserControl
**What:** A self-contained WinForms UserControl that encapsulates the entire cron builder UI -- preset buttons, field dropdowns, raw text field, description label, and next-run preview. Exposes a `CronExpression` property and a `CronExpressionChanged` event.
**When to use:** Embedded in the schedule panel of the job editor dialog (Phase 5 builds the full dialog, Phase 2 builds this reusable component).
**Example:**
```csharp
public sealed class CronBuilderControl : UserControl
{
    // Preset buttons (FlowLayoutPanel at top)
    // 5 ComboBox dropdowns (minute, hour, day-of-month, month, day-of-week)
    // TextBox for raw expression (bidirectional sync)
    // Label for human-readable description
    // Label for next-run preview

    public string CronExpression { get; set; }
    public event EventHandler? CronExpressionChanged;

    // Bidirectional sync:
    // - Dropdown change -> rebuild expression string -> update raw field + description + next-run
    // - Raw field change -> parse expression -> update dropdowns + description + next-run
    // - Preset button click -> set all dropdowns -> triggers dropdown change path
}
```

### Pattern 3: ScheduleType Enum Derived from Fields
**What:** Rather than adding a separate `ScheduleType` property to JobDefinition, derive the schedule type from which field is populated. However, CONTEXT.md specifies a ComboBox with explicit "Recurring" and "One-time" options, so a `ScheduleType` enum is cleaner for UI binding and storage.
**When to use:** Always -- the enum makes the UI mutually exclusive state explicit.
**Example:**
```csharp
public enum ScheduleType
{
    None = 0,      // No schedule configured
    Recurring = 1, // Uses CronExpression
    OneTime = 2    // Uses OneTimeScheduleUtc
}

// Add to JobDefinition:
public ScheduleType ScheduleType { get; set; } = ScheduleType.None;
```

### Pattern 4: Missed-Run Detection on Startup
**What:** When the application starts, SchedulingService compares each enabled recurring job's last-checked timestamp against all cron occurrences that would have fired between then and now. Each missed occurrence becomes a SkippedRunEntry.
**When to use:** Called once during application startup, after JobStorageService.Load().
**Example:**
```csharp
public sealed class SkippedRunEntry
{
    public string JobId { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public DateTime ScheduledTimeUtc { get; set; }
    public DateTime DetectedUtc { get; set; } = DateTime.UtcNow;
}

// In SchedulingService:
public IReadOnlyList<SkippedRunEntry> DetectMissedRuns(
    IReadOnlyDictionary<string, JobDefinition> jobs,
    DateTime lastAppShutdownUtc)
{
    var skipped = new List<SkippedRunEntry>();
    foreach (var job in jobs.Values)
    {
        if (!job.IsEnabled || job.ScheduleType != ScheduleType.Recurring
            || string.IsNullOrEmpty(job.CronExpression))
            continue;

        var missed = GetMissedOccurrences(job.CronExpression, lastAppShutdownUtc);
        foreach (var time in missed)
        {
            skipped.Add(new SkippedRunEntry
            {
                JobId = job.Id,
                JobName = job.Name,
                ScheduledTimeUtc = time
            });
        }
    }
    return skipped.AsReadOnly();
}
```

### Anti-Patterns to Avoid
- **Embedding cron logic in UI code:** All cron parsing, validation, and calculation must go through SchedulingService. The CronBuilderControl calls the service, never imports Cronos directly.
- **Using DateTime.Now for schedule calculations:** Cronos requires UTC or DateTimeOffset. Always store and calculate in UTC, convert to local only for display.
- **Building a timer/executor in this phase:** Phase 2 is schedule DATA and UI only. The actual timer that checks due jobs and triggers execution belongs in Phase 3 (SchedulerService).
- **Using CronFormat.IncludeSeconds:** User decision locks this to 5-field. Never pass CronFormat.IncludeSeconds to Cronos.
- **Coupling SkippedRunEntry to the History system:** Phase 4 builds history. For now, SkippedRunEntry is a standalone model. Phase 4 can integrate it later.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Cron expression parsing | Custom regex parser | Cronos CronExpression.Parse/TryParse | Cron has 30+ edge cases (ranges, steps, L/W/#, reversed ranges); Cronos handles them all |
| Next-occurrence calculation | Date arithmetic with cron fields | Cronos GetNextOccurrence with TimeZoneInfo | DST transitions create invisible bugs; Cronos matches Vixie Cron behavior |
| Human-readable cron text | Switch statement on common patterns | CronExpressionDescriptor | Handles all 5-field combinations, 29 locales, edge cases like "At 10:00 AM, every 3 months on the 15th" |
| Cron expression validation | Regex pattern matching | Cronos TryParse | Returns false for structurally invalid expressions; single source of truth |
| One-time date picker | Custom calendar control | WinForms DateTimePicker | Built-in, accessible, theme-compatible, handles timezone natively |

**Key insight:** Cron parsing is a solved problem with surprising depth. Hand-rolling a parser would miss reversed ranges (`23-01`), day-of-week numbering differences, month name handling, and dozens of other edge cases that Cronos handles with its 10+ years of production use via Hangfire.

## Common Pitfalls

### Pitfall 1: DateTime.Now in Cronos
**What goes wrong:** Cronos throws an exception if you pass `DateTime.Now` (local time) to GetNextOccurrence.
**Why it happens:** DST transitions make local DateTime ambiguous (one local time can map to two UTC times during fall-back).
**How to avoid:** Always use `DateTime.UtcNow` or `DateTimeOffset.UtcNow`. Convert to local only when displaying to the user via `TimeZoneInfo.Local`.
**Warning signs:** `ArgumentException: The supplied DateTime must have the Kind property set to DateTimeKind.Utc`

### Pitfall 2: Bidirectional Sync Loops in Cron Builder
**What goes wrong:** Dropdown change fires TextChanged, which fires dropdown change, infinite loop.
**Why it happens:** Bidirectional sync between dropdowns and raw text field without guard flags.
**How to avoid:** Use a `_suppressSyncEvents` boolean flag. Set it true before programmatic updates, check it at the start of each event handler.
**Warning signs:** Stack overflow or UI freeze when typing in the raw field or clicking a preset.

### Pitfall 3: CronExpressionDescriptor Field Count Mismatch
**What goes wrong:** CronExpressionDescriptor interprets 6-field expressions differently than Cronos. If someone accidentally passes a 6-field string, the description is wrong.
**Why it happens:** CronExpressionDescriptor treats 6 fields as either "seconds + 5 standard" or "5 standard + year" depending on context. Cronos treats 6 fields as "seconds + 5 standard" only when CronFormat.IncludeSeconds is specified.
**How to avoid:** Validate with Cronos TryParse (5-field, no IncludeSeconds) before passing to CronExpressionDescriptor. Reject any expression that does not have exactly 5 space-separated fields.
**Warning signs:** Description says "Every second" when user expected "Every minute."

### Pitfall 4: One-Time Schedule Timezone Storage
**What goes wrong:** User picks "March 15 at 3:00 PM" in local time, but it gets stored as UTC. When displayed back, timezone offset is applied twice.
**Why it happens:** Mixing local and UTC without consistent conversion.
**How to avoid:** DateTimePicker shows local time. On save, convert to UTC via `selectedDateTime.ToUniversalTime()`. On load, convert back via `storedUtc.ToLocalTime()` for display. The property name `OneTimeScheduleUtc` makes the contract explicit.
**Warning signs:** Schedule appears to fire 5-8 hours early or late.

### Pitfall 5: Missed-Run Detection Window Edge Cases
**What goes wrong:** If `lastAppShutdownUtc` is not persisted, the app detects ALL historical occurrences as missed on first run.
**Why it happens:** No shutdown timestamp to anchor the detection window.
**How to avoid:** Persist `LastAppShutdownUtc` in config.json (via ConfigurationService) on application close. On first install (no timestamp), default to DateTime.UtcNow (no missed runs -- clean slate).
**Warning signs:** Hundreds of "skipped" entries appear on first launch after enabling scheduling.

### Pitfall 6: Dropdown Values for Complex Cron Fields
**What goes wrong:** The visual builder cannot represent all valid cron expressions (e.g., `1,15,30 */2 1-15 * MON-FRI`).
**Why it happens:** ComboBox dropdowns support single-select; complex multi-value/range expressions need richer controls.
**How to avoid:** The raw text field is always editable and is the source of truth. Dropdowns handle common single-value cases. When the raw field contains a complex expression that cannot be represented in dropdowns, show the dropdowns as "Custom" (disabled/greyed) and let the description label provide clarity.
**Warning signs:** User types a complex expression, dropdowns show wrong values, user edits dropdown, loses their complex expression.

## Code Examples

### Cronos: Parse, Validate, Next Occurrence
```csharp
// Source: https://github.com/HangfireIO/Cronos
using Cronos;

// Parse (throws CronFormatException on invalid)
CronExpression expr = CronExpression.Parse("0 3 * * *"); // 5-field, no seconds

// TryParse (safe validation)
bool isValid = CronExpression.TryParse("0 3 * * *", out CronExpression? parsed);

// Next occurrence in UTC
DateTime? nextUtc = expr.GetNextOccurrence(DateTime.UtcNow);

// Next occurrence in local timezone (for display)
DateTimeOffset? nextLocal = expr.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Local);
// Display: nextLocal?.LocalDateTime.ToString("g")

// Get all occurrences in a range (for missed-run detection)
IEnumerable<DateTime> occurrences = expr.GetOccurrences(
    startUtc, endUtc, fromInclusive: false, toInclusive: false);
```

### CronExpressionDescriptor: Human-Readable Description
```csharp
// Source: https://github.com/bradymholt/cron-expression-descriptor
using CronExpressionDescriptor;

string desc = ExpressionDescriptor.GetDescription("0 3 * * *");
// Returns: "At 03:00 AM"

// With options
string desc24h = ExpressionDescriptor.GetDescription("0 3 * * *", new Options
{
    Use24HourTimeFormat = true,
    ThrowExceptionOnParseError = false
});
// Returns: "At 03:00"

// Safe usage (no exception on bad input)
string safeDesc = ExpressionDescriptor.GetDescription("invalid", new Options
{
    ThrowExceptionOnParseError = false
});
// Returns error message string instead of throwing
```

### Cron Preset Templates
```csharp
// Source: Project-specific, based on CONTEXT.md decisions
public static readonly (string Label, string Expression)[] CronPresets = new[]
{
    ("Every 5 min",    "*/5 * * * *"),
    ("Every 15 min",   "*/15 * * * *"),
    ("Every 30 min",   "*/30 * * * *"),
    ("Hourly",         "0 * * * *"),
    ("Daily midnight", "0 0 * * *"),
    ("Daily 3 AM",     "0 3 * * *"),
    ("Weekdays 9 AM",  "0 9 * * 1-5"),
    ("Weekly Monday",  "0 0 * * 1"),
    ("Monthly 1st",    "0 0 1 * *"),
    ("Quarterly",      "0 0 1 1,4,7,10 *"),
};
```

### Bidirectional Sync Guard Pattern
```csharp
// Source: Standard WinForms pattern for bidirectional data binding
private bool _suppressSyncEvents;

private void OnDropdownChanged(object? sender, EventArgs e)
{
    if (_suppressSyncEvents) return;
    _suppressSyncEvents = true;
    try
    {
        var expression = BuildExpressionFromDropdowns();
        _txtRawExpression.Text = expression;
        UpdateDescriptionAndPreview(expression);
    }
    finally
    {
        _suppressSyncEvents = false;
    }
}

private void OnRawExpressionChanged(object? sender, EventArgs e)
{
    if (_suppressSyncEvents) return;
    _suppressSyncEvents = true;
    try
    {
        var expression = _txtRawExpression.Text.Trim();
        if (TryParseToDropdowns(expression))
        {
            UpdateDropdownsFromExpression(expression);
        }
        else
        {
            SetDropdownsToCustom(); // Show "Custom" in dropdowns
        }
        UpdateDescriptionAndPreview(expression);
    }
    finally
    {
        _suppressSyncEvents = false;
    }
}
```

### InputValidator Extension for Cron
```csharp
// Source: Extending existing InputValidator pattern
public static class InputValidator
{
    // ... existing methods ...

    /// <summary>
    /// Validates a 5-field cron expression using Cronos.
    /// Returns null if valid, error message if invalid.
    /// </summary>
    public static string? ValidateCronExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "Cron expression cannot be empty.";

        if (!CronExpression.TryParse(expression.Trim(), out _))
            return "Invalid cron expression format. Expected 5 fields: minute hour day-of-month month day-of-week.";

        return null;
    }

    /// <summary>
    /// Validates that a DateTime is in the future.
    /// </summary>
    public static bool IsFutureDate(DateTime dateTimeUtc)
    {
        return dateTimeUtc > DateTime.UtcNow;
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| NCrontab for cron parsing | Cronos (by Hangfire) | ~2019 | Cronos handles DST, has TryParse, supports DateTimeOffset, and is actively maintained |
| Manual cron description strings | CronExpressionDescriptor | Stable since 2014 | 29-language support, handles all cron variants |
| Quartz.NET for simple scheduling | Lightweight Cronos + custom timer | Ongoing trend | Quartz.NET is heavyweight (full scheduler); Cronos is parse-only, letting you own the execution |

**Deprecated/outdated:**
- NCrontab: Still works but lacks TryParse and DST handling. Cronos is the modern replacement.
- CrontabSchedule (from NCrontab): Superseded by Cronos for next-occurrence calculation.

## Open Questions

1. **LastAppShutdownUtc persistence location**
   - What we know: Needs to be saved on app close and read on startup for missed-run detection
   - What's unclear: Should it go in config.json (ConfigurationService) or jobs.json (JobStorageService)?
   - Recommendation: Add to AppConfiguration (config.json) via ConfigurationService -- it is app-level state, not per-job state

2. **SkippedRunEntry persistence vs in-memory**
   - What we know: SCHD-07 says missed runs are "recorded as skipped"
   - What's unclear: Should skipped entries persist to disk or only exist in-memory for the current session?
   - Recommendation: Persist to a `skipped-runs.json` file or embed in jobs.json. Phase 4 (History) can later integrate these into the full run history. For Phase 2, in-memory list surfaced via the service is sufficient since the detection happens on each startup anyway.

3. **CronBuilderControl hosting before Phase 5**
   - What we know: Phase 5 builds the full job editor dialog. Phase 2 needs a schedule panel for testing and validation.
   - What's unclear: Should Phase 2 build a minimal test dialog, or just build the UserControl and test it programmatically?
   - Recommendation: Build the CronBuilderControl as a standalone UserControl. Unit test the SchedulingService. The control can be visually tested with a simple test harness form if needed, but the primary testing is service-level.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.7.0 + FluentAssertions 6.12.0 + Moq 4.20.70 |
| Config file | SSH_Helper.Tests/SSH_Helper.Tests.csproj |
| Quick run command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Scheduling" -x` |
| Full suite command | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj` |

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SCHD-01 | Cron expression validates and stores on JobDefinition | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests" -x` | Wave 0 |
| SCHD-02 | One-time schedule stores as UTC DateTime | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests" -x` | Wave 0 |
| SCHD-03 | One-time job auto-disables after completion | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.MarkOneTimeCompleted" -x` | Wave 0 |
| SCHD-04 | Human-readable cron description returned | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.GetDescription" -x` | Wave 0 |
| SCHD-05 | Cron builder dropdowns produce valid expression | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~CronBuilderTests" -x` | Wave 0 |
| SCHD-06 | Next-run preview calculates correct local time | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.GetNextRun" -x` | Wave 0 |
| SCHD-07 | Missed runs detected between two timestamps | unit | `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~SchedulingServiceTests.DetectMissedRuns" -x` | Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj --filter "FullyQualifiedName~Scheduling" -x`
- **Per wave merge:** `dotnet test SSH_Helper.Tests/SSH_Helper.Tests.csproj`
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `SSH_Helper.Tests/Services/SchedulingServiceTests.cs` -- covers SCHD-01, SCHD-02, SCHD-03, SCHD-04, SCHD-06, SCHD-07
- [ ] `SSH_Helper.Tests/UI/CronBuilderTests.cs` -- covers SCHD-05 (cron expression building logic, can test without UI)
- [ ] `SSH_Helper.Tests/Utilities/InputValidatorCronTests.cs` -- covers cron validation and future-date validation extensions
- [ ] NuGet install: `dotnet add SSH_Helper.csproj package Cronos --version 0.11.1 && dotnet add SSH_Helper.csproj package CronExpressionDescriptor --version 2.45.0`

## Sources

### Primary (HIGH confidence)
- [Cronos NuGet 0.11.1](https://www.nuget.org/packages/Cronos) - version, download count, dependencies
- [Cronos GitHub](https://github.com/HangfireIO/Cronos) - API usage, TryParse, GetNextOccurrence, GetOccurrences, DST handling, CronFormat enum
- [CronExpressionDescriptor NuGet 2.45.0](https://www.nuget.org/packages/CronExpressionDescriptor) - version, .NET Standard 2.0 target
- [CronExpressionDescriptor GitHub](https://github.com/bradymholt/cron-expression-descriptor) - ExpressionDescriptor.GetDescription API, Options class, locale support, field count handling
- [Cronos source: CronExpression.cs](https://github.com/HangfireIO/Cronos/blob/main/src/Cronos/CronExpression.cs) - TryParse method signature verified

### Secondary (MEDIUM confidence)
- [Cronos releases](https://github.com/HangfireIO/Cronos/releases) - TryParse addition, MaxYear bump to 2499, strong naming, nullable annotations
- WinForms cron control ecosystem search - confirmed no viable pre-built WinForms control

### Tertiary (LOW confidence)
- [rafaelbubach/CronControl](https://github.com/rafaelbubach/CronControl) - confirmed abandoned (2017, 3 commits, 5 stars), not suitable

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Cronos and CronExpressionDescriptor are the definitive .NET cron libraries, verified via NuGet and GitHub
- Architecture: HIGH - follows existing project patterns (service-oriented, event-driven, DialogTheme), model fields already exist from Phase 1
- Pitfalls: HIGH - DST/timezone pitfalls are well-documented in Cronos README; bidirectional sync is standard WinForms knowledge
- UI approach: HIGH - no viable pre-built control confirms custom builder is the only path; CONTEXT.md provides detailed UX specification

**Research date:** 2026-03-07
**Valid until:** 2026-04-07 (stable libraries, low churn)
