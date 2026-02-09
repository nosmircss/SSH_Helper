using FluentAssertions;
using SSH_Helper.Models;
using Xunit;

namespace SSH_Helper.Tests.Models;

public class FontSettingsTests
{
    #region Default Value Tests

    [Fact]
    public void Default_UIFontFamily_IsSegoeUISemibold()
    {
        var settings = new FontSettings();
        settings.UIFontFamily.Should().Be("Segoe UI Semibold");
    }

    [Fact]
    public void Default_CodeFontFamily_IsCascadiaCode()
    {
        var settings = new FontSettings();
        settings.CodeFontFamily.Should().Be("Cascadia Code");
    }

    [Theory]
    [InlineData(nameof(FontSettings.SectionTitleFontSize), 9.5f)]
    [InlineData(nameof(FontSettings.TreeViewFontSize), 9.5f)]
    [InlineData(nameof(FontSettings.EmptyLabelFontSize), 9.5f)]
    [InlineData(nameof(FontSettings.ExecuteButtonFontSize), 9.5f)]
    [InlineData(nameof(FontSettings.CodeEditorFontSize), 9.75f)]
    [InlineData(nameof(FontSettings.OutputAreaFontSize), 9.75f)]
    [InlineData(nameof(FontSettings.TabFontSize), 9f)]
    [InlineData(nameof(FontSettings.ButtonFontSize), 9f)]
    [InlineData(nameof(FontSettings.HostListFontSize), 9f)]
    [InlineData(nameof(FontSettings.MenuFontSize), 9f)]
    [InlineData(nameof(FontSettings.StatusBarFontSize), 9f)]
    public void Default_FontSizes_HaveExpectedValues(string propertyName, float expected)
    {
        var settings = new FontSettings();
        var property = typeof(FontSettings).GetProperty(propertyName);
        property.Should().NotBeNull();
        var value = (float)property!.GetValue(settings)!;
        value.Should().Be(expected);
    }

    [Fact]
    public void Default_GlobalScaleFactor_IsOne()
    {
        var settings = new FontSettings();
        settings.GlobalScaleFactor.Should().Be(1.0f);
    }

    [Fact]
    public void Default_LayoutSettings_HaveExpectedValues()
    {
        var settings = new FontSettings();
        settings.CodeEditorWordWrap.Should().BeFalse();
        settings.OutputAreaWordWrap.Should().BeFalse();
    }

    [Fact]
    public void Default_RowHeights_HaveExpectedValues()
    {
        var settings = new FontSettings();
        settings.TreeViewRowHeight.Should().Be(0);
        settings.HostListRowHeight.Should().Be(28);
    }

    [Fact]
    public void Default_CustomAccentColor_IsNull()
    {
        var settings = new FontSettings();
        settings.CustomAccentColor.Should().BeNull();
    }

    #endregion

    #region ScaledSize Tests

    [Theory]
    [InlineData(10f, 1.0f, 10f)]
    [InlineData(10f, 1.5f, 15f)]
    [InlineData(10f, 0.8f, 8f)]
    [InlineData(9.5f, 1.2f, 11.4f)]
    [InlineData(0f, 1.5f, 0f)]
    [InlineData(7f, 0.8f, 5.6f)]
    [InlineData(16f, 1.5f, 24f)]
    public void ScaledSize_ReturnsBaseTimesScale(float baseSize, float scale, float expected)
    {
        var settings = new FontSettings { GlobalScaleFactor = scale };
        settings.ScaledSize(baseSize).Should().BeApproximately(expected, 0.01f);
    }

    #endregion

    #region CreateDefault Tests

    [Fact]
    public void CreateDefault_ReturnsNonNullInstance()
    {
        var settings = FontSettings.CreateDefault();
        settings.Should().NotBeNull();
    }

    [Fact]
    public void CreateDefault_ReturnsIndependentInstances()
    {
        var a = FontSettings.CreateDefault();
        var b = FontSettings.CreateDefault();
        a.GlobalScaleFactor = 1.5f;
        b.GlobalScaleFactor.Should().Be(1.0f);
    }

    #endregion

    #region Boundary Value Tests

    [Theory]
    [InlineData(0.8f)]
    [InlineData(1.0f)]
    [InlineData(1.5f)]
    public void GlobalScaleFactor_BoundaryValues_StoreCorrectly(float value)
    {
        var settings = new FontSettings { GlobalScaleFactor = value };
        settings.GlobalScaleFactor.Should().Be(value);
    }

    [Theory]
    [InlineData(7f)]
    [InlineData(16f)]
    public void FontSizeProperties_BoundaryValues_StoreCorrectly(float value)
    {
        var settings = new FontSettings
        {
            SectionTitleFontSize = value,
            TreeViewFontSize = value,
            EmptyLabelFontSize = value,
            ExecuteButtonFontSize = value,
            CodeEditorFontSize = value,
            OutputAreaFontSize = value,
            TabFontSize = value,
            ButtonFontSize = value,
            HostListFontSize = value,
            MenuFontSize = value,
            StatusBarFontSize = value
        };

        settings.SectionTitleFontSize.Should().Be(value);
        settings.TreeViewFontSize.Should().Be(value);
        settings.EmptyLabelFontSize.Should().Be(value);
        settings.ExecuteButtonFontSize.Should().Be(value);
        settings.CodeEditorFontSize.Should().Be(value);
        settings.OutputAreaFontSize.Should().Be(value);
        settings.TabFontSize.Should().Be(value);
        settings.ButtonFontSize.Should().Be(value);
        settings.HostListFontSize.Should().Be(value);
        settings.MenuFontSize.Should().Be(value);
        settings.StatusBarFontSize.Should().Be(value);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(28)]
    [InlineData(50)]
    public void HostListRowHeight_BoundaryValues_StoreCorrectly(int value)
    {
        var settings = new FontSettings { HostListRowHeight = value };
        settings.HostListRowHeight.Should().Be(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    public void TreeViewRowHeight_BoundaryValues_StoreCorrectly(int value)
    {
        var settings = new FontSettings { TreeViewRowHeight = value };
        settings.TreeViewRowHeight.Should().Be(value);
    }

    #endregion

    #region Edge Value Tests (documenting model behavior)

    [Fact]
    public void FontFamily_EmptyString_Stores()
    {
        var settings = new FontSettings { UIFontFamily = "" };
        settings.UIFontFamily.Should().Be("");
    }

    [Fact]
    public void FontSize_ZeroValue_Stores()
    {
        var settings = new FontSettings { SectionTitleFontSize = 0f };
        settings.SectionTitleFontSize.Should().Be(0f);
    }

    [Fact]
    public void CustomAccentColor_StoresArgbValue()
    {
        var argb = System.Drawing.Color.CornflowerBlue.ToArgb();
        var settings = new FontSettings { CustomAccentColor = argb };
        settings.CustomAccentColor.Should().Be(argb);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void BooleanSettings_AllCombinations_StoreCorrectly(bool codeWrap, bool outputWrap)
    {
        var settings = new FontSettings
        {
            CodeEditorWordWrap = codeWrap,
            OutputAreaWordWrap = outputWrap
        };
        settings.CodeEditorWordWrap.Should().Be(codeWrap);
        settings.OutputAreaWordWrap.Should().Be(outputWrap);
    }

    #endregion
}
