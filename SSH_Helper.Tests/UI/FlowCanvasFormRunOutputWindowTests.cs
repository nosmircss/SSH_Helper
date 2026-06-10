using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class FlowCanvasFormRunOutputWindowTests
{
    [WinFormsFact]
    public void OpenAndCloseWindowMessages_RaiseTheirEvents()
    {
        using var form = new FlowCanvasForm(darkMode: false, configService: null);
        var opened = false;
        var closed = false;
        form.OnOpenRunOutputWindow += _ => opened = true;
        form.OnCloseRunOutputWindow += _ => closed = true;

        form.HandleHostMessage(JObject.FromObject(new { type = "open-run-output-window" }));
        form.HandleHostMessage(JObject.FromObject(new { type = "close-run-output-window" }));

        opened.Should().BeTrue();
        closed.Should().BeTrue();
    }
}
