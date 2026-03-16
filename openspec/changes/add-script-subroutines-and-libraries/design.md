## Context
The scripting runtime currently executes one flat `steps` tree with shared mutable variables. Recent collection improvements reduced boilerplate inside loops and conditions, but they did not address repeated step blocks. Adding reusable blocks crosses the parser, runtime, static analyzer, and editor surfaces, so the execution contract has to be explicit.

## Goals / Non-Goals
- Goals:
  - Add a reusable step abstraction for large scripts.
  - Keep caller/callee variable flow explicit and predictable.
  - Support file-backed library reuse without introducing preset/config dependencies.
- Non-Goals:
  - Macro expansion.
  - Relative imports.
  - Nested imports.
  - Expression-level function calls.
  - A map/filter DSL.

## Decisions
- Decision: Use `subroutines` plus `call` instead of macros.
  - Rationale: explicit args/outputs and runtime scope are easier to validate and reason about than text expansion.
- Decision: Use child variable scopes with explicit output bindings.
  - Rationale: avoids accidental global mutation and keeps reuse contracts visible at the call site.
- Decision: Require absolute import paths and file-backed libraries only in v1.
  - Rationale: scripts are usually stored as preset text, so there is no reliable relative source location to resolve from.
- Decision: Keep `call` map-only in v1.
  - Rationale: reduces parser ambiguity and keeps validation straightforward.
- Decision: Add `return` as subroutine-only control flow; keep `exit` as whole-script termination.
  - Rationale: preserves existing `exit` semantics while giving reusable blocks an early-exit primitive.

## Risks / Trade-offs
- Imported library loading adds new preflight failure modes.
  - Mitigation: resolve and validate imports once before host execution and surface clear validation errors.
- Scope isolation could surprise authors expecting global mutation.
  - Mitigation: require explicit `outputs` plus `call.out` binding and document the contract with examples.
- Static analysis could drift from runtime semantics.
  - Mitigation: treat caller-side `call.args` as the only external dependency source and add focused analyzer tests.

## Migration Plan
No migration is required. Existing scripts remain valid because all syntax additions are additive and opt-in.
