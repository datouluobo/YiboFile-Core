using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 统一剪贴板服务 - 封装 Windows 系统剪贴板操作
    /// </summary>
    public class ClipboardService
    {
        private static ClipboardService _instance;
        public static ClipboardService Instance => _instance ??= new ClipboardService();

        private IMessageBus _messageBus;

        /// <summary>
        /// 是否为剪切操作（用于视觉反馈）
        /// </summary>
        public bool IsCutOperation { get; private set; }

        /// <summary>
        /// 剪切的文件路径列表（用于视觉反馈）
        /// </summary>
        public IReadOnlyList<string> CutPaths { get; private set; } = Array.Empty<string>();

        private ClipboardService()
        {
            _messageBus = App.ServiceProvider?.GetService<IMessageBus>();
        }

        /// <summary>
        /// 设置消息总线
        /// </summary>
        public void SetMessageBus(IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        /// <summary>
        /// 设置复制路径到剪贴板
        /// </summary>
        public async Task<bool> SetCopyPathsAsync(IEnumerable<string> paths)
        {
            return await SetPathsToClipboardAsync(paths, false);
        }

        /// <summary>
        /// 设置剪切路径到剪贴板
        /// </summary>
        public async Task<bool> SetCutPathsAsync(IEnumerable<string> paths)
        {
            return await SetPathsToClipboardAsync(paths, true);
        }

        /// <summary>
        /// 从剪贴板获取路径和操作类型
        /// 优先使用内部缓存的剪切状态（不受系统剪贴板被清空影响）
        /// </summary>
        public async Task<(List<string> paths, bool isCut)> GetPathsFromClipboardAsync()
        {
            try
            {
                // 优先使用内部缓存（剪切操作时设置，不受系统剪贴板被清空影响）
                if (IsCutOperation && CutPaths.Count > 0)
                {
                    return (CutPaths.ToList(), true);
                }

                // Retry checking for file drop list
                var containsFileDropList = await EnsureUIThreadAsync(async () =>
                {
                    return await RetryAsync(() => Clipboard.ContainsFileDropList(), "ContainsFileDropList");
                });

                if (!containsFileDropList)
                {
                    return (new List<string>(), false);
                }

                return await EnsureUIThreadAsync(async () =>
                {
                    return await RetryAsync(() =>
                    {
                        var paths = new List<string>();
                        bool isCut = false;

                        var fileDropList = Clipboard.GetFileDropList();

                        foreach (string path in fileDropList)
                        {
                            paths.Add(path);
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
                                }
                            }
                        }

                        return (paths, isCut);
                    }, "GetFileDropList");
                });
            }
            catch (Exception)
            {
                return (new List<string>(), false);
            }
        }

        /// <summary>
        /// 清除剪贴板
        /// </summary>
        public async Task ClearAsync()
        {
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
                _messageBus?.Publish(new ClipboardCutStateChangedMessage(CutPaths));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 清除剪切状态（粘贴后调用）
        /// </summary>
        public async Task ClearCutStateAsync()
        {
            if (IsCutOperation)
            {
                await ClearAsync();
            }
        }

        private async Task<bool> SetPathsToClipboardAsync(IEnumerable<string> paths, bool isCut)
        {
            try
            {
                var pathList = new List<string>(paths);
                if (pathList.Count == 0)
                {
                    return false;
                }

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

                        // 先更新内部状态（即使 SetDataObject 失败，内部缓存也保留）
                        IsCutOperation = isCut;
                        CutPaths = isCut ? pathList : Array.Empty<string>();
                        _messageBus?.Publish(new ClipboardCutStateChangedMessage(CutPaths));

                        // 再尝试设置系统剪贴板（失败不影响内部状态）
                        try { Clipboard.SetDataObject(data, true); } catch { }

                        return true;
                    }, "SetDataObject");
                });
            }
            catch (Exception)
            {
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
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                // Await the Task<T> returned by InvokeAsync
                // InvokeAsync returns DispatcherOperation<Task<T>>
                return await await Application.Current.Dispatcher.InvokeAsync(action);
            }
            return await action();
        }

        private async Task<T> RetryAsync<T>(Func<T> action, string operationName, int maxRetries = 5, int delayMs = 50)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return action();
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // If max retries reached, just return default (fail silently instead of crash)
                    if (i == maxRetries - 1)
                    {
                        break;
                    }
                    await Task.Delay(delayMs);
                }
            }
            return default;
        }
    }
}

