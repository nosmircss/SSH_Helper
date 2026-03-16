using FluentAssertions;
using SSH_Helper.Utilities;
using Xunit;

namespace SSH_Helper.Tests.Utilities;

public class ContentHasherTests
{
    [Fact]
    public void ComputeHash_WithContent_ReturnsDeterministicSha256()
    {
        var hash = ContentHasher.ComputeHash("hello");

        // SHA256 of "hello" in uppercase hex
        hash.Should().Be("2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824");
    }

    [Fact]
    public void ComputeHash_SameInput_ReturnsSameHash()
    {
        var hash1 = ContentHasher.ComputeHash("test content");
        var hash2 = ContentHasher.ComputeHash("test content");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInputs_ReturnDifferentHashes()
    {
        var hash1 = ContentHasher.ComputeHash("input1");
        var hash2 = ContentHasher.ComputeHash("input2");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_EmptyString_ReturnsEmptyString()
    {
        var hash = ContentHasher.ComputeHash("");

        hash.Should().BeEmpty();
    }

    [Fact]
    public void ComputeHash_Null_ReturnsEmptyString()
    {
        var hash = ContentHasher.ComputeHash(null!);

        hash.Should().BeEmpty();
    }

    [Fact]
    public void ComputeHash_ReturnsUppercaseHex()
    {
        var hash = ContentHasher.ComputeHash("test");

        hash.Should().MatchRegex("^[0-9A-F]{64}$");
    }
}
