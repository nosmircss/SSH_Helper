using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FluentAssertions;
using SSH_Helper.Services.Scripting.Commands;
using Xunit;

namespace SSH_Helper.Tests.UI;

[Collection(CallbackUiSerialCollection.Name)]
public class BrowserCallbackFocusRestorerTests
{
    [Fact]
    public void NativeMethodsAdapter_ImportsUseRealWindowsEntryPoints()
    {
        var adapterType = typeof(BrowserCallbackFocusRestorer).GetNestedType("NativeMethodsAdapter", BindingFlags.NonPublic);
        adapterType.Should().NotBeNull();
        if (adapterType == null)
            return;

        var expectedEntryPoints = new Dictionary<string, string>
        {
            ["NativeIsIconic"] = "IsIconic",
            ["NativeSetForegroundWindow"] = "SetForegroundWindow",
            ["NativeShowWindow"] = "ShowWindow",
            ["NativeGetForegroundWindow"] = "GetForegroundWindow",
            ["NativeGetWindowThreadProcessId"] = "GetWindowThreadProcessId",
            ["NativeGetCurrentThreadId"] = "GetCurrentThreadId",
            ["NativeAttachThreadInput"] = "AttachThreadInput",
            ["NativeBringWindowToTop"] = "BringWindowToTop",
            ["NativeSetFocus"] = "SetFocus"
        };

        foreach (var expected in expectedEntryPoints)
        {
            var method = adapterType.GetMethod(expected.Key, BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull($"the focus restorer should declare {expected.Key}");
            if (method == null)
                continue;

            var import = method.GetCustomAttribute<DllImportAttribute>();
            import.Should().NotBeNull($"the native method {expected.Key} should remain a DllImport");
            if (import == null)
                continue;

            (string.IsNullOrWhiteSpace(import.EntryPoint) ? method.Name : import.EntryPoint)
                .Should().Be(expected.Value);
        }
    }

    [WinFormsFact]
    public void ScheduleUiActivationAttempts_AttemptsActivationImmediatelyBeforeRetryLoop()
    {
        using var form = new Form();
        form.Show();
        Application.DoEvents();

        var native = new RecordingNativeMethods
        {
            ForegroundWindow = form.Handle,
            CurrentThreadId = 77
        };

        native.ThreadIds[form.Handle] = 77;

        BrowserCallbackFocusRestorer.ScheduleUiActivationAttempts(form, native);

        native.Calls.Should().ContainInOrder(
            $"BringWindowToTop({form.Handle})",
            $"SetForegroundWindow({form.Handle})",
            $"SetFocus({form.Handle})");
    }

    [WinFormsFact]
    public void TryActivateForm_WhenForegroundThreadDiffers_AttachesInputQueuesAroundForegroundSwitch()
    {
        using var form = new Form();
        form.Show();
        Application.DoEvents();

        var foregroundWindow = new IntPtr(1234);
        var native = new RecordingNativeMethods
        {
            ForegroundWindow = foregroundWindow,
            CurrentThreadId = 77
        };

        native.ThreadIds[form.Handle] = 77;
        native.ThreadIds[foregroundWindow] = 88;

        BrowserCallbackFocusRestorer.TryActivateForm(form, native);

        native.Calls.Should().ContainInOrder(
            "AttachThreadInput(77,88,True)",
            $"BringWindowToTop({form.Handle})",
            $"SetForegroundWindow({form.Handle})",
            $"SetFocus({form.Handle})",
            "AttachThreadInput(77,88,False)");
    }

    private sealed class RecordingNativeMethods : IBrowserCallbackFocusNativeMethods
    {
        public Dictionary<IntPtr, uint> ThreadIds { get; } = new();
        public List<string> Calls { get; } = new();
        public IntPtr ForegroundWindow { get; set; }
        public uint CurrentThreadId { get; set; }

        public bool IsIconic(IntPtr hWnd)
        {
            Calls.Add($"IsIconic({hWnd})");
            return false;
        }

        public bool ShowWindow(IntPtr hWnd, int nCmdShow)
        {
            Calls.Add($"ShowWindow({hWnd},{nCmdShow})");
            return true;
        }

        public IntPtr GetForegroundWindow()
        {
            Calls.Add("GetForegroundWindow()");
            return ForegroundWindow;
        }

        public uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId)
        {
            Calls.Add($"GetWindowThreadProcessId({hWnd})");
            processId = 0;
            return ThreadIds.TryGetValue(hWnd, out var threadId) ? threadId : 0;
        }

        public uint GetCurrentThreadId()
        {
            Calls.Add("GetCurrentThreadId()");
            return CurrentThreadId;
        }

        public bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach)
        {
            Calls.Add($"AttachThreadInput({idAttach},{idAttachTo},{fAttach})");
            return true;
        }

        public bool BringWindowToTop(IntPtr hWnd)
        {
            Calls.Add($"BringWindowToTop({hWnd})");
            return true;
        }

        public IntPtr SetFocus(IntPtr hWnd)
        {
            Calls.Add($"SetFocus({hWnd})");
            return hWnd;
        }

        public bool SetForegroundWindow(IntPtr hWnd)
        {
            Calls.Add($"SetForegroundWindow({hWnd})");
            return true;
        }
    }
}
