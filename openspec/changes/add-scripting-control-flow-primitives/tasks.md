## 1. Model and parser
- [x] 1.1 Add new step model members and step types for control-flow primitives
- [x] 1.2 Parse `break`, `continue`, `elif`, and `try/catch/finally`
- [x] 1.3 Validate loop-only usage for `break` and `continue`

## 2. Runtime execution
- [x] 2.1 Register and implement `BreakCommand` and `ContinueCommand`
- [x] 2.2 Extend `IfCommand` to evaluate ordered `elif` branches
- [x] 2.3 Implement `TryCommand` for `try/catch/finally` flow
- [x] 2.4 Ensure control-flow signals propagate correctly through nested blocks

## 3. Verification
- [x] 3.1 Add parser tests for all new control-flow syntaxes
- [x] 3.2 Add executor tests for break/continue behavior and branch selection
- [x] 3.3 Add try/catch/finally execution tests
