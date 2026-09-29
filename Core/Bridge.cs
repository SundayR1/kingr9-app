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
    public class AppConfig
    {
        public bool rememberKey { get; set; }
        public string savedKey { get; set; } = "";
        public Dictionary<string, bool> toggles { get; set; } = new();
        public int prio { get; set; } = 2;
        public string powerPlan { get; set; } = "performance";
        public int lastScore { get; set; } = 40;
        public int lastApplied { get; set; } = 0;
        public int lastTotal { get; set; } = 0;
        public string srvApiKey { get; set; } = "";
        public string srvEmail { get; set; } = "";
        public string srvPass { get; set; } = "";
        public string srvToken { get; set; } = "";
        public DateTime srvTokenExp { get; set; } = DateTime.MinValue;
        public string discordWebhook { get; set; } = "";
    }

    /// <summary>
    /// à¸•à¸±à¸§à¹€à¸Šà¸·à¹ˆà¸­à¸¡ HTML UI â†” C# à¹à¸šà¸š host object â€” JS à¹€à¸£à¸µà¸¢à¸à¹€à¸¡à¸˜à¸­à¸”à¸•à¸£à¸‡à¹†
    /// à¸œà¹ˆà¸²à¸™ window.chrome.webview.hostObjects.sync.kr.*  à¹à¸¥à¸°à¹„à¸”à¹‰à¸œà¸¥à¸¥à¸±à¸žà¸˜à¹Œà¸à¸¥à¸±à¸šà¹ƒà¸™ call à¹€à¸”à¸µà¸¢à¸§
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public partial class Bridge
    {
        private readonly MainWindow _win;
        private readonly Logger _log;
        private readonly List<Tweak> _all;
        private readonly StatsService _stats;
        private System.Timers.Timer _autoTrim;
        private bool _busy;

        // progress à¸‚à¸­à¸‡ Optimize/Restore (JS à¸¡à¸² poll à¸œà¹ˆà¸²à¸™ Progress())
        private readonly object _runLock = new object();
        private bool _running, _done, _restart;
        private int _pct, _ok, _fail, _applied, _total, _score;

        // progress à¸‚à¸­à¸‡à¸›à¸¸à¹ˆà¸¡ Apply à¸—à¸±à¹ˆà¸§à¹„à¸› (r9net / power / fivem / svc / clean à¸¯à¸¥à¸¯) â€” JS à¹‚à¸Šà¸§à¹Œà¸«à¸™à¹‰à¸²à¹‚à¸«à¸¥à¸” + %
        private readonly object _jobLock = new object();
        private bool _jobRunning, _jobDone, _jobOk;
        private int _jobPct;
        private double _jobBase, _jobSpan = 100;      // à¸Šà¹ˆà¸§à¸‡ % à¸‚à¸­à¸‡à¸‚à¸±à¹‰à¸™à¸›à¸±à¸ˆà¸ˆà¸¸à¸šà¸±à¸™ (à¹à¸–à¸šà¹„à¸«à¸¥à¸ à¸²à¸¢à¹ƒà¸™à¸Šà¹ˆà¸§à¸‡à¸™à¸µà¹‰ à¹„à¸¡à¹ˆà¸—à¸±à¸šà¸‚à¸±à¹‰à¸™à¸­à¸·à¹ˆà¸™)
        private DateTime _jobStepStart = DateTime.UtcNow;
        private string _jobLabel = "", _jobDoneMsg = "";

        private AppConfig _cfg;
        private readonly object _cfgLock = new object();

        // à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸­à¸”à¸¡à¸´à¸™ (à¸”à¸¹à¸£à¸²à¸¢à¸¥à¸°à¹€à¸­à¸µà¸¢à¸”à¹ƒà¸™ LicenseService.AdminHwid)
        private bool IsAdmin() => LicenseService.IsAdmin();

        // ================= SERVER (Firebase Realtime Database â€” à¸Ÿà¸£à¸µ) =================
        // à¸§à¸´à¸˜à¸µà¹€à¸›à¸´à¸”à¹ƒà¸Šà¹‰: à¸ªà¸¡à¸±à¸„à¸£ firebase.google.com â†’ à¸ªà¸£à¹‰à¸²à¸‡à¹‚à¸›à¸£à¹€à¸ˆà¸à¸•à¹Œ â†’ à¹€à¸›à¸´à¸” Realtime Database
        // â†’ à¸„à¸±à¸”à¸¥à¸­à¸ URL à¸¡à¸²à¸§à¸²à¸‡à¹à¸—à¸™à¸„à¹ˆà¸²à¸§à¹ˆà¸²à¸‡à¸”à¹‰à¸²à¸™à¸¥à¹ˆà¸²à¸‡ (à¹€à¸Šà¹ˆà¸™ "https://xxx-default-rtdb.asia-southeast1.firebasedatabase.app")
        // à¸›à¸¥à¹ˆà¸­à¸¢à¸§à¹ˆà¸²à¸‡ = à¹ƒà¸Šà¹‰à¹‚à¸«à¸¡à¸”à¹„à¸Ÿà¸¥à¹Œ licenses.json à¹€à¸«à¸¡à¸·à¸­à¸™à¹€à¸”à¸´à¸¡
        // static readonly (à¸«à¹‰à¸²à¸¡ const) â€” à¹€à¸žà¸·à¹ˆà¸­à¹ƒà¸«à¹‰ Obfuscar à¹€à¸‚à¹‰à¸²à¸£à¸«à¸±à¸ªà¸„à¹ˆà¸²à¹„à¸”à¹‰à¸•à¸­à¸™ build (const à¸ˆà¸°à¸«à¸¥à¸¸à¸”à¹€à¸›à¹‡à¸™ plain text)
        private static readonly string FirebaseUrl = "https://kingr9-f3e43-default-rtdb.asia-southeast1.firebasedatabase.app";
        private static bool ServerOn =>
            !string.IsNullOrWhiteSpace(FirebaseUrl) && FirebaseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

        private static string ServerKeyUrl(string key) =>
            FirebaseUrl.TrimEnd('/') + "/keys/" + LicenseService.Normalize(key).Replace("-", "") + ".json";

        /// <summary>à¸žà¸£à¹‰à¸­à¸¡à¹ƒà¸Šà¹‰à¹‚à¸«à¸¡à¸”à¸­à¸­à¸™à¹„à¸¥à¸™à¹Œ (à¸¡à¸µ URL + à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸²à¸šà¸±à¸à¸Šà¸µà¹à¸­à¸”à¸¡à¸´à¸™à¹à¸¥à¹‰à¸§)</summary>
        private bool ServerReady =>
            ServerOn && !string.IsNullOrWhiteSpace(_cfg.srvApiKey) &&
            !string.IsNullOrWhiteSpace(_cfg.srvEmail) && !string.IsNullOrWhiteSpace(_cfg.srvPass);

        /// <summary>à¸¥à¹‡à¸à¸­à¸´à¸™à¸”à¹‰à¸§à¸¢à¸šà¸±à¸à¸Šà¸µà¹à¸­à¸”à¸¡à¸´à¸™ â†’ à¹„à¸”à¹‰ token (à¸­à¸²à¸¢à¸¸ ~1 à¸Šà¸¡. à¹€à¸à¹‡à¸š cache à¹ƒà¸™ config)</summary>
        private void EnsureToken()
        {
            if (!string.IsNullOrWhiteSpace(_cfg.srvToken) && DateTime.Now < _cfg.srvTokenExp.AddMinutes(-5)) return;
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" +
                Uri.EscapeDataString(_cfg.srvApiKey);
            using var resp = Http.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(new { email = _cfg.srvEmail, password = _cfg.srvPass, returnSecureToken = true }),
                Encoding.UTF8, "application/json")).Result;
            if (!resp.IsSuccessStatusCode)
                throw new Exception("à¹€à¸‚à¹‰à¸²à¸ªà¸¹à¹ˆà¸£à¸°à¸šà¸š server à¹„à¸¡à¹ˆà¸ªà¸³à¹€à¸£à¹‡à¸ˆ â€” à¸•à¸£à¸§à¸ˆ Web API Key / à¸­à¸µà¹€à¸¡à¸¥ / à¸£à¸«à¸±à¸ªà¸œà¹ˆà¸²à¸™à¹à¸­à¸”à¸¡à¸´à¸™");
            var j = JsonSerializer.Deserialize<JsonElement>(resp.Content.ReadAsStringAsync().Result);
            _cfg.srvToken = j.GetProperty("idToken").GetString() ?? "";
            _cfg.srvTokenExp = DateTime.Now.AddSeconds(3600);
            SaveCfg();
        }

        /// <summary>query auth à¸ªà¸³à¸«à¸£à¸±à¸š REST (à¸–à¹‰à¸²à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² à¸„à¸·à¸™à¸„à¹ˆà¸²à¸§à¹ˆà¸²à¸‡ = à¹€à¸£à¸µà¸¢à¸à¹à¸šà¸šà¹„à¸¡à¹ˆà¸¡à¸µ token)</summary>
        private string AuthQuery()
        {
            if (!ServerReady) return "";
            EnsureToken();
            return "?auth=" + Uri.EscapeDataString(_cfg.srvToken);
        }

        private static string HttpGet(string url)
        {
            try
            {
                using var resp = Http.GetAsync(url).Result;
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return "null";   // key à¹„à¸¡à¹ˆà¸¡à¸µà¸šà¸™ server
                return resp.Content.ReadAsStringAsync().Result;
            }
            catch { return null; }   // à¹€à¸™à¹‡à¸•/à¹€à¸‹à¸´à¸£à¹Œà¸Ÿà¹€à¸§à¸­à¸£à¹Œà¹€à¸‚à¹‰à¸²à¹„à¸¡à¹ˆà¸–à¸¶à¸‡
        }

        private static bool HttpPut(string url, string json)
        {
            try
            {
                using var resp = Http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json")).Result;
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private static bool HttpPatch(string url, string json)
        {
            try
            {
                using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                { Content = new StringContent(json, Encoding.UTF8, "application/json") };
                using var resp = Http.SendAsync(req).Result;
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>Activate à¸œà¹ˆà¸²à¸™ server â€” à¸œà¸¹à¸ HWID à¸—à¸µà¹ˆà¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸£à¸à¸—à¸±à¸™à¸—à¸µ + à¸•à¸£à¸§à¸ˆà¸­à¸²à¸¢à¸¸/à¸à¸²à¸£à¸£à¸°à¸‡à¸±à¸šà¸—à¸¸à¸à¸„à¸£à¸±à¹‰à¸‡
        /// offline=true = à¸•à¸´à¸”à¸•à¹ˆà¸­ server à¹„à¸¡à¹ˆà¸–à¸¶à¸‡ (à¹„à¸¡à¹ˆà¹ƒà¸Šà¹ˆà¸à¸²à¸£à¸–à¸¹à¸à¸›à¸à¸´à¹€à¸ªà¸˜ â€” à¹ƒà¸«à¹‰à¹ƒà¸Šà¹‰à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸—à¸™à¹„à¸”à¹‰)</summary>
        private (bool ok, string msg, int days, bool offline) ServerActivate(string norm, string hw)
        {
            try
            {
                string body = HttpGet(ServerKeyUrl(norm));
                if (body == null)
                    return (false, "à¹€à¸Šà¸·à¹ˆà¸­à¸¡à¸•à¹ˆà¸­ server à¹„à¸¡à¹ˆà¹„à¸”à¹‰ â€” à¸•à¸£à¸§à¸ˆà¸­à¸´à¸™à¹€à¸—à¸­à¸£à¹Œà¹€à¸™à¹‡à¸•", 0, true);
                if (string.IsNullOrWhiteSpace(body) || body == "null")
                    return (false, "à¹„à¸¡à¹ˆà¸žà¸š key à¸™à¸µà¹‰à¹ƒà¸™à¸£à¸°à¸šà¸š", 0, false);

                var j = JsonSerializer.Deserialize<JsonElement>(body);
                if (j.TryGetProperty("revoked", out var rv) && rv.ValueKind == JsonValueKind.True)
                    return (false, "key à¸™à¸µà¹‰à¸–à¸¹à¸à¸£à¸°à¸‡à¸±à¸šà¸à¸²à¸£à¹ƒà¸Šà¹‰à¸‡à¸²à¸™", 0, false);
                if (!j.TryGetProperty("days", out var dEl) || dEl.ValueKind != JsonValueKind.Number)
                    return (false, "à¸‚à¹‰à¸­à¸¡à¸¹à¸¥ key à¸šà¸™ server à¹„à¸¡à¹ˆà¸ªà¸¡à¸šà¸¹à¸£à¸“à¹Œ", 0, false);
                int days = dEl.GetInt32();
                string hwid = j.TryGetProperty("hwid", out var hEl) && hEl.ValueKind == JsonValueKind.String ? hEl.GetString() : "";
                string created = j.TryGetProperty("created", out var cEl) && cEl.ValueKind == JsonValueKind.String
                    ? cEl.GetString() : DateTime.Now.ToString("yyyy-MM-dd");

                if (string.IsNullOrEmpty(hwid))
                {
                    created = DateTime.Now.ToString("yyyy-MM-dd");   // à¸­à¸²à¸¢à¸¸à¹€à¸£à¸´à¹ˆà¸¡à¸™à¸±à¸šà¸§à¸±à¸™ Activate à¸‚à¸­à¸‡à¸¥à¸¹à¸à¸„à¹‰à¸²
                    if (!HttpPatch(ServerKeyUrl(norm), JsonSerializer.Serialize(new { hwid = hw, created, key = norm })))
                        return (false, "à¹€à¸Šà¸·à¹ˆà¸­à¸¡à¸•à¹ˆà¸­ server à¹„à¸¡à¹ˆà¸ªà¸³à¹€à¸£à¹‡à¸ˆ (à¸šà¸±à¸™à¸—à¸¶à¸ HWID)", 0, true);
                    LicenseService.UpsertLocal(norm, created, days);
                    return (true, "à¸¥à¹‡à¸­à¸„ HWID à¸ªà¸³à¹€à¸£à¹‡à¸ˆ", days, false);
                }
                if (hwid != hw) return (false, "key à¸™à¸µà¹‰à¸–à¸¹à¸à¸œà¸¹à¸à¸à¸±à¸šà¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸­à¸·à¹ˆà¸™à¸­à¸¢à¸¹à¹ˆ", 0, false);

                int left = (int)Math.Floor((DateTime.Parse(created).AddDays(days) - DateTime.Now).TotalDays);
                if (left < 0) return (false, "key à¸«à¸¡à¸”à¸­à¸²à¸¢à¸¸à¹à¸¥à¹‰à¸§ â€” à¸•à¸´à¸”à¸•à¹ˆà¸­à¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸žà¸·à¹ˆà¸­à¸•à¹ˆà¸­à¸­à¸²à¸¢à¸¸", 0, false);
                LicenseService.UpsertLocal(norm, created, days);
                return (true, "key à¸–à¸¹à¸à¸•à¹‰à¸­à¸‡", left, false);
            }
            catch
            {
                return (false, "à¹€à¸Šà¸·à¹ˆà¸­à¸¡à¸•à¹ˆà¸­ server à¹„à¸¡à¹ˆà¹„à¸”à¹‰ â€” à¸•à¸£à¸§à¸ˆà¸­à¸´à¸™à¹€à¸—à¸­à¸£à¹Œà¹€à¸™à¹‡à¸•", 0, true);
            }
        }

        public Bridge(MainWindow win, Logger log, List<Tweak> all)
        {
            _win = win;
            _log = log;
            _all = all;
            _cfg = LoadCfg();

            // à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸­à¸”à¸¡à¸´à¸™: à¸¢à¸à¸£à¸°à¸”à¸±à¸š key à¸‚à¸­à¸‡à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¸™à¸µà¹‰à¹€à¸›à¹‡à¸™à¸£à¸¹à¸›à¹à¸šà¸š KINGR9 à¸­à¸±à¸•à¹‚à¸™à¸¡à¸±à¸•à¸´ (à¸—à¸³à¸„à¸£à¸±à¹‰à¸‡à¹€à¸”à¸µà¸¢à¸§)
            try
            {
                string nk = LicenseService.EnsureAdminKey();
                if (nk != null)
                {
                    _cfg.savedKey = nk;
                    _cfg.rememberKey = true;
                    SaveCfg();
                    _log.Ok($"à¹€à¸›à¸¥à¸µà¹ˆà¸¢à¸™ key à¸‚à¸­à¸‡à¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸›à¹‡à¸™à¸£à¸¹à¸›à¹à¸šà¸š KINGR9: {nk}");
                }
            }
            catch { }

            _stats = new StatsService();
        }

        /// <summary>เรียกตอนปิดแอป — ล้าง timer และ stats ให้เรียบร้อย</summary>
        public void Cleanup()
        {
            try { _autoTrim?.Stop(); _autoTrim?.Dispose(); _autoTrim = null; } catch { }
            try { _stats?.Dispose(); } catch { }
        }

        private static AppConfig LoadCfg()
        {
            try
            {
                if (File.Exists(LicenseService.ConfigFile))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(LicenseService.ConfigFile)) ?? new AppConfig();
            }
            catch { }
            return new AppConfig();
        }

        private void SaveCfg()
        {
            lock (_cfgLock) { try { LicenseService.SaveConfig(_cfg); } catch { } }
        }

        private int _helloDoneFlag;

        private void Hello()
        {
            if (System.Threading.Interlocked.Exchange(ref _helloDoneFlag, 1) == 1) return;
            _log.Ok("bridge: host-object JS↔C# เชื่อมต่อสำเร็จ");
        }

        private static string J(object o) => JsonSerializer.Serialize(o);

        // à¸‡à¸²à¸™à¹„à¸«à¸™à¸™à¸²à¸™ (netsh/bcdedit/à¸§à¸±à¸”à¸‚à¸¢à¸°/à¸•à¸£à¸§à¸ˆ server) à¸«à¸™à¹‰à¸²à¸•à¹ˆà¸²à¸‡à¸ˆà¸°à¸‚à¸¶à¹‰à¸™ "Not Responding"
        // à¹ƒà¸«à¸¡à¹ˆ: bridge.js à¸ªà¹ˆà¸‡ { __kr, type, payload } à¸¡à¸²à¸—à¸²à¸‡ postMessage â†’ à¸‡à¸²à¸™à¸«à¸™à¸±à¸à¸—à¸±à¹‰à¸‡à¸«à¸¡à¸”
        // à¸¥à¸‡ background thread à¹à¸¥à¹‰à¸§à¸ªà¹ˆà¸‡à¸œà¸¥à¸à¸¥à¸±à¸šà¹€à¸›à¹‡à¸™ { __krRes, res } à¹ƒà¸«à¹‰ JS resolve à¸•à¸²à¸¡ id

        /// <summary>à¸£à¸±à¸š rpc à¸ˆà¸²à¸à¸«à¸™à¹‰à¸²à¹€à¸§à¹‡à¸š (à¹€à¸£à¸µà¸¢à¸à¸ˆà¸²à¸ MainWindow.WebMessageReceived â€” UI thread à¸•à¹‰à¸­à¸‡à¸à¸¥à¸±à¸šà¸—à¸±à¸™à¸—à¸µ)</summary>
        public void OnWebMessage(string json)
        {
            JsonElement msg;
            try { msg = JsonSerializer.Deserialize<JsonElement>(json ?? ""); } catch { return; }
            if (msg.ValueKind != JsonValueKind.Object || !msg.TryGetProperty("__kr", out var idEl)) return;  // à¹„à¸¡à¹ˆà¹ƒà¸Šà¹ˆ rpc
            long id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64() : 0;
            string type = msg.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.String ? tEl.GetString() : "";
            JsonElement payload = msg.TryGetProperty("payload", out var pEl) && pEl.ValueKind == JsonValueKind.Object ? pEl : default;

            string PS(string n) => payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.String ? (x.GetString() ?? "") : "";

            // à¸¥à¸²à¸/à¸¢à¹ˆà¸­/à¸‚à¸¢à¸²à¸¢/à¸›à¸´à¸”à¸«à¸™à¹‰à¸²à¸•à¹ˆà¸²à¸‡ â€” à¸—à¸³à¸šà¸™ UI thread à¸—à¸±à¸™à¸—à¸µ (WebMessageReceived à¸­à¸¢à¸¹à¹ˆà¸šà¸™ UI thread à¸­à¸¢à¸¹à¹ˆà¹à¸¥à¹‰à¸§)
            // à¸¢à¸´à¹ˆà¸‡à¹€à¸£à¹‡à¸§ DragMove à¸¢à¸´à¹ˆà¸‡à¸•à¸²à¸¡à¹€à¸¡à¸²à¸ªà¹Œà¸—à¸±à¸™ â€” à¸–à¹‰à¸²à¸£à¸­ Task.Run à¹€à¸¡à¸²à¸ªà¹Œà¸ˆà¸°à¸›à¸¥à¹ˆà¸­à¸¢à¸à¹ˆà¸­à¸™à¹à¸¥à¹‰à¸§à¸¥à¸²à¸à¹„à¸¡à¹ˆà¸•à¸´à¸”
            if (type == "window")
            {
                try
                {
                    switch (PS("action"))
                    {
                        case "drag": _win.DoDrag(); break;
                        case "min": _win.DoMin(); break;
                        case "max": _win.DoMax(); break;
                        case "close": _win.DoClose(); break;
                    }
                }
                catch { }
                PostRpc(id, J(new { ok = true }));
                return;
            }

            Task.Run(() =>
            {
                string result;
                try
                {
                    // à¸‡à¸²à¸™à¸¥à¸¹à¸à¹‚à¸‹à¹ˆà¸«à¸¥à¸²à¸¢à¸‚à¸±à¹‰à¸™ â†’ job à¹€à¸”à¸µà¸¢à¸§ à¹à¸–à¸š % à¹„à¸«à¸¥à¸•à¹ˆà¸­à¹€à¸™à¸·à¹ˆà¸­à¸‡à¸•à¸¥à¸­à¸”à¸—à¸±à¹‰à¸‡à¸Šà¸¸à¸”
                    if (type == "applyChain") result = RunChainJob(payload);
                    // à¸‡à¸²à¸™ apply à¸›à¸à¸•à¸´ â†’ à¸«à¹ˆà¸­ job (à¸à¸±à¸™à¸à¸”à¸‹à¹‰à¸­à¸™ + à¸£à¸²à¸¢à¸‡à¸²à¸™ %)
                    else if (IsApplyType(type)) result = RunApplyJob(ApplyLabel(type, payload), () => RunRpc(type, payload));
                    else result = RunRpc(type, payload);
                }
                catch (Exception ex) { result = J(new { ok = false, msg = "à¸œà¸´à¸”à¸žà¸¥à¸²à¸”: " + ex.Message }); }
                PostRpc(id, result);
            });
        }

        /// <summary>à¹€à¸ªà¹‰à¸™à¸—à¸²à¸‡ rpc (à¸•à¸±à¸§à¸ˆà¸£à¸´à¸‡ à¹„à¸¡à¹ˆà¸«à¹ˆà¸­ job) â†’ à¹€à¸¡à¸˜à¸­à¸”à¹€à¸”à¸´à¸¡à¸—à¸¸à¸à¸•à¸±à¸§ (à¸£à¸±à¸™à¸šà¸™ background thread â€” à¹€à¸¡à¸˜à¸­à¸”à¸—à¸µà¹ˆà¹à¸•à¸° UI à¹ƒà¸Šà¹‰ Dispatcher à¸ à¸²à¸¢à¹ƒà¸™à¸­à¸¢à¸¹à¹ˆà¹à¸¥à¹‰à¸§)</summary>
        private string RunRpc(string type, JsonElement payload)
        {
            string PS(string n) => payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.String ? (x.GetString() ?? "") : "";
            bool PB(string n) => payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.True;
            int PI(string n, int def) => payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.Number ? x.GetInt32() : def;

            switch (type)
            {
                case "hwid":           return Hwid();
                case "getSaved":       return GetSaved();
                case "copy":           return Copy(PS("text"));
                case "activate":       return Activate(PS("key"), PB("remember"));
                case "recover":        return Recover();
                case "enterDashboard": return EnterDashboard();
                case "dashboardReady": return DashboardInit();
                case "getJunk":        return Junk();
                case "cleanJunk":      return Clean();
                case "freeStandby":    return FreeStandby();
                case "setTweak":       return Tweak(PS("id"), PB("on"));
                case "optimize":       return OptimizeStart();
                case "restoreAll":     return RestoreStart();
                case "window":         return Window(PS("action"));
                case "logs":           return Logs();
                case "r9net":          return R9Net(PS("which"));
                case "sysTool":        return SysTool(PS("action"));
                case "motherboard":    return Motherboard();
                case "openUrl":        return OpenUrl(PS("url"));
                case "openFolder":     return OpenFolder(PS("which"));
                case "powerApply":     return PowerApply(PB("plan"), PB("hib"), PB("boost"), PB("delay"));
                case "powerPlans":     return PowerPlans();
                case "powerPlanDedupe": return PowerPlanDedupe();
                case "aresRun":        return AresRun(PB("update"));
                case "discordGet":     return DiscordGet();
                case "discordSave":    return DiscordSave(PS("url"));
                case "discordTest":    return DiscordTest();
                case "updateCheck":    return UpdateCheck();
                case "updateDownload": return UpdateDownload();
                case "updatePublish":  return UpdatePublish(PS("version"), PS("url"), PS("notes"));
                case "restartNow":     return RestartNow();
                case "fivemApply":     return FivemApply(PB("cache"), PB("prio"), PB("cef"), PB("pkg"));
                case "strApply":       return StrApply(PB("low"));
                case "svcApply":       return SvcApply(PB("netstack"), PB("services"), PB("usb"), PB("latency"), PB("bcd"));
                case "logout":         return Logout();
                case "openKeygen":     return OpenKeygen();
                case "genKey":         return GenerateKey(PI("days", 30), PS("note"));
                case "listKeys":       return ListKeys();
                case "removeKey":      return RemoveKey(PS("key"));
                case "resetHwid":      return ResetHwid(PS("key"));
                case "adminCheck":     return AdminCheck();
                case "exportKey":      return ExportKeyFile(PS("key"));
                case "importKey":      return ImportKeyLicenses(PS("json"));
                case "serverSave":     return ServerSave(PS("apiKey"), PS("email"), PS("pass"));
                case "unbanKey":       return UnbanKey(PS("key"));
                case "extendKey":      return ExtendKey(PS("key"), PI("days", 30));
                case "dbgScroll":      return DbgScroll();
                case "progress":       return Progress();
                case "stats":          return Stats();
                case "job":            return JobProgress();
                default:               return J(new { ok = false, msg = "unknown: " + type });
            }
        }

        /// <summary>à¸ªà¹ˆà¸‡à¸œà¸¥à¸¥à¸±à¸žà¸˜à¹Œà¸à¸¥à¸±à¸šà¸«à¸™à¹‰à¸²à¹€à¸§à¹‡à¸š (PostWebMessageAsJson à¸•à¹‰à¸­à¸‡à¹€à¸£à¸µà¸¢à¸à¸šà¸™ UI thread)</summary>
        private void PostRpc(long id, string resultJson)
        {
            JsonElement res;
            try { res = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(resultJson) ? "{}" : resultJson); }
            catch { res = JsonSerializer.Deserialize<JsonElement>("{}"); }
            string wrapped = JsonSerializer.Serialize(new { __krRes = id, res });
            _win.Dispatcher.BeginInvoke(new Action(() =>
            {
                try { _win.Web?.CoreWebView2?.PostWebMessageAsJson(wrapped); } catch { }
            }));
        }
    }
}