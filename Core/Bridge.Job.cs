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

        // ---------- JOB PROGRESS (ปุ่ม Apply ทั่วไป — กันกดซ้อน + รายงาน %) ----------
        private bool JobBegin(string label)
        {
            lock (_jobLock)
            {
                if (_jobRunning) return false;   // มีงานอื่นรันอยู่
                _jobRunning = true; _jobDone = false; _jobOk = false;
                _jobPct = 0; _jobBase = 0; _jobSpan = 100;
                _jobStepStart = DateTime.UtcNow;
                _jobLabel = label; _jobDoneMsg = "";
                return true;
            }
        }
        private void JobStep(int pct, string label = null)
        {
            lock (_jobLock)
            {
                _jobPct = Math.Max(_jobPct, Math.Min(100, pct));
                if (!string.IsNullOrEmpty(label)) _jobLabel = label;
            }
        }

        /// <summary>เริ่มขั้นที่ index/total — แถบจะไหลอยู่ในช่วงของขั้นนี้เท่านั้น (ขั้นถัดไปไหลช่วงถัดไป — ไม่มีเพดานรวมให้ค้าง)</summary>
        private void JobBeginStep(int index, int total, string label)
        {
            lock (_jobLock)
            {
                _jobBase = index * 100.0 / total;
                _jobSpan = 100.0 / total;
                _jobStepStart = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(label)) _jobLabel = label;
            }
        }

        /// <summary>ลูปไหล % — ไต่ถึง ~88% ของช่วงขั้นใน ~10 วิ แล้วคลานช้า ๆ ต่อเนื่อง (ไม่เกินช่วงของขั้นตัวเอง ไม่นิ่งสนิท)</summary>
        private void CreepLoop()
        {
            while (true)
            {
                double baseP, spanP; DateTime start;
                lock (_jobLock)
                {
                    if (!_jobRunning) return;
                    baseP = _jobBase; spanP = _jobSpan; start = _jobStepStart;
                }
                System.Threading.Thread.Sleep(150);
                double secs = (DateTime.UtcNow - start).TotalSeconds;
                double frac = 0.88 * (1 - Math.Exp(-secs / 8.0)) + Math.Min(0.10, secs / 600.0);
                int cap = (int)(baseP + spanP * 0.985);
                int pct = Math.Min(cap, (int)(baseP + spanP * frac));
                lock (_jobLock)
                {
                    if (!_jobRunning) return;
                    _jobPct = Math.Max(_jobPct, Math.Min(cap, pct));
                }
            }
        }
        private void JobEnd(bool ok, string msg)
        {
            lock (_jobLock)
            {
                _jobRunning = false; _jobDone = true; _jobOk = ok;
                _jobDoneMsg = msg ?? "";   // คง _jobPct ไว้ — หน้าโหลดฝั่ง JS เป็นคนเด้ง 100% เอง
            }
        }

        /// <summary>สถานะงานปัจจุบัน — JS poll ทุก 0.4 วิ ตอนโชว์หน้าโหลด</summary>
        public string JobProgress()
        {
            lock (_jobLock)
            {
                // ช่วงเปลี่ยนงาน (งานเก่าจบ งานใหม่ยังไม่เริ่ม) ส่ง pct = null → JS คงแถบไว้ ไม่กระโดดกลับ
                int? pct = (_jobRunning || _jobDone) ? _jobPct : (int?)null;
                var r = new { running = _jobRunning, done = _jobDone, pct = pct, label = _jobLabel, ok = _jobOk, msg = _jobDoneMsg };
                if (_jobDone) _jobDone = false;   // อ่านครั้งเดียวจบ
                return J(r);
            }
        }

        /// <summary>ห่องาน apply ทั่วไป — เริ่ม job (กันกดซ้อน) + % ไหลตามเวลาจริง จนงานจบเด้ง 100
        /// (งานที่มี % จริงต่อขั้น เช่น R9Net เป็นผู้เรียก JobStep เอง — JobStep ใช้ค่า Max จึงไล่ทับ % ประมาณได้)</summary>
        private string RunApplyJob(string label, Func<string> work)
        {
            if (!JobBegin(label)) return J(new { ok = false, msg = "มีงานกำลังทำงานอยู่ — รอจบก่อนแล้วกดใหม่" });
            var creep = Task.Run(CreepLoop);
            try
            {
                string result = work();
                bool ok = false; string msg = "";
                try
                {
                    var el = JsonSerializer.Deserialize<JsonElement>(result ?? "{}");
                    if (el.TryGetProperty("ok", out var o)) ok = o.ValueKind == JsonValueKind.True;
                    if (el.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String) msg = m.GetString() ?? "";
                }
                catch { }
                JobEnd(ok, msg);
                return result;
            }
            catch
            {
                JobEnd(false, "ล้มเหลว");
                throw;
            }
            finally { try { creep.Wait(300); } catch { } }
        }

        // ---------- CHAIN JOB (งานลูกโซ่หลายขั้นรวมเป็น job เดียว — แถบ % ไหลต่อเนื่องตลอด) ----------
        private static string PStr(JsonElement p, string n) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.String ? (x.GetString() ?? "") : "";

        private static bool IsApplyType(string t) =>
            t == "r9net" || t == "sysTool" || t == "powerApply" || t == "fivemApply" ||
            t == "strApply" || t == "svcApply" || t == "setTweak" || t == "cleanJunk" ||
            t == "powerPlanDedupe";

        /// <summary>ชื่อขั้นตอนของแต่ละ apply type (โชว์ในหน้าโหลด)</summary>
        private static string ApplyLabel(string type, JsonElement p)
        {
            switch (type)
            {
                case "setTweak":
                    return TweakLabel(PStr(p, "id"));
                case "r9net":
                    return PStr(p, "which") == "reset" ? "รีเซ็ต Adapter (เน็ตหลุดแป๊บ ~15 วิ)" : "R9 internet";
                case "sysTool":    return "System Tool";
                case "powerApply": return "Power & BCD";
                case "fivemApply": return "FiveM Tweaks";
                case "strApply":   return "Timer Resolution";
                case "svcApply":   return "Services & OS Tweaks";
                case "cleanJunk":  return "Junk Cleaner";
                case "powerPlanDedupe": return "ลบ Power Plan ซ้ำ";
                default:           return "กำลังทำงาน";
            }
        }

        /// <summary>รันงานลูกโซ่หลายขั้นใน job เดียว — % รวมทั้งชุดไหลต่อเนื่อง ขั้นไหน fail หยุดทันที</summary>
        private string RunChainJob(JsonElement stepsEl)
        {
            // JS ส่ง payload มาทั้งก้อนเป็น { steps: [...] } — รองรับทั้งแบบ wrapper และ array ตรง ๆ
            if (stepsEl.ValueKind == JsonValueKind.Object && stepsEl.TryGetProperty("steps", out var wrap) && wrap.ValueKind == JsonValueKind.Array)
                stepsEl = wrap;
            var steps = new List<(string label, string type, JsonElement payload)>();
            if (stepsEl.ValueKind != JsonValueKind.Array) return J(new { ok = false, msg = "รูปแบบ chain ไม่ถูกต้อง" });
            try
            {
                foreach (var s in stepsEl.EnumerateArray())
                {
                    string t = s.TryGetProperty("type", out var tt) && tt.ValueKind == JsonValueKind.String ? tt.GetString() ?? "" : "";
                    JsonElement pl = s.TryGetProperty("payload", out var pp) && pp.ValueKind == JsonValueKind.Object ? pp : default;
                    steps.Add((s.TryGetProperty("label", out var ll) && ll.ValueKind == JsonValueKind.String ? ll.GetString() ?? "" : "", t, pl));
                }
            }
            catch { return J(new { ok = false, msg = "รูปแบบ chain ไม่ถูกต้อง" }); }
            if (steps.Count == 0) return J(new { ok = false, msg = "ไม่มีขั้นตอนให้ทำ" });

            if (!JobBegin(ApplyLabel(steps[0].type, steps[0].payload)))
                return J(new { ok = false, msg = "มีงานกำลังทำงานอยู่ — รอจบก่อนแล้วกดใหม่" });

            var creep = Task.Run(CreepLoop);
            try
            {
                string lastMsg = "";
                for (int i = 0; i < steps.Count; i++)
                {
                    string lbl = $"ขั้นที่ {i + 1}/{steps.Count} · " + (steps[i].label.Length > 0 ? steps[i].label : ApplyLabel(steps[i].type, steps[i].payload));
                    JobBeginStep(i, steps.Count, lbl);
                    string r = RunRpc(steps[i].type, steps[i].payload);

                    bool ok = false; string msg = "";
                    try
                    {
                        var el = JsonSerializer.Deserialize<JsonElement>(r ?? "{}");
                        if (el.TryGetProperty("ok", out var o)) ok = o.ValueKind == JsonValueKind.True;
                        if (el.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String) msg = m.GetString() ?? "";
                    }
                    catch { }

                    JobStep((i + 1) * 100 / steps.Count, lbl);
                    if (!ok) { JobEnd(false, msg); return J(new { ok = false, msg = msg, failedStep = i + 1 }); }
                    lastMsg = msg;
                }
                JobEnd(true, lastMsg);
                return J(new { ok = true, msg = lastMsg });
            }
            catch
            {
                JobEnd(false, "ล้มเหลว");
                throw;
            }
            finally { try { creep.Wait(300); } catch { } }
        }

        // ---------- DISCORD WEBHOOK (แจ้งเตือน log เหตุการณ์สำคัญ — admin) ----------

        public string OptimizeStart()
        {
            lock (_runLock) { if (_busy) return J(new { ok = false, msg = "busy" }); _busy = true; }
            var sel = _all.Where(t => t.Recommended).ToList();
            Task.Run(() => RunJob(sel, true));
            return J(new { ok = true });
        }

        public string RestoreStart()
        {
            lock (_runLock) { if (_busy) return J(new { ok = false, msg = "busy" }); _busy = true; }
            Task.Run(() => RunDefaults());
            return J(new { ok = true });
        }

        /// <summary>ถอน tweak ทั้งหมดกลับค่าเริ่มต้น — ใช้ 12_System_Tools.ps1 -Action defaults</summary>
        private void RunDefaults()
        {
            lock (_runLock) { _busy = true; _running = true; _done = false; _ok = 0; _fail = 0; _pct = 30; }
            try
            {
                _log.Head("═══ RESTORE DEFAULTS — สำรองข้อมูลก่อนถอน tweak ═══");
                try { BackupService.Create(_log); }
                catch (Exception bex) { _log.Err("backup: " + bex.Message); }

                _log.Head("═══ RESTORE DEFAULTS — ถอน tweak ทั้งหมด (12_System_Tools -Action defaults) ═══");
                TweakRegistry.SystemTool(_log, "defaults");

                lock (_runLock)
                {
                    _ok = 1; _fail = 0; _total = 1; _applied = 0; _score = 40; _pct = 100;
                    _running = false; _done = true;
                }
                _cfg.lastScore = 40;
                _cfg.lastApplied = 0;
                SaveCfg();
                _log.Head("═══ ถอนค่าเริ่มต้นเสร็จ — แนะนำรีสตาร์ทเครื่อง ═══");
            }
            catch (Exception ex)
            {
                _log.Err("defaults: " + ex.Message);
                lock (_runLock) { _fail = 1; _done = true; }
            }
            finally
            {
                lock (_runLock) { _busy = false; _running = false; }
            }
        }

        public string Progress()
        {
            lock (_runLock)
            {
                var r = new
                {
                    running = _running,
                    done = _done,
                    pct = _pct,
                    ok = _ok,
                    fail = _fail,
                    applied = _applied,
                    total = _total,
                    score = _score
                };
                if (_done) _done = false;   // อ่านครั้งเดียวจบ
                return J(r);
            }
        }

        private void RunJob(List<Tweak> sel, bool apply)
        {
            lock (_runLock) { _busy = true; _running = true; _done = false; _ok = _fail = 0; _pct = 0; }

            try
            {
                bool restart = false;
                if (apply)
                {
                    _log.Head("กำลังสำรองข้อมูลก่อน Optimize...");
                    try { BackupService.Create(_log); }
                    catch (Exception ex) { _log.Err("backup: " + ex.Message); }
                }

                _log.Head($"═══ {(apply ? "OPTIMIZE" : "RESTORE")} : {sel.Count} รายการ ═══");

                for (int i = 0; i < sel.Count; i++)
                {
                    var t = sel[i];
                    _log.Head("â–º " + t.Name);
                    try
                    {
                        if (apply) t.ApplyAction?.Invoke(_log);
                        else t.UndoAction?.Invoke(_log);
                        _ok++;
                        if (t.NeedsRestart) restart = true;
                    }
                    catch (Exception ex) { _log.Err("ล้มเหลว: " + ex.Message); _fail++; }

                    lock (_runLock) { _pct = (int)((i + 1) * 100.0 / sel.Count); }
                }

                lock (_runLock)
                {
                    _total = sel.Count;
                    if (apply)
                    {
                        _applied = Math.Max(0, sel.Count - _fail);
                        _score = Math.Min(100, 40 + (int)Math.Round(60.0 * _applied / Math.Max(1, sel.Count)));
                    }
                    else { _applied = 0; _score = 40; }

                    _cfg.lastScore = _score;
                    _cfg.lastApplied = _applied;
                    _cfg.lastTotal = sel.Count;
                    SaveCfg();

                    _running = false;
                    _restart = restart;
                    _done = true;
                }

                _log.Head($"═══ เสร็จสิ้น — สำเร็จ {_ok} · ล้มเหลว {_fail} ═══");
                Notify(_fail > 0 ? "red" : "green", apply ? "⚡ Optimize เสร็จสิ้น" : "↩️ Restore เสร็จสิ้น",
                    "สำเร็จ **" + _ok + "** · ล้มเหลว **" + _fail + "**" + (restart ? "\nต้องรีสตาร์ทเครื่องจึงมีผลเต็มที่" : ""));

                if (restart)
                {
                    int okCount = _ok, failCount = _fail;
                    _win.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var wv = _win.Web != null ? _win.Web.CoreWebView2 : null;
                            if (wv != null)
                            {
                                // ถามผ่านหน้าต่างธีมของแอป (KR.confirm ในหน้าเว็บ) แทน MessageBox ของ Windows
                                _ = wv.ExecuteScriptAsync(
                                    "window.__krDispatch({event:'askRestart',ok:" + okCount + ",fail:" + failCount +
                                    ",apply:'" + (apply ? "opt" : "res") + "'});");
                                return;
                            }
                        }
                        catch { }
                        _log.Info("มี tweak ที่ต้องรีสตาร์ท — หน้าเว็บไม่พร้อมแสดงปุ่มยืนยัน ให้ผู้ใช้รีสตาร์ทเอง");
                    }));
                }
            }
            finally
            {
                lock (_runLock) { _busy = false; _running = false; }
            }
        }
    }
}