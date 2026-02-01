using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Previews;

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
        private readonly Action _loadCurrentDirectoryCallback;
        private readonly Action<string> _createTabCallback;

        /// <summary>
        /// 初始化 PreviewService
        /// </summary>
        public PreviewService(
            IMessageBus messageBus,
            Dispatcher dispatcher,
            Action loadCurrentDirectoryCallback,
            Action<string> createTabCallback)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _loadCurrentDirectoryCallback = loadCurrentDirectoryCallback ?? throw new ArgumentNullException(nameof(loadCurrentDirectoryCallback));
            _createTabCallback = createTabCallback ?? throw new ArgumentNullException(nameof(createTabCallback));

            // Subscribe to preview requests
            _messageBus.Subscribe<PreviewRequestMessage>(m => LoadFilePreviewAsync(m.FilePath));
        }

        private long _currentGeneration = 0;
        private System.Threading.CancellationTokenSource _currentCts;

        /// <summary>
        /// 加载文件预览 (异步)
        /// </summary>
        public async void LoadFilePreviewAsync(string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"[PreviewService] LoadFilePreviewAsync called for: '{filePath}'");
            System.Diagnostics.Debug.WriteLine($"[PreviewService] Call Stack: {new System.Diagnostics.StackTrace()}");

            // Cancel previous request
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = new System.Threading.CancellationTokenSource();
            var token = _currentCts.Token;

            // Increment generation to invalidate previous requests
            long generation = ++_currentGeneration;

            try
            {
                // Create the ViewModel via factory
                var viewModel = await YiboFile.Previews.PreviewFactory.CreateViewModelAsync(filePath, token);

                // Check if this request is still valid
                if (generation != _currentGeneration || token.IsCancellationRequested) return;

                // Publish the change message
                _messageBus.Publish(new PreviewChangedMessage(viewModel));
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                if (generation != _currentGeneration || token.IsCancellationRequested) return;

                _messageBus.Publish(new PreviewChangedMessage(new ErrorPreviewViewModel
                {
                    ErrorMessage = $"预览加载异常: {ex.Message}"
                }));
            }
        }

        /// <summary>
        /// 加载文件预览 (Legacy support)
        /// </summary>
        public void LoadFilePreview(FileSystemItem item)
        {
            LoadFilePreviewAsync(item.Path);
        }

        /// <summary>
        /// 清除预览内容
        /// </summary>
        public void ClearPreview()
        {
            // Cancel pending loads
            _currentCts?.Cancel();
            _currentCts?.Dispose();
            _currentCts = null;

            // Increment generation to invalidate pending loads
            _currentGeneration++;
            _messageBus.Publish(new PreviewChangedMessage(null));
        }

        /// <summary>
        /// 处理预览区打开文件请求
        /// </summary>
        public void HandlePreviewOpenFileRequest(string filePath)
        {
            LoadFilePreviewAsync(filePath);
        }
    }
}
