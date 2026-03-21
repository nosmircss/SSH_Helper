using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services;
using SSH_Helper.Services.Scripting;
using Xunit;

namespace SSH_Helper.Tests.UI;

public sealed class SettingsDialogBrowserCallbackTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _configPath;
    private readonly ConfigurationService _configService;

    public SettingsDialogBrowserCallbackTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"SettingsDialogBrowserCallbackTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _configPath = Path.Combine(_testDirectory, "config.json");
        _configService = new ConfigurationService(_configPath);
        _configService.Load();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for test temp folders.
        }
    }

    [WinFormsFact]
    public void ClearEmbeddedBrowserData_WhenConfirmed_InvokesProfileManager()
    {
        var profileManager = new RecordingBrowserCallbackWebViewProfileManager();
        var promptService = new RecordingSettingsDialogPromptService(DialogResult.Yes);

        using var dialog = new SettingsDialog(_configService, null, false, profileManager, promptService);

        InvokeMethod(dialog, "BtnClearEmbeddedBrowserData_Click", null!, EventArgs.Empty);

        profileManager.ClearEmbeddedBrowserDataCallCount.Should().Be(1);
        promptService.Messages.Should().Contain(message => message.Contains("resets SSH Helper's embedded-browser cookies", StringComparison.Ordinal));
    }

    private static void InvokeMethod(object obj, string methodName, params object[] args)
    {
        var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull($"method '{methodName}' should exist on {obj.GetType().Name}");
        method!.Invoke(obj, args);
    }

    private sealed class RecordingBrowserCallbackWebViewProfileManager : IBrowserCallbackWebViewProfileManager
    {
        public int ClearEmbeddedBrowserDataCallCount { get; private set; }

        public string UserDataDirectory => Path.Combine(Path.GetTempPath(), "unused");

        public IDisposable RegisterActiveSession()
        {
            throw new NotSupportedException();
        }

        public EmbeddedBrowserDataClearResult ClearEmbeddedBrowserData()
        {
            ClearEmbeddedBrowserDataCallCount++;
            return EmbeddedBrowserDataClearResult.Cleared;
        }
    }

    private sealed class RecordingSettingsDialogPromptService : ISettingsDialogPromptService
    {
        private readonly DialogResult _nextResult;

        public RecordingSettingsDialogPromptService(DialogResult nextResult)
        {
            _nextResult = nextResult;
        }

        public List<string> Messages { get; } = new();

        public DialogResult Show(IWin32Window? owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Messages.Add(message);
            return _nextResult;
        }
    }
}
