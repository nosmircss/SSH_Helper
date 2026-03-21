using FluentAssertions;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.Services.Scripting;

public sealed class BrowserCallbackWebViewProfileManagerTests : IDisposable
{
    private readonly string _rootDirectory;

    public BrowserCallbackWebViewProfileManagerTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"BrowserCallbackWebViewProfileManagerTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootDirectory))
            {
                Directory.Delete(_rootDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for test temp folders.
        }
    }

    [Fact]
    public void ClearEmbeddedBrowserData_WhenIdle_DeletesExistingProfileContents()
    {
        var profileDirectory = Path.Combine(_rootDirectory, "wv2");
        Directory.CreateDirectory(profileDirectory);
        File.WriteAllText(Path.Combine(profileDirectory, "Cookies"), "cached");

        var manager = new BrowserCallbackWebViewProfileManager(profileDirectory);

        var result = manager.ClearEmbeddedBrowserData();

        result.Should().Be(EmbeddedBrowserDataClearResult.Cleared);
        Directory.Exists(profileDirectory).Should().BeFalse();
    }

    [Fact]
    public void ClearEmbeddedBrowserData_WhenSessionActive_ReturnsActiveSessionBlocked()
    {
        var profileDirectory = Path.Combine(_rootDirectory, "wv2");
        var manager = new BrowserCallbackWebViewProfileManager(profileDirectory);
        using var registration = manager.RegisterActiveSession();

        var result = manager.ClearEmbeddedBrowserData();

        result.Should().Be(EmbeddedBrowserDataClearResult.ActiveSessionBlocked);
    }
}
