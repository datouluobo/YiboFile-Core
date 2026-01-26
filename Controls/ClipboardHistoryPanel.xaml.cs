using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.Services.ClipboardHistory;

namespace YiboFile.Controls
{
    /// <summary>
    /// 剪切板历史面板
    /// </summary>
    public partial class ClipboardHistoryPanel : UserControl
    {
        private ClipboardHistoryService _historyService;

        /// <summary>
        /// 项目被选中粘贴事件
        /// </summary>
        public event System.Action<ClipboardHistoryItem> ItemPasted;

        public ClipboardHistoryPanel()
        {
            InitializeComponent();

            _historyService = ClipboardHistoryService.Instance;
            DataContext = _historyService;

            // 监听历史变化更新空状态
            _historyService.ClipboardChanged += _ => UpdateEmptyState();
            _historyService.History.CollectionChanged += (s, e) => UpdateEmptyState();

            Loaded += (s, e) => UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            Dispatcher.Invoke(() =>
            {
                bool isEmpty = !_historyService.History.Any();
                EmptyHint.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
                HistoryTabs.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            });
        }

        private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is ClipboardHistoryItem item)
            {
                PasteItem(item);
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ClipboardHistoryItem item)
            {
                _historyService.RemoveItem(item);
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            _historyService.ClearHistory();
        }

        private void PasteItem(ClipboardHistoryItem item)
        {
            if (_historyService.SetToClipboard(item))
            {
                ItemPasted?.Invoke(item);
            }
        }

        /// <summary>
        /// 刷新列表绑定
        /// </summary>
        public void RefreshLists()
        {
            FileHistoryList.Items.Refresh();
            TextHistoryList.Items.Refresh();
            UpdateEmptyState();
        }
    }
}

