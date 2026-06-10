using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Services;

/// <summary>
/// Guards the runtime correlation invariant the Flow Canvas debug bridge depends on: every step
/// path the executor emits at runtime MUST be resolvable in the node↔stepPath maps the canvas
/// builds (fresh from YAML and via graph export). If they ever diverge, per-step highlight and
/// per-block output silently die from the divergence point on (the original "neon stops in the
/// loop" bug). Uses the real "syslog restart" preset (nested foreach + if).
/// </summary>
public class FlowCanvasStepPathParityTests
{
    private const string SyslogYaml = """
compact_errors: true
steps:
- multiselect:
    title: Choose Syslogd
    prompt: Choose syslog daemon
    options:
    - syslogd
    - syslogd2
    into: syslog_instance
    min: 1

- foreach:
    iterator: syslogd in syslog_instance
    do:
      - print:
          message: Processing {{syslogd}}
      - send:
          command: config log {{syslogd}} setting
      - send:
          command: show
          capture: current_settings
          suppress: true
      - parse:
          from: current_settings
          into: current_parsed
          format: fortigate
      - set:
          expression: syslog_status = json.get(current_parsed, "log.{{syslogd}}.setting.status", "disable")
      - if:
          condition: syslog_status == "enable"
          then:
            - send:
                command: set status disable
            - if:
                condition: port != "514"
                then:
                  - send:
                      command: y
            - print:
                message: '{{syslogd}} Restarted'
          else:
            - print:
                message: Nothing to do, {{syslogd}} is already disabled
""";

    [Fact]
    public void EveryRuntimeStepPath_IsResolvable_InBothCanvasMaps()
    {
        var bridge = new FlowCanvasBridge();
        var parser = new ScriptParser();

        // Authoritative runtime paths: exactly what ScriptExecutor.AssignStepPaths emits.
        var runtimePaths = RuntimePaths(parser, SyslogYaml);

        // Map built fresh from the YAML (run path with no cached map).
        var freshPaths = new HashSet<string>(bridge.BuildNodeIdToStepPathMap(SyslogYaml).Values, StringComparer.Ordinal);

        // Map built via graph export (run path when the canvas has applied its graph).
        var (nodes, edges) = bridge.TextToGraph(SyslogYaml);
        var export = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });
        var exportPaths = new HashSet<string>(export.NodeToStepPathMap.Values, StringComparer.Ordinal);

        // The exported YAML must yield the same runtime paths (structure round-trips).
        var reExportRuntime = RuntimePaths(parser, export.Yaml ?? "");

        var freshMissing = runtimePaths.Where(p => !freshPaths.Contains(p)).ToList();
        var exportMissing = reExportRuntime.Where(p => !exportPaths.Contains(p)).ToList();

        Assert.True(freshMissing.Count == 0, $"Fresh map missing runtime paths: {string.Join(", ", freshMissing)}");
        Assert.True(exportMissing.Count == 0, $"Export map missing runtime paths: {string.Join(", ", exportMissing)}");
    }

    private static List<string> RuntimePaths(ScriptParser parser, string yaml)
    {
        var script = parser.Parse(yaml);
        var assign = typeof(ScriptExecutor).GetMethod("AssignStepPaths", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AssignStepPaths not found");
        assign.Invoke(null, new object[] { script.Steps, "steps" });
        var acc = new List<string>();
        Collect(script.Steps, acc);
        return acc;
    }

    private static void Collect(List<ScriptStep> steps, List<string> acc)
    {
        foreach (var step in steps)
        {
            if (step.StepPath != null) acc.Add(step.StepPath);
            if (step.Then is { Count: > 0 }) Collect(step.Then, acc);
            if (step.Else is { Count: > 0 }) Collect(step.Else, acc);
            if (step.Elif != null)
                foreach (var b in step.Elif)
                    if (b.Then is { Count: > 0 }) Collect(b.Then, acc);
            if (step.Do is { Count: > 0 }) Collect(step.Do, acc);
            if (step.Try is { Count: > 0 }) Collect(step.Try, acc);
            if (step.Catch is { Count: > 0 }) Collect(step.Catch, acc);
            if (step.Finally is { Count: > 0 }) Collect(step.Finally, acc);
            if (step.Cases != null)
                foreach (var c in step.Cases)
                    if (c.Do is { Count: > 0 }) Collect(c.Do, acc);
            if (step.Parallel?.Steps is { Count: > 0 })
                foreach (var bs in step.Parallel.Steps)
                    Collect(new List<ScriptStep> { bs }, acc);
        }
    }
}
