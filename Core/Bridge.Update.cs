using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace KingR9Tools.Core
{
    public partial class Bridge
    {
        private void Notify(string kind, string title, string desc) => Discord.Send(_cfg.discordWebhook, kind, title, desc);

        public string DiscordGet()
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            return J(new { ok = true, url = _cfg.discordWebhook ?? "" });
        }

        public string DiscordSave(string url)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            string raw = (url ?? "").Trim();
            string cleaned = Discord.Clean(raw);
            // ถ้ากรอก URL มา แต่ไม่ใช่ Discord Webhook → คืน error
            if (raw.Length > 0 && cleaned.Length == 0)
                return J(new { ok = false, msg = "URL ไม่ถูกต้อง — ต้องเป็น Webhook ของ Discord (https://discord.com/api/webhooks/...)" });
            _cfg.discordWebhook = cleaned;
            SaveCfg();
            _log.Ok(cleaned.Length == 0 ? "ปิดการแจ้งเตือน Discord แล้ว" : "บันทึก Discord Webhook แล้ว");
            return J(new { ok = true, msg = cleaned.Length == 0 ? "ปิดการแจ้งเตือน Discord แล้ว" : "บันทึก Webhook แล้ว ✓" });
        }

        public string DiscordTest()
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            var (ok, msg) = Discord.SendNow(_cfg.discordWebhook, "KingR9Tools พร้อมแจ้งเตือน", "ทดสอบ Webhook จากหน้า Key Generator");
            _log.Ok("Discord test: " + msg);
            return J(new { ok, msg });
        }

        // ---------- AUTO UPDATE (ตรวจเวอร์ชันจาก server → โหลด → สลับไฟล์ → รีสตาร์ทเอง) ----------
        public static string AppVersion = "1.0.2";

        private static bool VersionNewer(string remote, string local)
        {
            try
            {
                var r = (remote ?? "").Split('.');
                var l = (local ?? "").Split('.');
                for (int i = 0; i < Math.Max(r.Length, l.Length); i++)
                {
                    int ri = i < r.Length && int.TryParse(r[i], out int a) ? a : 0;
                    int li = i < l.Length && int.TryParse(l[i], out int b) ? b : 0;
                    if (ri != li) return ri > li;
                }
            }
            catch { }
            return false;
        }

        private static bool IsHttpsUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
            !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);

        private static bool IsSha256(string value) => value != null && value.Length == 64 && value.All(Uri.IsHexDigit);

        private (string ver, string url, string notes, string sha256) UpdateManifest()
        {
            string body = HttpGet(FirebaseUrl.TrimEnd('/') + "/appUpdate.json" + AuthQuery());
            if (string.IsNullOrWhiteSpace(body) || body == "null") return ("", "", "", "");
            var j = JsonSerializer.Deserialize<JsonElement>(body);
            string ver = j.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            string url = j.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? "" : "";
            string notes = j.TryGetProperty("notes", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            string sha256 = j.TryGetProperty("sha256", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() ?? "" : "";
            return (ver, url, notes, sha256);
        }

        public string UpdateCheck()
        {
            Hello();
            try
            {
                if (!ServerReady) return J(new { ok = true, available = false, local = AppVersion });
                var (ver, url, notes, sha256) = UpdateManifest();
                bool avail = VersionNewer(ver, AppVersion) && IsHttpsUrl(url) && IsSha256(sha256);
                return J(new { ok = true, available = avail, version = ver, notes = notes, url = url, local = AppVersion });
            }
            catch { return J(new { ok = true, available = false, local = AppVersion }); }
        }

        // __UPD2__

        public string UpdateDownload()
        {
            Hello();
            try
            {
                if (!ServerReady) return J(new { ok = false, msg = "ยังไม่ได้เชื่อม server" });
                var (ver, url, _, sha256) = UpdateManifest();
                if (!VersionNewer(ver, AppVersion) || !IsHttpsUrl(url) || !IsSha256(sha256))
                    return J(new { ok = false, msg = "คุณใช้เวอร์ชันล่าสุดอยู่แล้ว ✓" });

                if (!JobBegin("อัปเดตแอป → v" + ver))
                    return J(new { ok = false, msg = "มีงานกำลังทำงานอยู่ — รอจบก่อนแล้วกดใหม่" });
                var creep = Task.Run(CreepLoop);
                try
                {
                    string exePath = "";
                    try { exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? ""; } catch { }
                    if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                        exePath = Path.Combine(AppContext.BaseDirectory, "KingR9Tools.exe");
                    string dir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
                    string newPath = Path.Combine(dir, "KingR9Tools_update.exe");
                    string oldPath = exePath;
                    // __UPD3__
                    // ดาวน์โหลดพร้อม % จริงจาก ContentLength
                    using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
                    using (var resp = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                    {
                        if (resp.RequestMessage?.RequestUri?.Scheme != Uri.UriSchemeHttps)
                        {
                            JobEnd(false, "ลิงก์ดาวน์โหลดเปลี่ยนออกจาก HTTPS");
                            return J(new { ok = false, msg = "ลิงก์ดาวน์โหลดไม่ปลอดภัย — ต้องใช้ HTTPS ตลอดการดาวน์โหลด" });
                        }
                        if (!resp.IsSuccessStatusCode)
                        {
                            JobEnd(false, "โหลดไฟล์ไม่สำเร็จ (HTTP " + (int)resp.StatusCode + ")");
                            return J(new { ok = false, msg = "โหลดไฟล์ไม่สำเร็จ (HTTP " + (int)resp.StatusCode + ")" });
                        }
                        long total = resp.Content.Headers.ContentLength ?? -1;
                        JobStep(0, "กำลังโหลด v" + ver + " ...");
                        using var src = resp.Content.ReadAsStreamAsync().Result;
                        using var dst = new FileStream(newPath, FileMode.Create, FileAccess.Write);
                        var buf = new byte[81920];
                        long done = 0, lastUi = 0;
                        int n;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        while ((n = src.Read(buf, 0, buf.Length)) > 0)
                        {
                            dst.Write(buf, 0, n);
                            done += n;
                            if (total > 0 && sw.ElapsedMilliseconds - lastUi > 300)
                            {
                                lastUi = sw.ElapsedMilliseconds;
                                JobStep((int)(done * 100 / total), "โหลด v" + ver + " · " + (done / 1048576.0).ToString("0.0") + "/" + (total / 1048576.0).ToString("0.0") + " MB");
                            }
                        }
                        if (total > 0) JobStep(100, "โหลดเสร็จ — กำลังสลับไฟล์...");
                    }
                    if (new FileInfo(newPath).Length < 1024 * 1024)
                    {
                        try { File.Delete(newPath); } catch { }
                        JobEnd(false, "ไฟล์ที่โหลดมาไม่สมบูรณ์");
                        return J(new { ok = false, msg = "ไฟล์ที่โหลดมาไม่สมบูรณ์ — ลองใหม่อีกครั้ง" });
                    }
                    // อัปเดตแบบ clean (ไม่ trigger AV): rename exe เก่า → move ตัวใหม่เข้าแทน → เปิดตัวใหม่
                    string actualSha256;
                    using (var sha = SHA256.Create())
                    using (var file = File.OpenRead(newPath))
                        actualSha256 = Convert.ToHexString(sha.ComputeHash(file));
                    if (!actualSha256.Equals(sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(newPath); } catch { }
                        JobEnd(false, "SHA-256 ของไฟล์อัปเดตไม่ตรงกับที่ประกาศไว้");
                        return J(new { ok = false, msg = "ไฟล์อัปเดตตรวจสอบไม่ผ่าน — ยกเลิกการติดตั้งเพื่อความปลอดภัย" });
                    }
                    string bakPath = oldPath + ".old";
                    try { if (File.Exists(bakPath)) File.Delete(bakPath); } catch { }
                    File.Move(oldPath, bakPath);
                    try { File.Move(newPath, oldPath); }
                    catch
                    {
                        if (!File.Exists(oldPath) && File.Exists(bakPath)) File.Move(bakPath, oldPath);
                        throw;
                    }

                    // เปิดตัวใหม่ พร้อม arg ให้ลบ .old ตอนเปิด
                    var psi = new System.Diagnostics.ProcessStartInfo(oldPath, "--cleanup")
                    { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);

                    _log.Ok("อัปเดตแอป v" + AppVersion + " → v" + ver + " — โหลดเสร็จ กำลังรีสตาร์ทแอป");
                    Notify("green", "⬆️ แอปอัปเดต", "v" + AppVersion + " → **v" + ver + "** — รีสตาร์ทแอปเรียบร้อย");
                    JobEnd(true, "อัปเดตสำเร็จ — แอปจะรีสตาร์ทเองในไม่ช้า");

#pragma warning disable CS4014
                    Task.Run(async () =>
                    {
                        await Task.Delay(1500);
                        try { _win.Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown())); } catch { }
                    });
#pragma warning restore CS4014
                    return J(new { ok = true, msg = "อัปเดตสำเร็จ — แอปจะรีสตาร์ทเอง (v" + ver + ")" });
                }
                catch
                {
                    JobEnd(false, "อัปเดตล้มเหลว");
                    throw;
                }
                finally { try { creep.Wait(300); } catch { } }
            }
            catch (Exception ex)
            {
                _log.Err("update: " + ex.Message);
                return J(new { ok = false, msg = "ผิดพลาด: " + ex.Message });
            }
        }

        public string UpdatePublish(string version, string url, string sha256, string notes)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "หน้านี้สำหรับแอดมินเท่านั้น" });
            if (!ServerReady) return J(new { ok = false, msg = "ยังไม่ได้ตั้งค่า server — กดตั้งค่าในหน้าเจน key ก่อน" });
            version = (version ?? "").Trim();
            url = (url ?? "").Trim();
            sha256 = (sha256 ?? "").Trim();
            notes = (notes ?? "").Trim();
            if (version.Length == 0 || !IsHttpsUrl(url) || !IsSha256(sha256))
                return J(new { ok = false, msg = "กรอก version (เช่น 1.0.2) และ URL ไฟล์ exe ให้ถูกต้อง" });
            bool up = HttpPut(FirebaseUrl.TrimEnd('/') + "/appUpdate.json" + AuthQuery(),
                JsonSerializer.Serialize(new { version, url, sha256 = sha256.ToUpperInvariant(), notes }));
            _log.Ok(up ? "เผยแพร่อัปเดต v" + version + " แล้ว" : "เผยแพร่อัปเดตล้มเหลว");
            return J(new { ok = up, msg = up
                ? "เผยแพร่ v" + version + " แล้ว ✓ — ลูกค้าจะเห็น banner อัปเดตตอนเปิดแอป"
                : "เผยแพร่ไม่สำเร็จ — ตรวจอินเทอร์เน็ต/การตั้งค่า server" });
        }

        // ---------- ARES ONE-CLICK (รวมจากโปรเจกต์ Ares Store / JX Setting — optimizer.ps1 ~50 ขั้นแบบ MAX) ----------
        // รันเป็น job เดียว แถบ % จริงมาจาก event ของสคริปต์เอง (JX|{t:'step',pct,idx,total,msg})
        // ระหว่างโหลด Windows Update จะโชว์ MB/s สด ๆ จบแล้วสรุป สำเร็จ/ล้มเหลว/ข้าม + แนะนำรีสตาร์ท
    }
}
