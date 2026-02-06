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
        public Window OwnerWindow { get; init; }
        public Func<AppConfig> GetConfig { get; init; }
        public Action<AppConfig> SaveConfig { get; init; }
        public Func<Library> GetCurrentLibrary { get; init; }
        public Action<Library> SetCurrentLibrary { get; init; }
        public Func<string> GetCurrentPath { get; init; }
        public Action<string> SetCurrentPath { get; init; }
        public Action<string> SetNavigationCurrentPath { get; init; }
        public Action<Library> LoadLibraryFiles { get; init; }
        public Action<string> NavigateToPathInternal { get; init; }
        public Action UpdateNavigationButtonsState { get; init; }
        public SearchService SearchService { get; init; }
        public Func<SearchCacheService> GetSearchCacheService { get; init; }
        public Func<SearchOptions> GetSearchOptions { get; init; }
        public Func<List<FileSystemItem>> GetCurrentFiles { get; init; }
        public Action<List<FileSystemItem>> SetCurrentFiles { get; init; }
        public Action ClearFilter { get; init; }
        public Func<string, Task> RefreshSearchTab { get; init; }
        public Func<string, object> FindResource { get; init; }
        public Services.Features.ITagService TagService { get; init; }
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
            if (!skipValidation && !ValidatePath(path, out string errorMessage))
            {
                YiboFile.DialogService.Warning(errorMessage);
                return;
            }

            if (!forceNewTab)
            {
                var existingTab = FindTabByPath(path);
                if (existingTab != null)
                {
                    if (activate) SwitchToTab(existingTab);
                    return;
                }
            }

            var newTab = new PathTab
            {
                Type = TabType.Path,
                Path = path,
                Title = CalculateTabDisplayTitle(path)
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
                    Type = TabType.Library,
                    Path = $"lib://{library.Name}",
                    Title = library.Name,
                    Library = library
                };
                CreateTabInternal(tab, activate);
                return;
            }

            var window = TimeSpan.FromSeconds(_config?.ReuseTabTimeWindow ?? 10);
            var recentTab = FindRecentTab(t => t.Type == TabType.Library && t.Library?.Id == library.Id, window);

            if (recentTab != null)
            {
                if (activate) SwitchToTab(recentTab);
                return;
            }

            var newTab = new PathTab
            {
                Type = TabType.Library,
                Path = $"lib://{library.Name}",
                Title = library.Name,
                Library = library
            };

            CreateTabInternal(newTab, activate);
        }

        public void CloseTab(PathTab tab)
        {
            EnsureUi();
            if (tab == null) return;
            if (!CanCloseTab(tab, _ui.GetCurrentLibrary?.Invoke() != null)) return;

            bool wasActive = (tab == _activeTab);
            RemoveTab(tab);

            if (wasActive)
            {
                var remainingTabs = GetTabsInOrder();
                if (remainingTabs.Count > 0) SwitchToTab(remainingTabs[0]);
                else CreatePathTab(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
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
