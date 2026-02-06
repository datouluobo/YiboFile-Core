using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Models;
using YiboFile.Services.Core;

namespace YiboFile.Services.Tabs
{
    public partial class TabService
    {
        public async void SwitchToTab(PathTab tab)
        {
            EnsureUi();
            if (tab == null) return;

            // 更新标签页活动状态（数据驱动的核心）
            foreach (var t in _tabs)
            {
                t.IsActive = (t == tab);
            }

            // [SSOT] 只负责更新业务状态，不再通过委托强推 UI 更新
            // UI 同步由 MainWindow 订阅 ActiveTabChanged 实现
            tab.LastAccessTime = DateTime.Now;
            SetActiveTab(tab);

            // 如果路径无效，则在后台检测并关闭标签页
            if (tab.Type == TabType.Path)
            {
                try
                {
                    string p = tab.Path;
                    if (!string.IsNullOrEmpty(p) && !ProtocolManager.IsVirtual(p))
                    {
                        bool exists = await Task.Run(() =>
                        {
                            try { return Directory.Exists(p); }
                            catch { return false; }
                        });

                        if (!exists)
                        {
                            // 延迟关闭，避免在切换过程中立即销毁
                            await Task.Delay(500);
                            CloseTab(tab);
                            return;
                        }
                    }
                }
                catch { }
            }

            // 联动更新主界面文件列表
            if (_ui != null)
            {
                if (tab.Type == TabType.Library && tab.Library != null)
                {
                    _ui.SetCurrentLibrary?.Invoke(tab.Library);
                    _ui.LoadLibraryFiles?.Invoke(tab.Library);
                }
                else if (tab.Type == TabType.Path)
                {
                    _ui.SetCurrentLibrary?.Invoke(null);
                    _ui.NavigateToPathInternal?.Invoke(tab.Path);
                }

                _ui.UpdateNavigationButtonsState?.Invoke();
                _ui.TabManager?.RaiseCloseOverlayRequested();
            }
        }

        public void SetupLibraryTabs(Library library)
        {
            EnsureUi();
            if (library == null || _ui == null) return;

            // 移除现有的库标签（非固定）
            var toRemove = _tabs.Where(t => t.Type == TabType.Library && !t.IsPinned).ToList();
            foreach (var t in toRemove) CloseTab(t);

            // 创建新的库标签
            OpenLibraryTab(library, forceNewTab: false, activate: true);
        }

        public void ClearTabsInLibraryMode()
        {
            EnsureUi();
            if (_ui == null) return;
            var toRemove = _tabs.Where(t => t.Type == TabType.Library && !t.IsPinned).ToList();
            foreach (var t in toRemove) CloseTab(t);
        }

        private string GetTabOpenPath(PathTab tab)
        {
            if (tab == null) return null;
            if (tab.Type == TabType.Path) return tab.Path;
            if (tab.Type == TabType.Library && tab.Library != null) return tab.Library.Path;
            return null;
        }

        public void OpenTabInExplorer(PathTab tab)
        {
            EnsureUi();
            string path = GetTabOpenPath(tab);
            if (!string.IsNullOrEmpty(path))
            {
                try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); }
                catch { try { System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\""); } catch { } }
            }
        }

        public void CloseOtherTabs(PathTab tab)
        {
            EnsureUi();
            var toRemove = _tabs.Where(t => t != tab && !t.IsPinned).ToList();
            foreach (var t in toRemove) CloseTab(t);
        }

        public void CloseAllTabs()
        {
            EnsureUi();
            var toRemove = _tabs.Where(t => !t.IsPinned).ToList();
            foreach (var t in toRemove) CloseTab(t);

            if (TabCount == 0)
            {
                CreatePathTab(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            }
        }
    }
}
