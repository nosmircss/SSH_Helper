## ADDED Requirements

### Requirement: Destructive array removal semantics
The scripting JSON function API SHALL treat `json.pop(arrayRef)` and `json.shift(arrayRef)` as destructive operations that remove and return an element from the referenced array variable.

#### Scenario: pop removes last element
- **WHEN** a script executes `set: last = json.pop(arr)` and `arr` references a JSON array with at least one element
- **THEN** `last` receives the previous last element value
- **AND** `arr` is updated to the same array without its previous last element

#### Scenario: shift removes first element
- **WHEN** a script executes `set: first = json.shift(arr)` and `arr` references a JSON array with at least one element
- **THEN** `first` receives the previous first element value
- **AND** `arr` is updated to the same array without its previous first element

#### Scenario: empty array removal is safe
- **WHEN** a script executes `json.pop(arr)` or `json.shift(arr)` and `arr` is an empty JSON array
- **THEN** the function returns `null`
- **AND** `arr` remains unchanged

### Requirement: Non-destructive first/last accessors
The scripting JSON function API SHALL provide non-destructive accessors for first/last array elements.

#### Scenario: last returns last element without mutation
- **WHEN** a script executes `set: last = json.last(arr)` and `arr` references a non-empty JSON array
- **THEN** `last` is set to the last element value
- **AND** `arr` remains unchanged

#### Scenario: first returns first element without mutation
- **WHEN** a script executes `set: first = json.first(arr)` and `arr` references a non-empty JSON array
- **THEN** `first` is set to the first element value
- **AND** `arr` remains unchanged

### Requirement: Writable-target enforcement for destructive removal
Destructive array removal functions SHALL only mutate writable top-level variable references (`arr` or `${arr}`).

#### Scenario: non-writable expression is rejected for pop/shift
- **WHEN** a script executes `json.pop(<expression>)` or `json.shift(<expression>)` where the argument is not a writable top-level variable reference
- **THEN** the function returns `null`
- **AND** emits a warning instructing the user to use `json.last/json.first` for non-destructive reads
- **AND** no source array is mutated

