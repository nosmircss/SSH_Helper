# Browser Callback Auto-Close Browser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a per-step `auto_close_browser` option so browser callback completion pages can stay open instead of always calling `window.close()`.

**Architecture:** Extend the existing `browser_callback_capture` contract with a boolean `auto_close_browser` defaulting to `true`. Keep the toggle scoped to the callback page's HTML close behavior while leaving the WebView2 dialog lifecycle unchanged, then cover the new contract with parser tests and completion-page regression tests.

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

- [ ] Add a tracked task entry for the auto-close toggle work in `tasks/todo.md`.
- [ ] Extend the active OpenSpec change to document `auto_close_browser` and the completion-page close semantics.

### Task 2: Add failing tests

**Files:**
- Modify: `SSH_Helper.Tests/Scripting/NetworkStepParserTests.cs`
- Modify: `SSH_Helper.Tests/Scripting/BrowserCallbackCaptureCommandTests.cs`
- Modify: `Services/Scripting/Models/ScriptStep.cs`
- Modify: `Services/Scripting/ScriptParser.cs`
- Modify: `Services/Scripting/Commands/BrowserCallbackCaptureCommand.cs`

- [ ] Write a parser test that expects `auto_close_browser` to parse and default to `true`.
- [ ] Write a query-mode completion-page test that expects `auto_close_browser: false` to omit `window.close()`.
- [ ] Write a fragment-mode bridge-page test that expects `auto_close_browser: false` to omit `window.close()`.
- [ ] Run the focused parser/command tests and confirm they fail before implementation.

### Task 3: Implement the toggle

**Files:**
- Modify: `Services/Scripting/Models/ScriptStep.cs`
- Modify: `Services/Scripting/ScriptParser.cs`
- Modify: `Services/Scripting/Commands/BrowserCallbackCaptureCommand.cs`

- [ ] Add `AutoCloseBrowser` to the browser callback options model and parser/validation path.
- [ ] Thread the flag into query and fragment completion-page HTML generation.
- [ ] Keep the WebView2 dialog close path unchanged so the toggle only controls `window.close()` on the callback page.

### Task 4: Docs and verification

**Files:**
- Modify: `SCRIPTING.md`
- Modify: `tasks/todo.md`

- [ ] Update `SCRIPTING.md` to document `auto_close_browser`, including that it controls the callback page's automatic tab close behavior.
- [ ] Run focused parser/runtime tests.
- [ ] Run the broader browser-callback/parser regression slice.
- [ ] Run `dotnet build .\SSH_Helper.sln -nologo`.
- [ ] Run `openspec validate add-browser-callback-webview2-mode --strict --no-interactive`.
- [ ] Capture the review outcome under task 113 in `tasks/todo.md`.
