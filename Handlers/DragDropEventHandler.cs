using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using YiboFile.Services;
using YiboFile.Services.FileOperations;
using YiboFile.Models.UI;
using YiboFile.Models;
using YiboFile.Interfaces;
using YiboFile.Services.Navigation;
using YiboFile.Services.Tabs;

namespace YiboFile.Handlers
{
    /// <summary>
    /// Handles drag and drop logic for MainWindow
    /// </summary>
    public class DragDropEventHandler
    {
        private readonly IShellWindow _window;
        private readonly INavigationCoordinator _navigationCoordinator;
        private readonly LibraryEventHandler _libraryEventHandler;
        private readonly TabService _secondTabService;

        private DragDropManager _dragDropManager;
        private DragDropManager _secondDragDropManager;

        public DragDropEventHandler(
            IShellWindow window,
            INavigationCoordinator navigationCoordinator,
            LibraryEventHandler libraryEventHandler,
            TabService secondTabService)
        {
            _window = window;
            _navigationCoordinator = navigationCoordinator;
            _libraryEventHandler = libraryEventHandler;
            _secondTabService = secondTabService;
        }

        public void Initialize()
        {
            try
            {
                // Initialize DragDropManager for main file list
                _dragDropManager = new DragDropManager();
                SetupDragDropManager(_dragDropManager, isPrimary: true);

                // Enable file list drag and drop for main list
                if (_window.FileBrowser?.FilesList != null)
                {
                    _dragDropManager.InitializeFileListDragDrop(_window.FileBrowser.FilesList);
                }

                // Initialize DragDropManager for second file list (dual mode)
                _secondDragDropManager = new DragDropManager();
                SetupDragDropManager(_secondDragDropManager, isPrimary: false);

                if (_window.SecondFileBrowser?.FilesList != null)
                {
                    _secondDragDropManager.InitializeFileListDragDrop(_window.SecondFileBrowser.FilesList);
                }

                // Initialize tab drop handlers
                InitializeTabDragDrop();

                // Initialize library and navigation panel drag drop
                InitializeNavigationPanelDragDrop();
            }
            catch (Exception)
            { }
        }

