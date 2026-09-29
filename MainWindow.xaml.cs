using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using KingR9Tools.Core;
using Microsoft.Web.WebView2.Core;

namespace KingR9Tools
{
    public partial class MainWindow : Window
    {
        private readonly Logger _log;
        private readonly string _logFile;
        private readonly List<Tweak> _all;
        private Bridge _bridge;

        public MainWindow()
        {
            InitializeComponent();

            _logFile = Path.Combine(BackupService.RootDir, $"log_{DateTime.Now:yyyyMMdd}.txt");
            _log = new Logger(_logFile);
            _all = TweakRegistry.Build();

            // ครั้งแรกของเครื่อง → สร้าง key เริ่มต้นผูกกับ HWID นี้ แล้วแจ้งผู้ใช้
            if (LicenseService.EnsureSeeded())
            {
                var lic = LicenseService.RecoverForThisMachine();
                if (lic != null)
                    MessageBox.Show(
                        "ยินดีต้อนรับสู่ KingR9 Tools!\n\n" +
                        "License Key ของเครื่องนี้:\n" + lic.key +
                        "\n\n(เก็บไว้ที่ " + LicenseService.StoreFile + ")",
                        "KingR9 Tools — Activation",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Loaded += async (s, e) => await InitWeb();

            // มุมหน้าต่างโค้ง 22px (ตัดที่ระดับ OS — ครอบคลุมทุกหน้า UI)
            SourceInitialized += (s, e) => ApplyRoundCorners();
            SizeChanged += (s, e) => ApplyRoundCorners();
            StateChanged += (s, e) => Dispatcher.BeginInvoke(
                new Action(ApplyRoundCorners), System.Windows.Threading.DispatcherPriority.Loaded);
            DpiChanged += (s, e) => ApplyRoundCorners();
        }

        // ---------- มุมหน้าต่างโค้ง ----------
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        /// <summary>ตัดมุมหน้าต่างให้โค้งเท่าการ์ด (22 DIP) — maximize = เหลี่ยม, minimize = ข้าม (กัน region เพี้ยน)</summary>
        private void ApplyRoundCorners()
        {
            try
            {
                if (WindowState == WindowState.Minimized) return;   // ตอน minimize ขนาดเพี้ยน — ห้ามตั้ง region
                if (ActualWidth < 100 || ActualHeight < 100) return; // กันค่าขยะ
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                if (WindowState == WindowState.Maximized) { SetWindowRgn(hwnd, IntPtr.Zero, true); return; }

                double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                int w = (int)Math.Ceiling(ActualWidth * dpi) + 1;
                int h = (int)Math.Ceiling(ActualHeight * dpi) + 1;
                int r = (int)Math.Ceiling(22 * dpi);
                SetWindowRgn(hwnd, CreateRoundRectRgn(0, 0, w, h, r, r), true);
            }
            catch { }
        }

        private async Task InitWeb()
        {
            string baseDir = WebAssets.ExtractAll(_log);
            try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(Path.Combine(baseDir, "logo.png"))); } catch { }
            string userData = Path.Combine(Sys.Local, "KingR9Tools", "WebView2");
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, userData);
                await Web.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                // กันเหนียว: App.OnStartup ตรวจ Runtime ก่อนเปิดหน้าต่างแล้ว — ถ้ายังล้มเหลว (เช่น Runtime หายกลางทาง)
                // ต้องแจ้งผู้ใช้ให้เห็นสาเหตุ ห้ามปิดเงียบเป็นหน้าจอว่าง
                _log.Err("webview2 init: " + ex.Message);
                MessageBox.Show(
                    "เปิดส่วนแสดงผล (WebView2) ไม่สำเร็จ:\n" + ex.Message +
                    "\n\nลองปิดโปรแกรมแล้วเปิดใหม่อีกครั้ง — ถ้ายังไม่ได้ ให้ติดตั้ง Microsoft WebView2 Runtime ก่อน",
                    "KingR9 Tools", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            var wv = Web.CoreWebView2;
            wv.Settings.AreDefaultContextMenusEnabled = false;
            wv.Settings.AreDevToolsEnabled = false;
            wv.Settings.IsStatusBarEnabled = false;
            wv.Settings.IsZoomControlEnabled = false;
            wv.Settings.AreBrowserAcceleratorKeysEnabled = false;

            // ให้พื้นที่ app-region:drag ในหน้าเว็บลากหน้าต่างได้แบบ native
            try { wv.Settings.IsNonClientRegionSupportEnabled = true; } catch { }

            // https://app.kingr9/* → โฟลเดอร์ของแอป (เลี่ยงข้อจำกัดของ file://)
            wv.SetVirtualHostNameToFolderMapping("app.kingr9", baseDir,
                CoreWebView2HostResourceAccessKind.Allow);

            // บอกหน้าเว็บว่ากำลังรันในแอป (สคริปต์เดโม่จะไม่ทำงาน — bridge.js จัดการแทน)
            await wv.AddScriptToExecuteOnDocumentCreatedAsync("window.KR_HOSTED = true;");

            _bridge = new Bridge(this, _log, _all);
            wv.AddHostObjectToScript("kr", _bridge);

            // RPC แบบไม่บล็อก — หน้าเว็บ postMessage({__kr,type,payload}) → Bridge รับที่นี่
            // (แทนการเรียก host object แบบ sync ซึ่งทุกคำสั่งบล็อก UI thread จนแอพ Not Responding)
            wv.WebMessageReceived += (s, e) =>
            {
                try { _bridge.OnWebMessage(e.WebMessageAsJson); }
                catch (Exception ex) { _log.Err("rpc-dispatch: " + ex.Message); }
            };

            // probe วัดว่า ExecuteScriptAsync เห็น context ของหน้าเว็บจริงหรือไม่
            wv.NavigationCompleted += async (s, e) =>
            {
                try
                {
                    var probe = await wv.ExecuteScriptAsync(
                        "String(typeof window.__krDispatch)+'|'+String(typeof window.KR)+'|'+location.pathname");
                    _log.Info("probe: " + (probe ?? "null"));
                    await wv.ExecuteScriptAsync("window.__krDispatch({event:'probe'});");
                }
                catch (Exception ex) { _log.Err("probe-err: " + ex.Message); }
            };

            // เคยติ๊ก "Remember this key" ไว้ → auto-login เข้า Dashboard เลย
            Navigate(SavedKey() != null);

            _log.Head("KingR9 Tools v1.0 (Web UI) พร้อมใช้งาน");
            _log.Info($"โหลด {_all.Count} tweaks · UI: WebView2");
            _log.Info($"Log: {_logFile}");
        }

