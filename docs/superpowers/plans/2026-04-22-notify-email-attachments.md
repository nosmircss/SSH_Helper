# Notify Email Attachments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add per-step email attachments to `notify` while keeping YAML, editor, Flow Canvas, docs, and SMTP dispatch behavior aligned.

**Architecture:** Extend the existing `notify` option model with an `attachments` string list, resolve those paths in `NotifyCommand`, and pass the resolved files into the SMTP-specific dispatcher only. Keep non-email channels unchanged, then expose the same field in parser-driven authoring surfaces and document the SMTP-only semantics.

**Tech Stack:** .NET 8, WinForms, YamlDotNet parser, System.Net.Mail SMTP delivery, React/TypeScript Flow Canvas, xUnit + FluentAssertions.

---

### Task 1: Lock the authoring surface with failing tests

**Files:**
- Modify: `SSH_Helper.Tests/Scripting/ScriptParserTests.cs`
- Modify: `SSH_Helper.Tests/Editor/ScriptAutocompleteProviderTests.cs`
- Modify: `SSH_Helper.Tests/Services/FlowCanvasBridgeTests.cs`

- [ ] **Step 1: Write the failing parser test**

Add a `ScriptParserTests` case that parses a `notify` step containing `attachments:` with multiple paths and asserts the resulting `NotifyOptions.Attachments` list is populated in order.

- [ ] **Step 2: Write the failing autocomplete test**

Extend the existing notify option-key completion test so `attachments` is expected alongside `profile`, `channel`, `title`, `message`, `level`, `mention`, `into`, and `on_error`.

- [ ] **Step 3: Write the failing Flow Canvas test**

Extend `FlowCanvasBridgeTests` so notify import preserves `attachments`, and the registry property-order assertion expects the new key in the notify block surface.

- [ ] **Step 4: Run the focused authoring tests and confirm RED**

Run:
`dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~FlowCanvasBridgeTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`

Expected: the new notify attachment assertions fail because the parser/editor/bridge do not know about `attachments` yet.

### Task 2: Lock the runtime/SMTP behavior with failing tests

**Files:**
- Modify: `SSH_Helper.Tests/Scripting/NotifyCommandTests.cs`
- Modify: `SSH_Helper.Tests/Services/NotificationServiceTests.cs`

- [ ] **Step 1: Write the failing notify-command tests**

Add coverage that:
- substitutes variables inside `attachments`,
- ignores `attachments` when `channel`/profile resolve to a non-SMTP channel,
- and forwards resolved attachment paths when the step resolves to SMTP.

- [ ] **Step 2: Write the failing SMTP service test**

Add service/dispatcher coverage that an SMTP notification with valid attachment paths includes all files, and that a missing/unreadable attachment returns a notify failure.

- [ ] **Step 3: Run the focused runtime tests and confirm RED**

Run:
`dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NotifyCommandTests|FullyQualifiedName~NotificationServiceTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`

Expected: the new attachment assertions fail because the runtime and SMTP dispatcher do not accept file paths yet.

### Task 3: Implement the notify attachment pipeline

**Files:**
- Modify: `Services/Scripting/Models/ScriptStep.cs`
- Modify: `Services/Scripting/ScriptParser.cs`
- Modify: `Services/Scripting/Commands/NotifyCommand.cs`
- Modify: `Services/Notifications/NotificationService.cs`
- Modify: `Services/Notifications/SmtpDispatcher.cs`
- Modify: `Services/Editor/ScriptAutocompleteProvider.cs`
- Modify: `Services/FlowCanvasBridge.cs`
- Modify: `FlowCanvas/src/blockDefs/registry.ts`

- [ ] **Step 1: Extend the `notify` model and parser**

Add `NotifyOptions.Attachments`, teach `ScriptParser` to parse `attachments`, and add the option to parser/editor vocabularies.

- [ ] **Step 2: Resolve attachment paths in `NotifyCommand`**

Substitute variables for each attachment entry, preserve order, and pass the resolved list into `NotificationService`.

- [ ] **Step 3: Update `NotificationService` and `SmtpDispatcher`**

Extend the service/dispatcher signatures to accept attachments, ignore them for non-SMTP channels, and attach them to `MailMessage` for SMTP delivery only.

- [ ] **Step 4: Add Flow Canvas parity**

Expose `attachments` on the notify block and keep import/export/preview behavior consistent with the new property surface.

- [ ] **Step 5: Re-run the focused tests and confirm GREEN**

Run the same focused commands from Tasks 1 and 2 and verify the new tests pass.

### Task 4: Finish docs, spec artifacts, and verification

**Files:**
- Modify: `SCRIPTING.md`
- Modify: `openspec/changes/add-notify-email-attachments/proposal.md`
- Modify: `openspec/changes/add-notify-email-attachments/tasks.md`
- Modify: `openspec/changes/add-notify-email-attachments/specs/scripting-notifications/spec.md`
- Modify: `tasks/todo.md`

- [ ] **Step 1: Document the new notify field**

Update `SCRIPTING.md` syntax/examples to show `attachments`, make the SMTP-only behavior explicit, and note that non-email channels ignore it.

- [ ] **Step 2: Keep the OpenSpec/task trackers honest**

Mirror completed work into `openspec/changes/add-notify-email-attachments/tasks.md` and the `263` review block in `tasks/todo.md`.

- [ ] **Step 3: Validate the OpenSpec change**

Run:
`cmd /c openspec validate add-notify-email-attachments --strict --no-interactive`

Expected: validation passes.

- [ ] **Step 4: Run final focused verification**

Run:
`dotnet test SSH_Helper.Tests\\SSH_Helper.Tests.csproj --filter "FullyQualifiedName~NotifyCommandTests|FullyQualifiedName~NotificationServiceTests|FullyQualifiedName~ScriptParserTests|FullyQualifiedName~ScriptAutocompleteProviderTests|FullyQualifiedName~FlowCanvasBridgeTests" -p:SkipFlowCanvasBuild=true -p:UseAppHost=false -v minimal`

Expected: all touched notify/parser/editor/bridge tests pass.
