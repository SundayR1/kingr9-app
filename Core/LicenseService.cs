using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KingR9Tools.Core
{
    public static class LicenseService
    {
        public sealed class License
        {
            public string key { get; set; }
            public string hwid { get; set; } = "";     // "" = ยังไม่ผูกเครื่อง
            public string created { get; set; }
            public int days { get; set; } = 30;
            public string note { get; set; } = "";
        }

        public static string StoreDir  => Path.Combine(Sys.Docs, "KingR9Tools");
        public static string StoreFile => Path.Combine(StoreDir, "licenses.json");
        public static string ConfigFile => Path.Combine(StoreDir, "config.json");

        // ล็อกไฟล์ — คำสั่งจากหน้าเว็บตอนนี้รันขนานกันบน background thread (ไม่บล็อก UI)
        // ต้องกันอ่าน/เขียน licenses.json / config.json ชนกัน
        private static readonly object IoLock = new object();

        // ---------- ลายเซ็นกันแก้ไฟล์ licenses.json (HMAC-SHA256) ----------
        // ใครแก้ไฟล์เอง (เพิ่ม key / แก้ hwid / ต่อวันเอง) จะไม่ผ่านตรวจลายเซ็น → โหมดออฟไลน์ปฏิเสธทันที
        private static readonly string SigSecret = "KingR9::sig::v1::f3e43::7Qz2Wm9xLd";
        private static HashSet<string> _sigValid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string ComputeSig(List<License> items)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SigSecret));
            return Convert.ToHexString(hmac.ComputeHash(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(items))));
        }

        // ---------- HWID ----------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetVolumeInformationW(string root, IntPtr name, uint nameSize,
            out uint serial, out uint maxLen, out uint flags, IntPtr fs, uint fsSize);

        private static string Sha(string s)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }

        public static string Hwid()
        {
            try
            {
                GetVolumeInformationW("C:\\", IntPtr.Zero, 0, out uint serial, out _, out _, IntPtr.Zero, 0);
                string cpu = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                    "ProcessorNameString", "") as string ?? "";
                string raw = $"{Environment.MachineName}|{serial:X8}|{cpu.Trim()}";
                string hex = Sha(raw).Substring(0, 16).ToUpperInvariant();
                return string.Join("-", Enumerable.Range(0, 4).Select(i => hex.Substring(i * 4, 4)));
            }
            catch { return "0000-0000-0000-0000"; }
        }

        // ---------- ADMIN ----------
        // HWID เครื่องแอดมิน — เครื่องเดียวที่ auto-gen key ตอนแรก / เข้าหน้า Key Generator ได้
        // (ถ้าเปลี่ยน CPU หรือลง Windows ใหม่จน HWID เปลี่ยน ให้แก้ค่านี้)
        // (static readonly ห้ามเปลี่ยนเป็น const — เพื่อให้ Obfuscar เข้ารหัสค่าตอน build)
        public static readonly string AdminHwid = "75D6-791C-BE9C-D5BB";
        public static bool IsAdmin() => Hwid() == AdminHwid;

        // ---------- STORE ----------
        /// <summary>อ่าน licenses.json โดยไม่ lock — ใช้เฉพาะภายใน lock(IoLock) เท่านั้น</summary>
        private static List<License> LoadNoLock()
        {
            try
            {
                if (!File.Exists(StoreFile)) { _sigValid.Clear(); return new List<License>(); }
                var tok = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(StoreFile));
                if (tok.ValueKind == JsonValueKind.Array)
                {
                    _sigValid.Clear();
                    return tok.Deserialize<List<License>>() ?? new List<License>();
                }
                if (tok.ValueKind == JsonValueKind.Object &&
                    tok.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array)
                {
                    string sig = tok.TryGetProperty("sig", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "" : "";
                    var items = it.Deserialize<List<License>>() ?? new List<License>();
                    if (!string.Equals(sig, ComputeSig(items), StringComparison.OrdinalIgnoreCase))
                    {
                        _sigValid.Clear();
                        return new List<License>();
                    }
                    _sigValid = new HashSet<string>(items.Select(x => Normalize(x.key)), StringComparer.OrdinalIgnoreCase);
                    return items;
                }
                _sigValid.Clear();
                return new List<License>();
            }
            catch { _sigValid.Clear(); return new List<License>(); }
        }

        private static List<License> Load()
        {
            lock (IoLock) { return LoadNoLock(); }
        }

        /// <summary>เขียน licenses.json โดยไม่ lock — ใช้เฉพาะภายใน lock(IoLock) เท่านั้น</summary>
        private static void SaveNoLock(List<License> list)
        {
            Directory.CreateDirectory(StoreDir);
            File.WriteAllText(StoreFile, JsonSerializer.Serialize(new
            {
                sig = ComputeSig(list),
                items = list
            }, new JsonSerializerOptions { WriteIndented = true }));
            _sigValid = new HashSet<string>(list.Select(x => Normalize(x.key)), StringComparer.OrdinalIgnoreCase);
        }

        private static void Save(List<License> list)
        {
            lock (IoLock) { SaveNoLock(list); }
        }

        /// <summary>สร้าง key เริ่มต้นเฉพาะเครื่องแอดมินเท่านั้น — เครื่องลูกค้าห้าม auto-gen (ไม่งั้นได้ key ฟรีทุกเครื่อง)</summary>
        public static bool EnsureSeeded()
        {
            if (Load().Count > 0) return false;
            if (!IsAdmin()) return false;
            CreateAdminKey(Hwid());
            return true;
        }

        public static string Normalize(string key) =>
            string.Join("-", (key ?? "").ToUpperInvariant().Replace("-", "")
                .Where(char.IsLetterOrDigit).Take(16).Select((c, i) => new { c, i })
                .GroupBy(x => x.i / 4).Select(g => new string(g.Select(x => x.c).ToArray())));

        public static string TodayUtc() => DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DateTime ParseCreatedUtc(string value)
        {
            var date = DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
            // Older releases could write Buddhist-calendar years on Thai Windows.
            if (date.Year >= 2400 && date.Year <= 2800) date = date.AddYears(-543);
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        }

        public static bool IsExpired(License l) =>
            l == null || l.days <= 0 || DateTime.UtcNow >= ParseCreatedUtc(l.created).AddDays(l.days);

        public static int DaysLeft(License l)
        {
            if (l == null || l.days <= 0) return 0;
            var remaining = ParseCreatedUtc(l.created).AddDays(l.days) - DateTime.UtcNow;
            return remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalDays);
        }

        /// <summary>ตรวจ key → ถ้าถูกต้องและยังไม่ผูกเครื่อง จะผูกกับ HWID นี้ทันที</summary>
        public static (bool ok, string message, int daysLeft, License lic) Validate(string key)
        {
            lock (IoLock)
            {
                string norm = Normalize(key);
                if (string.IsNullOrEmpty(norm) || norm.Length != 19)
                    return (false, "รูปแบบ key ไม่ถูกต้อง (XXXX-XXXX-XXXX-XXXX)", 0, null);

                string hw = Hwid();
                var list = LoadNoLock();
                var l = list.FirstOrDefault(x => Normalize(x.key) == norm);
                if (l == null) return (false, "ไม่พบ key นี้ในระบบ", 0, null);
                if (!IsAdmin() && !_sigValid.Contains(norm))
                    return (false, "ข้อมูล key ในเครื่องไม่ถูกต้อง — เชื่อมต่ออินเทอร์เน็ตเพื่อยืนยันกับ server", 0, null);
                if (!string.IsNullOrEmpty(l.hwid) && l.hwid != hw)
                    return (false, "key นี้ถูกผูกกับเครื่องอื่นอยู่", 0, null);
                if (IsExpired(l))
                    return (false, "key หมดอายุแล้ว — ติดต่อแอดมินเพื่อต่ออายุ", 0, null);

                if (string.IsNullOrEmpty(l.hwid))
                {
                    l.hwid = hw;
                    SaveNoLock(list);
                }
                return (true, "key ถูกต้อง", DaysLeft(l), l);
            }
        }

        /// <summary>"Lost key?" — ดึง key ที่ผูกกับเครื่องนี้กลับมา</summary>
        public static License RecoverForThisMachine()
        {
            string hw = Hwid();
            return Load().FirstOrDefault(x => x.hwid == hw && !IsExpired(x));
        }

        // ---------- KEY GENERATOR ----------
        /// <summary>รายชื่อ key ทั้งหมดในระบบ</summary>
        public static List<License> AllKeys() => Load();

        /// <summary>เจน key ใหม่ตาม prefix เช่น "KING" → KING-XXXX-XXXX-XXXX — hwid ว่าง = ยังไม่ผูกเครื่อง (ล็อคเครื่องแรกที่ Activate)</summary>
        public static License CreateKey(int days, string note, string prefix)
        {
            if (days < 1) days = 30;
            prefix = new string((prefix ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).Take(12).ToArray());
            var list = Load();
            int randLen = 16 - prefix.Length;
            string key;
            do
            {
                string hex = Convert.ToHexString(RandomNumberGenerator.GetBytes((randLen + 1) / 2));
                key = prefix + hex.Substring(0, randLen);
            } while (list.Any(x => Normalize(x.key) == Normalize(key)));

            var lic = new License
            {
                key = FormatKey(key),
                hwid = "",
                created = TodayUtc(),
                days = days,
                note = (note ?? "").Trim()
            };
            list.Add(lic);
            Save(list);
            return lic;
        }

        private static string FormatKey(string raw) =>
            string.Join("-", Enumerable.Range(0, 4).Select(i => raw.Substring(i * 4, 4)));

        /// <summary>key ของแอดมิน (ขึ้นต้น KINGR9 · ผูกเครื่องตัวเอง · อายุ ~10 ปี) — ลบ key เดิมที่ผูกกับเครื่องนี้ทิ้ง</summary>
        public static string CreateAdminKey(string hw)
        {
            var list = Load();
            list.RemoveAll(x => x.hwid == hw);
            string key;
            do
            {
                string hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
                key = "KINGR9" + hex;
            } while (list.Any(x => Normalize(x.key) == Normalize(key)));
            list.Add(new License
            {
                key = FormatKey(key),
                hwid = hw,
                created = TodayUtc(),
                days = 3650,
                note = "ADMIN"
            });
            Save(list);
            return FormatKey(key);
        }

        /// <summary>เครื่องแอดมิน: ถ้า key ที่ผูกกับเครื่องยังไม่ได้ขึ้นต้น KINGR9 → เปลี่ยนให้อัตโนมัติ (คืน key ใหม่ / null = ไม่ต้องเปลี่ยน)</summary>
        public static string EnsureAdminKey()
        {
            if (!IsAdmin()) return null;
            var mine = Load().FirstOrDefault(x => x.hwid == Hwid());
            if (mine != null && mine.key.StartsWith("KING")) return null;
            return CreateAdminKey(Hwid());
        }

        /// <summary>ลบ key ออกจากระบบ (key ที่ถูกผูกไว้แล้วจะใช้ไม่ได้ทันที)</summary>
        public static bool RemoveKey(string key)
        {
            string norm = Normalize(key);
            var list = Load();
            int n = list.RemoveAll(x => Normalize(x.key) == norm);
            if (n > 0) Save(list);
            return n > 0;
        }

        /// <summary>รีเซ็ตการผูก HWID — key กลับไปเป็น "ว่าง" รอเครื่องใหม่มา Activate แทน</summary>
        public static bool ResetHwid(string key)
        {
            string norm = Normalize(key);
            var list = Load();
            var l = list.FirstOrDefault(x => Normalize(x.key) == norm);
            if (l == null) return false;
            l.hwid = "";
            Save(list);
            return true;
        }

        /// <summary>เก็บสำเนา key จาก server ไว้ในเครื่อง (ใช้ auto-login ยามออฟไลน์ / Lost key?)</summary>
        public static void UpsertLocal(string normKey, string created, int days)
        {
            var list = Load();
            string dashed = Normalize(normKey);
            var l = list.FirstOrDefault(x => Normalize(x.key) == dashed);
            if (l == null)
                list.Add(new License { key = dashed, hwid = Hwid(), created = created, days = days });
            else
            {
                l.hwid = Hwid();
                l.created = created;
                l.days = days;
            }
            Save(list);
        }

        /// <summary>รวม key จากไฟล์ที่แอดมินส่งมา — เพิ่มเฉพาะ key ที่ยังไม่มีในเครื่อง (เริ่มนับอายุวันนำเข้า และผูก HWID เครื่องนี้ตอน Activate)</summary>
        public static int MergeLicenses(IEnumerable<License> incoming)
        {
            if (incoming == null) return 0;
            var list = Load();
            int added = 0;
            foreach (var l in incoming)
            {
                if (l == null || string.IsNullOrWhiteSpace(l.key)) continue;
                string norm = Normalize(l.key);
                if (string.IsNullOrEmpty(norm) || norm.Length != 19) continue;
                if (list.Any(x => Normalize(x.key) == norm)) continue;
                list.Add(new License
                {
                    key = norm,
                    hwid = "",                                        // ให้ผูกใหม่กับเครื่องนี้ตอน Activate
                    created = TodayUtc(),    // อายุเริ่มนับวันนำเข้า
                    days = l.days > 0 ? l.days : 30,
                    note = l.note ?? ""
                });
                added++;
            }
            if (added > 0) Save(list);
            return added;
        }

        // ---------- CONFIG ----------
        public static JsonElement Config()
        {
            lock (IoLock)
            {
                try
                {
                    if (File.Exists(ConfigFile))
                        return JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(ConfigFile));
                }
                catch { }
                return JsonSerializer.Deserialize<JsonElement>("{}");
            }
        }

        public static void SaveConfig(object cfg)
        {
            lock (IoLock)
            {
                Directory.CreateDirectory(StoreDir);
                File.WriteAllText(ConfigFile, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
    }
}
