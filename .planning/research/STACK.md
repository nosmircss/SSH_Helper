# Stack Research: Job Scheduling for SSH_Helper

**Domain:** In-app job scheduling for a .NET 8 WinForms desktop application
**Researched:** 2026-03-07
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Cronos | 0.11.1 | Cron expression parsing and next-occurrence calculation | De facto .NET cron library. MIT license. Handles DST transitions correctly (matches Vixie Cron behavior). Fastest cron parser in .NET by an order of magnitude (~31ns per parse). From HangfireIO team. Targets .NET Standard 1.0+ so fully compatible with .NET 8. |
| CronExpressionDescriptor | 2.45.0 | Convert cron expressions to human-readable text | Required for the "next-run preview" UI in the job editor. Zero dependencies. Supports 5/6/7-part cron expressions. MIT license. |
| System.Threading.Timer | built-in | Timer ticks to check for due jobs | Built into .NET runtime. Lightweight, thread-pool based. Better than `System.Timers.Timer` for service-layer code (no UI thread affinity). Use a single timer that ticks every ~15-30 seconds to evaluate which jobs are due. |
| SemaphoreSlim | built-in | Bounded concurrent job execution | Built-in async-friendly concurrency limiter. `WaitAsync(CancellationToken)` enables non-blocking bounded parallelism. User-configurable max concurrency maps directly to constructor parameter. |
| CancellationTokenSource | built-in | Job and scheduler cancellation | Built-in .NET cancellation infrastructure. Supports linked tokens (scheduler-level + per-job). Integrates with existing `SshExecutionService` cancellation patterns. |
| Newtonsoft.Json | 13.0.3 (existing) | Job definition persistence | Already in the project for `ConfigurationService`. Reuse for job definition serialization to `%LocalAppData%\SSH_Helper\jobs.json`. No new dependency needed. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Threading.Channels | built-in | Job execution queue | Use a bounded channel as the dispatch queue between the scheduler tick loop and the executor. Provides backpressure when max concurrency is reached. Optional -- `SemaphoreSlim` alone may suffice for this scale. |
| TimeZoneInfo | built-in | Time zone handling | Cronos requires UTC or DateTimeOffset for next-occurrence calculation. Use `TimeZoneInfo.Local` to convert for display. Already part of .NET runtime. |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| xUnit + Moq (existing) | Test scheduler logic | Time-dependent tests should inject `IClock` / `TimeProvider` abstraction. .NET 8 ships `TimeProvider` as a built-in abstraction -- use it. |
| TimeProvider (.NET 8) | Testable time abstraction | Built-in since .NET 8. Inject `TimeProvider.System` in production, `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`) in tests. |
| Microsoft.Extensions.TimeProvider.Testing | 8.x | Fake time for unit tests | Allows advancing time deterministically in scheduler tests. Dev dependency only. |

## Installation

```bash
# New dependencies (only 2 new NuGet packages for production)
dotnet add SSH_Helper.csproj package Cronos --version 0.11.1
dotnet add SSH_Helper.csproj package CronExpressionDescriptor --version 2.45.0

# Test dependency for time manipulation
dotnet add SSH_Helper.Tests/SSH_Helper.Tests.csproj package Microsoft.Extensions.TimeProvider.Testing --version 8.12.0
```

Everything else (SemaphoreSlim, CancellationTokenSource, System.Threading.Timer, TimeProvider, Channels) is built into .NET 8.

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| Cronos (parsing only) + custom scheduler | Quartz.NET 3.x | Never for this project. Quartz is a full scheduler framework designed for ASP.NET/hosted services with DI, IHost, and persistent job stores. Massive dependency surface (~15 packages). Overkill for a WinForms app that needs to check a timer every 30 seconds. |
| Cronos + custom scheduler | Hangfire | Never for this project. Hangfire requires a persistent backing store (SQL Server, Redis, etc.) and a hosted environment. Designed for web applications. Completely wrong abstraction for a desktop app. |
| Cronos + custom scheduler | Coravel | Never for this project. Coravel is ASP.NET Core specific (depends on `IHost`, `IServiceProvider` DI patterns). Would require pulling in the entire generic host infrastructure. |
| SemaphoreSlim | System.Threading.Channels | Channels are better if you want a clear producer-consumer queue with backpressure. For this project's scale (likely <50 concurrent jobs), SemaphoreSlim is simpler and sufficient. Could layer Channels on top later if dispatch queue semantics are needed. |
| System.Threading.Timer | PeriodicTimer (.NET 6+) | PeriodicTimer is async-native and avoids re-entrancy issues. Good alternative if the scheduler loop runs as an async loop rather than timer callbacks. Either works; PeriodicTimer is slightly more modern. Choose based on whether the scheduler is callback-based or loop-based. |
| Newtonsoft.Json (existing) | System.Text.Json (built-in) | Could use STJ for new job persistence since the project already uses both. But ConfigurationService uses Newtonsoft.Json, and job persistence should follow the same pattern for consistency. Stick with Newtonsoft.Json. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Quartz.NET | Massive dependency graph (~15 packages). Designed for server-side hosted services with DI containers. Forces IJob interface implementation pattern that doesn't match existing service architecture. XML/JSON job configuration is unnecessary when you control the scheduler. | Cronos + custom ~200-line SchedulerService |
| Hangfire | Requires external persistent store (SQL Server/Redis). Server-oriented architecture. Dashboard UI is web-based. Wrong paradigm entirely for a desktop app. | Cronos + SemaphoreSlim + JSON persistence |
| NCrontab | Abandoned/unmaintained. Last meaningful update years ago. No DST handling. Cronos is strictly superior in every dimension. | Cronos |
| System.Timers.Timer | Has hidden SynchronizationContext affinity in WinForms. Can fire on the UI thread unexpectedly. `System.Threading.Timer` or `PeriodicTimer` are more predictable in service-layer code. | System.Threading.Timer or PeriodicTimer |
| Windows Task Scheduler integration | Explicitly out of scope per PROJECT.md. Adds external OS dependency. Users can't manage jobs from within the app. Defeats the purpose of an in-app scheduler. | In-app scheduler with Cronos |
| FluentScheduler | Unmaintained (last release 2019). No async support. Thread-based execution model conflicts with WinForms STA requirements. | Cronos + async custom scheduler |
| BackgroundService / IHostedService | Requires `Microsoft.Extensions.Hosting` generic host. Would mean restructuring the WinForms app to run inside a host. Not worth the architectural change for a timer loop. | System.Threading.Timer / PeriodicTimer in a plain service class |

