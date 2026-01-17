using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

/// <summary>
/// Tests for the InputValidator utility class.
/// </summary>
public class InputValidatorTests
{
    #region IsValidIpAddress Tests

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    public void IsValidIpAddress_ValidIpAddresses_ReturnsTrue(string ip, bool expected)
    {
        var result = InputValidator.IsValidIpAddress(ip);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("192.168.1.1:22", true)]
    [InlineData("10.0.0.1:443", true)]
    [InlineData("192.168.1.1:65535", true)]
    [InlineData("192.168.1.1:1", true)]
    public void IsValidIpAddress_ValidIpWithPort_ReturnsTrue(string ipWithPort, bool expected)
    {
        var result = InputValidator.IsValidIpAddress(ipWithPort);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsValidIpAddress_NullOrWhitespace_ReturnsFalse(string? ip)
    {
        var result = InputValidator.IsValidIpAddress(ip!);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("256.1.1.1")]       // Octet > 255
    [InlineData("192.168.1")]       // Only 3 octets
    [InlineData("192.168.1.1.1")]   // 5 octets
    [InlineData("192.168.1.a")]     // Non-numeric octet
    [InlineData("not.an.ip.addr")]  // Text
    [InlineData("-1.0.0.1")]        // Negative octet
    public void IsValidIpAddress_InvalidIpFormat_ReturnsFalse(string ip)
    {
        var result = InputValidator.IsValidIpAddress(ip);
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("192.168.1.1:0")]       // Port 0 is invalid
    [InlineData("192.168.1.1:65536")]   // Port > 65535
    [InlineData("192.168.1.1:-1")]      // Negative port
    [InlineData("192.168.1.1:abc")]     // Non-numeric port
    public void IsValidIpAddress_InvalidPort_ReturnsFalse(string ipWithPort)
    {
        var result = InputValidator.IsValidIpAddress(ipWithPort);
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidPort Tests

    [Theory]
    [InlineData(1, true)]
    [InlineData(22, true)]
    [InlineData(443, true)]
    [InlineData(8080, true)]
    [InlineData(65535, true)]
    public void IsValidPort_ValidPorts_ReturnsTrue(int port, bool expected)
    {
        var result = InputValidator.IsValidPort(port);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void IsValidPort_InvalidPorts_ReturnsFalse(int port)
    {
        var result = InputValidator.IsValidPort(port);
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidTimeout Tests

    [Theory]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(60, true)]
    [InlineData(3600, true)]   // Max 1 hour
    public void IsValidTimeout_ValidTimeouts_ReturnsTrue(int timeout, bool expected)
    {
        var result = InputValidator.IsValidTimeout(timeout);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3601)]   // Over 1 hour
    [InlineData(10000)]
    public void IsValidTimeout_InvalidTimeouts_ReturnsFalse(int timeout)
    {
        var result = InputValidator.IsValidTimeout(timeout);
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidDelay Tests

    [Theory]
    [InlineData(0, true)]       // 0 is valid for delay
    [InlineData(1000, true)]
    [InlineData(60000, true)]   // Max 1 minute
    public void IsValidDelay_ValidDelays_ReturnsTrue(int delay, bool expected)
    {
        var result = InputValidator.IsValidDelay(delay);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(60001)]   // Over 1 minute
    [InlineData(100000)]
    public void IsValidDelay_InvalidDelays_ReturnsFalse(int delay)
    {
        var result = InputValidator.IsValidDelay(delay);
        result.Should().BeFalse();
    }

    #endregion

    #region SanitizeColumnName Tests

    [Theory]
    [InlineData("Host IP", "Host_IP")]
    [InlineData("Column Name", "Column_Name")]
    [InlineData("Multiple  Spaces", "Multiple__Spaces")]
    [InlineData("  Trimmed  ", "Trimmed")]
    public void SanitizeColumnName_ReplacesSpacesWithUnderscores(string input, string expected)
    {
        var result = InputValidator.SanitizeColumnName(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SanitizeColumnName_NullOrWhitespace_ReturnsDefault(string? input)
    {
        var result = InputValidator.SanitizeColumnName(input!);
        result.Should().Be("Column");
    }

    [Fact]
    public void SanitizeColumnName_NoSpaces_ReturnsSameString()
    {
        var result = InputValidator.SanitizeColumnName("ValidColumn");
        result.Should().Be("ValidColumn");
    }

    #endregion

    #region IsNotEmpty Tests

    [Theory]
    [InlineData("text", true)]
    [InlineData("  text  ", true)]
    [InlineData("a", true)]
    public void IsNotEmpty_NonEmptyStrings_ReturnsTrue(string input, bool expected)
    {
        var result = InputValidator.IsNotEmpty(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsNotEmpty_NullOrWhitespace_ReturnsFalse(string? input)
    {
        var result = InputValidator.IsNotEmpty(input);
        result.Should().BeFalse();
    }

    #endregion

    #region ParseIntOrDefault Tests

    [Theory]
    [InlineData("42", 0, 42)]
    [InlineData("0", 10, 0)]
    [InlineData("-5", 0, -5)]
    [InlineData("1000", 0, 1000)]
    public void ParseIntOrDefault_ValidIntegers_ReturnsValue(string input, int defaultValue, int expected)
    {
        var result = InputValidator.ParseIntOrDefault(input, defaultValue);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("abc", 42, 42)]
    [InlineData("", 10, 10)]
    [InlineData(null, 5, 5)]
    [InlineData("12.5", 0, 0)]   // Not an integer
    public void ParseIntOrDefault_InvalidInput_ReturnsDefault(string? input, int defaultValue, int expected)
    {
        var result = InputValidator.ParseIntOrDefault(input, defaultValue);
        result.Should().Be(expected);
    }

    #endregion

    #region Clamp Tests

    [Theory]
    [InlineData(5, 0, 10, 5)]     // Within range
    [InlineData(0, 0, 10, 0)]     // At min
    [InlineData(10, 0, 10, 10)]   // At max
    [InlineData(-5, 0, 10, 0)]    // Below min
    [InlineData(15, 0, 10, 10)]   // Above max
    public void Clamp_ReturnsValueWithinRange(int value, int min, int max, int expected)
    {
        var result = InputValidator.Clamp(value, min, max);
        result.Should().Be(expected);
    }

    #endregion
}
