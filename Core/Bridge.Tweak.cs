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
        public string AresRun(bool doUpdate)
        {
            Hello();
            string script = Path.Combine(WebAssets.Root, "Scripts", "Ares", "optimizer.ps1");
            if (!File.Exists(script))
                return J(new { ok = false, msg = "à¹„à¸¡à¹ˆà¸žà¸š Ares optimizer.ps1 â€” à¸¥à¸­à¸‡à¸›à¸´à¸”à¹à¸¥à¹‰à¸§à¹€à¸›à¸´à¸”à¹‚à¸›à¸£à¹à¸à¸£à¸¡à¹ƒà¸«à¸¡à¹ˆ" });
            if (!JobBegin("Ares One-Click Optimizer"))
                return J(new { ok = false, msg = "à¸¡à¸µà¸‡à¸²à¸™à¸à¸³à¸¥à¸±à¸‡à¸—à¸³à¸‡à¸²à¸™à¸­à¸¢à¸¹à¹ˆ â€” à¸£à¸­à¸ˆà¸šà¸à¹ˆà¸­à¸™à¹à¸¥à¹‰à¸§à¸à¸”à¹ƒà¸«à¸¡à¹ˆ" });
            var creep = Task.Run(CreepLoop);
            try
            {
                string vive = Path.Combine(WebAssets.Root, "Scripts", "Ares", "vivetool", "ViVeTool.exe");
                // ดาวน์โหลด ViVeTool จาก GitHub ถ้ายังไม่มี (ไม่ฝังใน exe อีกต่อไปเพื่อไม่ให้ AV flag)
                if (!File.Exists(vive))
                {
                    try
                    {
                        string vtDir = Path.GetDirectoryName(vive);
                        Directory.CreateDirectory(vtDir);
                        string zipPath = Path.Combine(vtDir, "vivetool.zip");
                        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                        var bytes = http.GetByteArrayAsync("https://github.com/thebookisclosed/ViVe/releases/download/v0.3.3/ViVeTool-v0.3.3.zip").Result;
                        File.WriteAllBytes(zipPath, bytes);
                        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, vtDir, true);
                        try { File.Delete(zipPath); } catch { }
                    }
                    catch { }
                }
                string wall = Path.Combine(WebAssets.Root, "Scripts", "Ares", "wallpaper", "ares_store_wallpaper.png");
                _log.Head("â•â•â• ARES ONE-CLICK : MAX OPTIMIZER (update=" + (doUpdate ? "ON" : "OFF") + ") â•â•â•");
                int okC = 0, failC = 0, skipC = 0;
                bool gotDone = false;
                string args = "-NoProfile -ExecutionPolicy RemoteSigned -File \"" + script + "\"";

                Sys.RunStream("powershell.exe", args, 45 * 60 * 1000, line =>
                {
                    if (string.IsNullOrEmpty(line) || !line.StartsWith("JX|")) return;
                    try
                    {
                        var ev = JsonSerializer.Deserialize<JsonElement>(line.Substring(3));
                        string t = ev.TryGetProperty("t", out var tt) && tt.ValueKind == JsonValueKind.String ? tt.GetString() : "";

                        if (t == "step")
                        {
                            int pct = ev.TryGetProperty("pct", out var pe) && pe.ValueKind == JsonValueKind.Number ? pe.GetInt32() : 0;
                            string m = ev.TryGetProperty("msg", out var me) && me.ValueKind == JsonValueKind.String ? me.GetString() : "";
                            int idx = ev.TryGetProperty("idx", out var ie) && ie.ValueKind == JsonValueKind.Number ? ie.GetInt32() : 0;
                            int tot = ev.TryGetProperty("total", out var te) && te.ValueKind == JsonValueKind.Number ? te.GetInt32() : 0;
                            bool skip = ev.TryGetProperty("skip", out var se) && se.ValueKind == JsonValueKind.True;
                            string lbl = (tot > 0 ? "à¸‚à¸±à¹‰à¸™à¸—à¸µà¹ˆ " + idx + "/" + tot + " Â· " : "") + m + (skip ? " â€” à¸‚à¹‰à¸²à¸¡ (à¸•à¸±à¹‰à¸‡à¹„à¸§à¹‰à¹à¸¥à¹‰à¸§)" : "");
                            JobStep(pct, lbl);
                            _log.Info("Ares: " + m + (skip ? " (à¸‚à¹‰à¸²à¸¡)" : ""));
                        }
                        else if (t == "dl")
                        {
                            // pct à¸‚à¸­à¸‡à¸”à¸²à¸§à¸™à¹Œà¹‚à¸«à¸¥à¸”à¹€à¸›à¹‡à¸™ % à¸‚à¸­à¸‡à¹„à¸Ÿà¸¥à¹Œà¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™ â€” à¹„à¸¡à¹ˆà¸”à¸±à¸™à¹à¸–à¸šà¸£à¸§à¸¡ à¸­à¸±à¸›à¹€à¸”à¸•à¹à¸„à¹ˆà¸‚à¹‰à¸­à¸„à¸§à¸²à¸¡
                            string head = ev.TryGetProperty("head", out var he) && he.ValueKind == JsonValueKind.String ? he.GetString() : "à¸à¸³à¸¥à¸±à¸‡à¸”à¸²à¸§à¸™à¹Œà¹‚à¸«à¸¥à¸” update";
                            double dmb = ev.TryGetProperty("doneMB", out var de) && de.ValueKind == JsonValueKind.Number ? de.GetDouble() : 0;
                            double tmb = ev.TryGetProperty("totalMB", out var te2) && te2.ValueKind == JsonValueKind.Number ? te2.GetDouble() : 0;
                            double spd = ev.TryGetProperty("speed", out var se2) && se2.ValueKind == JsonValueKind.Number ? se2.GetDouble() : 0;
                            JobStep(0, head + " " + dmb.ToString("0.0") + "/" + tmb.ToString("0.0") + " MB Â· " + spd.ToString("0.0") + " MB/s");
                        }
                        else if (t == "install")
                        {
                            JobStep(0, "à¸à¸³à¸¥à¸±à¸‡à¸•à¸´à¸”à¸•à¸±à¹‰à¸‡ Windows Update (DISM) â€” à¹ƒà¸Šà¹‰à¹€à¸§à¸¥à¸²à¸™à¸²à¸™ à¸«à¹‰à¸²à¸¡à¸›à¸´à¸”à¹à¸­à¸ž");
                        }
                        else if (t == "dldone")
                        {
                            JobStep(0, "à¸”à¸²à¸§à¸™à¹Œà¹‚à¸«à¸¥à¸”/à¸•à¸´à¸”à¸•à¸±à¹‰à¸‡ update à¹€à¸ªà¸£à¹‡à¸ˆ â€” à¸—à¸³à¸‚à¸±à¹‰à¸™à¸–à¸±à¸”à¹„à¸›à¸•à¹ˆà¸­");
                        }
                        else if (t == "done")
                        {
                            gotDone = true;
                            if (ev.TryGetProperty("ok", out var oe) && oe.ValueKind == JsonValueKind.Number) okC = oe.GetInt32();
                            if (ev.TryGetProperty("fail", out var fe) && fe.ValueKind == JsonValueKind.Number) failC = fe.GetInt32();
                            if (ev.TryGetProperty("skipped", out var ske) && ske.ValueKind == JsonValueKind.Number) skipC = ske.GetInt32();
                        }
                    }
                    catch { }
                }, ("ARES_VIVETOOL", vive), ("ARES_WALLPAPER", wall), ("ARES_DO_UPDATE", doUpdate ? "1" : "0"));

                string msg = gotDone
                    ? "Ares à¹€à¸ªà¸£à¹‡à¸ˆà¸ªà¸¡à¸šà¸¹à¸£à¸“à¹Œ â€” à¸ªà¸³à¹€à¸£à¹‡à¸ˆ " + okC + " Â· à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§ " + failC + " Â· à¸‚à¹‰à¸²à¸¡ " + skipC + " Â· à¸„à¸§à¸£à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡"
                    : "Ares à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸£à¸²à¸¢à¸‡à¸²à¸™à¸œà¸¥à¸ªà¸£à¸¸à¸› (à¸ªà¸„à¸£à¸´à¸›à¸•à¹Œà¸«à¸¢à¸¸à¸”à¸à¸¥à¸²à¸‡à¸—à¸²à¸‡/timeout) â€” à¹€à¸Šà¹‡à¸„à¸«à¸™à¹‰à¸² Logs";
                if (!gotDone) _log.Err("Ares: script ended without done event (crash/timeout)");
                if (failC > 0) _log.Err("Ares: à¸šà¸²à¸‡à¸‚à¸±à¹‰à¸™à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§ " + failC + " à¸‚à¸±à¹‰à¸™");
                _log.Ok(msg);
                Notify(gotDone ? (failC > 0 ? "yellow" : "green") : "red", "âš¡ Ares One-Click",
                    gotDone ? "à¸ªà¸³à¹€à¸£à¹‡à¸ˆ **" + okC + "** Â· à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§ **" + failC + "** Â· à¸‚à¹‰à¸²à¸¡ **" + skipC + "**" : "à¸ªà¸„à¸£à¸´à¸›à¸•à¹Œà¸«à¸¢à¸¸à¸”à¸à¸¥à¸²à¸‡à¸—à¸²à¸‡ / timeout â€” à¹€à¸Šà¹‡à¸„à¸«à¸™à¹‰à¸² Logs");
                JobEnd(gotDone, msg);
                return J(new { ok = gotDone, msg, okCount = okC, fail = failC, skipped = skipC });
            }
            catch
            {
                JobEnd(false, "Ares à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§");
                throw;
            }
            finally { try { creep.Wait(300); } catch { } }
        }

        // ---------- TOGGLES ----------
        public string Tweak(string id, bool on)
        {
            Hello();
            try
            {
                _cfg.toggles[id] = on;
                SaveCfg();

                switch (id)
                {
                    case "pw_park":
                        Sys.Cmd($"powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES {(on ? "100" : "5")}");
                        Sys.Cmd($"powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES {(on ? "100" : "5")}");
                        Sys.Cmd("powercfg /setactive SCHEME_CURRENT");
                        _log.Ok($"CPU Core Parking: {(on ? "à¸›à¸´à¸” (à¸—à¸¸à¸à¸„à¸­à¸£à¹Œà¸žà¸£à¹‰à¸­à¸¡à¸—à¸³à¸‡à¸²à¸™)" : "à¸„à¸·à¸™à¸„à¹ˆà¸² Auto")}");
                        return J(new { ok = true, msg = on ? "à¸›à¸´à¸” Core Parking à¹à¸¥à¹‰à¸§" : "à¸„à¸·à¸™à¸„à¹ˆà¸² Core Parking" });

                    case "pw_turbo":
                        Sys.Cmd($"powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {(on ? "2" : "1")}");
                        Sys.Cmd($"powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {(on ? "2" : "1")}");
                        Sys.Cmd("powercfg /setactive SCHEME_CURRENT");
                        _log.Ok($"Turbo Boost: {(on ? "Aggressive" : "à¸›à¸à¸•à¸´")}");
                        return J(new { ok = true, msg = on ? "à¸¥à¹‡à¸­à¸ Turbo Boost (Aggressive)" : "à¸„à¸·à¸™à¸„à¹ˆà¸² Turbo Boost" });

                    case "mem_auto":
                        SetAutoTrim(on);
                        _log.Ok($"Auto Standby Trim: {(on ? "à¸—à¸¸à¸ 1 à¸Šà¸±à¹ˆà¸§à¹‚à¸¡à¸‡" : "à¸›à¸´à¸”")}");
                        return J(new { ok = true, msg = on ? "Auto Trim à¹€à¸›à¸´à¸” (à¸—à¸¸à¸ 1 à¸Šà¸¡.)" : "à¸›à¸´à¸” Auto Trim" });

                    default:
                        {
                            var t = _all.FirstOrDefault(x => x.Id == id);
                            if (t == null) return J(new { ok = false, msg = "à¹„à¸¡à¹ˆà¸£à¸¹à¹‰à¸ˆà¸±à¸ tweak: " + id });
                            t.IsSelected = on;
                            if (on) t.ApplyAction?.Invoke(_log);
                            else t.UndoAction?.Invoke(_log);
                            if (id == "r9_power") { _cfg.powerPlan = on ? "r9" : "default"; SaveCfg(); }
                            return J(new { ok = true, msg = $"{t.Name}: {(on ? "à¹ƒà¸Šà¹‰à¸‡à¸²à¸™" : "à¸„à¸·à¸™à¸„à¹ˆà¸²")}à¹à¸¥à¹‰à¸§" });
                        }
                }
            }
            catch (Exception ex)
            {
                _log.Err(id + ": " + ex.Message);
                return J(new { ok = false, msg = ex.Message });
            }
        }

        private void SetAutoTrim(bool on)
        {
            if (!on)
            {
                if (_autoTrim != null) { try { _autoTrim.Stop(); _autoTrim.Dispose(); } catch { } _autoTrim = null; }
                return;
            }
            if (_autoTrim != null) return;
            var t = new System.Timers.Timer(3600000) { AutoReset = true };
            t.Elapsed += (s, e) =>
            {
                try
                {
                    double freed = Memory.PurgeStandby();
                    if (freed > 0.05) _log.Info($"Auto Trim: à¸„à¸·à¸™ RAM {freed:N2} GB");
                }
                catch { }
            };
            t.Start();
            _autoTrim = t;
        }

        // ---------- MEMORY / CLEANER ----------

        private sealed class PlanInfo { public string Guid = ""; public string Name = ""; public bool Active; }

        /// <summary>à¸”à¸¶à¸‡à¸£à¸²à¸¢à¸à¸²à¸£ power plan à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”à¸ˆà¸²à¸ WMI (à¸Šà¸·à¹ˆà¸­à¸ à¸²à¸©à¸²à¹„à¸—à¸¢/à¸­à¸±à¸‡à¸à¸¤à¸©à¹„à¸¡à¹ˆà¸¡à¸µà¸›à¸±à¸à¸«à¸² encoding)</summary>
        private static List<PlanInfo> PowerPlanList()
        {
            var list = new List<PlanInfo>();
            try
            {
                using var mos = new ManagementObjectSearcher("root\\cimv2\\power", "SELECT InstanceID, ElementName, IsActive FROM Win32_PowerPlan");
                foreach (var o in mos.Get())
                {
                    string iid = (o["InstanceID"] ?? "").ToString();
                    int a = iid.IndexOf('{'), b = iid.LastIndexOf('}');
                    string g = (a >= 0 && b > a) ? iid.Substring(a + 1, b - a - 1) : iid;
                    bool act = false;
                    try { act = Convert.ToBoolean(o["IsActive"]); } catch { }
                    list.Add(new PlanInfo { Guid = g, Name = ((o["ElementName"] ?? "").ToString()).Trim(), Active = act });
                }
            }
            catch { }
            return list;
        }

        /// <summary>à¸¥à¸šà¹à¸œà¸™à¸—à¸µà¹ˆà¸Šà¸·à¹ˆà¸­à¸‹à¹‰à¸³à¸à¸±à¸™ â€” à¹€à¸«à¸¥à¸·à¸­à¸­à¸±à¸™à¹€à¸”à¸µà¸¢à¸§à¸•à¹ˆà¸­à¸Šà¸·à¹ˆà¸­ (à¸à¸¥à¸¸à¹ˆà¸¡à¹„à¸«à¸™à¸¡à¸µà¹à¸œà¸™ active à¸ˆà¸°à¹€à¸à¹‡à¸šà¸•à¸±à¸§ active à¹„à¸§à¹‰) â†’ à¸„à¸·à¸™à¸ˆà¸³à¸™à¸§à¸™à¸—à¸µà¹ˆà¸¥à¸šà¹„à¸”à¹‰</summary>
        private int PowerPlanDedupeInternal(Logger log)
        {
            var plans = PowerPlanList();
            int removed = 0;
            foreach (var grp in plans.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            {
                var keep = grp.FirstOrDefault(p => p.Active) ?? grp.First();
                foreach (var p in grp)
                {
                    if (p.Guid.Equals(keep.Guid, StringComparison.OrdinalIgnoreCase)) continue;
                    string outp = Sys.Run("powercfg.exe", "/delete \"" + p.Guid + "\"");
                    if (outp.StartsWith("ERR")) { log?.Warn("à¸¥à¸š power plan à¹„à¸¡à¹ˆà¸ªà¸³à¹€à¸£à¹‡à¸ˆ: " + p.Name + " â†’ " + outp); continue; }
                    removed++;
                    log?.Ok("à¸¥à¸š power plan à¸‹à¹‰à¸³: " + p.Name + " (" + p.Guid.Substring(0, 8) + ") â€” à¹€à¸à¹‡à¸š \"" + keep.Name + "\" à¹„à¸§à¹‰");
                }
            }
            return removed;
        }

        /// <summary>à¸£à¸²à¸¢à¸à¸²à¸£ power plan à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸” + à¸ˆà¸³à¸™à¸§à¸™à¸—à¸µà¹ˆà¸‹à¹‰à¸³ (à¹‚à¸Šà¸§à¹Œà¹ƒà¸™à¸«à¸™à¹‰à¸² Powerplan)</summary>
        public string PowerPlans()
        {
            Hello();
            try
            {
                var plans = PowerPlanList();
                var dupGroups = plans.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
                return J(new
                {
                    ok = true,
                    total = plans.Count,
                    plans = plans.Select(p => new { name = p.Name, guid = p.Guid.Length > 8 ? p.Guid.Substring(0, 8) : p.Guid, active = p.Active }),
                    dupCount = dupGroups.Sum(g => g.Count() - 1),
                    dupNames = dupGroups.Select(g => g.Key + " Ã—" + g.Count())
                });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸¥à¸š power plan à¸—à¸µà¹ˆà¸‹à¹‰à¸³à¸à¸±à¸™ à¹€à¸«à¸¥à¸·à¸­à¸­à¸±à¸™à¹€à¸”à¸µà¸¢à¸§à¸•à¹ˆà¸­à¸Šà¸·à¹ˆà¸­</summary>
        public string PowerPlanDedupe()
        {
            Hello();
            try
            {
                int removed = PowerPlanDedupeInternal(_log);
                _log.Ok(removed > 0 ? $"à¸¥à¸š power plan à¸‹à¹‰à¸³ {removed} à¸•à¸±à¸§ â€” à¹€à¸«à¸¥à¸·à¸­à¸­à¸±à¸™à¹€à¸”à¸µà¸¢à¸§à¸•à¹ˆà¸­à¸Šà¸·à¹ˆà¸­à¹à¸¥à¹‰à¸§" : "à¹„à¸¡à¹ˆà¸¡à¸µ power plan à¸‹à¹‰à¸³");
                return J(new
                {
                    ok = true,
                    removedCount = removed,
                    msg = removed > 0 ? "à¸¥à¸š Power Plan à¸‹à¹‰à¸³ " + removed + " à¸•à¸±à¸§à¹à¸¥à¹‰à¸§ âœ“ (à¹€à¸«à¸¥à¸·à¸­à¸­à¸±à¸™à¹€à¸”à¸µà¸¢à¸§à¸•à¹ˆà¸­à¸Šà¸·à¹ˆà¸­)" : "à¹„à¸¡à¹ˆà¸žà¸š Power Plan à¸‹à¹‰à¸³"
                });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸œà¸¹à¹‰à¹ƒà¸Šà¹‰à¸à¸” "à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸¥à¸¢" à¸ˆà¸²à¸à¸«à¸™à¹‰à¸²à¸•à¹ˆà¸²à¸‡à¸˜à¸µà¸¡à¸‚à¸­à¸‡à¹à¸­à¸› (à¹à¸—à¸™ MessageBox à¹€à¸”à¸´à¸¡) â€” à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹ƒà¸™ 5 à¸§à¸´</summary>
        public string RestartNow()
        {
            Hello();
            _log.Ok("à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹ƒà¸™ 5 à¸§à¸´à¸™à¸²à¸—à¸µ (à¸¢à¸·à¸™à¸¢à¸±à¸™à¸ˆà¸²à¸à¸«à¸™à¹‰à¸²à¹€à¸§à¹‡à¸š)");
            Notify("blue", "ðŸ”„ à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡", "à¸œà¸¹à¹‰à¹ƒà¸Šà¹‰à¸à¸”à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¸ˆà¸²à¸à¹à¸­à¸› â€” à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸ˆà¸°à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹ƒà¸™ 5 à¸§à¸´à¸™à¸²à¸—à¸µ");
            Sys.Cmd("shutdown /r /t 5 /c \"KingR9 Tools - restarting\"");
            return J(new { ok = true, msg = "à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹ƒà¸™ 5 à¸§à¸´à¸™à¸²à¸—à¸µ..." });
        }

        // ---------- R9 GROUP PAGES (à¸„à¹ˆà¸²à¸•à¸£à¸‡à¸•à¸²à¸¡à¸ªà¸„à¸£à¸´à¸›à¸•à¹Œ 07/08/09) ----------
        /// <summary>à¸«à¸™à¹‰à¸² Powerplan â€” 4 checkbox</summary>
        public string PowerApply(bool plan, bool hib, bool boost, bool delay)
        {
            Hello();
            try
            {
                int n = 0;
                if (plan)
                {
                    TweakRegistry.RunFile(_log, "13_R9_Powerplan.ps1");
                    _cfg.powerPlan = "r9";
                    n++;
                    // à¸•à¸£à¸§à¸ˆ + à¸¥à¸š power plan à¸—à¸µà¹ˆà¸‹à¹‰à¸³à¸à¸±à¸™à¸—à¸±à¸™à¸—à¸µ (à¹€à¸«à¸¥à¸·à¸­à¸­à¸±à¸™à¹€à¸”à¸µà¸¢à¸§à¸•à¹ˆà¸­à¸Šà¸·à¹ˆà¸­ â€” à¸à¸±à¸™à¹à¸œà¸™ R9 à¸‹à¹‰à¸­à¸™à¸ˆà¸²à¸à¸à¸²à¸£à¸à¸”à¸«à¸¥à¸²à¸¢à¸£à¸­à¸š)
                    try
                    {
                        int rm = PowerPlanDedupeInternal(_log);
                        if (rm > 0) _log.Ok("à¸¥à¸š power plan à¸—à¸µà¹ˆà¸‹à¹‰à¸³à¸à¸±à¸™ " + rm + " à¸•à¸±à¸§ (à¹€à¸«à¸¥à¸·à¸­à¸­à¸±à¸™à¹€à¸”à¸µà¸¢à¸§à¸•à¹ˆà¸­à¸Šà¸·à¹ˆà¸­)");
                    }
                    catch { }
                }
                if (hib)
                {
                    Sys.Cmd("powercfg /hibernate off");
                    Sys.SetDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled", 0);
                    Sys.SetDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabledDefault", 0);
                    Sys.SetDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0);
                    Sys.SetDword(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1);
                    _log.Ok("à¸›à¸´à¸” Hibernate / Fast Boot / Power Throttling à¹à¸¥à¹‰à¸§");
                    n++;
                }
                if (boost)
                {
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2");
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFEPP 0");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFEPP 0");
                    Sys.Cmd("powercfg /setactive SCHEME_CURRENT");
                    _log.Ok("CPU Boost Policy: Aggressive + EPP = 0 à¹à¸¥à¹‰à¸§");
                    n++;
                }
                if (delay)
                {
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 eeee5929-f4ab-4d24-b637-a35a90957a54 0");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 eeee5929-f4ab-4d24-b637-a35a90957a54 0");
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0");
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                    Sys.Cmd("powercfg /setactive SCHEME_CURRENT");
                    _log.Ok("Minimize Delay: PCIe ASPM off Â· NVMe idle 0 Â· USB suspend off à¹à¸¥à¹‰à¸§");
                    n++;
                }
                SaveCfg();
                return J(new { ok = true, msg = $"à¹ƒà¸Šà¹‰ Power Plan Tweaks {n} à¸à¸¥à¸¸à¹ˆà¸¡à¹à¸¥à¹‰à¸§" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸«à¸™à¹‰à¸² FiveM Settings â€” 4 checkbox (à¸„à¹ˆà¸²à¸ˆà¸²à¸à¸ªà¸„à¸£à¸´à¸›à¸•à¹Œ 09)</summary>
        public string FivemApply(bool cache, bool prio, bool cef, bool pkg)
        {
            Hello();
            try
            {
                int n = 0;
                if (cache)
                {
                    string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveM", "FiveM.app");
                    string[] dirs = { "cache", Path.Combine("data", "cache"), Path.Combine("data", "server-cache"), Path.Combine("data", "nui-storage") };
                    foreach (var d in dirs)
                    {
                        try { var full = Path.Combine(app, d); if (Directory.Exists(full)) Directory.Delete(full, true); } catch { }
                    }
                    _log.Ok("à¸¥à¹‰à¸²à¸‡ FiveM cache à¸—à¸±à¹‰à¸‡ 4 à¹‚à¸Ÿà¸¥à¹€à¸”à¸­à¸£à¹Œà¹à¸¥à¹‰à¸§");
                    n++;
                }
                if (prio)
                {
                    foreach (var exe in new[] { "FiveM.exe", "FiveM_GTAProcess.exe", "GTA5.exe" })
                        Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exe + @"\PerfOptions", "CpuPriorityClass", 3);
                    _log.Ok("CpuPriorityClass = 3 (High) à¹ƒà¸«à¹‰ FiveM.exe / FiveM_GTAProcess.exe / GTA5.exe à¹à¸¥à¹‰à¸§");
                    n++;
                }
                if (cef)
                {
                    Sys.SetDword(@"HKCU\Software\CitizenFX\FiveM", "CEFHardwareAcceleration", 0);
                    _log.Ok("CEFHardwareAcceleration = 0 (à¸›à¸´à¸” HW accel à¸‚à¸­à¸‡ CEF UI) à¹à¸¥à¹‰à¸§");
                    n++;
                }
                if (pkg)
                {
                    Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\MSMQ\Parameters", "TCPNoDelay", 1);
                    Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)4294967295));
                    Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0);
                    _log.Ok("package transmission: MSMQ NoDelay + NetworkThrottling off + Responsiveness 0 à¹à¸¥à¹‰à¸§");
                    n++;
                }
                return J(new { ok = true, msg = $"à¹ƒà¸Šà¹‰ FiveM Tweaks {n} à¸£à¸²à¸¢à¸à¸²à¸£à¹à¸¥à¹‰à¸§" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸«à¸™à¹‰à¸² FiveM Settings â€” dropdown STR (Global Timer Resolution Requests)</summary>
        public string StrApply(bool low)
        {
            Hello();
            try
            {
                string k = @"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel";
                if (low)
                {
                    Sys.SetDword(k, "GlobalTimerResolutionRequests", 1);
                    Sys.SetDword(k, "TimerResolution", 5000);
                    _log.Ok("Global Timer Resolution Requests: 0.5ms (TimerResolution=5000) à¹à¸¥à¹‰à¸§");
                    return J(new { ok = true, msg = "Timer Resolution: 0.5ms" });
                }
                Sys.DelValue(k, "GlobalTimerResolutionRequests");
                Sys.DelValue(k, "TimerResolution");
                _log.Ok("Global Timer Resolution Requests à¸à¸¥à¸±à¸š Default à¹à¸¥à¹‰à¸§");
                return J(new { ok = true, msg = "Timer Resolution: Default" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸«à¸™à¹‰à¸² FiveM Settings â€” Windows Services &amp; OS Tweaks 5 checkbox</summary>
        public string SvcApply(bool netstack, bool services, bool usb, bool latency, bool bcd)
        {
            Hello();
            try
            {
                int n = 0;
                Tweak Find(string id) => _all.FirstOrDefault(x => x.Id == id);
                if (netstack) { Find("r9_netsh")?.ApplyAction(_log); n++; }
                if (services) { Find("r9_services")?.ApplyAction(_log); n++; }
                if (usb)
                {
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0");
                    Sys.Cmd("powercfg /setactive SCHEME_CURRENT");
                    _log.Ok("à¸›à¸´à¸” USB Selective Suspend Power Saver à¹à¸¥à¹‰à¸§");
                    n++;
                }
                if (latency) { Find("lat_nagle")?.ApplyAction(_log); n++; }
                if (bcd)
                {
                    Sys.Cmd("bcdedit /deletevalue useplatformclock");
                    Sys.Cmd("bcdedit /deletevalue useplatformtick");
                    Sys.Cmd("bcdedit /set disabledynamictick no");
                    _log.Ok("BCD: à¹€à¸„à¸¥à¸µà¸¢à¸£à¹Œ useplatformclock/tick + dynamic tick ON à¹à¸¥à¹‰à¸§ (à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¸¡à¸µà¸œà¸¥)");
                    n++;
                }
                return J(new { ok = true, msg = $"à¹ƒà¸Šà¹‰ Services & OS Tweaks {n} à¸£à¸²à¸¢à¸à¸²à¸£à¹à¸¥à¹‰à¸§" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        // ---------- TOOLS / INFO ----------
        public string Logs()
        {
            var items = Logger.Raw().Select(e => new { t = e[0], lvl = e[1], msg = e[2] });
            return J(new { ok = true, items });
        }

        public string SysTool(string action)
        {
            Hello();
            TweakRegistry.SystemTool(_log, action);
            return J(new { ok = true, msg = "system tools: " + action });
        }

        public string Motherboard()
        {
            Hello();
            string prod = "-", manu = "-", cpu = "-", gpu = "-", ram = "-";
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT Product, Manufacturer FROM Win32_BaseBoard");
                foreach (ManagementObject o in mos.Get())
                using (o)
                {
                    prod = (o["Product"] ?? "-").ToString();
                    manu = (o["Manufacturer"] ?? "-").ToString();
                    break;
                }
            }
            catch { }
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (ManagementObject o in mos.Get())
                using (o) { cpu = (o["Name"] ?? "-").ToString().Trim(); break; }
            }
            catch { }
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                foreach (ManagementObject o in mos.Get())
                using (o) { gpu = (o["Name"] ?? "-").ToString(); break; }
            }
            catch { }
            try
            {
                using var mos = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject o in mos.Get())
                using (o)
                { ram = (Convert.ToDouble(o["TotalPhysicalMemory"]) / 1073741824.0).ToString("0.#") + " GB"; break; }
            }
            catch { }
            _log.Info("Motherboard: " + prod + " Â· " + manu);
            return J(new { ok = true, product = prod, manufacturer = manu, cpu = cpu, gpu = gpu, ram = ram });
        }

        public string OpenUrl(string url)
        {
            Hello();
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return J(new { ok = false, msg = "no url" });
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return J(new { ok = false, msg = "scheme blocked" });
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return J(new { ok = true });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸Šà¸·à¹ˆà¸­à¸‚à¸±à¹‰à¸™à¸•à¸­à¸™à¹à¸šà¸šà¸­à¹ˆà¸²à¸™à¸‡à¹ˆà¸²à¸¢à¸ªà¸³à¸«à¸£à¸±à¸š tweak à¹à¸•à¹ˆà¸¥à¸°à¸•à¸±à¸§ (à¹‚à¸Šà¸§à¹Œà¹ƒà¸™à¸«à¸™à¹‰à¸²à¹‚à¸«à¸¥à¸”)</summary>
        private static string TweakLabel(string id)
        {
            switch (id)
            {
                case "r9_nic":   return "à¸›à¸£à¸±à¸š NIC Adapter";
                case "r9_netsh": return "à¸›à¸£à¸±à¸š Netsh (cubic Â· MTU)";
                case "r9_qos":   return "à¸ªà¸£à¹‰à¸²à¸‡ QoS DSCP 46";
                case "r9_gta5":  return "à¹€à¸‚à¸µà¸¢à¸™à¹„à¸Ÿà¸¥à¹Œ config à¹€à¸à¸¡ (GTA5/FiveM)";
                case "mem_auto": return "à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² Auto Trim";
                case "pw_park":  return "à¸›à¸£à¸±à¸š CPU Core Parking";
                case "pw_turbo": return "à¸›à¸£à¸±à¸š Turbo Boost";
                default:         return "à¸à¸³à¸¥à¸±à¸‡à¸›à¸£à¸±à¸š tweak";
            }
        }

        /// <summary>à¸Šà¸·à¹ˆà¸­à¸‚à¸±à¹‰à¸™à¸•à¸­à¸™à¹à¸šà¸šà¸­à¹ˆà¸²à¸™à¸‡à¹ˆà¸²à¸¢à¸ªà¸³à¸«à¸£à¸±à¸šà¸«à¸™à¹‰à¸²à¹‚à¸«à¸¥à¸” (à¹„à¸¡à¹ˆà¹‚à¸Šà¸§à¹Œà¸Šà¸·à¹ˆà¸­à¹„à¸Ÿà¸¥à¹Œ .bat)</summary>
        private static string StepLabel(string file)
        {
            switch (Path.GetFileName(file).ToLowerInvariant())
            {
                case "01_registry_tweaks.bat":  return "à¸›à¸£à¸±à¸š Registry (TCP/IP Â· AFD Â· NetBT)";
                case "02_netsh_tweaks.bat":     return "à¸›à¸£à¸±à¸š Netsh (cubic Â· MTU)";
                case "03_bcdedit_tweaks.bat":   return "à¸›à¸£à¸±à¸š BCD (dynamic tick)";
                case "04_nic_tweaks.bat":       return "à¸›à¸£à¸±à¸š NIC Adapter";
                case "05_bindings_dns.bat":     return "à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² Bindings + DNS";
                case "06_reset_adapter.bat":    return "à¸£à¸µà¹€à¸‹à¹‡à¸• Adapter (à¹€à¸™à¹‡à¸•à¸«à¸¥à¸¸à¸”à¹à¸›à¹Šà¸š ~15 à¸§à¸´)";
                case "99_restore_defaults.bat": return "à¸„à¸·à¸™à¸„à¹ˆà¸²à¹€à¸£à¸´à¹ˆà¸¡à¸•à¹‰à¸™à¸‚à¸­à¸‡à¸Šà¸¸à¸” R9";
                default:                        return "à¸à¸³à¸¥à¸±à¸‡à¸—à¸³à¸‡à¸²à¸™";
            }
        }

        /// <summary>R9 internet â€” à¸Šà¸¸à¸” .bat à¸ˆà¸²à¸ InternetR9 (registry/netsh/bcd/nic/bindings/restore)</summary>
        public string R9Net(string which)
        {
            Hello();
            try
            {
                const string dir = "R9Internet\\";
                string[] files = which switch
                {
                    "registry" => new[] { "01_registry_tweaks.bat" },
                    "netsh"    => new[] { "02_netsh_tweaks.bat" },
                    "bcd"      => new[] { "03_bcdedit_tweaks.bat" },
                    "nic"      => new[] { "04_nic_tweaks.bat" },
                    "bindings" => new[] { "05_bindings_dns.bat" },
                    "restore"  => new[] { "99_restore_defaults.bat" },
                    "reset"    => new[] { "06_reset_adapter.bat" },
                    "all"      => new[]
                    {
                        "01_registry_tweaks.bat", "02_netsh_tweaks.bat", "03_bcdedit_tweaks.bat",
                        "04_nic_tweaks.bat", "05_bindings_dns.bat", "06_reset_adapter.bat"
                    },
                    _ => Array.Empty<string>()
                };
                if (files.Length == 0) return J(new { ok = false, msg = "à¹„à¸¡à¹ˆà¸£à¸¹à¹‰à¸ˆà¸±à¸à¸Šà¸¸à¸”: " + which });

                _log.Head("R9 internet â€” " + which);
                for (int i = 0; i < files.Length; i++)
                {
                    string lbl = $"à¸‚à¸±à¹‰à¸™à¸—à¸µà¹ˆ {i + 1}/{files.Length} Â· " + StepLabel(files[i]);
                    JobBeginStep(i, files.Length, lbl);
                    TweakRegistry.RunBat(_log, dir + files[i]);
                    JobStep((i + 1) * 100 / files.Length, lbl);
                }
                _log.Ok("R9 internet (" + which + ") à¹€à¸ªà¸£à¹‡à¸ˆà¸ªà¸¡à¸šà¸¹à¸£à¸“à¹Œ");
                return J(new { ok = true, msg = "R9 internet: " + which + " âœ“" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

    }
}