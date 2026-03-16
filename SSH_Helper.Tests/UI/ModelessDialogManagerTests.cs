using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.UI;

public class ModelessDialogManagerTests
{
    [WinFormsFact]
    public void ShowOrActivate_ReusesExistingDialogInstance()
    {
        using var owner = new Form();
        owner.Show();
        Application.DoEvents();

        var manager = new ModelessDialogManager<TestDialog>();

        var first = manager.ShowOrActivate(() => new TestDialog(), owner);
        var second = manager.ShowOrActivate(() => new TestDialog(), owner);

        second.Should().BeSameAs(first);
        manager.Current.Should().BeSameAs(first);
        Application.OpenForms.Cast<Form>().Count(form => form is TestDialog).Should().Be(1);

        first.Close();
        Application.DoEvents();

        manager.Current.Should().BeNull();
    }

    [WinFormsFact]
    public void ShowOrActivate_WhenDialogCloses_RequestsOwnerReactivation()
    {
        using var owner = new Form();
        owner.Show();
        Application.DoEvents();

        var manager = new ModelessDialogManager<TestDialog>();
        Form? restoredOwner = null;
        var restoreCalls = 0;
        ModelessDialogManager<TestDialog>.RestoreOwnerActivationOverrideForTests = form =>
        {
            restoreCalls++;
            restoredOwner = form;
        };

        try
        {
            var dialog = manager.ShowOrActivate(() => new TestDialog(), owner);
            Application.DoEvents();

            dialog.Close();

            restoreCalls.Should().Be(1);
            restoredOwner.Should().BeSameAs(owner);
            manager.Current.Should().BeNull();
        }
        finally
        {
            ModelessDialogManager<TestDialog>.RestoreOwnerActivationOverrideForTests = null;
        }
    }

    [WinFormsFact]
    public void ShowOrActivate_WhenDialogUsesCenterParent_CentersOverOwnerOnFirstShow()
    {
        using var owner = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(200, 150),
            Size = new Size(900, 700)
        };
        owner.Show();
        Application.DoEvents();

        var manager = new ModelessDialogManager<CenteredTestDialog>();

        using var dialog = manager.ShowOrActivate(() => new CenteredTestDialog(), owner);
        Application.DoEvents();

        var expectedLocation = new Point(
            owner.Left + ((owner.Width - dialog.Width) / 2),
            owner.Top + ((owner.Height - dialog.Height) / 2));

        dialog.Location.Should().Be(expectedLocation);
    }

    private sealed class TestDialog : Form
    {
    }

    private sealed class CenteredTestDialog : Form
    {
        public CenteredTestDialog()
        {
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(320, 180);
        }
    }
}
