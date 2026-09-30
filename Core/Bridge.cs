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
    /// ตัวเชื่อม HTML UI ↔ C# แบบ host object — JS เรียกเมธอดตรงๆ
    /// ผ่าน window.chrome.webview.hostObjects.sync.kr.*  และได้ผลลัพธ์กลับใน call เดียว
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public partial class Bridge
    {
        private readonly MainWindow _win;
        private readonly Logger _log;
        private readonly List<Tweak> _all;
        private readonly StatsService _stats;
        private bool _busy;

        // progress ของ Optimize/Restore (JS มา poll ผ่าน Progress())
        private readonly object _runLock = new object();
        private bool _running, _done, _restart;
        private int _pct, _ok, _fail, _applied, _total, _score;

        // progress ของปุ่ม Apply ทั่วไป (r9net / power / fivem / svc / clean ฯลฯ) — JS โชว์หน้าโหลด + %
        private readonly object _jobLock = new object();
        private bool _jobRunning, _jobDone, _jobOk;
        private int _jobPct;
        private double _jobBase, _jobSpan = 100;      // ช่วง % ของขั้นปัจจุบัน (แถบไหลภายในช่วงนี้ ไม่ทับขั้นอื่น)
        private DateTime _jobStepStart = DateTime.UtcNow;
        private string _jobLabel = "", _jobDoneMsg = "";

        private AppConfig _cfg;
        private readonly object _cfgLock = new object();

        // เครื่องแอดมิน (ดูรายละเอียดใน LicenseService.AdminHwid)
        private bool IsAdmin() => LicenseService.IsAdmin();

        // ================= SERVER (Firebase Realtime Database — ฟรี) =================
        // วิธีเปิดใช้: สมัคร firebase.google.com → สร้างโปรเจกต์ → เปิด Realtime Database
        // → คัดลอก URL มาวางแทนค่าว่างด้านล่าง (เช่น "https://xxx-default-rtdb.asia-southeast1.firebasedatabase.app")
        // ปล่อยว่าง = ใช้โหมดไฟล์ licenses.json เหมือนเดิม
        // static readonly (ห้าม const) — เพื่อให้ Obfuscar เข้ารหัสค่าได้ตอน build (const จะหลุดเป็น plain text)
        private static readonly string FirebaseUrl = "https://kingr9-f3e43-default-rtdb.asia-southeast1.firebasedatabase.app";
        private static bool ServerOn =>
            !string.IsNullOrWhiteSpace(FirebaseUrl) && FirebaseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

        private static string ServerKeyUrl(string key) =>
            FirebaseUrl.TrimEnd('/') + "/keys/" + LicenseService.Normalize(key).Replace("-", "") + ".json";

        /// <summary>พร้อมใช้โหมดออนไลน์ (มี URL + ตั้งค่าบัญชีแอดมินแล้ว)</summary>
        private bool ServerReady =>
            ServerOn && !string.IsNullOrWhiteSpace(_cfg.srvApiKey) &&
            !string.IsNullOrWhiteSpace(_cfg.srvEmail) && !string.IsNullOrWhiteSpace(_cfg.srvPass);

        /// <summary>ล็กอินด้วยบัญชีแอดมิน → ได้ token (อายุ ~1 ชม. เก็บ cache ใน config)</summary>
        private void EnsureToken()
        {
            if (!string.IsNullOrWhiteSpace(_cfg.srvToken) && DateTime.Now < _cfg.srvTokenExp.AddMinutes(-5)) return;
            string url = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" +
                Uri.EscapeDataString(_cfg.srvApiKey);
            using var resp = Http.PostAsync(url, new StringContent(
                JsonSerializer.Serialize(new { email = _cfg.srvEmail, password = _cfg.srvPass, returnSecureToken = true }),
                Encoding.UTF8, "application/json")).Result;
            if (!resp.IsSuccessStatusCode)
                throw new Exception("เข้าสู่ระบบ server ไม่สำเร็จ — ตรวจ Web API Key / อีเมล / รหัสผ่านแอดมิน");
            var j = JsonSerializer.Deserialize<JsonElement>(resp.Content.ReadAsStringAsync().Result);
            _cfg.srvToken = j.GetProperty("idToken").GetString() ?? "";
            _cfg.srvTokenExp = DateTime.Now.AddSeconds(3600);
            SaveCfg();
        }

        /// <summary>query auth สำหรับ REST (ถ้ายังไม่ตั้งค่า คืนค่าว่าง = เรียกแบบไม่มี token)</summary>
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
                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return "null";   // key ไม่มีบน server
                return resp.Content.ReadAsStringAsync().Result;
            }
            catch { return null; }   // เน็ต/เซิร์ฟเวอร์เข้าไม่ถึง
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

        /// <summary>Activate ผ่าน server — ผูก HWID ที่เครื่องแรกทันที + ตรวจอายุ/การระงับทุกครั้ง
        /// offline=true = ติดต่อ server ไม่ถึง (ไม่ใช่การถูกปฏิเสธ — ให้ใช้ข้อมูลในเครื่องแทนได้)</summary>
        private (bool ok, string msg, int days, bool offline) ServerActivate(string norm, string hw)
        {
            try
            {
                string body = HttpGet(ServerKeyUrl(norm));
                if (body == null)
                    return (false, "เชื่อมต่อ server ไม่ได้ — ตรวจอินเทอร์เน็ต", 0, true);
                if (string.IsNullOrWhiteSpace(body) || body == "null")
                    return (false, "ไม่พบ key นี้ในระบบ", 0, false);

                var j = JsonSerializer.Deserialize<JsonElement>(body);
                if (j.TryGetProperty("revoked", out var rv) && rv.ValueKind == JsonValueKind.True)
                    return (false, "key นี้ถูกระงับการใช้งาน", 0, false);
                if (!j.TryGetProperty("days", out var dEl) || dEl.ValueKind != JsonValueKind.Number)
                    return (false, "ข้อมูล key บน server ไม่สมบูรณ์", 0, false);
                int days = dEl.GetInt32();
                string hwid = j.TryGetProperty("hwid", out var hEl) && hEl.ValueKind == JsonValueKind.String ? hEl.GetString() : "";
                string created = j.TryGetProperty("created", out var cEl) && cEl.ValueKind == JsonValueKind.String
                    ? cEl.GetString() : LicenseService.TodayUtc();

                if (string.IsNullOrEmpty(hwid))
                {
                    created = LicenseService.TodayUtc();   // อายุเริ่มนับวัน Activate ของลูกค้า
                    if (!HttpPatch(ServerKeyUrl(norm), JsonSerializer.Serialize(new { hwid = hw, created, key = norm })))
                        return (false, "เชื่อมต่อ server ไม่สำเร็จ (บันทึก HWID)", 0, true);
                    LicenseService.UpsertLocal(norm, created, days);
                    return (true, "ล็อค HWID สำเร็จ", days, false);
                }
                if (hwid != hw) return (false, "key นี้ถูกผูกกับเครื่องอื่นอยู่", 0, false);

                var expiry = new LicenseService.License { created = created, days = days };
                int left = LicenseService.DaysLeft(expiry);
                if (LicenseService.IsExpired(expiry)) return (false, "key หมดอายุแล้ว — ติดต่อแอดมินเพื่อต่ออายุ", 0, false);
                LicenseService.UpsertLocal(norm, created, days);
                return (true, "key ถูกต้อง", left, false);
            }
            catch
            {
                return (false, "เชื่อมต่อ server ไม่ได้ — ตรวจอินเทอร์เน็ต", 0, true);
            }
        }

        public Bridge(MainWindow win, Logger log, List<Tweak> all)
        {
            _win = win;
            _log = log;
            _all = all;
            _cfg = LoadCfg();

            // เครื่องแอดมิน: ยกระดับ key ของเครื่องนี้เป็นรูปแบบ KINGR9 อัตโนมัติ (ทำครั้งเดียว)
            try
            {
                string nk = LicenseService.EnsureAdminKey();
                if (nk != null)
                {
                    _cfg.savedKey = nk;
                    _cfg.rememberKey = true;
                    SaveCfg();
                    _log.Ok($"เปลี่ยน key ของแอดมินเป็นรูปแบบ KINGR9: {nk}");
                }
            }
            catch { }

            _stats = new StatsService();
        }

        /// <summary>เรียกตอนปิดแอป — ล้าง timer และ stats ให้เรียบร้อย</summary>
        public void Cleanup()
        {
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

        // งานไหนนาน (netsh/bcdedit/วัดขยะ/ตรวจ server) หน้าต่างจะขึ้น "Not Responding"
        // ใหม่: bridge.js ส่ง { __kr, type, payload } มาทาง postMessage → งานหนักทั้งหมด
        // ลง background thread แล้วส่งผลกลับเป็น { __krRes, res } ให้ JS resolve ตาม id

        /// <summary>รับ rpc จากหน้าเว็บ (เรียกจาก MainWindow.WebMessageReceived — UI thread ต้องกลับทันที)</summary>
        public void OnWebMessage(string json)
        {
            JsonElement msg;
            try { msg = JsonSerializer.Deserialize<JsonElement>(json ?? ""); } catch { return; }
            if (msg.ValueKind != JsonValueKind.Object || !msg.TryGetProperty("__kr", out var idEl)) return;  // ไม่ใช่ rpc
            long id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64() : 0;
            string type = msg.TryGetProperty("type", out var tEl) && tEl.ValueKind == JsonValueKind.String ? tEl.GetString() : "";
            JsonElement payload = msg.TryGetProperty("payload", out var pEl) && pEl.ValueKind == JsonValueKind.Object ? pEl : default;

            string PS(string n) => payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(n, out var x) && x.ValueKind == JsonValueKind.String ? (x.GetString() ?? "") : "";

            // ลาก/ย่อ/ขยาย/ปิดหน้าต่าง — ทำบน UI thread ทันที (WebMessageReceived อยู่บน UI thread อยู่แล้ว)
            // ยิ่งเร็ว DragMove ยิ่งตามเมาส์ทัน — ถ้ารอ Task.Run เมาส์จะปล่อยก่อนแล้วลากไม่ติด
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
                    // งานลูกโซ่หลายขั้น → job เดียว แถบ % ไหลต่อเนื่องตลอดทั้งชุด
                    if (type == "applyChain") result = RunChainJob(payload);
                    // งาน apply ปกติ → ห่อ job (กันกดซ้อน + รายงาน %)
                    else if (IsApplyType(type)) result = RunApplyJob(ApplyLabel(type, payload), () => RunRpc(type, payload));
                    else result = RunRpc(type, payload);
                }
                catch (Exception ex) { result = J(new { ok = false, msg = "ผิดพลาด: " + ex.Message }); }
                PostRpc(id, result);
            });
        }

        /// <summary>เส้นทาง rpc (ตัวจริง ไม่ห่อ job) → เมธอดเดิมทุกตัว (รันบน background thread — เมธอดที่แตะ UI ใช้ Dispatcher ภายในอยู่แล้ว)</summary>
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
                case "clearRam":       return ClearRam();
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
                case "updatePublish":  return UpdatePublish(PS("version"), PS("url"), PS("sha256"), PS("notes"));
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

        private string ClearRam()
        {
            var result = Memory.ClearAppWorkingSets();
            _log.Ok($"คืน working set ของ {result.ProcessesTrimmed} แอป; RAM ว่าง {result.AvailableBeforeGb:F2} → {result.AvailableAfterGb:F2} GB");
            return J(new
            {
                ok = true,
                processesTrimmed = result.ProcessesTrimmed,
                availableBeforeGb = result.AvailableBeforeGb,
                availableAfterGb = result.AvailableAfterGb
            });
        }
        /// <summary>ส่งผลลัพธ์กลับหน้าเว็บ (PostWebMessageAsJson ต้องเรียกบน UI thread)</summary>
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
