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
        private void OnClipboardHistoryItemPasted(ClipboardHistoryItem item)
        {
            // 关闭 Popup
            var popup = FindName("ClipboardHistoryPopup") as Popup;
            if (popup != null)
                popup.IsOpen = false;

            // 显示通知
            Services.Core.NotificationService.ShowSuccess($"已复制到剪切板");
        }

        /// <summary>
        /// 剪切板历史按钮点击
        /// </summary>
        private void ClipboardHistoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var popup = FindName("ClipboardHistoryPopup") as Popup;
            if (popup != null)
            {
                popup.IsOpen = !popup.IsOpen;

                // 刷新列表
                if (popup.IsOpen)
                {
                    var panel = FindName("ClipboardHistoryPanelControl") as Controls.ClipboardHistoryPanel;
                    panel?.RefreshLists();
                }
            }
        }

        /// <summary>
        /// 清理剪切板历史服务
        /// </summary>
        private void CleanupClipboardHistory()
        {
            _clipboardHistoryService?.Dispose();
        }
    }
}

