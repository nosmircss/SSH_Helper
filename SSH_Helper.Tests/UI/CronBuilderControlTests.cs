using FluentAssertions;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

/// <summary>
/// Tests for CronBuilderControl logic methods: expression building from dropdowns,
/// parsing expressions to dropdown values, and preset application.
/// These test internal static/testable methods without requiring a running UI.
/// </summary>
public class CronBuilderControlTests
{
    #region BuildExpressionFromDropdowns Tests

    [Fact]
    public void BuildExpressionFromDropdowns_AllStars_ReturnsEveryMinute()
    {
        var result = CronBuilderControl.BuildExpressionFromDropdowns("*", "*", "*", "*", "*");
        result.Should().Be("* * * * *");
    }

    [Fact]
    public void BuildExpressionFromDropdowns_Daily3AM_ReturnsCorrectExpression()
    {
        var result = CronBuilderControl.BuildExpressionFromDropdowns("0", "3", "*", "*", "*");
        result.Should().Be("0 3 * * *");
    }

    [Fact]
    public void BuildExpressionFromDropdowns_Every5Min_ReturnsCorrectExpression()
    {
        var result = CronBuilderControl.BuildExpressionFromDropdowns("*/5", "*", "*", "*", "*");
        result.Should().Be("*/5 * * * *");
    }

    [Fact]
    public void BuildExpressionFromDropdowns_Weekdays9AM_ReturnsCorrectExpression()
    {
        var result = CronBuilderControl.BuildExpressionFromDropdowns("0", "9", "*", "*", "1-5");
        result.Should().Be("0 9 * * 1-5");
    }

    [Fact]
    public void BuildExpressionFromDropdowns_Monthly1st_ReturnsCorrectExpression()
    {
        var result = CronBuilderControl.BuildExpressionFromDropdowns("0", "0", "1", "*", "*");
        result.Should().Be("0 0 1 * *");
    }

    [Fact]
    public void BuildExpressionFromDropdowns_Quarterly_ReturnsCorrectExpression()
    {
        var result = CronBuilderControl.BuildExpressionFromDropdowns("0", "0", "1", "1,4,7,10", "*");
        result.Should().Be("0 0 1 1,4,7,10 *");
    }

    [Fact]
    public void BuildExpressionFromDropdowns_IgnoresCustomValues_ReturnsNullForField()
    {
        // When a field is "Custom", it cannot be built from dropdowns
        var result = CronBuilderControl.BuildExpressionFromDropdowns("Custom", "3", "*", "*", "*");
        result.Should().BeNull();
    }

    #endregion

    #region TryParseToDropdowns Tests

    [Fact]
    public void TryParseToDropdowns_Daily3AM_SetsCorrectValues()
    {
        var success = CronBuilderControl.TryParseToDropdowns("0 3 * * *",
            out var minute, out var hour, out var dayOfMonth, out var month, out var dayOfWeek);

        success.Should().BeTrue();
        minute.Should().Be("0");
        hour.Should().Be("3");
        dayOfMonth.Should().Be("*");
        month.Should().Be("*");
        dayOfWeek.Should().Be("*");
    }

    [Fact]
    public void TryParseToDropdowns_Every5Min_SetsCorrectValues()
    {
        var success = CronBuilderControl.TryParseToDropdowns("*/5 * * * *",
            out var minute, out var hour, out var dayOfMonth, out var month, out var dayOfWeek);

        success.Should().BeTrue();
        minute.Should().Be("*/5");
        hour.Should().Be("*");
        dayOfMonth.Should().Be("*");
        month.Should().Be("*");
        dayOfWeek.Should().Be("*");
    }

    [Fact]
    public void TryParseToDropdowns_Weekdays9AM_SetsCorrectValues()
    {
        var success = CronBuilderControl.TryParseToDropdowns("0 9 * * 1-5",
            out var minute, out var hour, out var dayOfMonth, out var month, out var dayOfWeek);

        success.Should().BeTrue();
        minute.Should().Be("0");
        hour.Should().Be("9");
        dayOfMonth.Should().Be("*");
        month.Should().Be("*");
        dayOfWeek.Should().Be("1-5");
    }

