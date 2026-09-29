using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace KingR9Tools
{
    /// <summary>
    /// จุดเริ่มแอป — ตรวจ WebView2 Runtime ก่อนเปิดหน้าหลักเสมอ
    /// เครื่องที่เพิ่งลง Windows ใหม่ (Win10 ส่วนใหญ่) ไม่มี Runtime → เดิมหน้าจอจะว่าง/เปิดไม่ขึ้น
    /// ถ้าไม่มีจะเด้งหน้า "ติดตั้งให้ฉันเลย" (ตัวติดตั้งฝังมากับ .exe) แล้วเปิดแอปต่อให้อัตโนมัติ
    /// + จับ error ทุกเส้น — ห้ามแอปตายเงียบ ๆ โดยผู้ใช้ไม่รู้สาเหตุ
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;   // เราคุมการปิดเอง (กันปิดทันทีตอน dialog ปิด)

            // ลบไฟล์ .old ที่เหลือจากการอัปเดต (ถ้ามี)
            try
            {
                string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string old = exe + ".old";
                if (!string.IsNullOrEmpty(exe) && System.IO.File.Exists(old))
                    System.IO.File.Delete(old);
            }
            catch { }

            // ---- จับ error ทุกเส้น: โชว์กล่องแจ้งสาเหตุเสมอ ไม่ปิดเงียบ ----
            DispatcherUnhandledException += (s, args) =>
            {
                Fatal("เกิดข้อผิดพลาดในโปรแกรม", args.Exception);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                Fatal("เกิดข้อผิดพลาดร้ายแรง", args.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                try
                {
                    string logPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "KingR9Tools", "crash.log");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] UnobservedTask: {args.Exception}\n");
                }
                catch { }
                args.SetObserved();
            };

            // ---- ตรวจ WebView2 Runtime → ไม่มี = เด้งหน้าติดตั้งอัตโนมัติ ----
            if (!WebRtInstalled())
            {
                var setup = new WebRtWindow();
                setup.ShowDialog();
                if (!setup.Installed)   // ผู้ใช้ปิดเอง หรือติดตั้งไม่สำเร็จ
                {
                    Shutdown();
                    return;
                }
            }

            MainWindow = new MainWindow();
            MainWindow.Show();
            ShutdownMode = ShutdownMode.OnLastWindowClose;   // กลับเป็นพฤติกรรมปกติ (ปิดหน้าต่าง = ปิดแอป)
        }

        /// <summary>true = เครื่องนี้มี WebView2 Runtime พร้อมใช้</summary>
        public static bool WebRtInstalled()
        {
            try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
            catch { return false; }
        }

        private static void Fatal(string title, Exception ex)
        {
            try
            {
                MessageBox.Show(
                    "KingR9 Tools ขออภัยในความไม่สะดวก\n\n" + title +
                    "\n" + (ex == null ? "ไม่ทราบสาเหตุ" : ex.Message),
                    "KingR9 Tools", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // MessageBox ไม่สามารถแสดงได้ — บันทึกลงไฟล์เป็น fallback
                try
                {
                    string logPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "KingR9Tools", "crash.log");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath));
                    System.IO.File.AppendAllText(logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}: {ex?.ToString() ?? "unknown"}\n");
                }
                catch { }
            }
        }
    }
}
