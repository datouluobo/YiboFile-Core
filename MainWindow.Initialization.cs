using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Search;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Tabs;
using YiboFile.Services.Preview;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Config;
using YiboFile.Services.Archive; // Import Archive Service
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models.Navigation;


using YiboFile.Helpers;
using YiboFile.Handlers;
using YiboFile.Models.UI;
using System.ComponentModel;

namespace YiboFile
{
    public partial class MainWindow
    {
        internal List<FileSystemItem> _secondCurrentFiles = new List<FileSystemItem>();

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Global mouse down logic is now handled within individual controls (FileBrowserControl)

            // Apply the same global mouse down logic for the Secondary File Browser
            // If the Secondary Address Bar is in edit mode and the click is outside it, close edit mode.
            if (SecondFileBrowser != null && SecondFileBrowser.AddressBarControl != null &&
                SecondFileBrowser.AddressBarControl.IsEditMode)
            {
                var source = e.OriginalSource as DependencyObject;
                bool isAddressBar = false;

                // Check if the click target is within the AddressBarControl
                var current = source;
                while (current != null)
                {
                    if (current == SecondFileBrowser.AddressBarControl)
                    {
                        isAddressBar = true;
                        break;
                    }
                    if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                    {
                        current = VisualTreeHelper.GetParent(current);
                    }
                    else if (current is FrameworkContentElement fce)
                    {
                        current = fce.Parent;
                    }
                    else
                    {
                        current = null;
                    }
                }

                if (!isAddressBar)
                {
                    // If clicked outside, exit edit mode
                    SecondFileBrowser.AddressBarControl.SwitchToBreadcrumbMode();
                }
            }
        }

        // ... existing codes ...


        // 响应式布局现在由 FileListControl 内部的 ListView.SizeChanged 处理
        // 此方法已废弃
        /*
        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 将 ColCenter 的实际宽度传递给 FileListControl 进行响应式布局
            if (FileBrowser?.FileList != null && ColCenter != null)
            {
                FileBrowser.FileList.ApplyResponsiveLayout(ColCenter.ActualWidth);
            }
        }
        */



        private void InitializeEvents()
        {
            // 全局鼠标事件
            this.PreviewMouseDown += MainWindow_PreviewMouseDown;


            // 响应式布局现在由 FileListControl 内部处理，不再需要此事件
            /*
            if (RootGrid != null)
            {
                RootGrid.SizeChanged += RootGrid_SizeChanged;
            }
            */

            // RightPanel.NotesHeightChanged 已由 EventBridgeService 桥接 → NotesHeightChangedMessage

            if (NavigationPanelControl != null)
            {
                // Library events handled by LibraryEventHandler
                NavigationPanelControl.LibraryManageClick += (s, e) => _viewModel?.ActivePane?.NewLibraryCommand?.Execute(null);


                NavigationPanelControl.PathManageClick += (s, e) =>
                {
                    var window = new YiboFile.Windows.NavigationSettingsWindow("Path");
                    window.Owner = this;
                    window.ShowDialog();
                };

                if (NavigationPanelControl.TagBrowsePanelControl != null)
                {
                    NavigationPanelControl.TagBrowsePanelControl.TagClicked += (tagId, tagName) =>
                    {
                        if (string.IsNullOrEmpty(tagName)) return;
                        _ = _navigationCoordinator?.NavigateAsync(new YiboFile.Models.Navigation.NavigationRequest
                        {
                            Target = YiboFile.Models.Navigation.NavigationTarget.FromTag(tagName),
                            Pane = GetActivePaneId(), // Using helper method from MainWindow
                            Source = NavigationSource.SidebarTag
                        });
                    };
                    NavigationPanelControl.TagBrowsePanelControl.BackRequested += (s, e) =>
                    {
                        // Navigate back when back button is clicked in TagBrowsePanel
                        _viewModel?.Navigation?.NavigateBackCommand?.Execute(null);
                    };
                }
            }

            if (FileBrowser != null)
            {
                // [FIX] 显式绑定路径变更事件，确保主面板导航正确 - 使用统一导航协调器
                FileBrowser.PathChanged += (s, path) => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.AddressBar, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Main);
                FileBrowser.BreadcrumbClicked += (s, path) => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.Breadcrumb, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Main);
            }

            if (SecondFileBrowser != null)
            {
                // [FIX] 显式绑定路径变更事件，确保副面板导航正确 - 使用统一导航协调器
                SecondFileBrowser.PathChanged += (s, path) => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.AddressBar, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second);
                SecondFileBrowser.BreadcrumbClicked += (s, path) => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.Breadcrumb, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second);
            }



            // 初始化主题切换事件
            InitializeThemeEvents();

            // 订阅分割器折叠事件，动态调整标签页边距
            if (SplitterRight != null)
            {
                SplitterRight.CollapsedStateChanged += (s, e) => UpdateTabManagerMargin();
            }
        }

        internal void InitializeServiceEvents()
        {
            // 此处的直接服务事件订阅已迁移至 WindowOrchestrator 的服务桥接逻辑中。
            // 详见 WindowOrchestrator.SetupServiceMessageBridges。

            if (NavigationPanelControl != null)
            {
                // [FIX] Events Handled by Commands and MessageBus
                // NavigationPanelControl.DrivesTreeViewItemClick += DrivesTreeViewItem_Click;
                // NavigationPanelControl.QuickAccessListBoxSelectionChanged += QuickAccessListBox_SelectionChanged;
                // NavigationPanelControl.FavoriteListBoxPreviewMouseDown += OnFavoriteListBoxPreviewMouseDown;
                // NavigationPanelControl.FavoriteListBoxSelectionChanged += OnFavoriteListBoxSelectionChanged;
            }

            this.Activated += OnActivated;
        }

        private void OnActivated(object sender, EventArgs e)
        {
            string currentPath = (IsDualListMode && IsSecondPaneFocused) ? _viewModel?.SecondaryPane?.CurrentPath : _currentPath;
            if (currentPath != null && currentPath.StartsWith("search://"))
            {
                CheckAndRefreshSearchTab(currentPath);
            }
        }

        internal FileOperationContext GetActiveFileOperationContext()
        {
            bool useSecond = _viewModel?.ActivePane == _viewModel?.SecondaryPane;
            var targetBrowser = useSecond ? SecondFileBrowser : FileBrowser;
            var targetPath = useSecond ? _viewModel?.SecondaryPane?.CurrentPath : _currentPath;

            Library targetLibrary = null;
            if (useSecond)
            {
                if (!string.IsNullOrEmpty(targetPath) && targetPath.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
                {
                    var libName = targetPath.Substring(6).Split('/')[0];
                    targetLibrary = _libraryService?.GetAllLibraries()?.FirstOrDefault(l =>
                        string.Equals(l.Name, libName, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                targetLibrary = _currentLibrary;
            }

            return new FileOperationContext
            {
                TargetPath = targetPath,
                CurrentLibrary = targetLibrary,
                OwnerWindow = this,
                RefreshCallback = () =>
                {
                    if (useSecond) RefreshActiveFileList();
                    else RefreshFileList();
                }
            };
        }
    }
}

