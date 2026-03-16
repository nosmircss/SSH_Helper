# Change: Update scripting collection ergonomics

## Why
Collection-heavy scripts currently require verbose manual loops for membership checks, dedupe, normalization, and dynamic collection setup. Operators can already store static lists in `vars:`, but the runtime lacks the collection operators and helpers needed to make real-world scripts concise.

## What Changes
- Add collection membership operators `in` and `not in` for conditions.
- Add collection-focused expression helpers: `list`, `compact`, `distinct`, `push_unique`, `trim_all`, `lower_all`, and `upper_all`.
- Let `foreach` resolve collection expressions instead of only named variables.
- Normalize collection semantics so `.length`, `length()`, emptiness, and truthiness use structural collection behavior for lists and JSON containers.
- Update scripting documentation and a bundled sample to show the preferred collection style, including YAML `vars:` lists.

## Impact
- Affected specs: `scripting-runtime`, `scripting-expressions`
- Affected code: `Services/Scripting/ValueResolver.cs`, `Services/Scripting/ExpressionEvaluator.cs`, `Services/Scripting/Commands/SetCommand.cs`, `Services/Scripting/Commands/ForeachCommand.cs`, docs/tests/sample scripts
