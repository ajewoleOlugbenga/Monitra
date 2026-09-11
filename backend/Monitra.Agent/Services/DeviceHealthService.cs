using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Windows.Forms;

namespace Monitra.Agent.Services;

public record DeviceHealthReading(
    double CpuUsagePercent,
    double MemoryUsagePercent,
    double DiskUsagePercent,
    double? BatteryPercent,
    double DiskFreeGb,
    string TopProcessesJson);

/// <summary>
/// Samples system-wide resource usage for the IT fleet-health dashboard. Like idle/foreground
/// tracking, this needs OS-level access no web app can get to.
/// </summary>
[SupportedOSPlatform("windows")]
public class DeviceHealthService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    // Simple, dependency-free CPU sample: total processor time across all processes over a
    // short window, versus wall-clock time elapsed x core count. Good enough for a fleet-health
    // gauge; not as precise as a PerformanceCounter warm-started ahead of time.
    public async Task<DeviceHealthReading> SampleAsync(CancellationToken cancellationToken)
    {
        var cpuBefore = GetTotalProcessorTime();
        var wallBefore = DateTime.UtcNow;
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        var cpuAfter = GetTotalProcessorTime();
        var wallAfter = DateTime.UtcNow;

        var cpuDelta = (cpuAfter - cpuBefore).TotalMilliseconds;
        var wallDelta = (wallAfter - wallBefore).TotalMilliseconds * Environment.ProcessorCount;
        var cpuPercent = wallDelta > 0 ? Math.Clamp(cpuDelta / wallDelta * 100.0, 0, 100) : 0;

        var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        double memPercent = 0;
        if (GlobalMemoryStatusEx(ref memStatus))
        {
            memPercent = memStatus.dwMemoryLoad;
        }

        var systemDrive = DriveInfo.GetDrives()
            .FirstOrDefault(d => d.IsReady && d.Name.StartsWith(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\"));

        double diskPercent = 0;
        double diskFreeGb = 0;
        if (systemDrive != null)
        {
            diskFreeGb = systemDrive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            diskPercent = 100.0 * (1 - (double)systemDrive.AvailableFreeSpace / systemDrive.TotalSize);
        }

        double? batteryPercent = null;
        var powerStatus = SystemInformation.PowerStatus;
        if (powerStatus.BatteryChargeStatus != BatteryChargeStatus.NoSystemBattery)
        {
            batteryPercent = Math.Round(powerStatus.BatteryLifePercent * 100.0, 1);
        }

        var topProcesses = GetTopProcessesByMemory(5);

        return new DeviceHealthReading(
            Math.Round(cpuPercent, 1),
            Math.Round(memPercent, 1),
            Math.Round(diskPercent, 1),
            batteryPercent,
            Math.Round(diskFreeGb, 2),
            topProcesses);
    }

    private static TimeSpan GetTotalProcessorTime()
    {
        var total = TimeSpan.Zero;
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                total += process.TotalProcessorTime;
            }
            catch
            {
                // Access-denied on some system/elevated processes is expected when the agent
                // runs unprivileged - skip rather than fail the whole sample.
            }
            finally
            {
                process.Dispose();
            }
        }
        return total;
    }

    private static string GetTopProcessesByMemory(int count)
    {
        var top = System.Diagnostics.Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    return new { p.ProcessName, MemoryMb = Math.Round(p.WorkingSet64 / 1024.0 / 1024.0, 1) };
                }
                catch
                {
                    return null;
                }
                finally
                {
                    p.Dispose();
                }
            })
            .Where(x => x != null)
            .GroupBy(x => x!.ProcessName)
            .Select(g => new { ProcessName = g.Key, MemoryMb = g.Sum(x => x!.MemoryMb) })
            .OrderByDescending(x => x.MemoryMb)
            .Take(count)
            .ToList();

        return JsonSerializer.Serialize(top);
    }
}
