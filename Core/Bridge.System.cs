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
                // à¸§à¸±à¸”à¸‚à¸™à¸²à¸”à¸à¹ˆà¸­à¸™ â†’ à¸¥à¹‰à¸²à¸‡à¸”à¹‰à¸§à¸¢à¸ªà¸„à¸£à¸´à¸›à¸•à¹Œà¸‚à¸­à¸‡à¹€à¸£à¸² (12_System_Tools -Action junk) â†’ à¸§à¸±à¸”à¸«à¸¥à¸±à¸‡
                var before = CleanerService.Measure(null);
                double beforeTotal = CleanerService.Total(before);
                _log.Info($"à¸¥à¹‰à¸²à¸‡à¸‚à¸¢à¸°à¸”à¹‰à¸§à¸¢ 12_System_Tools.ps1 -Action junk (à¸à¹ˆà¸­à¸™ {beforeTotal:N2} GB)...");
                TweakRegistry.SystemTool(_log, "junk");

                var after = CleanerService.Measure(null);
                double total = Math.Round(Math.Max(0, beforeTotal - CleanerService.Total(after)), 2);

                var cats = before.Zip(after, (b, a) => new
                {
                    id = b.Id,
                    name = b.Name,
                    gb = Math.Max(0, Math.Round(b.Gb - a.Gb, 2))
                }).ToList();

                _log.Ok($"Junk Cleaner à¸„à¸·à¸™à¸žà¸·à¹‰à¸™à¸—à¸µà¹ˆ {total:N2} GB");
                return J(new { ok = true, cats, total });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        public string FreeStandby()
        {
            Hello();
            try
            {
                double freed = Memory.PurgeStandby();
                _log.Ok($"Purge Standby List: à¸„à¸·à¸™ RAM à¸§à¹ˆà¸²à¸‡ {freed:N2} GB");
                return J(new { ok = true, freed = Math.Round(freed, 2) });
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
        // ---------- POWER PLAN DEDUPE (à¸•à¸£à¸§à¸ˆ + à¸¥à¸šà¹à¸œà¸™à¹„à¸Ÿà¸‹à¹‰à¸³ à¹€à¸«à¸¥à¸·à¸­à¸­à¸±à¸™à¹€à¸”à¸µà¸¢à¸§à¸•à¹ˆà¸­à¸Šà¸·à¹ˆà¸­) ----------
    }
}