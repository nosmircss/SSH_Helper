using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using ScintillaNET;
using SSH_Helper.Models;
using SSH_Helper.Services.Editor;
using SSH_Helper.UI;
using Xunit;
using Xunit.Abstractions;

namespace SSH_Helper.Tests.UI;

public class ScintillaScriptEditorPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public ScintillaScriptEditorPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [WinFormsFact]
    public void ReferenceProfile_MeetsLatencyBudgets()
    {
        using var control = new ScintillaScriptEditorControl();
        using var validationService = new ScriptEditorValidationService();
        control.SetAutocompleteProvider(new ScriptAutocompleteProvider(() => Array.Empty<string>()));
        control.SetSyntaxHighlighter(new YamlSshSyntaxHighlighter());
        control.SetValidationService(validationService);
        control.ApplyCommandEditorSettings(new CommandEditorSettings
        {
            EnableAutocomplete = true,
            AutocompleteShowOnTyping = true,
            EnableInlineValidation = true,
            EnableSmartEnter = true,
            ShowInlineWarnings = true
        });

        var editor = GetInnerEditor(control);
        var script500 = BuildScript(lineCount: 500);
        control.Text = script500;

        var keystrokeSamples = MeasureKeystrokeLatencies(editor, iterations: 160);
        var completionSamples = MeasureCompletionLatencies(control, iterations: 140);
        var eofEnterSamples = MeasureEofEnterLatencies(control, editor, iterations: 120);

        var keystrokeP95 = Percentile(keystrokeSamples, 0.95);
        var completionP95 = Percentile(completionSamples, 0.95);
        var eofEnterP95 = Percentile(eofEnterSamples, 0.95);

        _output.WriteLine($"keystroke p95: {keystrokeP95:F2} ms");
        _output.WriteLine($"completion p95: {completionP95:F2} ms");
        _output.WriteLine($"eof-enter p95: {eofEnterP95:F2} ms");

        keystrokeP95.Should().BeLessOrEqualTo(50d);
        completionP95.Should().BeLessOrEqualTo(120d);
        eofEnterP95.Should().BeLessOrEqualTo(100d);
    }

    private static List<double> MeasureKeystrokeLatencies(Scintilla editor, int iterations)
    {
        var samples = new List<double>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var insertPosition = editor.TextLength;
            var sw = Stopwatch.StartNew();
            editor.InsertText(insertPosition, "x");
            editor.DeleteRange(insertPosition, 1);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }

        return samples;
    }

    private static List<double> MeasureCompletionLatencies(
        ScintillaScriptEditorControl control,
        int iterations)
    {
        var samples = new List<double>(iterations);
        control.Text = "st";
        control.SelectionStart = control.Text.Length;
        control.SelectionLength = 0;

        for (var i = 0; i < iterations; i++)
        {
            control.SelectionStart = control.Text.Length;
            control.SelectionLength = 0;

            var sw = Stopwatch.StartNew();
            InvokeNonPublic(control, "ShowCompletionPopup");
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);

            InvokeNonPublic(control, "HideCompletionPopup");
        }

        return samples;
    }

    private static List<double> MeasureEofEnterLatencies(
        ScintillaScriptEditorControl control,
        Scintilla editor,
        int iterations)
    {
        var baseline = control.Text;
        var samples = new List<double>(iterations);

        for (var i = 0; i < iterations; i++)
        {
            control.SelectionStart = control.Text.Length;
            control.SelectionLength = 0;

            var sw = Stopwatch.StartNew();
            var handled = (bool)InvokeNonPublic(control, "HandleSmartEnter", new KeyEventArgs(Keys.Enter))!;
            sw.Stop();
            handled.Should().BeTrue();
            samples.Add(sw.Elapsed.TotalMilliseconds);

            editor.CanUndo.Should().BeTrue();
            editor.Undo();
            control.Text.Should().Be(baseline);
        }

        return samples;
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        values.Should().NotBeEmpty();
        var sorted = values.OrderBy(v => v).ToArray();
        var index = (int)Math.Ceiling(percentile * sorted.Length) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }

    private static string BuildScript(int lineCount)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("steps:");
        for (var i = 0; i < lineCount; i++)
        {
            sb.AppendLine($"  - send: \"show interface {i}\"");
        }

        return sb.ToString();
    }

    private static Scintilla GetInnerEditor(ScintillaScriptEditorControl control)
    {
        var field = typeof(ScintillaScriptEditorControl).GetField("_editor", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (Scintilla)field!.GetValue(control)!;
    }

    private static object? InvokeNonPublic(object instance, string methodName, params object?[]? parameters)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull($"private method '{methodName}' should exist for this test");
        return method!.Invoke(instance, parameters);
    }
}
