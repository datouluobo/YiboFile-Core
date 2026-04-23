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
using YiboFile.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.Handlers
{
    public class LibraryEventHandler
    {
        private readonly IShellWindow _shellWindow;
        private readonly LibraryService _libraryService;
        private readonly INavigationCoordinator _navigationCoordinator;
        private readonly NavigationService _navigationService;
        private readonly Services.FileList.FileListService _fileListService;
        private readonly Services.ColumnManagement.ColumnService _columnService;
        private readonly Services.UI.IDialogService _dialogService;
        public LibraryEventHandler(
            IShellWindow shellWindow,
            LibraryService libraryService,
            INavigationCoordinator navigationCoordinator,
            NavigationService navigationService,
            Services.FileList.FileListService fileListService,
            Services.ColumnManagement.ColumnService columnService,
            Services.UI.IDialogService dialogService = null)
        {
            _shellWindow = shellWindow ?? throw new ArgumentNullException(nameof(shellWindow));
            _libraryService = libraryService ?? throw new ArgumentNullException(nameof(libraryService));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));
            _columnService = columnService ?? throw new ArgumentNullException(nameof(columnService));
            _dialogService = dialogService ?? App.ServiceProvider?.GetRequiredService<Services.UI.IDialogService>();
        }

        public void Initialize()
        {
            if (_shellWindow.LibrariesListBox == null) return;

            _shellWindow.LibrariesListBox.PreviewMouseDown += LibrariesListBox_PreviewMouseDown;
            _shellWindow.LibrariesListBox.SelectionChanged += LibrariesListBox_SelectionChanged;
            _shellWindow.LibrariesListBox.ContextMenuOpening += LibrariesListBox_ContextMenuOpening;

            if (_shellWindow.LibraryContextMenu is ContextMenu cm)
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
                    _shellWindow.ClearLegacyFileState();
                }

                // 使用库服务加载文件
                _libraryService.LoadLibraryFiles(library,
                    (path) => YiboFile.DatabaseManager.GetFolderSize(path),
                    (bytes) => _fileListService.FormatFileSize(bytes),
                    targetPane);
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"加载库文件失败: {ex.Message}");
            }
        }

        public void HighlightMatchingLibrary(Library currentLibrary)
        {
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
                    _navigationCoordinator.HandleLibraryNavigation(library, clickType, _shellWindow.GetActivePaneId());
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void LibrariesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_shellWindow.IsInternalUiUpdate) return;

            if (_shellWindow.LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                var currentLib = _shellWindow.ViewModel?.ActivePane?.CurrentLibrary;
                if (selectedLibrary == currentLib) return;

                _navigationCoordinator.HandleLibraryNavigation(selectedLibrary, ClickType.LeftClick, _shellWindow.GetActivePaneId());
            }
            else
            {
                if (_shellWindow.ViewModel?.ActivePane != null)
                {
                    _shellWindow.ViewModel.ActivePane.CurrentLibrary = null;
                }
                ConfigurationService.Instance.Set(c => c.LastLibraryId, 0);
                ConfigurationService.Instance.SaveNow();

                _shellWindow.ClearLegacyFileState();

                // Clear Primary Pane specifically if needed, logic says:
                _shellWindow.ViewModel?.PrimaryPane?.FileList?.UpdateFiles(new List<FileSystemItem>());

                if (_shellWindow.FileBrowser != null)
                {
                    _shellWindow.FileBrowser.AddressText = "";
                }

                _navigationService.ClearItemHighlights();
            }
        }

        private void LibrariesListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_shellWindow.LibraryContextMenu is ContextMenu cm)
            {
                var selectedLibrary = _shellWindow.LibrariesListBox.SelectedItem as Library;
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
            var currentLib = _shellWindow.ViewModel?.ActivePane?.CurrentLibrary;
            if (currentLib != null) LoadLibraryFiles(currentLib);
        }

        public void LibraryOpenInExplorer_Click()
        {
            if (_shellWindow.LibrariesListBox.SelectedItem is Library lib && lib.Paths != null && lib.Paths.Count > 0)
            {
                System.Diagnostics.Process.Start("explorer.exe", lib.Paths[0]);
            }
        }

        public void LibraryRename_Click()
        {
            if (_shellWindow.LibrariesListBox.SelectedItem is Library lib)
            {
                string newName = _dialogService?.ShowInput("请输入新名称:", lib.Name, "重命名库");
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    // [TODO] 实现重命名逻辑（由于这是 EventHandler，可能需要调用 libraryService）
                    _libraryService?.UpdateLibraryName(lib.Id, newName);
                    LoadLibraries();
                }
            }
        }

        public void LibraryDelete_Click()
        {
            if (_shellWindow.LibrariesListBox.SelectedItem is Library lib)
            {
                if (_dialogService?.Confirm($"确定要删除库 \"{lib.Name}\" 吗？", "确认删除") == true)
                {
                    _libraryService?.DeleteLibrary(lib.Id, lib.Name);
                    LoadLibraries();
                }
            }
        }

        public void LibraryManage_Click()
        {
            var messageBus = (YiboFile.ViewModels.Messaging.IMessageBus)App.ServiceProvider?.GetService(typeof(YiboFile.ViewModels.Messaging.IMessageBus));
            messageBus?.Publish(new YiboFile.ViewModels.Messaging.Messages.OpenContentTabMessage("management"));
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
        }

        public void AddFileToLibrary_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _shellWindow.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                _dialogService?.ShowInfo("请先选择要添加到库的文件或文件夹", "提示");
                return;
            }

            Library targetLibrary = null;
            var currentLib = _shellWindow.ViewModel?.ActivePane?.CurrentLibrary;

            if (currentLib != null)
            {
                targetLibrary = currentLib;
            }
            else
            {
                var libraries = _libraryService.LoadLibraries();
                if (libraries.Count == 0)
                {
                    _dialogService?.ShowInfo("当前没有可用的库，请先创建一个库", "提示");
                    return;
                }

                var owner = _shellWindow as Window;
                var dialog = new Window
                {
                    Title = "选择库",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = owner
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
                        _dialogService?.ShowWarning("请选择一个库", "提示");
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
                _dialogService?.ShowError($"添加失败:\n{string.Join("\n", failedItems)}");
            }

            currentLib = _shellWindow.ViewModel?.ActivePane?.CurrentLibrary;
            if (currentLib != null && currentLib.Id == targetLibrary.Id)
            {
                LoadLibraryFiles(currentLib);
            }
        }
    }
}
