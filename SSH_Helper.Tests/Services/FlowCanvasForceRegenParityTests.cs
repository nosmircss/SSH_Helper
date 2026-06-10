using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Models;
using Xunit;
using Xunit.Abstractions;

namespace SSH_Helper.Tests.Services;

// Reproduces the user's post-fix break: wiring a block to the inner if's `continue` handle then
// deleting it leaves _stepPath contiguous (the React renumber works) BUT sets _forceGraphExport on
// the foreach, pushing it down the graph-regeneration export path. This test forces that path and
// checks the runtime node->stepPath map still matches the paths the executor assigns to the EMITTED
// YAML. If it diverges, regeneration (not _stepPath staleness) is the real defect.
public class FlowCanvasForceRegenParityTests
{
    private readonly ITestOutputHelper _out;
    public FlowCanvasForceRegenParityTests(ITestOutputHelper output) => _out = output;

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
            - send:
                command: end
                expect: Do you want to continue\? \(y/n\)
            - if:
                condition: port != "514"
                then:
                  - send:
                      command: y
            - send:
                command: config log {{syslogd}} setting
            - send:
                command: set status enable
            - if:
                condition: interface_select_method == "specify"
                then:
                  - send:
                      command: set interface {{interface}}
            - send:
                command: end
                expect: Do you want to continue\? \(y/n\)
            - send:
                command: y
            - print:
                message: '{{syslogd}} Restarted'
          else:
            - print:
                message: Nothing to do, {{syslogd}} is already disabled
""";

    [Fact]
    public void ForcingForeachRegeneration_KeepsMapMatchingEmittedYaml()
    {
        var bridge = new FlowCanvasBridge();
        var parser = new ScriptParser();

        var (nodes, edges) = bridge.TextToGraph(SyslogYaml);

        // Simulate the post-edit state: the connect flagged the foreach for graph regeneration.
        foreach (var n in nodes.OfType<JObject>())
        {
            if (string.Equals(n["data"]?["blockType"]?.ToString(), "foreach", StringComparison.OrdinalIgnoreCase))
            {
                var props = (JObject)(n["data"]!["props"] ??= new JObject());
                props["_forceGraphExport"] = true;
            }
        }

        var export = bridge.ExportGraphToYaml(new JObject { ["nodes"] = nodes, ["edges"] = edges });
        Assert.True(export.Success, "Export failed: " + string.Join("; ", export.Errors));

        var mapValues = new HashSet<string>(export.NodeToStepPathMap.Values, StringComparer.Ordinal);

        var script = parser.Parse(export.Yaml ?? "");
        var assign = typeof(ScriptExecutor).GetMethod("AssignStepPaths", BindingFlags.NonPublic | BindingFlags.Static)!;
        assign.Invoke(null, new object[] { script.Steps, "steps" });
        var runtime = new List<string>();
        Collect(script.Steps, runtime);

        var missing = runtime.Where(p => !mapValues.Contains(p)).ToList();

        if (missing.Count > 0)
        {
            _out.WriteLine("=== EMITTED YAML ===");
            _out.WriteLine(export.Yaml);
            _out.WriteLine("=== MAP VALUES ===");
            foreach (var v in mapValues.OrderBy(x => x, StringComparer.Ordinal)) _out.WriteLine("  " + v);
        }

        Assert.True(missing.Count == 0, $"Regeneration emitted {missing.Count} runtime paths the map can't resolve: {string.Join(", ", missing)}");
    }

    private static void Collect(List<ScriptStep> steps, List<string> acc)
    {
        foreach (var step in steps)
        {
            if (step.StepPath != null) acc.Add(step.StepPath);
            if (step.Then is { Count: > 0 }) Collect(step.Then, acc);
            if (step.Else is { Count: > 0 }) Collect(step.Else, acc);
            if (step.Elif != null) foreach (var b in step.Elif) if (b.Then is { Count: > 0 }) Collect(b.Then, acc);
            if (step.Do is { Count: > 0 }) Collect(step.Do, acc);
            if (step.Try is { Count: > 0 }) Collect(step.Try, acc);
            if (step.Catch is { Count: > 0 }) Collect(step.Catch, acc);
            if (step.Finally is { Count: > 0 }) Collect(step.Finally, acc);
            if (step.Cases != null) foreach (var c in step.Cases) if (c.Do is { Count: > 0 }) Collect(c.Do, acc);
            if (step.Parallel?.Steps is { Count: > 0 }) foreach (var bs in step.Parallel.Steps) Collect(new List<ScriptStep> { bs }, acc);
        }
    }
}
