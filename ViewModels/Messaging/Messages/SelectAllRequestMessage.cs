using YiboFile.Services.Navigation;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 请求某个面板的全选操作
    /// </summary>
    public class SelectAllRequestMessage
    {
        public PaneId TargetPane { get; }

        public SelectAllRequestMessage(PaneId targetPane)
        {
            TargetPane = targetPane;
        }
    }
}
