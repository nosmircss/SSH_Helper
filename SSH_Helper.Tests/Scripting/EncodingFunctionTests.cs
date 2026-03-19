using System.Threading;
using FluentAssertions;
using SSH_Helper.Services.Scripting;
using SSH_Helper.Services.Scripting.Commands;
using SSH_Helper.Services.Scripting.Models;
using Xunit;

namespace SSH_Helper.Tests.Scripting;

public class EncodingFunctionTests
{
    private readonly SetCommand _command = new();

    private async Task<string> Eval(string expression, ScriptContext? context = null)
    {
        context ??= new ScriptContext();
        var step = new ScriptStep { Set = $"result = {expression}" };
        await _command.ExecuteAsync(step, context, CancellationToken.None);
        return context.GetVariableString("result");
    }

    // --- base64 ---

    [Fact]
    public async Task Base64_RoundTrip()
    {
        var encoded = await Eval("base64_encode(\"hello world\")");
        encoded.Should().Be("aGVsbG8gd29ybGQ=");

        var context = new ScriptContext();
        context.SetVariable("encoded", encoded);
        var decoded = await Eval("base64_decode(encoded)", context);
        decoded.Should().Be("hello world");
    }

    [Fact]
    public async Task Base64Encode_Empty()
    {
        var result = await Eval("base64_encode(\"\")");
        result.Should().BeEmpty();
    }

    // --- url encode/decode ---

    [Fact]
    public async Task UrlEncode_SpecialChars()
    {
        var result = await Eval("url_encode(\"hello world&foo=bar\")");
        result.Should().Be("hello%20world%26foo%3Dbar");
    }

    [Fact]
    public async Task UrlDecode_RoundTrip()
    {
        var context = new ScriptContext();
        context.SetVariable("encoded", "hello%20world%26foo%3Dbar");
        var result = await Eval("url_decode(encoded)", context);
        result.Should().Be("hello world&foo=bar");
    }

    // --- hash ---

    [Fact]
    public async Task Hash_SHA256_Default()
    {
        var result = await Eval("hash(\"hello\")");
        result.Should().Be("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }

    [Fact]
    public async Task Hash_MD5()
    {
        var result = await Eval("hash(\"hello\", \"md5\")");
        result.Should().Be("5d41402abc4b2a76b9719d911017c592");
    }

    [Fact]
    public async Task Hash_SHA1()
    {
        var result = await Eval("hash(\"hello\", \"sha1\")");
        result.Should().Be("aaf4c61ddcc5e8a2dabede0f3b482cd9aea9434d");
    }

    // --- hex encode/decode ---

    [Fact]
    public async Task HexEncode_RoundTrip()
    {
        var encoded = await Eval("hex_encode(\"ABC\")");
        encoded.Should().Be("414243");

        var context = new ScriptContext();
        context.SetVariable("hex", encoded);
        var decoded = await Eval("hex_decode(hex)", context);
        decoded.Should().Be("ABC");
    }

    [Fact]
    public async Task HexDecode_Invalid_ReturnsEmpty()
    {
        var result = await Eval("hex_decode(\"xyz\")");
        result.Should().BeEmpty();
    }
}
