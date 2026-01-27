using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class ScriptValidationFormatterTests
{
    [Fact]
    public void FormatSuccessMessage_ReturnsExpectedText()
    {
        var message = ScriptValidationFormatter.FormatSuccessMessage();

        message.Should().Be("Script validation succeeded (no errors found).");
    }

    [Fact]
    public void FormatFailureMessage_WithErrors_JoinsWithNewLines()
    {
        var errors = new[] { "Error one", "Error two" };

        var message = ScriptValidationFormatter.FormatFailureMessage(errors);

        message.Should().Be("Script validation failed:" + Environment.NewLine + "Error one" + Environment.NewLine + "Error two");
    }

    [Fact]
    public void FormatFailureMessage_WithNoErrors_ReturnsFallback()
    {
        var message = ScriptValidationFormatter.FormatFailureMessage(Array.Empty<string>());

        message.Should().Be("Script validation failed.");
    }

    [Fact]
    public void FormatExceptionMessage_UsesExceptionMessage()
    {
        var ex = new InvalidOperationException("boom");

        var message = ScriptValidationFormatter.FormatExceptionMessage(ex);

        message.Should().Be("Script validation error: boom");
    }
}
