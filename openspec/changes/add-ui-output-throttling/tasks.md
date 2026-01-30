## 1. Implementation
- [x] 1.1 Add UI output throttler helper to buffer output and flush on a 50ms timer.
- [x] 1.2 Append output to the history buffer immediately (unthrottled) while UI rendering is throttled.
- [x] 1.3 Add a command-completed signal from SshShellSession and forward it through SshExecutionService.
- [x] 1.4 Flush buffered UI output on command completion and on execution end.
- [x] 1.5 Bypass throttling for debug output lines.

## 2. Tests
- [x] 2.1 Add unit tests for the output throttler buffer/flush behavior.
- [ ] 2.2 Add a test verifying debug output bypasses throttling.
