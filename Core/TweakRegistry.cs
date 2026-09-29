using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KingR9Tools.Core
{
    /// <summary>
    /// ทะเบียน tweak ชุดใหม่ — ยึดสคริปต์ R9 Settings (แกะเป็น .ps1) เป็นตัวจริง
    /// ทุก tweak ปรับค่า = รันสคริปต์ในโฟลเดอร์ Scripts\ ของแอป
    /// </summary>
    public static class TweakRegistry
    {
        public const string CAT_NET   = "Network Core";
        public const string CAT_NIC   = "NIC Adapter";
        public const string CAT_POWER = "Power & BCD";
        public const string CAT_FIVEM = "FiveM / GTA5";
        public const string CAT_SVC   = "Services";

        public static readonly Dictionary<string, (string icon, string sub)> CatMeta = new()
        {
            [CAT_NET]   = ("🌐", "Netsh · MTU · Nagle · QoS · DNS"),
            [CAT_NIC]   = ("🔌", "Adapter Optimization · Interface Power"),
            [CAT_POWER] = ("⚡", "BCD · USB · Hibernate · R9 Plan"),
            [CAT_FIVEM] = ("🎮", "Priority · Cache · STR 0.5ms · Game Config"),
            [CAT_SVC]   = ("🛠", "ปิด 20 services ที่ไม่จำเป็น"),
        };

        /// <summary>พาธโฟลเดอร์สคริปต์ (แตกจากใน .exe → %LocalAppData%\KingR9Tools\web\Scripts)</summary>
        public static string ScriptsDir
        {
            get
            {
                string root = string.IsNullOrEmpty(WebAssets.Root) ? AppContext.BaseDirectory : WebAssets.Root;
                return Path.Combine(root, "Scripts");
            }
        }

        private static string ScriptPath(string file) => Path.Combine(ScriptsDir, file);

        /// <summary>รันสคริปต์ .ps1 พร้อม stream ผลลัพธ์เข้า log</summary>
        private static void RunScript(Logger log, string file, string args = "")
        {
            string path = ScriptPath(file);
            if (!File.Exists(path))
            {
                log.Err($"ไม่พบสคริปต์: {file}");
                return;
            }

            log.Info($"รัน: {file} {args}".TrimEnd());
            string cmd = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " +
                         $"& '{path}' {args}".TrimEnd();
            string outp = Sys.Run("powershell.exe",
                $"-NoProfile -ExecutionPolicy RemoteSigned -Command \"{cmd.Replace("\"", "\\\"")}\"", 600000);   // 10 นาที — งาน junk/defaults อาจช้ามาก

            foreach (var raw in (outp ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r').Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("✗") || line.ToLowerInvariant().Contains("error") ||
                    line.ToLowerInvariant().Contains("ไม่พบ") || line.StartsWith("ERR:"))
                    log.Warn("   " + line);
                else
                    log.Info("   " + line);
            }
        }

        /// <summary>รันเครื่องมือใน 12_System_Tools.ps1</summary>
        public static void SystemTool(Logger log, string action) =>
            RunScript(log, "12_System_Tools.ps1", "-Action " + action);

        /// <summary>รันไฟล์สคริปต์ในโฟลเดอร์ Scripts โดยตรง (เรียกจาก Bridge ได้)</summary>
        public static void RunFile(Logger log, string file, string args = "") =>
            RunScript(log, file, args);

        /// <summary>รันไฟล์ .bat (ชุด R9 internet) ผ่าน cmd — stdin ต่อ NUL กัน prompt ค้าง</summary>
        public static void RunBat(Logger log, string file, string args = "")
        {
            string path = ScriptPath(file);
            if (!File.Exists(path))
            {
                log.Err("ไม่พบสคริปต์: " + file);
                return;
            }

            log.Info("รัน: " + file + " " + args);
            string outp = Sys.Run("cmd.exe",
                $"/c \"\"{path}\" {args} < NUL\"".TrimEnd(), 180000);   // 3 นาที — พอสำหรับ .bat ก้อนใหญ่ (timeout จริง ฆ่าทั้ง tree)

            foreach (var raw in (outp ?? "").Split('\n'))
            {
                var line = raw.TrimEnd('\r').Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("✗") || line.ToLowerInvariant().Contains("error") ||
                    line.ToLowerInvariant().Contains("access is denied") || line.StartsWith("ERR:"))
                    log.Warn("   " + line);
                else
                    log.Info("   " + line);
            }
        }

        public static List<Tweak> Build()
        {
            var L = new List<Tweak>();

            void Add(string id, string cat, string name, string desc, bool rec,
                     Action<Logger> apply, Action<Logger> undo, bool restart = false)
                => L.Add(new Tweak
                {
                    Id = id, Category = cat, Name = name, Description = desc,
                    Recommended = rec, IsSelected = rec,
                    ApplyAction = apply, UndoAction = undo, NeedsRestart = restart
                });

            // ==========================================================
            //  ลำดับเดียวกับ README — ใช้เป็นลำดับ Optimize ด้วย
            // ==========================================================

            // 1) NIC Optimization (01)
            Add("r9_nic", CAT_NIC, "NIC Optimization (R9)",
                "ปิด EEE / Green Ethernet / WoL / Offload · Flow Control off · RSS 4 Queues · RX 4096 / TX 128 · ความเร็ว 2.5G + autotuning normal",
                true,
                log => RunScript(log, "01_Network_Adapter_Optimization.ps1"),
                null);

            // 2) Netsh TCP + MTU + Latency Registry (05)
            Add("r9_netsh", CAT_NET, "Netsh TCP + MTU 1492 + Latency Registry",
                "netsh global 8 ค่า (chimney/rss/netdma/dca/ecn/timestamps/heuristics) · MTU 1492 ทุก interface · TcpAckFrequency/NoDelay/DelAckTicks · Psched · Dnscache TTL",
                true,
                log => RunScript(log, "05_Netsh_TCP_MTU_Latency_Registry.ps1"),
                null,
                restart: true);

            // 3) QoS Policy FiveM (02)
            Add("r9_qos", CAT_NET, "QoS DSCP 46 (FiveM / GTA5)",
                "สร้าง QoS Policy DSCP 46 (Expedited Forwarding) ให้ FiveM.exe / FiveM_GTAProcess.exe / GTA5.exe + ปิด NLA",
                true,
                log => RunScript(log, "02_QoS_Policy_FiveM.ps1"),
                log =>
                {
                    foreach (var n in new[] { "fivem", "fivem_gtaprocess", "GTA5" })
                        Sys.DelKey(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\QoS\" + n);
                    Sys.DelValue(@"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\QoS", "Do not use NLA");
                    log.Ok("ลบ QoS policies + NLA แล้ว");
                });

            // 4) Services Disable (06)
            Add("r9_services", CAT_SVC, "ปิด 20 Services (R9)",
                "SysMain · DiagTrack · WSearch · GameDVR · telemetry · SSDP/UPnP · Fax · RemoteRegistry ฯลฯ — เปิดกลับได้ที่ toggle",
                true,
                log => RunScript(log, "06_Windows_Services_Disable.ps1"),
                log =>
                {
                    string[] svcs = { "SysMain", "DiagTrack", "dmwappushservice", "MapsBroker", "WSearch",
                        "WerSvc", "RemoteRegistry", "Fax", "RetailDemo", "WMPNetworkSvc", "PhoneSvc",
                        "AJRouter", "lfsvc", "PimIndexMaintenanceSvc", "BcastDVRUserService",
                        "diagnosticshub.standardcollector.service", "SSDPSRV", "upnphost", "TrkWks",
                        "TabletInputService" };
                    foreach (var s in svcs)
                    {
                        Sys.Run("sc.exe", $"config \"{s}\" start= auto");
                        Sys.Run("sc.exe", $"start \"{s}\"");
                    }
                    log.Ok("เปิด services ทั้ง 20 กลับ (start= auto)");
                });

            // 5) BCD + USB + Hibernate (07)
            Add("r9_bcd", CAT_POWER, "BCD + USB Selective Suspend + Hibernate",
                "ลบ useplatformclock · ลบ useplatformtick · dynamic tick ON (ค่าปลอดภัย) · USB suspend off · Hibernate/Fast Startup off · PowerThrottlingOff",
                true,
                log => RunScript(log, "07_BCD_USB_Hibernate_Tweaks.ps1"),
                log =>
                {
                    Sys.Cmd("bcdedit /deletevalue disabledynamictick");
                    Sys.Cmd("bcdedit /deletevalue useplatformtick");
                    Sys.Cmd("powercfg /hibernate on");
                    Sys.DelValue(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff");
                    log.Ok("คืน BCD + Hibernate + PowerThrottling กลับ (รีสตาร์ทมีผล)");
                },
                restart: true);

            // 6) R9 Power Plan (08) — ⚠️ ลบแผนเดิมทั้งหมด
            Add("r9_power", CAT_POWER, "R9 Power Plan + CPU Boost",
                "⚠️ สร้างแผน R9 จาก Ultimate แล้วลบแผนอื่นทิ้ง · ปลดล็อกค่า CPU ซ่อน (Boost/EPP/Core Parking 100%) · NVMe/Wi-Fi/Sleep latency",
                true,
                log => RunScript(log, "13_R9_Powerplan.ps1"),
                log =>
                {
                    Sys.Cmd("powercfg -restoredefaultschemes");
                    log.Ok("restoredefaultschemes แล้ว — แผน R9 ถูกลบ กลับแผน Windows เริ่มต้น");
                });

            // 7) FiveM Tweaks + STR 0.5ms (09)
            Add("r9_fivem", CAT_FIVEM, "FiveM Tweaks + STR Timer 0.5ms",
                "ล้าง cache 4 โฟลเดอร์ · CpuPriorityClass=3 ให้ 3 process · CEF HW accel off · NetworkThrottling/SystemResponsiveness · ติดตั้ง STR service (0.5ms)",
                true,
                log => RunScript(log, "09_FiveM_Tweaks.ps1"),
                log => SystemTool(log, "defaults"),
                restart: true);

            // 8) GTA5 settings.xml + CitizenFX.ini (10)
            Add("r9_gta5", CAT_FIVEM, "GTA5 settings.xml + CitizenFX.ini",
                "เขียนค่าเกม: 1080p144 borderless · VSync off · เงา/หญ้าต่ำ Texture สูง · AF 16x + ini (net_mtu 1472 · interp 0 · RenderAhead 1) — ปิดเกมก่อนรัน",
                false,
                log => RunScript(log, "10_GTA5_CitizenFX_Config.ps1"),
                null);

            // 9) Interface + Adapter Power (11) — รีสตาร์ทการ์ดเน็ต
            Add("r9_iface", CAT_NIC, "Interface Power (Restart NIC)",
                "MTU 1472 · Hop 64 · ECN off · ClampMss · BaseReach 15s · ปิด power saving ระดับ driver (EnablePME/NicAutoPowerSaver) — ⚠️ เน็ตหลุดแป๊บนึง",
                false,
                log => RunScript(log, "11_Adapter_Interface_Power_Settings.ps1"),
                null);

            // ---------- granular toggles (หน้า Dashboard) ----------
            // Nagle off (ค่าเดียวกับส่วน 3 ของสคริปต์ 05)
            Add("lat_nagle", CAT_NET, "TCP No-Delay (Nagle off)",
                "TcpAckFrequency=1 · TCPNoDelay=1 · TcpDelAckTicks=0 ทุก interface — เกมส่ง packet ทันที",
                true,
                log =>
                {
                    string p = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
                    Sys.SetDword(p, "TcpAckFrequency", 1);
                    Sys.SetDword(p, "TCPNoDelay", 1);
                    Sys.SetDword(p, "TcpDelAckTicks", 0);
                    foreach (var iface in Sys.SubKeys(p + @"\Interfaces"))
                    {
                        Sys.SetDword(iface, "TcpAckFrequency", 1);
                        Sys.SetDword(iface, "TCPNoDelay", 1);
                        Sys.SetDword(iface, "TcpDelAckTicks", 0);
                    }
                    log.Ok("Nagle off ทุก interface แล้ว");
                },
                log =>
                {
                    string p = @"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";
                    foreach (var v in new[] { "TcpAckFrequency", "TCPNoDelay", "TcpDelAckTicks" })
                        Sys.DelValue(p, v);
                    foreach (var iface in Sys.SubKeys(p + @"\Interfaces"))
                        foreach (var v in new[] { "TcpAckFrequency", "TCPNoDelay", "TcpDelAckTicks" })
                            Sys.DelValue(iface, v);
                    log.Ok("คืนค่า Nagle ปกติแล้ว");
                });

            // MMCSS low latency (ค่าเดียวกับส่วน 4 ของสคริปต์ 09)
            Add("fm_mmcss", CAT_FIVEM, "Low-Latency Render / MMCSS",
                "NetworkThrottlingIndex=FFFFFFFF (ปิดหน่วงเน็ตตอนเล่น media) · SystemResponsiveness=0 (ไม่สงวน CPU 20%)",
                true,
                log =>
                {
                    string p = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                    Sys.SetDword(p, "NetworkThrottlingIndex", unchecked((int)4294967295));
                    Sys.SetDword(p, "SystemResponsiveness", 0);
                    log.Ok("MMCSS low-latency ตั้งแล้ว");
                },
                log =>
                {
                    string p = @"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
                    Sys.SetDword(p, "NetworkThrottlingIndex", 10);
                    Sys.SetDword(p, "SystemResponsiveness", 20);
                    log.Ok("คืน MMCSS ค่าเริ่มต้น (10/20)");
                });

            // DNS cache flush + optimize TTL
            Add("dnscache_opt", CAT_NET, "DNS Cache Flush & Optimize",
                "flushdns ทันที + MaxCacheTtl 86400 / NegativeTtl 5 (resolve น้อยลง ตอบเร็วขึ้น)",
                true,
                log =>
                {
                    Sys.Cmd("ipconfig /flushdns");
                    string p = @"HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters";
                    Sys.SetDword(p, "MaxCacheTtl", 86400);
                    Sys.SetDword(p, "MaxNegativeCacheTtl", 5);
                    log.Ok("flush DNS + ตั้ง TTL แล้ว");
                },
                log =>
                {
                    string p = @"HKLM\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters";
                    Sys.DelValue(p, "MaxCacheTtl");
                    Sys.DelValue(p, "MaxNegativeCacheTtl");
                    Sys.Cmd("ipconfig /flushdns");
                    log.Ok("คืนค่า DNS cache เริ่มต้น");
                });

            // 10) AFD Buffer Pool Tuning (14) — จาก aspas setting 03_TCPIP_AFD
            Add("r9_afd", CAT_NET, "AFD Buffer Pool Tuning",
                "SmallBuffer 4KB · MediumBuffer 16KB · LargeBuffer 128KB · Pool depth 16/8/4 · DoNotHoldNICBuffers · TransmitWorker 32 · BufferMultiplier 2",
                true,
                log => RunScript(log, "14_AFD_Buffer_Tuning.ps1"),
                log =>
                {
                    string p = @"HKLM\SYSTEM\CurrentControlSet\Services\AFD\Parameters";
                    foreach (var v in new[] {
                        "SmallBufferSize", "MediumBufferSize", "LargeBufferSize",
                        "SmallBufferListDepth", "MediumBufferListDepth", "LargBufferListDepth",
                        "DoNotHoldNICBuffers", "TransmitWorker", "BufferMultiplier" })
                        Sys.DelValue(p, v);
                    log.Ok("ลบ AFD Buffer Pool tweaks แล้ว (คืนค่า Windows default)");
                });

            // 11) NIC Interrupt Latency Tuning (15) — จาก aspas setting 04_NIC_Registry
            Add("r9_nic_lat", CAT_NIC, "NIC Interrupt Latency Tuning",
                "ITR 976 · TxIntDelay 0 · RxIntDelay 0 · CoalesceBufferSize 1024 — ลด interrupt delay ให้เหมาะกับ FiveM (UDP packet ถี่)",
                true,
                log => RunScript(log, "15_NIC_Latency_Tuning.ps1"),
                log =>
                {
                    // ITR/Delay values อยู่ใน NIC registry subkey ของแต่ละ adapter
                    // ลบ key ออก → driver จะกลับใช้ค่า default อัตโนมัติ
                    var nicRoot = Sys.SubKeys(@"HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002bE10318}");
                    foreach (var sub in nicRoot)
                    {
                        foreach (var v in new[] { "ITR", "TxIntDelay", "RxIntDelay", "CoalesceBufferSize" })
                            Sys.DelValue(sub, v);
                    }
                    log.Ok("คืน NIC interrupt defaults แล้ว");
                });

            return L;
        }
    }
}