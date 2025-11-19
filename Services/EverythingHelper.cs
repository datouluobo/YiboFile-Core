using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OoiMRR.Services
{
    /// <summary>
    /// Everything 搜索服务包装类
    /// 提供 Everything SDK 的 C# 接口封装
    /// </summary>
    public static class EverythingHelper
    {
        private const string DLL_NAME = "Everything64.dll";
        private static Process _everythingProcess;
        private static bool _isInitialized = false;
        private static readonly object _lockObject = new object();
        
        // Everything SDK API 声明
        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern int Everything_SetSearch(string lpSearchString);
        
        [DllImport(DLL_NAME)]
        private static extern bool Everything_Query(bool bWait);
        
        [DllImport(DLL_NAME)]
        private static extern int Everything_GetNumResults();
        
        [DllImport(DLL_NAME, CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathName(int nIndex, StringBuilder lpString, int nMaxCount);
        
        [DllImport(DLL_NAME)]
        private static extern void Everything_Reset();
        
        [DllImport(DLL_NAME)]
        private static extern uint Everything_GetLastError();
        
        [DllImport(DLL_NAME)]
        private static extern bool Everything_IsDBLoaded();
        
        [DllImport(DLL_NAME)]
        private static extern void Everything_SetMax(int max);
        
        [DllImport(DLL_NAME)]
        private static extern int Everything_GetMajorVersion();
        
        [DllImport(DLL_NAME)]
        private static extern int Everything_GetMinorVersion();
        
        [DllImport(DLL_NAME)]
        private static extern void Everything_SetMatchPath(bool bEnable);
        
        [DllImport(DLL_NAME)]
        private static extern void Everything_SetMatchCase(bool bEnable);
        
        [DllImport(DLL_NAME)]
        private static extern void Everything_SetMatchWholeWord(bool bEnable);
        
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
                // 1. 检查 Everything 是否已在运行
                if (IsEverythingRunning())
                {
                    lock (_lockObject)
                    {
                        _isInitialized = true;
                    }
                    Debug.WriteLine("EverythingHelper: Everything 已在运行");
                    return true;
                }
                
                // 2. 尝试启动打包的 Everything
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string everythingPath = Path.Combine(appDir, "Dependencies", "Everything", "Everything.exe");
                
                if (!File.Exists(everythingPath))
                {
                    Debug.WriteLine($"EverythingHelper: Everything.exe 不存在: {everythingPath}");
                    return false;
                }
                
                // 启动 Everything（以服务模式运行，后台静默）
                var startInfo = new ProcessStartInfo
                {
                    FileName = everythingPath,
                    Arguments = "-startup", // 启动参数：后台运行
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                
                _everythingProcess = Process.Start(startInfo);
                
                if (_everythingProcess == null)
                {
                    Debug.WriteLine("EverythingHelper: 无法启动 Everything 进程");
                    return false;
                }
                
                Debug.WriteLine($"EverythingHelper: Everything 进程已启动 (PID: {_everythingProcess.Id})");
                
                // 等待 Everything 初始化（最多等待 5 秒）
                for (int i = 0; i < 50; i++)
                {
                    await Task.Delay(100);
                    if (IsEverythingRunning() && Everything_IsDBLoaded())
                    {
                        lock (_lockObject)
                        {
                            _isInitialized = true;
                        }
                        int major = Everything_GetMajorVersion();
                        int minor = Everything_GetMinorVersion();
                        Debug.WriteLine($"EverythingHelper: Everything 已成功启动并加载索引 (版本: {major}.{minor})");
                        return true;
                    }
                }
                
                Debug.WriteLine("EverythingHelper: Everything 启动超时");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EverythingHelper: 初始化失败: {ex.Message}");
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
                Everything_Reset();
                Everything_SetSearch("");
                return Everything_Query(true);
            }
            catch (DllNotFoundException)
            {
                Debug.WriteLine("EverythingHelper: DLL 未找到，请确保 Everything64.dll 在正确位置");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EverythingHelper: 检查运行状态失败: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 搜索文件
        /// </summary>
        /// <param name="searchText">搜索文本</param>
        /// <param name="maxResults">最大结果数</param>
        /// <param name="searchPath">限制搜索路径（可选，如 "C:\\" 或 "D:\\Folder"）</param>
        /// <param name="matchCase">是否区分大小写</param>
        /// <param name="matchWholeWord">是否全字匹配</param>
        /// <returns>文件路径列表</returns>
        public static List<string> SearchFiles(
            string searchText, 
            int maxResults = 10000, 
            string searchPath = null,
            bool matchCase = false,
            bool matchWholeWord = false)
        {
            var results = new List<string>();
            
            if (!IsEverythingRunning())
            {
                throw new Exception("Everything 未运行，请先启动 Everything 程序");
            }
            
            try
            {
                // 设置搜索选项
                Everything_SetMatchCase(matchCase);
                Everything_SetMatchWholeWord(matchWholeWord);
                Everything_SetMatchPath(true); // 允许搜索路径
                
                // 构建搜索字符串
                string searchQuery = searchText;
                if (!string.IsNullOrEmpty(searchPath))
                {
                    // 限制搜索路径：path: 前缀
                    // 注意：Everything 的路径搜索需要完整路径，支持通配符
                    string normalizedPath = searchPath.TrimEnd('\\') + "\\";
                    searchQuery = $"path:{normalizedPath}* {searchText}";
                }
                
                Everything_Reset();
                Everything_SetMax(maxResults);
                Everything_SetSearch(searchQuery);
                
                if (Everything_Query(true))
                {
                    int count = Math.Min(Everything_GetNumResults(), maxResults);
                    var sb = new StringBuilder(260);
                    
                    for (int i = 0; i < count; i++)
                    {
                        sb.Clear();
                        Everything_GetResultFullPathName(i, sb, sb.Capacity);
                        if (sb.Length > 0)
                        {
                            string filePath = sb.ToString();
                            // 验证文件是否存在（Everything 可能返回已删除的文件）
                            if (File.Exists(filePath) || Directory.Exists(filePath))
                            {
                                results.Add(filePath);
                            }
                        }
                    }
                    
                    Debug.WriteLine($"EverythingHelper: 搜索 '{searchText}' 找到 {results.Count} 个结果");
                }
                else
                {
                    uint error = Everything_GetLastError();
                    Debug.WriteLine($"EverythingHelper: 搜索失败，错误代码: {error}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EverythingHelper: 搜索异常: {ex.Message}");
                throw;
            }
            
            return results;
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
                    int major = Everything_GetMajorVersion();
                    int minor = Everything_GetMinorVersion();
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
        }
    }
}