        /// <summary>คืน key ที่จำไว้ถ้ายังใช้ได้ ไม่งั้น null</summary>
        private string SavedKey()
        {
            try
            {
                if (!File.Exists(LicenseService.ConfigFile)) return null;
                var d = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(LicenseService.ConfigFile));
                if (d.TryGetProperty("rememberKey", out var rk) && rk.ValueKind == JsonValueKind.True &&
                    d.TryGetProperty("savedKey", out var sk) && sk.ValueKind == JsonValueKind.String)
                {
                    string key = sk.GetString();
                    if (string.IsNullOrWhiteSpace(key)) return null;
                    var (ok, _, _, _) = LicenseService.Validate(key);
                    if (ok)
                    {
                        _log.Ok($"Auto-login ด้วย key ที่จำไว้: {key}");
                        return key;
                    }
                }
            }
            catch { }
            return null;
        }

        public void Navigate(bool dashboard)
        {
            if (dashboard) NavigateDashboard();
            else NavigateLogin();
        }

        private readonly string _cacheBust = "v=" + DateTime.Now.Ticks;

        public void NavigateLogin() =>
            Web.CoreWebView2?.Navigate("https://app.kingr9/kingr9tools_login.html?" + _cacheBust);

        public void NavigateDashboard() =>
            Web.CoreWebView2?.Navigate("https://app.kingr9/kingr9tools_dashboard.html?" + _cacheBust);

        public void NavigateKeygen() =>
            Web.CoreWebView2?.Navigate("https://app.kingr9/kingr9tools_keygen.html?" + _cacheBust);

        // ---------- WINDOW ACTIONS (เรียกจาก Bridge) ----------
        public void DoDrag() { try { DragMove(); } catch { } }
        public void DoMin() => WindowState = WindowState.Minimized;
        public void DoMax() =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        public void DoClose() => Close();

        protected override void OnClosed(EventArgs e)
        {
            _bridge?.Cleanup();
            base.OnClosed(e);
        }
    }
}