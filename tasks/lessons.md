# Lessons

## 2026-04-05
- When a user provides an explicit multi-finding implementation plan, I must mirror it as checkable `tasks/todo.md` items, complete all items end-to-end, and record both focused and broader verification evidence before marking the task done.
- When sharing scripting recipes, I must verify every referenced function exists in `FunctionRegistry`/`SCRIPTING.md`; never use undocumented helpers like `random_string(...)` in runnable examples.
- When users call out workaround-heavy docs for missing primitives, I should prefer implementing the missing first-class function (with tests/docs) over keeping complex recipe workarounds.
- When a user asks for regex-like shorthand in generation APIs, prefer direct char-class shorthand support (for example `[a-zA-Z0-9@#$%^]`) over forcing long explicit character strings.
- When a user says the upstream secret schema cannot change, I must adapt Vault runtime behavior to that existing shape instead of pushing a flattening/migration workaround.
- When a user reports Vault writes still produce escaped JSON strings, I must test and harden the exact write payload coercion path (including already-escaped array/object text), not assume the first structured-value fix covers all real data states.
- When adding a new credential type, I must verify portable/non-portable isolation by using `CredentialTargets.*` target builders and add an explicit portable-prefix regression test.

## 2026-04-04
- When `localcmd` command banners can include multiline command text, I must format the displayed command with explicit line-break markers (`ScriptingHelpers.FormatForDisplay`) so output surfaces never visually merge adjacent tokens (for example `utf8notepad`).
- When a user clarifies they want output preserved in Execution Details (interactive session audit log) rather than script variables, I must shift the fix to the interactive history pipeline instead of expanding `into` capture variables.
- When interactive local terminal windows can be closed by the user (`X`), I must treat Windows close exit code `-1073741510` as a user-initiated close in interactive mode rather than a generic nonzero failure.
- When users report `keep_open` closes immediately, I must verify shell-token normalization (`powershell` vs `powershell.exe`/path variants) because keep-open logic can silently bypass if shell aliases are not recognized.
- When `localcmd` runs in `run_mode: background` with `lifetime: detached`, I must treat process-handle metadata failures (PID lookup/dispose) as non-fatal after spawn; only spawn failures should trigger step failure.
- When adding a new localcmd noise-control flag (`quiet`), I must also evaluate send-parity expectations and include `suppress` behavior for live output, not just banner suppression.
- When introducing a new command or command options, I must update autocomplete required-option metadata (`ScriptAutocompleteProvider.RequiredOptionKeysByCommand`) so required tags stay consistent with parser/runtime validation.
- When adding keep-open behavior to interactive local command launches, I must validate which process handle is actually being awaited; waiting on a terminal launcher (`wt.exe`) can break capture semantics, so keep-open modes should use a directly tracked shell process when capture/exit tracking is expected.
- When interactive `localcmd` transcript capture must be reliable, I must avoid waiting on terminal launchers (`wt.exe`) for non-keep-open `powershell`/`cmd` runs and instead track the real shell process lifetime directly.
- When changing interactive launch paths (`wt.exe` vs direct shell), I must verify `working_dir` is still honored in both branches; if `-d` is no longer used, `ProcessStartInfo.WorkingDirectory` must be set explicitly.
- When a command option is exposed across multiple modes (foreground/background/interactive), I must verify runtime behavior, dependency analysis, and docs all agree on which output variables are produced per mode.
- When adding new audit/transcript capture behavior, I must reuse the existing `InteractiveTerminalSessionDetails` history pipeline instead of inventing a separate storage model, so execution-details UX stays unified.
- When a user asks for a "full README update" after I fixed only one stale line, I must perform an end-to-end documentation sweep (features, usage, shortcuts, build prerequisites), cross-check claims against live code paths, and capture verification evidence in `tasks/todo.md` rather than shipping another narrow edit.

