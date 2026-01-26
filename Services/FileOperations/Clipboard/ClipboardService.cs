using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 统一剪贴板服务 - 封装 Windows 系统剪贴板操作
    /// </summary>
    public class ClipboardService
    {
        private static ClipboardService _instance;
        public static ClipboardService Instance => _instance ??= new ClipboardService();

        /// <summary>
        /// 是否为剪切操作（用于视觉反馈）
        /// </summary>
        public bool IsCutOperation { get; private set; }

        /// <summary>
        /// 剪切的文件路径列表（用于视觉反馈）
        /// </summary>
        public IReadOnlyList<string> CutPaths { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// 剪切状态变化事件（用于 UI 刷新半透明效果）
        /// </summary>
        public event Action<IReadOnlyList<string>> CutStateChanged;

        /// <summary>
        /// 设置复制路径到剪贴板
        /// </summary>
        public async Task<bool> SetCopyPathsAsync(IEnumerable<string> paths)
        {
            Debug.WriteLine("[ClipboardService] SetCopyPathsAsync called");
            return await SetPathsToClipboardAsync(paths, false);
        }

        /// <summary>
        /// 设置剪切路径到剪贴板
        /// </summary>
        public async Task<bool> SetCutPathsAsync(IEnumerable<string> paths)
        {
            Debug.WriteLine("[ClipboardService] SetCutPathsAsync called");
            return await SetPathsToClipboardAsync(paths, true);
        }

        /// <summary>
        /// 从剪贴板获取路径和操作类型
        /// </summary>
        public async Task<(List<string> paths, bool isCut)> GetPathsFromClipboardAsync()
        {
            Debug.WriteLine("[ClipboardService] GetPathsFromClipboardAsync - START");
            try
            {
                // Retry checking for file drop list
                Debug.WriteLine("[ClipboardService] Checking ContainsFileDropList...");
                var containsFileDropList = await EnsureUIThreadAsync(async () =>
                {
                    return await RetryAsync(() => Clipboard.ContainsFileDropList(), "ContainsFileDropList");
                });

                Debug.WriteLine($"[ClipboardService] ContainsFileDropList = {containsFileDropList}");

                if (!containsFileDropList)
                {
                    Debug.WriteLine("[ClipboardService] No file drop list in clipboard, returning empty");
                    return (new List<string>(), false);
                }

                Debug.WriteLine("[ClipboardService] Getting file drop list...");
                return await EnsureUIThreadAsync(async () =>
                {
                    return await RetryAsync(() =>
                    {
                        var paths = new List<string>();
                        bool isCut = false;

                        var fileDropList = Clipboard.GetFileDropList();
                        Debug.WriteLine($"[ClipboardService] FileDropList count: {fileDropList?.Count ?? 0}");

                        foreach (string path in fileDropList)
                        {
                            paths.Add(path);
                            Debug.WriteLine($"[ClipboardService] Path: {path}");
                        }

                        // 检测是否为剪切操作
                        if (Clipboard.ContainsData("Preferred DropEffect"))
                        {
                            var data = Clipboard.GetData("Preferred DropEffect");
                            if (data is MemoryStream ms)
                            {
                                var bytes = ms.ToArray();
                                if (bytes.Length >= 4)
                                {
                                    int effect = BitConverter.ToInt32(bytes, 0);
                                    isCut = (effect == 2); // DROPEFFECT_MOVE
                                    Debug.WriteLine($"[ClipboardService] DropEffect = {effect}, isCut = {isCut}");
                                }
                            }
                        }

                        Debug.WriteLine($"[ClipboardService] Returning {paths.Count} paths, isCut = {isCut}");
                        return (paths, isCut);
                    }, "GetFileDropList");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClipboardService] GetPathsFromClipboardAsync EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[ClipboardService] StackTrace: {ex.StackTrace}");
                return (new List<string>(), false);
            }
        }

        /// <summary>
        /// 清除剪贴板
        /// </summary>
        public async Task ClearAsync()
        {
            Debug.WriteLine("[ClipboardService] ClearAsync called");
            try
            {
                await EnsureUIThreadAsync(async () =>
                {
                    return await RetryAsync(() =>
                    {
                        Clipboard.Clear();
                        return true;
                    }, "Clear");
                });
                IsCutOperation = false;
                CutPaths = Array.Empty<string>();
                CutStateChanged?.Invoke(CutPaths);
                Debug.WriteLine("[ClipboardService] ClearAsync completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClipboardService] ClearAsync EXCEPTION: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除剪切状态（粘贴后调用）
        /// </summary>
        public async Task ClearCutStateAsync()
        {
            Debug.WriteLine($"[ClipboardService] ClearCutStateAsync called, IsCutOperation = {IsCutOperation}");
            if (IsCutOperation)
            {
                await ClearAsync();
            }
        }

        private async Task<bool> SetPathsToClipboardAsync(IEnumerable<string> paths, bool isCut)
        {
            Debug.WriteLine($"[ClipboardService] SetPathsToClipboardAsync isCut={isCut}");
            try
            {
                var pathList = new List<string>(paths);
                if (pathList.Count == 0)
                {
                    Debug.WriteLine("[ClipboardService] Empty path list, returning false");
                    return false;
                }

                Debug.WriteLine($"[ClipboardService] Setting {pathList.Count} paths to clipboard");
                return await EnsureUIThreadAsync(async () =>
                {
                    return await RetryAsync(() =>
                    {
                        var data = new DataObject();
                        var fileDropList = new System.Collections.Specialized.StringCollection();
                        fileDropList.AddRange(pathList.ToArray());
                        data.SetFileDropList(fileDropList);

                        // 设置操作类型
                        int effect = isCut ? 2 : 5; // DROPEFFECT_MOVE or DROPEFFECT_COPY
                        var ms = new MemoryStream(BitConverter.GetBytes(effect));
                        data.SetData("Preferred DropEffect", ms);

                        Clipboard.SetDataObject(data, true);
                        Debug.WriteLine("[ClipboardService] SetDataObject completed");

                        // 更新内部状态
                        IsCutOperation = isCut;
                        CutPaths = isCut ? pathList : Array.Empty<string>();

                        // 触发事件通知 UI 更新
                        CutStateChanged?.Invoke(CutPaths);

                        return true;
                    }, "SetDataObject");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClipboardService] SetPathsToClipboardAsync EXCEPTION: {ex.Message}");
                return false;
            }
        }

        private T EnsureUIThread<T>(Func<T> action)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                return Application.Current.Dispatcher.Invoke(action);
            }
            return action();
        }

        private async Task<T> EnsureUIThreadAsync<T>(Func<Task<T>> action)
        {
            Debug.WriteLine($"[ClipboardService] EnsureUIThreadAsync - CheckAccess = {Application.Current?.Dispatcher?.CheckAccess()}");
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                Debug.WriteLine("[ClipboardService] Dispatching to UI thread...");
                // Await the Task<T> returned by InvokeAsync
                // InvokeAsync returns DispatcherOperation<Task<T>>
                return await await Application.Current.Dispatcher.InvokeAsync(action);
            }
            return await action();
        }

        private async Task<T> RetryAsync<T>(Func<T> action, string operationName, int maxRetries = 5, int delayMs = 50)
        {
            Debug.WriteLine($"[ClipboardService] RetryAsync '{operationName}' starting, maxRetries={maxRetries}");
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    var result = action();
                    Debug.WriteLine($"[ClipboardService] RetryAsync '{operationName}' succeeded on attempt {i + 1}");
                    return result;
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    Debug.WriteLine($"[ClipboardService] RetryAsync '{operationName}' attempt {i + 1} failed: {ex.Message}");
                    // If max retries reached, just return default (fail silently instead of crash)
                    if (i == maxRetries - 1)
                    {
                        Debug.WriteLine($"[ClipboardService] RetryAsync '{operationName}' MAX RETRIES REACHED, returning default");
                        break;
                    }
                    await Task.Delay(delayMs);
                }
            }
            return default;
        }
    }
}

