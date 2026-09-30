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
        // ================= LOGIN =================
        public string Hwid() { Hello(); return J(new { ok = true, hwid = LicenseService.Hwid() }); }

        public string GetSaved() { Hello(); return J(new { key = _cfg.rememberKey ? _cfg.savedKey : "" }); }

        public string Copy(string text)
        {
            try { _win.Dispatcher.Invoke(() => { try { Clipboard.SetText(text ?? ""); } catch { } }); }
            catch { }
            return J(new { ok = true });
        }

        public string Activate(string key, bool remember)
        {
            Hello();
            try
            {
                if (LicenseService.IsAdminRecoveryKey(key?.Trim()))
                {
                    _cfg.savedKey = key.Trim();
                    _cfg.rememberKey = true;
                    SaveCfg();
                    _log.Ok("Admin recovery key activated.");
                    return J(new { ok = true, msg = "Admin recovery key activated", days = 36500 });
                }

                string norm = LicenseService.Normalize(key);
                if (string.IsNullOrEmpty(norm) || norm.Length != 19)
                {
                    _log.Warn("Activate ล้มเหลว: รูปแบบ key ไม่ถูกต้อง");
                    return J(new { ok = false, msg = "รูปแบบ key ไม่ถูกต้อง (XXXX-XXXX-XXXX-XXXX)" });
                }

                // โหมดออนไลน์ — ตรวจกับ server (เครื่องแอดมินใช้ local ตามปกติ)
                if (ServerOn && !LicenseService.IsAdmin())
                {
                    var (sok, smsg, sdays, soff) = ServerActivate(norm, LicenseService.Hwid());
                    if (sok)
                    {
                        _cfg.rememberKey = remember;
                        _cfg.savedKey = LicenseService.Normalize(norm);
                        SaveCfg();
                        _log.Ok($"Activate สำเร็จ (server): {norm} (เหลือ {sdays} วัน)");
                        Notify("green", "🔑 Key Activate", "**" + norm + "** — ผูกเครื่องแล้ว\nเหลืออายุ **" + sdays + " วัน**");
                        return J(new { ok = true, msg = "key ถูกต้อง", days = sdays });
                    }
                    if (!soff)
                    {
                        _log.Warn("Activate ล้มเหลว: " + smsg);
                        return J(new { ok = false, msg = smsg });
                    }
                    // server เข้าไม่ถึง → ใช้ข้อมูลที่เคยบันทึกในเครื่องแทน (ยังล็อค HWID อยู่)
                    var (lok, lmsg, ldays, llic) = LicenseService.Validate(key);
                    if (lok)
                    {
                        _cfg.rememberKey = remember;
                        _cfg.savedKey = LicenseService.Normalize(key);
                        SaveCfg();
                        _log.Warn("server เข้าไม่ถึง — Activate ด้วยข้อมูลในเครื่องแทน (เหลือ " + ldays + " วัน)");
                        Notify("yellow", "🔑 Key Activate (offline)", "**" + norm + "** — server เข้าไม่ถึง ใช้ข้อมูลในเครื่องแทน\nเหลืออายุ **" + ldays + " วัน**");
                        return J(new { ok = true, msg = "key ถูกต้อง", days = ldays });
                    }
                    _log.Warn("Activate ล้มเหลว (ออฟไลน์): " + smsg);
                    return J(new { ok = false, msg = smsg });
                }

                var (ok, msg, days, lic) = LicenseService.Validate(key);
                if (ok)
                {
                    _cfg.rememberKey = remember;
                    _cfg.savedKey = remember ? LicenseService.Normalize(key) : "";
                    SaveCfg();
                    _log.Ok($"Activate สำเร็จ: {lic.key} (เหลือ {days} วัน)");
                    Notify("green", "🔑 Key Activate", "**" + lic.key + "** — ผูกเครื่องแล้ว\nเหลืออายุ **" + days + " วัน**");
                }
                else _log.Warn("Activate ล้มเหลว: " + msg);
                return J(new { ok, msg, days });
            }
            catch (Exception ex)
            {
                _log.Err("activate: " + ex.Message);
                return J(new { ok = false, msg = "ผิดพลาด: " + ex.Message });
            }
        }

        public string Recover()
        {
            var l = LicenseService.RecoverForThisMachine();
            return J(new { ok = l != null, key = l?.key });
        }

        public string EnterDashboard()
        {
            _win.Dispatcher.BeginInvoke(new Action(_win.NavigateDashboard));
            return J(new { ok = true });
        }
    }
}