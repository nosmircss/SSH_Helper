using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class HistoryListBoxTests
{
    private const string LongHistoryEntry =
        "2026-03-13 09:55:59 - QA WriteFile JSON [Windows] with a long suffix that should still measure as a wrapped history row";

    [WinFormsFact]
    public void HeightOnlyResize_DoesNotDoMoreVariableHeightWorkThanWidthChangingResize()
    {
        var heightOnlyWork = MeasureResizeWork(new Size(180, 140), new Size(180, 220));
        var widthChangingWork = MeasureResizeWork(new Size(180, 140), new Size(220, 220));

        heightOnlyWork.MeasureItemCalls.Should().BeLessOrEqualTo(widthChangingWork.MeasureItemCalls);
        heightOnlyWork.InvalidatedCalls.Should().BeLessOrEqualTo(widthChangingWork.InvalidatedCalls);
    }

    [WinFormsFact]
    public void FontChange_FollowedByExplicitRefreshVariableItemHeights_DuplicatesWork()
    {
        using var nextFont = new Font("Segoe UI", 11f);
        using var probe = CreateAttachedProbe();
        probe.Control.Items.Add(LongHistoryEntry);
        probe.Control.DrawMode = DrawMode.OwnerDrawVariable;
        probe.Control.Size = new Size(180, 140);
        probe.Control.RefreshVariableItemHeights();
        probe.ResetCounters();

        probe.Control.Font = nextFont;
        var measureItemCallsAfterFontChange = probe.MeasureItemCalls;
        var invalidatedCallsAfterFontChange = probe.InvalidatedCalls;
        (measureItemCallsAfterFontChange > 0 || invalidatedCallsAfterFontChange > 0).Should().BeTrue();
        probe.Control.RefreshVariableItemHeights();

        probe.MeasureItemCalls.Should().Be(measureItemCallsAfterFontChange);
        probe.InvalidatedCalls.Should().Be(invalidatedCallsAfterFontChange);
    }

    [WinFormsFact]
    public void WidthStableExplicitRefresh_FollowingAnInitialRefresh_DoesNotRepeatWork()
    {
        using var probe = CreateAttachedProbe();
        probe.Control.Items.Add(LongHistoryEntry);
        probe.Control.DrawMode = DrawMode.OwnerDrawVariable;
        probe.Control.Size = new Size(180, 140);
        probe.Control.RefreshVariableItemHeights();
        probe.ResetCounters();

        probe.Control.RefreshVariableItemHeights();

        probe.MeasureItemCalls.Should().Be(0);
        probe.InvalidatedCalls.Should().Be(0);
    }

    private static HistoryListBoxProbe CreateAttachedProbe()
    {
        var host = new Panel();
        host.CreateControl();

        var control = new HistoryListBoxProbe(host);

        return control;
    }

    private static ResizeWork MeasureResizeWork(Size initialSize, Size resizedSize)
    {
        using var probe = CreateAttachedProbe();
        probe.Control.Items.Add(LongHistoryEntry);
        probe.Control.DrawMode = DrawMode.OwnerDrawVariable;
        probe.Control.Size = initialSize;
        probe.Control.RefreshVariableItemHeights();
        probe.ResetCounters();

        probe.Control.Size = resizedSize;

        return new ResizeWork(probe.MeasureItemCalls, probe.InvalidatedCalls);
    }

    private sealed class HistoryListBoxProbe : IDisposable
    {
        private readonly Control _host;

        private readonly HistoryListBox _control;

        public int MeasureItemCalls { get; private set; }

        public int InvalidatedCalls { get; private set; }

        public HistoryListBoxProbe(Control host)
        {
            _host = host;
            _control = new HistoryListBox
            {
                Dock = DockStyle.Fill
            };

            _host.Controls.Add(_control);
            _ = _control.Handle;

            _control.MeasureItem += OnMeasureItem;
            _control.Invalidated += OnInvalidated;
        }

        public HistoryListBox Control => _control;

        public void ResetCounters()
        {
            MeasureItemCalls = 0;
            InvalidatedCalls = 0;
        }

        public void Dispose()
        {
            _control.MeasureItem -= OnMeasureItem;
            _control.Invalidated -= OnInvalidated;
            _control.Dispose();
            _host.Dispose();
        }

        private void OnMeasureItem(object? sender, MeasureItemEventArgs e)
        {
            MeasureItemCalls++;
        }

        private void OnInvalidated(object? sender, InvalidateEventArgs e)
        {
            InvalidatedCalls++;
        }
    }

    private sealed record ResizeWork(int MeasureItemCalls, int InvalidatedCalls);
}