## 2026-04-03
- When a user reports "build fails now" after my change, I must validate with the same build command/profile they are using (including warning-as-error settings and IDE file-lock conditions) before assuming a code regression.
- When portable/self-update runs from synced folders (for example OneDrive Desktop), I must treat copy/relaunch as transiently lock-prone and add retry logic in updater scripts instead of assuming immediate file availability.
- When introducing parallel standard/portable editions, I must audit all shared OS-level namespaces in one pass (credential targets, updater temp staging paths, and scheduler ownership) instead of stopping after the first collision point.
- When users ask for YAML key order to match the Flow Canvas Properties panel, I must not rely on parser key catalogs that are alphabetically normalized; I need explicit panel-order mappings (especially for grouped Core/Advanced/On Error layouts like `playsound`).
- When a user broadens a key-order request to "all blocks," I must run an exhaustive drift check across every registry block/command pair, not stop at the one block that was just reported.
- When a user adds a documentation-follow-through correction, I must update both behavior docs and QA docs in the same implementation pass (`README`, `SCRIPTING`, harness docs, changelog), not treat docs as optional cleanup.
- When opening dialogs from Flow Canvas-hosted actions, I must set the dialog owner to the Flow Canvas window (`FlowCanvasForm`) rather than `Form1`; otherwise Windows can activate the main form and steal focus.
- When autocomplete displays `required` option tags, I must audit the required-key map against parser/runtime validation for every command family and add command-level regression tests; partial spot-fixes (for example only `into`) drift quickly and miss keys like `choose.options`.
- When handling `Enter` on whitespace-only lines in step payloads, I must support a second-enter fallback that dedents to sibling command/block indentation; otherwise users get stuck at nested option indent when trying to start the next command.
- When key-up autocomplete is enabled in the script editor, I must explicitly exclude non-text system keys (for example `Print Screen`/`Keys.Snapshot`) so screenshot/hotkey input cannot spuriously open suggestions.
- For smart-enter YAML editing, I must not treat every trailing `key:` as a nested-block start; scalar step options (for example `from:`/`into:`/`pattern:`) should keep sibling indentation unless the key is a known block-style option.
- When inferring script-step autocomplete context, I must handle both YAML list styles under `steps:` (indented `  - command` and indentless `- command`); assuming only indented style will hide `Ctrl+Space` suggestions on valid scripts.
- When users trigger manual autocomplete with `Ctrl+Space`, I must test blank-line continuation contexts separately from auto-typing behavior; provider logic needs explicit manual-request context to avoid suppressing valid `steps` suggestions.
- When step-command autocomplete applies inside `steps:` list items, I must support command suggestions even if the user has not typed `- ` yet, and completion commit should inject the missing list marker automatically.
- When inserting Windows file paths into YAML from editor tooling, I must treat double-quoted insertion sites as a special case and convert them to single-quoted values so backslashes remain literal and paths stay valid.
- When users ask about specific text-entry edge contexts (`path: ""`, `path: "`, `path: '`), I must add explicit regression coverage for each context before changing insertion logic; quote-adjacent caret behavior is easy to mis-handle without tests.
- For quote-pair placeholders like `path: ""`, I must test both caret positions (between quotes and after closing quote); caret-index assumptions around opening/closing quotes can produce malformed leading quote artifacts.
- When autocomplete popups rely on handle-based outside-click dismissal, I must treat native child HWNDs (like list scrollbar internals) as inside the popup via `IsChild(...)`; `Control.FromHandle(...)` alone can misclassify valid internal clicks and break mouse scrolling/selection.
- When validating WinForms mouse interactions that mix focus transitions and `BeginInvoke` callbacks, I must run at least one regression that processes queued UI messages between mouse-down and mouse-up; direct `SendMessage` click tests can miss real dismissal races.

## 2026-04-02
- When interactive transcript cleanup changes do not resolve autocomplete corruption, I must instrument and compare raw terminal chunks (`RawData`), terminal-stripped chunks (`StrippedData`), and captured transcript chunks before attempting another behavior fix.
- When terminal output can include ANSI cursor rewrite sequences, transcript assembly must process the raw chunk stream statefully; appending stripped chunk fragments will concatenate autocomplete candidates into invalid commands.

## 2026-04-01
- When fixing interactive terminal selection rendering bugs, I must verify full selection workflow parity (including scroll-while-selecting and cross-scroll copy behavior), not just remove the immediate visual artifact.
- When cleaning interactive terminal audit transcripts, I must apply cursor-aware terminal normalization for backspace/tab-autocomplete edits instead of stripping control characters directly.

