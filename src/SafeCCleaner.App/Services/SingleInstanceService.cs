using System.Runtime.InteropServices;

namespace SafeCCleaner.App.Services;

internal static class SingleInstanceService
{
    private const string MutexName = @"Local\SafeCCleaner.SingleInstance.v1";
    private const string ShowMessageName = "SafeCCleaner.ShowExistingWindow.v1";
    private static Mutex? _mutex;

    public static uint ShowExistingWindowMessage { get; } = RegisterWindowMessage(ShowMessageName);

    public static bool TryAcquire()
    {
        if (_mutex is not null)
            return true;

        try
        {
            _mutex = new Mutex(false, MutexName, out var createdNew);
            if (createdNew)
                return true;

            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch (Exception ex)
        {
            // Failing open is safer than preventing the application from starting at all.
            DiagnosticLog.Write("Single-instance guard could not be initialized.", ex);
            return true;
        }
    }

    public static void SignalExistingInstance()
    {
        try
        {
            if (ShowExistingWindowMessage == 0)
                return;

            // Repeat briefly to cover the small race where the primary process owns
            // the mutex but is still creating its main window/message hook.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                PostMessage(new IntPtr(0xffff), ShowExistingWindowMessage, IntPtr.Zero, IntPtr.Zero); // HWND_BROADCAST
                if (attempt < 2)
                    Thread.Sleep(80);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Write("Could not signal existing Safe C: Cleaner instance.", ex);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
