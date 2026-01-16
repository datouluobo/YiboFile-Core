using System;
using System.IO;

namespace YiboFile.Services.Core
{
    public static class FileLogger
    {
        private static string _logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_debug.log");
        private static object _lock = new object();

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

