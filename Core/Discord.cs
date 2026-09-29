using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KingR9Tools.Core
{
    /// <summary>
    /// แจ้งเตือนเหตุการณ์สำคัญเข้า Discord ผ่าน Webhook (fire-and-forget — ไม่บล็อกงาน)
    /// มี rate limit: ส่งเร็วสุดทุก 1.5 วิ และคิวไม่เกิน 8 ข้อความ (ส่วนเกิน drop) กัน Discord 429
    /// </summary>
    public static class Discord
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(6) };
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static DateTime _lastSend = DateTime.MinValue;
        private static int _queued;

        /// <summary>ตรวจ webhook URL — คืน URL ที่ใช้ได้ หรือ "" ถ้าไม่ใช่ webhook ของ Discord</summary>
        public static string Clean(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            url = url.Trim();
            if (!url.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://ptb.discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://canary.discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase))
                return "";
            return url;
        }

        /// <summary>ส่งแบบไม่รอผล (ใช้กับ event ปกติ) · kind: green/yellow/red/blue</summary>
        public static void Send(string webhook, string kind, string title, string desc)
        {
            string url = Clean(webhook);
            if (url.Length == 0) return;
            if (Interlocked.Increment(ref _queued) > 8) { Interlocked.Decrement(ref _queued); return; }
            _ = Task.Run(async () =>
            {
                try
                {
                    await Gate.WaitAsync();
                    try
                    {
                        double since = (DateTime.UtcNow - _lastSend).TotalMilliseconds;
                        if (since < 1500) await Task.Delay((int)(1500 - since));
                        int color = kind switch
                        {
                            "green"  => 3066993,
                            "red"    => 15158332,
                            "yellow" => 16098885,
                            _        => 3447003
                        };
                        var payload = new
                        {
                            username = "KingR9Tools",
                            embeds = new object[]
                            {
                                new
                                {
                                    title,
                                    description = desc,
                                    color,
                                    footer = new { text = Environment.MachineName + " · KingR9Tools" },
                                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                                }
                            }
                        };
                        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                        using var _ = await Http.PostAsync(url, content);
                        _lastSend = DateTime.UtcNow;
                    }
                    finally { Gate.Release(); }
                }
                catch { }
                finally { Interlocked.Decrement(ref _queued); }
            });
        }

        /// <summary>ส่งแบบรอผล (ปุ่ม Test) — คืน (สำเร็จ?, ข้อความ)</summary>
        public static (bool ok, string msg) SendNow(string webhook, string title, string desc)
        {
            string url = Clean(webhook);
            if (url.Length == 0) return (false, "URL ไม่ถูกต้อง — ต้องเป็น Webhook ของ Discord (https://discord.com/api/webhooks/...)");
            try
            {
                var payload = new { username = "KingR9Tools", content = "🧪 **" + title + "** — " + desc };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var resp = Http.PostAsync(url, content).Result;
                return resp.IsSuccessStatusCode
                    ? (true, "ส่งเข้า Discord แล้ว — เช็คช่องของคุณได้เลย ✓")
                    : (false, "Discord ปฏิเสธ (HTTP " + (int)resp.StatusCode + ") — webhook อาจถูกลบ/URL ไม่ถูกต้อง");
            }
            catch (Exception ex) { return (false, "เชื่อมต่อไม่ได้: " + ex.Message); }
        }
    }
}
