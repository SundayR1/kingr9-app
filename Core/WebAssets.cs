using System;
using System.IO;
using System.Reflection;

namespace KingR9Tools.Core
{
    /// <summary>
    /// แตกไฟล์ UI (html/js) และสคริปต์ (ps1/bat) ที่ฝังมากับ .exe ออกมาใช้
    /// ทำให้แจกโปรแกรมได้แค่ไฟล์ .exe ไฟล์เดียว — ไฟล์จะถูกแตกไปที่ %LocalAppData%\KingR9Tools\web
    /// </summary>
    public static class WebAssets
    {
        /// <summary>โฟลเดอร์รากของไฟล์ที่แตกแล้ว (root ของ virtual host + โฟลเดอร์สคริปต์)</summary>
        public static string Root { get; private set; } = "";

        public static string ExtractAll(Logger log = null)
        {
            try
            {
                string root = Path.Combine(Sys.Local, "KingR9Tools", "web");
                Directory.CreateDirectory(Path.Combine(root, "Scripts", "R9Internet"));

                var asm = Assembly.GetExecutingAssembly();
                int n = 0;
                foreach (var res in asm.GetManifestResourceNames())
                {
                    string norm = res.Replace('\\', '.').Replace('/', '.');
                    string rel;
                    if (norm.StartsWith("UI.", StringComparison.OrdinalIgnoreCase))
                        rel = norm.Substring(3);
                    else if (norm.StartsWith("Scripts.R9Internet.", StringComparison.OrdinalIgnoreCase))
                        rel = "Scripts\\R9Internet\\" + norm.Substring("Scripts.R9Internet.".Length);
                    else if (norm.StartsWith("Scripts.Ares.vivetool.", StringComparison.OrdinalIgnoreCase))
                        rel = "Scripts\\Ares\\vivetool\\" + norm.Substring("Scripts.Ares.vivetool.".Length);
                    else if (norm.StartsWith("Scripts.Ares.wallpaper.", StringComparison.OrdinalIgnoreCase))
                        rel = "Scripts\\Ares\\wallpaper\\" + norm.Substring("Scripts.Ares.wallpaper.".Length);
                    else if (norm.StartsWith("Scripts.Ares.", StringComparison.OrdinalIgnoreCase))
                        rel = "Scripts\\Ares\\" + norm.Substring("Scripts.Ares.".Length);
                    else if (norm.StartsWith("Scripts.", StringComparison.OrdinalIgnoreCase))
                        rel = "Scripts\\" + norm.Substring("Scripts.".Length);
                    else continue;

                    string target = Path.Combine(root, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    // ต่อไฟล์แบบไม่ให้ไฟล์ไหนล็อกทั้งระบบ — ไฟล์ไหนแตกไม่ได้ (เช่น ViVeTool.exe กำลังถูกใช้งานอยู่) ข้ามไปไฟล์อื่น
                    try
                    {
                        using (var src = asm.GetManifestResourceStream(res))
                        using (var dst = File.Create(target))
                            src.CopyTo(dst);
                        n++;
                    }
                    catch { }
                }

                Root = root;
                log?.Info($"แตกไฟล์ในตัว {n} รายการ → {root}");
                return root;
            }
            catch (Exception ex)
            {
                // สำรอง: ถ้าแตกไม่ได้ ใช้โฟลเดอร์ข้าง exe (โหมด dev)
                Root = AppContext.BaseDirectory;
                log?.Err("extract assets: " + ex.Message);
                return Root;
            }
        }
    }
}