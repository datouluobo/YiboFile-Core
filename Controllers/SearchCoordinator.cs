using System;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Search;

namespace YiboFile.Controllers
{
    /// <summary>
    /// 搜索协调器（混合架构 - 仅处理复杂业务逻辑）
    /// 职责：处理搜索范围预设的复杂多状态协调逻辑
    /// </summary>
    public class SearchCoordinator : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly SearchViewModel _viewModel;

        public SearchCoordinator(
            IMessageBus messageBus,
            SearchViewModel viewModel)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // ✅ 仅订阅复杂业务逻辑消息
            _messageBus.Subscribe<RequestSetScopePresetMessage>(OnSetScopePresetRequested);
        }

        /// <summary>
        /// 处理设置搜索范围预设请求（复杂的多状态协调逻辑）
        /// </summary>
        private void OnSetScopePresetRequested(RequestSetScopePresetMessage message)
        {
            if (string.IsNullOrEmpty(message.Preset))
                return;

            // ✅ 业务逻辑：根据预设更新 ViewModel 多个相关状态
            switch (message.Preset)
            {
                case "AllScope":
                    // 全部范围：搜索所有内容
                    _viewModel.SearchNames = true;
                    _viewModel.SearchFolders = true;
                    _viewModel.SearchNotes = true;
                    _viewModel.SearchMode = SearchMode.All;
                    break;

                case "FileName":
                    // 文件名模式：仅搜索文件名，排除文件夹
                    _viewModel.SearchNames = true;
                    _viewModel.SearchFolders = false;
                    _viewModel.SearchNotes = false;
                    _viewModel.SearchMode = SearchMode.FileName;

                    // 业务规则：文件名模式下不能选择"仅文件夹"过滤器
                    if (_viewModel.TypeFilter == FileTypeFilter.Folders)
                        _viewModel.TypeFilter = FileTypeFilter.All;
                    break;

                case "Folder":
                    // 文件夹模式：仅搜索文件夹
                    _viewModel.SearchNames = false;
                    _viewModel.SearchFolders = true;
                    _viewModel.SearchNotes = false;
                    _viewModel.SearchMode = SearchMode.Folder;
                    _viewModel.TypeFilter = FileTypeFilter.Folders;
                    break;

                case "Notes":
                    // 备注模式：仅搜索备注，排除文件夹
                    _viewModel.SearchNames = false;
                    _viewModel.SearchFolders = false;
                    _viewModel.SearchNotes = true;
                    _viewModel.SearchMode = SearchMode.Notes;

                    // 业务规则：备注模式下不能选择"仅文件夹"过滤器
                    if (_viewModel.TypeFilter == FileTypeFilter.Folders)
                        _viewModel.TypeFilter = FileTypeFilter.All;
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine($"[SearchCoordinator] Unknown scope preset: {message.Preset}");
                    return;
            }

            System.Diagnostics.Debug.WriteLine($"[SearchCoordinator] Scope preset applied: {message.Preset}");
        }

        #region 公共方法（供外部调用）

        /// <summary>
        /// 设置目标面板 ID
        /// </summary>
        public void SetTargetPane(string paneId)
        {
            _viewModel.SetTargetPane(paneId);
        }

        /// <summary>
        /// 重置过滤器（委托给 ViewModel）
        /// </summary>
        public void ResetFilters()
        {
            _viewModel.ResetFilters();
        }

        #endregion

        public void Dispose()
        {
            // MessageBus 会自动处理取消订阅
        }
    }
}
