using System.Runtime.InteropServices;

namespace Api.Services;

/// <summary>
/// One host-load reading. Serialised as camelCase, and the first three members keep the
/// original names (cpu / ram / disk) so anything already consuming /api/metrics sees the
/// same contract it saw before — everything after them is additive detail for the panel.
/// </summary>
public sealed record MetricsSnapshot(
    double Cpu,
    double Ram,
    double Disk,
    int Cores,
    string Host,
    string Os,
    long UptimeSeconds,
    long RamUsedBytes,
    long RamTotalBytes,
    long DiskUsedBytes,
    long DiskTotalBytes,
    string DiskDrive);

/// <summary>
/// Host load sampler behind the Dashboard's System Metrics panel.
///
/// WHY WIN32 AND NOT PerformanceCounter: the counter strings ("Processor", "% Processor Time",
/// "Memory") only exist in an English Windows. On a localised install — the office Windows
/// Server, for instance — constructing them throws, and /api/metrics dies with a 500. The two
/// kernel32 calls below have no locale-dependent names, cost microseconds, and need no NuGet
/// package, which also keeps the production deployment self-contained (that host has no
/// internet).
///
/// WHY A SINGLETON: CPU usage is a DELTA between two samples, so the sampler has to remember
/// the previous reading. A per-request instance could never compute one, which is why the
/// previous implementation slept 100ms inside the HTTP handler to fake an interval. A shared
/// instance turns the gap between two polls into the measurement window instead.
/// </summary>
public sealed class SystemMetricsService
{
    // 10 000 ticks = 1ms; a window shorter than this is too noisy to report as a percentage.
    private const long MinCpuWindowTicks = 200_000;

    private readonly object _gate = new();
    private long _prevIdle, _prevKernel, _prevUser;
    private double _lastCpu;

    public SystemMetricsService()
    {
        if (OperatingSystem.IsWindows())
        {
            // Prime the baseline so the first real request measures a real interval rather
            // than reporting 0 (or a spike) off a zero-delta window.
            SampleCpu();
        }
    }

    public MetricsSnapshot Sample()
    {
        var cpu = OperatingSystem.IsWindows() ? SampleCpu() : _lastCpu;
        var ram = SampleRam();
        var disk = SampleDisk();

        return new MetricsSnapshot(
            Cpu: Round(cpu),
            Ram: Round(ram.Percent),
            Disk: Round(disk.Percent),
            Cores: Environment.ProcessorCount,
            Host: Environment.MachineName,
            Os: RuntimeInformation.OSDescription,
            UptimeSeconds: Environment.TickCount64 / 1000,
            RamUsedBytes: ram.Used,
            RamTotalBytes: ram.Total,
            DiskUsedBytes: disk.Used,
            DiskTotalBytes: disk.Total,
            DiskDrive: disk.Drive);
    }

    private static double Round(double v) => Math.Round(Math.Clamp(v, 0, 100), 1);

    // ---- CPU ---------------------------------------------------------------------------

    private double SampleCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return _lastCpu;

        lock (_gate)
        {
            // kernel time INCLUDES idle time, so the window length is kernel+user and the
            // busy part of it is that total minus idle.
            var idleDelta = idle - _prevIdle;
            var totalDelta = (kernel - _prevKernel) + (user - _prevUser);

            _prevIdle = idle;
            _prevKernel = kernel;
            _prevUser = user;

            if (totalDelta >= MinCpuWindowTicks)
            {
                _lastCpu = Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100);
            }

            return _lastCpu;
        }
    }

    // ---- RAM ---------------------------------------------------------------------------

    private static (double Percent, long Used, long Total) SampleRam()
    {
        if (!OperatingSystem.IsWindows()) return (0, 0, 0);

        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status)) return (0, 0, 0);

        var total = (long)status.ullTotalPhys;
        var used = total - (long)status.ullAvailPhys;

        // Derived from the same two numbers the panel prints in GB, so the percentage and the
        // "9.4 / 15.9 GB" caption can never disagree with each other.
        var percent = total > 0 ? used * 100.0 / total : 0;
        return (percent, used, total);
    }

    // ---- Disk --------------------------------------------------------------------------

    private static (double Percent, long Used, long Total, string Drive) SampleDisk()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        try
        {
            var drive = new DriveInfo(root);
            var used = drive.TotalSize - drive.AvailableFreeSpace;
            var percent = drive.TotalSize > 0 ? used * 100.0 / drive.TotalSize : 0;
            return (percent, used, drive.TotalSize, drive.Name.TrimEnd('\\', '/'));
        }
        catch (Exception)
        {
            // A drive query can fail on a locked-down host; reporting "no disk data" must not
            // take the whole endpoint (and with it the panel) down.
            return (0, 0, 0, root.TrimEnd('\\', '/'));
        }
    }

    // ---- interop -----------------------------------------------------------------------

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

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
}
