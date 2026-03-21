using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SSH_Helper.Services.Scripting.Commands
{
    internal interface IBrowserCallbackFocusNativeMethods
    {
        bool IsIconic(IntPtr hWnd);
        bool SetForegroundWindow(IntPtr hWnd);
        bool ShowWindow(IntPtr hWnd, int nCmdShow);
        IntPtr GetForegroundWindow();
        uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        uint GetCurrentThreadId();
        bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        bool BringWindowToTop(IntPtr hWnd);
        IntPtr SetFocus(IntPtr hWnd);
    }

    internal static class BrowserCallbackFocusRestorer
    {
        private static readonly IBrowserCallbackFocusNativeMethods NativeMethods = new NativeMethodsAdapter();
        internal static Action<Form>? ScheduleUiActivationAttemptsOverrideForTests { get; set; }

        internal static void RequestApplicationFocusRestore()
        {
            try
            {
                var targetForm = GetTargetForm();
                if (targetForm != null)
                {
                    if (targetForm.InvokeRequired)
                    {
                        targetForm.BeginInvoke((Action)(() => ScheduleUiActivationAttempts(targetForm)));
                        return;
                    }

                    ScheduleUiActivationAttempts(targetForm);
                    return;
                }
            }
            catch
            {
                // Best-effort only. Focus behavior can still be blocked by OS foreground lock rules.
            }

            var handle = Process.GetCurrentProcess().MainWindowHandle;
            if (handle == IntPtr.Zero)
                return;

            NativeMethods.ShowWindow(handle, NativeMethodsAdapter.SW_RESTORE);
            NativeMethods.SetForegroundWindow(handle);
        }

        internal static void ScheduleUiActivationAttempts(Form form, IBrowserCallbackFocusNativeMethods? nativeMethods = null)
        {
            if (form.IsDisposed || !form.IsHandleCreated)
                return;

            var activationOverride = ScheduleUiActivationAttemptsOverrideForTests;
            if (activationOverride != null)
            {
                activationOverride(form);
                return;
            }

            var methods = nativeMethods ?? NativeMethods;
            var intervals = new[] { 350, 650, 1000, 1500, 2200 };
            TryActivateForm(form, methods);
            _ = RunRetryLoopAsync(form, methods, intervals);
        }

        internal static void TryActivateForm(Form form, IBrowserCallbackFocusNativeMethods? nativeMethods = null)
        {
            var methods = nativeMethods ?? NativeMethods;
            var handle = form.Handle;
            if (handle == IntPtr.Zero)
                return;

            var isMinimized = form.WindowState == FormWindowState.Minimized || methods.IsIconic(handle);
            if (isMinimized)
            {
                methods.ShowWindow(handle, NativeMethodsAdapter.SW_RESTORE);
            }

            var currentThreadId = methods.GetCurrentThreadId();
            var foregroundWindow = methods.GetForegroundWindow();
            var foregroundThreadId = foregroundWindow != IntPtr.Zero
                ? methods.GetWindowThreadProcessId(foregroundWindow, out _)
                : 0;

            var attached = false;
            try
            {
                if (foregroundWindow != IntPtr.Zero &&
                    foregroundThreadId != 0 &&
                    foregroundThreadId != currentThreadId)
                {
                    attached = methods.AttachThreadInput(currentThreadId, foregroundThreadId, true);
                }

                form.Show();
                form.TopMost = true;
                methods.BringWindowToTop(handle);
                form.BringToFront();
                form.Activate();
                methods.SetForegroundWindow(handle);
                methods.SetFocus(handle);
            }
            finally
            {
                form.TopMost = false;
                if (attached)
                {
                    methods.AttachThreadInput(currentThreadId, foregroundThreadId, false);
                }
            }
        }

        private static Form? GetTargetForm()
        {
            if (Application.OpenForms.Count <= 0)
                return null;

            if (Form.ActiveForm is Form activeForm && !activeForm.IsDisposed && activeForm.Visible)
                return activeForm;

            foreach (Form form in Application.OpenForms)
            {
                if (!form.IsDisposed && form.Visible)
                    return form;
            }

            var fallback = Application.OpenForms[0] as Form;
            if (fallback == null)
                return null;

            return fallback.IsDisposed ? null : fallback;
        }

        private static async Task RunRetryLoopAsync(
            Form form,
            IBrowserCallbackFocusNativeMethods methods,
            IReadOnlyList<int> intervals)
        {
            foreach (var interval in intervals)
            {
                try
                {
                    await Task.Delay(interval).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }

                try
                {
                    if (form.IsDisposed || !form.IsHandleCreated)
                        return;

                    if (form.InvokeRequired)
                    {
                        form.BeginInvoke((Action)(() =>
                        {
                            if (!form.IsDisposed && form.IsHandleCreated)
                            {
                                TryActivateForm(form, methods);
                            }
                        }));
                        continue;
                    }

                    TryActivateForm(form, methods);
                }
                catch
                {
                    return;
                }
            }
        }

        private sealed class NativeMethodsAdapter : IBrowserCallbackFocusNativeMethods
        {
            internal const int SW_RESTORE = 9;

            public bool IsIconic(IntPtr hWnd) => NativeIsIconic(hWnd);
            public bool SetForegroundWindow(IntPtr hWnd) => NativeSetForegroundWindow(hWnd);
            public bool ShowWindow(IntPtr hWnd, int nCmdShow) => NativeShowWindow(hWnd, nCmdShow);
            public IntPtr GetForegroundWindow() => NativeGetForegroundWindow();
            public uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId) => NativeGetWindowThreadProcessId(hWnd, out processId);
            public uint GetCurrentThreadId() => NativeGetCurrentThreadId();
            public bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach) => NativeAttachThreadInput(idAttach, idAttachTo, fAttach);
            public bool BringWindowToTop(IntPtr hWnd) => NativeBringWindowToTop(hWnd);
            public IntPtr SetFocus(IntPtr hWnd) => NativeSetFocus(hWnd);

            [DllImport("user32.dll", EntryPoint = "IsIconic")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool NativeIsIconic(IntPtr hWnd);

            [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool NativeSetForegroundWindow(IntPtr hWnd);

            [DllImport("user32.dll", EntryPoint = "ShowWindow")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool NativeShowWindow(IntPtr hWnd, int nCmdShow);

            [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
            private static extern IntPtr NativeGetForegroundWindow();

            [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
            private static extern uint NativeGetWindowThreadProcessId(IntPtr hWnd, out uint processId);

            [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
            private static extern uint NativeGetCurrentThreadId();

            [DllImport("user32.dll", EntryPoint = "AttachThreadInput")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool NativeAttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [DllImport("user32.dll", EntryPoint = "BringWindowToTop")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool NativeBringWindowToTop(IntPtr hWnd);

            [DllImport("user32.dll", EntryPoint = "SetFocus")]
            private static extern IntPtr NativeSetFocus(IntPtr hWnd);
        }
    }
}
