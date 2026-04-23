using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;
using YiboFile.Controls.Helpers;
using YiboFile.Interfaces;
using YiboFile.Models;
using YiboFile.Models.Navigation;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Navigation;
using YiboFile.ViewModels;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 文件列表鼠标事件处理器
    /// </summary>
    public class FileListMouseHandler
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly INavigationCoordinator _navigationCoordinator;
        private readonly NavigationModeService _navigationModeService;
        private readonly IShellWindow _shellWindow;
        private readonly PaneId _paneId;
        private readonly Action<FileSystemItem> _handleFileOpen;

        private System.Windows.Point _mouseDownPoint;
        private bool _isMouseDownOnListView = false;
        private bool _isMouseDownOnColumnHeader = false;
        private readonly SlowClickRenameBehavior _slowClickRename;

        public FileListMouseHandler(
            FileBrowserControl fileBrowser,
            INavigationCoordinator navigationCoordinator,
            NavigationModeService navigationModeService,
            IShellWindow shellWindow,
            PaneId paneId,
            Action<FileSystemItem> handleFileOpen)
        {
            _fileBrowser = fileBrowser;
            _navigationCoordinator = navigationCoordinator;
            _navigationModeService = navigationModeService;
            _shellWindow = shellWindow;
            _paneId = paneId;
            _handleFileOpen = handleFileOpen;
            _slowClickRename = new SlowClickRenameBehavior();
        }

        private PaneViewModel CurrentPane => _paneId == PaneId.Main ? _shellWindow.ViewModel.PrimaryPane : _shellWindow.ViewModel.SecondaryPane;

        public void OnPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            if (src != null)
            {
                if (FindAncestor<GridViewColumnHeader>(src) != null ||
                    FindAncestor<System.Windows.Controls.Primitives.Thumb>(src) != null)
                {
                    e.Handled = true;
                    return;
                }
            }
            HandleDoubleClick(e);
            _slowClickRename.OnDoubleClick();
        }

        public void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            if (src != null)
            {
                if (FindAncestor<GridViewColumnHeader>(src) != null ||
                    FindAncestor<System.Windows.Controls.Primitives.Thumb>(src) != null)
                {
                    e.Handled = true;
                    return;
                }
            }
            HandleDoubleClick(e);
            _slowClickRename.OnDoubleClick();
        }

        public void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 最开始就检查是否有任何项正在重命名，如果有，直接返回
            bool isAnyItemRenaming = false;
            foreach (var item in listView.Items)
            {
                if (item is FileSystemItem fsItem && fsItem.IsRenaming)
                {
                    isAnyItemRenaming = true;
                    break;
                }
            }
            
            if (isAnyItemRenaming)
            {
                System.Diagnostics.Debug.WriteLine("[FileListMouseHandler] Any item is renaming, skipping all mouse down handling");
                return;
            }

            if (e.ClickCount == 2)
            {
                var src = e.OriginalSource as DependencyObject;
                if (src != null)
                {
                    var header = FindAncestor<GridViewColumnHeader>(src);
                    var thumb = FindAncestor<System.Windows.Controls.Primitives.Thumb>(src);
                    if (header != null && thumb != null && header.Column != null)
                    {
                        _shellWindow.AutoSizeGridViewColumn(header.Column);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (!listView.IsFocused)
            {
                listView.Focus();
            }

            if (_fileBrowser?.AddressBarControl?.IsEditMode == true)
            {
                _fileBrowser.AddressBarControl.SwitchToBreadcrumbMode();
            }

            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);

            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                int depth = 0;
                while (current != null && current != listView && depth < 10)
                {
                    if (current is GridViewColumnHeader || current.GetType().Name.Contains("Thumb") || current.GetType().Name == "Thumb")
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    var parent = VisualTreeHelper.GetParent(current);
                    if (parent is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    current = parent;
                    depth++;
                }

                if (listView.View is GridView gridView && gridView.Columns.Count > 0)
                {
                    if (hitPoint.Y < 30)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }
                }
            }

            _mouseDownPoint = e.GetPosition(listView);
            _isMouseDownOnListView = true;
            _isMouseDownOnColumnHeader = false;

            // 慢单击重命名：记录 MouseDown 候选
            _slowClickRename.OnMouseDown(listView, e);

            bool isListViewItem = false;
            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is System.Windows.Controls.ListViewItem)
                    {
                        isListViewItem = true;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            if (!isListViewItem && e.ChangedButton == MouseButton.Left)
            {
                bool isMultiSelect = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
                                     (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (!isMultiSelect)
                {
                    listView.SelectedItem = null;
                    listView.SelectedItems.Clear();
                }
            }
        }

        public void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle) return;

            var listView = sender as ListView;
            if (listView == null) return;

            var hitResult = VisualTreeHelper.HitTest(listView, e.GetPosition(listView));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listView)
            {
                if (current is System.Windows.Controls.ListViewItem item)
                {
                    if (item.Content is FileSystemItem selectedItem)
                    {
                        if (selectedItem.IsDirectory)
                        {
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, NavigationCoordinator.GetClickType(e), pane: _paneId);
                            e.Handled = true;
                            return;
                        }
                    }
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        public void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 最开始就检查是否有任何项正在重命名，如果有，直接返回
            bool isAnyItemRenaming = false;
            foreach (var item in listView.Items)
            {
                if (item is FileSystemItem fsItem && fsItem.IsRenaming)
                {
                    isAnyItemRenaming = true;
                    break;
                }
            }
            
            if (isAnyItemRenaming)
            {
                System.Diagnostics.Debug.WriteLine("[FileListMouseHandler] Any item is renaming, skipping all mouse up handling");
                _isMouseDownOnListView = false;
                return;
            }

            if (_isMouseDownOnColumnHeader)
            {
                _isMouseDownOnColumnHeader = false;
                _isMouseDownOnListView = false;
                return;
            }

            if (!_isMouseDownOnListView)
                return;

            var originalSource = e.OriginalSource as DependencyObject;
            DependencyObject checkSource = originalSource;
            while (checkSource != null)
            {
                if (checkSource is GridViewColumnHeader || checkSource.GetType().Name.Contains("Thumb") || checkSource.GetType().Name == "Thumb")
                {
                    _isMouseDownOnListView = false;
                    return;
                }
                checkSource = VisualTreeHelper.GetParent(checkSource);
            }

            System.Windows.Point mouseUpPoint = e.GetPosition(listView);
            double distance = Math.Sqrt(Math.Pow(mouseUpPoint.X - _mouseDownPoint.X, 2) +
                                      Math.Pow(mouseUpPoint.Y - _mouseDownPoint.Y, 2));

            if (distance > SystemParameters.MinimumHorizontalDragDistance || mouseUpPoint.Y < 30)
            {
                _isMouseDownOnListView = false;
                return;
            }

            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);

            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is ListViewItem)
                    {
                        // 慢单击重命名：松手在 ListViewItem 上时启动延迟
                        _slowClickRename.OnMouseUp(listView, e);

                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                current = hitResult.VisualHit;
                while (current != null)
                {
                    if (current is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                if (listView.SelectedItems.Count > 0)
                {
                    listView.SelectedItems.Clear();
                }
            }

            // 慢单击重命名：在空白区点击时也取消
            _slowClickRename.Cancel();

            _isMouseDownOnListView = false;
            _isMouseDownOnColumnHeader = false;
        }

        public void OnPreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;

            var thumbAncestor = FindAncestor<System.Windows.Controls.Primitives.Thumb>(originalSource);
            if (thumbAncestor != null)
            {
                var thumbHeader = FindAncestor<GridViewColumnHeader>(originalSource);
                if (thumbHeader?.Column != null)
                {
                    _shellWindow.AutoSizeGridViewColumn(thumbHeader.Column);
                    e.Handled = true;
                    return;
                }
            }

            if (FindAncestor<GridViewColumnHeader>(originalSource) != null)
            {
                e.Handled = true;
                return;
            }

            if (FindAncestor<ListViewItem>(originalSource) != null) return;

            if (_fileBrowser?.FilesList == null) return;
            var hitResult = VisualTreeHelper.HitTest(_fileBrowser.FilesList, e.GetPosition(_fileBrowser.FilesList));
            if (hitResult != null && FindAncestor<ListViewItem>(hitResult.VisualHit) != null)
                return;

            var currentPath = CurrentPane?.CurrentPath;
            if (!string.IsNullOrEmpty(currentPath))
            {
                if (Directory.Exists(currentPath) || ProtocolManager.IsVirtual(currentPath))
                {
                    if (ProtocolManager.Parse(currentPath).Type == ProtocolType.Archive)
                    {
                        return;
                    }

                    var parentPath = Directory.GetParent(currentPath);
                    if (parentPath != null)
                    {
                        _navigationCoordinator.HandlePathNavigation(parentPath.FullName, NavigationSource.FileList, ClickType.LeftClick, pane: _paneId);
                        e.Handled = true;
                    }
                }
            }
        }

        private void HandleDoubleClick(MouseButtonEventArgs e)
        {
            if (_fileBrowser?.FilesList == null) return;
            var hitResult = VisualTreeHelper.HitTest(_fileBrowser.FilesList, e.GetPosition(_fileBrowser.FilesList));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != _fileBrowser.FilesList)
            {
                if (current is System.Windows.Controls.ListViewItem item)
                {
                    if (item.Content is FileSystemItem selectedItem)
                    {
                        if (selectedItem.Type == "Lib")
                        {
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FileList, NavigationCoordinator.GetClickType(e), pane: _paneId);
                            e.Handled = true;
                            return;
                        }

                        if (selectedItem.IsDirectory)
                        {
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, NavigationCoordinator.GetClickType(e), pane: _paneId);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            _handleFileOpen(selectedItem);
                            e.Handled = true;
                            return;
                        }
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }

            if (_fileBrowser?.FilesSelectedItem is FileSystemItem backupItem)
            {
                if (backupItem.IsDirectory)
                {
                    if (Directory.Exists(backupItem.Path))
                    {
                        if (CurrentPane?.NavigationMode == "Library")
                        {
                            _navigationModeService.SwitchNavigationMode("Path");
                        }
                        _navigationCoordinator.HandlePathNavigation(backupItem.Path, NavigationSource.FileList, ClickType.LeftClick, pane: _paneId);
                        e.Handled = true;
                    }
                }
                else
                {
                    _handleFileOpen(backupItem);
                    e.Handled = true;
                }
            }
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