## 2026-03-30
- When a secondary editor surface (Flow Canvas) mirrors preset command text from `Form1`, I must wire synchronization into the shared preset-load path (`LoadPresetIntoEditor`) rather than only the one-time window-open path.

## 2026-03-25
- When giving Flow Canvas YAML expectations, I must verify against actual bridge/export output before answering; `if` containers can flatten when `_yamlSnippet` takes precedence over graph branch metadata.

## 2026-03-23
- When handling debug resume actions in `ScriptExecutor`, `Step` must explicitly enable `DebugState.StepMode`; otherwise stepping from a breakpoint degenerates into full-run continue behavior.
- When rendering Flow Canvas Vars inspector entries, any password/secret/token/key-named variable must be masked at display time so raw credential values are never shown in the UI.
- When debug pauses can happen concurrently (for example parallel branches), `DebugState.WaitForResumeAsync` must not replace the active resume signal per waiter; all current waiters must share the same signal or one branch can remain blocked forever.
- When auditing Flow Canvas properties-panel reliability, I must explicitly include `select`/dropdown controls in the same root-cause pass as text inputs; first-interaction persistence can fail independently from typing behavior.
- When rendering Flow Canvas node previews, I must treat `props._preview` as import metadata only; live block text must come from canonical editable props (`previewKey`) to avoid stale on-canvas text after property edits.

## 2026-03-20
- When a Flow Canvas breakpoint must pause on the first block, I must apply node-map and breakpoint state synchronously before `ScriptExecutor.ExecuteAsync` starts; async polling bootstrap can miss step `steps/0`.
- When Flow Canvas export mixes regenerated simple steps with stored container `_yamlSnippet` blocks, I must normalize top-level indentation before run/test so a no-edit canvas run cannot rewrite valid YAML into an invalid mixed-indent `steps` list.
- When a user points out that a cell-level WinForms status cue disappears under selection, I must look for a non-selected-owned surface such as the row header instead of continuing to fight the selected-cell paint path.
- When I smooth a WinForms add-preset tree mutation, I must verify the new selection is fully visible in the actual viewport instead of only preserving `TopNode`; a preserved anchor is not enough if the inserted row ends up clipped below the fold.
- When I create a new preset in `Form1`, I must route the post-create load through the same preset-loading path as ordinary selection changes; hand-populating the editor can skip required environment-restore logic.
- When I fix full-visibility for one preset-tree insertion path in WinForms, I must audit the matching undo/restore path in the same pass; undelete uses the same viewport-sensitive selection pattern and can regress independently if I only patch add.
- When a WinForms tab-strip flicker survives buffered overlays and `WM_ERASEBKGND` fixes, I must remove the native tab header from the visible surface entirely; as long as native `TabControl` chrome is still what the user sees, repaint flashes can survive any seam patch around it.
- When a user still sees WinForms flicker specifically in the tab-header gap beside the last tab, I must inspect `WM_ERASEBKGND` for the tab control itself; buffering nearby panels is not enough if the header gap stays unpainted until the post-paint seam patch.
- When a WinForms dark-mode flicker survives earlier header buffering, I must inspect runtime-created panels and `UseVisualStyleBackColor` on the tab pages themselves; raw child panels and themed tab-page background erase can still flash even when the tab strip overlay looks fixed.
- When a WinForms tab control looks correct in an isolated managed-paint test but still shows native seams at runtime, I must treat that as a paint-order bug and look for a buffered post-`WM_PAINT` overlay path instead of continuing to tune only the managed `Paint` rectangles.
- When a user still sees WinForms tab-strip flicker after I remove an extra managed handler, I must inspect any remaining direct `Graphics.FromHwnd` or post-`WM_PAINT` overdraw next; buffered event wiring alone does not eliminate flicker if a control still patches pixels after native paint.
- When a custom WinForms tab control already owns its border/header overlay in `WndProc`, I must not also layer a form-level `Paint` handler onto that same control; the duplicate overlay can survive broader buffering fixes as a small residual flicker around the tab strip.
- When I verify WinForms repaint batching, I should not assert absolute `Invalidated` counts across a font change unless I control the framework noise; the safer contract is to compare the counts before and after the explicit follow-up call.
- When I add WinForms handle/paint regression tests, I must avoid showing a real top-level `Form` unless visibility is part of the behavior under test; otherwise the test can leak a blank desktop window during runs.
- When a user reports multi-window callback focus still falls through to another app, I must verify the modeless close path explicitly restores activation to the main SSH Helper form; fixing owner selection and modal flicker alone is not enough.
- When I keep a modal browser surface open after success, I must also update its affordances and completion styling; leaving the footer button as `Cancel` and the embedded HTML unthemed creates a misleading, half-finished UX.
- When a user names an option by the visible behavior they expect, I must verify the entire user-facing surface, not just one internal layer; `auto_close_browser: false` was incomplete when the page stayed open but the host WebView2 window still auto-closed.
- When I hand off a self-contained `browser_callback_capture` preset, I must state explicitly that the command itself starts the temporary localhost listener before opening the browser; users should not have to infer that from the `start_url` alone.
- When I change WinForms/browser callback focus restoration, I must verify the real interactive foreground result, not just native-call ordering in a unit test; Windows activation behavior can still regress even when the API sequence looks stronger on paper.
- When I wrap Windows P/Invoke calls in helper names like `NativeIsIconic`, I must explicitly set `EntryPoint` (or keep the extern name exact) and add a test for the import mapping; mocked focus tests will not catch missing exports and the bug will surface only at runtime.
- When I add a new script option that changes live browser-launch behavior, I must verify the end-to-end preset execution path in the actual app, not just parser/command unit tests; otherwise the UI can still behave like the old path and I will miss it until the user tries it manually.
- When I add a buffered WinForms container wrapper, I must make background-erase suppression conditional on a clearly opaque, fully-owned surface; unconditional `WM_ERASEBKGND` suppression is too broad for a reusable control.
- When I verify WinForms/browser-callback repaint behavior, I must not run multiple UI-heavy `dotnet test` processes in parallel; shared activation state and visible-form cleanup can create false failures that disappear on a serial rerun.
- When I keep a browser callback window modeless to avoid modal close flicker, I must not disable the entire owner form as a substitute lock; that broad disabled-state repaint can blank labels and reintroduce whole-form flicker during launch.

