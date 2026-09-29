using System;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace KingR9Tools.Core
{
    public sealed class StatsSnapshot
    {
        public int cpuPct = -1, gpuPct = -1;
        public string cpuTemp = "-", gpuTemp = "-";
        public double ramUsedGb, ramTotalGb, ramCachedGb, ramFreeGb;
        public int ramPct;
        public int pingMs = -1, jitterMs = -1;
        public double diskUsedGb, diskTotalGb;
        public int diskPct = -1;
    }

    /// <summary>ดึงสถิติจริงของเครื่องทุก 2 วินาที แล้ว push ให้ UI</summary>
    public class StatsService : IDisposable
    {
        private readonly DispatcherTimer _t;
        private int _busy;
        private string _smi;
        private int _lastPing = -1;

        /// <summary>สแนปช็อตล่าสุด (JS จะมา poll ค่านี้)</summary>
        public StatsSnapshot Latest { get; private set; }

        public StatsService()
        {
            _t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _t.Tick += async (s, e) => await Poll();
        }

        public void Start()
        {
            Task.Run(() => _smi = FindNvidiaSmi());
            _t.Start();
        }

        public void Stop() { _t.Stop(); }

        private static string FindNvidiaSmi()
        {
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (var p in new[]
            {
                Path.Combine(win, "System32", "nvidia-smi.exe"),
                Path.Combine(pf, "NVIDIA Corporation", "NVSMI", "nvidia-smi.exe")
            })
                if (File.Exists(p)) return p;
            return null;
        }

        private async Task Poll()
        {
            if (System.Threading.Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                var snap = await Task.Run(() =>
                {
                    var s = new StatsSnapshot();

                    // CPU LOAD
                    try
                    {
                        using var mos = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                        foreach (ManagementObject o in mos.Get())
                        using (o) { s.cpuPct = Convert.ToInt32(o["LoadPercentage"]); break; }
                    }
                    catch { }

                    // CPU TEMP (บางเมนบอร์ดไม่เปิด ACPI — ไม่เป็นไร ใช้ค่า "-" ได้)
                    try
                    {
                        using var mos = new ManagementObjectSearcher(@"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                        foreach (ManagementObject o in mos.Get())
                        using (o)
                        {
                            double ct = Convert.ToDouble(o["CurrentTemperature"]) / 10.0 - 273.15;
                            if (ct >= 25 && ct <= 110) s.cpuTemp = Math.Round(ct) + "°C";
                            break;
                        }
                    }
                    catch { }

                    // GPU (nvidia-smi ถ้ามีการ์ด NVIDIA)
                    if (_smi != null)
                    {
                        string outp = Sys.Run(_smi, "--query-gpu=utilization.gpu,temperature.gpu --format=csv,noheader,nounits");
                        var parts = (outp ?? "").Split(',');
                        if (parts.Length >= 2)
                        {
                            if (int.TryParse(parts[0].Trim(), out int gu)) s.gpuPct = gu;
                            if (int.TryParse(parts[1].Trim(), out int gt)) s.gpuTemp = gt + "°C";
                        }
                    }

                    // RAM
                    var ram = Memory.GetRam();
                    s.ramUsedGb = ram.UsedGb; s.ramTotalGb = ram.TotalGb;
                    s.ramCachedGb = ram.CachedGb; s.ramFreeGb = ram.FreeGb; s.ramPct = ram.Pct;

                    // DISK (C:)
                    try
                    {
                        var drv = new DriveInfo("C");
                        s.diskTotalGb = Math.Round(drv.TotalSize / 1073741824.0, 0);
                        s.diskUsedGb = Math.Round((drv.TotalSize - drv.AvailableFreeSpace) / 1073741824.0, 0);
                        s.diskPct = (int)Math.Round((drv.TotalSize - drv.AvailableFreeSpace) * 100.0 / drv.TotalSize);
                    }
                    catch { }

                    // PING
                    try
                    {
                        using var p = new Ping();
                        var reply = p.Send("1.1.1.1", 1200);
                        if (reply.Status == IPStatus.Success)
                        {
                            s.pingMs = (int)reply.RoundtripTime;
                            if (_lastPing >= 0) s.jitterMs = Math.Abs(s.pingMs - _lastPing);
                            _lastPing = s.pingMs;
                        }
                    }
                    catch { }

                    return s;
                });

                Latest = snap;
            }
            finally { System.Threading.Interlocked.Exchange(ref _busy, 0); }
        }

        public void Dispose() { _t.Stop(); }
    }
}