using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;
using YiboFile.Interfaces;
using YiboFile.Models;
using YiboFile.Models.Navigation;
using YiboFile.Services.Navigation;
using YiboFile.ViewModels;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 文件列表键盘事件处理器
    /// </summary>
    public class FileListKeyboardHandler
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly NavigationCoordinator _navigationCoordinator;
        private readonly IShellWindow _shellWindow;
        private readonly Action<FileSystemItem> _handleFileOpen;
        private readonly PaneId _paneId;

        public FileListKeyboardHandler(
            FileBrowserControl fileBrowser,
            NavigationCoordinator navigationCoordinator,
            IShellWindow shellWindow,
            PaneId paneId,
            Action<FileSystemItem> handleFileOpen)
        {
            _fileBrowser = fileBrowser;
            _navigationCoordinator = navigationCoordinator;
            _shellWindow = shellWindow;
            _paneId = paneId;
            _handleFileOpen = handleFileOpen;
        }

        private PaneViewModel CurrentPane => _paneId == PaneId.Main ? _shellWindow.ViewModel.PrimaryPane : _shellWindow.ViewModel.SecondaryPane;

        public void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
                return;

            if (_fileBrowser?.FilesSelectedItem is FileSystemItem renamingItem && renamingItem.IsRenaming)
            {
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
            {
                if (listView.Items.Count == 0)
                    return;

                int currentIndex = listView.SelectedIndex;
                var wrapPanel = FindDescendant<WrapPanel>(listView);
                bool isTilesView = listView.View == null && wrapPanel != null;
                int columns = 1;
                if (isTilesView)
                {
                    double itemWidth = wrapPanel.ItemWidth > 0 ? wrapPanel.ItemWidth : (wrapPanel.Children.Count > 0 ? ((FrameworkElement)wrapPanel.Children[0]).ActualWidth : 160);
                    double viewportWidth = wrapPanel.ActualWidth > 0 ? wrapPanel.ActualWidth : listView.ActualWidth;
                    columns = Math.Max(1, (int)Math.Floor(viewportWidth / Math.Max(1, itemWidth)));
                }

                if (e.Key == Key.Down)
                {
                    if (isTilesView)
                    {
                        int next = currentIndex + columns;
                        if (next < listView.Items.Count)
                        {
                            listView.SelectedIndex = next;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = listView.Items.Count - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    if (isTilesView)
                    {
                        int prev = currentIndex - columns;
                        if (prev >= 0)
                        {
                            listView.SelectedIndex = prev;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = 0;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Home)
                {
                    listView.SelectedIndex = 0;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.End)
                {
                    listView.SelectedIndex = listView.Items.Count - 1;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    if (isTilesView)
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        CurrentPane?.Commands?.NavigateBackCommand?.Execute(null);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    if (isTilesView)
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (listView.SelectedItem is FileSystemItem selectedItem && selectedItem.IsDirectory)
                        {
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, ClickType.LeftClick, pane: _paneId);
                        }
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    if (selectedItem.IsRenaming)
                    {
                        return;
                    }

                    if (selectedItem.IsDirectory)
                    {
                        _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, ClickType.LeftClick, pane: _paneId);
                    }
                    else
                    {
                        _handleFileOpen(selectedItem);
                    }
                }
                e.Handled = true;
            }
        }

        private T FindDescendant<T>(DependencyObject d) where T : DependencyObject
        {
            if (d == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(d, i);
                if (child is T t) return t;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