## 2026-03-15
- When a user narrows a popup-ownership cleanup to allow some ownerless dialogs, I must preserve explicit exceptions for startup/global flows instead of force-owning every modal call site.
- When I add or adjust a modeless dialog launched from `Form1`, I must verify the close path explicitly restores activation to the main window instead of assuming WinForms ownership will do it automatically.
- When I restore owner activation from a modeless dialog close path, I must verify the timing on the UI thread; an unnecessary deferred `BeginInvoke` can cause visible focus flicker by letting another app activate briefly first.
- When I reason about WinForms `TreeView` display order, I must not use `TreeNode.IsVisible` as a proxy for logical tree visibility; it is viewport-dependent and can break adjacent-node selection rules.
- When I preserve tree expansion state during WinForms reselection, I must gate on collapsed ancestors, not on `TreeNode.IsVisible`; off-screen root nodes are still valid selection targets.
- When I rebuild a WinForms `TreeView` to preserve selection, I must also preserve `TopNode` while redraw is suspended or users will see a jump-to-top/jump-back flicker.
- When deleting a single item from a WinForms `TreeView`, I should prefer in-place node removal over clearing and rebuilding the whole tree; full rebuilds are prone to scroll-state regressions and visible flicker.

## 2026-03-13
- When a user narrows status-bar progress behavior, I must encode the exact simplification they asked for instead of preserving extra host/preset detail from the earlier plan.
- When I drive a WinForms status bar from `Progress<T>`, I must guard late UI-thread callbacks with a run token and confirm the exact visibility threshold so 1x1 runs do not show a pointless progress bar.

