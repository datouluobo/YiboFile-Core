using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace YiboFile.Services.Shell
{
    /// <summary>
    /// Shell 动词执行器 - 直接调用常用 Shell 命令
    /// </summary>
    public interface IShellVerbExecutor
    {
        bool Execute(string verb, IEnumerable<string> paths);
        List<ShellVerbInfo> GetCommonVerbs(IEnumerable<string> paths);
    }

    public class ShellVerbExecutor : IShellVerbExecutor
    {
        // 常用 Shell 动词定义
        private static readonly Dictionary<string, ShellVerbInfo> CommonVerbs = new()
        {
            { "openas", new ShellVerbInfo("openas", "打开方式...", "🔧") },
            { "properties", new ShellVerbInfo("properties", "属性", "ℹ️") },
            { "cut", new ShellVerbInfo("cut", "剪切", "✂️") },
            { "copy", new ShellVerbInfo("copy", "复制", "📄") },
            { "delete", new ShellVerbInfo("delete", "删除", "🗑️") },
            { "rename", new ShellVerbInfo("rename", "重命名", "✏️") },
        };

        public bool Execute(string verb, IEnumerable<string> paths)
        {
            if (string.IsNullOrEmpty(verb) || paths == null || !paths.Any())
                return false;

            try
            {
                var pathList = paths.ToList();

                // 对于多文件选择，逐个执行
                foreach (var path in pathList)
                {
                    if (!File.Exists(path) && !Directory.Exists(path))
                        continue;

                    var sei = new SHELLEXECUTEINFO
                    {
                        cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
                        fMask = SEE_MASK_INVOKEIDLIST | SEE_MASK_FLAG_NO_UI,
                        lpVerb = verb,
                        lpFile = path,
                        nShow = SW_SHOWNORMAL
                    };

                    bool result = ShellExecuteEx(ref sei);

                    if (!result)
                    {
                        int error = Marshal.GetLastWin32Error();

                        // 如果失败，尝试使用 Process.Start 作为后备
                        TryFallbackExecution(verb, path);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public List<ShellVerbInfo> GetCommonVerbs(IEnumerable<string> paths)
        {
            var result = new List<ShellVerbInfo>();

            if (paths == null || !paths.Any())
                return result;

            var pathList = paths.ToList();
            bool hasFiles = pathList.Any(p => File.Exists(p));
            bool hasDirs = pathList.Any(p => Directory.Exists(p));
            bool isSingleSelection = pathList.Count == 1;

            // 根据选择类型返回适用的动词
            if (hasFiles)
            {
                result.Add(CommonVerbs["openas"]);
            }

            result.Add(CommonVerbs["properties"]);

            return result;
        }

        private void TryFallbackExecution(string verb, string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Verb = verb,
                    UseShellExecute = true,
                    ErrorDialog = false
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
            }
        }

        #region Win32 Interop

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHELLEXECUTEINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpVerb;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpFile;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIconOrMonitor;
            public IntPtr hProcess;
        }

        private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
        private const uint SEE_MASK_FLAG_NO_UI = 0x00000400;
        private const int SW_SHOWNORMAL = 1;

        #endregion
    }

    /// <summary>
    /// Shell 动词信息
    /// </summary>
    public class ShellVerbInfo
    {
        public string Verb { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }

        public ShellVerbInfo(string verb, string displayName, string icon)
        {
            Verb = verb;
            DisplayName = displayName;
            Icon = icon;
        }
    }
}