        private void SetupDragDropManager(DragDropManager manager, bool isPrimary)
        {
            manager.RequestRefresh = () =>
            {
                try
                {
                    // Refresh the source panel (the panel where drag dropped)
                    if (isPrimary)
                    {
                        if (_window.ViewModel?.ActivePane?.CurrentLibrary != null)
                            _libraryEventHandler?.LoadLibraryFiles(_window.ViewModel.ActivePane.CurrentLibrary);
                        else
                            _navigationCoordinator.HandlePathNavigation(_window.ViewModel?.ActivePane?.CurrentPath, YiboFile.Models.Navigation.NavigationSource.External, YiboFile.Models.Navigation.ClickType.LeftClick);
                    }
                    else
                    {
                        var secondTab = _secondTabService?.ActiveTab;
                        if (secondTab != null && !string.IsNullOrEmpty(secondTab.Path) && Directory.Exists(secondTab.Path))
                        {
                            _window.ViewModel?.SecondaryPane?.NavigateTo(secondTab.Path);
                        }
                    }

                    // Also refresh the other panel if in dual mode and it's showing the affected directory
                    if (_window.IsDualPaneMode)
                    {
                        if (isPrimary && _window.SecondFileBrowser != null)
                        {
                            // Refresh second panel
                            var secondTab = _secondTabService?.ActiveTab;
                            if (secondTab != null && !string.IsNullOrEmpty(secondTab.Path) && Directory.Exists(secondTab.Path))
                            {
                                _window.ViewModel?.SecondaryPane?.NavigateTo(secondTab.Path);
                            }
                        }
                        else if (!isPrimary && _window.FileBrowser != null)
                        {
                            // Refresh main panel
                            if (_window.ViewModel?.ActivePane?.CurrentLibrary != null)
                                _libraryEventHandler?.LoadLibraryFiles(_window.ViewModel.ActivePane.CurrentLibrary);
                            else
                                _navigationCoordinator.HandlePathNavigation(_window.ViewModel?.ActivePane?.CurrentPath, YiboFile.Models.Navigation.NavigationSource.External, YiboFile.Models.Navigation.ClickType.LeftClick);
                        }
                    }
                }
                catch { }
            };

            manager.GetCurrentPath = () =>
            {
                if (isPrimary)
                {
                    return _window.ViewModel?.ActivePane?.CurrentLibrary == null ? _window.ViewModel?.ActivePane?.CurrentPath : null;
                }
                else
                {
                    var secondTab = _secondTabService?.ActiveTab;
                    return secondTab?.Path;
                }
            };

            manager.UndoService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.FileOperations.Undo.UndoService)) as YiboFile.Services.FileOperations.Undo.UndoService;
            manager.TaskQueueService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.FileOperations.TaskQueue.TaskQueueService)) as YiboFile.Services.FileOperations.TaskQueue.TaskQueueService;
        }

        private void InitializeTabDragDrop()
        {
            // 旧的 Tab 拖拽逻辑已废弃，由 TabDragDropBehavior 接管
            // 参考 YiboFile.Handlers.TabDragDropBehavior
        }

        public void InitializeNavigationPanelDragDrop()
        {
            if (_window.NavigationPanelControl == null) return;

            // Helper to attach events
            void AttachDragDrop(UIElement element)
            {
                if (element == null) return;
                element.AllowDrop = true;
                element.DragOver += NavigationItem_DragOver;
                element.Drop += NavigationItem_Drop;
            }

            AttachDragDrop(_window.NavigationPanelControl.LibrariesListBoxControl);
            AttachDragDrop(_window.NavigationPanelControl.QuickAccessListBoxControl);
            AttachDragDrop(_window.NavigationPanelControl.FolderFavoritesListBoxControl);
            AttachDragDrop(_window.NavigationPanelControl.DrivesTreeViewControl);
        }

        private void NavigationItem_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var targetPath = GetPathFromDragTarget(sender as FrameworkElement, e.GetPosition(sender as IInputElement));

                // Only allow drop if we found a valid target path that is a directory
                if (!string.IsNullOrEmpty(targetPath) && (Directory.Exists(targetPath) || targetPath.StartsWith("lib://")))
                {
                    e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            }
        }

        private void NavigationItem_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            var targetPath = GetPathFromDragTarget(sender as FrameworkElement, e.GetPosition(sender as IInputElement));

            if (!string.IsNullOrEmpty(targetPath))
            {
                if (targetPath.StartsWith("lib://"))
                {
                    var libName = targetPath.Substring(6);
                    Library lib = null;

                    var libs = _window.LibrariesListBox?.ItemsSource as IEnumerable<Library>;
                    lib = libs?.FirstOrDefault(l => l.Name == libName);

                    if (lib != null && lib.Paths != null && lib.Paths.Count > 0)
                    {
                        // 确保使用绝对路径作为拖拽目标
                        targetPath = lib.Paths[0];
                        try
                        {
                            targetPath = Path.GetFullPath(targetPath);
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else
                    {
                        return; // Cannot drop on empty library or invalid lib path
                    }
                }

                if (Directory.Exists(targetPath))
                {
                    bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                    _dragDropManager?.PerformFileOperation(files, targetPath, isCopy);
                }
            }
        }

        private string GetPathFromDragTarget(FrameworkElement container, Point point)
        {
            if (container == null) return null;

            var hitTest = VisualTreeHelper.HitTest(container, point);
            var element = hitTest?.VisualHit;

            while (element != null && element != container)
            {
                if (element is FrameworkElement fe && fe.DataContext != null)
                {
                    // Check for common Path properties
                    var dc = fe.DataContext;

                    // Library
                    if (dc is Library lib) return $"lib://{lib.Name}";

                    // QuickAccess / Drives (NavigationItem / NavigationItemViewModel)
                    // Use reflection or dynamic to be safe
                    var type = dc.GetType();
                    var pathProp = type.GetProperty("Path");
                    if (pathProp != null)
                    {
                        var p = pathProp.GetValue(dc) as string;
                        if (!string.IsNullOrEmpty(p)) return p;
                    }

                    // Favorite
                    if (dc is Favorite fav) return fav.Path;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }
    }
}
