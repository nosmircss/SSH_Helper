# localcmd Script Command Design

## Context

SSH_Helper's YAML scripting engine currently has 38 command types, all of which either operate over SSH (send, interactive) or perform local I/O (readfile, writefile, http, ping). There is no command that executes an arbitrary local process on the machine running the app.

Users need to run local commands as part of hybrid workflows: generate configs locally then push via SSH, pull output and process it with PowerShell, restart local services, run build tools, or orchestrate mixed local/remote automation. This command fills that gap.

## YAML Syntax

### YAML key: `localcmd`

### Shorthand form (PowerShell default)

```yaml
- localcmd: Get-Process | Select-Object -First 5
```

### Full form

```yaml
- localcmd:
    command: "dotnet build"
    shell: powershell              # powershell (default) or custom
    shell_path: "python"          # path to custom executable (only when shell: custom)
    args: ["-NoProfile"]          # preferred list form; scalar string also accepted
    env:
      CONFIGURATION: Release       # optional per-process environment variables
    working_dir: "C:\\Scripts"
    interactive: false             # true = launch external terminal window
    keep_open: false               # interactive only: keeps terminal open after command exits
    run_mode: foreground           # foreground (default) or background
    lifetime: detached             # background only: detached (default), script, app
    kill_on_cancel: false          # background only, applies when lifetime != detached
    fail_on_nonzero: true          # default true
    success_codes: [0]             # default [0]
    max_output_bytes: 1048576      # per stream capture cap; panel streaming still live
    confirm: once                  # always (default), once, never
    into: build                    # prefix for structured output variables
    timeout: 30                    # foreground only
    on_error: continue
```

`interactive: true` and `run_mode: background` are mutually exclusive.

### Output Variables

When `into: <prefix>` is specified and `run_mode: foreground`, three variables are created:

| Variable | Type | Description |
|---|---|---|
| `<prefix>_stdout` | string | Standard output text |
| `<prefix>_stderr` | string | Standard error text |
| `<prefix>_exit_code` | int | Process exit code |

When `into: <prefix>` is specified and `run_mode: background`, startup metadata variables are created:

| Variable | Type | Description |
|---|---|---|
| `<prefix>_pid` | int | Spawned process id (when available) |
| `<prefix>_started` | bool | `true` if process spawn succeeded |
| `<prefix>_start_error` | string | Startup error message when spawn fails |

The existing cross-cutting `capture` property captures combined stdout into a single variable (foreground mode only).

When neither `into` nor `capture` is specified, stdout is emitted to the script output panel but not stored.

## Execution Model

### Shell Wrapping

Commands are always executed through a shell to support pipes, redirects, and shell builtins.

| `shell` value | Process spawned | Arguments constructed |
|---|---|---|
| `powershell` (default) | `powershell.exe` | `-NoLogo -NonInteractive -Command "<command>"` + user `args` |
| `custom` | `shell_path` executable | user `args` + command as final argument |

`args` is normalized to `List<string>` internally:
- YAML sequence form is passed as-is.
- YAML scalar form is accepted for backward compatibility and treated as a single argument.

The `command` string has `{{variable}}` references substituted via `context.SubstituteVariables()` before execution.

### Streaming Output

Foreground mode streams output in real time using `Process.OutputDataReceived` and `Process.ErrorDataReceived` events:

- stdout lines -> `context.EmitOutput(line, ScriptOutputType.CommandOutput)`
- stderr lines -> `context.EmitOutput(line, ScriptOutputType.Warning)`

Both streams are simultaneously accumulated into `StringBuilder` buffers for final capture into `into_*` variables after the process exits.

`max_output_bytes` limits stored capture size per stream. When exceeded, capture buffers are truncated with a marker, while panel streaming continues.

### Per-Host Execution

`localcmd` runs once per host iteration (same as `send`). Host variables like `{{Host_IP}}`, `{{port}}`, and custom grid columns are available and substituted into the command.

