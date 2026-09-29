using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace KingR9Tools.Core
{
    public static class Sys
    {
        // ---------- PROCESS ----------
        public static string Run(string exe, string args, int timeoutMs = 60000)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };
                using var p = Process.Start(psi);
                // ★ อ่าน stdout + stderr พร้อมกันคนละ task — กัน pipe deadlock
                //   (แบบเดิม: อ่าน stdout จนจบก่อน แล้วค่อยอ่าน stderr → ถ้าลูกเขียน stderr จน pipe เต็ม
                //    ลูกจะบล็อกรอให้เราอ่าน stderr ขณะที่เราบล็อกรอ stdout จบ → ค้างตลอดกาล
                //    และบรรทัด timeout ไม่มีวันถูกเรียก — เจอกับสคริปต์ที่ยิง error ลง stderr เยอะ เช่น ชุด NIC)
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(true); } catch { }          // ฆ่าทั้ง process tree (cmd + ลูกทุกตัว)
                    try { p.WaitForExit(5000); } catch { }
                }
                string o = "", e = "";
                try { if (outTask.Wait(10000)) o = outTask.Result ?? ""; } catch { }
                try { if (errTask.Wait(10000)) e = errTask.Result ?? ""; } catch { }
                return o + e;
            }
            catch (Exception ex) { return "ERR:" + ex.Message; }
        }

        /// <summary>รันโปรแกรมแบบอ่าน stdout ทีละบรรทัดสด ๆ (ส่งต่อให้ onLine ทันทีที่พิมพ์ออกมา)
        /// ใช้กับสคริปต์ที่รายงานความคืบหน้าระหว่างทำงาน — stderr อ่านข้างเบียร์กัน pipe deadlock
        /// เกิน timeoutMs watchdog จะ Kill(true) ทั้ง process tree ให้เอง (งานไม่มีวันค้างนิรันดร์)</summary>
        public static void RunStream(string exe, string args, int timeoutMs, Action<string> onLine, params (string key, string val)[] env)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };
                foreach (var (key, val) in env) psi.Environment[key] = val;
                using var p = Process.Start(psi);
                var errTask = p.StandardError.ReadToEndAsync();
                // watchdog: หมดเวลา → ฆ่าทั้งทรี stdout ปิด → ReadLine ได้ null → จบเอง (แม้สคริปต์เงียบสนิท)
                using var watchdog = new System.Threading.Timer(
                    _ => { try { p.Kill(true); } catch { } }, null, timeoutMs, System.Threading.Timeout.Infinite);
                string ln;
                while ((ln = p.StandardOutput.ReadLine()) != null)
                {
                    try { onLine(ln); } catch { }
                }
                if (!p.WaitForExit(10000)) { try { p.Kill(true); } catch { } }
                try { if (errTask.Wait(5000)) { _ = errTask.Result; } } catch { }
            }
            catch
            {
                // เปิด process ไม่ได้ — ให้ผู้เรียกจัดการต่อเอง (onLine จะไม่ถูกเรียกอีก)
            }
        }

        public static string Cmd(string command) => Run("cmd.exe", "/c " + command);
        public static string Netsh(string args) => Run("netsh.exe", args);
        public static string PS(string script) =>
            Run("powershell.exe", "-NoProfile -ExecutionPolicy RemoteSigned -Command \"" + script.Replace("\"", "\\\"") + "\"");

        // ---------- REGISTRY ----------
        private static RegistryKey RootOf(ref string path)
        {
            int i = path.IndexOf('\\');
            string root = i < 0 ? path : path.Substring(0, i);
            path = i < 0 ? "" : path.Substring(i + 1);
            return root.ToUpperInvariant() switch
            {
                "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
                "HKCU" or "HKEY_CURRENT_USER"  => Registry.CurrentUser,
                "HKCR" or "HKEY_CLASSES_ROOT"  => Registry.ClassesRoot,
                "HKU"  or "HKEY_USERS"         => Registry.Users,
                _ => Registry.LocalMachine
            };
        }

        public static bool SetDword(string fullPath, string name, long value)
        {
            try
            {
                string p = fullPath; var root = RootOf(ref p);
                using var k = root.CreateSubKey(p, true);
                k.SetValue(name, unchecked((int)value), RegistryValueKind.DWord);
                return true;
            }
            catch { return false; }
        }

        public static bool SetString(string fullPath, string name, string value)
        {
            try
            {
                string p = fullPath; var root = RootOf(ref p);
                using var k = root.CreateSubKey(p, true);
                k.SetValue(name, value, RegistryValueKind.String);
                return true;
            }
            catch { return false; }
        }

        public static void DelValue(string fullPath, string name)
        {
            try
            {
                string p = fullPath; var root = RootOf(ref p);
                using var k = root.OpenSubKey(p, true);
                k?.DeleteValue(name, false);
            }
            catch { }
        }

        public static void DelKey(string fullPath)
        {
            try
            {
                string p = fullPath; var root = RootOf(ref p);
                root.DeleteSubKeyTree(p, false);
            }
            catch { }
        }

        public static List<string> SubKeys(string fullPath)
        {
            var list = new List<string>();
            try
            {
                string p = fullPath; var root = RootOf(ref p);
                using var k = root.OpenSubKey(p);
                if (k != null) foreach (var s in k.GetSubKeyNames()) list.Add(fullPath + "\\" + s);
            }
            catch { }
            return list;
        }

        public static string GetString(string fullPath, string name)
        {
            try
            {
                string p = fullPath; var root = RootOf(ref p);
                using var k = root.OpenSubKey(p);
                return k?.GetValue(name)?.ToString();
            }
            catch { return null; }
        }

        // ---------- NETWORK ----------
        public const string NicClassRoot =
            @"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002bE10318}";

        public static List<NetworkInterface> ActiveNics() =>
            NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();

        public static List<string> NicClassKeys()
        {
            var ids = new HashSet<string>(ActiveNics().Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var sub in SubKeys(NicClassRoot))
            {
                string leaf = sub.Substring(sub.LastIndexOf('\\') + 1);
                if (leaf.Length != 4 || !int.TryParse(leaf, out _)) continue;
                string guid = GetString(sub, "NetCfgInstanceId");
                if (!string.IsNullOrEmpty(guid) && ids.Contains(guid)) result.Add(sub);
            }
            if (result.Count == 0)
                foreach (var sub in SubKeys(NicClassRoot))
                {
                    string leaf = sub.Substring(sub.LastIndexOf('\\') + 1);
                    if (leaf.Length == 4 && int.TryParse(leaf, out _)) result.Add(sub);
                }
            return result;
        }

        // ---------- FS ----------
        public static double DeleteContents(string dir)
        {
            double mb = 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    try { mb += new FileInfo(f).Length / 1048576.0; } catch { }
                foreach (var f in Directory.EnumerateFiles(dir)) try { File.Delete(f); } catch { }
                foreach (var d in Directory.EnumerateDirectories(dir)) try { Directory.Delete(d, true); } catch { }
            }
            catch { }
            return mb;
        }

        public static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public static string Docs  => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static string Win   => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        public static string Temp  => Path.GetTempPath();
    }
}