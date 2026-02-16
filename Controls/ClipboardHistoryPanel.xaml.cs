using System.Windows.Controls;
using YiboFile.Services.ClipboardHistory;
using YiboFile.ViewModels;

namespace YiboFile.Controls
{
    /// <summary>
    /// 剪切板历史面板
    /// </summary>
    public partial class ClipboardHistoryPanel : UserControl
    {
        private readonly ClipboardViewModel _viewModel;

        /// <summary>
        /// 项目被选中粘贴事件
        /// </summary>
        public event System.Action<ClipboardHistoryItem> ItemPasted;

        public ClipboardHistoryPanel()
        {
            InitializeComponent();
            _viewModel = new ClipboardViewModel();
            DataContext = _viewModel;
            _viewModel.ItemPasted += OnItemPasted;
        }

        private void OnItemPasted(ClipboardHistoryItem item)
        {
            ItemPasted?.Invoke(item);
        }

        /// <summary>
        /// 刷新列表绑定
        /// </summary>
        public void RefreshLists()
        {
            // View auto-refreshes due to ObservableCollection and ICollectionView
        }
    }
}
