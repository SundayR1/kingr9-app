using System;
using System.Runtime.InteropServices;

namespace KingR9Tools.Core
{
    public static class Memory
    {
        // ---------- RAM ----------
        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile;
            public ulong ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX b);

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public UIntPtr CommitTotal, CommitLimit, CommitPeak;
            public UIntPtr PhysicalTotal, PhysicalAvailable;
            public UIntPtr SystemCache, KernelTotal, KernelPaged, KernelNonpaged;
            public UIntPtr PageSize;
            public uint HandleCount, ProcessCount, ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(ref PERFORMANCE_INFORMATION p, uint cb);

        public sealed class RamInfo { public double TotalGb, UsedGb, FreeGb, CachedGb; public int Pct; }

        public static RamInfo GetRam()
        {
            var b = new MEMORYSTATUSEX();
            if (!GlobalMemoryStatusEx(b)) return new RamInfo();
            double tot = b.ullTotalPhys / 1073741824.0;
            double free = b.ullAvailPhys / 1073741824.0;
            double cached = 0;
            try
            {
                var pi = new PERFORMANCE_INFORMATION { cb = (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION)) };
                if (GetPerformanceInfo(ref pi, pi.cb))
                    cached = Convert.ToDouble(pi.SystemCache) * Convert.ToDouble(pi.PageSize) / 1073741824.0;
            }
            catch { }
            double used = tot - free;
            return new RamInfo
            {
                TotalGb = tot,
                UsedGb = used,
                FreeGb = free,
                CachedGb = cached,
                Pct = tot > 0 ? (int)Math.Round(used / tot * 100.0) : 0
            };
        }

    }
}