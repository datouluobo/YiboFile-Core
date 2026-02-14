using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Models;
using YiboFile.Services;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.Models.Navigation;

namespace YiboFile.Handlers
{
    public class LibraryEventHandler
    {
        private readonly MainWindow _window;
        private readonly LibraryService _libraryService;
        private readonly NavigationCoordinator _navigationCoordinator;
        private readonly NavigationService _navigationService;
        private readonly Services.FileList.FileListService _fileListService;
        private readonly Services.ColumnManagement.ColumnService _columnService;

        public LibraryEventHandler(
            MainWindow window,
            LibraryService libraryService,
            NavigationCoordinator navigationCoordinator,
            NavigationService navigationService,
            Services.FileList.FileListService fileListService,
            Services.ColumnManagement.ColumnService columnService)
        {
            _window = window;
            _libraryService = libraryService;
            _navigationCoordinator = navigationCoordinator;
            _navigationService = navigationService;
            _fileListService = fileListService;
            _columnService = columnService;
        }

        public void Initialize()
        {
            if (_window.LibrariesListBox == null) return;

            _window.LibrariesListBox.PreviewMouseDown += LibrariesListBox_PreviewMouseDown;
            _window.LibrariesListBox.SelectionChanged += LibrariesListBox_SelectionChanged; // Needs fix for _isInternalUpdate
            _window.LibrariesListBox.ContextMenuOpening += LibrariesListBox_ContextMenuOpening;

            // Context Menu Click Handling
            // LibrariesListBox usually has a context menu assigned in XAML or dynamically.
            // Assuming context menu events are handled here via attached logic or direct subscription if accessible.
            if (_window.NavigationPanelControl?.LibraryContextMenuControl is ContextMenu cm)
            {
                foreach (var item in cm.Items.OfType<MenuItem>())
                {
                    item.Click += LibraryContextMenu_Click;
                }
            }
        }

        /// <summary>
        /// 加载所有库
        /// </summary>
        public void LoadLibraries()
        {
            _libraryService.LoadLibraries();
        }

        /// <summary>
        /// 加载库文件
        /// </summary>
        public void LoadLibraryFiles(Library library, PaneId targetPane = PaneId.Main)
        {
            try
            {
                if (targetPane == PaneId.Main)
                {
                    _window._currentFiles.Clear();
                    _window._currentPath = null; // 标记当前在库模式下
                    if (_window.FileBrowser != null)
                    {
                        _window.FileBrowser.NavUpEnabled = false;
                        _window.FileBrowser.SetSearchStatus(false);
                    }
                }

                // 使用库服务加载文件
                _libraryService.LoadLibraryFiles(library,
                    (path) => YiboFile.DatabaseManager.GetFolderSize(path),
                    (bytes) => _fileListService.FormatFileSize(bytes),
                    targetPane);
            }
            catch (Exception ex)
            {
                DialogService.Error($"加载库文件失败: {ex.Message}", owner: _window);
            }
        }

        public void HighlightMatchingLibrary(Library currentLibrary)
        {
            // Delegate directly to NavigationService as per original logic
            _navigationService?.HighlightMatchingLibrary(currentLibrary);
        }

        // --- Event Handlers ---

        private void LibrariesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var hitResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext is Library library)
                {
                    e.Handled = true;
                    _navigationCoordinator.HandleLibraryNavigation(library, clickType, _window.GetActivePaneId());
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void LibrariesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Internal update flag needs to be accessible or managed.
            // Assuming _isInternalUpdate is exposed or we can check transient state.
            // For now, let's access it via internal if possible, otherwise rely on logic.
            // _window._isInternalUpdate is internal.
            if (_window._isInternalUpdate) return;

            if (_window.LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                if (selectedLibrary == _window._currentLibrary) return;

                _navigationCoordinator.HandleLibraryNavigation(selectedLibrary, ClickType.LeftClick, _window.GetActivePaneId());
            }
            else
            {
                _window._currentLibrary = null;
                ConfigurationService.Instance.Set(c => c.LastLibraryId, 0);
                ConfigurationService.Instance.SaveNow();

                _window._currentFiles.Clear();
                if (_window.FileBrowser != null)
                {
                    _window._viewModel?.PrimaryPane?.FileList?.UpdateFiles(new List<FileSystemItem>());
                    _window.FileBrowser.AddressText = "";
                }

                _navigationService.ClearItemHighlights();
            }
        }

