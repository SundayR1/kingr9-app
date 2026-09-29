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
        // ================= WINDOW =================
        public string Window(string action)
        {
            _win.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (action == "drag") _win.DoDrag();
                    else if (action == "min") _win.DoMin();
                    else if (action == "max") _win.DoMax();
                    else if (action == "close") _win.DoClose();
                }
                catch { }
            }));
            return J(new { ok = true });
        }

        public string LicenseInfo()
        {
            var lic = LicenseService.RecoverForThisMachine();
            return lic == null
                ? J(new { key = "â€”", hwid = LicenseService.Hwid(), days = 0, status = "Inactive" })
                : J(new { key = lic.key, hwid = lic.hwid, days = LicenseService.DaysLeft(lic), status = "Active" });
        }

    }
}