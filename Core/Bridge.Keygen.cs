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
                _log.Ok("Logout â€” à¸¥à¹‰à¸²à¸‡à¸à¸²à¸£à¸ˆà¸³ key à¹à¸¥à¹‰à¸§ à¸à¸¥à¸±à¸šà¸«à¸™à¹‰à¸² Activate");
            }
            catch { }
            _win.Dispatcher.BeginInvoke(new Action(_win.NavigateLogin));
            return J(new { ok = true });
        }

        public string OpenKeygen()
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            _win.Dispatcher.BeginInvoke(new Action(_win.NavigateKeygen));
            return J(new { ok = true });
        }

        /// <summary>à¹€à¸ˆà¸™ key à¹ƒà¸«à¸¡à¹ˆ â€” à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¸œà¸¹à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡ à¹ƒà¸„à¸£à¹€à¸­à¸²à¹„à¸› Activate à¸„à¸™à¹à¸£à¸à¸ˆà¸°à¹‚à¸”à¸™à¸¥à¹‡à¸­à¸„ HWID à¸—à¸±à¸™à¸—à¸µ</summary>
        public string GenerateKey(int days, string note)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            try
            {
                var lic = LicenseService.CreateKey(days, note, "KING");
                if (ServerOn)
                {
                    bool up = HttpPut(ServerKeyUrl(lic.key) + AuthQuery(), JsonSerializer.Serialize(new
                    { key = lic.key, hwid = "", created = lic.created, days = lic.days, note = lic.note, revoked = false }));
                    _log.Ok("à¸­à¸±à¸›à¹‚à¸«à¸¥à¸” key à¸‚à¸¶à¹‰à¸™ server: " + (up ? "à¸ªà¸³à¹€à¸£à¹‡à¸ˆ â€” à¸ªà¹ˆà¸‡à¹„à¸”à¹‰à¹à¸„à¹ˆà¸•à¸±à¸§ key" : "à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§ (à¸•à¸£à¸§à¸ˆà¹€à¸™à¹‡à¸•/à¸à¸²à¸£à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server)"));
                }
                _log.Ok($"à¹€à¸ˆà¸™ key à¹ƒà¸«à¸¡à¹ˆ: {lic.key} ({lic.days} à¸§à¸±à¸™) â€” à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¸œà¸¹à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡");
                Notify("blue", "ðŸ— à¹€à¸ˆà¸™ key à¹ƒà¸«à¸¡à¹ˆ", "**" + lic.key + "**\nà¸­à¸²à¸¢à¸¸ " + lic.days + " à¸§à¸±à¸™" + (string.IsNullOrWhiteSpace(lic.note) ? "" : " Â· à¹‚à¸™à¹‰à¸•: " + lic.note));
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
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });

            // à¹‚à¸«à¸¡à¸”à¸­à¸­à¸™à¹„à¸¥à¸™à¹Œ â€” à¸”à¸¶à¸‡à¸£à¸²à¸¢à¸à¸²à¸£à¸ˆà¸²à¸ server (à¸ªà¸–à¸²à¸™à¸° HWID à¹à¸¡à¹ˆà¸™à¸¢à¸³à¸ˆà¸²à¸à¸—à¸¸à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸¥à¸¹à¸à¸„à¹‰à¸²)
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
                            try { daysLeft = (int)Math.Floor((DateTime.Parse(created).AddDays(days) - DateTime.Now).TotalDays); } catch { }
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
                    _log.Err("listkeys(server): " + ex.Message + " â†’ à¹ƒà¸Šà¹‰à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸—à¸™");
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
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            if (ServerOn && !ServerReady) return J(new { ok = false, msg = "à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server â€” à¸à¸”à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸²à¹ƒà¸™à¸«à¸™à¹‰à¸²à¹€à¸ˆà¸™ key à¸à¹ˆà¸­à¸™" });
            bool ok = LicenseService.RemoveKey(key);
            if (ServerOn)
            {
                try
                {
                    bool up = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { revoked = true }));
                    ok = up;
                    _log.Warn("à¸£à¸°à¸‡à¸±à¸š key à¸šà¸™ server à¹à¸¥à¹‰à¸§: " + key + " â€” à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸¥à¸¹à¸à¸„à¹‰à¸²à¸ˆà¸°à¹€à¸‚à¹‰à¸²à¹ƒà¸Šà¹‰à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸—à¸±à¸™à¸—à¸µà¸—à¸µà¹ˆà¹€à¸›à¸´à¸”à¹à¸­à¸›à¸„à¸£à¸±à¹‰à¸‡à¸–à¸±à¸”à¹„à¸›");
                }
                catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
            }
            else if (ok) _log.Warn($"à¸¥à¸š key à¸­à¸­à¸à¸ˆà¸²à¸à¸£à¸°à¸šà¸š: {key}");
            return J(new { ok });
        }

        /// <summary>à¸£à¸µ HWID â€” à¸›à¸¥à¸”à¸à¸²à¸£à¸œà¸¹à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹€à¸”à¸´à¸¡ à¹ƒà¸«à¹‰ key à¸à¸¥à¸±à¸šà¹„à¸›à¸£à¸­à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹ƒà¸«à¸¡à¹ˆ (à¹€à¸‰à¸žà¸²à¸°à¹à¸­à¸”à¸¡à¸´à¸™)</summary>
        public string ResetHwid(string key)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            if (ServerOn && !ServerReady) return J(new { ok = false, msg = "à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server â€” à¸à¸”à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸²à¹ƒà¸™à¸«à¸™à¹‰à¸²à¹€à¸ˆà¸™ key à¸à¹ˆà¸­à¸™" });
            bool ok = LicenseService.ResetHwid(key);
            if (ServerOn)
            {
                try
                {
                    bool up = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { hwid = "" }));
                    ok = up;
                    _log.Ok("à¸£à¸µ HWID à¸šà¸™ server à¹à¸¥à¹‰à¸§: " + key + " â€” à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹€à¸”à¸´à¸¡à¸ˆà¸°à¹‚à¸”à¸™à¸”à¸µà¸”à¸à¸¥à¸±à¸šà¸«à¸™à¹‰à¸² Activate à¸„à¸£à¸±à¹‰à¸‡à¸–à¸±à¸”à¹„à¸›à¸—à¸µà¹ˆà¹€à¸›à¸´à¸”à¹à¸­à¸›");
                }
                catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
            }
            else if (ok) _log.Ok($"à¸£à¸µ HWID à¹à¸¥à¹‰à¸§: {key} â€” key à¸à¸¥à¸±à¸šà¹€à¸›à¹‡à¸™ 'à¸§à¹ˆà¸²à¸‡' à¸£à¸­à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹ƒà¸«à¸¡à¹ˆ Activate");
            return J(new { ok });
        }

        /// <summary>à¸šà¸­à¸à¸«à¸™à¹‰à¸²à¹€à¸§à¹‡à¸šà¸§à¹ˆà¸²à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸™à¸µà¹‰à¹ƒà¸Šà¹ˆà¹à¸­à¸”à¸¡à¸´à¸™à¹„à¸«à¸¡ (à¹„à¸§à¹‰à¸‹à¹ˆà¸­à¸™/à¹‚à¸Šà¸§à¹Œà¹€à¸¡à¸™à¸¹ Key Generator)</summary>
        public string AdminCheck()
        {
            Hello();
            return J(new { ok = true, admin = IsAdmin(), server = ServerOn, ready = ServerReady });
        }

        /// <summary>à¸šà¸±à¸™à¸—à¸¶à¸à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹€à¸Šà¸·à¹ˆà¸­à¸¡à¸•à¹ˆà¸­ server (API Key + à¸šà¸±à¸à¸Šà¸µà¹à¸­à¸”à¸¡à¸´à¸™) à¸ˆà¸²à¸à¸«à¸™à¹‰à¸²à¹€à¸ˆà¸™ key à¹ƒà¸™à¹à¸­à¸›</summary>
        public string ServerSave(string apiKey, string email, string pass)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
                return J(new { ok = false, msg = "à¸à¸£à¸­à¸à¹ƒà¸«à¹‰à¸„à¸£à¸šà¸—à¸¸à¸à¸Šà¹ˆà¸­à¸‡" });
            try
            {
                _cfg.srvApiKey = apiKey.Trim();
                _cfg.srvEmail = email.Trim();
                _cfg.srvPass = pass;
                _cfg.srvToken = "";
                EnsureToken();   // à¸—à¸”à¸ªà¸­à¸šà¸¥à¹‡à¸à¸­à¸´à¸™à¸—à¸±à¸™à¸—à¸µ â€” à¸œà¸´à¸”à¸žà¸¥à¸²à¸”à¸ˆà¸° throw
                SaveCfg();
                _log.Ok("à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server à¸ªà¸³à¹€à¸£à¹‡à¸ˆ â€” à¹‚à¸«à¸¡à¸”à¸­à¸­à¸™à¹„à¸¥à¸™à¹Œà¸žà¸£à¹‰à¸­à¸¡à¹ƒà¸Šà¹‰");
                return J(new { ok = true });
            }
            catch (Exception ex)
            {
                _cfg.srvApiKey = ""; _cfg.srvEmail = ""; _cfg.srvPass = ""; _cfg.srvToken = "";
                SaveCfg();
                return J(new { ok = false, msg = ex.Message });
            }
        }

        /// <summary>à¸›à¸¥à¸”à¸£à¸°à¸‡à¸±à¸š key à¸—à¸µà¹ˆà¸–à¸¹à¸à¸£à¸°à¸‡à¸±à¸šà¹„à¸§à¹‰ (server)</summary>
        public string UnbanKey(string key)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            if (!ServerReady) return J(new { ok = false, msg = "à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server" });
            try
            {
                bool ok = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { revoked = false }));
                if (ok) _log.Ok("à¸›à¸¥à¸”à¸£à¸°à¸‡à¸±à¸š key: " + key);
                return J(new { ok });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸•à¹ˆà¸­à¸­à¸²à¸¢à¸¸ key â€” à¸™à¸±à¸šà¹ƒà¸«à¸¡à¹ˆà¸ˆà¸²à¸à¸§à¸±à¸™à¸™à¸µà¹‰ (server)</summary>
        public string ExtendKey(string key, int days)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            if (!ServerReady) return J(new { ok = false, msg = "à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server" });
            if (days < 1) return J(new { ok = false, msg = "à¸ˆà¸³à¸™à¸§à¸™à¸§à¸±à¸™à¹„à¸¡à¹ˆà¸–à¸¹à¸à¸•à¹‰à¸­à¸‡" });
            try
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                bool ok = HttpPatch(ServerKeyUrl(key) + AuthQuery(), JsonSerializer.Serialize(new { days = days, created = today }));
                if (ok)
                {
                    LicenseService.UpsertLocal(key, today, days);
                    _log.Ok($"à¸•à¹ˆà¸­à¸­à¸²à¸¢à¸¸ key: {key} â†’ {days} à¸§à¸±à¸™ (à¸™à¸±à¸šà¸ˆà¸²à¸à¸§à¸±à¸™à¸™à¸µà¹‰)");
                }
                return J(new { ok });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸ªà¹ˆà¸‡à¸­à¸­à¸ key à¹€à¸›à¹‡à¸™à¹„à¸Ÿà¸¥à¹Œ licenses.json (à¹„à¸Ÿà¸¥à¹Œà¹€à¸”à¸µà¸¢à¸§) à¸¥à¸‡ Desktop â€” à¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸„à¸·à¸­ "à¹ƒà¸š license" à¸—à¸µà¹ˆà¸ªà¹ˆà¸‡à¹ƒà¸«à¹‰à¸¥à¸¹à¸à¸„à¹‰à¸²</summary>
        public string ExportKeyFile(string key)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            try
            {
                string norm = LicenseService.Normalize(key);
                var lic = LicenseService.AllKeys().FirstOrDefault(x => LicenseService.Normalize(x.key) == norm);
                if (lic == null) return J(new { ok = false, msg = "à¹„à¸¡à¹ˆà¸žà¸š key à¸™à¸µà¹‰à¹ƒà¸™à¸£à¸°à¸šà¸š" });

                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string file = Path.Combine(desktop, "KingR9-key-" + norm + ".json");
                var one = new List<LicenseService.License>
                {
                    new LicenseService.License { key = lic.key, hwid = "", created = lic.created, days = lic.days, note = lic.note }
                };
                File.WriteAllText(file, JsonSerializer.Serialize(one, new JsonSerializerOptions { WriteIndented = true }));
                _log.Ok($"à¸ªà¹ˆà¸‡à¸­à¸­à¸ key à¹€à¸›à¹‡à¸™à¹„à¸Ÿà¸¥à¹Œ: {file}");
                return J(new { ok = true, file = Path.GetFileName(file) });
            }
            catch (Exception ex) { return J(new { ok = false, msg = ex.Message }); }
        }

        /// <summary>à¸™à¸³à¹€à¸‚à¹‰à¸²à¹„à¸Ÿà¸¥à¹Œ key à¸—à¸µà¹ˆà¹à¸­à¸”à¸¡à¸´à¸™à¸ªà¹ˆà¸‡à¸¡à¸² (à¸¥à¸¹à¸à¸„à¹‰à¸²à¹ƒà¸Šà¹‰ â€” à¹„à¸¡à¹ˆà¸•à¹‰à¸­à¸‡à¹€à¸›à¹‡à¸™à¹à¸­à¸”à¸¡à¸´à¸™)</summary>
        public string ImportKeyLicenses(string json)
        {
            Hello();
            try
            {
                var incoming = JsonSerializer.Deserialize<List<LicenseService.License>>(json ?? "[]");
                if (incoming == null || incoming.Count == 0) return J(new { ok = false, msg = "à¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¹„à¸¡à¹ˆà¸¡à¸µ key" });
                int n = LicenseService.MergeLicenses(incoming);
                if (n == 0) return J(new { ok = false, msg = "key à¹ƒà¸™à¹„à¸Ÿà¸¥à¹Œà¸™à¸µà¹‰à¸¡à¸µà¸­à¸¢à¸¹à¹ˆà¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸¥à¹‰à¸§" });
                _log.Ok($"à¸™à¸³à¹€à¸‚à¹‰à¸² key à¸ˆà¸²à¸à¹„à¸Ÿà¸¥à¹Œ: {n} à¸£à¸²à¸¢à¸à¸²à¸£");
                return J(new { ok = true, count = n, key = LicenseService.Normalize(incoming[0].key) });
            }
            catch (Exception ex) { return J(new { ok = false, msg = "à¹„à¸Ÿà¸¥à¹Œà¹„à¸¡à¹ˆà¸–à¸¹à¸à¸•à¹‰à¸­à¸‡: " + ex.Message }); }
        }

        /// <summary>(debug) à¸§à¸±à¸” viewport/scroll à¸ˆà¸£à¸´à¸‡à¸‚à¸­à¸‡à¸«à¸™à¹‰à¸²à¹€à¸§à¹‡à¸š â†’ à¹€à¸‚à¸µà¸¢à¸™à¸¥à¸‡ log à¹„à¸Ÿà¸¥à¹Œ</summary>
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