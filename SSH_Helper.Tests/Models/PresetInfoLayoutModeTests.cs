using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class PresetInfoLayoutModeTests
{
    [Fact]
    public void LayoutMode_defaults_to_null_meaning_inherit_default()
    {
        new PresetInfo().LayoutMode.Should().BeNull();
    }

    [Fact]
    public void Clone_copies_layout_mode()
    {
        var p = new PresetInfo { Commands = "print: hi", LayoutMode = LayoutMode.Manual };
        p.Clone().LayoutMode.Should().Be(LayoutMode.Manual);
    }

    [Fact]
    public void Clone_preserves_null_layout_mode()
    {
        var p = new PresetInfo { Commands = "print: hi" };
        p.Clone().LayoutMode.Should().BeNull();
    }
}
