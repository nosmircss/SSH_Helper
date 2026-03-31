using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Models;
using SSH_Helper.UI;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class UnsavedPresetDiffDialogTests
{
    [WinFormsFact]
    public void ImpactedSave_ShowsDiffAndCollapsedAffectedJobsSection()
    {
        var impact = new PresetSaveImpact(
            "Nightly",
            "Schedulers",
            new List<JobDefinition>
            {
                new() { Id = "job-a", Name = "Alpha", TargetType = JobTargetType.Preset, TargetName = "Nightly" },
                new() { Id = "job-b", Name = "Bravo", TargetType = JobTargetType.Folder, TargetName = "Schedulers" }
            });

        using var dialog = new UnsavedPresetDiffDialog(
            savedPresetName: "Nightly",
            currentPresetName: "Nightly",
            savedTimeout: 30,
            currentTimeoutText: "45",
            savedCommands: "echo nightly",
            currentCommands: "echo nightly updated",
            darkMode: false,
            impact: impact,
            promptMode: PresetSavePromptMode.SaveDiscardCancel);

        dialog.Show();
        Application.DoEvents();

        var impactCount = GetField<Label>(dialog, "_lblImpactCount");
        var impactSummary = GetField<Label>(dialog, "_lblImpactSummary");
        var toggleButton = GetField<Button>(dialog, "_btnToggleAffectedJobs");
        var jobsPanel = GetField<Panel>(dialog, "_panelAffectedJobs");
        var jobsList = GetField<ListBox>(dialog, "_lstAffectedJobs");
        var diffBox = GetField<RichTextBox>(dialog, "_txtDiff");

        impactCount.Text.Should().Be("Preset 'Nightly' is used by 2 scheduled jobs.");
        impactSummary.Text.Should().Contain("Future scheduled and Run Now executions will use the updated preset 'Nightly'.");
        jobsPanel.Visible.Should().BeFalse();
        toggleButton.Text.Should().Be("Show affected scheduled jobs (2)");
        diffBox.Text.Should().Contain("~ Timeout: 30 -> 45");
        diffBox.Text.Should().Contain("echo nightly updated");

        toggleButton.PerformClick();
        Application.DoEvents();

        jobsPanel.Visible.Should().BeTrue();
        jobsList.Items.Cast<object>().Should().Contain(new object[]
        {
            "Alpha",
            "Bravo [Folder: Schedulers]"
        });

        FindButton(dialog, "Save").PerformClick();
        dialog.SelectedAction.Should().Be(PresetSaveImpactAction.SaveExisting);
    }

    [WinFormsFact]
    public void RenamePrompt_OffersRenameAndCreateNewWithoutDiscardWhenRequested()
    {
        var impact = new PresetSaveImpact(
            "Nightly",
            "Schedulers",
            new List<JobDefinition>
            {
                new() { Id = "job-a", Name = "Nightly Refresh", TargetType = JobTargetType.Preset, TargetName = "Nightly" }
            });

        using var dialog = new UnsavedPresetDiffDialog(
            savedPresetName: "Nightly",
            currentPresetName: "Nightly 2",
            savedTimeout: 30,
            currentTimeoutText: "30",
            savedCommands: "echo nightly",
            currentCommands: "echo nightly renamed",
            darkMode: false,
            impact: impact,
            promptMode: PresetSavePromptMode.RenameExistingCreateNewCancel);

        dialog.Show();
        Application.DoEvents();

        FindButton(dialog, "Rename Existing").Should().NotBeNull();
        FindButton(dialog, "Create New").Should().NotBeNull();
        FindControls<Button>(dialog).Should().NotContain(button => string.Equals(button.Text, "Discard", StringComparison.Ordinal));
        GetField<Label>(dialog, "_lblImpactSummary").Text.Should().Contain("Create New saves 'Nightly 2' as a separate preset instead.");

        FindButton(dialog, "Create New").PerformClick();
        dialog.SelectedAction.Should().Be(PresetSaveImpactAction.CreateNew);
    }

    [WinFormsFact]
    public void NonImpactedSave_PreservesDiffPromptWithoutImpactSection()
    {
        using var dialog = new UnsavedPresetDiffDialog(
            savedPresetName: "Nightly",
            currentPresetName: "Nightly",
            savedTimeout: 30,
            currentTimeoutText: "60",
            savedCommands: "echo nightly",
            currentCommands: "echo nightly updated",
            darkMode: false,
            impact: null,
            promptMode: PresetSavePromptMode.SaveDiscardCancel);

        dialog.Show();
        Application.DoEvents();

        GetField<Label>(dialog, "_lblImpactCount").Visible.Should().BeFalse();
        GetField<Label>(dialog, "_lblImpactSummary").Visible.Should().BeFalse();
        GetField<Button>(dialog, "_btnToggleAffectedJobs").Visible.Should().BeFalse();
        GetField<RichTextBox>(dialog, "_txtDiff").Text.Should().Contain("echo nightly updated");

        FindButton(dialog, "Discard").PerformClick();
        dialog.SelectedAction.Should().Be(PresetSaveImpactAction.Discard);
    }

    [WinFormsFact]
    public void SavePrompt_LongCommandDiff_DoesNotTruncateOutput()
    {
        var savedLines = Enumerable.Range(1, 12_050)
            .Select(index => $"echo line {index}")
            .ToArray();
        var currentLines = savedLines.ToArray();
        currentLines[^1] = "echo line 12050 updated";

        using var dialog = new UnsavedPresetDiffDialog(
            savedPresetName: "Nightly",
            currentPresetName: "Nightly",
            savedTimeout: 30,
            currentTimeoutText: "30",
            savedCommands: string.Join('\n', savedLines),
            currentCommands: string.Join('\n', currentLines),
            darkMode: false,
            impact: null,
            promptMode: PresetSavePromptMode.SaveDiscardCancel);

        dialog.Show();
        Application.DoEvents();

        var diffBox = GetField<RichTextBox>(dialog, "_txtDiff");
        diffBox.Text.Should().Contain("+ echo line 12050 updated");
        diffBox.Text.Should().NotContain("... diff truncated");
    }

    private static Button FindButton(Control root, string text)
    {
        return FindControls<Button>(root).Single(control => string.Equals(control.Text, text, StringComparison.Ordinal));
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindControls<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static T GetField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull($"field '{fieldName}' should exist on {obj.GetType().Name}");
        return (T)field!.GetValue(obj)!;
    }
}
