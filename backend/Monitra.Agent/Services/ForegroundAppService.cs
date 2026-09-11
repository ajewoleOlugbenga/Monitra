using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Monitra.Agent.Services;

/// <summary>
/// Identifies the process behind the currently focused window via Win32 (GetForegroundWindow +
/// GetWindowThreadProcessId). Like idle detection, this needs OS-level access no browser or web
/// app can get to.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ForegroundAppService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static string? GetForegroundProcessName()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process exited between the two calls above.
            return null;
        }
    }
}
