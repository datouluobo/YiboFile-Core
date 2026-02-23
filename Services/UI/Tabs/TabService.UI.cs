using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YiboFile.Controls;
using YiboFile.Dialogs;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Search;

namespace YiboFile.Services.Tabs
{
    public class TabUiContext
    {
        public FileBrowserControl FileBrowser { get; init; }
        public TabManagerControl TabManager { get; init; }
        public Dispatcher Dispatcher { get; init; }
        public System.Windows.Window OwnerWindow { get; init; }
        public Func<AppConfig> GetConfig { get; init; }
        public Action<AppConfig> SaveConfig { get; init; }
        public Func<Library> GetCurrentLibrary { get; init; }
        public Action<Library> SetCurrentLibrary { get; init; }
        public Action UpdateNavigationButtonsState { get; init; }
        public SearchService SearchService { get; init; }
        public Func<SearchCacheService> GetSearchCacheService { get; init; }
        public Func<SearchOptions> GetSearchOptions { get; init; }
        public Func<string> GetCurrentNavigationMode { get; init; }
    }

    public partial class TabService
    {
        public void AttachUiContext(TabUiContext context)
        {
            _ui = context;
            if (_ui?.GetConfig != null) _config = _ui.GetConfig();
            _widthCalculator = new TabWidthCalculator(_config, GetTabKey, GetPinnedTabWidth);
            InitializeTabsDragDrop();

            if (_ui?.TabManager != null)
            {
                _ui.TabManager.NewTabCommand = this.NewTabCommand;
                _ui.TabManager.UpdateTabWidthsCommand = this.UpdateTabWidthsCommand;
            }
        }

        private void EnsureUi()
        {
            if (_config == null && _ui?.GetConfig != null) _config = _ui.GetConfig();
        }

        public void CreatePathTab(string path, bool forceNewTab = false, bool skipValidation = false, bool activate = true)
        {
            EnsureUi();
            if (string.IsNullOrEmpty(path)) return;

            // Detect virtual protocols and delegate to specialized methods
            if (path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase))
            {
                CreateTagTab(path.Substring(6), forceNewTab, activate);
                return;
            }
            if (path.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
            {
                // 解析库名称并在专门的库标签页中打开
                string libraryName = path.Substring(6);
                // 这里我们假设可以通过名称找到库，或者直接创建一个临时的库标签项
                // 实际项目中可能需要 LibraryService 配合。
                // 现有的 OpenLibraryTab 接受 Library 对象，所以我们需要先获取库对象。
                // 如果库对象不方便获取，我们可以先按路径模式创建但显式指定类型。
                CreateLibraryTabByName(libraryName, forceNewTab, activate);
                return;
            }
            if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                CreateSearchTab(path, forceNewTab, activate);
                return;
            }
            // yibofile:// 协议用于特殊面板标签页（设置、关于等）
            if (path.StartsWith("yibofile://", StringComparison.OrdinalIgnoreCase))
            {
                var contentTypeId = path.Substring("yibofile://".Length).Trim();
                if (!string.IsNullOrEmpty(contentTypeId))
                {
                    CreateSpecialTab(contentTypeId, activate);
                }
                return;
            }

            if (!skipValidation && !ValidatePath(path, out string errorMessage))
            {
                YiboFile.DialogService.Warning(errorMessage);
                return;
            }

            if (!forceNewTab)
            {
                var existingTab = FindTabByPath(path);
                if (existingTab != null && existingTab.ContentTypeId == TabContentTypes.Path)
                {
                    if (activate) SwitchToTab(existingTab);
                    return;
                }
            }

            var newTab = new PathTab
            {
                ContentTypeId = TabContentTypes.Path,
                Type = TabType.Path,
                Path = path,
                Title = CalculateTabDisplayTitle(path)
            };

            CreateTabInternal(newTab, activate);
        }