### Run Mode

`run_mode: foreground` (default):
1. Start process.
2. Stream output.
3. Wait for process exit.
4. Apply exit-code policy.

`run_mode: background`:
1. Start process.
2. Return success immediately after spawn.
3. No wait and no stdout/stderr/exit-code capture.
4. `on_error` only applies to spawn failures.

### Process Lifetime (Background)

`lifetime` behavior for background mode:
- `detached` (default): process is not tied to script or app lifetime.
- `script`: process tree is terminated when script execution finishes or is cancelled.
- `app`: process tree is terminated on app shutdown.

`kill_on_cancel` controls cancellation cleanup for background processes when `lifetime` is `script` or `app`.

### Exit Code Policy (Foreground)

- Process success is determined by `success_codes` (default `[0]`).
- If `fail_on_nonzero: true` and exit code is not in `success_codes`, the step fails.
- If `fail_on_nonzero: false`, non-success exit codes do not fail the step.
- For non-success exit code + `on_error: continue`, return `CommandResult.Suppressed(...)` and still populate `*_exit_code`.

### Timeout

Foreground mode uses the step-level `timeout` property (seconds). If exceeded, the process and its entire process tree are killed, and the command returns `CommandResult.ApplyOnError(step, "...")`.

`timeout` is ignored in background mode.

### Cancellation

Foreground mode: when the script `CancellationToken` fires, `Process.Kill(entireProcessTree: true)` is called.

Background mode: cancellation behavior follows `lifetime` + `kill_on_cancel`.

### Interactive Mode

When `interactive: true` is set (foreground only):

1. The command is launched in an external terminal window (Windows Terminal if available, otherwise PowerShell window)
2. Script execution pauses until the terminal window is closed by the user
3. No stdout/stderr capture occurs (user interacts directly with the terminal)
4. The process exit code is still captured into `<prefix>_exit_code` if `into` is specified
5. The `title` property (optional) sets the terminal window title

```yaml
# Interactive mode example
- localcmd:
    command: "ssh-keygen"
    interactive: true
    title: "Generate SSH Key"
```

The external terminal is launched via:
- `wt.exe -d <working_dir> --title <title> -- <shell> <args> <command>` (if Windows Terminal is installed)
- Fallback: `start <shell>` with `/K` flag to keep the window open

## Security: Per-Command Confirmation

Confirmation policy is controlled by `confirm`:
- `always` (default): prompt for every `localcmd` step.
- `once`: prompt once, then auto-approve subsequent `localcmd` steps in the same script execution and current host scope.
- `never`: no prompt.

Dialog content:
- Fully resolved command text (after variable substitution)
- Shell
- Working directory

Dialog actions:
- **Run** - execute this command
- **Run All** - execute this and suppress further prompts in scope
- **Cancel** - abort the script

Run All scope rule:
- Scope = current script execution + current host.
- If resolved command text changes due to variable substitution, re-prompt unless `confirm: never`.

### Implementation

Confirmation callback is injected into `LocalCmdCommand` via `ILocalCmdConfirmation`:

```csharp
public interface ILocalCmdConfirmation
{
    Task<LocalCmdConfirmResult> ConfirmAsync(string resolvedCommand, string shell, string workingDir);
}

public enum LocalCmdConfirmResult { Run, RunAll, Cancel }
```

`ScriptExecutor` constructor is extended to make injection explicit:

```csharp
public ScriptExecutor(
    IBrowserCallbackUiHost? uiHost = null,
    ILocalCmdConfirmation? localCmdConfirmation = null)
```

`SshExecutionService` must pass the confirmation implementation in all script execution paths (manual, Flow Canvas, scheduler):
- `ExecuteScriptWithPool(...)`
- `ExecuteScriptWithoutPool(...)`
- `ExecuteScriptLocal(...)`

`ScriptContext` tracks per-run localcmd approval state for Run All suppression and command-change re-prompt logic.

