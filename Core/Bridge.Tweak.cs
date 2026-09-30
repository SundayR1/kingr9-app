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
                return J(new { ok = false, msg = "ไม่พบ Ares optimizer.ps1 — ลองปิดแล้วเปิดโปรแกรมใหม่" });
            if (!JobBegin("Ares One-Click Optimizer"))
                return J(new { ok = false, msg = "มีงานกำลังทำงานอยู่ — รอจบก่อนแล้วกดใหม่" });
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
                            string lbl = (tot > 0 ? "ขั้นที่ " + idx + "/" + tot + " · " : "") + m + (skip ? " — ข้าม (ตั้งไว้แล้ว)" : "");
                            JobStep(pct, lbl);
                            _log.Info("Ares: " + m + (skip ? " (ข้าม)" : ""));
                        }
                        else if (t == "dl")
                        {
                            // pct ของดาวน์โหลดเป็น % ของไฟล์เท่านั้น — ไม่ดันแถบรวม อัปเดตแค่ข้อความ
                            string head = ev.TryGetProperty("head", out var he) && he.ValueKind == JsonValueKind.String ? he.GetString() : "กำลังดาวน์โหลด update";
                            double dmb = ev.TryGetProperty("doneMB", out var de) && de.ValueKind == JsonValueKind.Number ? de.GetDouble() : 0;
                            double tmb = ev.TryGetProperty("totalMB", out var te2) && te2.ValueKind == JsonValueKind.Number ? te2.GetDouble() : 0;
                            double spd = ev.TryGetProperty("speed", out var se2) && se2.ValueKind == JsonValueKind.Number ? se2.GetDouble() : 0;
                            JobStep(0, head + " " + dmb.ToString("0.0") + "/" + tmb.ToString("0.0") + " MB · " + spd.ToString("0.0") + " MB/s");
                        }
                        else if (t == "install")
                        {
                            JobStep(0, "กำลังติดตั้ง Windows Update (DISM) — ใช้เวลานาน ห้ามปิดแอพ");
                        }
                        else if (t == "dldone")
                        {
                            JobStep(0, "ดาวน์โหลด/ติดตั้ง update เสร็จ — ทำขั้นถัดไปต่อ");
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
                    ? "Ares เสร็จสมบูรณ์ — สำเร็จ " + okC + " · ล้มเหลว " + failC + " · ข้าม " + skipC + " · ควรรีสตาร์ทเครื่อง"
                    : "Ares ไม่ได้รายงานผลสรุป (สคริปต์หยุดกลางทาง/timeout) — เช็คหน้า Logs";
                if (!gotDone) _log.Err("Ares: script ended without done event (crash/timeout)");
                if (failC > 0) _log.Err("Ares: บางขั้นล้มเหลว " + failC + " ขั้น");
                _log.Ok(msg);
                Notify(gotDone ? (failC > 0 ? "yellow" : "green") : "red", "âš¡ Ares One-Click",
                    gotDone ? "สำเร็จ **" + okC + "** · ล้มเหลว **" + failC + "** · ข้าม **" + skipC + "**" : "สคริปต์หยุดกลางทาง / timeout — เช็คหน้า Logs");
                JobEnd(gotDone, msg);
                return J(new { ok = gotDone, msg, okCount = okC, fail = failC, skipped = skipC });
            }
            catch
            {
                JobEnd(false, "Ares ล้มเหลว");
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
                        _log.Ok($"CPU Core Parking: {(on ? "ปิด (ทุกคอร์พร้อมทำงาน)" : "คืนค่า Auto")}");
                        return J(new { ok = true, msg = on ? "ปิด Core Parking แล้ว" : "คืนค่า Core Parking" });

                    case "pw_turbo":
                        Sys.Cmd($"powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {(on ? "2" : "1")}");
                        Sys.Cmd($"powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE {(on ? "2" : "1")}");
                        Sys.Cmd("powercfg /setactive SCHEME_CURRENT");
                        _log.Ok($"Turbo Boost: {(on ? "Aggressive" : "ปกติ")}");
                        return J(new { ok = true, msg = on ? "ล็อก Turbo Boost (Aggressive)" : "คืนค่า Turbo Boost" });

                    case "mem_auto":
                        return J(new { ok = false, msg = "Standby memory trimming is no longer available." });

                    default:
                        {
                            var t = _all.FirstOrDefault(x => x.Id == id);
                            if (t == null) return J(new { ok = false, msg = "ไม่รู้จัก tweak: " + id });
                            t.IsSelected = on;
                            if (on) t.ApplyAction?.Invoke(_log);
                            else t.UndoAction?.Invoke(_log);
                            if (id == "r9_power") { _cfg.powerPlan = on ? "r9" : "default"; SaveCfg(); }
                            return J(new { ok = true, msg = $"{t.Name}: {(on ? "ใช้งาน" : "คืนค่า")}แล้ว" });
                        }
                }
            }
            catch (Exception ex)
            {
                _log.Err(id + ": " + ex.Message);
                return J(new { ok = false, msg = ex.Message });
            }
        }

        // ---------- MEMORY / CLEANER ----------

        private sealed class PlanInfo { public string Guid = ""; public string Name = ""; public bool Active; }

        /// <summary>ดึงรายการ power plan ทั้งหมดจาก WMI (ชื่อภาษาไทย/อังกฤษไม่มีปัญหา encoding)</summary>
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

        /// <summary>ลบแผนที่ชื่อซ้ำกัน — เหลืออันเดียวต่อชื่อ (กลุ่มไหนมีแผน active จะเก็บตัว active ไว้) → คืนจำนวนที่ลบได้</summary>
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
                    if (outp.StartsWith("ERR")) { log?.Warn("ลบ power plan ไม่สำเร็จ: " + p.Name + " → " + outp); continue; }
                    removed++;
                    log?.Ok("ลบ power plan ซ้ำ: " + p.Name + " (" + p.Guid.Substring(0, 8) + ") — เก็บ \"" + keep.Name + "\" ไว้");
                }
            }
            return removed;
        }

        /// <summary>รายการ power plan ทั้งหมด + จำนวนที่ซ้ำ (โชว์ในหน้า Powerplan)</summary>
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
                    dupNames = dupGroups.Select(g => g.Key + " ×" + g.Count())
                });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>ลบ power plan ที่ซ้ำกัน เหลืออันเดียวต่อชื่อ</summary>
        public string PowerPlanDedupe()
        {
            Hello();
            try
            {
                int removed = PowerPlanDedupeInternal(_log);
                _log.Ok(removed > 0 ? $"ลบ power plan ซ้ำ {removed} ตัว — เหลืออันเดียวต่อชื่อแล้ว" : "ไม่มี power plan ซ้ำ");
                return J(new
                {
                    ok = true,
                    removedCount = removed,
                    msg = removed > 0 ? "ลบ Power Plan ซ้ำ " + removed + " ตัวแล้ว ✓ (เหลืออันเดียวต่อชื่อ)" : "ไม่พบ Power Plan ซ้ำ"
                });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>ผู้ใช้กด "รีสตาร์ทเลย" จากหน้าต่างธีมของแอป (แทน MessageBox เดิม) — รีสตาร์ทใน 5 วิ</summary>
        public string RestartNow()
        {
            Hello();
            _log.Ok("รีสตาร์ทเครื่องใน 5 วินาที (ยืนยันจากหน้าเว็บ)");
            Notify("blue", "🔄 รีสตาร์ทเครื่อง", "ผู้ใช้กดรีสตาร์ทจากแอป — เครื่องจะรีสตาร์ทใน 5 วินาที");
            Sys.Cmd("shutdown /r /t 5 /c \"KingR9 Tools - restarting\"");
            return J(new { ok = true, msg = "รีสตาร์ทใน 5 วินาที..." });
        }

        // ---------- R9 GROUP PAGES (ค่าตรงตามสคริปต์ 07/08/09) ----------
        /// <summary>หน้า Powerplan — 4 checkbox</summary>
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
                    // ตรวจ + ลบ power plan ที่ซ้ำกันทันที (เหลืออันเดียวต่อชื่อ — กันแผน R9 ซ้อนจากการกดหลายรอบ)
                    try
                    {
                        int rm = PowerPlanDedupeInternal(_log);
                        if (rm > 0) _log.Ok("ลบ power plan ที่ซ้ำกัน " + rm + " ตัว (เหลืออันเดียวต่อชื่อ)");
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
                    _log.Ok("ปิด Hibernate / Fast Boot / Power Throttling แล้ว");
                    n++;
                }
                if (boost)
                {
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 2");
                    Sys.Cmd("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFEPP 0");
                    Sys.Cmd("powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFEPP 0");
                    Sys.Cmd("powercfg /setactive SCHEME_CURRENT");
                    _log.Ok("CPU Boost Policy: Aggressive + EPP = 0 แล้ว");
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
                    _log.Ok("Minimize Delay: PCIe ASPM off · NVMe idle 0 · USB suspend off แล้ว");
                    n++;
                }
                SaveCfg();
                return J(new { ok = true, msg = $"ใช้ Power Plan Tweaks {n} กลุ่มแล้ว" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>หน้า FiveM Settings — 4 checkbox (ค่าจากสคริปต์ 09)</summary>
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
                    _log.Ok("ล้าง FiveM cache ทั้ง 4 โฟลเดอร์แล้ว");
                    n++;
                }
                if (prio)
                {
                    foreach (var exe in new[] { "FiveM.exe", "FiveM_GTAProcess.exe", "GTA5.exe" })
                        Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exe + @"\PerfOptions", "CpuPriorityClass", 3);
                    _log.Ok("CpuPriorityClass = 3 (High) ให้ FiveM.exe / FiveM_GTAProcess.exe / GTA5.exe แล้ว");
                    n++;
                }
                if (cef)
                {
                    Sys.SetDword(@"HKCU\Software\CitizenFX\FiveM", "CEFHardwareAcceleration", 0);
                    _log.Ok("CEFHardwareAcceleration = 0 (ปิด HW accel ของ CEF UI) แล้ว");
                    n++;
                }
                if (pkg)
                {
                    Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\MSMQ\Parameters", "TCPNoDelay", 1);
                    Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)4294967295));
                    Sys.SetDword(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0);
                    _log.Ok("package transmission: MSMQ NoDelay + NetworkThrottling off + Responsiveness 0 แล้ว");
                    n++;
                }
                return J(new { ok = true, msg = $"ใช้ FiveM Tweaks {n} รายการแล้ว" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>หน้า FiveM Settings — dropdown STR (Global Timer Resolution Requests)</summary>
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
                    _log.Ok("Global Timer Resolution Requests: 0.5ms (TimerResolution=5000) แล้ว");
                    return J(new { ok = true, msg = "Timer Resolution: 0.5ms" });
                }
                Sys.DelValue(k, "GlobalTimerResolutionRequests");
                Sys.DelValue(k, "TimerResolution");
                _log.Ok("Global Timer Resolution Requests กลับ Default แล้ว");
                return J(new { ok = true, msg = "Timer Resolution: Default" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>หน้า FiveM Settings — Windows Services &amp; OS Tweaks 5 checkbox</summary>
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
                    _log.Ok("ปิด USB Selective Suspend Power Saver แล้ว");
                    n++;
                }
                if (latency) { Find("lat_nagle")?.ApplyAction(_log); n++; }
                if (bcd)
                {
                    Sys.Cmd("bcdedit /deletevalue useplatformclock");
                    Sys.Cmd("bcdedit /deletevalue useplatformtick");
                    Sys.Cmd("bcdedit /set disabledynamictick no");
                    _log.Ok("BCD: เคลียร์ useplatformclock/tick + dynamic tick ON แล้ว (รีสตาร์ทมีผล)");
                    n++;
                }
                return J(new { ok = true, msg = $"ใช้ Services & OS Tweaks {n} รายการแล้ว" });
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
            _log.Info("Motherboard: " + prod + " · " + manu);
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

        /// <summary>ชื่อขั้นตอนแบบอ่านง่ายสำหรับ tweak แต่ละตัว (โชว์ในหน้าโหลด)</summary>
        private static string TweakLabel(string id)
        {
            switch (id)
            {
                case "r9_nic":   return "ปรับ NIC Adapter";
                case "r9_netsh": return "ปรับ Netsh (cubic · MTU)";
                case "r9_qos":   return "สร้าง QoS DSCP 46";
                case "r9_gta5":  return "เขียนไฟล์ config เกม (GTA5/FiveM)";
                case "pw_park":  return "ปรับ CPU Core Parking";
                case "pw_turbo": return "ปรับ Turbo Boost";
                default:         return "กำลังปรับ tweak";
            }
        }

        /// <summary>ชื่อขั้นตอนแบบอ่านง่ายสำหรับหน้าโหลด (ไม่โชว์ชื่อไฟล์ .bat)</summary>
        private static string StepLabel(string file)
        {
            switch (Path.GetFileName(file).ToLowerInvariant())
            {
                case "01_registry_tweaks.bat":  return "ปรับ Registry (TCP/IP · AFD · NetBT)";
                case "02_netsh_tweaks.bat":     return "ปรับ Netsh (cubic · MTU)";
                case "03_bcdedit_tweaks.bat":   return "ปรับ BCD (dynamic tick)";
                case "04_nic_tweaks.bat":       return "ปรับ NIC Adapter";
                case "05_bindings_dns.bat":     return "ตั้งค่า Bindings + DNS";
                case "06_reset_adapter.bat":    return "รีเซ็ต Adapter (เน็ตหลุดแป๊บ ~15 วิ)";
                case "99_restore_defaults.bat": return "คืนค่าเริ่มต้นของชุด R9";
                default:                        return "กำลังทำงาน";
            }
        }

        /// <summary>R9 internet — ชุด .bat จาก InternetR9 (registry/netsh/bcd/nic/bindings/restore)</summary>
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
                if (files.Length == 0) return J(new { ok = false, msg = "ไม่รู้จักชุด: " + which });

                _log.Head("R9 internet — " + which);
                for (int i = 0; i < files.Length; i++)
                {
                    string lbl = $"ขั้นที่ {i + 1}/{files.Length} · " + StepLabel(files[i]);
                    JobBeginStep(i, files.Length, lbl);
                    TweakRegistry.RunBat(_log, dir + files[i]);
                    JobStep((i + 1) * 100 / files.Length, lbl);
                }
                _log.Ok("R9 internet (" + which + ") เสร็จสมบูรณ์");
                return J(new { ok = true, msg = "R9 internet: " + which + " âœ“" });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

    }
}
