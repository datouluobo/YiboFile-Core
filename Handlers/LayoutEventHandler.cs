using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Controls;
using YiboFile.Models;
using YiboFile.Models.Navigation;
using YiboFile.Services.Config;
using YiboFile.Services.Core;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Navigation;
using YiboFile.Services.Tabs;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Modules;

using YiboFile.Interfaces;

using YiboFile.Services.Orchestration;
using YiboFile.Services.Search;
using YiboFile.Services.FileInfo; // Corrected namespace
using YiboFile.Services; // LibraryService

namespace YiboFile.Handlers
{
    public class LayoutEventHandler
    {
        private readonly IShellWindow _window;
        private readonly IMessageBus _messageBus;
        private readonly LayoutModule _layoutModule;

        // Injected Services
        private readonly NavigationModeService _navigationModeService;
        private readonly TabService _secondTabService;
        private readonly WindowStateManager _windowStateManager;
        private readonly INavigationCoordinator _navigationCoordinator;
        private readonly SearchCacheService _searchCacheService;
        private readonly FileInfoService _secondFileInfoService;
        private readonly LibraryService _libraryService;
        private readonly Services.UI.IDialogService _dialogService;

        private bool _secondTabEventsSubscribed = false;
        private bool _secondFileBrowserEventsInitialized = false;


        public LayoutEventHandler(
            IShellWindow window,
            IMessageBus messageBus,
            LayoutModule layoutModule,
            NavigationModeService navigationModeService,
            TabService secondTabService,
            WindowStateManager windowStateManager,
            INavigationCoordinator navigationCoordinator,
            SearchCacheService searchCacheService,
            FileInfoService secondFileInfoService,
            LibraryService libraryService,
            Services.UI.IDialogService dialogService = null)
        {
            _window = window;
            _messageBus = messageBus;
            _layoutModule = layoutModule;
            _navigationModeService = navigationModeService;
            _secondTabService = secondTabService;
            _windowStateManager = windowStateManager;
            _navigationCoordinator = navigationCoordinator;
            _searchCacheService = searchCacheService;
            _secondFileInfoService = secondFileInfoService;
            _libraryService = libraryService;
            _dialogService = dialogService ?? App.ServiceProvider?.GetRequiredService<Services.UI.IDialogService>();
        }

