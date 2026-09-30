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
        // ================= LOGOUT / KEYGEN =================
        public string Logout()
        {
            Hello();
            try
            {
                _cfg.rememberKey = false;
                _cfg.savedKey = "";
                SaveCfg();
                _log.Ok("Logout — ล้างการจำ key แล้ว กลับหน้า Activate");
            }
            catch { }
            _win.Dispatcher.BeginInvoke(new Action(_win.NavigateLogin));
            return J(new { ok = true });
        }

        public string OpenKeygen()
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            _win.Dispatcher.BeginInvoke(new Action(_win.NavigateKeygen));
            return J(new { ok = true });
        }

        /// <summary>เจน key ใหม่ — ยังไม่ผูกเครื่อง ใครเอาไป Activate คนแรกจะโดนล็อค HWID ทันที</summary>
        public string GenerateKey(int days, string note)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            try
            {
                var lic = LicenseService.CreateKey(days, note, "KING");
                if (ServerOn)
                {
                    bool up = HttpPut(ServerKeyUrl(lic.key) + AuthQuery(), JsonSerializer.Serialize(new
                    { key = lic.key, hwid = "", created = lic.created, days = lic.days, note = lic.note, revoked = false }));
                    _log.Ok("อัปโหลด key ขึ้น server: " + (up ? "สำเร็จ — ส่งได้แค่ตัว key" : "ล้มเหลว (ตรวจเน็ต/การตั้งค่า server)"));
                }
                _log.Ok($"เจน key ใหม่: {lic.key} ({lic.days} วัน) — ยังไม่ผูกเครื่อง");
                Notify("blue", "🗝 เจน key ใหม่", "**" + lic.key + "**\nอายุ " + lic.days + " วัน" + (string.IsNullOrWhiteSpace(lic.note) ? "" : " · โน้ต: " + lic.note));
                return J(new { ok = true, key = lic.key, days = lic.days, created = lic.created });
            }
            catch (Exception ex)
            {
                _log.Err("genkey: " + ex.Message);
                return J(new { ok = false, msg = ex.Message });
            }
        }

        public string ListKeys()
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });

            // โหมดออนไลน์ — ดึงรายการจาก server (สถานะ HWID แม่นยำจากทุกเครื่องลูกค้า)
            if (ServerReady)
            {
                try
                {
                    string body = HttpGet(FirebaseUrl.TrimEnd('/') + "/keys.json" + AuthQuery());
                    var items = new List<object>();
                    if (!string.IsNullOrWhiteSpace(body) && body != "null")
                    {
                        var map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
                        foreach (var kv in map)
                        {
                            var e = kv.Value;
                            string created = e.TryGetProperty("created", out var cEl) && cEl.ValueKind == JsonValueKind.String ? cEl.GetString() : "";
                            int days = e.TryGetProperty("days", out var dEl) && dEl.ValueKind == JsonValueKind.Number ? dEl.GetInt32() : 30;
                            string hwid = e.TryGetProperty("hwid", out var hEl) && hEl.ValueKind == JsonValueKind.String ? hEl.GetString() : "";
                            bool revoked = e.TryGetProperty("revoked", out var rEl) && rEl.ValueKind == JsonValueKind.True;
                            int daysLeft = 0;
                            try { daysLeft = LicenseService.DaysLeft(new LicenseService.License { created = created, days = days }); } catch { }
                            items.Add(new
                            {
                                key = e.TryGetProperty("key", out var kEl) && kEl.ValueKind == JsonValueKind.String ? kEl.GetString() : LicenseService.Normalize(kv.Key),
                                note = e.TryGetProperty("note", out var nEl) && nEl.ValueKind == JsonValueKind.String ? nEl.GetString() : "",
                                created,
                                days,
                                daysLeft,
                                bound = !string.IsNullOrEmpty(hwid),
                                hwid,
                                revoked
                            });
                        }
                    }
                    return J(new { ok = true, items, server = true });
                }
                catch (Exception ex)
                {
                    _log.Err("listkeys(server): " + ex.Message + " → ใช้ข้อมูลในเครื่องแทน");
                }
            }

            var items2 = LicenseService.AllKeys().Select(l => new
            {
                key = l.key,
                note = l.note ?? "",
                created = l.created,
                days = l.days,
                daysLeft = LicenseService.DaysLeft(l),
                bound = !string.IsNullOrEmpty(l.hwid),
                hwid = l.hwid ?? "",
                revoked = false
            }).ToList();
            return J(new { ok = true, items = items2, server = false });
        }

        public string RemoveKey(string key)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            if (ServerOn && !ServerReady) return J(new { ok = false, msg = "ยังไม่ได้ตั้งค่า server — กดตั้งค่าในหน้าเจน key ก่อน" });
            bool ok = LicenseService.RemoveKey(key);
            if (ServerOn)
            {
                try
                {
                    bool up = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { revoked = true }));
                    ok = up;
                    _log.Warn("ระงับ key บน server แล้ว: " + key + " — เครื่องลูกค้าจะเข้าใช้ไม่ได้ทันทีที่เปิดแอปครั้งถัดไป");
                }
                catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
            }
            else if (ok) _log.Warn($"ลบ key ออกจากระบบ: {key}");
            return J(new { ok });
        }

        /// <summary>รี HWID — ปลดการผูกเครื่องเดิม ให้ key กลับไปรอเครื่องใหม่ (เฉพาะแอดมิน)</summary>
        public string ResetHwid(string key)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            if (ServerOn && !ServerReady) return J(new { ok = false, msg = "ยังไม่ได้ตั้งค่า server — กดตั้งค่าในหน้าเจน key ก่อน" });
            bool ok = LicenseService.ResetHwid(key);
            if (ServerOn)
            {
                try
                {
                    bool up = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { hwid = "" }));
                    ok = up;
                    _log.Ok("รี HWID บน server แล้ว: " + key + " — เครื่องเดิมจะโดนดีดกลับหน้า Activate ครั้งถัดไปที่เปิดแอป");
                }
                catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
            }
            else if (ok) _log.Ok($"รี HWID แล้ว: {key} — key กลับเป็น 'ว่าง' รอเครื่องใหม่ Activate");
            return J(new { ok });
        }

        /// <summary>บอกหน้าเว็บว่าเครื่องนี้ใช่แอดมินไหม (ไว้ซ่อน/โชว์เมนู Key Generator)</summary>
        public string AdminCheck()
        {
            Hello();
            return J(new { ok = true, admin = IsAdmin(), server = ServerOn, ready = ServerReady });
        }

        /// <summary>บันทึกข้อมูลเชื่อมต่อ server (API Key + บัญชีแอดมิน) จากหน้าเจน key ในแอป</summary>
        public string ServerSave(string apiKey, string email, string pass)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
                return J(new { ok = false, msg = "กรอกให้ครบทุกช่อง" });
            try
            {
                _cfg.srvApiKey = apiKey.Trim();
                _cfg.srvEmail = email.Trim();
                _cfg.srvPass = pass;
                _cfg.srvToken = "";
                EnsureToken();   // ทดสอบล็กอินทันที — ผิดพลาดจะ throw
                SaveCfg();
                _log.Ok("ตั้งค่า server สำเร็จ — โหมดออนไลน์พร้อมใช้");
                return J(new { ok = true });
            }
            catch (Exception ex)
            {
                _cfg.srvApiKey = ""; _cfg.srvEmail = ""; _cfg.srvPass = ""; _cfg.srvToken = "";
                SaveCfg();
                return J(new { ok = false, msg = ex.Message });
            }
        }

        /// <summary>ปลดระงับ key ที่ถูกระงับไว้ (server)</summary>
        public string UnbanKey(string key)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            if (!ServerReady) return J(new { ok = false, msg = "ยังไม่ได้ตั้งค่า server" });
            try
            {
                bool ok = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { revoked = false }));
                if (ok) _log.Ok("ปลดระงับ key: " + key);
                return J(new { ok });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>ต่ออายุ key — นับใหม่จากวันนี้ (server)</summary>
        public string ExtendKey(string key, int days)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            if (!ServerReady) return J(new { ok = false, msg = "ยังไม่ได้ตั้งค่า server" });
            if (days < 1) return J(new { ok = false, msg = "จำนวนวันไม่ถูกต้อง" });
            try
            {
                string today = LicenseService.TodayUtc();
                bool ok = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { days = days, created = today }));
                if (ok)
                {
                    LicenseService.UpsertLocal(key, today, days);
                    _log.Ok($"ต่ออายุ key: {key} → {days} วัน (นับจากวันนี้)");
                }
                return J(new { ok });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>ส่งออก key เป็นไฟล์ licenses.json (ไฟล์เดียว) ลง Desktop — ไฟล์นี้คือ "ใบ license" ที่ส่งให้ลูกค้า</summary>
        public string ExportKeyFile(string key)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            try
            {
                string norm = LicenseService.Normalize(key);
                var lic = LicenseService.AllKeys().FirstOrDefault(x => LicenseService.Normalize(x.key) == norm);
                if (lic == null) return J(new { ok = false, msg = "ไม่พบ key นี้ในระบบ" });

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string file = Path.Combine(desktop, "KingR9-key-" + norm + ".json");
                var one = new List<LicenseService.License>
                {
                    new LicenseService.License { key = lic.key, hwid = "", created = lic.created, days = lic.days, note = lic.note }
                };
                File.WriteAllText(file, JsonSerializer.Serialize(one, new JsonSerializerOptions { WriteIndented = true }));
                _log.Ok($"ส่งออก key เป็นไฟล์: {file}");
                return J(new { ok = true, file = Path.GetFileName(file) });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>นำเข้าไฟล์ key ที่แอดมินส่งมา (ลูกค้าใช้ — ไม่ต้องเป็นแอดมิน)</summary>
        public string ImportKeyLicenses(string json)
        {
            Hello();
            try
            {
                var incoming = JsonSerializer.Deserialize<List<LicenseService.License>>(json ?? "[]");
                if (incoming == null || incoming.Count == 0) return J(new { ok = false, msg = "ไฟล์นี้ไม่มี key" });
                int n = LicenseService.MergeLicenses(incoming);
                if (n == 0) return J(new { ok = false, msg = "key ในไฟล์นี้มีอยู่ในเครื่องแล้ว" });
                _log.Ok($"นำเข้า key จากไฟล์: {n} รายการ");
                return J(new { ok = true, count = n, key = LicenseService.Normalize(incoming[0].key) });
            }
            catch (Exception ex) { return J(new { ok = false, msg = "ไฟล์ไม่ถูกต้อง: " + ex.Message }); }
        }

        /// <summary>(debug) วัด viewport/scroll จริงของหน้าเว็บ → เขียนลง log ไฟล์</summary>
        public string DbgScroll()
        {
            Hello();
            var wv = _win.Web != null ? _win.Web.CoreWebView2 : null;
            if (wv == null) return J(new { ok = false });
            const string js = "JSON.stringify({iw:innerWidth,ih:innerHeight,dpr:devicePixelRatio," +
                "body:document.body.scrollHeight," +
                "app:(function(){var a=document.querySelector('.app');return a?Math.round(a.getBoundingClientRect().bottom):null;})()," +
                "sc:(function(){var s=document.querySelector('.scroll');return s?{sh:s.scrollHeight,ch:s.clientHeight,st:s.scrollTop," +
                "top:Math.round(s.getBoundingClientRect().top),bot:Math.round(s.getBoundingClientRect().bottom)}:null;})()})";
            _ = DbgRun(wv, js);
            return J(new { ok = true });
        }

        private async System.Threading.Tasks.Task DbgRun(Microsoft.Web.WebView2.Core.CoreWebView2 wv, string js)
        {
            try { var r = await wv.ExecuteScriptAsync(js); _log.Info("DBG-SCROLL: " + r); }
            catch (Exception ex) { _log.Err("DbgScroll: " + ex.Message); }
        }
    }
}