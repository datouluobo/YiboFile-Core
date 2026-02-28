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
        private bool _isDualListMode = false;

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

            SetLayoutFocusCommand = new RelayCommand(() => _messageBus.Publish(new RequestLayoutModeMessage("Focus")));
            SetLayoutWorkCommand = new RelayCommand(() => _messageBus.Publish(new RequestLayoutModeMessage("Work")));
            SetLayoutFullCommand = new RelayCommand(() => _messageBus.Publish(new RequestLayoutModeMessage("Full")));
            ToggleDualListCommand = new RelayCommand(() => _messageBus.Publish(new RequestDualListToggleMessage()));

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
                { "Focus", new NavigationRailItem { Id = "Focus", IconKey = "Icon_Layout_Focus", ToolTip = "专注模式 (Ctrl+Shift+F)", Command = SetLayoutFocusCommand } },
                { "Work", new NavigationRailItem { Id = "Work", IconKey = "Icon_Layout_Work", ToolTip = "工作模式 (Ctrl+Shift+W)", Command = SetLayoutWorkCommand } },
                { "Full", new NavigationRailItem { Id = "Full", IconKey = "Icon_Layout_Full", ToolTip = "完整模式 (Ctrl+Shift+A)", Command = SetLayoutFullCommand } },
                { "DualList", new NavigationRailItem { Id = "DualList", IconKey = "Icon_DualList", ToolTip = "双列表模式", Command = ToggleDualListCommand } },
                { "Settings", new NavigationRailItem { Id = "Settings", IconKey = "Icon_Window_Settings", ToolTip = "设置", Command = OpenSettingsCommand } },
                { "About", new NavigationRailItem { Id = "About", IconKey = "Icon_Window_About", ToolTip = "关于", Command = OpenAboutCommand } }
            };

            var topKeys = _configService?.Config?.RailTopItems ?? new List<string> { "Path", "Library", "Tag", "Tasks", "Backup", "Clipboard" };
            var bottomKeys = _configService?.Config?.RailBottomItems ?? new List<string> { "Focus", "Work", "Full", "DualList", "Settings", "About" };

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
                
                else if (item.Id == "Focus") item.IsActive = ActiveLayoutMode == "Focus";
                else if (item.Id == "Work") item.IsActive = ActiveLayoutMode == "Work";
                else if (item.Id == "Full") item.IsActive = ActiveLayoutMode == "Full";
                
                else if (item.Id == "DualList") item.IsActive = IsDualListMode;
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
        /// 当前激活的布局模式
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
        /// 是否为双列表模式
        /// </summary>
        public bool IsDualListMode
        {
            get => _isDualListMode;
            set
            {
                if (SetProperty(ref _isDualListMode, value))
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

        public ICommand SetLayoutFocusCommand { get; }
        public ICommand SetLayoutWorkCommand { get; }
        public ICommand SetLayoutFullCommand { get; }
        public ICommand ToggleDualListCommand { get; }

        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenAboutCommand { get; }

        #endregion
    }
}
