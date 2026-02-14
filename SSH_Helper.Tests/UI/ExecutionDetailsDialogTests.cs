using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ExecutionDetailsDialogTests
{
    [WinFormsFact]
    public void Constructor_WithInteractiveSessions_AddsInteractiveTabAndRows()
    {
        var details = CreateDetailsWithInteractiveSessions();
        using var dialog = new ExecutionDetailsDialog(details, string.Empty, darkMode: false);

        var tabControl = GetField<TabControl>(dialog, "_tabControl");
        var interactiveTab = tabControl.TabPages
            .Cast<TabPage>()
            .FirstOrDefault(tab => string.Equals(tab.Text, "Interactive", StringComparison.Ordinal));

        interactiveTab.Should().NotBeNull();

        var grid = GetField<DataGridView>(dialog, "_gridInteractiveSessions");
        grid.Rows.Count.Should().Be(2);

        grid.ClearSelection();
        grid.CurrentCell = grid.Rows[1].Cells[0];
        grid.Rows[1].Selected = true;
        InvokeMethod(dialog, "UpdateInteractiveTranscriptFromSelection");

        var transcriptBox = GetField<TextBox>(dialog, "_txtInteractiveTranscript");
        transcriptBox.Text.Should().Contain("show interfaces");
    }

    [WinFormsFact]
    public void Constructor_WithoutInteractiveSessions_ShowsEmptyState()
    {
        var details = new ExecutionDetails
        {
            PresetName = "No Interactive",
            PresetType = "YamlScript",
            StartTimeUtc = DateTime.UtcNow.AddSeconds(-10),
            EndTimeUtc = DateTime.UtcNow,
            Hosts = new List<SSH_Helper.Models.HostExecutionContext>()
        };

        using var dialog = new ExecutionDetailsDialog(details, string.Empty, darkMode: false);
        var grid = GetField<DataGridView>(dialog, "_gridInteractiveSessions");
        var transcriptBox = GetField<TextBox>(dialog, "_txtInteractiveTranscript");

        grid.Rows.Count.Should().Be(0);
        transcriptBox.Text.Should().ContainEquivalentOf("no interactive terminal sessions");
    }

    [WinFormsFact]
    public void Constructor_InteractiveTab_DefaultLayout_PrioritizesTranscriptArea()
    {
        var details = CreateDetailsWithInteractiveSessions();
        using var dialog = new ExecutionDetailsDialog(details, string.Empty, darkMode: false);
        dialog.Show();
        Application.DoEvents();

        var split = GetField<SplitContainer>(dialog, "_interactiveSplit");
        split.SplitterDistance.Should().BeLessThanOrEqualTo(split.Panel1MinSize + 20);
        split.Panel2.Height.Should().BeGreaterThan(split.Panel1.Height);

        dialog.Close();
    }

    [WinFormsFact]
    public void Constructor_WithHostFilter_LimitsHostsAndContextToSelectedHost()
    {
        var details = CreateDetailsWithMultipleHosts();
        using var dialog = new ExecutionDetailsDialog(details, "host-two-output", "10.0.0.2", darkMode: false);

        var hostsGrid = GetField<DataGridView>(dialog, "_gridHosts");
        hostsGrid.Rows.Count.Should().Be(1);
        hostsGrid.Rows[0].Cells["HostAddress"].Value.Should().Be("10.0.0.2");

        var contextGrid = GetField<DataGridView>(dialog, "_gridContext");
        contextGrid.Rows.Count.Should().BeGreaterThan(0);
        foreach (DataGridViewRow row in contextGrid.Rows)
        {
            row.Cells["HostAddress"].Value.Should().Be("10.0.0.2");
        }
    }

    private static ExecutionDetails CreateDetailsWithInteractiveSessions()
    {
        var now = DateTime.UtcNow;
        return new ExecutionDetails
        {
            PresetName = "Interactive Audit",
            PresetType = "YamlScript",
            StartTimeUtc = now.AddMinutes(-5),
            EndTimeUtc = now,
            Hosts = new List<SSH_Helper.Models.HostExecutionContext>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Success = true,
                    TimestampUtc = now
                }
            },
            InteractiveSessions = new List<InteractiveTerminalSessionDetails>
            {
                new()
                {
                    SessionNumber = 1,
                    HostAddress = "10.0.0.1",
                    SessionMode = "separate",
                    EmulationMode = "full",
                    StartedAtUtc = now.AddMinutes(-4),
                    EndedAtUtc = now.AddMinutes(-3),
                    CloseReason = "user_closed",
                    Completed = true,
                    Transcript = "show version"
                },
                new()
                {
                    SessionNumber = 2,
                    HostAddress = "10.0.0.1",
                    SessionMode = "shared",
                    EmulationMode = "full",
                    StartedAtUtc = now.AddMinutes(-2),
                    EndedAtUtc = now.AddMinutes(-1),
                    CloseReason = "disconnected",
                    Completed = true,
                    Transcript = "show interfaces"
                }
            }
        };
    }

    private static ExecutionDetails CreateDetailsWithMultipleHosts()
    {
        var now = DateTime.UtcNow;
        return new ExecutionDetails
        {
            PresetName = "Scoped Host",
            PresetType = "YamlScript",
            StartTimeUtc = now.AddMinutes(-5),
            EndTimeUtc = now,
            Hosts = new List<SSH_Helper.Models.HostExecutionContext>
            {
                new()
                {
                    HostAddress = "10.0.0.1",
                    Success = true,
                    TimestampUtc = now.AddMinutes(-1),
                    Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["site"] = "alpha"
                    }
                },
                new()
                {
                    HostAddress = "10.0.0.2",
                    Success = false,
                    TimestampUtc = now,
                    Variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["site"] = "beta",
                        ["role"] = "edge"
                    }
                }
            },
            InteractiveSessions = new List<InteractiveTerminalSessionDetails>
            {
                new()
                {
                    SessionNumber = 1,
                    HostAddress = "10.0.0.1",
                    SessionMode = "shared",
                    EmulationMode = "full",
                    StartedAtUtc = now.AddMinutes(-4),
                    EndedAtUtc = now.AddMinutes(-3),
                    CloseReason = "user_closed",
                    Completed = true,
                    Transcript = "show version"
                },
                new()
                {
                    SessionNumber = 2,
                    HostAddress = "10.0.0.2",
                    SessionMode = "separate",
                    EmulationMode = "full",
                    StartedAtUtc = now.AddMinutes(-2),
                    EndedAtUtc = now.AddMinutes(-1),
                    CloseReason = "disconnected",
                    Completed = true,
                    Transcript = "show interfaces"
                }
            }
        };
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }

    private static void InvokeMethod(object obj, string methodName)
    {
        var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"method '{methodName}' should exist on {obj.GetType().Name}");
        method!.Invoke(obj, null);
    }
}
