using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Monitra.Agent.Services;

/// <summary>
/// System-wide keyboard/mouse idle time via the Win32 GetLastInputInfo API. This measures
/// input across the whole OS, not just this process - something no web page or browser
/// extension can observe, which is the core reason this feature requires a native agent.
/// </summary>
[SupportedOSPlatform("windows")]
public static class IdleTimeService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var idleTicks = (uint)Environment.TickCount - info.dwTime;
        return TimeSpan.FromMilliseconds(idleTicks);
    }
}
