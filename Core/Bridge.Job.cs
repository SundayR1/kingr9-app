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

        // ---------- JOB PROGRESS (à¸›à¸¸à¹ˆà¸¡ Apply à¸—à¸±à¹ˆà¸§à¹„à¸› â€” à¸à¸±à¸™à¸à¸”à¸‹à¹‰à¸­à¸™ + à¸£à¸²à¸¢à¸‡à¸²à¸™ %) ----------
        private bool JobBegin(string label)
        {
            lock (_jobLock)
            {
                if (_jobRunning) return false;   // à¸¡à¸µà¸‡à¸²à¸™à¸­à¸·à¹ˆà¸™à¸£à¸±à¸™à¸­à¸¢à¸¹à¹ˆ
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

        /// <summary>à¹€à¸£à¸´à¹ˆà¸¡à¸‚à¸±à¹‰à¸™à¸—à¸µà¹ˆ index/total â€” à¹à¸–à¸šà¸ˆà¸°à¹„à¸«à¸¥à¸­à¸¢à¸¹à¹ˆà¹ƒà¸™à¸Šà¹ˆà¸§à¸‡à¸‚à¸­à¸‡à¸‚à¸±à¹‰à¸™à¸™à¸µà¹‰à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™ (à¸‚à¸±à¹‰à¸™à¸–à¸±à¸”à¹„à¸›à¹„à¸«à¸¥à¸Šà¹ˆà¸§à¸‡à¸–à¸±à¸”à¹„à¸› â€” à¹„à¸¡à¹ˆà¸¡à¸µà¹€à¸žà¸”à¸²à¸™à¸£à¸§à¸¡à¹ƒà¸«à¹‰à¸„à¹‰à¸²à¸‡)</summary>
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

        /// <summary>à¸¥à¸¹à¸›à¹„à¸«à¸¥ % â€” à¹„à¸•à¹ˆà¸–à¸¶à¸‡ ~88% à¸‚à¸­à¸‡à¸Šà¹ˆà¸§à¸‡à¸‚à¸±à¹‰à¸™à¹ƒà¸™ ~10 à¸§à¸´ à¹à¸¥à¹‰à¸§à¸„à¸¥à¸²à¸™à¸Šà¹‰à¸² à¹† à¸•à¹ˆà¸­à¹€à¸™à¸·à¹ˆà¸­à¸‡ (à¹„à¸¡à¹ˆà¹€à¸à¸´à¸™à¸Šà¹ˆà¸§à¸‡à¸‚à¸­à¸‡à¸‚à¸±à¹‰à¸™à¸•à¸±à¸§à¹€à¸­à¸‡ à¹„à¸¡à¹ˆà¸™à¸´à¹ˆà¸‡à¸ªà¸™à¸´à¸—)</summary>
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
                _jobDoneMsg = msg ?? "";   // à¸„à¸‡ _jobPct à¹„à¸§à¹‰ â€” à¸«à¸™à¹‰à¸²à¹‚à¸«à¸¥à¸”à¸à¸±à¹ˆà¸‡ JS à¹€à¸›à¹‡à¸™à¸„à¸™à¹€à¸”à¹‰à¸‡ 100% à¹€à¸­à¸‡
            }
        }

        /// <summary>à¸ªà¸–à¸²à¸™à¸°à¸‡à¸²à¸™à¸›à¸±à¸ˆà¸ˆà¸¸à¸šà¸±à¸™ â€” JS poll à¸—à¸¸à¸ 0.4 à¸§à¸´ à¸•à¸­à¸™à¹‚à¸Šà¸§à¹Œà¸«à¸™à¹‰à¸²à¹‚à¸«à¸¥à¸”</summary>
        public string JobProgress()
        {
            lock (_jobLock)
            {
                // à¸Šà¹ˆà¸§à¸‡à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™à¸‡à¸²à¸™ (à¸‡à¸²à¸™à¹€à¸à¹ˆà¸²à¸ˆà¸š à¸‡à¸²à¸™à¹ƒà¸«à¸¡à¹ˆà¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹€à¸£à¸´à¹ˆà¸¡) à¸ªà¹ˆà¸‡ pct = null â†’ JS à¸„à¸‡à¹à¸–à¸šà¹„à¸§à¹‰ à¹„à¸¡à¹ˆà¸à¸£à¸°à¹‚à¸”à¸”à¸à¸¥à¸±à¸š
                int? pct = (_jobRunning || _jobDone) ? _jobPct : (int?)null;
                var r = new { running = _jobRunning, done = _jobDone, pct = pct, label = _jobLabel, ok = _jobOk, msg = _jobDoneMsg };
                if (_jobDone) _jobDone = false;   // à¸­à¹ˆà¸²à¸™à¸„à¸£à¸±à¹‰à¸‡à¹€à¸”à¸µà¸¢à¸§à¸ˆà¸š
                return J(r);
            }
        }

        /// <summary>à¸«à¹ˆà¸­à¸‡à¸²à¸™ apply à¸—à¸±à¹ˆà¸§à¹„à¸› â€” à¹€à¸£à¸´à¹ˆà¸¡ job (à¸à¸±à¸™à¸à¸”à¸‹à¹‰à¸­à¸™) + % à¹„à¸«à¸¥à¸•à¸²à¸¡à¹€à¸§à¸¥à¸²à¸ˆà¸£à¸´à¸‡ à¸ˆà¸™à¸‡à¸²à¸™à¸ˆà¸šà¹€à¸”à¹‰à¸‡ 100
        /// (à¸‡à¸²à¸™à¸—à¸µà¹ˆà¸¡à¸µ % à¸ˆà¸£à¸´à¸‡à¸•à¹ˆà¸­à¸‚à¸±à¹‰à¸™ à¹€à¸Šà¹ˆà¸™ R9Net à¹€à¸›à¹‡à¸™à¸œà¸¹à¹‰à¹€à¸£à¸µà¸¢à¸ JobStep à¹€à¸­à¸‡ â€” JobStep à¹ƒà¸Šà¹‰à¸„à¹ˆà¸² Max à¸ˆà¸¶à¸‡à¹„à¸¥à¹ˆà¸—à¸±à¸š % à¸›à¸£à¸°à¸¡à¸²à¸“à¹„à¸”à¹‰)</summary>
        private string RunApplyJob(string label, Func<string> work)
        {
            if (!JobBegin(label)) return J(new { ok = false, msg = "à¸¡à¸µà¸‡à¸²à¸™à¸à¸³à¸¥à¸±à¸‡à¸—à¸³à¸‡à¸²à¸™à¸­à¸¢à¸¹à¹ˆ â€” à¸£à¸­à¸ˆà¸šà¸à¹ˆà¸­à¸™à¹à¸¥à¹‰à¸§à¸à¸”à¹ƒà¸«à¸¡à¹ˆ" });
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
                JobEnd(false, "à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§");
                throw;
            }
            finally { try { creep.Wait(300); } catch { } }
        }

        // ---------- CHAIN JOB (à¸‡à¸²à¸™à¸¥à¸¹à¸à¹‚à¸‹à¹ˆà¸«à¸¥à¸²à¸¢à¸‚à¸±à¹‰à¸™à¸£à¸§à¸¡à¹€à¸›à¹‡à¸™ job à¹€à¸”à¸µà¸¢à¸§ â€” à¹à¸–à¸š % à¹„à¸«à¸¥à¸•à¹ˆà¸­à¹€à¸™à¸·à¹ˆà¸­à¸‡à¸•à¸¥à¸­à¸”) ----------
        private static string PStr(JsonElement p, string n) =>
            p.ValueKind == JsonValueKind.Object && p.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.String ? (x.GetString() ?? "") : "";

        private static bool IsApplyType(string t) =>
            t == "r9net" || t == "sysTool" || t == "powerApply" || t == "fivemApply" ||
            t == "strApply" || t == "svcApply" || t == "setTweak" || t == "cleanJunk" ||
            t == "powerPlanDedupe";

        /// <summary>à¸Šà¸·à¹ˆà¸­à¸‚à¸±à¹‰à¸™à¸•à¸­à¸™à¸‚à¸­à¸‡à¹à¸•à¹ˆà¸¥à¸° apply type (à¹‚à¸Šà¸§à¹Œà¹ƒà¸™à¸«à¸™à¹‰à¸²à¹‚à¸«à¸¥à¸”)</summary>
        private static string ApplyLabel(string type, JsonElement p)
        {
            switch (type)
            {
                case "setTweak":
                    return TweakLabel(PStr(p, "id"));
                case "r9net":
                    return PStr(p, "which") == "reset" ? "à¸£à¸µà¹€à¸‹à¹‡à¸• Adapter (à¹€à¸™à¹‡à¸•à¸«à¸¥à¸¸à¸”à¹à¸›à¹Šà¸š ~15 à¸§à¸´)" : "R9 internet";
                case "sysTool":    return "System Tool";
                case "powerApply": return "Power & BCD";
                case "fivemApply": return "FiveM Tweaks";
                case "strApply":   return "Timer Resolution";
                case "svcApply":   return "Services & OS Tweaks";
                case "cleanJunk":  return "Junk Cleaner";
                case "powerPlanDedupe": return "à¸¥à¸š Power Plan à¸‹à¹‰à¸³";
                default:           return "à¸à¸³à¸¥à¸±à¸‡à¸—à¸³à¸‡à¸²à¸™";
            }
        }

        /// <summary>à¸£à¸±à¸™à¸‡à¸²à¸™à¸¥à¸¹à¸à¹‚à¸‹à¹ˆà¸«à¸¥à¸²à¸¢à¸‚à¸±à¹‰à¸™à¹ƒà¸™ job à¹€à¸”à¸µà¸¢à¸§ â€” % à¸£à¸§à¸¡à¸—à¸±à¹‰à¸‡à¸Šà¸¸à¸”à¹„à¸«à¸¥à¸•à¹ˆà¸­à¹€à¸™à¸·à¹ˆà¸­à¸‡ à¸‚à¸±à¹‰à¸™à¹„à¸«à¸™ fail à¸«à¸¢à¸¸à¸”à¸—à¸±à¸™à¸—à¸µ</summary>
        private string RunChainJob(JsonElement stepsEl)
        {
            // JS à¸ªà¹ˆà¸‡ payload à¸¡à¸²à¸—à¸±à¹‰à¸‡à¸à¹‰à¸­à¸™à¹€à¸›à¹‡à¸™ { steps: [...] } â€” à¸£à¸­à¸‡à¸£à¸±à¸šà¸—à¸±à¹‰à¸‡à¹à¸šà¸š wrapper à¹à¸¥à¸° array à¸•à¸£à¸‡ à¹†
            if (stepsEl.ValueKind == JsonValueKind.Object && stepsEl.TryGetProperty("steps", out var wrap) && wrap.ValueKind == JsonValueKind.Array)
                stepsEl = wrap;
            var steps = new List<(string label, string type, JsonElement payload)>();
            if (stepsEl.ValueKind != JsonValueKind.Array) return J(new { ok = false, msg = "à¸£à¸¹à¸›à¹à¸šà¸š chain à¹„à¸¡à¹ˆà¸–à¸¹à¸à¸•à¹‰à¸­à¸‡" });
            try
            {
                foreach (var s in stepsEl.EnumerateArray())
                {
                    string t = s.TryGetProperty("type", out var tt) && tt.ValueKind == JsonValueKind.String ? tt.GetString() ?? "" : "";
                    JsonElement pl = s.TryGetProperty("payload", out var pp) && pp.ValueKind == JsonValueKind.Object ? pp : default;
                    steps.Add((s.TryGetProperty("label", out var ll) && ll.ValueKind == JsonValueKind.String ? ll.GetString() ?? "" : "", t, pl));
                }
            }
            catch { return J(new { ok = false, msg = "à¸£à¸¹à¸›à¹à¸šà¸š chain à¹„à¸¡à¹ˆà¸–à¸¹à¸à¸•à¹‰à¸­à¸‡" }); }
            if (steps.Count == 0) return J(new { ok = false, msg = "à¹„à¸¡à¹ˆà¸¡à¸µà¸‚à¸±à¹‰à¸™à¸•à¸­à¸™à¹ƒà¸«à¹‰à¸—à¸³" });

            if (!JobBegin(ApplyLabel(steps[0].type, steps[0].payload)))
                return J(new { ok = false, msg = "à¸¡à¸µà¸‡à¸²à¸™à¸à¸³à¸¥à¸±à¸‡à¸—à¸³à¸‡à¸²à¸™à¸­à¸¢à¸¹à¹ˆ â€” à¸£à¸­à¸ˆà¸šà¸à¹ˆà¸­à¸™à¹à¸¥à¹‰à¸§à¸à¸”à¹ƒà¸«à¸¡à¹ˆ" });

            var creep = Task.Run(CreepLoop);
            try
            {
                string lastMsg = "";
                for (int i = 0; i < steps.Count; i++)
                {
                    string lbl = $"à¸‚à¸±à¹‰à¸™à¸—à¸µà¹ˆ {i + 1}/{steps.Count} Â· " + (steps[i].label.Length > 0 ? steps[i].label : ApplyLabel(steps[i].type, steps[i].payload));
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
                JobEnd(false, "à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§");
                throw;
            }
            finally { try { creep.Wait(300); } catch { } }
        }

        // ---------- DISCORD WEBHOOK (à¹à¸ˆà¹‰à¸‡à¹€à¸•à¸·à¸­à¸™ log à¹€à¸«à¸•à¸¸à¸à¸²à¸£à¸“à¹Œà¸ªà¸³à¸„à¸±à¸ â€” admin) ----------

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

        /// <summary>à¸–à¸­à¸™ tweak à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”à¸à¸¥à¸±à¸šà¸„à¹ˆà¸²à¹€à¸£à¸´à¹ˆà¸¡à¸•à¹‰à¸™ â€” à¹ƒà¸Šà¹‰ 12_System_Tools.ps1 -Action defaults</summary>
        private void RunDefaults()
        {
            lock (_runLock) { _busy = true; _running = true; _done = false; _ok = 0; _fail = 0; _pct = 30; }
            try
            {
                _log.Head("â•â•â• RESTORE DEFAULTS â€” à¸ªà¸³à¸£à¸­à¸‡à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¸à¹ˆà¸­à¸™à¸–à¸­à¸™ tweak â•â•â•");
                try { BackupService.Create(_log); }
                catch (Exception bex) { _log.Err("backup: " + bex.Message); }

                _log.Head("â•â•â• RESTORE DEFAULTS â€” à¸–à¸­à¸™ tweak à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸” (12_System_Tools -Action defaults) â•â•â•");
                TweakRegistry.SystemTool(_log, "defaults");

                lock (_runLock)
                {
                    _ok = 1; _fail = 0; _total = 1; _applied = 0; _score = 40; _pct = 100;
                    _running = false; _done = true;
                }
                _cfg.lastScore = 40;
                _cfg.lastApplied = 0;
                SaveCfg();
                _log.Head("â•â•â• à¸–à¸­à¸™à¸„à¹ˆà¸²à¹€à¸£à¸´à¹ˆà¸¡à¸•à¹‰à¸™à¹€à¸ªà¸£à¹‡à¸ˆ â€” à¹à¸™à¸°à¸™à¸³à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡ â•â•â•");
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
                if (_done) _done = false;   // à¸­à¹ˆà¸²à¸™à¸„à¸£à¸±à¹‰à¸‡à¹€à¸”à¸µà¸¢à¸§à¸ˆà¸š
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
                    _log.Head("à¸à¸³à¸¥à¸±à¸‡à¸ªà¸³à¸£à¸­à¸‡à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¸à¹ˆà¸­à¸™ Optimize...");
                    try { BackupService.Create(_log); }
                    catch (Exception ex) { _log.Err("backup: " + ex.Message); }
                }

                _log.Head($"â•â•â• {(apply ? "OPTIMIZE" : "RESTORE")} : {sel.Count} à¸£à¸²à¸¢à¸à¸²à¸£ â•â•â•");

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
                    catch (Exception ex) { _log.Err("à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§: " + ex.Message); _fail++; }

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

                _log.Head($"â•â•â• à¹€à¸ªà¸£à¹‡à¸ˆà¸ªà¸´à¹‰à¸™ â€” à¸ªà¸³à¹€à¸£à¹‡à¸ˆ {_ok} Â· à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§ {_fail} â•â•â•");
                Notify(_fail > 0 ? "red" : "green", apply ? "âš¡ Optimize à¹€à¸ªà¸£à¹‡à¸ˆà¸ªà¸´à¹‰à¸™" : "â†©ï¸ Restore à¹€à¸ªà¸£à¹‡à¸ˆà¸ªà¸´à¹‰à¸™",
                    "à¸ªà¸³à¹€à¸£à¹‡à¸ˆ **" + _ok + "** Â· à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§ **" + _fail + "**" + (restart ? "\nà¸•à¹‰à¸­à¸‡à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸ˆà¸¶à¸‡à¸¡à¸µà¸œà¸¥à¹€à¸•à¹‡à¸¡à¸—à¸µà¹ˆ" : ""));

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
                                // à¸–à¸²à¸¡à¸œà¹ˆà¸²à¸™à¸«à¸™à¹‰à¸²à¸•à¹ˆà¸²à¸‡à¸˜à¸µà¸¡à¸‚à¸­à¸‡à¹à¸­à¸› (KR.confirm à¹ƒà¸™à¸«à¸™à¹‰à¸²à¹€à¸§à¹‡à¸š) à¹à¸—à¸™ MessageBox à¸‚à¸­à¸‡ Windows
                                _ = wv.ExecuteScriptAsync(
                                    "window.__krDispatch({event:'askRestart',ok:" + okCount + ",fail:" + failCount +
                                    ",apply:'" + (apply ? "opt" : "res") + "'});");
                                return;
                            }
                        }
                        catch { }
                        _log.Info("à¸¡à¸µ tweak à¸—à¸µà¹ˆà¸•à¹‰à¸­à¸‡à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸— â€” à¸«à¸™à¹‰à¸²à¹€à¸§à¹‡à¸šà¹„à¸¡à¹ˆà¸žà¸£à¹‰à¸­à¸¡à¹à¸ªà¸”à¸‡à¸›à¸¸à¹ˆà¸¡à¸¢à¸·à¸™à¸¢à¸±à¸™ à¹ƒà¸«à¹‰à¸œà¸¹à¹‰à¹ƒà¸Šà¹‰à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸­à¸‡");
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