## 2026-03-12
- When a user asks for clearer QA preset prerequisites or execution expectations, I must encode those details directly in each preset `description` instead of assuming the preset name or folder is enough context.
- When I expand the scripting runtime to accept new expression forms, I must update `ScriptDependencyAnalyzer` in the same pass or the missing-column preflight will drift and flag expression text as fake grid columns.
- When a scripting surface accepts plain scalar text as a valid literal value, I must align missing-column analysis with the runtime resolver and avoid treating arbitrary words in that scalar as expression tokens.
- When a user cancels a script-driven file picker and asks for the script to stop, I must model that as a real script cancellation or exit path, not a suppressible step error routed through `on_error`.

## 2026-03-09
- When a user narrows a follow-up implementation to a specific subset of review findings, I must implement only that approved scope and drop adjacent enhancements I suggested on my own.
- When I swap a single-row WinForms input for a multi-line help label, I must reflow the rows beneath it and add a layout regression test instead of assuming the old fixed `y` offsets still hold.

## 2026-03-08
- When scheduler lifecycle notifications share the same pane as live host output, I should remove or relocate them instead of mixing them into the merged output stream.
- When a user reports scheduler history flooding with the same repeated failure, I should collapse consecutive identical failures in persisted history instead of only tweaking the grid presentation.
- When a user says a scheduler safeguard is confusing, I should prefer a save-time explanation over a hidden blocked-state flag or secondary recovery workflow.
- When I add a new save-time warning to an existing preset workflow, I must preserve the existing diff/context the user relies on instead of replacing it with a narrower confirmation dialog.

## 2026-03-06
- If I run verification with custom output paths inside the repo, I must either clean those generated folders or exclude them from compile globs before handing off.
- Before saying testing is complete, I must run at least one normal `dotnet build` for the touched project, not only a workaround-based test command.
- If verification required special build flags, I must say that explicitly and explain whether the normal build path also passes.
- When a user corrects UI indicator behavior, I must capture the exact visibility rule instead of assuming the indicator should always be visible.
- When I add a nested context-menu command in WinForms, I should verify primary click behavior explicitly instead of assuming submenu expansion works acceptably by default.
- When a user reports a WinForms menu still does nothing after a UI patch, I should replace the fragile interaction model instead of iterating on the same submenu assumption.
- When I launch follow-up UI from a WinForms context-menu command, I should use a regular dialog or another non-menu surface instead of opening a second `ContextMenuStrip` inside the active menu lifecycle.
- When I show inherited configuration in a details pane, I should include the source scope and refresh the current selection when related environment state changes, otherwise different folders can look unchanged.
- When a WinForms TreeView uses custom click handling for full-row selection, I should not rely on `AfterSelect` alone to refresh detail panes; I need a click-path fallback for folder nodes.
- When a read-only custom editor is reused as a details pane, I must ensure programmatic `Text` and `Clear()` operations temporarily bypass read-only or subsequent detail refreshes will silently fail.
- When a manual switch updates both the active environment and the base environment, I must refresh folder-detail UI after the final base-environment write, not only from the earlier environment-changed event.
- When a user corrects autocomplete scope, I should encode the exact context boundary they asked for, not flatten it into a broader suppression rule; header-area and post-section behavior may need different completion rules.
- When a user says autocomplete still leaks after a scope fix, I must verify popup lifecycle on caret movement as well as provider filtering; stale visible suggestions can survive even when fresh completion results are already correct.
- When a user corrects autocomplete scope again, I must update the manual invocation rules too; preserving `Ctrl+Space` behavior from an earlier assumption can still violate the real boundary they want.
- When a user reports Tab behavior on a blank editor line, I must verify trailing-newline indexing specifically; helpers that enumerate line starts often collapse the final blank line back onto the previous content line.
- When adding a preset dirty indicator, I must place it in the active editor header the user sees while typing, not only in the presets tree pane.
- When drafting new OpenSpec changes around already-implemented work, I should not anchor the proposal set to an older active change unless the user explicitly wants to keep that dependency; if the user wants fresh proposals, I should frame them as standalone changes.
- When a user says parity should include the look-and-feel as well as behavior, I should extend the spec/tasks to include visual parity explicitly instead of assuming behavioral parity is enough.
