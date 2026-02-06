using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services.Config;

namespace YiboFile.Services.Tabs
{
    public partial class TabService
    {
        private Point _tabDragStartPoint;
        private PathTab _draggingTab;
        private bool _isDragging;

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

        #region 标签页 UI 操作 (Drag Helper)

        public void InitializeTabsDragDrop()
        {
            EnsureUi();
            try
            {
                var panel = _ui.TabManager?.TabsPanelControl;
                if (panel == null) return;
                panel.AllowDrop = true;
                panel.DragOver -= TabsPanel_DragOver;
                panel.Drop -= TabsPanel_Drop;
                panel.DragOver += TabsPanel_DragOver;
                panel.Drop += TabsPanel_Drop;
            }
            catch { }
        }

        private void TabsPanel_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("YiboFile_TabKey"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey)
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void TabsPanel_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent("YiboFile_TabKey")) return;
                var key = e.Data.GetData("YiboFile_TabKey") as string;
                if (string.IsNullOrEmpty(key) || _ui.TabManager?.TabsPanelControl == null) return;

                var panel = _ui.TabManager.TabsPanelControl;
                var mousePos = e.GetPosition(panel);
                var children = panel.Children.OfType<StackPanel>().ToList();
                int childrenCount = children.Count;

                int targetIndex = 0;
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = children[i] as FrameworkElement;
                    if (child == null) continue;
                    var pos = child.TransformToAncestor(panel).Transform(new Point(0, 0));
                    double mid = pos.X + child.ActualWidth / 2;
                    if (mousePos.X > mid) targetIndex = i + 1;
                }

                var tab = _tabs.FirstOrDefault(t => GetTabKey(t) == key);

                // 处理跨面板拖拽
                if (tab == null)
                {
                    if (_ui.OwnerWindow is MainWindow mainWindow)
                    {
                        TabService otherService = null;
                        if (this == mainWindow._tabService) otherService = mainWindow._secondTabService;
                        else if (this == mainWindow._secondTabService) otherService = mainWindow._tabService;

                        if (otherService != null)
                        {
                            var otherTab = otherService.Tabs.FirstOrDefault(t => otherService.GetTabKey(t) == key);
                            if (otherTab != null)
                            {
                                bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;

                                if (!isCopy)
                                {
                                    otherService.RemoveTab(otherTab);
                                }

                                PathTab newTab = null;
                                if (otherTab.Type == TabType.Library && otherTab.Library != null)
                                {
                                    OpenLibraryTab(otherTab.Library, forceNewTab: true, activate: true);
                                    newTab = ActiveTab;
                                }
                                else
                                {
                                    CreatePathTab(otherTab.Path, forceNewTab: true, skipValidation: true, activate: true);
                                    newTab = ActiveTab;
                                }

                                if (newTab == null) return;

                                bool isPinned = false;
                                if (e.Data.GetDataPresent("YiboFile_TabPinned"))
                                {
                                    isPinned = (bool)e.Data.GetData("YiboFile_TabPinned");
                                }
                                if (isPinned && !newTab.IsPinned)
                                {
                                    TogglePinTab(newTab);
                                }

                                int pinnedCount = _tabs.Count(t => t.IsPinned);
                                if (newTab.IsPinned) targetIndex = Math.Min(targetIndex, pinnedCount);
                                else targetIndex = Math.Max(targetIndex, pinnedCount);

                                UpdateTabOrderAfterDrag(newTab, targetIndex, pinnedCount);
                                UpdateTabWidths();
                                return;
                            }
                        }
                    }

                    TabService globalSourceService = null;
                    if (e.Data.GetDataPresent("YiboFile_SourceServiceHash"))
                    {
                        var hash = (int)e.Data.GetData("YiboFile_SourceServiceHash");
                        lock (_allInstances)
                        {
                            globalSourceService = _allInstances.FirstOrDefault(s => s.GetHashCode() == hash);
                        }
                    }

                    if (globalSourceService != null && globalSourceService != this)
                    {
                        var otherTab = globalSourceService.Tabs.FirstOrDefault(t => globalSourceService.GetTabKey(t) == key);
                        if (otherTab != null)
                        {
                            bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                            if (!isCopy) globalSourceService.RemoveTab(otherTab);

                            PathTab newTab = null;
                            if (otherTab.Type == TabType.Library && otherTab.Library != null)
                            {
                                OpenLibraryTab(otherTab.Library, forceNewTab: true, activate: true);
                                newTab = ActiveTab;
                            }
                            else
                            {
                                CreatePathTab(otherTab.Path, forceNewTab: true, skipValidation: true, activate: true);
                                newTab = ActiveTab;
                            }

                            if (newTab != null)
                            {
                                if (e.Data.GetDataPresent("YiboFile_TabPinned") && (bool)e.Data.GetData("YiboFile_TabPinned") && !newTab.IsPinned)
                                    TogglePinTab(newTab);

                                int pinnedCount = _tabs.Count(t => t.IsPinned);
                                UpdateTabOrderAfterDrag(newTab, targetIndex, pinnedCount);
                                UpdateTabWidths();
                            }
                            return;
                        }
                    }
                    return;
                }

                int pinnedCountLocal = _tabs.Count(t => t.IsPinned);
                if (tab.IsPinned) targetIndex = Math.Min(targetIndex, pinnedCountLocal);
                else targetIndex = Math.Max(targetIndex, pinnedCountLocal);

                int currentIndex = _tabs.IndexOf(tab);
                if (currentIndex == -1 || currentIndex == targetIndex) return;

                UpdateTabOrderAfterDrag(tab, targetIndex, pinnedCountLocal);
                UpdateTabWidths();
            }
            catch { }
        }

        #endregion
    }
}
