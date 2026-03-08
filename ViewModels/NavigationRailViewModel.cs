using System.Windows.Input;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Tabs;

using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 导航工具栏视图模型（混合架构 - 仅状态）
    /// 职责：存储 UI 状态，由 NavigationCoordinator 驱动
    /// </summary>
    public class NavigationRailViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private string _activeNavigationMode = "Path";
        private string _activeLayoutMode = "Work";
        private bool _isDualPaneMode = false;
        private bool _isLeftPanelCollapsed = false;
        private PaneMode _currentPaneMode = PaneMode.Single;

        public ObservableCollection<NavigationRailItem> TopItems { get; } = new();
        public ObservableCollection<NavigationRailItem> BottomItems { get; } = new();

        private readonly IConfigurationService _configService;

        public NavigationRailViewModel(IMessageBus messageBus, IConfigurationService configService)
        {
            _messageBus = messageBus;
            _configService = configService;

            // ✅ 命令仅发布请求消息，不处理业务逻辑
            NavigateToPathCommand = new RelayCommand(() =>
            {

                _messageBus.Publish(new RequestNavigationModeMessage("Path"));
            });
            NavigateToLibraryCommand = new RelayCommand(() =>
            {

                _messageBus.Publish(new RequestNavigationModeMessage("Library"));
            });
            NavigateToTagCommand = new RelayCommand(() =>
            {

                _messageBus.Publish(new RequestNavigationModeMessage("Tag"));
            });
            NavigateToTasksCommand = new RelayCommand(() => _messageBus.Publish(new OpenContentTabMessage(TabContentTypes.Tasks)));
            NavigateToBackupCommand = new RelayCommand(() => _messageBus.Publish(new OpenContentTabMessage(TabContentTypes.Backup)));
            NavigateToClipboardCommand = new RelayCommand(() => _messageBus.Publish(new OpenContentTabMessage(TabContentTypes.Clipboard)));

            ToggleSidebarCommand = new RelayCommand(() => _messageBus.Publish(new RequestSidebarToggleMessage()));
            ToggleDualPaneCommand = new RelayCommand(() => _messageBus.Publish(new RequestDualPaneToggleMessage()));
            CyclePaneModeCommand = new RelayCommand(() => _messageBus.Publish(new RequestPaneModeToggleMessage()));
            SwapPanesCommand = new RelayCommand(() => _messageBus.Publish(new RequestSwapPanesMessage()));

            OpenSettingsCommand = new RelayCommand(() => _messageBus.Publish(new OpenContentTabMessage(TabContentTypes.Settings)));
            OpenAboutCommand = new RelayCommand(() => _messageBus.Publish(new OpenContentTabMessage(TabContentTypes.About)));

            InitializeItems();
            UpdateActiveStates();
        }

        private void InitializeItems()
        {
            var dict = new Dictionary<string, NavigationRailItem>
            {
                { "Path", new NavigationRailItem { Id = "Path", IconKey = "Icon_Nav_Path", ToolTip = "文件路径", Command = NavigateToPathCommand } },
                { "Library", new NavigationRailItem { Id = "Library", IconKey = "Icon_Nav_Library", ToolTip = "库", Command = NavigateToLibraryCommand } },
                { "Tag", new NavigationRailItem { Id = "Tag", IconKey = "Icon_Nav_Tag", ToolTip = "标签", Command = NavigateToTagCommand, IsVisible = App.IsTagTrainAvailable } },
                { "Tasks", new NavigationRailItem { Id = "Tasks", IconKey = "Icon_Window_Tasks", ToolTip = "任务队列", Command = NavigateToTasksCommand } },
                { "Backup", new NavigationRailItem { Id = "Backup", IconKey = "Icon_Backup", ToolTip = "备份管理器", Command = NavigateToBackupCommand } },
                { "Clipboard", new NavigationRailItem { Id = "Clipboard", IconKey = "Icon_Clipboard", ToolTip = "剪贴板历史", Command = NavigateToClipboardCommand } },
                { "ToggleSidebar", new NavigationRailItem { Id = "ToggleSidebar", IconKey = "Icon_Layout_Work", ToolTip = "切换左侧栏显示/隐藏", Command = ToggleSidebarCommand } },
                { "PaneMode", new NavigationRailItem { Id = "PaneMode", IconKey = "Icon_DualPane", ToolTip = "单栏 → 双栏 → 预览", Command = CyclePaneModeCommand } },
                { "SwapPanes", new NavigationRailItem { Id = "SwapPanes", IconKey = "Icon_SwapHorizontal", ToolTip = "交换左右栏", Command = SwapPanesCommand } },
                { "Settings", new NavigationRailItem { Id = "Settings", IconKey = "Icon_Window_Settings", ToolTip = "设置", Command = OpenSettingsCommand } },
                { "About", new NavigationRailItem { Id = "About", IconKey = "Icon_Window_About", ToolTip = "关于", Command = OpenAboutCommand } }
            };

            var topKeys = _configService?.Config?.RailTopItems ?? new List<string> { "Path", "Library", "Tag", "Tasks", "Backup", "Clipboard" };
            var bottomKeys = _configService?.Config?.RailBottomItems ?? new List<string> { "ToggleSidebar", "PaneMode", "SwapPanes", "Settings", "About" };

            // 防止有丢失的数据被漏在字典里没显示
            var usedKeys = new HashSet<string>();

            foreach(var key in topKeys)
            {
                if(dict.TryGetValue(key, out var item))
                {
                    TopItems.Add(item);
                    usedKeys.Add(key);
                }
            }

            foreach(var key in bottomKeys)
            {
                if(dict.TryGetValue(key, out var item))
                {
                    BottomItems.Add(item);
                    usedKeys.Add(key);
                }
            }

            // Fallback for any items not found in config
            foreach(var kvp in dict)
            {
                if(!usedKeys.Contains(kvp.Key))
                {
                    TopItems.Add(kvp.Value);
                }
            }
        }

        public void SaveSettings()
        {
            if (_configService?.Config != null)
            {
                _configService.Config.RailTopItems = TopItems.Select(x => x.Id).ToList();
                _configService.Config.RailBottomItems = BottomItems.Select(x => x.Id).ToList();
                _configService.SaveNow();
            }
        }

        private void UpdateActiveStates()
        {
            foreach (var item in TopItems.Concat(BottomItems))
            {
                if (item.Id == "Path") item.IsActive = ActiveNavigationMode == "Path";
                else if (item.Id == "Library") item.IsActive = ActiveNavigationMode == "Library";
                else if (item.Id == "Tag") item.IsActive = ActiveNavigationMode == "Tag";
                else if (item.Id == "Tasks") item.IsActive = ActiveNavigationMode == "Tasks";
                else if (item.Id == "Backup") item.IsActive = ActiveNavigationMode == "Backup";
                else if (item.Id == "Clipboard") item.IsActive = ActiveNavigationMode == "Clipboard";
                
                else if (item.Id == "ToggleSidebar") 
                {
                    item.IsActive = false;
                    item.IconKey = _isLeftPanelCollapsed ? "Icon_Layout_Focus" : "Icon_Layout_Work";
                }
                
                else if (item.Id == "PaneMode")
                {
                    item.IsActive = _currentPaneMode != PaneMode.Single;
                    // 根据当前模式显示对应的图标
                    switch (_currentPaneMode)
                    {
                        case PaneMode.Single:
                            item.IconKey = "Icon_SinglePane";
                            item.ToolTip = "当前: 单栏 (点击切换到双栏)";
                            break;
                        case PaneMode.DualPane:
                            item.IconKey = "Icon_DualPane";
                            item.ToolTip = "当前: 双栏 (点击切换到预览)";
                            break;
                        case PaneMode.Preview:
                            item.IconKey = "Icon_Preview";
                            item.ToolTip = "当前: 预览 (点击切换到单栏)";
                            break;
                    }
                }
                else if (item.Id == "SwapPanes")
                {
                    item.IsActive = false;
                    // 单栏时禁用，双栏或预览模式允许
                    item.IsEnabled = _currentPaneMode != PaneMode.Single;
                }
                else item.IsActive = false;
            }
        }

        #region 状态属性（由 Coordinator 更新）

        /// <summary>
        /// 当前激活的导航模式
        /// </summary>
        public string ActiveNavigationMode
        {
            get => _activeNavigationMode;
            set 
            {
                if (SetProperty(ref _activeNavigationMode, value))
                {
                    UpdateActiveStates();
                }
            }
        }

        /// <summary>
        /// 当前激活的布局模式（兼容旧逻辑保留）
        /// </summary>
        public string ActiveLayoutMode
        {
            get => _activeLayoutMode;
            set
            {
                if (SetProperty(ref _activeLayoutMode, value))
                {
                    UpdateActiveStates();
                }
            }
        }

        /// <summary>
        /// 是否为双列表模式（兼容旧逻辑保留）
        /// </summary>
        public bool IsDualPaneMode
        {
            get => _isDualPaneMode;
            set
            {
                if (SetProperty(ref _isDualPaneMode, value))
                {
                    UpdateActiveStates();
                }
            }
        }

        /// <summary>
        /// 是否折叠左侧面板
        /// </summary>
        public bool IsLeftPanelCollapsed
        {
            get => _isLeftPanelCollapsed;
            set
            {
                if (SetProperty(ref _isLeftPanelCollapsed, value))
                {
                    UpdateActiveStates();
                }
            }
        }

        /// <summary>
        /// 当前面板模式（三态）
        /// </summary>
        public PaneMode CurrentPaneMode
        {
            get => _currentPaneMode;
            set
            {
                if (SetProperty(ref _currentPaneMode, value))
                {
                    UpdateActiveStates();
                }
            }
        }

        /// <summary>
        /// 标签功能是否可用
        /// </summary>
        public bool IsTagFeatureAvailable => App.IsTagTrainAvailable;

        #endregion

        #region 命令（仅发布消息）

        public ICommand NavigateToPathCommand { get; }
        public ICommand NavigateToLibraryCommand { get; }
        public ICommand NavigateToTagCommand { get; }
        public ICommand NavigateToTasksCommand { get; }
        public ICommand NavigateToBackupCommand { get; }
        public ICommand NavigateToClipboardCommand { get; }

        public ICommand ToggleSidebarCommand { get; }
        public ICommand ToggleDualPaneCommand { get; }
        public ICommand CyclePaneModeCommand { get; }
        public ICommand SwapPanesCommand { get; }

        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenAboutCommand { get; }

        #endregion
    }
}
