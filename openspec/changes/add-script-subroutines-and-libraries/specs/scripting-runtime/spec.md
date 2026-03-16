## ADDED Requirements
### Requirement: Script subroutine execution
The scripting runtime SHALL support named `subroutines` that execute reusable step blocks through a `call` step.

#### Scenario: Call local subroutine with explicit outputs
- **WHEN** an executable script defines `subroutines.lookup_service`
- **AND** a `call` step invokes that subroutine with `args` and `out`
- **THEN** the runtime executes the subroutine steps in a child scope
- **AND** copies only the explicitly mapped outputs back into caller variables

#### Scenario: Imported library subroutine call
- **WHEN** an executable script imports a library file using `imports.path` and `imports.as`
- **AND** a `call` step references `alias.subroutine`
- **THEN** the imported subroutine is resolved before host execution begins
- **AND** the runtime executes the referenced library subroutine like a local subroutine

### Requirement: Subroutine-local variable isolation
Subroutine execution SHALL isolate local variables from caller scope by default.

#### Scenario: Local mutation without output binding does not leak
- **WHEN** a subroutine mutates a local variable or an input argument value
- **AND** that variable is not declared as an output and bound through `call.out`
- **THEN** the caller scope does not observe the mutation after the call completes

### Requirement: Subroutine return control flow
The scripting runtime SHALL support `return` as an early exit from the current subroutine only.

#### Scenario: Return exits current subroutine but not whole script
- **WHEN** a subroutine executes `return: true`
- **THEN** the current subroutine stops executing remaining subroutine steps
- **AND** caller script execution continues with the next step after the `call`

#### Scenario: Exit inside subroutine still exits whole script
- **WHEN** a subroutine executes an `exit` step
- **THEN** existing whole-script exit semantics remain unchanged

### Requirement: File-based library loading
The scripting runtime SHALL support file-backed reusable libraries through top-level `imports`.

#### Scenario: Import path is resolved and validated once
- **WHEN** an executable script includes one or more imports
- **THEN** the runtime validates each absolute path with existing read-path rules
- **AND** parses imported library files before host execution begins
- **AND** surfaces import failures as preflight failures instead of per-host runtime failures

### Requirement: Defensive call graph limits
The scripting runtime SHALL reject recursive call graphs and enforce a defensive maximum call depth.

#### Scenario: Recursive call graph is blocked
- **WHEN** subroutine definitions create a direct or indirect cycle
- **THEN** validation rejects the script before execution

#### Scenario: Excessive nested call depth stops execution
- **WHEN** nested calls exceed the runtime maximum call depth
- **THEN** execution fails with an explicit call-depth error
