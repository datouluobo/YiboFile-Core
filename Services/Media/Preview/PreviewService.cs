using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Previews;
using System.Collections.Concurrent;
using YiboFile.Services.Navigation;
using YiboFile.ViewModels.Messaging;

namespace YiboFile.Services.Preview
{
    /// <summary>
    /// 文件预览服务
    /// 负责管理文件预览的加载、清除和事件处理
    /// </summary>
    public class PreviewService
    {
        private readonly IMessageBus _messageBus;
        private readonly Dispatcher _dispatcher;
        public PreviewService(
            IMessageBus messageBus,
            Dispatcher dispatcher)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            // Subscribe to preview requests
            _messageBus.Subscribe<PreviewRequestMessage>(m => 
            {
                // Ensure the background loading doesn't block the UI thread during creation or initial I/O checks
                System.Threading.Tasks.Task.Run(async () => await LoadFilePreviewAsync(m.FilePath, m.TargetPane));
            });
        }

        private readonly ConcurrentDictionary<PaneId, long> _generations = new ConcurrentDictionary<PaneId, long>();
        private readonly ConcurrentDictionary<PaneId, System.Threading.CancellationTokenSource> _ctsDict = new ConcurrentDictionary<PaneId, System.Threading.CancellationTokenSource>();

        /// <summary>
        /// 加载文件预览 (异步)
        /// </summary>
        public async System.Threading.Tasks.Task LoadFilePreviewAsync(string filePath, PaneId pane)
        {
            // Cancel previous work for this pane
            if (_ctsDict.TryGetValue(pane, out var oldCts))
            {
                oldCts?.Cancel();
                oldCts?.Dispose();
            }
            
            var newCts = new System.Threading.CancellationTokenSource();
            _ctsDict[pane] = newCts;
            var token = newCts.Token;

            // Increment generation to invalidate previous requests
            long generation = _generations.AddOrUpdate(pane, 1, (_, current) => current + 1);

            try
            {
                // Start loading immediately
                var loadingTask = YiboFile.Previews.PreviewFactory.CreateViewModelAsync(filePath, token);

                // Wait for either completion or small delay (50ms)
                // This prevents flickering for fast-loading files
                var delayTask = System.Threading.Tasks.Task.Delay(50, token);

                var completedTask = await System.Threading.Tasks.Task.WhenAny(loadingTask, delayTask);

                if (_generations.TryGetValue(pane, out var currentGen) && generation != currentGen || token.IsCancellationRequested) return;

                if (completedTask == delayTask)
                {
                    // Loading is taking longer than 50ms.
                    // Show "Loading..." / Empty state now.
                    _messageBus.Publish(new PreviewChangedMessage(null, pane));

                    // Await the actual load
                    var viewModel = await loadingTask;

                    if (_generations.TryGetValue(pane, out currentGen) && generation != currentGen || token.IsCancellationRequested) return;
                    _messageBus.Publish(new PreviewChangedMessage(viewModel, pane));
                }
                else
                {
                    // Loading finished quickly (<50ms).
                    // Update UI directly without clearing first (prevents flicker).
                    var viewModel = await loadingTask;

                    if (_generations.TryGetValue(pane, out currentGen) && generation != currentGen || token.IsCancellationRequested) return;
                    _messageBus.Publish(new PreviewChangedMessage(viewModel, pane));
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                if (_generations.TryGetValue(pane, out var currentGen) && generation != currentGen || token.IsCancellationRequested) return;

                _messageBus.Publish(new PreviewChangedMessage(new ErrorPreviewViewModel
                {
                    ErrorMessage = $"预览加载异常: {ex.Message}"
                }, pane));
            }
        }

        /// <summary>
        /// 清除预览内容
        /// </summary>
        public void ClearPreview(PaneId pane)
        {
            if (_ctsDict.TryGetValue(pane, out var oldCts))
            {
                oldCts?.Cancel();
                oldCts?.Dispose();
                _ctsDict.TryRemove(pane, out _);
            }

            // Increment generation to invalidate pending loads
            _generations.AddOrUpdate(pane, 1, (_, current) => current + 1);
            _messageBus.Publish(new PreviewChangedMessage(null, pane));
        }

        /// <summary>
        /// 处理预览区打开文件请求
        /// </summary>
        public void HandlePreviewOpenFileRequest(string filePath, PaneId pane)
        {
            _ = LoadFilePreviewAsync(filePath, pane);
        }
    }
}