## Flow Canvas Block

A new block definition in the `'io'` category in `FlowCanvas/src/blockDefs/registry.ts`:

```typescript
{
  type: 'localcmd',
  label: 'Local Command',
  category: 'io',
  icon: 'terminal',
  description: 'Run a command on the local machine',
  previewKey: 'command',
  properties: [
    { key: 'command', label: 'Command', type: 'textarea', required: true,
      placeholder: 'Get-Process | Select-Object -First 5',
      helpText: 'The command to execute locally', group: 'core' },
    { key: 'shell', label: 'Shell', type: 'select',
      options: ['powershell', 'custom'], defaultValue: 'powershell',
      helpText: 'Shell to execute the command in. "custom" enables Shell Path.', group: 'core' },
    { key: 'shell_path', label: 'Shell Path', type: 'text',
      placeholder: 'python',
      helpText: 'Path to custom shell executable (Shell=custom)', group: 'core' },
    { key: 'args', label: 'Shell Arguments', type: 'textarea',
      placeholder: '["-NoProfile"]',
      helpText: 'Prefer JSON array syntax. Scalar string still supported.', group: 'core' },
    { key: 'env', label: 'Environment (JSON)', type: 'textarea',
      placeholder: '{"CONFIGURATION":"Release"}',
      helpText: 'Optional process environment variables', group: 'core' },
    { key: 'working_dir', label: 'Working Directory', type: 'text',
      placeholder: 'C:\\Scripts', browse: 'file',
      helpText: 'Directory to run the command in', group: 'core' },
    { key: 'interactive', label: 'Interactive', type: 'boolean', defaultValue: false,
      helpText: 'Open in an external terminal window (foreground only)', group: 'core' },
    { key: 'keep_open', label: 'Keep Open', type: 'boolean', defaultValue: false,
      helpText: 'Keep terminal open after command completion (interactive only)', group: 'core' },
    { key: 'run_mode', label: 'Run Mode', type: 'select',
      options: ['foreground', 'background'], defaultValue: 'foreground',
      helpText: 'Foreground waits for completion; background returns after spawn', group: 'core' },
    { key: 'lifetime', label: 'Background Lifetime', type: 'select',
      options: ['detached', 'script', 'app'], defaultValue: 'detached',
      helpText: 'Applies only when run_mode=background', group: 'advanced' },
    { key: 'kill_on_cancel', label: 'Kill On Cancel', type: 'boolean', defaultValue: false,
      helpText: 'Applies to non-detached background mode', group: 'advanced' },
    { key: 'fail_on_nonzero', label: 'Fail On Non-Zero', type: 'boolean', defaultValue: true,
      helpText: 'Fail when exit code is not in success_codes', group: 'advanced' },
    { key: 'success_codes', label: 'Success Codes', type: 'text',
      placeholder: '0,3010',
      helpText: 'Comma-separated allowed exit codes', group: 'advanced' },
    { key: 'max_output_bytes', label: 'Max Capture Bytes', type: 'number', defaultValue: 1048576,
      helpText: 'Per-stream capture limit', group: 'advanced' },
    { key: 'confirm', label: 'Confirm Policy', type: 'select',
      options: ['always', 'once', 'never'], defaultValue: 'always',
      helpText: 'Prompt policy before execution', group: 'advanced' },
    { key: 'into', label: 'Into Prefix', type: 'text',
      placeholder: 'result',
      helpText: 'Prefix for stdout/stderr/exit_code (or pid/start metadata)', group: 'core' },
    timeoutProp,
    onErrorProp,
  ],
}
```

## Options Model

