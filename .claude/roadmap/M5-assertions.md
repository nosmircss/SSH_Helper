# M5: Test Assertions (Future)

**Status**: NOT STARTED | **Design**: Sketch only (needs full design before implementation)

**Why**: Completes the "Postman for SSH" story. Assert expected output, get pass/fail reports per host. Combined with the scheduler (M4), this enables automated monitoring and compliance checking.

**Dependencies**: Best implemented after M2 (multi-protocol) and M4 (scheduler) — assertions in scheduled multi-protocol workflows.

---

## Progress Checklist

- [ ] Design full assertion syntax and options
- [ ] Create `Services/Scripting/Commands/AssertCommand.cs`
- [ ] Add `Assert` StepType and options to `ScriptStep.cs`
- [ ] Update `ScriptParser.cs` and `ScriptExecutor.cs`
- [ ] Add assertion result tracking to `ExecutionResult` or new model
- [ ] Build assertion summary view in output/history
- [ ] Add visual pass/fail indicators (green/red)
- [ ] Write tests for assertion evaluation
- [ ] Manual smoke test: assertions in scheduled multi-protocol workflow

---

## Concept Sketch

### YAML Syntax

```yaml
# Simple assertion
- assert: "${status} contains 'OK'"

# Assertion with custom message
- assert:
    condition: "${firmware_version} matches '^7\\.'"
    message: "Firmware must be version 7.x"

# Assertion on HTTP status
- http:
    url: "https://api.example.com/health"
    into: health
- assert: "${health_status} == 200"

# Multiple assertions in a check block
- assert:
    condition: "${ping_result} == success"
    message: "Host must be reachable"
- assert:
    condition: "${https_check} == open"
    message: "HTTPS port must be open"
- assert:
    condition: "${firmware_version} != ''"
    message: "Firmware version must be detected"
```

### Result Model

```csharp
public class AssertionResult
{
    public string Condition { get; set; }
    public string? Message { get; set; }
    public bool Passed { get; set; }
    public string? ActualValue { get; set; }  // For debugging failures
    public int StepNumber { get; set; }
}
```

### Per-Host Summary

```
Host 192.168.1.1: 5/5 passed ✓
Host 192.168.1.2: 4/5 passed ✗
  FAILED: Firmware must be version 7.x (got: "6.4.15")
Host 192.168.1.3: 3/5 passed ✗
  FAILED: HTTPS port must be open (got: "closed")
  FAILED: Firmware version must be detected (got: "")
```

### Behavior

- Assertions use the existing `ExpressionEvaluator` for condition evaluation
- Failed assertions are logged but do NOT stop execution (collect all results)
- Optional `fail_fast: true` to stop on first failure
- Results attached to `ExecutionResult` for display in history
- Visual indicators: green checkmark for all-pass, red X with failure count

### Scheduler Integration (M4)

- Scheduled jobs with assertions auto-generate pass/fail summaries
- Notifications include assertion results: "Health Check: 48/50 hosts passed (2 failed)"
- Failed hosts listed in notification detail

---

## Open Questions (To Resolve Before Implementation)

1. Should `assert` be a hard fail (exit script) or soft fail (log and continue)?
   - Leaning: soft fail by default, with `fail_fast: true` option
2. Should assertion results be stored separately from regular output?
   - Leaning: yes, as structured data alongside output text
3. Should there be an `expect` alias for `assert` (more natural for some users)?
   - Leaning: yes, both work identically
4. Should we support assertion groups/suites for organized reporting?
   - Leaning: defer to later iteration
