using System;
using System.IO;
using System.Linq;

namespace KingR9Tools.Core
{
    public static class BackupService
    {
        public static string RootDir =>
            Path.Combine(Sys.Docs, "KingR9Tools");
        public static string BackupDir =>
            Path.Combine(RootDir, "Backup");

        /// <summary>โฟลเดอร์ backup ล่าสุด (ตาม yyyyMMdd_HHmmss) หรือ null ถ้ายังไม่เคย</summary>
        public static string LatestBackupDir()
        {
            if (!Directory.Exists(BackupDir)) return null;
            return Directory.GetDirectories(BackupDir)
                .OrderByDescending(d => Path.GetFileName(d))
                .FirstOrDefault();
        }

        /// <summary>เวลาตั้งแต่ backup ล่าสุด — MaxValue ถ้ายังไม่เคย</summary>
        public static TimeSpan LastBackupAge()
        {
            var latest = LatestBackupDir();
            if (latest == null) return TimeSpan.MaxValue;
            var info = new DirectoryInfo(latest);
            return DateTime.Now - info.CreationTime;
        }

        /// <summary>
        /// สำรอง Registry + BCD + netsh + services
        /// <para><paramref name="force"/>= true → สำรองทุกครั้ง</para>
        /// <para><paramref name="force"/>= false → ข้ามถ้า backup แล้วภายใน 1 ชม.</para>
        /// </summary>
        public static string Create(Logger log, bool force = false)
        {
            if (!force && LastBackupAge() < TimeSpan.FromHours(1))
            {
                string existing = LatestBackupDir();
                log.Info($"ข้ามสำรอง (สำรองไว้แล้วภายใน 1 ชม. → {Path.GetFileName(existing)})");
                return existing;
            }

            log.Head("กำลังสำรองข้อมูลระบบ...");
            string dir = Path.Combine(BackupDir, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dir);

            var keys = new (string name, string path)[]
            {
                ("Tcpip",    @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip"),
                ("AFD",      @"HKLM\SYSTEM\CurrentControlSet\Services\AFD"),
                ("Dnscache", @"HKLM\SYSTEM\CurrentControlSet\Services\Dnscache"),
                ("Psched",   @"HKLM\SYSTEM\CurrentControlSet\Services\Psched"),
                ("MMCSS",    @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia"),
                ("NICClass", Sys.NicClassRoot),
                ("IFEO",     @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options"),
                ("Power",    @"HKLM\SYSTEM\CurrentControlSet\Control\Power"),
                ("Kernel",   @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel"),
            };

            foreach (var k in keys)
            {
                Sys.Run("reg.exe", $"export \"{k.path}\" \"{Path.Combine(dir, k.name)}.reg\" /y");
                log.Ok($"export {k.name}.reg");
            }

            Sys.Cmd($"bcdedit /export \"{Path.Combine(dir, "bcd.bak")}\"");
            File.WriteAllText(Path.Combine(dir, "netsh_tcp.txt"), Sys.Netsh("int tcp show global"));
            File.WriteAllText(Path.Combine(dir, "powercfg_list.txt"), Sys.Cmd("powercfg /list"));
            File.WriteAllText(Path.Combine(dir, "services.txt"), Sys.Cmd("sc query type= service state= all"));

            log.Ok($"สำรองเสร็จ → {dir}");
            return dir;
        }
    }
}