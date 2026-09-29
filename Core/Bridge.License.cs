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
                string norm = LicenseService.Normalize(key);
                if (string.IsNullOrEmpty(norm) || norm.Length != 19)
                {
                    _log.Warn("Activate à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§: à¸£à¸¹à¸›à¹à¸šà¸š key à¹„à¸¡à¹ˆà¸–à¸¹à¸à¸•à¹‰à¸­à¸‡");
                    return J(new { ok = false, msg = "à¸£à¸¹à¸›à¹à¸šà¸š key à¹„à¸¡à¹ˆà¸–à¸¹à¸à¸•à¹‰à¸­à¸‡ (XXXX-XXXX-XXXX-XXXX)" });
                }

                // à¹‚à¸«à¸¡à¸”à¸­à¸­à¸™à¹„à¸¥à¸™à¹Œ â€” à¸•à¸£à¸§à¸ˆà¸à¸±à¸š server (à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸­à¸”à¸¡à¸´à¸™à¹ƒà¸Šà¹‰ local à¸•à¸²à¸¡à¸›à¸à¸•à¸´)
                if (ServerOn && !LicenseService.IsAdmin())
                {
                    var (sok, smsg, sdays, soff) = ServerActivate(norm, LicenseService.Hwid());
                    if (sok)
                    {
                        _cfg.rememberKey = remember;
                        _cfg.savedKey = LicenseService.Normalize(norm);
                        SaveCfg();
                        _log.Ok($"Activate à¸ªà¸³à¹€à¸£à¹‡à¸ˆ (server): {norm} (à¹€à¸«à¸¥à¸·à¸­ {sdays} à¸§à¸±à¸™)");
                        Notify("green", "ðŸ”‘ Key Activate", "**" + norm + "** â€” à¸œà¸¹à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸¥à¹‰à¸§\nà¹€à¸«à¸¥à¸·à¸­à¸­à¸²à¸¢à¸¸ **" + sdays + " à¸§à¸±à¸™**");
                        return J(new { ok = true, msg = "key à¸–à¸¹à¸à¸•à¹‰à¸­à¸‡", days = sdays });
                    }
                    if (!soff)
                    {
                        _log.Warn("Activate à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§: " + smsg);
                        return J(new { ok = false, msg = smsg });
                    }
                    // server à¹€à¸‚à¹‰à¸²à¹„à¸¡à¹ˆà¸–à¸¶à¸‡ â†’ à¹ƒà¸Šà¹‰à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¸—à¸µà¹ˆà¹€à¸„à¸¢à¸šà¸±à¸™à¸—à¸¶à¸à¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸—à¸™ (à¸¢à¸±à¸‡à¸¥à¹‡à¸­à¸„ HWID à¸­à¸¢à¸¹à¹ˆ)
                    var (lok, lmsg, ldays, llic) = LicenseService.Validate(key);
                    if (lok)
                    {
                        _cfg.rememberKey = remember;
                        _cfg.savedKey = LicenseService.Normalize(key);
                        SaveCfg();
                        _log.Warn("server à¹€à¸‚à¹‰à¸²à¹„à¸¡à¹ˆà¸–à¸¶à¸‡ â€” Activate à¸”à¹‰à¸§à¸¢à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸—à¸™ (à¹€à¸«à¸¥à¸·à¸­ " + ldays + " à¸§à¸±à¸™)");
                        Notify("yellow", "ðŸ”‘ Key Activate (offline)", "**" + norm + "** â€” server à¹€à¸‚à¹‰à¸²à¹„à¸¡à¹ˆà¸–à¸¶à¸‡ à¹ƒà¸Šà¹‰à¸‚à¹‰à¸­à¸¡à¸¹à¸¥à¹ƒà¸™à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸—à¸™\nà¹€à¸«à¸¥à¸·à¸­à¸­à¸²à¸¢à¸¸ **" + ldays + " à¸§à¸±à¸™**");
                        return J(new { ok = true, msg = "key à¸–à¸¹à¸à¸•à¹‰à¸­à¸‡", days = ldays });
                    }
                    _log.Warn("Activate à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§ (à¸­à¸­à¸Ÿà¹„à¸¥à¸™à¹Œ): " + smsg);
                    return J(new { ok = false, msg = smsg });
                }

                var (ok, msg, days, lic) = LicenseService.Validate(key);
                if (ok)
                {
                    _cfg.rememberKey = remember;
                    _cfg.savedKey = remember ? LicenseService.Normalize(key) : "";
                    SaveCfg();
                    _log.Ok($"Activate à¸ªà¸³à¹€à¸£à¹‡à¸ˆ: {lic.key} (à¹€à¸«à¸¥à¸·à¸­ {days} à¸§à¸±à¸™)");
                    Notify("green", "ðŸ”‘ Key Activate", "**" + lic.key + "** â€” à¸œà¸¹à¸à¹€à¸„à¸£à¸·à¹ˆà¸­à¸‡à¹à¸¥à¹‰à¸§\nà¹€à¸«à¸¥à¸·à¸­à¸­à¸²à¸¢à¸¸ **" + days + " à¸§à¸±à¸™**");
                }
                else _log.Warn("Activate à¸¥à¹‰à¸¡à¹€à¸«à¸¥à¸§: " + msg);
                return J(new { ok, msg, days });
            }
            catch (Exception ex)
            {
                _log.Err("activate: " + ex.Message);
                return J(new { ok = false, msg = "à¸œà¸´à¸”à¸žà¸¥à¸²à¸”: " + ex.Message });
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