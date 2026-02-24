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
        private readonly NavigationCoordinator _navigationCoordinator;
        private readonly SearchCacheService _searchCacheService;
        private readonly FileInfoService _secondFileInfoService;
        private readonly LibraryService _libraryService;

        private bool _secondTabEventsSubscribed = false;
        private bool _secondFileBrowserEventsInitialized = false;


        public LayoutEventHandler(
            IShellWindow window,
            IMessageBus messageBus,
            LayoutModule layoutModule,
            NavigationModeService navigationModeService,
            TabService secondTabService,
            WindowStateManager windowStateManager,
            NavigationCoordinator navigationCoordinator,
            SearchCacheService searchCacheService,
            FileInfoService secondFileInfoService,
            LibraryService libraryService)
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
        }

        public void Initialize()
        {
            // 订阅 MVVM 消息，实现桥接
            _messageBus?.Subscribe<LayoutModeChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => ApplyLayoutModeUI(m.Mode));
            });

            _messageBus?.Subscribe<DualListModeChangedMessage>(m =>
            {
                _window.Dispatcher.Invoke(() => SetDualListMode(m.IsEnabled));
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
                SetDualListMode(_layoutModule.IsDualListMode);

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
            if (!_layoutModule.IsDualListMode) return;
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
            if (!_layoutModule.IsDualListMode)
            {
                // 单列表模式：清除边框
                if (_window.FileBrowser?.FocusBorderControl != null) _window.FileBrowser.FocusBorderControl.BorderBrush = new SolidColorBrush(Colors.Transparent);
                if (_window.SecondFileBrowser?.FocusBorderControl != null) _window.SecondFileBrowser.FocusBorderControl.BorderBrush = new SolidColorBrush(Colors.Transparent);

                if (_window.FileBrowser != null) _window.FileBrowser.BorderThickness = new Thickness(0);
                if (_window.SecondFileBrowser != null) _window.SecondFileBrowser.BorderThickness = new Thickness(0);
                return;
            }

            // 双列表模式：显示焦点边框
            var focusBrush = new SolidColorBrush(Color.FromArgb(120, 0, 120, 215)); // 半透明蓝色
            var normalBrush = new SolidColorBrush(Colors.Transparent);
            var isSecondFocused = _layoutModule.IsSecondPaneFocused;

            if (_window.FileBrowser?.FocusBorderControl != null)
            {
                _window.FileBrowser.FocusBorderControl.BorderBrush = isSecondFocused ? normalBrush : focusBrush;
            }
            if (_window.FileBrowser != null) _window.FileBrowser.BorderThickness = new Thickness(0);

            if (_window.SecondFileBrowser?.FocusBorderControl != null)
            {
                _window.SecondFileBrowser.FocusBorderControl.BorderBrush = isSecondFocused ? focusBrush : normalBrush;
            }
            if (_window.SecondFileBrowser != null) _window.SecondFileBrowser.BorderThickness = new Thickness(0);
        }

        internal void SetDualListMode(bool enable)
        {
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
                    // 先绑定 UI 上下文
                    AttachSecondTabServiceUiContext();

                    // 然后应用实际配置
                    _secondTabService.UpdateConfig(ConfigurationService.Instance.Config);

                    // 通知 WindowStateManager
                    if (_windowStateManager != null)
                    {
                        _windowStateManager.SetSecondTabService(_secondTabService);
                        _windowStateManager.RestoreSecondaryTabs();
                    }
                }

                InitializeSecondFileBrowserEvents();
                LoadSecondFileBrowserContent();

                // 为副列表创建初始标签页
                EnsureSecondTabExists();
            }
        }

        internal void AttachSecondTabServiceUiContext()
        {
            if (_secondTabService == null || _window.SecondTabManager == null) return;

            var uiContext = new TabUiContext
            {
                FileBrowser = _window.SecondFileBrowser,
                TabManager = _window.SecondTabManager,
                Dispatcher = _window.Dispatcher,
                OwnerWindow = (_window as Window),
                GetConfig = () => ConfigurationService.Instance.Config,
                SaveConfig = (config) => ConfigurationService.Instance.SaveNow(),

                // FindResource removed from context
            };

            _secondTabService.AttachUiContext(uiContext);

            if (!_secondTabEventsSubscribed)
            {
                _secondTabEventsSubscribed = true;

                // _secondTabService.ActiveTabChanged += (s, tab) => SyncSecondUiWithActiveTab(tab); // Handled by message bus now

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
        }

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
                if (_secondTabService.ActiveTab.Type == TabType.Library && _secondTabService.ActiveTab.Library != null)
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
            if (!_layoutModule.IsDualListMode || _window.SecondFileBrowser == null) return;

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
            if (!_layoutModule.IsDualListMode || _window.SecondFileBrowser == null) return;
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
                YiboFile.DialogService.Error($"加载标签文件失败: {ex.Message}", owner: (System.Windows.Window)_window); // Corrected cast
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
                        MessageBox.Show("暂不支持直接打开压缩包内的文件。\n请先解压后再试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        public (Controls.FileBrowserControl browser, string path, Library library) GetActiveContext()
        {
            if (_layoutModule.IsDualListMode && _layoutModule.IsSecondPaneFocused && _window.SecondFileBrowser != null)
            {
                var secLib = _window.ViewModel?.SecondaryPane?.CurrentLibrary;
                return (_window.SecondFileBrowser, _window.ViewModel?.SecondaryPane?.CurrentPath, secLib);
            }
            return (_window.FileBrowser, _window.ViewModel.CurrentPath, _window.ViewModel.ActivePane?.CurrentLibrary);
        }

        public void RefreshActiveFileList()
        {
            if (_layoutModule.IsDualListMode && _layoutModule.IsSecondPaneFocused && _window.SecondFileBrowser != null)
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
