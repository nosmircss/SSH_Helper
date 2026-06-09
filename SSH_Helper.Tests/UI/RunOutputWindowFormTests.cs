using System.Collections.Concurrent;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using SSH_Helper.Services;
using SSH_Helper.UI;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public sealed class RunOutputWindowFormTests
{
    [WinFormsFact]
    public void SendRunOutputAppend_QueuesRunOutputMessage()
    {
        using var win = new RunOutputWindowForm(darkMode: true, configService: null);
        win.SendRunOutputAppend("hello\n");
        var q = GetField<ConcurrentQueue<string>>(win, "_pendingMessages");
        var msg = ReadMessageOfType(q, "run-output");
        msg.Should().NotBeNull();
        msg!["chunk"]?.ToString().Should().Be("hello\n");
    }

    [WinFormsFact]
    public void SendRunState_QueuesExecutionStartedOrFinished()
    {
        using var win = new RunOutputWindowForm(darkMode: true, configService: null);
        win.SendRunState(true);
        win.SendRunState(false);
        var q = GetField<ConcurrentQueue<string>>(win, "_pendingMessages");
        ReadMessageOfType(q, "execution-started").Should().NotBeNull();
        ReadMessageOfType(q, "execution-finished").Should().NotBeNull();
    }

    [WinFormsFact]
    public void SaveRunOutputPrefs_PersistsToWindowState_AndSeedReplaysThem()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"RunOutWin_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var cfg = new ConfigurationService(Path.Combine(dir, "config.json"));
            using var win = new RunOutputWindowForm(darkMode: true, configService: cfg);
            InvokePrivate(win, "SaveRunOutputPrefs", JObject.FromObject(new { runOutputColor = false, runOutputWrap = true }));
            cfg.GetCurrent().WindowState!.FlowCanvasRunOutputColor.Should().BeFalse();
            cfg.GetCurrent().WindowState!.FlowCanvasRunOutputWrap.Should().BeTrue();

            var q = GetField<ConcurrentQueue<string>>(win, "_pendingMessages");
            InvokePrivate(win, "SendPersistedPrefs");
            var restore = ReadMessageOfType(q, "layout-restore");
            restore.Should().NotBeNull();
            restore!["runOutputColor"]?.Value<bool>().Should().BeFalse();
            restore["runOutputWrap"]?.Value<bool>().Should().BeTrue();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static JObject? ReadMessageOfType(ConcurrentQueue<string> q, string type)
    {
        foreach (var json in q.ToArray())
        {
            var p = JObject.Parse(json);
            if (string.Equals(p["type"]?.ToString(), type, StringComparison.Ordinal)) return p;
        }
        return null;
    }
    private static void InvokePrivate(object o, string m, params object?[] a)
    {
        var mi = o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic);
        mi.Should().NotBeNull($"{m} should exist");
        mi!.Invoke(o, a);
    }
    private static T GetField<T>(object o, string f) where T : class
    {
        var fi = o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic);
        fi.Should().NotBeNull();
        return (fi!.GetValue(o) as T)!;
    }
}
