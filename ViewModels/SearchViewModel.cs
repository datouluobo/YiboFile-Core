using System;
using System.Windows.Input;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 搜索视图模型
    /// 管理搜索输入、选项及历史记录
    /// </summary>
    public class SearchViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private string _searchText;
        private bool _searchNames = true;
        private bool _searchNotes = true;
        private string _targetPaneId = "Primary";

        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        /// <summary>
        /// 是否搜索文件名
        /// </summary>
        public bool SearchNames
        {
            get => _searchNames;
            set => SetProperty(ref _searchNames, value);
        }

        /// <summary>
        /// 是否搜索备注
        /// </summary>
        public bool SearchNotes
        {
            get => _searchNotes;
            set => SetProperty(ref _searchNotes, value);
        }

        /// <summary>
        /// 执行搜索命令
        /// </summary>
        public ICommand SearchCommand { get; }

        /// <summary>
        /// 清空搜索命令
        /// </summary>
        public ICommand ClearSearchCommand { get; }

        public SearchViewModel(IMessageBus messageBus)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            SearchCommand = new RelayCommand(ExecuteSearch);
            ClearSearchCommand = new RelayCommand(ClearSearch);
        }

        /// <summary>
        /// 设置目标面板 ID
        /// </summary>
        public void SetTargetPane(string paneId)
        {
            _targetPaneId = paneId;
        }

        private void ExecuteSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            // 发布执行搜索消息，由 SearchModule 处理
            _messageBus.Publish(new ExecuteSearchMessage(SearchText, SearchNames, SearchNotes, _targetPaneId));
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }
    }
}