        public void Initialize()
        {
            // 订阅 MVVM 消息，实现桥接
            _messageBus?.Subscribe<LayoutModeChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => ApplyLayoutModeUI(m.Mode));
            });

            _messageBus?.Subscribe<DualPaneModeChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => SetDualPaneMode(m.IsEnabled));
            });

            _messageBus?.Subscribe<FocusedPaneChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() =>
                {
                    // 同步 UI 状态
                    UpdateFocusBorders();

                    // 将焦点设置到对应的文件列表
                    if (m.IsSecondPaneFocused)
                    {
                        _window.SecondFileBrowser?.FilesList?.Focus();
                    }
                    else
                    {
                        _window.FileBrowser?.FilesList?.Focus();
                    }
                });
            });

            // ═══ 跨面板预览协调 ═══
            _messageBus?.Subscribe<PreviewPaneVisibilityChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => OnPreviewPaneVisibilityChanged(m));
            });

            // ═══ 三态面板模式切换 ═══
            _messageBus?.Subscribe<PaneModeChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => OnPaneModeChanged(m.Mode));
            });

            // 焦点变更：在预览模式下需要同步切换 Preview 宿主
            _messageBus?.Subscribe<FocusedPaneChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => UpdateCrossPreviewForPreviewState());
            });

            // TabActiveChangedMessage subscription removed - handled by TabsModule -> RestoreNavigationStateMessage


            // 桥接到旧有的导航切换逻辑
            _messageBus?.Subscribe<NavigationModeChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => _navigationModeService?.SwitchNavigationMode(m.Mode));
            });

            // 应用初始 UI 状态
            if (_layoutModule != null)
            {
                ApplyLayoutModeUI(_layoutModule.CurrentLayoutMode);

                // 应用初始双列表状态（触发事件绑定和内容加载）
                SetDualPaneMode(_layoutModule.IsDualPaneMode);

                // 应用初始的三态面板模式 (例如恢复 Preview 模式的视图UI)
                OnPaneModeChanged(_layoutModule.CurrentPaneMode);

                // 核心焦点桥接：确保点击主面板任何区域都能同步焦点状态
                if (_window.FileBrowser != null)
                {
                    _window.FileBrowser.PreviewMouseDown += (s, e) => { if (_layoutModule.IsSecondPaneFocused) _layoutModule.SetFocusedPane(false); };
                }
                if (_window.TabManager != null)
                {
                    _window.TabManager.PreviewMouseDown += (s, e) => { if (_layoutModule.IsSecondPaneFocused) _layoutModule.SetFocusedPane(false); };
                }
            }
        }

        internal void SwitchLayoutModeByIndex(int modeIndex)
        {
            string mode = modeIndex switch
            {
                0 => "Focus",
                1 => "Work",
                2 => "Full",
                _ => null
            };

            if (mode != null)
            {
                _layoutModule?.SwitchLayoutMode(mode);
            }
        }

        internal void SwitchFocusedPane()
        {
            if (!_layoutModule.IsDualPaneMode) return;
            _layoutModule?.SwitchFocusedPane();
        }

        // 供 KeyboardEventHandler 调用的直接切换方法
        internal void SwitchFocusedPaneFromKeyboard()
        {
            SwitchFocusedPane();
        }

        private void ApplyLayoutModeUI(string mode)
        {
            _window.UpdateTabManagerMargin();
        }

        internal void UpdateTabManagerLayout()
        {
            var tabManager = _window.TabManager;
            if (tabManager != null && tabManager.Parent is Grid grid)
            {
                Grid.SetColumn(tabManager, 3);
                Grid.SetColumnSpan(tabManager, 1);
            }
            _window.UpdateTabManagerMargin();
        }

        internal void UpdateFocusBorders()
        {
            // The border styling is now fully handled by MVVM data binding (IsActive) in FileBrowserControl.xaml
            // Doing it in code-behind with hardcoded brushes caused visual bugs and conflicting borders with the grid splitters.
        }

        internal void SetDualPaneMode(bool enable)
        {
            // ═══ 直接操控 ColRight 的列宽 ═══
            var colRight = _window.ColRight;
            if (colRight != null)
            {
                if (enable)
                {
                    // 仅当宽度未分配时（如刚从单栏切换过来）才重置，保留用户配置和持久化宽度
                    colRight.MinWidth = 250;
                    if (colRight.Width.Value <= 0)
                    {
                        var cfg = YiboFile.Services.Config.ConfigurationService.Instance.Config;
                        if (cfg != null && cfg.ColRightWidth >= 250)
                        {
                            colRight.Width = new System.Windows.GridLength(cfg.ColRightWidth);
                        }
                        else
                        {
                            colRight.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                        }
                    }

                    if (_window.ColCenter != null && _window.ColCenter.Width.Value <= 0)
                    {
                        _window.ColCenter.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                    }
                }
                else
                {
                    // 折叠：设为 0 隐藏
                    colRight.MinWidth = 0;
                    colRight.Width = new System.Windows.GridLength(0);
                }
            }

            // 调整标签页布局
            UpdateTabManagerLayout();

            // 更新焦点边框
            UpdateFocusBorders();

            // 如果切换到双列表模式，初始化副列表
            if (enable && _window.SecondFileBrowser != null)
            {
                // 初始化副标签页服务内容（即便服务已由 Orchestrator 创建，仍需绑定 UI 上下文）
                if (!_secondTabEventsSubscribed && _window.SecondTabManager != null)
                {
                    // 通知 WindowStateManager
                    if (_windowStateManager != null)
                    {
                        _windowStateManager.SetSecondTabService(_secondTabService);
                        _windowStateManager.RestoreSecondaryTabs();
                    }

                    _secondTabEventsSubscribed = true;

                    _window.SecondTabManager.PreviewMouseDown += (s, e) =>
                    {
                        if (!_layoutModule.IsSecondPaneFocused)
                        {
                            _layoutModule.SetFocusedPane(true);
                        }
                    };

                    if (_window.SecondFileBrowser != null)
                    {
                        _window.SecondFileBrowser.PreviewMouseDown += (s, e) =>
                        {
                            if (!_layoutModule.IsSecondPaneFocused)
                            {
                                _layoutModule.SetFocusedPane(true);
                            }
                        };
                    }
                }

                InitializeSecondFileBrowserEvents();
                LoadSecondFileBrowserContent();

                // 为副列表创建初始标签页
                EnsureSecondTabExists();
            }

            if (!enable)
            {
                // 关闭双栏时，清除所有跨面板预览状态
                ClearAllCrossPreview();
            }
        }

        // ═══ 跨面板预览协调 ═══

        /// <summary>
        /// 处理预览面板可见性变更：实现跨面板预览逻辑。
        /// 确保整个窗口始终只有两个主要面板区域，不产生额外的内部列分割。
        /// </summary>
        private void OnPreviewPaneVisibilityChanged(PreviewPaneVisibilityChangedMessage msg)
        {
            var primaryHost = _window.PrimaryContentHost;
            var secondHost = _window.SecondContentHost;
            var vm = _window.ViewModel;

            if (primaryHost == null || secondHost == null || vm == null) return;

            if (!_layoutModule.IsDualPaneMode)
            {
                // ═ 单栏模式 ═
                // A栏（主面板）的文件列表显示在左侧，A栏的预览显示在右侧（利用ColRight）
                if (msg.Pane == PaneId.Main)
                {
                    if (msg.IsVisible)
                    {
                        // 展开 ColRight 并让 SecondContentHost 显示 A 的预览
                        if (_window.SplitterRight != null) _window.SplitterRight.Visibility = Visibility.Visible;
                        
                        var colRight = _window.ColRight;
                        if (colRight != null)
                        {
                            // 使用配置中的最后右侧面板宽度，或者默认 360
                            double configWidth = YiboFile.Services.Config.ConfigurationService.Instance.Config.RightPanelWidth;
                            colRight.MinWidth = 250;
                            colRight.Width = new System.Windows.GridLength(configWidth > 0 ? configWidth : 360);
                        }
                        
                        secondHost.SetCrossPreview(vm.PrimaryPane?.Preview);
                        if (vm.PrimaryPane != null) vm.PrimaryPane.IsInnerPreviewVisible = false;
                        FileLogger.Log("[LayoutEventHandler] 单栏模式: A 的预览在右侧打开");
                    }
                    else
                    {
                        // 关闭 ColRight
                        if (_window.SplitterRight != null) _window.SplitterRight.Visibility = Visibility.Collapsed;
                        
                        var colRight = _window.ColRight;
                        if (colRight != null)
                        {
                            colRight.MinWidth = 0;
                            colRight.Width = new System.Windows.GridLength(0);
                        }
                        
                        secondHost.SetCrossPreview(null);
                        if (vm.PrimaryPane != null) vm.PrimaryPane.IsInnerPreviewVisible = (_layoutModule.CurrentPaneMode != PaneMode.Preview);
                        FileLogger.Log("[LayoutEventHandler] 单栏模式: A 的预览关闭");
                    }
                    UpdateTabManagerLayout();
                }
                return;
            }

            // ═ 双栏模式 ═
            // A栏的预览在B栏位置打开，B栏的预览在A栏位置打开。
            if (msg.Pane == PaneId.Main)
            {
                // A栏（主面板）预览开关被切换
                if (msg.IsVisible)
                {
                    // A的预览在B的位置打开 → SecondContentHost 显示 A 的预览
                    secondHost.SetCrossPreview(vm.PrimaryPane?.Preview);
                    if (vm.PrimaryPane != null) vm.PrimaryPane.IsInnerPreviewVisible = false;
                    FileLogger.Log("[LayoutEventHandler] 跨面板预览: A→B 启用");
                }
                else
                {
                    // 关闭 → 恢复 B 的正常显示
                    secondHost.SetCrossPreview(null);
                    if (vm.PrimaryPane != null) vm.PrimaryPane.IsInnerPreviewVisible = (_layoutModule.CurrentPaneMode != PaneMode.Preview);
                    FileLogger.Log("[LayoutEventHandler] 跨面板预览: A→B 关闭");
                }
            }
            else if (msg.Pane == PaneId.Second)
            {
                // B栏（副面板）预览开关被切换
                if (msg.IsVisible)
                {
                    // B的预览在A的位置打开 → PrimaryContentHost 显示 B 的预览
                    primaryHost.SetCrossPreview(vm.SecondaryPane?.Preview);
                    if (vm.SecondaryPane != null) vm.SecondaryPane.IsInnerPreviewVisible = false;
                    FileLogger.Log("[LayoutEventHandler] 跨面板预览: B→A 启用");
                }
                else
                {
                    // 关闭 → 恢复 A 的正常显示
                    primaryHost.SetCrossPreview(null);
                    if (vm.SecondaryPane != null) vm.SecondaryPane.IsInnerPreviewVisible = (_layoutModule.CurrentPaneMode != PaneMode.Preview);
                    FileLogger.Log("[LayoutEventHandler] 跨面板预览: B→A 关闭");
                }
            }
        }

        /// <summary>
        /// 清除所有跨面板预览状态（切换到单栏模式时调用）。
        /// </summary>
        private void ClearAllCrossPreview()
        {
            var vm = _window.ViewModel;

            _window.PrimaryContentHost?.SetCrossPreview(null);
            _window.SecondContentHost?.SetCrossPreview(null);

            // 同时关闭两个面板的预览状态标志
            if (vm?.PrimaryPane?.Preview != null) vm.PrimaryPane.Preview.IsVisible = false;
            if (vm?.SecondaryPane?.Preview != null) vm.SecondaryPane.Preview.IsVisible = false;
        }

        /// <summary>
        /// 处理三态面板模式变更
        /// </summary>
        private void OnPaneModeChanged(PaneMode mode)
        {
            var primaryHost = _window.PrimaryContentHost;
            var secondHost = _window.SecondContentHost;
            var vm = _window.ViewModel;
            var colRight = _window.ColRight;

            switch (mode)
            {
                case PaneMode.Single:
                    // 回到单栏：关闭跨面板预览，折叠右侧列
                    ClearAllCrossPreview();
                    if (_window.SplitterRight != null) _window.SplitterRight.Visibility = Visibility.Collapsed;
                    if (_window.SecondFileBrowserContainer != null) _window.SecondFileBrowserContainer.Visibility = Visibility.Collapsed;
                    if (colRight != null)
                    {
                        colRight.MinWidth = 0;
                        colRight.Width = new System.Windows.GridLength(0);
                    }
                    UpdateTabManagerLayout();
                    FileLogger.Log("[LayoutEventHandler] 面板模式 → 单栏 (物理容器关闭)");
                    break;

                case PaneMode.DualPane:
                    // 双栏模式由 SetDualPaneMode 处理（LayoutModule 已发 DualPaneModeChangedMessage）
                    if (_window.SplitterRight != null) _window.SplitterRight.Visibility = Visibility.Visible;
                    if (_window.SecondFileBrowserContainer != null) _window.SecondFileBrowserContainer.Visibility = Visibility.Visible;
                    // 这里只需确保预览状态已清除
                    primaryHost?.SetCrossPreview(null);
                    secondHost?.SetCrossPreview(null);
                    FileLogger.Log("[LayoutEventHandler] 面板模式 → 双栏");
                    break;

                case PaneMode.Preview:
                    // 预览模式：展开右侧列，在非焦点栏显示焦点栏的预览
                    if (_window.SplitterRight != null) _window.SplitterRight.Visibility = Visibility.Visible;
                    if (_window.SecondFileBrowserContainer != null) _window.SecondFileBrowserContainer.Visibility = Visibility.Visible;
                    if (colRight != null)
                    {
                        colRight.MinWidth = 250;
                        // 仅当宽度未分配时才覆盖，保护持久化的像素宽度
                        if (colRight.Width.Value <= 0)
                        {
                            var cfg = YiboFile.Services.Config.ConfigurationService.Instance.Config;
                            if (cfg != null && cfg.ColRightWidth >= 250)
                            {
                                colRight.Width = new System.Windows.GridLength(cfg.ColRightWidth);
                            }
                            else
                            {
                                colRight.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                            }
                        }
                        
                        // 更新中间列
                        if (_window.ColCenter != null && _window.ColCenter.Width.Value <= 0)
                        {
                            _window.ColCenter.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                        }
                    }

                    UpdateCrossPreviewForPreviewState();

                    UpdateTabManagerLayout();
                    FileLogger.Log("[LayoutEventHandler] 面板模式 → 预览");
                    break;
            }

            // 更新底层各个面板的底侧内部预览窗口显示状态（处于主预览模式时，这些应该收起）
            if (vm != null)
            {
                if (vm.PrimaryPane != null) 
                    vm.PrimaryPane.IsInnerPreviewVisible = (mode != PaneMode.Preview) && (mode == PaneMode.DualPane || !vm.PrimaryPane.Preview.IsVisible);
                if (vm.SecondaryPane != null) 
                    vm.SecondaryPane.IsInnerPreviewVisible = (mode != PaneMode.Preview) && (mode == PaneMode.DualPane || !vm.SecondaryPane.Preview.IsVisible);
            }
        }

        /// <summary>
        /// 在预览模式下，根据焦点侧动态切换预览宿主。
        /// 若焦点在左，右侧显示预览；若焦点在右，左侧显示预览。
        /// </summary>
        private void UpdateCrossPreviewForPreviewState()
        {
            if (_layoutModule == null || _layoutModule.CurrentPaneMode != PaneMode.Preview) return;

            var primaryHost = _window.PrimaryContentHost;
            var secondHost = _window.SecondContentHost;
            var vm = _window.ViewModel;

            if (vm != null && primaryHost != null && secondHost != null)
            {
                if (_layoutModule.IsSecondPaneFocused)
                {
                    // 焦点在副面板（右列表）：这时左侧（主面板）显示副面板的预览，右侧显示正常列表内容
                    secondHost.SetCrossPreview(null);
                    primaryHost.SetCrossPreview(vm.SecondaryPane?.Preview);
                    FileLogger.Log("[LayoutEventHandler] 预览方向：左预览 + 右列表");
                }
                else
                {
                    // 焦点在主面板（左列表）：这时右侧（副面板）显示主面板的预览，左侧显示正常列表内容
                    primaryHost.SetCrossPreview(null);
                    secondHost.SetCrossPreview(vm.PrimaryPane?.Preview);
                    FileLogger.Log("[LayoutEventHandler] 预览方向：左列表 + 右预览");
                }
            }
        }

        // UI Context attachment moved to WindowOrchestrator

        // SyncSecondUiWithActiveTab and OnSecondActiveTabPropertyChanged removed.
        // Navigation synchronization is now handled by TabsModule publishing RestoreNavigationStateMessage
        // and PaneViewModel handling it.


        private void EnsureSecondTabExists()
        {
            if (_secondTabService == null) return;

            if (_secondTabService.Tabs.Count == 0)
            {
                var path = _window.ViewModel?.SecondaryPane?.CurrentPath ?? _window.ViewModel.CurrentPath;
                _secondTabService.CreatePathTab(path, forceNewTab: true, activate: true);
            }
        }

        private void LoadSecondFileBrowserContent()
        {
            if (_secondTabService?.ActiveTab != null)
            {
                if (_secondTabService.ActiveTab.ContentTypeId == TabContentTypes.Library && _secondTabService.ActiveTab.Library != null)
                {
                    LoadSecondFileBrowserLibrary(_secondTabService.ActiveTab.Library);
                    return;
                }
                else if (!string.IsNullOrEmpty(_secondTabService.ActiveTab.Path))
                {
                    LoadSecondFileBrowserDirectory(_secondTabService.ActiveTab.Path);
                    return;
                }
            }

            string targetPath = _window.ViewModel?.SecondaryPane?.CurrentPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                targetPath = _window.ViewModel.CurrentPath;
            }
            LoadSecondFileBrowserDirectory(targetPath);
        }

        internal void LoadSecondFileBrowserDirectory(string path)
        {
            _navigationCoordinator?.HandlePathNavigation(
                path,
                YiboFile.Models.Navigation.NavigationSource.External,
                YiboFile.Models.Navigation.ClickType.LeftClick,
                pane: PaneId.Second);
            UpdateFocusBorders();
        }

        private void LoadSecondFileBrowserLibrary(Library library)
        {
            if (library == null || _window.SecondFileBrowser == null) return;

            _navigationCoordinator?.HandleLibraryNavigation(
                library,
                YiboFile.Models.Navigation.ClickType.LeftClick,
                pane: PaneId.Second);

            // Legacy InfoService call - consider moving to MessageBus subscription if needed
            _window.Dispatcher.InvokeAsync(() =>
            {
                if (_window.SecondFileBrowser == null) return;
                _secondFileInfoService?.ShowLibraryInfo(library);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        internal void NavigateSecondaryPaneToLibrary(Library library)
        {
            if (!_layoutModule.IsDualPaneMode || _window.SecondFileBrowser == null) return;

            if (library == null)
            {
                if (ConfigurationService.Instance.Config.LastLibraryId > 0)
                {
                    library = _libraryService.GetLibrary(ConfigurationService.Instance.Config.LastLibraryId);
                }
                if (library == null)
                {
                    library = _libraryService.LoadLibraries().FirstOrDefault();
                }
            }

            if (library != null)
            {
                if (_window.ViewModel.SecondaryPane.NavigationMode == "Library" &&
                    _window.ViewModel.SecondaryPane.CurrentLibrary?.Id == library.Id)
                {
                    return;
                }

                LoadSecondFileBrowserLibrary(library);
            }
        }

        internal void NavigateSecondaryPaneToTag(TagViewModel tag)
        {
            if (!_layoutModule.IsDualPaneMode || _window.SecondFileBrowser == null) return;
            if (tag != null)
            {
                LoadSecondFileBrowserTag(tag);
            }
        }

        private void LoadSecondFileBrowserTag(TagViewModel tag)
        {
            if (tag == null || _window.SecondFileBrowser == null) return;

            if (_window.ViewModel.SecondaryPane.CurrentTag != tag || _window.ViewModel.SecondaryPane.NavigationMode != "Tag")
            {
                _window.ViewModel.SecondaryPane.NavigateTo($"tag://{tag.Name}");
            }

            try
            {
                _secondFileInfoService?.ShowFileInfo(new FileSystemItem
                {
                    Name = tag.Name,
                    Path = $"tag://{tag.Name}",
                    Type = "标签",
                    IsDirectory = true,
                    Size = "-",
                    Tags = tag.Name
                });
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"加载标签文件失败: {ex.Message}");
            }
        }

        private void InitializeSecondFileBrowserEvents()
        {
            if (_secondFileBrowserEventsInitialized || _window.SecondFileBrowser == null) return;
            _secondFileBrowserEventsInitialized = true;

            var browser = _window.SecondFileBrowser;

            browser.PathChanged += (s, newPath) =>
            {
                if (string.IsNullOrEmpty(newPath)) return;
                _navigationCoordinator?.HandlePathNavigation(newPath, YiboFile.Models.Navigation.NavigationSource.AddressBar, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second);
            };

            browser.BreadcrumbClicked += (s, path) =>
            {
                if (string.IsNullOrEmpty(path)) return;
                _navigationCoordinator?.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.Breadcrumb, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second);
            };

            browser.FilesPreviewMouseDoubleClick += SecondFileBrowser_FilesDoubleClick;

            browser.FilesPreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;
                var listView = browser.FilesList;
                if (listView == null) return;
                var hitResult = VisualTreeHelper.HitTest(listView, e.GetPosition(listView));
                if (hitResult == null) return;

                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is ListViewItem item && item.Content is FileSystemItem selectedItem && selectedItem.IsDirectory)
                    {
                        _navigationCoordinator?.NavigateAsync(new YiboFile.Models.Navigation.NavigationRequest
                        {
                            Target = YiboFile.Models.Navigation.NavigationTarget.FromPath(selectedItem.Path),
                            ForceNewTab = true,
                            Pane = PaneId.Second
                        });
                        e.Handled = true;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            };

            browser.FilesSelectionChanged += (s, e) =>
            {
                if (_window.SecondFileBrowser != null)
                {
                    _window.ViewModel?.SecondaryPane?.UpdateSelection(_window.SecondFileBrowser.FilesSelectedItems);
                }
            };

            browser.PreviewMouseDown += (s, e) =>
            {
                if (!_layoutModule.IsSecondPaneFocused) _layoutModule?.SetFocusedPane(true);
            };
            _window.FileBrowser.PreviewMouseDown += (s, e) =>
            {
                if (_layoutModule.IsSecondPaneFocused) _layoutModule?.SetFocusedPane(false);
            };

            browser.GotFocus += (s, e) =>
            {
                if (!_layoutModule.IsSecondPaneFocused) _layoutModule?.SetFocusedPane(true);
            };
            _window.FileBrowser.GotFocus += (s, e) =>
            {
                if (_layoutModule.IsSecondPaneFocused) _layoutModule?.SetFocusedPane(false);
            };
        }

        private void SecondFileBrowser_FilesDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_window.SecondFileBrowser.FilesSelectedItem is FileSystemItem item)
            {
                if (item.IsDirectory)
                {
                    _navigationCoordinator?.HandlePathNavigation(item.Path, YiboFile.Models.Navigation.NavigationSource.FolderClick, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second);
                }
                else
                {
                    var protocolInfo = Services.Core.ProtocolManager.Parse(item.Path);
                    if (protocolInfo.Type == ProtocolType.Archive)
                    {
                        _dialogService?.ShowInfo("暂不支持直接打开压缩包内的文件。\n请先解压后再试。", "提示");
                        return;
                    }
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = item.Path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        _dialogService?.ShowError($"无法打开文件: {ex.Message}");
                    }
                }
            }
        }

        public (Controls.FileBrowserControl browser, string path, Library library) GetActiveContext()
        {
            if (_layoutModule.IsDualPaneMode && _layoutModule.IsSecondPaneFocused && _window.SecondFileBrowser != null)
            {
                var secLib = _window.ViewModel?.SecondaryPane?.CurrentLibrary;
                return (_window.SecondFileBrowser, _window.ViewModel?.SecondaryPane?.CurrentPath, secLib);
            }
            return (_window.FileBrowser, _window.ViewModel.CurrentPath, _window.ViewModel.ActivePane?.CurrentLibrary);
        }

        public void RefreshActiveFileList()
        {
            if (_layoutModule.IsDualPaneMode && _layoutModule.IsSecondPaneFocused && _window.SecondFileBrowser != null)
            {
                if (_window.ViewModel?.SecondaryPane?.NavigationMode == "Library" && _window.ViewModel.SecondaryPane.CurrentLibrary != null)
                {
                    LoadSecondFileBrowserLibrary(_window.ViewModel.SecondaryPane.CurrentLibrary);
                }
                else if (!string.IsNullOrEmpty(_window.ViewModel?.SecondaryPane?.CurrentPath))
                {
                    LoadSecondFileBrowserDirectory(_window.ViewModel.SecondaryPane.CurrentPath);
                }
            }
            else
            {
                _window.RefreshFileList();
            }
        }
    }
}
