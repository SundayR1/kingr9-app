using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows; // <--- เพิ่มบรรทัดนี้
using System.Windows.Media;

namespace KingR9Tools.Core
{
    public enum LogLevel { Info, Ok, Warn, Error, Head }

    public class LogEntry
    {
        public string Text { get; set; }
        public Brush Color { get; set; }
        public FontWeight Weight { get; set; } = FontWeights.Normal;
    }

    public class Logger
    {
        public ObservableCollection<LogEntry> Entries { get; } = new();
        public event Action Added;
        private readonly string _file;
        private readonly StreamWriter _writer;
        private readonly object _writeLock = new object();

        // บัฟเฟอร์ดิบสำหรับหน้า Logs ในเว็บ UI ([time, level, msg])
        private static readonly object _rawLock = new object();
        private static readonly List<string[]> _raw = new();

        /// <summary>log ล่าสุดสูงสุด 400 แถว (thread-safe)</summary>
        public static List<string[]> Raw()
        {
            lock (_rawLock) return new List<string[]>(_raw);
        }

        public Logger(string file)
        {
            _file = file;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            _writer = new StreamWriter(file, append: true, System.Text.Encoding.UTF8) { AutoFlush = true };
        }

        public void Log(string msg, LogLevel lv = LogLevel.Info)
        {
            string ts = DateTime.Now.ToString("HH:mm:ss");
            lock (_writeLock) { try { _writer.WriteLine($"[{ts}] {msg}"); } catch { } }

            string lvlName = lv switch
            {
                LogLevel.Ok => "OK",
                LogLevel.Warn => "WARN",
                LogLevel.Error => "ERROR",
                _ => "INFO"
            };
            lock (_rawLock)
            {
                _raw.Add(new[] { ts, lvlName, msg.Trim() });
                if (_raw.Count > 400) _raw.RemoveAt(0);
            }

            string hex = lv switch
            {
                LogLevel.Ok    => "#4ADE80",
                LogLevel.Warn  => "#FBBF24",
                LogLevel.Error => "#F87171",
                LogLevel.Head  => "#F5C542",
                _              => "#9AA4B2"
            };

            var e = new LogEntry
            {
                Text = $"[{ts}]  {msg}",
                Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                Weight = lv == LogLevel.Head ? FontWeights.Bold : FontWeights.Normal
            };

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Entries.Add(e);
                if (Entries.Count > 800) Entries.RemoveAt(0);
                Added?.Invoke();
            });
        }

        public void Ok(string m)   => Log("  " + m, LogLevel.Ok);
        public void Warn(string m) => Log("  " + m, LogLevel.Warn);
        public void Err(string m)  => Log("  " + m, LogLevel.Error);
        public void Info(string m) => Log("  " + m, LogLevel.Info);
        public void Head(string m) => Log(m, LogLevel.Head);
    }
}