## Stack Patterns by Variant

**If the scheduler tick loop is callback-based (simpler):**
- Use `System.Threading.Timer` with a 15-30 second interval
- On each tick: check all enabled jobs, compute next occurrence via Cronos, fire any that are due
- Guard against re-entrant ticks with a simple `Interlocked.CompareExchange` flag
- Because this is the pattern closest to existing `SshExecutionService` event-driven design

**If the scheduler tick loop is async-loop-based (more modern):**
- Use `PeriodicTimer` in an async loop started on a background thread
- `while (await timer.WaitForNextTickAsync(ct))` pattern
- Slightly cleaner cancellation semantics
- Better fit if the scheduler service manages its own lifecycle

**For bounded concurrency (either variant):**
- `SemaphoreSlim(maxConcurrency, maxConcurrency)` where maxConcurrency comes from user settings
- Each job execution calls `await semaphore.WaitAsync(ct)` before starting, `Release()` in finally
- Dynamically changing concurrency limit: create a new SemaphoreSlim when setting changes (drain existing first)

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| Cronos 0.11.1 | .NET 8.0+ | Targets .NET Standard 1.0, compatible with all .NET versions |
| CronExpressionDescriptor 2.45.0 | .NET 8.0+ | Targets .NET Standard 1.1 and 2.0 |
| Microsoft.Extensions.TimeProvider.Testing 8.x | .NET 8.0 | Must match major version with target framework. Use 8.x for .NET 8. |
| Newtonsoft.Json 13.0.3 | .NET 8.0+ | Already in project, no compatibility concerns |

## Key API Surface

### Cronos Usage Pattern

```csharp
// Parse a cron expression (5-part standard + optional seconds)
var expr = CronExpression.Parse("0 */6 * * *", CronFormat.Standard);

// Get next occurrence from now
DateTimeOffset? next = expr.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Local);

// Get multiple upcoming occurrences (for preview UI)
var upcoming = expr.GetOccurrences(
    DateTimeOffset.UtcNow,
    DateTimeOffset.UtcNow.AddDays(7),
    TimeZoneInfo.Local
).Take(10);

// Validation without exceptions
if (CronExpression.TryParse("invalid", out var parsed))
{
    // valid
}
```

### CronExpressionDescriptor Usage Pattern

```csharp
// Human-readable description for UI
string desc = ExpressionDescriptor.GetDescription("0 */6 * * *");
// Returns: "Every 6 hours"
```

### SemaphoreSlim Bounded Execution Pattern

```csharp
private SemaphoreSlim _concurrencyLimiter = new(maxConcurrency, maxConcurrency);

async Task ExecuteJobAsync(JobDefinition job, CancellationToken ct)
{
    await _concurrencyLimiter.WaitAsync(ct);
    try
    {
        // Delegate to existing SshExecutionService
        await _sshExecutionService.ExecuteAsync(job.Hosts, job.Commands, ct);
    }
    finally
    {
        _concurrencyLimiter.Release();
    }
}
```

## Sources

- [NuGet - Cronos 0.11.1](https://www.nuget.org/packages/Cronos) -- version and compatibility verified (HIGH confidence)
- [GitHub - HangfireIO/Cronos](https://github.com/HangfireIO/Cronos) -- features, DST handling, performance claims (HIGH confidence)
- [NuGet - CronExpressionDescriptor 2.45.0](https://www.nuget.org/packages/CronExpressionDescriptor) -- version verified (HIGH confidence)
- [GitHub - bradymholt/cron-expression-descriptor](https://github.com/bradymholt/cron-expression-descriptor) -- API and features (HIGH confidence)
- [Microsoft Learn - Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) -- bounded channel patterns (HIGH confidence)
- [Microsoft Learn - SemaphoreSlim](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim) -- async concurrency limiter (HIGH confidence)
- [Quartz.NET](https://www.quartz-scheduler.net/) -- evaluated and rejected for this use case (HIGH confidence)
- [Hangfire](https://www.hangfire.io/) -- evaluated and rejected for this use case (HIGH confidence)

---
*Stack research for: In-app job scheduling (.NET 8 WinForms)*
*Researched: 2026-03-07*
