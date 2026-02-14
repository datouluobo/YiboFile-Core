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
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.Handlers
{
    /// <summary>
    /// Handles drag and drop logic for MainWindow
    /// </summary>
    public class DragDropEventHandler
    {
        private readonly MainWindow _window;
        private DragDropManager _dragDropManager;
        private DragDropManager _secondDragDropManager;

        public DragDropEventHandler(MainWindow window)
        {
            _window = window;
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
                        if (_window._currentLibrary != null)
                            _window._libraryEventHandler?.LoadLibraryFiles(_window._currentLibrary);
                        else
                            _window.LoadCurrentDirectory();
                    }
                    else
                    {
                        var secondTab = _window._secondTabService?.ActiveTab;
                        if (secondTab != null && !string.IsNullOrEmpty(secondTab.Path) && Directory.Exists(secondTab.Path))
                        {
                            _window.LoadSecondFileBrowserDirectory(secondTab.Path);
                        }
                    }

                    // Also refresh the other panel if in dual mode and it's showing the affected directory
                    if (_window.IsDualListMode)
                    {
                        if (isPrimary && _window.SecondFileBrowser != null)
                        {
                            // Refresh second panel
                            var secondTab = _window._secondTabService?.ActiveTab;
                            if (secondTab != null && !string.IsNullOrEmpty(secondTab.Path) && Directory.Exists(secondTab.Path))
                            {
                                _window.LoadSecondFileBrowserDirectory(secondTab.Path);
                            }
                        }
                        else if (!isPrimary && _window.FileBrowser != null)
                        {
                            // Refresh main panel
                            if (_window._currentLibrary != null)
                                _window._libraryEventHandler?.LoadLibraryFiles(_window._currentLibrary);
                            else
                                _window.LoadCurrentDirectory();
                        }
                    }
                }
                catch { }
            };

            manager.GetCurrentPath = () =>
            {
                if (isPrimary)
                {
                    return _window._currentLibrary == null ? _window._currentPath : null;
                }
                else
                {
                    var secondTab = _window._secondTabService?.ActiveTab;
                    return secondTab?.Path;
                }
            };

            manager.UndoService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.FileOperations.Undo.UndoService)) as YiboFile.Services.FileOperations.Undo.UndoService;
            manager.TaskQueueService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.FileOperations.TaskQueue.TaskQueueService)) as YiboFile.Services.FileOperations.TaskQueue.TaskQueueService;
        }

        private void InitializeTabDragDrop()
        {
            // Find tab container panels and enable drop
            try
            {
                // Main tab panel
                if (_window.TabManager?.TabsPanelControl != null)
                {
                    _window.TabManager.TabsPanelControl.AllowDrop = true;
                    _window.TabManager.TabsPanelControl.Drop += TabPanel_Drop;
                    _window.TabManager.TabsPanelControl.DragOver += TabPanel_DragOver;
                }

                // Second tab panel
                if (_window.SecondTabManager?.TabsPanelControl != null)
                {
                    _window.SecondTabManager.TabsPanelControl.AllowDrop = true;
                    _window.SecondTabManager.TabsPanelControl.Drop += TabPanel_Drop;
                    _window.SecondTabManager.TabsPanelControl.DragOver += TabPanel_DragOver;
                }
            }
            catch (Exception)
            { }
        }

        private Point _lastTabDragPoint;
        private long _lastTabDragTime;
        private Button _lastHoveredTabButton;

        private void TabPanel_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Point currentPos = e.GetPosition(sender as IInputElement);
                long currentTicks = DateTime.Now.Ticks;

                // Throttle HitTest
                if (Math.Abs(currentPos.X - _lastTabDragPoint.X) > 5 ||
                    Math.Abs(currentPos.Y - _lastTabDragPoint.Y) > 5 ||
                    (currentTicks - _lastTabDragTime) > 500000) // 50ms
                {
                    _lastHoveredTabButton = FindTabButtonAtPoint(sender as Panel, currentPos);

                    _lastTabDragPoint = currentPos;
                    _lastTabDragTime = currentTicks;
                }

                if (_lastHoveredTabButton != null)
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

        private void TabPanel_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            // Find which tab button was dropped on
            var tabButton = FindTabButtonAtPoint(sender as Panel, e.GetPosition(sender as IInputElement));
            if (tabButton == null) return;

            // Get the tab's path from button Tag (Tag is PathTab object)
            // Note: In TabManagerControl, buttons tags are likely PathTab objects
            if (tabButton.Tag is YiboFile.Services.Tabs.PathTab tab && !string.IsNullOrEmpty(tab.Path) && Directory.Exists(tab.Path))
            {
                // Determine operation type
                bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                // Perform the operation
                _dragDropManager?.PerformFileOperation(files, tab.Path, isCopy);
            }
        }

        private Button FindTabButtonAtPoint(Panel panel, Point point)
        {
            if (panel == null) return null;

            var hitTest = VisualTreeHelper.HitTest(panel, point);
            var element = hitTest?.VisualHit;

            while (element != null && element != panel)
            {
                if (element is Button button && button.Tag is YiboFile.Services.Tabs.PathTab)
                {
                    return button;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
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
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DragDrop] Path.GetFullPath failed for library path {targetPath}: {ex.Message}");
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
