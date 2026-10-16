namespace Touchstone.Benchmark.Workload
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    internal static class ProcessMemory
    {
        [DllImport("libc", EntryPoint = "getrusage", SetLastError = true)]
        private static extern int GetResourceUsage(int who, IntPtr usage);

        internal static long PeakBytes()
        {
            using (Process current = Process.GetCurrentProcess())
            {
                long peak = current.PeakWorkingSet64;
                if (peak > 0) return peak;
            }
            // On macOS the managed process API can return zero. Darwin rusage
            // contains two 16-byte timevals followed by ru_maxrss in bytes.
            if (OperatingSystem.IsMacOS())
            {
                IntPtr usage = Marshal.AllocHGlobal(256);
                try
                {
                    if (GetResourceUsage(0, usage) != 0) throw new InvalidOperationException("getrusage failed: " + Marshal.GetLastPInvokeError());
                    return Marshal.ReadInt64(usage, 32);
                }
                finally { Marshal.FreeHGlobal(usage); }
            }
            throw new PlatformNotSupportedException("Peak working set is unavailable on this platform.");
        }
    }
}