        public void CreateTagTab(string tagName, bool forceNewTab = false, bool activate = true)
        {
            EnsureUi();
            if (string.IsNullOrEmpty(tagName)) return;

            string path = $"tag://{tagName}";

            if (!forceNewTab)
            {
                // Isomorphic reuse: Find existing Tag tab
                var existingTab = _tabs.FirstOrDefault(t => t.ContentTypeId == TabContentTypes.Tag && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
                if (existingTab != null)
                {
                    if (activate) SwitchToTab(existingTab);
                    return;
                }
            }

            var newTab = new PathTab
            {
                ContentTypeId = TabContentTypes.Tag,
                Type = TabType.Tag,
                Path = path,
                Title = tagName
            };

            CreateTabInternal(newTab, activate);
        }

        public void CreateSearchTab(string searchPath, bool forceNewTab = false, bool activate = true)
        {
            EnsureUi();
            if (string.IsNullOrEmpty(searchPath)) return;

            if (!forceNewTab)
            {
                var existingTab = _tabs.FirstOrDefault(t => t.ContentTypeId == TabContentTypes.Search && string.Equals(t.Path, searchPath, StringComparison.OrdinalIgnoreCase));
                if (existingTab != null)
                {
                    if (activate) SwitchToTab(existingTab);
                    return;
                }
            }

            string title = "搜索结果";
            if (searchPath.StartsWith("search://", StringComparison.OrdinalIgnoreCase)) title = "搜索: " + searchPath.Substring(9);
            else if (searchPath.StartsWith("content://", StringComparison.OrdinalIgnoreCase)) title = "内容: " + searchPath.Substring(10);

            var newTab = new PathTab
            {
                ContentTypeId = TabContentTypes.Search,
                Type = TabType.Search,
                Path = searchPath,
                Title = title
            };

            CreateTabInternal(newTab, activate);
        }

        public void CreateLibraryTabByName(string libraryName, bool forceNewTab = false, bool activate = true)
        {
            EnsureUi();
            if (string.IsNullOrEmpty(libraryName)) return;

            string path = $"lib://{libraryName}";

            if (!forceNewTab)
            {
                var existingTab = _tabs.FirstOrDefault(t => t.ContentTypeId == TabContentTypes.Library && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
                if (existingTab != null)
                {
                    if (activate) SwitchToTab(existingTab);
                    return;
                }
            }

            var newTab = new PathTab
            {
                ContentTypeId = TabContentTypes.Library,
                Type = TabType.Library,
                Path = path,
                Title = libraryName
            };

            CreateTabInternal(newTab, activate);
        }

        public PathTab CreateBlankTab()
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            CreatePathTab(desktopPath, forceNewTab: true);
            return ActiveTab as PathTab;
        }

        public void OpenLibraryTab(Library library, bool forceNewTab = false, bool activate = true)
        {
            EnsureUi();
            if (library == null) return;
            if (forceNewTab)
            {
                var tab = new PathTab
                {
                    ContentTypeId = TabContentTypes.Library,
                    Type = TabType.Library,
                    Path = $"lib://{library.Name}",
                    Title = library.Name,
                    Library = library
                };
                CreateTabInternal(tab, activate);
                return;
            }

            var window = TimeSpan.FromSeconds(_config?.ReuseTabTimeWindow ?? 10);
            var recentTab = FindRecentTab(t => t.ContentTypeId == TabContentTypes.Library && t.Library?.Id == library.Id, window);

            if (recentTab != null)
            {
                if (activate) SwitchToTab(recentTab);
                return;
            }

            var newTab = new PathTab
            {
                ContentTypeId = TabContentTypes.Library,
                Type = TabType.Library,
                Path = $"lib://{library.Name}",
                Title = library.Name,
                Library = library
            };

            CreateTabInternal(newTab, activate);
        }

        public void CloseTab(PathTab tab)
        {
            if (tab == null) return;

            // Rule 1: Last Global Tab Closure behavior (Preserve app instance)
            if (TabCount <= 1)
            {
                var homePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var homeType = TabType.Path;
                var homeTitle = CalculateTabDisplayTitle(homePath);



                if (tab.Path != homePath)
                {
                    tab.Path = homePath;
                    tab.Type = homeType;
                    tab.Title = homeTitle;
                    if (homeType == TabType.Library) tab.Library = null;

                    _messageBus.Publish(new YiboFile.ViewModels.Messaging.Messages.NavigateToPathMessage(
                        homePath, AddToHistory: false, Pane: this.Pane));
                }
                return;
            }





            // Determine next tab BEFORE removal to ensure type stability
            PathTab nextCandidate = null;
            if (tab.IsActive)
            {
                // Prefer switching to a sibling of the SAME type
                nextCandidate = _tabs.Where(t => t != tab && t.Type == tab.Type).LastOrDefault();
                // If none, fallback to any previous tab
                if (nextCandidate == null) nextCandidate = _tabs.Where(t => t != tab).LastOrDefault();
            }

            RemoveTab(tab);

            if (tab.IsActive && nextCandidate != null)
            {
                SwitchToTab(nextCandidate);
            }
        }

        public void CreateDuplicateTab(PathTab sourceTab = null)
        {
            var tabToDuplicate = sourceTab ?? ActiveTab;
            if (tabToDuplicate == null)
            {
                // Fallback to Desktop if no active tab
                CreatePathTab(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                return;
            }

            if (tabToDuplicate.Type == TabType.Library && tabToDuplicate.Library != null)
            {
                OpenLibraryTab(tabToDuplicate.Library, forceNewTab: true, activate: true);
            }
            else if (tabToDuplicate.Type == TabType.Tag)
            {
                // Assuming Tag support in TabService exists or will be added
                var newTab = new PathTab
                {
                    Type = TabType.Tag,
                    Path = tabToDuplicate.Path,
                    Title = tabToDuplicate.Title
                };
                CreateTabInternal(newTab, true);
            }
            else
            {
                CreatePathTab(tabToDuplicate.Path, forceNewTab: true, activate: true);
            }
        }

        private void CreateTabInternal(PathTab tab, bool activate = true)
        {
            EnsureUi();

            // 绑定关闭命令
            tab.CloseCommand = new YiboFile.ViewModels.RelayCommand(() => CloseTab(tab));

            // 绑定选择命令
            tab.SelectCommand = new YiboFile.ViewModels.RelayCommand(() => SwitchToTab(tab));

            // 添加到数据集合，ItemsControl 会自动感知并根据 DataTemplate 渲染
            AddTab(tab);

            // 应用标题覆盖
            ApplyTabOverrides(tab);

            if (activate) SwitchToTab(tab);
        }

        public void RenameDisplayTitle(PathTab tab)
        {
            EnsureUi();
            try
            {
                var newTitle = DialogService.ShowInput("请输入新的显示标题：", GetEffectiveTitle(tab), "输入", owner: _ui.OwnerWindow);
                if (newTitle != null) SetTabOverrideTitle(tab, newTitle.Trim());
            }
            catch { }
        }

        public void InitializeTabSizeHandler() { /* Managed by Command in View */ }

        private void UpdateTabWidths()
        {
            EnsureUi();
            // This method might still be used internally (e.g. after adding/removing tabs)
            // But it needs a width. We can try to get it from context if still needed, 
            // but the command is the primary driver now.
        }

        public void ApplyPinVisual(PathTab tab) { /* Managed by XAML */ }
        public void ReorderTabs() { /* Managed by ObservableCollection */ }
    }
}
