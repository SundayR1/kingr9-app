using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KingR9Tools.Core
{
    public static class CleanerService
    {
        public sealed class Cat
        {
            public string Id, Name;
            public double Gb;
            public readonly List<string> Dirs = new();
        }

        private static string L => Sys.Local;
        private static string W => Sys.Win;

        public static List<Cat> Categories() => new()
        {
            new Cat { Id = "temp", Name = "Temp & System Files", Dirs =
            {
                Sys.Temp,
                Path.Combine(W, "Temp")
            }},
            new Cat { Id = "inet", Name = "INet / Thumbnail Cache", Dirs =
            {
                Path.Combine(L, @"Microsoft\Windows\INetCache"),
            }},
            new Cat { Id = "wlogs", Name = "Windows Logs", Dirs =
            {
                Path.Combine(W, "Logs")
            }},
            new Cat { Id = "bin", Name = "Recycle Bin", Dirs = { } },   // ล้างโดยสคริปต์ (เถังรีไซเคิลวัดขนาดไม่ได้ตรงๆ)
        };

        private static double DirSize(string dir)
        {
            double mb = 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    try { mb += new FileInfo(f).Length / 1048576.0; } catch { }
            }
            catch { }
            return mb;
        }

        public static double Total(List<Cat> cats) => cats.Sum(c => c.Gb);

        /// <summary>วัดขนาดขยะจริงทุกหมวด</summary>
        public static List<Cat> Measure(Logger log)
        {
            var cats = Categories();
            foreach (var c in cats)
            {
                double mb = 0;
                foreach (var d in c.Dirs) mb += DirSize(d);
                c.Gb = Math.Round(mb / 1024.0, 2);
            }
            log?.Info($"สแกนไฟล์ขยะ: รวม {Total(cats):N2} GB");
            return cats;
        }

        /// <summary>ล้างจริง — อัปเดต Gb เป็นค่าที่ลบได้ต่อหมวด</summary>
        public static List<Cat> Clean(Logger log)
        {
            var cats = Categories();
            foreach (var c in cats)
            {
                double freed = 0;
                foreach (var d in c.Dirs) freed += Sys.DeleteContents(d);
                c.Gb = Math.Round(freed / 1024.0, 2);
                log.Ok($"ล้าง {c.Name}: {c.Gb:N2} GB");
            }
            log.Head($"Cache Cleanup เสร็จ — คืนพื้นที่รวม {Total(cats):N2} GB");
            return cats;
        }
    }
}