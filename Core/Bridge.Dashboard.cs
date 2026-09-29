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
        // ================= DASHBOARD =================
        public string DashboardInit()
        {
            Hello();

            // à¹‚à¸«à¸¡à¸”à¸­à¸­à¸™à¹„à¸¥à¸™à¹Œ: à¸–à¸²à¸¡ server à¸—à¸¸à¸à¸„à¸£à¸±à¹‰à¸‡à¸—à¸µà¹ˆà¹€à¸‚à¹‰à¸² Dashboard â€” à¸–à¸¹à¸à¸›à¸à¸´à¹€à¸ªà¸˜ (à¸£à¸µ/à¸£à¸°à¸‡à¸±à¸š/à¸œà¸¹à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸­à¸·à¹ˆà¸™) = à¸”à¸µà¸”à¸à¸¥à¸±à¸šà¸«à¸™à¹‰à¸² Activate
            // à¸ªà¹ˆà¸§à¸™ "à¹€à¸Šà¸·à¹ˆà¸­à¸¡à¸•à¹ˆà¸­ server à¹„à¸¡à¹ˆà¹„à¸”à¹‰" (à¸­à¸­à¸Ÿà¹„à¸¥à¸™à¹Œ) = à¸›à¸¥à¹ˆà¸­à¸¢à¸œà¹ˆà¸²à¸™ à¹ƒà¸Šà¹‰à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸•à¹ˆà¸­
            if (ServerOn && !LicenseService.IsAdmin() && _cfg.rememberKey && !string.IsNullOrWhiteSpace(_cfg.savedKey))
            {
                var (sok, smsg, _, soff) = ServerActivate(_cfg.savedKey, LicenseService.Hwid());
                if (!sok && !soff)
                {
                    _log.Warn("server à¸›à¸à¸´à¹€à¸ªà¸˜ key à¸—à¸µà¹ˆà¸ˆà¸³à¹„à¸§à¹‰: " + smsg + " â†’ à¸à¸¥à¸±à¸šà¸«à¸™à¹‰à¸² Activate");
                    _cfg.rememberKey = false;
                    _cfg.savedKey = "";
                    SaveCfg();
                    _win.Dispatcher.BeginInvoke(new Action(_win.NavigateLogin));
                    return J(new { ok = false, kick = true, msg = smsg });
                }
                if (!sok && soff) _log.Warn("server à¹€à¸‚à¹‰à¸²à¹„à¸¡à¹ˆà¸–à¸¶à¸‡ â€” à¹ƒà¸Šà¹‰à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸•à¹ˆà¸­à¸Šà¸±à¹ˆà¸§à¸„à¸£à¸²à¸§");
            }

            foreach (var kv in _cfg.toggles)
            {
                var t = _all.FirstOrDefault(x => x.Id == kv.Key);
                if (t != null) t.IsSelected = kv.Value;
            }

            _stats.Start();

            var lic = LicenseService.RecoverForThisMachine();
            int total = _all.Count(x => x.Recommended);
            if (_cfg.lastTotal <= 0) { _cfg.lastTotal = total; SaveCfg(); }

            _log.Head("Dashboard à¸žà¸£à¹‰à¸­à¸¡à¹ƒà¸Šà¹‰à¸‡à¸²à¸™ â€” real-time monitor à¹€à¸£à¸´à¹ˆà¸¡à¸—à¸³à¸‡à¸²à¸™");

            return J(new
            {
                ok = true,
                version = AppVersion,
                toggles = _cfg.toggles,
                powerPlan = _cfg.powerPlan,
                score = _cfg.lastScore,
                applied = _cfg.lastApplied,
                total,
                license = lic == null
                    ? (object)new { key = "â€”", hwid = LicenseService.Hwid(), days = 0, status = "Inactive" }
                    : new { key = lic.key, hwid = lic.hwid, days = LicenseService.DaysLeft(lic), status = "Active" }
            });
        }

        public string Stats()
        {
            var s = _stats.Latest;
            if (s == null) return J(new { ready = false });
            return J(new
            {
                ready = true,
                cpu = s.cpuPct, cpuTemp = s.cpuTemp,
                gpu = s.gpuPct, gpuTemp = s.gpuTemp,
                ramUsed = Math.Round(s.ramUsedGb, 1),
                ramTotal = Math.Round(s.ramTotalGb, 0),
                ramPct = s.ramPct,
                ramCached = Math.Round(s.ramCachedGb, 1),
                ramFree = Math.Round(s.ramFreeGb, 1),
                ping = s.pingMs,
                jitter = s.jitterMs,
                diskPct = s.diskPct,
                diskUsed = Math.Round(s.diskUsedGb, 0),
                diskTotal = Math.Round(s.diskTotalGb, 0)
            });
        }
    }
}
