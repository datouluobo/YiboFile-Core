using System.Linq;
using YiboFile.Models;
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
        public override string Name => "FileList";

        public FileListModule(IMessageBus messageBus)
            : base(messageBus)
        {
        }

        protected override void OnInitialize()
        {
        }

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
            var items = selectedItems?.Cast<YiboFile.Models.FileSystemItem>().ToList();
            Publish(new FileSelectionChangedMessage(items));
        }

        #endregion
    }
}
