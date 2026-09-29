using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KingR9Tools
{
    /// <summary>
    /// หน้าติดตั้ง WebView2 Runtime อัตโนมัติ — แสดงเฉพาะเครื่องที่ไม่มี Runtime
    /// (เจอบ่อยบน Windows 10 ที่เพิ่งลงใหม่ → เดิมแอปจะเปิดมาเป็นหน้าจอว่าง)
    /// ตัวติดตั้ง (Microsoft Evergreen Bootstrapper ~1.7MB) ฝังมากับ .exe
    /// แอปรันสิทธิ์ Admin อยู่แล้ว (app.manifest) → ติดตั้งได้เลย ไม่เด้ง UAC ซ้ำ
    /// หมายเหตุ: bootstrapper จะดาวน์โหลด runtime จาก Microsoft → เครื่องต้องต่ออินเทอร์เน็ต
    /// </summary>
    public class WebRtWindow : Window
    {
        /// <summary>true = ติดตั้งสำเร็จ ให้แอปเปิดต่อได้เลย</summary>
        public bool Installed { get; private set; }

        private readonly StackPanel _panel;
        private readonly TextBlock _status;
        private readonly Button _installBtn;
        private Button _manualBtn;

        public WebRtWindow()
        {
            Title = "KingR9 Tools — เตรียมส่วนแสดงผล";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            var bg = new SolidColorBrush(Color.FromRgb(0x0B, 0x0F, 0x14));
            var fg = new SolidColorBrush(Color.FromRgb(0xE8, 0xEE, 0xF5));
            var gold = new SolidColorBrush(Color.FromRgb(0xF5, 0xC5, 0x42));
            var green = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80));
            Background = bg;

            _panel = new StackPanel { Margin = new Thickness(30, 26, 30, 22) };

            try
            {
                var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UI.logo.png");
                if (stream != null)
                {
                    var img = new Image
                    {
                        Width = 54, Height = 54, Margin = new Thickness(0, 0, 0, 10),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    stream.Dispose();
                    img.Source = bmp;
                    _panel.Children.Add(img);
                }
            }
            catch { }

            _panel.Children.Add(new TextBlock
            {
                Text = "⚙️ ต้องติดตั้ง WebView2 Runtime ก่อน",
                FontSize = 16, FontWeight = FontWeights.Bold, Foreground = gold,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _panel.Children.Add(new TextBlock
            {
                Text = "เครื่องนี้ยังไม่มีส่วนแสดงผล Microsoft WebView2 (ส่วนที่ KingR9 Tools ใช้วาดหน้าจอ)\n" +
                       "เจอบ่อยบน Windows 10 ที่เพิ่งลงใหม่ — ไม่ต้องกังวล แอปจัดการให้เองได้\n\n" +
                       "กดปุ่มด้านล่างเพื่อติดตั้งอัตโนมัติ (ต้องต่ออินเทอร์เน็ต · ใช้เวลา ~1-2 นาที)\n" +
                       "พอติดตั้งเสร็จ แอปจะเปิดต่อให้ทันที",
                TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 19,
                Foreground = fg, Margin = new Thickness(0, 12, 0, 16)
            });

            _status = new TextBlock
            {
                Text = "", TextWrapping = TextWrapping.Wrap, FontSize = 12,
                Foreground = green, Margin = new Thickness(0, 0, 0, 10)
            };
            _panel.Children.Add(_status);

            _installBtn = new Button
            {
                Content = "ติดตั้งให้ฉันเลย (แนะนำ)",
                Height = 42, FontSize = 13.5, FontWeight = FontWeights.Bold,
                Foreground = bg, Background = gold, BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _installBtn.Click += async (s, e) => await Install();
            _panel.Children.Add(_installBtn);

            var noBtn = new Button
            {
                Content = "ปิดโปรแกรม", Height = 34, FontSize = 12,
                Foreground = fg, Background = Brushes.Transparent,
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            noBtn.Click += (s, e) => Close();
            _panel.Children.Add(noBtn);

            Content = _panel;
        }

        private async Task Install()
        {
            _installBtn.IsEnabled = false;
            try
            {
                _status.Text = "กำลังดาวน์โหลดตัวติดตั้ง…";

                string dir = Path.Combine(Path.GetTempPath(), "KingR9Tools");
                Directory.CreateDirectory(dir);
                string exe = Path.Combine(dir, "MicrosoftEdgeWebview2Setup.exe");

                // ดาวน์โหลด WebView2 Bootstrapper จาก Microsoft โดยตรง (ไม่ฝัง exe ใน resource อีกต่อไป)
                using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(3) })
                {
                    var bytes = await http.GetByteArrayAsync(
                        "https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    await File.WriteAllBytesAsync(exe, bytes);
                }

                _status.Text = "กำลังติดตั้ง WebView2 Runtime… อย่าปิดหน้าต่างนี้";
                var psi = new ProcessStartInfo(exe, "/silent /install") { UseShellExecute = true };
                using (var p = Process.Start(psi))
                {
                    await Task.Run(() => p.WaitForExit(300000));   // สูงสุด 5 นาที
                }

                if (App.WebRtInstalled())
                {
                    Installed = true;
                    _status.Text = "ติดตั้งเสร็จแล้ว ✓ กำลังเปิด KingR9 Tools…";
                    await Task.Delay(900);
                    Close();
                    return;
                }

                Fail("ติดตั้งไม่สำเร็จ — เครื่องต่ออินเทอร์เน็ตอยู่ไหม? ลองกดติดตั้งอีกครั้ง หรือใช้ปุ่มด้านล่างติดตั้งเองแล้วเปิดแอปใหม่");
            }
            catch (Exception ex)
            {
                Fail("ติดตั้งไม่สำเร็จ: " + ex.Message);
            }
        }

        /// <summary>แสดงสาเหตุ + ปุ่มสำรอง (เปิดหน้าดาวน์โหลดของ Microsoft)</summary>
        private void Fail(string msg)
        {
            _status.Text = msg;
            _installBtn.IsEnabled = true;
            if (_manualBtn != null) return;
            _manualBtn = new Button
            {
                Content = "เปิดหน้าดาวน์โหลด WebView2 จาก Microsoft (ติดตั้งเอง แล้วเปิดแอปใหม่)",
                Height = 36, FontSize = 12, Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x22, 0x2C)),
                Margin = new Thickness(0, 8, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            _manualBtn.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true }); }
                catch { }
            };
            _panel.Children.Add(_manualBtn);
        }
    }
}
