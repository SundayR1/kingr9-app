using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace KingR9Tools.Core
{
    public partial class Bridge
    {
        public string Junk()
        {
            Hello();
            try
            {
                var cats = CleanerService.Measure(_log);
                return J(new
                {
                    ok = true,
                    cats = cats.Select(c => new { id = c.Id, name = c.Name, gb = c.Gb }),
                    total = CleanerService.Total(cats)
                });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        public string Clean()
        {
            Hello();
            try
            {
                // วัดขนาดก่อน → ล้างด้วยสคริปต์ของเรา (12_System_Tools -Action junk) → วัดหลัง
                var before = CleanerService.Measure(null);
                double beforeTotal = CleanerService.Total(before);
                _log.Info($"ล้างขยะด้วย 12_System_Tools.ps1 -Action junk (ก่อน {beforeTotal:N2} GB)...");
                TweakRegistry.SystemTool(_log, "junk");

                var after = CleanerService.Measure(null);
                double total = Math.Round(Math.Max(0, beforeTotal - CleanerService.Total(after)), 2);

                var cats = before.Zip(after, (b, a) => new
                {
                    id = b.Id,
                    name = b.Name,
                    gb = Math.Max(0, Math.Round(b.Gb - a.Gb, 2))
                }).ToList();

                _log.Ok($"Junk Cleaner คืนพื้นที่ {total:N2} GB");
                return J(new { ok = true, cats, total });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        public string OpenFolder(string which)
        {
            try
            {
                string dir = which == "backup" ? BackupService.BackupDir : BackupService.RootDir;
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex) { _log.Err("open: " + ex.Message); }
            return J(new { ok = true });
        }

        // ---------- OPTIMIZE / RESTORE ----------
        // ---------- POWER PLAN DEDUPE (ตรวจ + ลบแผนไฟซ้ำ เหลืออันเดียวต่อชื่อ) ----------
    }
}