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
        private void Notify(string kind, string title, string desc) => Discord.Send(_cfg.discordWebhook, kind, title, desc);

        public string DiscordGet()
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            return J(new { ok = true, url = _cfg.discordWebhook ?? "" });
        }

        public string DiscordSave(string url)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            string raw = (url ?? "").Trim();
            string cleaned = Discord.Clean(raw);
            // à¸–à¹‰à¸²à¸à¸£à¸­à¸ URL à¸¡à¸² à¹à¸•à¹ˆà¹„à¸¡à¹ˆà¹ƒà¸Šà¹ˆ Discord Webhook â†’ à¸„à¸·à¸™ error
            if (raw.Length > 0 && cleaned.Length == 0)
                return J(new { ok = false, msg = "URL à¹„à¸¡à¹ˆà¸–à¸¹à¸à¸•à¹‰à¸­à¸‡ â€” à¸•à¹‰à¸­à¸‡à¹€à¸›à¹‡à¸™ Webhook à¸‚à¸­à¸‡ Discord (https://discord.com/api/webhooks/...)" });
            _cfg.discordWebhook = cleaned;
            SaveCfg();
            _log.Ok(cleaned.Length == 0 ? "à¸›à¸´à¸”à¸à¸²à¸£à¹à¸ˆà¹‰à¸‡à¹€à¸•à¸·à¸­à¸™ Discord à¹à¸¥à¹‰à¸§" : "à¸šà¸±à¸™à¸—à¸¶à¸ Discord Webhook à¹à¸¥à¹‰à¸§");
            return J(new { ok = true, msg = cleaned.Length == 0 ? "à¸›à¸´à¸”à¸à¸²à¸£à¹à¸ˆà¹‰à¸‡à¹€à¸•à¸·à¸­à¸™ Discord à¹à¸¥à¹‰à¸§" : "à¸šà¸±à¸™à¸—à¸¶à¸ Webhook à¹à¸¥à¹‰à¸§ âœ“" });
        }

        public string DiscordTest()
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            var (ok, msg) = Discord.SendNow(_cfg.discordWebhook, "KingR9Tools à¸žà¸£à¹‰à¸­à¸¡à¹à¸ˆà¹‰à¸‡à¹€à¸•à¸·à¸­à¸™", "à¸—à¸”à¸ªà¸­à¸š Webhook à¸ˆà¸²à¸à¸«à¸™à¹‰à¸² Key Generator");
            _log.Ok("Discord test: " + msg);
            return J(new { ok, msg });
        }

        // ---------- AUTO UPDATE (à¸•à¸£à¸§à¸ˆà¹€à¸§à¸­à¸£à¹Œà¸Šà¸±à¸™à¸ˆà¸²à¸ server â†’ à¹‚à¸«à¸¥à¸” â†’ à¸ªà¸¥à¸±à¸šà¹„à¸Ÿà¸¥à¹Œ â†’ à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸­à¸‡) ----------
        public static string AppVersion = "1.0.1";

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

        private (string ver, string url, string notes) UpdateManifest()
        {
            string body = HttpGet(FirebaseUrl.TrimEnd('/') + "/appUpdate.json" + AuthQuery());
            if (string.IsNullOrWhiteSpace(body) || body == "null") return ("", "", "");
            var j = JsonSerializer.Deserialize<JsonElement>(body);
            string ver = j.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
            string url = j.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() ?? "" : "";
            string notes = j.TryGetProperty("notes", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            return (ver, url, notes);
        }

        public string UpdateCheck()
        {
            Hello();
            try
            {
                if (!ServerReady) return J(new { ok = true, available = false, local = AppVersion });
                var (ver, url, notes) = UpdateManifest();
                bool avail = VersionNewer(ver, AppVersion) && url.StartsWith("http", StringComparison.OrdinalIgnoreCase);
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
                if (!ServerReady) return J(new { ok = false, msg = "à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¹€à¸Šà¸·à¹ˆà¸­à¸¡ server" });
                var (ver, url, _) = UpdateManifest();
                if (!VersionNewer(ver, AppVersion) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    return J(new { ok = false, msg = "à¸„à¸¸à¸“à¹ƒà¸Šà¹‰à¹€à¸§à¸­à¸£à¹Œà¸Šà¸±à¸™à¸¥à¹ˆà¸²à¸ªà¸¸à¸”à¸­à¸¢à¸¹à¹ˆà¹à¸¥à¹‰à¸§ âœ“" });

                if (!JobBegin("à¸­à¸±à¸›à¹€à¸”à¸•à¹à¸­à¸› â†’ v" + ver))
                    return J(new { ok = false, msg = "à¸¡à¸µà¸‡à¸²à¸™à¸à¸³à¸¥à¸±à¸‡à¸—à¸³à¸‡à¸²à¸™à¸­à¸¢à¸¹à¹ˆ â€” à¸£à¸­à¸ˆà¸šà¸à¹ˆà¸­à¸™à¹à¸¥à¹‰à¸§à¸à¸”à¹ƒà¸«à¸¡à¹ˆ" });
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
                    // à¸”à¸²à¸§à¸™à¹Œà¹‚à¸«à¸¥à¸”à¸žà¸£à¹‰à¸­à¸¡ % à¸ˆà¸£à¸´à¸‡à¸ˆà¸²à¸ ContentLength
                    using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
                    using (var resp = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
                    {
                        if (!resp.IsSuccessStatusCode)
                        {
                            JobEnd(false, "à¹‚à¸«à¸¥à¸”à¹„à¸Ÿà¸¥à¹Œà¹„à¸¡à¹ˆà¸ªà¸³à¹€à¸£à¹‡à¸ˆ (HTTP " + (int)resp.StatusCode + ")");
                            return J(new { ok = false, msg = "à¹‚à¸«à¸¥à¸”à¹„à¸Ÿà¸¥à¹Œà¹„à¸¡à¹ˆà¸ªà¸³à¹€à¸£à¹‡à¸ˆ (HTTP " + (int)resp.StatusCode + ")" });
                        }
                        long total = resp.Content.Headers.ContentLength ?? -1;
                        JobStep(0, "à¸à¸³à¸¥à¸±à¸‡à¹‚à¸«à¸¥à¸” v" + ver + " ...");
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
                                JobStep((int)(done * 100 / total), "à¹‚à¸«à¸¥à¸” v" + ver + " Â· " + (done / 1048576.0).ToString("0.0") + "/" + (total / 1048576.0).ToString("0.0") + " MB");
                            }
                        }
                        if (total > 0) JobStep(100, "à¹‚à¸«à¸¥à¸”à¹€à¸ªà¸£à¹‡à¸ˆ â€” à¸à¸³à¸¥à¸±à¸‡à¸ªà¸¥à¸±à¸šà¹„à¸Ÿà¸¥à¹Œ...");
                    }
                    if (new FileInfo(newPath).Length < 1024 * 1024)
                    {
                        try { File.Delete(newPath); } catch { }
                        JobEnd(false, "à¹„à¸Ÿà¸¥à¹Œà¸—à¸µà¹ˆà¹‚à¸«à¸¥à¸”à¸¡à¸²à¹„à¸¡à¹ˆà¸ªà¸¡à¸šà¸¹à¸£à¸“à¹Œ");
                        return J(new { ok = false, msg = "à¹„à¸Ÿà¸¥à¹Œà¸—à¸µà¹ˆà¹‚à¸«à¸¥à¸”à¸¡à¸²à¹„à¸¡à¹ˆà¸ªà¸¡à¸šà¸¹à¸£à¸“à¹Œ â€” à¸¥à¸­à¸‡à¹ƒà¸«à¸¡à¹ˆà¸­à¸µà¸à¸„à¸£à¸±à¹‰à¸‡" });
                    }
                    // อัปเดตแบบ clean (ไม่ trigger AV): rename exe เก่า → move ตัวใหม่เข้าแทน → เปิดตัวใหม่
                    string bakPath = oldPath + ".old";
                    try { if (File.Exists(bakPath)) File.Delete(bakPath); } catch { }
                    try { File.Move(oldPath, bakPath); } catch { }
                    File.Move(newPath, oldPath);

                    // เปิดตัวใหม่ พร้อม arg ให้ลบ .old ตอนเปิด
                    var psi = new System.Diagnostics.ProcessStartInfo(oldPath, "--cleanup")
                    { UseShellExecute = true };
                    System.Diagnostics.Process.Start(psi);

                    _log.Ok("à¸­à¸±à¸›à¹€à¸”à¸•à¹à¸­à¸› v" + AppVersion + " â†’ v" + ver + " â€” à¹‚à¸«à¸¥à¸”à¹€à¸ªà¸£à¹‡à¸ˆ à¸à¸³à¸¥à¸±à¸‡à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹à¸­à¸›");
                    Notify("green", "â¬†ï¸ à¹à¸­à¸›à¸­à¸±à¸›à¹€à¸”à¸•", "v" + AppVersion + " â†’ **v" + ver + "** â€” à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹à¸­à¸›à¹€à¸£à¸µà¸¢à¸šà¸£à¹‰à¸­à¸¢");
                    JobEnd(true, "à¸­à¸±à¸›à¹€à¸”à¸•à¸ªà¸³à¹€à¸£à¹‡à¸ˆ â€” à¹à¸­à¸›à¸ˆà¸°à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸­à¸‡à¹ƒà¸™à¹„à¸¡à¹ˆà¸Šà¹‰à¸²");

#pragma warning disable CS4014
                    Task.Run(async () =>
                    {
                        await Task.Delay(1500);
                        try { _win.Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown())); } catch { }
                    });
#pragma warning restore CS4014
                    return J(new { ok = true, msg = "à¸­à¸±à¸›à¹€à¸”à¸•à¸ªà¸³à¹€à¸£à¹‡à¸ˆ â€” à¹à¸­à¸›à¸ˆà¸°à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—à¹€à¸­à¸‡ (v" + ver + ")" });
                }
                catch
                {
                    JobEnd(false, "à¸­à¸±à¸›à¹€à¸”à¸•à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§");
                    throw;
                }
                finally { try { creep.Wait(300); } catch { } }
            }
            catch (Exception ex)
            {
                _log.Err("update: " + ex.Message);
                return J(new { ok = false, msg = "à¸œà¸´à¸”à¸žà¸¥à¸²à¸”: " + ex.Message });
            }
        }

        public string UpdatePublish(string version, string url, string notes)
        {
            Hello();
            if (!IsAdmin()) return J(new { ok = false, msg = "à¸«à¸™à¹‰à¸²à¸™à¸µà¹‰à¸ªà¸³à¸«à¸£à¸±à¸šà¹à¸­à¸”à¸¡à¸´à¸™à¹€à¸—à¹ˆà¸²à¸™à¸±à¹‰à¸™" });
            if (!ServerReady) return J(new { ok = false, msg = "à¸¢à¸±à¸‡à¹„à¸¡à¹ˆà¹„à¸”à¹‰à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server â€” à¸à¸”à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸²à¹ƒà¸™à¸«à¸™à¹‰à¸²à¹€à¸ˆà¸™ key à¸à¹ˆà¸­à¸™" });
            version = (version ?? "").Trim();
            url = (url ?? "").Trim();
            notes = (notes ?? "").Trim();
            if (version.Length == 0 || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return J(new { ok = false, msg = "à¸à¸£à¸­à¸ version (à¹€à¸Šà¹ˆà¸™ 1.0.1) à¹à¸¥à¸° URL à¹„à¸Ÿà¸¥à¹Œ exe à¹ƒà¸«à¹‰à¸–à¸¹à¸à¸•à¹‰à¸­à¸‡" });
            bool up = HttpPut(FirebaseUrl.TrimEnd('/') + "/appUpdate.json" + AuthQuery(),
                JsonSerializer.Serialize(new { version, url, notes }));
            _log.Ok(up ? "à¹€à¸œà¸¢à¹à¸žà¸£à¹ˆà¸­à¸±à¸›à¹€à¸”à¸• v" + version + " à¹à¸¥à¹‰à¸§" : "à¹€à¸œà¸¢à¹à¸žà¸£à¹ˆà¸­à¸±à¸›à¹€à¸”à¸•à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§");
            return J(new { ok = up, msg = up
                ? "à¹€à¸œà¸¢à¹à¸žà¸£à¹ˆ v" + version + " à¹à¸¥à¹‰à¸§ âœ“ â€” à¸¥à¸¹à¸à¸„à¹‰à¸²à¸ˆà¸°à¹€à¸«à¹‡à¸™ banner à¸­à¸±à¸›à¹€à¸”à¸•à¸•à¸­à¸™à¹€à¸›à¸´à¸”à¹à¸­à¸›"
                : "à¹€à¸œà¸¢à¹à¸žà¸£à¹ˆà¹„à¸¡à¹ˆà¸ªà¸³à¹€à¸£à¹‡à¸ˆ â€” à¸•à¸£à¸§à¸ˆà¸­à¸´à¸™à¹€à¸—à¸­à¸£à¹Œà¹€à¸™à¹‡à¸•/à¸à¸²à¸£à¸•à¸±à¹‰à¸‡à¸„à¹ˆà¸² server" });
        }

        // ---------- ARES ONE-CLICK (à¸£à¸§à¸¡à¸ˆà¸²à¸à¹‚à¸›à¸£à¹€à¸ˆà¸à¸•à¹Œ Ares Store / JX Setting â€” optimizer.ps1 ~50 à¸‚à¸±à¹‰à¸™à¹à¸šà¸š MAX) ----------
        // à¸£à¸±à¸™à¹€à¸›à¹‡à¸™ job à¹€à¸”à¸µà¸¢à¸§ à¹à¸–à¸š % à¸ˆà¸£à¸´à¸‡à¸¡à¸²à¸ˆà¸²à¸ event à¸‚à¸­à¸‡à¸ªà¸„à¸£à¸´à¸›à¸•à¹Œà¹€à¸­à¸‡ (JX|{t:'step',pct,idx,total,msg})
        // à¸£à¸°à¸«à¸§à¹ˆà¸²à¸‡à¹‚à¸«à¸¥à¸” Windows Update à¸ˆà¸°à¹‚à¸Šà¸§à¹Œ MB/s à¸ªà¸” à¹† à¸ˆà¸šà¹à¸¥à¹‰à¸§à¸ªà¸£à¸¸à¸› à¸ªà¸³à¹€à¸£à¹‡à¸ˆ/à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§/à¸‚à¹‰à¸²à¸¡ + à¹à¸™à¸°à¸™à¸³à¸£à¸µà¸ªà¸•à¸²à¸£à¹Œà¸—
    }
}