```csharp
public class LocalCmdOptions
{
    public string? Command { get; set; }
    public string Shell { get; set; } = "powershell";   // powershell | custom
    public string? ShellPath { get; set; }               // required when Shell = custom
    public List<string> Args { get; set; } = new();      // scalar YAML normalized to one item
    public Dictionary<string, string>? Env { get; set; }
    public string? WorkingDir { get; set; }
    public bool Interactive { get; set; }
    public bool KeepOpen { get; set; }                // interactive only
    public string RunMode { get; set; } = "foreground"; // foreground | background
    public string Lifetime { get; set; } = "detached";  // detached | script | app
    public bool KillOnCancel { get; set; }
    public bool FailOnNonZero { get; set; } = true;
    public List<int> SuccessCodes { get; set; } = new() { 0 };
    public int MaxOutputBytes { get; set; } = 1024 * 1024;
    public string Confirm { get; set; } = "always";     // always | once | never
    public string? Title { get; set; }                   // interactive window title
    public string? Into { get; set; }                    // variable prefix for structured output
}
```

## Files to Modify

| File | Change |
|---|---|
| `Services/Scripting/Models/ScriptStep.cs` | Add `StepType.LocalCmd`, add `LocalCmdOptions? LocalCmd`, add `LocalCmdOptions` class, add check in `GetStepType()` |
| `Services/Scripting/Commands/LocalCmdCommand.cs` | **New file** - implements `IScriptCommand` using `System.Diagnostics.Process` |
| `Services/Scripting/Commands/ILocalCmdConfirmation.cs` | **New file** - confirmation interface and result enum |
| `Services/Scripting/ScriptExecutor.cs` | Add ctor overload/dependency for `ILocalCmdConfirmation`; register `{ StepType.LocalCmd, new LocalCmdCommand(localCmdConfirmation) }` |
| `Services/Scripting/ScriptContext.cs` | Add localcmd Run All approval state for current run + host + command fingerprint checks |
| `Services/Scripting/ScriptParser.cs` | Add `localcmd` to `KnownStepKeys`, `CommandOptionKeys`, `StepRootOptionKeysByCommand`; parse list/scalar `args`, `env`, `run_mode`, `lifetime`, `kill_on_cancel`, `fail_on_nonzero`, `success_codes`, `max_output_bytes`, `confirm` |
| `Services/Scripting/ScriptDependencyAnalyzer.cs` | Add localcmd variable reference analysis (`command`, `working_dir`, `env`) and generated `into_*` variables |
| `Services/SshExecutionService.cs` | Inject confirmation implementation into `ScriptExecutor` in all execution paths (pooled, non-pooled, local) |
| `FlowCanvas/src/blockDefs/registry.ts` | Add `localcmd` block definition in `io` category |
| `Services/FlowCanvasBridge.cs` | Handle `localcmd` in YAML-to-graph and graph-to-YAML translation |

## Verification

1. **Unit tests**: `LocalCmdCommand` with mocked process runner - stdout/stderr capture, exit code policy, timeout, cancellation, shell selection.
2. **Parser tests**: shorthand + full form parse correctly, including list/scalar `args`, `env`, `success_codes`, `confirm`, `run_mode`.
3. **Exit policy test**: non-zero with `fail_on_nonzero: true` fails; with `on_error: continue` returns suppressed failure and still captures `*_exit_code`.
4. **Background mode test**: `run_mode: background` returns immediately, sets `*_pid`/`*_started`, and does not populate `*_stdout`/`*_stderr`.
5. **Lifetime tests**: background process cleanup behavior for `lifetime=detached|script|app`, including cancellation paths.
6. **Interactive test**: `interactive: true` launches external terminal and blocks until close.
7. **Confirmation tests**: `confirm=always|once|never`, Run All suppression scope is current script execution + current host, and command-text changes re-prompt unless `confirm=never`.
8. **Per-host test**: script targeting multiple hosts with `localcmd: ping {{Host_IP}}` executes once per host with correct substitution.
9. **Flow Canvas test**: add localcmd block, export YAML, verify keys/ordering; import YAML with localcmd, verify block appears.
10. **Integration test**: mixed script (`localcmd` + existing commands) succeeds in manual, Flow Canvas, and scheduler execution paths.
