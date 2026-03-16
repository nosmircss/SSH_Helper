using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class CronBuilderControlLayoutTests
{
    [WinFormsFact]
    public void LargerFontAndNarrowWidth_ExpandLayoutToKeepWrappedContentVisible()
    {
        using var host = new Form
        {
            Size = new Size(560, 420),
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000)
        };

        using var control = new CronBuilderControl
        {
            Location = Point.Empty,
            Size = new Size(460, 200)
        };

        host.Controls.Add(control);
        host.Show();
        Application.DoEvents();

        using var font = new Font("Segoe UI", 12f);
        control.Font = font;
        control.Width = 460;
        Application.DoEvents();

        var presetPanel = GetField<FlowLayoutPanel>(control, "_presetPanel");
        var requiredPresetHeight = MeasureVisibleChildBottom(presetPanel);
        var requiredContentHeight = MeasureVisibleChildBottom(control);

        presetPanel.Height.Should().BeGreaterOrEqualTo(requiredPresetHeight);
        control.ClientSize.Height.Should().BeGreaterOrEqualTo(requiredContentHeight);
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }

    private static int MeasureVisibleChildBottom(Control control)
    {
        var contentBottom = 0;
        foreach (Control child in control.Controls)
        {
            if (!child.Visible)
            {
                continue;
            }

            var margin = child.Margin;
            var candidateBottom = child.Bottom + margin.Bottom;
            if (candidateBottom > contentBottom)
            {
                contentBottom = candidateBottom;
            }
        }

        return contentBottom;
    }
}