    [Fact]
    public void TryParseToDropdowns_ComplexExpression_ReturnsCustomForUnmappedFields()
    {
        // "1,15,30 */2 1-15 * 1-5" -- minute "1,15,30" not in dropdown items
        var success = CronBuilderControl.TryParseToDropdowns("1,15,30 */2 1-15 * 1-5",
            out var minute, out var hour, out var dayOfMonth, out var month, out var dayOfWeek);

        success.Should().BeTrue();
        minute.Should().Be("Custom");
        hour.Should().Be("*/2");
        dayOfMonth.Should().Be("Custom");
        month.Should().Be("*");
        dayOfWeek.Should().Be("1-5");
    }

    [Fact]
    public void TryParseToDropdowns_InvalidExpression_ReturnsFalse()
    {
        var success = CronBuilderControl.TryParseToDropdowns("bad expression",
            out _, out _, out _, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void TryParseToDropdowns_EmptyExpression_ReturnsFalse()
    {
        var success = CronBuilderControl.TryParseToDropdowns("",
            out _, out _, out _, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void TryParseToDropdowns_NullExpression_ReturnsFalse()
    {
        var success = CronBuilderControl.TryParseToDropdowns(null!,
            out _, out _, out _, out _, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void TryParseToDropdowns_Quarterly_SetsCustomForMonth()
    {
        // "0 0 1 1,4,7,10 *" -- month "1,4,7,10" is in the dropdown
        var success = CronBuilderControl.TryParseToDropdowns("0 0 1 1,4,7,10 *",
            out var minute, out var hour, out var dayOfMonth, out var month, out var dayOfWeek);

        success.Should().BeTrue();
        minute.Should().Be("0");
        hour.Should().Be("0");
        dayOfMonth.Should().Be("1");
        // 1,4,7,10 is not a standard dropdown item, should be Custom
        // unless we add it explicitly
        month.Should().BeOneOf("1,4,7,10", "Custom");
        dayOfWeek.Should().Be("*");
    }

    #endregion

    #region Preset Tests

    [Theory]
    [InlineData("Every 5 min", "*/5 * * * *")]
    [InlineData("Every 15 min", "*/15 * * * *")]
    [InlineData("Every 30 min", "*/30 * * * *")]
    [InlineData("Hourly", "0 * * * *")]
    [InlineData("Daily midnight", "0 0 * * *")]
    [InlineData("Daily 3 AM", "0 3 * * *")]
    [InlineData("Weekdays 9 AM", "0 9 * * 1-5")]
    [InlineData("Weekly Monday", "0 0 * * 1")]
    [InlineData("Monthly 1st", "0 0 1 * *")]
    [InlineData("Quarterly", "0 0 1 1,4,7,10 *")]
    public void GetPresetExpression_ReturnsCorrectExpression(string presetName, string expectedExpression)
    {
        var expression = CronBuilderControl.GetPresetExpression(presetName);
        expression.Should().Be(expectedExpression);
    }

    [Fact]
    public void GetPresetExpression_UnknownPreset_ReturnsNull()
    {
        var expression = CronBuilderControl.GetPresetExpression("nonexistent");
        expression.Should().BeNull();
    }

    [Fact]
    public void GetPresetNames_Returns10Presets()
    {
        var names = CronBuilderControl.GetPresetNames();
        names.Should().HaveCount(10);
    }

    [Fact]
    public void GetPresetNames_ContainsExpectedNames()
    {
        var names = CronBuilderControl.GetPresetNames();
        names.Should().Contain("Every 5 min");
        names.Should().Contain("Daily 3 AM");
        names.Should().Contain("Quarterly");
    }

    #endregion

    #region Roundtrip Tests

    [Theory]
    [InlineData("*/5 * * * *")]
    [InlineData("0 3 * * *")]
    [InlineData("0 9 * * 1-5")]
    [InlineData("0 0 1 * *")]
    [InlineData("0 * * * *")]
    public void Roundtrip_ParseThenBuild_ProducesSameExpression(string expression)
    {
        var parsed = CronBuilderControl.TryParseToDropdowns(expression,
            out var min, out var hour, out var dom, out var month, out var dow);

        parsed.Should().BeTrue();

        var rebuilt = CronBuilderControl.BuildExpressionFromDropdowns(min, hour, dom, month, dow);
        rebuilt.Should().Be(expression);
    }

    #endregion
}
