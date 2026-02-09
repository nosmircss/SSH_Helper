## Context
Script authors currently switch mental models between inline scalar commands (`send: ...`) and nested map commands (`http:`, `log:`). This inconsistency causes friction in smart-enter flows, key completion expectations, and option discoverability. Because scripts are not yet widely deployed, we can take a hard-breaking format cleanup now and align docs/presets at the same time.

## Goals / Non-Goals
- Goals:
  - Define one canonical command payload shape for authoring and parsing.
  - Support a concise shorthand for a small set of single-primary-field commands.
  - Improve editor ergonomics around option entry after command payload lines.
  - Keep runtime semantics unchanged; only syntax/authoring contracts change.
- Non-Goals:
  - Support shorthand for every command type.
  - Redesign command runtime behavior or expression language semantics.

## Decisions
- Decision: Canonical nested map payloads for converted commands
  - Convert historically inline commands to explicit map payloads with required keys.
  - Rationale: one structural pattern reduces mental load and autocomplete branching.

- Decision: Explicit primary payload keys
  - Use semantically named primary keys (`command`, `message`, `seconds`, `expression`, `condition`, `iterator`) rather than generic `value`.
  - Rationale: improves readability and self-documentation in scripts.

- Decision: Keep canonical map shape as the internal contract, with explicit shorthand aliases
  - Parser accepts shorthand only for commands with one obvious primary field:
    - `send`, `print`, `wait`, `set`, `log`, `if`, `foreach`, `while`, `exit`
  - Commands with richer payload contracts remain map-only.
  - Rationale: improves readability and typing speed while retaining one canonical option vocabulary and avoiding broad parser ambiguity.

- Decision: Smart-enter defaults to option continuation
  - Enter on command payload lines keeps option-level indentation; `Ctrl+Enter` explicitly creates a sibling step.
  - Rationale: matches the most common follow-up action after entering payload text (add options), while preserving fast next-step flow.

## Canonical Payload Shape
- `send`: `command` (+ optional `capture`, `suppress`, `expect`, `timeout`, `on_error`)
- `print`: `message`
- `wait`: `seconds`
- `set`: `expression`
- `exit`: `status` (optional) + `message` (optional)
- `if`: `condition`, `then`, `elif`, `else`
- `foreach`: `iterator`, `when`, `do`
- `while`: `condition`, `max_iterations`, `do`
- `try`: `do`, `catch`, `finally`
- Existing map commands (`log`, `http`, `ping`, `dns`, `portcheck`, `sftp`, `webhook`, `readfile`, `writefile`, `input`, `updatecolumn`, `updateenvironment`, `parse`, `extract`) remain map-based; key expectations are formalized/cleaned where needed.

## Supported Shorthand Aliases
- `send: <command>` -> `send.command`
- `print: <message>` -> `print.message`
- `wait: <seconds>` -> `wait.seconds`
- `set: <expression>` -> `set.expression`
- `log: <message>` -> `log.message`
- `if: <condition>` -> `if.condition`
- `foreach: <iterator>` -> `foreach.iterator`
- `while: <condition>` -> `while.condition`
- `exit: <message>` -> `exit.status=success`, `exit.message=<message>`
- `exit: failure "<message>"` / `exit: success "<message>"` / `exit: error "<message>"` remain accepted shorthand prefixes.

## Risks / Trade-offs
- Risk: Breaking existing sample/preset content during transition.
  - Mitigation: update `SCRIPTING.md`, `qa_presets.json`, and sample scripts in the same change.
- Risk: Parser/model churn across many command entry points.
  - Mitigation: staged parser tests per command plus focused regression runs.
- Risk: Keyboard-behavior change surprise for users used to current Enter behavior.
  - Mitigation: add explicit `Ctrl+Enter` next-step affordance and document in editor section.

## Migration Plan
1. Implement parser/model updates for canonical command maps.
2. Add validation diagnostics for legacy inline syntax with replacement examples.
3. Update editor smart-enter and `Ctrl+Enter` logic.
4. Update docs/presets/samples to canonical format.
5. Run runtime/editor validation suites and manual smoke checks.

## Open Questions
- None.
