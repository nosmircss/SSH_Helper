using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public class InteractiveTerminalFormTests
{
    [WinFormsFact]
    public void AdjustScrollbackOffset_WhenSelectionExists_PreservesSelectedText()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateInteractiveTerminalForm(initialColumns: 24, initialRows: 3);
        _ = form.Handle;

        InvokeMethod(
            form,
            "EnableDetachedReadOnlyMode",
            null,
            BuildDetachedLines(9));

        var terminalView = GetFieldValue<object>(form, "_terminalView");
        InvokeMethod(terminalView, "SelectAllVisible");
        GetPropertyValue<bool>(terminalView, "HasSelection").Should().BeTrue(
            "selecting visible text should produce a non-empty selection before scrolling");
        var selectedTextBeforeScroll = (InvokeMethod(terminalView, "GetSelectedText") as string) ?? string.Empty;
        selectedTextBeforeScroll.Should().NotBeEmpty(
            "a visible selection should produce copyable text prior to viewport movement");

        InvokeMethod(form, "AdjustScrollbackOffset", 1);

        GetPropertyValue<bool>(terminalView, "HasSelection").Should().BeTrue(
            "scrolling should keep selection anchored to text instead of clearing it");
        var selectedTextAfterScroll = (InvokeMethod(terminalView, "GetSelectedText") as string) ?? string.Empty;
        selectedTextAfterScroll.Should().Be(
            selectedTextBeforeScroll,
            "scrolling the viewport should not rebind the selection to different text");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    [WinFormsFact]
    public void MouseDragSelection_AcrossScrollback_CanSpanBeyondSingleViewport()
    {
        var visibleOpenFormsBefore = SnapshotVisibleOpenForms();
        using var form = CreateInteractiveTerminalForm(initialColumns: 24, initialRows: 3);
        _ = form.Handle;

        InvokeMethod(
            form,
            "EnableDetachedReadOnlyMode",
            null,
            BuildDetachedLines(9));

        var terminalView = GetFieldValue<object>(form, "_terminalView");
        var startPoint = GetCellCenter(terminalView, column: 0, row: 2);
        var endPoint = GetCellCenter(terminalView, column: 0, row: 0);
        InvokeMethod(
            terminalView,
            "OnMouseDown",
            new MouseEventArgs(MouseButtons.Left, clicks: 1, startPoint.X, startPoint.Y, delta: 0));

        // Scroll upward while still selecting to extend across off-screen rows.
        InvokeMethod(form, "AdjustScrollbackOffset", 3);
        InvokeMethod(
            terminalView,
            "OnMouseMove",
            new MouseEventArgs(MouseButtons.Left, clicks: 0, endPoint.X, endPoint.Y, delta: 0));
        InvokeMethod(
            terminalView,
            "OnMouseUp",
            new MouseEventArgs(MouseButtons.Left, clicks: 1, endPoint.X, endPoint.Y, delta: 0));

        var selectedText = (InvokeMethod(terminalView, "GetSelectedText") as string) ?? string.Empty;
        selectedText.Should().NotBeEmpty(
            "a drag selection across scrollback should still produce copyable text");
        selectedText.Split(Environment.NewLine).Length.Should().BeGreaterThan(3,
            "scroll-while-selecting should allow selecting more than one viewport of rows");
        AssertNoNewVisibleOpenForms(visibleOpenFormsBefore);
    }

    private static Form CreateInteractiveTerminalForm(int? initialColumns = null, int? initialRows = null)
    {
        var assembly = typeof(SSH_Helper.Form1).Assembly;
        var formType = assembly.GetType("SSH_Helper.Forms.InteractiveTerminalForm", throwOnError: false);
        formType.Should().NotBeNull("InteractiveTerminalForm should exist for interactive terminal UI coverage");

        var instance = Activator.CreateInstance(
            formType!,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { "Interactive Terminal Test", null, null, initialColumns, initialRows },
            culture: null);

        instance.Should().BeAssignableTo<Form>();
        return (Form)instance!;
    }

    private static string BuildDetachedLines(int lineCount)
    {
        return string.Join("\n", Enumerable.Range(1, lineCount).Select(i => $"line {i:00}"));
    }

    private static Point GetCellCenter(object terminalView, int column, int row)
    {
        var cellSize = GetPropertyValue<Size>(terminalView, "CellSize");
        var cellWidth = Math.Max(1, cellSize.Width);
        var cellHeight = Math.Max(1, cellSize.Height);
        return new Point(
            (column * cellWidth) + (cellWidth / 2),
            (row * cellHeight) + (cellHeight / 2));
    }

    private static Form[] SnapshotVisibleOpenForms()
    {
        return Application.OpenForms.Cast<Form>().Where(form => form.Visible).ToArray();
    }

    private static void AssertNoNewVisibleOpenForms(IEnumerable<Form> openFormsBefore)
    {
        Application.OpenForms.Cast<Form>()
            .Where(form => form.Visible)
            .Except(openFormsBefore)
            .Should()
            .BeEmpty();
    }

    private static T GetFieldValue<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull($"expected private field '{fieldName}' on {instance.GetType().Name}");

        var value = field!.GetValue(instance);
        value.Should().BeAssignableTo<T>($"field '{fieldName}' should be assignable to {typeof(T).Name}");
        return (T)value!;
    }

    private static T GetPropertyValue<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull($"expected property '{propertyName}' on {instance.GetType().Name}");

        var value = property!.GetValue(instance);
        value.Should().BeAssignableTo<T>($"property '{propertyName}' should be assignable to {typeof(T).Name}");
        return (T)value!;
    }

    private static object? InvokeMethod(object instance, string methodName, params object?[]? args)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        method.Should().NotBeNull($"expected method '{methodName}' on {instance.GetType().Name}");
        return method!.Invoke(instance, args);
    }
}
