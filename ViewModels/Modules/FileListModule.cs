using System;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 文件列表模块
    /// 处理文件列表的刷新、选择变更等功能
    /// </summary>
    public class FileListModule : ModuleBase
    {
        private readonly Action _onRefreshCallback;
        private readonly Action _onClearFilterCallback;

        public override string Name => "FileList";

        public FileListModule(
            IMessageBus messageBus,
            Action onRefreshCallback = null,
            Action onClearFilterCallback = null)
            : base(messageBus)
        {
            _onRefreshCallback = onRefreshCallback;
            _onClearFilterCallback = onClearFilterCallback;
        }

        protected override void OnInitialize()
        {
            Subscribe<RefreshFileListMessage>(OnRefresh);
            Subscribe<ClearFilterMessage>(OnClearFilter);
            Subscribe<PathChangedMessage>(OnPathChanged);
        }

        #region 消息处理

        private void OnRefresh(RefreshFileListMessage message)
        {
            _onRefreshCallback?.Invoke();
        }

        private void OnClearFilter(ClearFilterMessage message)
        {
            _onClearFilterCallback?.Invoke();
        }

        private void OnPathChanged(PathChangedMessage message)
        {
            // 路径变更时自动刷新文件列表
            _onRefreshCallback?.Invoke();
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 刷新文件列表
        /// </summary>
        public void Refresh(string path = null)
        {
            Publish(new RefreshFileListMessage(path));
        }

        /// <summary>
        /// 清除过滤器
        /// </summary>
        public void ClearFilter()
        {
            Publish(new ClearFilterMessage());
        }

        /// <summary>
        /// 通知文件选择变更
        /// </summary>
        public void NotifySelectionChanged(System.Collections.IList selectedItems)
        {
            Publish(new FileSelectionChangedMessage(selectedItems));
        }

        #endregion
    }
}
