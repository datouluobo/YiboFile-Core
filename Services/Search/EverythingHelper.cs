using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace YiboFile.Services
{
    /// <summary>
    /// Everything 搜索服务包装类
    /// 提供 Everything SDK 的 C# 接口封装
    /// </summary>
    public static class EverythingHelper
    {
        private const string DLL_PATH = "Dependencies\\Everything\\Everything64.dll";
        private static IntPtr _dllHandle;
        private static Process _everythingProcess;
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();

        // Windows API 用于动态加载 DLL
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        // Everything API 函数指针委托定义（使用 stdcall 调用约定）
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate void SetSearchDelegate(string lpSearchString);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool QueryDelegate(bool bWait);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetNumResultsDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode)]
        private delegate void GetResultFullPathNameDelegate(int nIndex, StringBuilder lpString, int nMaxCount);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void ResetDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint GetLastErrorDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool IsDBLoadedDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetMaxDelegate(int max);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetOffsetDelegate(int offset);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetMajorVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int GetMinorVersionDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetMatchPathDelegate(bool bEnable);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetMatchCaseDelegate(bool bEnable);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void SetMatchWholeWordDelegate(bool bEnable);

        // 函数指针
        private static SetSearchDelegate _SetSearch;
        private static QueryDelegate _Query;
        private static GetNumResultsDelegate _GetNumResults;
        private static GetResultFullPathNameDelegate _GetResultFullPathName;
        private static ResetDelegate _Reset;
        private static GetLastErrorDelegate _GetLastError;
        private static IsDBLoadedDelegate _IsDBLoaded;
        private static SetMaxDelegate _SetMax;
        private static SetOffsetDelegate _SetOffset;
        private static GetMajorVersionDelegate _GetMajorVersion;
        private static GetMinorVersionDelegate _GetMinorVersion;
        private static SetMatchPathDelegate _SetMatchPath;
        private static SetMatchCaseDelegate _SetMatchCase;
        private static SetMatchWholeWordDelegate _SetMatchWholeWord;

        /// <summary>
        /// 加载 Everything DLL
        /// </summary>
        private static bool LoadEverythingDLL()
        {
            try
            {
                string fullDllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DLL_PATH);
                if (!File.Exists(fullDllPath))
                {
                    return false;
                }

                _dllHandle = LoadLibrary(fullDllPath);
                if (_dllHandle == IntPtr.Zero)
                {
                    return false;
                }

                // 获取函数地址 - Everything SDK 函数名（根据官方文档：https://www.voidtools.com/zh-cn/support/everything/sdk/）
                // 注意：字符串相关函数导出名区分 A/W，需要同时尝试
                var functions = new[]
                {
                    (new[] { "Everything_SetSearchW", "Everything_SetSearchA" }, typeof(SetSearchDelegate)),
                    (new[] { "Everything_QueryW", "Everything_QueryA" }, typeof(QueryDelegate)),
                    (new[] { "Everything_GetNumResults" }, typeof(GetNumResultsDelegate)),
                    (new[] { "Everything_GetResultFullPathNameW", "Everything_GetResultFullPathNameA" }, typeof(GetResultFullPathNameDelegate)),
                    (new[] { "Everything_Reset" }, typeof(ResetDelegate)),
                    (new[] { "Everything_GetLastError" }, typeof(GetLastErrorDelegate)),
                    (new[] { "Everything_IsDBLoaded" }, typeof(IsDBLoadedDelegate)),
                    (new[] { "Everything_SetMax" }, typeof(SetMaxDelegate)),
                    (new[] { "Everything_SetOffset" }, typeof(SetOffsetDelegate)),
                    (new[] { "Everything_GetMajorVersion" }, typeof(GetMajorVersionDelegate)),
                    (new[] { "Everything_GetMinorVersion" }, typeof(GetMinorVersionDelegate)),
                    (new[] { "Everything_SetMatchPath" }, typeof(SetMatchPathDelegate)),
                    (new[] { "Everything_SetMatchCase" }, typeof(SetMatchCaseDelegate)),
                    (new[] { "Everything_SetMatchWholeWord" }, typeof(SetMatchWholeWordDelegate))
                };

                foreach (var (funcNames, delegateType) in functions)
                {
                    IntPtr funcPtr = IntPtr.Zero;
                    string usedName = null;
                    foreach (var name in funcNames)
                    {
                        usedName = name;
                        funcPtr = GetProcAddress(_dllHandle, name);
                        if (funcPtr != IntPtr.Zero) break;
                    }
                    Debug.WriteLine($"EverythingHelper: 函数 {string.Join("/", funcNames)} 地址: {funcPtr}");

                    if (funcPtr == IntPtr.Zero)
                    {
                        Debug.WriteLine($"EverythingHelper: 警告 - 函数 {string.Join("/", funcNames)} 未找到");
                    }
                    else
                    {
                        try
                        {
                            switch (usedName)
                            {
                                case "Everything_SetSearchW":
                                case "Everything_SetSearchA":
                                    _SetSearch = Marshal.GetDelegateForFunctionPointer<SetSearchDelegate>(funcPtr);
                                    break;
                                case "Everything_QueryW":
                                case "Everything_QueryA":
                                case "Everything_Query":
                                    _Query = Marshal.GetDelegateForFunctionPointer<QueryDelegate>(funcPtr);
                                    break;
                                case "Everything_GetNumResults":
                                    _GetNumResults = Marshal.GetDelegateForFunctionPointer<GetNumResultsDelegate>(funcPtr);
                                    break;
                                case "Everything_GetResultFullPathNameW":
                                case "Everything_GetResultFullPathNameA":
                                    _GetResultFullPathName = Marshal.GetDelegateForFunctionPointer<GetResultFullPathNameDelegate>(funcPtr);
                                    break;
                                case "Everything_Reset":
                                    _Reset = Marshal.GetDelegateForFunctionPointer<ResetDelegate>(funcPtr);
                                    break;
                                case "Everything_GetLastError":
                                    _GetLastError = Marshal.GetDelegateForFunctionPointer<GetLastErrorDelegate>(funcPtr);
                                    break;
                                case "Everything_IsDBLoaded":
                                    _IsDBLoaded = Marshal.GetDelegateForFunctionPointer<IsDBLoadedDelegate>(funcPtr);
                                    break;
                                case "Everything_SetMax":
                                    _SetMax = Marshal.GetDelegateForFunctionPointer<SetMaxDelegate>(funcPtr);
                                    break;
                                case "Everything_SetOffset":
                                    _SetOffset = Marshal.GetDelegateForFunctionPointer<SetOffsetDelegate>(funcPtr);
                                    break;
                                case "Everything_GetMajorVersion":
                                    _GetMajorVersion = Marshal.GetDelegateForFunctionPointer<GetMajorVersionDelegate>(funcPtr);
                                    break;
                                case "Everything_GetMinorVersion":
                                    _GetMinorVersion = Marshal.GetDelegateForFunctionPointer<GetMinorVersionDelegate>(funcPtr);
                                    break;
                                case "Everything_SetMatchPath":
                                    _SetMatchPath = Marshal.GetDelegateForFunctionPointer<SetMatchPathDelegate>(funcPtr);
                                    break;
                                case "Everything_SetMatchCase":
                                    _SetMatchCase = Marshal.GetDelegateForFunctionPointer<SetMatchCaseDelegate>(funcPtr);
                                    break;
                                case "Everything_SetMatchWholeWord":
                                    _SetMatchWholeWord = Marshal.GetDelegateForFunctionPointer<SetMatchWholeWordDelegate>(funcPtr);
                                    break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                // 检查关键函数是否加载成功
                if (_SetSearch == null || _Query == null || _GetNumResults == null || _GetResultFullPathName == null)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 卸载 Everything DLL
        /// </summary>
        private static void UnloadEverythingDLL()
        {
            if (_dllHandle != IntPtr.Zero)
            {
                FreeLibrary(_dllHandle);
                _dllHandle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 初始化 Everything：检查并启动 Everything 服务
        /// </summary>
        public static async Task<bool> InitializeAsync()
        {
            lock (_lockObject)
            {
                if (_isInitialized)
                    return true;
            }

            try
            {
                // 1. 加载 DLL
                if (!LoadEverythingDLL())
                {
                    return false;
                }

                // 2. 检查 Everything 是否已在运行
                if (IsEverythingRunning())
                {
                    lock (_lockObject)
                    {
                        _isInitialized = true;
                    }
                    return true;
                }

                // 3. 尝试启动打包的 Everything
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string everythingPath = Path.Combine(appDir, "Dependencies", "Everything", "Everything.exe");

                if (!File.Exists(everythingPath))
                {
                    UnloadEverythingDLL();
                    return false;
                }

                // 启动 Everything（后台运行，根据命令行选项文档：-startup = "Run 'Everything' in the background"）
                var startInfo = new ProcessStartInfo
                {
                    FileName = everythingPath,
                    Arguments = "-startup", // 后台启动参数（官方文档确认）
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _everythingProcess = Process.Start(startInfo);

                if (_everythingProcess == null)
                {
                    return false;
                }

                Debug.WriteLine($"EverythingHelper: Everything 进程已启动 (PID: {_everythingProcess.Id})");

                // 等待 Everything 初始化（最多等待 5 秒）
                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(100);
                    if (IsEverythingRunning() && IsDBLoaded())
                    {
                        lock (_lockObject)
                        {
                            _isInitialized = true;
                        }
                        int major = GetMajorVersion();
                        int minor = GetMinorVersion();
                        Debug.WriteLine($"EverythingHelper: Everything 已成功启动并加载索引 (版本: {major}.{minor})");
                        return true;
                    }
                }

                UnloadEverythingDLL();
                return false;
            }
            catch
            {
                UnloadEverythingDLL();
                return false;
            }
        }

        /// <summary>
        /// 检查 Everything 是否正在运行
        /// </summary>
        public static bool IsEverythingRunning()
        {
            try
            {
                if (_dllHandle == IntPtr.Zero)
                {
                    if (!LoadEverythingDLL())
                    {
                        return false;
                    }
                }
                if (_IsDBLoaded == null) return false;
                return _IsDBLoaded();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 搜索文件
        /// </summary>
        public static List<string> SearchFiles(
            string searchText,
            int maxResults = 5000,
            string searchPath = null,
            bool matchCase = false,
            bool matchWholeWord = false)
        {
            var results = new List<string>();

            if (_dllHandle == IntPtr.Zero || _IsDBLoaded == null || !_IsDBLoaded())
            {
                throw new Exception("Everything 未运行，请先启动 Everything 程序");
            }

            try
            {
                // 设置搜索选项
                SetMatchCase(matchCase);
                SetMatchWholeWord(matchWholeWord);
                SetMatchPath(true); // 允许搜索路径

                // 构建搜索字符串
                string searchQuery = searchText;
                if (!string.IsNullOrEmpty(searchPath))
                {
                    // 限制搜索路径：path: 前缀
                    string normalizedPath = searchPath.TrimEnd('\\') + "\\";
                    searchQuery = $"path:{normalizedPath}* {searchText}";
                }

                Reset();
                SetMax(maxResults);
                _SetOffset?.Invoke(0);
                SetSearch(BuildEverythingQueryString(searchQuery));

                if (Query(true))
                {
                    int count = Math.Min(GetNumResults(), maxResults);
                    var sb = new StringBuilder(4096);

                    for (int i = 0; i < count; i++)
                    {
                        sb.Clear();
                        GetResultFullPathName(i, sb, sb.Capacity);
                        if (sb.Length > 0)
                        {
                            string filePath = sb.ToString();
                            results.Add(filePath);
                        }
                    }
                }
            }
            catch
            {
                throw;
            }

            return results;
        }

        public static List<string> SearchFilesPaged(
            string searchText,
            int offset,
            int pageSize,
            string searchPath = null,
            bool matchCase = false,
            bool matchWholeWord = false)
        {
            var results = new List<string>();
            if (_dllHandle == IntPtr.Zero || _IsDBLoaded == null || !_IsDBLoaded())
            {
                throw new Exception("Everything 未运行，请先启动 Everything 程序");
            }
            try
            {
                SetMatchCase(matchCase);
                SetMatchWholeWord(matchWholeWord);
                SetMatchPath(true);
                string searchQuery = searchText;
                if (!string.IsNullOrEmpty(searchPath))
                {
                    string normalizedPath = searchPath.TrimEnd('\\') + "\\";
                    searchQuery = $"path:{normalizedPath}* {searchText}";
                }
                Reset();
                SetMax(pageSize);
                _SetOffset?.Invoke(offset);
                SetSearch(BuildEverythingQueryString(searchQuery));
                if (Query(true))
                {
                    int count = Math.Min(GetNumResults(), pageSize);
                    var sb = new StringBuilder(4096);
                    for (int i = 0; i < count; i++)
                    {
                        sb.Clear();
                        GetResultFullPathName(i, sb, sb.Capacity);
                        if (sb.Length > 0)
                        {
                            results.Add(sb.ToString());
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
            return results;
        }

        private static string BuildEverythingQueryString(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return keyword;
            // 保留现有通配符；否则为每个词追加 * 作为前缀匹配
            var tokens = keyword.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var rebuilt = new List<string>();
            foreach (var t in tokens)
            {
                if (t.Contains("*") || t.Contains("?")) { rebuilt.Add(t); continue; }
                rebuilt.Add(t + "*");
            }
            return string.Join(" ", rebuilt);
        }

        /// <summary>
        /// 获取 Everything 版本信息
        /// </summary>
        public static string GetVersion()
        {
            try
            {
                if (IsEverythingRunning())
                {
                    int major = GetMajorVersion();
                    int minor = GetMinorVersion();
                    return $"{major}.{minor}";
                }
            }
            catch
            {
            }
            return "未知";
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Cleanup()
        {
            // 注意：不关闭 Everything 进程，因为可能被其他程序使用
            // 如果需要关闭，可以调用 _everythingProcess?.Kill()
            lock (_lockObject)
            {
                _isInitialized = false;
            }
            UnloadEverythingDLL();
        }

        // Everything API 包装方法
        private static void SetSearch(string lpSearchString) => _SetSearch?.Invoke(lpSearchString);
        private static bool Query(bool bWait) => _Query?.Invoke(bWait) ?? false;
        private static int GetNumResults() => _GetNumResults?.Invoke() ?? 0;
        private static void GetResultFullPathName(int nIndex, StringBuilder lpString, int nMaxCount) => _GetResultFullPathName?.Invoke(nIndex, lpString, nMaxCount);
        private static void Reset() => _Reset?.Invoke();
        private static uint GetLastError() => _GetLastError?.Invoke() ?? 0;
        private static bool IsDBLoaded() => _IsDBLoaded?.Invoke() ?? false;
        private static void SetMax(int max) => _SetMax?.Invoke(max);
        private static int GetMajorVersion() => _GetMajorVersion?.Invoke() ?? 0;
        private static int GetMinorVersion() => _GetMinorVersion?.Invoke() ?? 0;
        private static void SetMatchPath(bool bEnable) => _SetMatchPath?.Invoke(bEnable);
        private static void SetMatchCase(bool bEnable) => _SetMatchCase?.Invoke(bEnable);
        private static void SetMatchWholeWord(bool bEnable) => _SetMatchWholeWord?.Invoke(bEnable);

        /// <summary>
        /// 强制重建 Everything 索引
        /// </summary>
        public static void ForceRebuildIndex()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string everythingPath = Path.Combine(appDir, "Dependencies", "Everything", "Everything.exe");

                if (!File.Exists(everythingPath))
                {
                    // 尝试查找系统安装的 Everything? 暂不，只支持内置的
                    return;
                }

                // 发送 -rebuild 命令
                var startInfo = new ProcessStartInfo
                {
                    FileName = everythingPath,
                    Arguments = "-rebuild",
                    UseShellExecute = false
                };
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EverythingHelper: 重建索引失败 {ex.Message}");
            }
        }
    }
}

