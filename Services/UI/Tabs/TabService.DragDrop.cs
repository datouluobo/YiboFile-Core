using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services.Config;

namespace YiboFile.Services.Tabs
{
    public partial class TabService
    {

        #region 拖拽排序

        /// <summary>
        /// 更新标签页拖拽后的顺序
        /// </summary>
        public void UpdateTabOrderAfterDrag(PathTab draggedTab, int targetIndex, int pinnedCount)
        {
            if (draggedTab == null || !_tabs.Contains(draggedTab)) return;

            var pinned = _tabs.Where(t => t.IsPinned).ToList();
            var unpinned = _tabs.Where(t => !t.IsPinned).ToList();

            if (draggedTab.IsPinned)
            {
                pinned.Remove(draggedTab);
                targetIndex = Math.Min(targetIndex, pinnedCount);
                pinned.Insert(targetIndex, draggedTab);
                _config.PinnedTabs = pinned.Select(t => GetTabKey(t)).ToList();

                ConfigurationService.Instance.Set(cfg => cfg.PinnedTabs, _config.PinnedTabs);

                _tabs.Clear();
                foreach (var t in pinned.Concat(unpinned)) _tabs.Add(t);
            }
            else
            {
                int unTarget = Math.Max(0, targetIndex - pinnedCount);
                int unCurrent = unpinned.IndexOf(draggedTab);
                if (unCurrent == -1) return;
                unpinned.Remove(draggedTab);
                if (unTarget > unpinned.Count) unTarget = unpinned.Count;
                unpinned.Insert(unTarget, draggedTab);
                _tabs.Clear();
                foreach (var t in pinned.Concat(unpinned)) _tabs.Add(t);
            }
        }
        #endregion
    }
}
