using System.Windows;
using System.Windows.Controls.Primitives;
using YiboFile.Services.ClipboardHistory;

namespace YiboFile
{
    /// <summary>
    /// MainWindow 剪切板历史功能分部类
    /// </summary>
    public partial class MainWindow
    {
        private ClipboardHistoryService _clipboardHistoryService;

        /// <summary>
        /// 初始化剪切板历史服务
        /// </summary>
        private void InitializeClipboardHistory()
        {
            _clipboardHistoryService = ClipboardHistoryService.Instance;
            _clipboardHistoryService.StartListening(this);

            // 订阅面板粘贴事件
            var panel = FindName("ClipboardHistoryPanelControl") as Controls.ClipboardHistoryPanel;
            if (panel != null)
            {
                panel.ItemPasted += OnClipboardHistoryItemPasted;
            }
        }

        /// <summary>
        /// 剪切板历史项粘贴后关闭面板
        /// </summary>
        /// <summary>
        /// 剪切板历史项粘贴后关闭面板
        /// </summary>
        private void OnClipboardHistoryItemPasted(ClipboardHistoryItem item)
        {
            // 关闭剪切板面板 (切回 Default 模式)
            // Use Dispatcher to ensure thread safety
            Dispatcher.Invoke(() =>
            {
                if (NavigationRail != null)
                {
                    // Automatically switch back to path view after pasting
                    NavigationRail.SetActiveMode("Path");
                }
            });

            // 显示通知
            Services.Core.NotificationService.ShowSuccess($"已复制到剪切板");
        }

        // Legacy handler removed
        // private void ClipboardHistoryBtn_Click(object sender, RoutedEventArgs e) { ... }

        /// <summary>
        /// 清理剪切板历史服务
        /// </summary>
        private void CleanupClipboardHistory()
        {
            _clipboardHistoryService?.Dispose();
        }
    }
}

