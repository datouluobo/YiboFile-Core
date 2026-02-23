using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 任务队列标签页内容。
    /// 将现有的 TaskQueuePanel 包装为标签页内容。
    /// 取代原有的 ActiveSpecialPanel="Tasks" 机制。
    /// </summary>
    public class TaskQueueTabContent : ITabContent
    {
        private TaskQueuePanel _cachedView;

        public string Id => TabContentTypes.Tasks;
        public string Title => "任务队列";
        public string IconKey => "Icon_Window_Tasks";
        public bool AllowMultiple => false;
        public bool SupportsSecondaryPane => true;

        public UserControl CreateView()
        {
            if (_cachedView == null)
            {
                _cachedView = new TaskQueuePanel();
            }
            return _cachedView;
        }

        public void OnActivated()
        {
            // 任务队列激活时可刷新任务列表
            // TaskQueuePanel 内部通过数据绑定自动更新
        }

        public void OnDeactivated()
        {
            // 无需特殊处理，任务进度通过后台服务持续更新
        }

        public void OnClosed()
        {
            _cachedView = null;
        }
    }
}
