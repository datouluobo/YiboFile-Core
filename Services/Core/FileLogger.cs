using System;
using System.IO;

namespace YiboFile.Services.Core
{
    public static class FileLogger
    {
        private static string _logPath;
        private static object _lock = new object();

        static FileLogger()
        {
            string portableDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
            string logDir = Directory.Exists(portableDir) ? portableDir : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile");
            
            try
            {
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            catch { }
            
            _logPath = Path.Combine(logDir, "startup_debug.log");
        }

        public static void Log(string message)
        {
            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
                }
            }
            catch { }
        }

        public static void LogException(string context, Exception ex)
        {
            Log($"[ERROR] {context}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

