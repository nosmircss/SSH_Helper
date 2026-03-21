# Browser Callback Show-After-Seconds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-step `show_after_seconds` option so WebView2 browser callbacks can stay hidden unless the flow takes longer than the configured delay.

**Architecture:** Keep browser selection script-local. Parse and validate `show_after_seconds` on `browser_callback_capture`, pass it through the existing browser-callback UI host request, and let the WebView2 session decide whether to show the modal dialog immediately or only after a timer elapses while the callback is still pending.

**Tech Stack:** C#, WinForms, WebView2, xUnit, FluentAssertions, OpenSpec

---

### Task 1: Update the spec and task tracker

**Files:**
- Modify: `tasks/todo.md`
- Modify: `openspec/changes/add-browser-callback-webview2-mode/proposal.md`
- Modify: `openspec/changes/add-browser-callback-webview2-mode/tasks.md`
- Modify: `openspec/changes/add-browser-callback-webview2-mode/design.md`
- Modify: `openspec/changes/add-browser-callback-webview2-mode/specs/scripting-network-steps/spec.md`
- Modify: `openspec/changes/add-browser-callback-webview2-mode/specs/scripting-runtime/spec.md`

- [ ] Add a tracked task entry for the delayed-reveal work in `tasks/todo.md`.
- [ ] Extend the active OpenSpec change to document `show_after_seconds`, WebView2-only applicability, and hidden-until-slow dialog behavior.

### Task 2: Add failing parser tests

**Files:**
- Modify: `SSH_Helper.Tests/Scripting/NetworkStepParserTests.cs`
- Modify: `Services/Scripting/Models/ScriptStep.cs`
- Modify: `Services/Scripting/ScriptParser.cs`

- [ ] Write a parser test that expects `show_after_seconds` to parse into the browser callback options and default to zero when omitted.
- [ ] Write a validation test that rejects negative `show_after_seconds` values with line-specific context.
- [ ] Run the focused parser tests and confirm they fail before implementation.

### Task 3: Add failing runtime tests

**Files:**
- Modify: `SSH_Helper.Tests/Scripting/BrowserCallbackCaptureCommandTests.cs`
- Modify: `Services/Scripting/BrowserCallbackUiHost.cs`
- Modify: `Services/Scripting/Commands/BrowserCallbackCaptureCommand.cs`

- [ ] Add a runtime test that expects WebView2 mode with `show_after_seconds > 0` to launch an embedded session without immediately showing it.
- [ ] Add a runtime test that expects the dialog to reveal only after the configured delay if the callback has not completed yet.
- [ ] Add a runtime test that expects early callback completion to close the session without any reveal.
- [ ] Run the focused runtime tests and confirm they fail before implementation.

### Task 4: Implement delayed reveal

**Files:**
- Modify: `Services/Scripting/Models/ScriptStep.cs`
- Modify: `Services/Scripting/ScriptParser.cs`
- Modify: `Services/Scripting/BrowserCallbackUiHost.cs`
- Modify: `Services/Scripting/Commands/BrowserCallbackCaptureCommand.cs`
- Modify: `UI/BrowserCallbackWebViewDialog.cs`

- [ ] Add `ShowAfterSeconds` to the browser callback options model and parser/validation path.
- [ ] Thread `ShowAfterSeconds` through the browser callback UI launch request and fake/test session seam.
- [ ] Implement the WebView2 session timing so delayed steps initialize hidden, reveal only after the delay if still pending, and never reveal once close/completion has begun.
- [ ] Keep `show_after_seconds: 0` as the current immediate-show behavior.

### Task 5: Docs and verification

**Files:**
- Modify: `SCRIPTING.md`
- Modify: `tasks/todo.md`

- [ ] Update `SCRIPTING.md` to document `show_after_seconds`, including that it applies only to `browser_mode: webview2`.
- [ ] Run focused parser/runtime tests.
- [ ] Run the broader browser-callback/parser regression slice.
- [ ] Run `dotnet build .\SSH_Helper.sln -nologo`.
- [ ] Run `openspec validate add-browser-callback-webview2-mode --strict --no-interactive`.
- [ ] Capture the review outcome under task 112 in `tasks/todo.md`.
