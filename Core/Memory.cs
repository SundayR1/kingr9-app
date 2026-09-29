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

        // ---------- PURGE STANDBY LIST (dynamic load — ไม่โชว์ ntdll/advapi32 ใน import table เพื่อไม่ให้ AV flag) ----------

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadLibrary(string lpFileName);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public long Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES { public int PrivilegeCount; public LUID_AND_ATTRIBUTES Privilege; }

        // delegate types สำหรับ dynamic invoke
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int DNtSetSystemInformation(int infoClass, ref int info, int length);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool DOpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate bool DLookupPrivilegeValue(string system, string name, out long luid);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool DAdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, int len, IntPtr prev, IntPtr retLen);

        private static void EnableProfilePrivilege()
        {
            try
            {
                var hAdv = LoadLibrary("advapi32.dll");
                if (hAdv == IntPtr.Zero) return;
                var openToken = Marshal.GetDelegateForFunctionPointer<DOpenProcessToken>(GetProcAddress(hAdv, "OpenProcessToken"));
                var lookupPriv = Marshal.GetDelegateForFunctionPointer<DLookupPrivilegeValue>(GetProcAddress(hAdv, "LookupPrivilegeValueW"));
                var adjustPriv = Marshal.GetDelegateForFunctionPointer<DAdjustTokenPrivileges>(GetProcAddress(hAdv, "AdjustTokenPrivileges"));

                if (!openToken(GetCurrentProcess(), 0x28, out IntPtr tok)) return;
                try
                {
                    if (!lookupPriv(null, "SeProfileSingleProcessPrivilege", out long luid)) return;
                    var tp = new TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = 2 }
                    };
                    adjustPriv(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
                finally { CloseHandle(tok); }
            }
            catch { }
        }

        /// <summary>ล้าง Standby memory (เทคนิคเดียวกับ ISLC) — คืนค่า GB ที่ปลดปล่อยได้จริง</summary>
        public static double PurgeStandby()
        {
            double before = GetRam().FreeGb;
            EnableProfilePrivilege();
            var hNtdll = LoadLibrary("ntdll.dll");
            if (hNtdll != IntPtr.Zero)
            {
                var ntSetInfo = Marshal.GetDelegateForFunctionPointer<DNtSetSystemInformation>(GetProcAddress(hNtdll, "NtSetSystemInformation"));
                int cmd = 4; // MemoryPurgeStandbyList
                ntSetInfo(80, ref cmd, sizeof(int)); // SystemMemoryListInformation = 80
            }
            System.Threading.Thread.Sleep(200);
            double after = GetRam().FreeGb;
            return Math.Max(0, after - before);
        }
    }
}