        private void LibrariesListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_window.NavigationPanelControl?.LibraryContextMenuControl is ContextMenu cm)
            {
                var selectedLibrary = _window.LibrariesListBox.SelectedItem as Library;
                bool hasSelection = selectedLibrary != null;

                SetLibraryMenuItemVisibility(cm, "LibraryRefreshItem", !hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryOpenInExplorerItem", hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryRenameItem", hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryDeleteItem", hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryManageItem", true);
            }
        }

        private void SetLibraryMenuItemVisibility(ContextMenu cm, string name, bool visible)
        {
            var item = cm.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == name);
            if (item != null) item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetLibraryMenuItemAvailability(ContextMenu cm, string name, bool enabled)
        {
            var item = cm.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == name);
            if (item != null) item.IsEnabled = enabled;
        }

        public void LibraryContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                switch (menuItem.Name)
                {
                    case "LibraryRefreshItem":
                        LibraryRefresh_Click();
                        break;
                    case "LibraryOpenInExplorerItem":
                        LibraryOpenInExplorer_Click();
                        break;
                    case "LibraryRenameItem":
                        LibraryRename_Click();
                        break;
                    case "LibraryDeleteItem":
                        LibraryDelete_Click();
                        break;
                    case "LibraryManageItem":
                        LibraryManage_Click();
                        break;
                }
            }
        }

        public void LibraryRefresh_Click()
        {
            _libraryService?.LoadLibraries();
            if (_window._currentLibrary != null) LoadLibraryFiles(_window._currentLibrary);
        }

        public void LibraryOpenInExplorer_Click()
        {
            if (_window.LibrariesListBox.SelectedItem is Library lib && lib.Paths != null && lib.Paths.Count > 0)
            {
                System.Diagnostics.Process.Start("explorer.exe", lib.Paths[0]);
            }
        }

        public void LibraryRename_Click()
        {
            if (_window.LibrariesListBox.SelectedItem is Library lib)
            {
                var dialog = new YiboFile.Controls.Dialogs.InputDialog("重命名库", "请输入新名称:", lib.Name);
                dialog.Owner = _window;
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    DialogService.Info("库重命名功能待实现", owner: _window);
                }
            }
        }

        public void LibraryDelete_Click()
        {
            if (_window.LibrariesListBox.SelectedItem is Library lib)
            {
                if (DialogService.Ask($"确定要删除库 \"{lib.Name}\" 吗？", "确认删除", _window))
                {
                    _libraryService?.DeleteLibrary(lib.Id, lib.Name);
                    LoadLibraries();
                }
            }
        }

        public void LibraryManage_Click()
        {
            DialogService.Info("库管理功能待完善", owner: _window);
        }

        public void ImportLibrary_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _libraryService.ImportLibrary(dialog.SelectedPath);
                    _libraryService.LoadLibraries();
                }
            }
        }

        public void ExportLibrary_Click(object sender, RoutedEventArgs e)
        {
            DialogService.Info("导出库功能待实现", owner: _window);
        }

        public void AddFileToLibrary_Click(object sender, RoutedEventArgs e)
        {
            // Logic copied from MainWindow.Library.cs
            var selectedItems = _window.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要添加到库的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Library targetLibrary = null;

            if (_window._currentLibrary != null)
            {
                targetLibrary = _window._currentLibrary;
            }
            else
            {
                var libraries = _libraryService.LoadLibraries();
                if (libraries.Count == 0)
                {
                    MessageBox.Show("当前没有可用的库，请先创建一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Create Dialog (Simplified for brevity, assuming existing logic works)
                // Re-implementing dialog logic here or extracting to a helper/service would be better.
                // For now, replicating the inline dialog creation logic.

                var dialog = new Window
                {
                    Title = "选择库",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = _window
                };

                var listBox = new ListBox
                {
                    DisplayMemberPath = "Name",
                    Margin = new Thickness(10),
                    ItemsSource = libraries
                };

                var okButton = new Button
                {
                    Content = "确定",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Width = 80,
                    Height = 30,
                    IsCancel = true
                };

                okButton.Click += (s, args) =>
                {
                    if (listBox.SelectedItem is Library selectedLib)
                    {
                        targetLibrary = selectedLib;
                        dialog.DialogResult = true;
                        dialog.Close();
                    }
                    else
                    {
                        MessageBox.Show("请选择一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };

                cancelButton.Click += (s, args) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };

                var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
                var label = new Label { Content = "请选择要添加到的库:", Margin = new Thickness(10, 10, 10, 5) };
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(listBox);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;

                if (dialog.ShowDialog() != true) return;
            }

            if (targetLibrary == null) return;

            int successCount = 0;
            int failCount = 0;
            var failedItems = new List<string>();

            foreach (var item in selectedItems)
            {
                try
                {
                    string pathToAdd = item.IsDirectory ? item.Path : System.IO.Path.GetDirectoryName(item.Path);
                    var existingPaths = _libraryService.GetLibraryPaths(targetLibrary.Id);
                    if (!existingPaths.Any(p => p.Path.Equals(pathToAdd, StringComparison.OrdinalIgnoreCase)))
                    {
                        _libraryService.AddLibraryPath(targetLibrary.Id, pathToAdd);
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        failedItems.Add($"{item.Name} (已存在于库中)");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    failedItems.Add($"{item.Name} ({ex.Message})");
                }
            }

            if (failCount > 0 && successCount == 0)
            {
                MessageBox.Show($"添加失败:\n{string.Join("\\n", failedItems)}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (_window._currentLibrary != null && _window._currentLibrary.Id == targetLibrary.Id)
            {
                LoadLibraryFiles(_window._currentLibrary);
            }
        }
    }
}
