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

    private sealed class TestDialog : Form
    {
    }
}
