# Change: Add UI output throttling

## Why
High-frequency SSH output events can overwhelm the UI append pipeline and make the interface sluggish during large or chatty outputs. Throttling UI updates improves responsiveness without altering the underlying SSH output stream.

## What Changes
- Throttle UI output rendering to flush at most every 50ms.
- Force a flush at end of command to minimize perceived latency.
- Bypass throttling for debug output ([DEBUG], [SSH DEBUG]).
- Preserve full output capture for history (unthrottled).

## Impact
- Affected specs: ssh-execution
- Affected code: Form1.cs, Services/SshShellSession.cs, Services/SshExecutionService.cs, Utilities (new OutputThrottler helper)
